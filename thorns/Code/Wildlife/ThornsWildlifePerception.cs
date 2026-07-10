namespace Sandbox;

/// <summary>Radius-first perception + optional LOS after cheap filtering (THORNS §8). Host uses spatial player queries + shared LOS budget.</summary>
public static class ThornsWildlifePerception
{
	const int MaxPreyConsider = 36;

	static readonly List<GameObject> SpatialQueryBuffer = new();
	static readonly List<GameObject> CandidateRoots = new();
	static readonly List<float> CandidateDistSq = new();

	const int LosPositiveSlots = 48;
	static readonly GameObject[] LosPositiveW = new GameObject[LosPositiveSlots];
	static readonly GameObject[] LosPositiveP = new GameObject[LosPositiveSlots];
	static readonly double[] LosPositiveExp = new double[LosPositiveSlots];
	static int _losPositiveRing;

	static bool TryLosPositiveCache( GameObject wildlifeRoot, GameObject playerRoot )
	{
		if ( wildlifeRoot is null || playerRoot is null || !wildlifeRoot.IsValid() || !playerRoot.IsValid() )
			return false;

		var t = Time.Now;
		for ( var i = 0; i < LosPositiveSlots; i++ )
		{
			if ( LosPositiveW[i] == wildlifeRoot && LosPositiveP[i] == playerRoot && t < LosPositiveExp[i] )
				return true;
		}

		return false;
	}

	static void StoreLosPositiveCache( GameObject wildlifeRoot, GameObject playerRoot )
	{
		if ( wildlifeRoot is null || playerRoot is null || !wildlifeRoot.IsValid() || !playerRoot.IsValid() )
			return;

		var i = ++_losPositiveRing % LosPositiveSlots;
		LosPositiveW[i] = wildlifeRoot;
		LosPositiveP[i] = playerRoot;
		LosPositiveExp[i] = Time.Now + ThornsPerformanceBudgets.HostWildlifeLosPositiveCacheTtlSeconds;
	}

	static void RetainSmallestKByDistSq( List<GameObject> roots, List<float> d2, int k )
	{
		var n = roots.Count;
		if ( n <= k )
			return;

		for ( var i = 0; i < k; i++ )
		{
			var bestIdx = i;
			var bestD = d2[i];
			for ( var j = i + 1; j < n; j++ )
			{
				if ( d2[j] >= bestD )
					continue;
				bestD = d2[j];
				bestIdx = j;
			}

			if ( bestIdx == i )
				continue;

			(roots[i], roots[bestIdx]) = (roots[bestIdx], roots[i]);
			(d2[i], d2[bestIdx]) = (d2[bestIdx], d2[i]);
		}

		roots.RemoveRange( k, n - k );
		d2.RemoveRange( k, n - k );
	}

	static void InsertionSortByDistSq( List<GameObject> roots, List<float> d2 )
	{
		var n = roots.Count;
		for ( var i = 1; i < n; i++ )
		{
			var r = roots[i];
			var k = d2[i];
			var j = i - 1;
			while ( j >= 0 && d2[j] > k )
			{
				roots[j + 1] = roots[j];
				d2[j + 1] = d2[j];
				j--;
			}

			roots[j + 1] = r;
			d2[j + 1] = k;
		}
	}

	public static GameObject HostFindNearestPlayerInRadius(
		GameObject selfRoot,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		float radius,
		bool requireLos,
		float losMaxDistance,
		float senseHeightOffset )
	{
		if ( !Networking.IsHost || !selfRoot.IsValid() || director is null )
			return null;

		ThornsAiPerceptionMetrics.RecordWildlifePerceptionCall();

		director.HostQueryPlayersNearPlanar( selfFlat, radius, SpatialQueryBuffer );

		CandidateRoots.Clear();
		CandidateDistSq.Clear();

		for ( var i = 0; i < SpatialQueryBuffer.Count; i++ )
		{
			var root = SpatialQueryBuffer[i];
			if ( !root.IsValid() )
				continue;

			var hp = root.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			if ( ThornsWildlifeMountRules.PawnIsMounted( root ) )
				continue;

			var p = root.WorldPosition.WithZ( 0 );
			var d = (p - selfFlat).LengthSquared;
			var ups = root.Components.Get<ThornsPlayerUpgrades>();
			var ghostMul = ups.IsValid() ? ups.GetGhostWildlifeDetectionRadiusMultiplier() : 1f;
			var effR = radius * ghostMul;
			var effR2 = effR * effR;
			if ( d > effR2 )
				continue;

			CandidateRoots.Add( root );
			CandidateDistSq.Add( d );
		}

		ThornsAiPerceptionMetrics.RecordPerceptionPlayerConsiderations( CandidateRoots.Count );

		var maxCand = ThornsPerformanceBudgets.HostWildlifeMaxPlayerCandidatesPerPredatorThink;
		if ( CandidateRoots.Count > maxCand )
		{
			ThornsAiPerceptionMetrics.RecordPerceptionCandidateCapDrop();
			RetainSmallestKByDistSq( CandidateRoots, CandidateDistSq, maxCand );
		}

		if ( CandidateRoots.Count == 0 )
			return null;

		InsertionSortByDistSq( CandidateRoots, CandidateDistSq );

		if ( !requireLos )
			return CandidateRoots[0];

		var maxLos = ThornsPerformanceBudgets.HostWildlifeMaxLosProbesPerPredatorThink;
		for ( var i = 0; i < CandidateRoots.Count; i++ )
		{
			if ( i >= maxLos )
			{
				ThornsAiPerceptionMetrics.RecordLosProbeThinkCapHit();
				break;
			}

			var root = CandidateRoots[i];
			var ups = root.Components.Get<ThornsPlayerUpgrades>();
			var ghostMul = ups.IsValid() ? ups.GetGhostWildlifeDetectionRadiusMultiplier() : 1f;
			if ( HasLosWildlifeToPlayer( selfRoot, root, losMaxDistance * ghostMul, senseHeightOffset ) )
				return root;
		}

		return null;
	}

