namespace Sandbox;

/// <summary>Shared client + host checks for hold-to-tame (HP gate, distance, trace fallback).</summary>
public static class ThornsWildlifeTamingRules
{
	public const float MaxTameDistance = 290f;

	/// <summary>Boss creatures use a larger interaction radius (scaled mesh / capsule).</summary>
	public const float BossWildlifeTameDistanceMul = 1.85f;

	public static float GetMaxTameDistanceFor( ThornsWildlifeIdentity wid ) =>
		wid is not null && wid.IsValid() && wid.IsBossWildlifeSync
			? MaxTameDistance * BossWildlifeTameDistanceMul
			: MaxTameDistance;
	public const float TraceDistance = 560f;

	/// <summary>Unified slack so client preview, hold completion, and host RPC agree on boundary HP.</summary>
	public const float ThresholdEpsilon = 0.0025f;

	public static float GetTamingThresholdForPawnRoot( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return ThornsUpgradeBalance.TamingThresholdBaseHpFraction;

		var upg = pawnRoot.Components.Get<ThornsPlayerUpgrades>();
		return upg.IsValid()
			? upg.GetTamingHealthFractionThreshold()
			: ThornsUpgradeBalance.TamingThresholdBaseHpFraction;
	}

	public static bool TryGetWildlifeHealthFraction( ThornsWildlifeIdentity wid, out float health01 )
	{
		health01 = 1f;
		if ( wid is null || !wid.IsValid() )
			return false;

		var hp = wid.Components.Get<ThornsHealth>();
		if ( !hp.IsValid() || hp.MaxHealth <= 0.01f )
			return false;

		health01 = hp.CurrentHealth / hp.MaxHealth;
		return true;
	}

	public static bool IsEligibleToTame(
		ThornsWildlifeIdentity wid,
		GameObject pawnRoot,
		float thresholdHpFraction,
		float maxDistance )
	{
		if ( wid is null || !wid.IsValid()
		     || wid.WildlifeId == Guid.Empty
		     || wid.HostIsTamed )
			return false;

		if ( !TryGetWildlifeHealthFraction( wid, out var hpFrac ) )
			return false;

		if ( hpFrac > thresholdHpFraction + ThresholdEpsilon )
			return false;

		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		return ( pawnRoot.WorldPosition - wid.GameObject.WorldPosition ).Length <= maxDistance;
	}

	public static bool TryGetRayTameCandidate( GameObject pawnRoot, out ThornsWildlifeIdentity wid, out float health01 )
	{
		wid = default;
		health01 = 1f;

		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var tr = ThornsTraceUtility.RunRay( pawnRoot.Scene, new Ray( eye, rot.Forward ), TraceDistance, ThornsTraceProfile.TamingWorldPick, pawnRoot );

		if ( !tr.Hit || !tr.GameObject.IsValid() )
			return false;

		wid = tr.GameObject.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		return wid.IsValid() && TryGetWildlifeHealthFraction( wid, out health01 );
	}

	/// <summary>
	/// Ray-hit untamed wildlife that is <b>too healthy</b> to tame yet — for "weaken first" HUD copy (same distance / look gates as tame flow).
	/// </summary>
	public static bool TryGetRayUntamedTooHealthyForTamingUi(
		GameObject pawnRoot,
		float thresholdHpFraction,
		out ThornsWildlifeIdentity wid,
		out float health01 )
	{
		wid = default;
		health01 = 1f;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !TryGetRayTameCandidate( pawnRoot, out wid, out health01 ) )
			return false;

		if ( !wid.IsValid() || wid.WildlifeId == Guid.Empty || wid.HostIsTamed )
			return false;

		if ( !TryGetWildlifeHealthFraction( wid, out health01 ) )
			return false;

		if ( health01 <= thresholdHpFraction + ThresholdEpsilon )
			return false;

		if ( ( pawnRoot.WorldPosition - wid.GameObject.WorldPosition ).Length > GetMaxTameDistanceFor( wid ) )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, wid.GameObject, GetMaxTameDistanceFor( wid ) );
	}

	/// <summary>When the aim ray hits terrain or an obscuring collider, pick the best cone candidate in tame range.</summary>
	public static bool TryGetConeFallbackTameCandidate(
		GameObject pawnRoot,
		float thresholdHpFraction,
		out ThornsWildlifeIdentity wid,
		out float health01 )
	{
		wid = default;
		health01 = 1f;

		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		var forward = rot.Forward.Normal;
		var pawnPos = pawnRoot.WorldPosition;
		ThornsWildlifeIdentity best = default;
		var bestScore = -1f;

		foreach ( var cand in pawnRoot.Scene.GetAllComponents<ThornsWildlifeIdentity>() )
		{
			if ( !cand.IsValid() )
				continue;

			if ( !IsEligibleToTame( cand, pawnRoot, thresholdHpFraction, GetMaxTameDistanceFor( cand ) ) )
				continue;

			var targetPos = cand.GameObject.WorldPosition + Vector3.Up * 40f;
			var to = ( targetPos - eye );
			var len = to.Length;
			if ( len < 8f || len > GetMaxTameDistanceFor( cand ) + 40f )
				continue;

			var dir = to / len;
			var dot = Vector3.Dot( forward, dir );
			if ( dot < 0.55f )
				continue;

			var dist = ( cand.GameObject.WorldPosition - pawnPos ).Length;
			var score = dot * 2.2f - dist * 0.0015f;
			if ( score > bestScore )
			{
				bestScore = score;
				best = cand;
			}
		}

		if ( !best.IsValid() )
			return false;

		wid = best;
		return TryGetWildlifeHealthFraction( wid, out health01 );
	}

	public static bool ClientTryGetTameCandidate( GameObject pawnRoot, float thresholdHpFraction, out ThornsWildlifeIdentity wid, out float health01 )
	{
		if ( TryGetRayTameCandidate( pawnRoot, out wid, out health01 )
		     && IsEligibleToTame( wid, pawnRoot, thresholdHpFraction, GetMaxTameDistanceFor( wid ) ) )
			return true;

		return TryGetConeFallbackTameCandidate( pawnRoot, thresholdHpFraction, out wid, out health01 );
	}

	/// <summary>Crosshair ray → wildlife identity (tamed or not). Used for mount / dismount interact.</summary>
	public static bool TryGetRayWildlifeUnderAim( GameObject pawnRoot, float maxDistance, out ThornsWildlifeIdentity wid )
	{
		wid = default;

		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var eye, out var rot ) )
			return false;

		// Trace length must follow maxDistance (mount uses long rays; taming ray callers use TraceDistance-sized caps).
		var rayLen = Math.Clamp( maxDistance, 64f, 500_000f );
		var tr = ThornsTraceUtility.RunRay( pawnRoot.Scene, new Ray( eye, rot.Forward ), rayLen, ThornsTraceProfile.TamingWorldPick, pawnRoot );

		if ( !tr.Hit || !tr.GameObject.IsValid() )
			return false;

		if ( tr.Distance > maxDistance )
			return false;

		wid = tr.GameObject.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		return wid.IsValid() && wid.WildlifeId != Guid.Empty;
	}
}
