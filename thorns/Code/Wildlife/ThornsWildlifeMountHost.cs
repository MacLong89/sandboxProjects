using System;

namespace Sandbox;

/// <summary>Host-only mount attach/dismount helpers (wildlife rider + pawn parenting).</summary>
public static class ThornsWildlifeMountHost
{
	public static bool HostTryResolvePawnRootForConnection( Scene scene, Guid connectionId, out GameObject pawnRoot )
	{
		pawnRoot = default;
		if ( scene is null || !scene.IsValid() || connectionId == Guid.Empty )
			return false;

		foreach ( var pawn in scene.GetAllComponents<ThornsPawn>() )
		{
			if ( !pawn.IsValid() )
				continue;
			var go = pawn.GameObject;
			if ( !go.IsValid() || go.Network.OwnerId != connectionId )
				continue;
			pawnRoot = go;
			return true;
		}

		return false;
	}

	public static void HostDismountConnectionFromAnyWildlife( Scene scene, Guid connectionId )
	{
		if ( !Networking.IsHost || scene is null || !scene.IsValid() || connectionId == Guid.Empty )
			return;

		if ( ThornsWildlifeTameRegistry.TryGetMountedByRider( connectionId, out var wid ) && wid.IsValid() )
			HostDismountRiderFromWildlife( wid );
	}

	public static void HostDismountPawnIfMounted( GameObject riderPawnRoot )
	{
		if ( !Networking.IsHost || riderPawnRoot is null || !riderPawnRoot.IsValid() )
			return;

		var ix = riderPawnRoot.Components.Get<ThornsWildlifeMountInteractor>();
		if ( !ix.IsValid() )
			return;

		var mountId = ix.MountedWildlifeId;
		if ( mountId == Guid.Empty )
			return;

		if ( ThornsWildlifeIdentity.ActiveByHost.TryGetValue( mountId, out var wid ) && wid.IsValid() )
			HostDismountRiderFromWildlife( wid );
		else
		{
			ix.MountedWildlifeIdSync = "";
			HostRestorePawnPhysicsAfterDismount( riderPawnRoot, null );
		}
	}

	public static void HostDismountRiderFromWildlife( ThornsWildlifeIdentity wid )
	{
		if ( !Networking.IsHost || wid is null || !wid.IsValid() )
			return;

		var riderGuid = wid.TameRiderConnectionId;
		var tameGo = wid.GameObject;
		var wildId = wid.WildlifeId;

		wid.TameRiderConnectionIdSync = "";
		wid.HostMountSteerPlanar = Vector3.Zero;
		wid.HostLastMountSteerReceiveTime = 0.0;
		ThornsWildlifeTameRegistry.RefreshRiderIndex( wid );

		var scene = wid.GameObject.Scene;
		if ( !scene.IsValid() )
			return;

		if ( riderGuid != Guid.Empty )
		{
			foreach ( var pawn in scene.GetAllComponents<ThornsPawn>() )
			{
				if ( !pawn.IsValid() )
					continue;
				if ( pawn.GameObject.Network.OwnerId != riderGuid )
					continue;

				var ix = pawn.GameObject.Components.Get<ThornsWildlifeMountInteractor>();
				if ( ix.IsValid() && ix.MountedWildlifeId == wildId )
					ix.MountedWildlifeIdSync = "";

				HostRestorePawnPhysicsAfterDismount( pawn.GameObject, tameGo );
				return;
			}
		}

		// Fallback: owner id drift / ordering — any pawn physically parented under this wildlife root.
		foreach ( var pawn in scene.GetAllComponents<ThornsPawn>() )
		{
			if ( !pawn.IsValid() )
				continue;
			if ( !HostPawnTransformUnderWildlifeRoot( pawn.GameObject, tameGo ) )
				continue;

			var ix = pawn.GameObject.Components.Get<ThornsWildlifeMountInteractor>();
			if ( ix.IsValid() && ix.MountedWildlifeId == wildId )
				ix.MountedWildlifeIdSync = "";

			HostRestorePawnPhysicsAfterDismount( pawn.GameObject, tameGo );
			return;
		}
	}

	/// <summary>
	/// Host: if a pawn is parented under a tame hierarchy but is not in a valid ride state (non-mountable species,
	/// tame shows no rider, or owner mismatch), detach and re-enable CC. Fixes stuck "riding" without <see cref="ThornsWildlifeMountInteractor.MountedWildlifeId"/>.
	/// </summary>
	public static void HostUnstickPawnOrphanedWildlifeParent( GameObject riderRoot )
	{
		if ( !Networking.IsHost || riderRoot is null || !riderRoot.IsValid() )
			return;

		if ( !HostTryFindTamedWildlifeIdentityInParentChain( riderRoot, out var wid ) || !wid.IsValid() )
			return;

		var ownerOk = wid.TameRiderConnectionId == riderRoot.Network.OwnerId;
		var validRide = wid.Definition.AllowPlayerMount
		                && wid.TameRiderConnectionId != Guid.Empty
		                && ownerOk;

		if ( validRide )
			return;

		var ix = riderRoot.Components.Get<ThornsWildlifeMountInteractor>();
		if ( ownerOk )
			wid.TameRiderConnectionIdSync = "";
		if ( ix.IsValid() && ix.MountedWildlifeId == wid.WildlifeId )
			ix.MountedWildlifeIdSync = "";

		HostRestorePawnPhysicsAfterDismount( riderRoot, wid.GameObject );
	}

