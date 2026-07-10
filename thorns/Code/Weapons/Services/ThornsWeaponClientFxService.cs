#nullable disable

using System;

namespace Sandbox;

public sealed class ThornsWeaponClientFxService
{
	readonly IThornsWeaponFxHost _host;
	readonly ThornsWeaponObserverSyncService _observerSync;

	public ThornsWeaponClientFxService( IThornsWeaponFxHost host, ThornsWeaponObserverSyncService observerSync )
	{
		_host = host;
		_observerSync = observerSync;
	}

	public void PlayLocalFireFeedback()
	{
		// Optional prediction FX — gun shot audio plays only after RpcFireOutcome (ammo spent) so dry-fire matches reality.
	}

	public void SendRpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool hitMarkerHighlight,
		int fpAttackPresentationKind,
		float clientKickPitch,
		float clientKickYaw,
		bool feedbackHasEndpoint,
		Vector3? feedbackHitWorld,
		ThornsWeaponImpactSurfaceKind feedbackSurface,
		bool feedbackTargetKilled )
	{
		var (kmP, kmY) = PackKickMilliDegrees(
			fpAttackPresentationKind == 0 ? clientKickPitch : 0f,
			fpAttackPresentationKind == 0 ? clientKickYaw : 0f );
		var hx = 0;
		var hy = 0;
		var hz = 0;
		if ( feedbackHasEndpoint && feedbackHitWorld.HasValue )
			ThornsWeaponCombatFeedback.PackHitMm( feedbackHitWorld.Value, out hx, out hy, out hz );

		_host.RpcFireOutcome(
			ammunitionExpended,
			damageAppliedToTarget,
			damageDealt,
			hitMarkerHighlight,
			fpAttackPresentationKind,
			kmP,
			kmY,
			feedbackHasEndpoint,
			hx,
			hy,
			hz,
			(int)feedbackSurface,
			feedbackTargetKilled );

		if ( Networking.IsHost
		     && ammunitionExpended
		     && fpAttackPresentationKind == 0
		     && _host.HostTryResolveMirrorGunFireSoundPath( out var observerGunPath ) )
		{
			_host.HostMaybeBroadcastObserverGunshot( _host.GameObject.Network.OwnerId, observerGunPath );
		}
	}

	/// <summary>
	/// Pack visual kick for <see cref="ThornsWeapon.RpcFireOutcome"/> — ints avoid flaky trailing-float Rpc deserialization on some builds.
	/// </summary>
	static (int pitchMilli, int yawMilli) PackKickMilliDegrees( float pitchDeg, float yawDeg ) =>
		((int)MathF.Round( pitchDeg * 1000f ), (int)MathF.Round( yawDeg * 1000f ));

	public void RpcFireOutcome(
		bool ammunitionExpended,
		bool damageAppliedToTarget,
		float damageDealt,
		bool hitMarkerHighlight,
		int fpAttackPresentationKind,
		int clientKickPitchMilliDegrees,
		int clientKickYawMilliDegrees,
		bool feedbackHasEndpoint,
		int feedbackHitXMm,
		int feedbackHitYMm,
		int feedbackHitZMm,
		int feedbackSurfaceKind,
		bool feedbackTargetKilled )
	{
		var clientKickPitchDegrees = clientKickPitchMilliDegrees / 1000f;
		var clientKickYawDegrees = clientKickYawMilliDegrees / 1000f;

		if ( ammunitionExpended )
		{
			var cidSwingFire = (_host.OwnerMirrorCombatWeaponDefinitionId ?? "").Trim();
			var isToolLightMelee = fpAttackPresentationKind == 1
			                       && ThornsToolMeleeCombat.IsToolMeleeCombatId( cidSwingFire );
			var skipPredictedToolStrikeFx = isToolLightMelee
			                                && ThornsToolMeleeCombat.ClientPrimaryStrikePresentationAlreadyPlayed();

			if ( isToolLightMelee )
				ThornsToolMeleeCombat.ClientSyncPrimaryStrikeCadenceFromAuthoritative();

			PlayOwnerFireOrMeleeOutcomeSound(
				fpAttackPresentationKind,
				damageAppliedToTarget,
				feedbackHasEndpoint,
				(ThornsWeaponImpactSurfaceKind)feedbackSurfaceKind );

			if ( !skipPredictedToolStrikeFx && fpAttackPresentationKind is 1 or 2 )
			{
				var cidSwing = (_host.OwnerMirrorCombatWeaponDefinitionId ?? "").Trim();
				if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cidSwing ) )
					ThornsViewModelController.TryTriggerHarvestToolSwingForLocalOwner( _host.GameObject );
			}

			if ( feedbackHasEndpoint
			     && ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var traceStart, out _ ) )
			{
				var endWorld = ThornsWeaponCombatFeedback.UnpackHitMm( feedbackHitXMm, feedbackHitYMm, feedbackHitZMm );
				var surf = (ThornsWeaponImpactSurfaceKind)feedbackSurfaceKind;

				if ( fpAttackPresentationKind == 0 )
					ThornsWeaponCombatFeedback.SpawnGunTracerAndImpactLocal( traceStart, endWorld, surf, damageAppliedToTarget );
				else if ( fpAttackPresentationKind is 1 or 2 )
					ThornsWeaponCombatFeedback.SpawnImpactOnlyLocal( endWorld, surf );
			}

			if ( damageAppliedToTarget )
			{
				var hudFx = _host.GameObject.Components.Get<ThornsDebugHudHost>();
				hudFx?.NotifyLocalWeaponHitFeedback( damageDealt, hitMarkerHighlight, feedbackTargetKilled );

				OwnerPlayHitMarkerSound( hitMarkerHighlight );

				if ( feedbackTargetKilled && !string.IsNullOrWhiteSpace( ThornsWeapon.KillConfirmSoundResource ) )
					Sound.Play( ThornsWeapon.KillConfirmSoundResource, _host.GameObject.WorldPosition );

			}

			var fp = _host.ResolveLocalFpAnimator();
			if ( fp.IsValid() )
			{
				if ( fpAttackPresentationKind == 1 && !skipPredictedToolStrikeFx )
				{
					var lightMul = 1f;
					var cid = (_host.OwnerMirrorCombatWeaponDefinitionId ?? "").Trim();
					if ( string.Equals( cid, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
						lightMul = 0.5f;
					fp.OwnerNotifyMeleeAttackCommitted( heavy: false, lightAttackPresentationDurationMultiplier: lightMul );
				}
				else if ( fpAttackPresentationKind == 2 )
					fp.OwnerNotifyMeleeAttackCommitted( heavy: true );
				else if ( fpAttackPresentationKind == 0 )
					fp.OwnerNotifyServerConfirmedFire();
			}

			if ( fpAttackPresentationKind == 0
			     && (clientKickPitchMilliDegrees != 0 || clientKickYawMilliDegrees != 0) )
			{
				var pawnForMove = _host.GameObject.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
				var pawnRootForMove = pawnForMove.IsValid() ? pawnForMove.GameObject : _host.GameObject;
				var move = pawnRootForMove.IsValid() ? pawnRootForMove.Components.Get<ThornsPawnMovement>() : default;
				if ( move.IsValid() )
				{
					move.OwnerApplyMomentaryWeaponRecoil( clientKickPitchDegrees, clientKickYawDegrees );
				}
			}
		}
	}

	void PlayOwnerFireOrMeleeOutcomeSound(
		int fpAttackPresentationKind,
		bool damageAppliedToTarget,
		bool feedbackHasEndpoint,
		ThornsWeaponImpactSurfaceKind feedbackSurface )
	{
		if ( fpAttackPresentationKind is 1 or 2 )
		{
			if ( !damageAppliedToTarget )
			{
				var cidNoDmg = _host.ClientMirrorCombatDefinitionId ?? "";
				if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cidNoDmg )
				     && feedbackHasEndpoint
				     && feedbackSurface is ThornsWeaponImpactSurfaceKind.Terrain or ThornsWeaponImpactSurfaceKind.Metal )
				{
					var hb = _host.GameObject.Components.Get<ThornsHotbarEquipment>();
					var inv = _host.GameObject.Components.Get<ThornsInventory>();
					var itemId = "";
					if ( hb.IsValid() && inv.IsValid() )
					{
						var sel = hb.ClientMirrorSelectedHotbar;
						if ( sel >= 0 && inv.TryGetClientMirrorSlot( sel, out var net ) && net.Quantity > 0
						     && !string.IsNullOrWhiteSpace( net.ItemId ) )
							itemId = net.ItemId;
					}

					if ( string.IsNullOrWhiteSpace( itemId )
					     && string.Equals( cidNoDmg.Trim(), ThornsToolMeleeCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase ) )
						itemId = "primitive_tool";

					var pathContact = ThornsToolMeleeCombat.GetMeleeWorldContactStrikeSoundPathForTool( _host.GameObject, itemId );
					ThornsGameplaySfx.PlayToolStrikeContactDeduped( _host.GameObject, pathContact );
					return;
				}

				ThornsGameplaySfx.PlayMeleeMiss( _host.GameObject );
				return;
			}

			var cidMelee = _host.ClientMirrorCombatDefinitionId ?? "";
			if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cidMelee ) )
			{
				var hb = _host.GameObject.Components.Get<ThornsHotbarEquipment>();
				var inv = _host.GameObject.Components.Get<ThornsInventory>();
				var itemId = "";
				if ( hb.IsValid() && inv.IsValid() )
				{
					var sel = hb.ClientMirrorSelectedHotbar;
					if ( sel >= 0 && inv.TryGetClientMirrorSlot( sel, out var net ) && net.Quantity > 0
					     && !string.IsNullOrWhiteSpace( net.ItemId ) )
						itemId = net.ItemId;
				}

				if ( string.IsNullOrWhiteSpace( itemId )
				     && string.Equals( cidMelee.Trim(), ThornsToolMeleeCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase ) )
					itemId = "primitive_tool";

				var toolHitPath = ThornsToolMeleeCombat.GetMeleeHitSoundPathForToolItemId( itemId );
				_observerSync.PlayOwnerWeaponSoundAtEar(
					toolHitPath,
					ThornsGameplaySfx.VolumeMultiplierForToolStrikePath( toolHitPath ) );
				return;
			}

			if ( string.Equals( cidMelee, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			{
				var pathMelee = fpAttackPresentationKind == 2 ? ThornsWeapon.KnifeStabHeavySoundResource : ThornsWeapon.KnifeStabLightSoundResource;
				_observerSync.PlayOwnerWeaponSoundAtEar( pathMelee );
			}

			return;
		}

		if ( fpAttackPresentationKind != 0 )
			return;

		var cid = _host.ClientMirrorCombatDefinitionId ?? "";
		string path = null;
		if ( string.Equals( cid, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			path = ThornsWeapon.ShotgunFireSoundResource;
		else if ( MagazineWeaponUsesM4StyleFireSound( cid ) )
			path = ThornsWeapon.M4FireSoundResource;

		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		_observerSync.PlayOwnerWeaponSoundAtEar( path );
	}

	static bool MagazineWeaponUsesM4StyleFireSound( string combatKey )
	{
		if ( string.IsNullOrWhiteSpace( combatKey ) )
			return false;

		return string.Equals( combatKey, "m4", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "mp5", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "sniper", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Local owner: short tick at camera/ears when a host-validated hit deals damage (players, NPCs, wildlife). Tuned via <see cref="ThornsWeapon.HitMarkerBodyVolume"/> / <see cref="ThornsWeapon.HitMarkerHeadshotVolume"/> when the sound API supports it.</summary>
	void OwnerPlayHitMarkerSound( bool accentHit )
	{
		var bodyPath = string.IsNullOrWhiteSpace( _host.HitMarkerBodySound ) ? ThornsWeapon.HitMarkerBodySoundDefault : _host.HitMarkerBodySound.Trim();
		var hsPath = string.IsNullOrWhiteSpace( _host.HitMarkerHeadshotSound ) ? bodyPath : _host.HitMarkerHeadshotSound.Trim();
		var path = accentHit ? hsPath : bodyPath;
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( _host.GameObject, out var ear, out _ ) )
			ear = _host.GameObject.WorldPosition;

		var h = Sound.Play( path, ear );
		var wantVol = accentHit ? _host.HitMarkerHeadshotVolume : _host.HitMarkerBodyVolume;
		if ( h.IsValid )
			h.Volume = Math.Clamp( wantVol, 0f, 2f );
	}
}
