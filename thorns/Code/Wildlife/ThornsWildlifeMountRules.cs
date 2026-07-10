using System;

namespace Sandbox;

/// <summary>Client + host rules for riding owned mountable tames (tap Use on crosshair hit).</summary>
public static class ThornsWildlifeMountRules
{
	/// <summary>Ray length for resolving wildlife under the crosshair when tapping Use to mount.</summary>
	public const float MountAimRayMaxDistance = 200_000f;

	/// <summary>Max distance from the rider pawn root to the tame root for a mount to be allowed (host + client gate).</summary>
	public const float MaxMountDistanceFromPawnWorld = 500f;

	/// <summary>Scales species <see cref="ThornsWildlifeSpeciesDefinition.MountRiderHeightUp"/> / forward for boss fauna mesh scale.</summary>
	public static float MountSeatScaleForWildlife( ThornsWildlifeIdentity wid ) =>
		wid is { IsValid: true, IsBossWildlifeSync: true }
			? ThornsWildlifeSpawn.BossWildlifeModelAndHitboxMul
			: 1f;

	/// <summary>World-space offset from the tame root origin to the rider seat (planar forward from mount facing).</summary>
	public static Vector3 ComputeMountRiderSeatWorldOffset(
		ThornsWildlifeIdentity wid,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flatForward )
	{
		if ( def is null )
			return Vector3.Zero;

		var scale = MountSeatScaleForWildlife( wid );
		var fwd = flatForward;
		if ( fwd.LengthSquared < 1e-4f )
			fwd = Vector3.Forward;
		else
			fwd = fwd.Normal;

		return fwd * ( def.MountRiderForward * scale ) + Vector3.Up * ( def.MountRiderHeightUp * scale );
	}

	/// <summary>Seat offset in the tame root's local space (rider parented to mount).</summary>
	public static Vector3 ComputeMountRiderSeatLocalOffset(
		ThornsWildlifeIdentity wid,
		ThornsWildlifeSpeciesDefinition def )
	{
		if ( def is null )
			return Vector3.Zero;

		var scale = MountSeatScaleForWildlife( wid );
		return Vector3.Forward * ( def.MountRiderForward * scale ) + Vector3.Up * ( def.MountRiderHeightUp * scale );
	}

	/// <summary>Whether <paramref name="pawnRoot"/> is close enough to <paramref name="tameRoot"/> to mount (3D world units).</summary>
	public static bool IsPawnWithinMaxMountDistance( GameObject pawnRoot, GameObject tameRoot )
	{
		if ( pawnRoot is null || tameRoot is null || !pawnRoot.IsValid() || !tameRoot.IsValid() )
			return false;

		var max = MaxMountDistanceFromPawnWorld;
		return ( tameRoot.WorldPosition - pawnRoot.WorldPosition ).LengthSquared <= max * max;
	}

	/// <summary>Crosshair ray or view-cone picks your mountable tame; must also be within <see cref="MaxMountDistanceFromPawnWorld"/>.</summary>
	/// <param name="rejectReason">Populated when this returns false (for diagnostics).</param>
	public static bool ClientTryGetMountTapTarget( GameObject pawnRoot, out ThornsWildlifeIdentity wid, out string rejectReason )
	{
		wid = default;
		rejectReason = "";

		if ( pawnRoot is null || !pawnRoot.IsValid() )
		{
			rejectReason = "invalid_pawn_root";
			return false;
		}

		var lc = Connection.Local;
		if ( lc is null )
		{
			rejectReason = "no_local_connection";
			return false;
		}

		if ( ThornsWildlifeTamingRules.TryGetRayWildlifeUnderAim( pawnRoot, MountAimRayMaxDistance, out var fromRay ) && fromRay.IsValid() )
		{
			if ( TryFinishMountTapValidation( lc.Id, fromRay, pawnRoot, out rejectReason ) )
			{
				wid = fromRay;
				return true;
			}
		}

		if ( ClientTryGetOwnedMountableInViewCone( pawnRoot, lc.Id, out wid ) && wid.IsValid()
		     && TryFinishMountTapValidation( lc.Id, wid, pawnRoot, out rejectReason ) )
			return true;

		wid = default;
		rejectReason = string.IsNullOrEmpty( rejectReason ) ? "no_mount_target_ray_or_view_cone" : rejectReason;
		return false;
	}