	/// <summary>
	/// Host: tamed creatures must not be parented under a pawn. If they are (replication / hierarchy edge),
	/// their world position tracks the player and reads as teleported &quot;into&quot; the owner. Detach and place beside the pawn.
	/// </summary>
	public static void HostDetachTameWildlifeIfUnderPawnHierarchy( ThornsWildlifeIdentity wid )
	{
		if ( !Networking.IsHost || wid is null || !wid.IsValid() || !wid.HostIsTamed )
			return;

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
			return;

		GameObject pawnAncestor = default;
		for ( var p = tameGo.Parent; p.IsValid(); p = p.Parent )
		{
			var pawn = p.Components.Get<ThornsPawn>();
			if ( pawn is { IsValid: true } )
			{
				pawnAncestor = pawn.GameObject;
				break;
			}
		}

		if ( !pawnAncestor.IsValid() )
			return;

		ThornsWildlifeMountDebug.Write(
			$"HostDetachTameWildlifeIfUnderPawnHierarchy: tame={tameGo.Name} wildlifeId={wid.WildlifeId} was under pawn={pawnAncestor.Name}" );

		tameGo.SetParent( null );

		var motor = tameGo.Components.Get<ThornsWildlifeMotor>();
		if ( !motor.IsValid() )
			return;

		var nudge = ThornsWildlifeBrain.HostTameUsesBulkyFollowSpacing( wid.Species ) ? 118f : 92f;
		var pawnPos = pawnAncestor.WorldPosition;
		var tameFlat = tameGo.WorldPosition.WithZ( 0f );
		var pawnFlat = pawnPos.WithZ( 0f );
		var away = tameFlat - pawnFlat;
		if ( away.LengthSquared < 1f )
		{
			var right = pawnAncestor.WorldRotation.Right.WithZ( 0f );
			if ( right.LengthSquared > 1e-4f )
				away = right.Normal;
			else
				away = Vector3.Right;
		}
		else
			away = away.Normal;

		var dest = pawnPos + away * nudge + Vector3.Up * 8f;
		motor.HostTeleportToWorldPosition( dest );
	}

	static bool HostPawnTransformUnderWildlifeRoot( GameObject pawnGo, GameObject wildlifeRoot )
	{
		for ( var p = pawnGo.Parent; p.IsValid(); p = p.Parent )
		{
			if ( p == wildlifeRoot )
				return true;
		}

		return false;
	}

	static bool HostTryFindTamedWildlifeIdentityInParentChain( GameObject riderRoot, out ThornsWildlifeIdentity wid )
	{
		wid = null;
		for ( var p = riderRoot.Parent; p.IsValid(); p = p.Parent )
		{
			var id = p.Components.Get<ThornsWildlifeIdentity>();
			if ( id is { IsValid: true, HostIsTamed: true } )
			{
				wid = id;
				return true;
			}
		}

		return false;
	}

	public static void HostApplyMount( ThornsWildlifeIdentity wid, GameObject riderPawnRoot )
	{
		if ( !Networking.IsHost || wid is null || !wid.IsValid() || riderPawnRoot is null || !riderPawnRoot.IsValid() )
		{
			ThornsWildlifeMountDebug.Write(
				$"HostApplyMount aborted: isHost={Networking.IsHost} widOk={wid is not null && wid.IsValid} riderOk={riderPawnRoot is not null && riderPawnRoot.IsValid}" );
			return;
		}

		if ( !wid.Definition.AllowPlayerMount )
		{
			ThornsWildlifeMountDebug.Write( "HostApplyMount aborted: species not mountable" );
			return;
		}

		HostDismountPawnIfMounted( riderPawnRoot );

		if ( !string.IsNullOrEmpty( wid.TameRiderConnectionIdSync ) )
			HostDismountRiderFromWildlife( wid );

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
		{
			ThornsWildlifeMountDebug.Write( "HostApplyMount aborted: invalid tame GameObject" );
			return;
		}

		var def = wid.Definition;
		var seatLocal = ThornsWildlifeMountRules.ComputeMountRiderSeatLocalOffset( wid, def );

		wid.TameRiderConnectionIdSync = riderPawnRoot.Network.OwnerId.ToString( "D" );
		wid.HostMountSteerPlanar = Vector3.Zero;
		wid.HostLastMountSteerReceiveTime = Time.Now;
		ThornsWildlifeTameRegistry.RefreshRiderIndex( wid );

		var mountIx = riderPawnRoot.Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() )
			mountIx.MountedWildlifeIdSync = wid.WildlifeId.ToString( "D" );

		LocalSyncRiderMountPresentation( riderPawnRoot, wid, seatLocal );