	public static ThornsWildlifeBrain HostFindNearestPreyBrain(
		GameObject selfRoot,
		Vector3 selfFlat,
		float radius,
		ThornsWildlifeSpeciesKind selfKind,
		int nearbyPackMembers = 0 )
	{
		if ( !Networking.IsHost || !selfRoot.IsValid() )
			return default;

		var best = float.MaxValue;
		ThornsWildlifeBrain pick = default;
		var r2 = radius * radius;
		var n = 0;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( n++ > MaxPreyConsider )
				break;

			if ( !brain.IsValid() || brain.GameObject == selfRoot )
				continue;

			var id = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !id.IsValid() || id.Definition.IsPredator )
				continue;

			if ( id.HostIsTamed )
				continue;

			if ( id.Species == selfKind )
				continue;

			var rel = ThornsAnimalRelationshipTable.Resolve( selfKind, id.Species, nearbyPackMembers );
			if ( !ThornsAnimalRelationshipTable.ShouldHunt( rel ) )
				continue;

			var other = brain.GameObject.WorldPosition.WithZ( 0 );
			var d = (other - selfFlat).LengthSquared;
			if ( d > r2 || d >= best )
				continue;

			var ohp = brain.Components.Get<ThornsHealth>();
			if ( ohp.IsValid() && ( !ohp.IsAlive || ohp.IsDeadState ) )
				continue;