	/// <summary>
	/// When the aim ray hits terrain or props first, pick your mountable tame roughly under the crosshair (wide cone, long range cap for perf).
	/// </summary>
	static bool ClientTryGetOwnedMountableInViewCone( GameObject pawnRoot, Guid localConnectionId, out ThornsWildlifeIdentity wid )
	{
		wid = default;
		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var lc = Connection.Local;
		if ( lc is null )
			return false;

		var accountKey = ThornsPersistenceIdentity.GetStableAccountKey( lc );
		var fwd = rot.Forward.Normal;
		ThornsWildlifeIdentity best = default;
		var bestScore = float.NegativeInfinity;
		const float maxConsider = 120_000f;
		const float minDot = 0.5f;

		ThornsWildlifeTameRegistry.ForEachOwnedBy( localConnectionId, accountKey, cand =>
		{
			if ( !cand.IsValid() || cand.WildlifeId == Guid.Empty )
				return;
			if ( !cand.Definition.AllowPlayerMount )
				return;

			if ( !IsPawnWithinMaxMountDistance( pawnRoot, cand.GameObject ) )
				return;

			var hp = cand.Components.Get<ThornsHealth>();
			if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
				return;

			var aimPoint = cand.GameObject.WorldPosition + Vector3.Up * 48f;
			var to = aimPoint - eye;
			var dist = to.Length;
			if ( dist < 16f || dist > maxConsider )
				return;

			var dir = to / dist;
			var dot = Vector3.Dot( fwd, dir );
			if ( dot < minDot )
				return;

			var score = dot * 200f - dist * 0.00012f;
			if ( score > bestScore )
			{
				bestScore = score;
				best = cand;
			}
		} );

		wid = best;
		return wid.IsValid();
	}

	static bool TryFinishMountTapValidation( Guid localConnectionId, ThornsWildlifeIdentity wid, GameObject pawnRoot, out string rejectReason )
	{
		rejectReason = "";

		if ( !wid.HostIsTamed )
		{
			rejectReason = $"hit_untamed species={wid.Species}";
			return false;
		}

		if ( !wid.Definition.AllowPlayerMount )
		{
			rejectReason = $"species_not_mountable species={wid.Species}";
			return false;
		}

		if ( !ThornsWildlifeIdentity.HostCallerOwnsTame( localConnectionId, wid ) )
		{
			rejectReason = $"not_your_tame tameOwnerConn={wid.TameOwnerConnectionId} yourConn={localConnectionId} accountKey={wid.TameOwnerAccountKeySync}";
			return false;
		}

		if ( !IsPawnWithinMaxMountDistance( pawnRoot, wid.GameObject ) )
		{
			rejectReason = $"too_far_for_mount max={MaxMountDistanceFromPawnWorld}";
			return false;
		}

		if ( !PawnCanMountTargetTame( pawnRoot, wid ) )
		{
			rejectReason = "not_aiming_at_tame";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Client + host mount aim gate — uses the same hitbox-aware wildlife pick as tap targeting, not
	/// <see cref="ThornsWorldUseAim.PawnLooksAtInteractableRoot"/> (InteractionUse skips hitboxes and often misses deer CC / skinned mesh).
	/// </summary>
	public static bool PawnCanMountTargetTame( GameObject pawnRoot, ThornsWildlifeIdentity wid )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || wid is null || !wid.IsValid() )
			return false;

		if ( !IsPawnWithinMaxMountDistance( pawnRoot, wid.GameObject ) )
			return false;

		if ( ThornsWildlifeTamingRules.TryGetRayWildlifeUnderAim( pawnRoot, MountAimRayMaxDistance, out var rayWid )
		     && rayWid.IsValid()
		     && rayWid.WildlifeId == wid.WildlifeId )
			return true;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var aimPoint = wid.GameObject.WorldPosition + Vector3.Up * 48f;
		var to = aimPoint - eye;
		var dist = to.Length;
		if ( dist < 24f )
			return true;

		if ( dist > MaxMountDistanceFromPawnWorld )
			return false;

		var dot = Vector3.Dot( rot.Forward.Normal, to / Math.Max( dist, 0.001f ) );
		return dot >= 0.42f;
	}

	/// <summary>True when the pawn is parented to a tame and riding — wildlife/bandits skip these for perception and aggro.</summary>
	public static bool PawnIsMounted( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var ix = pawnRoot.Components.Get<ThornsWildlifeMountInteractor>();
		return ix.IsValid() && ix.MountedWildlifeId != Guid.Empty;
	}
}

/// <summary>World HUD tame hold progress (mount no longer uses a hold bar).</summary>
public static class ThornsMountHoldHudBridge
{
	public static Guid ActiveWildlifeId;

	public static float Progress01;

	public static void Clear()
	{
		ActiveWildlifeId = Guid.Empty;
		Progress01 = 0f;
	}
}