		ThornsWildlifeMountDebug.Write(
			$"HostApplyMount OK tame={tameGo.Name} wildlifeId={wid.WildlifeId} rider={riderPawnRoot.Name} ownerConn={riderPawnRoot.Network.OwnerId} parented={riderPawnRoot.Parent == tameGo} pcEnabled={riderPawnRoot.Components.Get<PlayerController>() is { IsValid: true, Enabled: false }}" );
	}

	/// <summary>Parent rider to tame, snap to seat, disable walking physics (host + local owner presentation).</summary>
	public static void LocalSyncRiderMountPresentation(
		GameObject riderPawnRoot,
		ThornsWildlifeIdentity wid,
		Vector3? seatLocalOverride = null )
	{
		if ( riderPawnRoot is null || !riderPawnRoot.IsValid() || wid is null || !wid.IsValid() )
			return;

		var tameGo = wid.GameObject;
		if ( !tameGo.IsValid() )
			return;

		var seatLocal = seatLocalOverride ?? ThornsWildlifeMountRules.ComputeMountRiderSeatLocalOffset( wid, wid.Definition );

		if ( riderPawnRoot.Parent != tameGo )
		{
			riderPawnRoot.SetParent( tameGo, false );
			riderPawnRoot.LocalScale = Vector3.One;
		}

		riderPawnRoot.LocalRotation = Rotation.Identity;
		riderPawnRoot.LocalPosition = seatLocal;

		var pc = riderPawnRoot.Components.Get<PlayerController>();
		if ( pc.IsValid() )
		{
			pc.WishVelocity = Vector3.Zero;
			if ( pc.Body.IsValid() )
				pc.Body.Velocity = Vector3.Zero;
			pc.Enabled = false;
		}
	}

	/// <summary>Local owner: restore pawn physics when mount sync clears or presentation drifts.</summary>
	public static void LocalEnsureRiderDetachedFromMount( GameObject riderPawnRoot )
	{
		if ( riderPawnRoot is null || !riderPawnRoot.IsValid() )
			return;

		if ( HostTryFindTamedWildlifeIdentityInParentChain( riderPawnRoot, out var wid ) && wid.IsValid() )
			HostRestorePawnPhysicsAfterDismount( riderPawnRoot, wid.GameObject );
		else
		{
			var pc = riderPawnRoot.Components.Get<PlayerController>();
			if ( pc.IsValid() && !pc.Enabled )
			{
				pc.WishVelocity = Vector3.Zero;
				if ( pc.Body.IsValid() )
					pc.Body.Velocity = Vector3.Zero;
				pc.Enabled = true;
				riderPawnRoot.Components.Get<ThornsPawnMovement>()?.LocalResetAfterMountDismount();
			}
		}
	}

	static void HostRestorePawnPhysicsAfterDismount( GameObject riderPawnRoot, GameObject tameGo )
	{
		if ( riderPawnRoot is null || !riderPawnRoot.IsValid() )
			return;

		riderPawnRoot.SetParent( null );
		riderPawnRoot.LocalScale = Vector3.One;
		riderPawnRoot.LocalRotation = Rotation.Identity;

		foreach ( var ch in riderPawnRoot.Children )
		{
			if ( !ch.IsValid() )
				continue;
			if ( ch.Name != "View" && ch.Name != "Body" )
				continue;
			ch.LocalScale = Vector3.One;
			ch.LocalRotation = Rotation.Identity;
			if ( ch.Name == "View" )
				ch.LocalPosition = Vector3.Zero;
		}

		if ( tameGo is not null && tameGo.IsValid() )
		{
			var side = tameGo.WorldRotation.Right.WithZ( 0f );
			if ( side.LengthSquared < 1e-4f )
				side = Vector3.Right;
			else
				side = side.Normal;
			var dismountScale = ThornsWildlifeMountRules.MountSeatScaleForWildlife( tameGo.Components.Get<ThornsWildlifeIdentity>() );
			riderPawnRoot.WorldPosition = tameGo.WorldPosition + side * ( 72f * dismountScale ) + Vector3.Up * ( 12f * dismountScale );
		}

		var ww = FindDescendantNamed( riderPawnRoot, ThornsWeapon.WorldVisualChildName );
		if ( ww.IsValid() )
			ThornsWeapon.ParentWorldWeaponToCitizenRig( riderPawnRoot, ww );

		var pc = riderPawnRoot.Components.Get<PlayerController>();
		if ( pc.IsValid() )
		{
			pc.WishVelocity = Vector3.Zero;
			if ( pc.Body.IsValid() )
				pc.Body.Velocity = Vector3.Zero;
			pc.Enabled = true;
		}

		var mv = riderPawnRoot.Components.Get<ThornsPawnMovement>();
		if ( mv.IsValid() )
			mv.HostResetCapsuleAfterMountDismount();
	}

	static GameObject FindDescendantNamed( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( !c.IsValid() )
				continue;
			if ( c.Name == name )
				return c;
			var nested = FindDescendantNamed( c, name );
			if ( nested.IsValid() )
				return nested;
		}

		return default;
	}
}