			best = d;
			pick = brain;
		}

		return pick;
	}

	public static bool HasLosWildlifeToPlayer(
		GameObject wildlifeRoot,
		GameObject playerRoot,
		float maxDistance,
		float senseHeightOffset )
	{
		if ( !wildlifeRoot.IsValid() || !playerRoot.IsValid() )
			return false;

		if ( TryLosPositiveCache( wildlifeRoot, playerRoot ) )
		{
			ThornsAiPerceptionMetrics.RecordLosCacheHit();
			return true;
		}

		var eye = wildlifeRoot.WorldPosition + Vector3.Up * senseHeightOffset;
		Vector3 tgt;
		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( playerRoot, out var pe, out _ ) )
			tgt = pe;
		else
			tgt = playerRoot.WorldPosition + Vector3.Up * 56f;

		var delta = tgt - eye;
		var len = delta.Length;
		if ( len < 8f || len > maxDistance )
			return false;

		var dir = delta.Normal;
		var traceLen = Math.Min( len - 4f, maxDistance );
		if ( traceLen < 6f )
		{
			StoreLosPositiveCache( wildlifeRoot, playerRoot );
			return true;
		}

		var scene = wildlifeRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( !ThornsWildlifeLosBudget.TryConsumeTrace() )
			return false;

		ThornsAiPerceptionMetrics.RecordLosTrace();

		var tr = ThornsTraceUtility.RunRay( scene, new Ray( eye, dir ), traceLen, ThornsTraceProfile.AiLineOfSight, wildlifeRoot );

		var ok = !tr.Hit || TraceHitIsTargetRoot( tr.GameObject, playerRoot );

		if ( ok )
			StoreLosPositiveCache( wildlifeRoot, playerRoot );

		return ok;
	}

	static bool TraceHitIsTargetRoot( GameObject hitGo, GameObject targetRoot )
	{
		if ( !hitGo.IsValid() || !targetRoot.IsValid() )
			return false;

		if ( hitGo == targetRoot )
			return true;

		for ( var p = hitGo; p.IsValid(); p = p.Parent )
		{
			if ( p == targetRoot )
				return true;
		}

		return false;
	}

	public static GameObject HostFindNearestBanditInRadius(
		GameObject selfRoot,
		Vector3 selfFlat,
		float radius,
		bool requireLos,
		float losMaxDistance,
		float senseHeightOffset )
	{
		if ( !Networking.IsHost || !selfRoot.IsValid() )
			return null;

		var best = float.MaxValue;
		GameObject pick = null;
		var r2 = radius * radius;
		var n = 0;

		foreach ( var brain in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( n++ > MaxPreyConsider )
				break;

			if ( !brain.IsValid() || brain.GameObject == selfRoot )
				continue;

			var root = brain.GameObject;
			var hp = root.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			var d = (root.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;
			if ( d > r2 || d >= best )
				continue;

			if ( requireLos && !HasLosWildlifeToPlayer( selfRoot, root, losMaxDistance, senseHeightOffset ) )
				continue;

			best = d;
			pick = root;
		}

		return pick;
	}

	public static GameObject HostFindNearestThreatForPrey(
		GameObject selfRoot,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		float fearRadius,
		float predatorAggroVsPrey,
		ThornsWildlifeSpeciesKind observerSpecies = default )
	{
		if ( !Networking.IsHost )
			return null;

		ThornsAiPerceptionMetrics.RecordWildlifePerceptionCall();

		var preyProfile = observerSpecies != default
			? ThornsAnimalBehaviorProfile.Get( observerSpecies )
			: ThornsAnimalBehaviorProfile.Get( ThornsWildlifeSpeciesKind.Deer );

		var effFearRadius = fearRadius * preyProfile.DetectionRadiusMul;
		var best = float.MaxValue;
		GameObject threat = null;

		director.HostQueryPlayersNearPlanar( selfFlat, effFearRadius, SpatialQueryBuffer );

		var playerConsider = 0;
		for ( var i = 0; i < SpatialQueryBuffer.Count; i++ )
		{
			var root = SpatialQueryBuffer[i];
			if ( !root.IsValid() )
				continue;

			var hp = root.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			if ( ThornsWildlifeMountRules.PawnIsMounted( root ) )
				continue;

			playerConsider++;
			var ups = root.Components.Get<ThornsPlayerUpgrades>();
			var ghostMul = ups.IsValid() ? ups.GetGhostWildlifeDetectionRadiusMultiplier() : 1f;
			var effFear = effFearRadius * ghostMul;
			var effFearR2 = effFear * effFear;
			var d = (root.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;
			if ( d < effFearR2 && d < best )
			{
				best = d;
				threat = root;
			}
		}

		ThornsAiPerceptionMetrics.RecordPerceptionPlayerConsiderations( playerConsider );

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() || brain.GameObject == selfRoot )
				continue;

			var id = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !id.IsValid() )
				continue;

			var d = (brain.GameObject.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;

			if ( observerSpecies != default )
			{
				var rel = ThornsAnimalRelationshipTable.Resolve( observerSpecies, id.Species );
				if ( rel is ThornsAnimalRelationshipKind.Ignore or ThornsAnimalRelationshipKind.Curious )
					continue;

				if ( rel is ThornsAnimalRelationshipKind.Avoid )
				{
					var avoidR = predatorAggroVsPrey * 0.42f;
					if ( d > avoidR * avoidR )
						continue;
				}
				else if ( !ThornsAnimalRelationshipTable.ShouldFlee( rel )
				         && !ThornsAnimalRelationshipTable.ShouldStandGround( rel ) )
					continue;
			}
			else if ( !id.Definition.IsPredator )
			{
				continue;
			}

			var predatorProfile = ThornsAnimalBehaviorProfile.Get( id.Species );
			var effPredR = ThornsAnimalPerceptionService.GetEffectiveThreatRadiusForPrey(
				id.Definition,
				predatorProfile,
				preyProfile );
			var effPredR2 = effPredR * effPredR;

			// Wild and tamed predators (e.g. pet wolves) scare herbivores when close.
			var php = brain.Components.Get<ThornsHealth>();
			if ( php.IsValid() && ( !php.IsAlive || php.IsDeadState ) )
				continue;

			if ( d < effPredR2 && d < best )
			{
				best = d;
				threat = brain.GameObject;
			}
		}

		var banditFearR2 = effFearRadius * effFearRadius;
		foreach ( var bandit in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( !bandit.IsValid() )
				continue;

			var root = bandit.GameObject;
			var hp = root.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			var d = (root.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;
			if ( d < banditFearR2 && d < best )
			{
				best = d;
				threat = root;
			}
		}

		return threat;
	}
}
