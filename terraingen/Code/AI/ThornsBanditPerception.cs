namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Player;

/// <summary>Low-frequency vision and sound perception for bandits (host-only).</summary>
public static class ThornsBanditPerception
{
	static readonly List<GameObject> PlayerScratch = new( 16 );

	static float VisionAcquireRangeWorld( ThornsBanditArchetypeConfig cfg ) =>
		Math.Max( cfg.VisionRangeWorld, cfg.EngagementRangeWorld * 0.92f );

	static float VisionConeForState( ThornsBanditBrain brain, ThornsBanditArchetypeConfig cfg, float targetDistSq )
	{
		if ( brain.State is not (ThornsBanditAiState.Patrol or ThornsBanditAiState.Investigate) )
			return cfg.VisionConeDegrees;

		var wide = cfg.VisionConeDegrees + 50f;
		var engageR = cfg.EngagementRangeWorld;
		if ( targetDistSq < engageR * engageR * 0.64f )
			wide += 25f;

		return Math.Min( 200f, wide );
	}

	public static Vector3 ResolveAimPoint( GameObject root )
	{
		if ( !root.IsValid() )
			return default;

		if ( ThornsLocalPlayer.TryGetAuthoritativeEye( root, out var eye, out _ ) )
			return eye;

		var mount = ThornsBanditUtil.ResolveMountedAnimalBrain( root );
		if ( mount.IsValid() )
			return mount.GameObject.WorldPosition + Vector3.Up * 72f;

		return root.WorldPosition + Vector3.Up * 56f;
	}

	public static bool IsInVisionCone( Vector3 selfFlat, Rotation selfRot, Vector3 targetFlat, float coneDegrees )
	{
		var forward = selfRot.Forward.WithZ( 0 );
		if ( forward.LengthSquared < 1e-4f )
			return true;

		var to = (targetFlat - selfFlat).Normal;
		var dot = Vector3.Dot( forward.Normal, to );
		var halfCos = MathF.Cos( coneDegrees * 0.5f * MathF.PI / 180f );
		return dot >= halfCos;
	}

	public static bool HasClearLos( GameObject self, GameObject target, float maxDistance )
	{
		if ( !self.IsValid() || !target.IsValid() )
			return false;

		if ( !ThornsLocalPlayer.TryGetAuthoritativeEye( self, out var eye, out _ ) )
			eye = self.WorldPosition + Vector3.Up * 64f;

		var tgt = ResolveAimPoint( target );
		var delta = tgt - eye;
		var len = delta.Length;
		if ( len > maxDistance )
			return false;

		if ( len <= ThornsBanditCombatTuning.ImmediateNoticeNoLosDistance )
			return true;

		var dir = delta.Normal;
		var traceLen = Math.Min( len - 6f, maxDistance );
		if ( traceLen < 8f )
			return true;

		var scene = self.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var selfBrain = self.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		var tr = ThornsBanditTraceUtil.RunRay( scene, new Ray( eye, dir ), traceLen, ThornsBanditTraceUtil.LosProfile, self );
		if ( !tr.Hit )
			return true;

		if ( TraceHitIsRoot( tr.GameObject, target ) )
			return true;

		if ( selfBrain.IsValid() && TraceHitIsPeerBandit( tr.GameObject, selfBrain ) )
			return true;

		return TraceHitIsTargetRiderMount( tr.GameObject, target );
	}

	static bool TraceHitIsTargetRiderMount( GameObject hitGo, GameObject targetRoot )
	{
		if ( !targetRoot.IsValid() || !hitGo.IsValid() )
			return false;

		var mount = ThornsBanditUtil.ResolveMountedAnimalBrain( targetRoot );
		return mount.IsValid() && TraceHitIsRoot( hitGo, mount.GameObject );
	}

	static bool TraceHitIsPeerBandit( GameObject hitGo, ThornsBanditBrain self )
	{
		var peer = hitGo.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		return peer.IsValid() && peer != self;
	}

	public static bool TryAcquireImmediateProximityThreat(
		ThornsBanditBrain brain,
		ThornsBanditDirector director,
		Vector3 selfFlat,
		ThornsBanditArchetypeConfig cfg,
		out GameObject best )
	{
		best = default;
		if ( brain is null || !brain.IsValid() || director is null )
			return false;

		var noticeR = ThornsBanditCombatTuning.CloseNoticeDistance;
		var noticeR2 = noticeR * noticeR;
		var noLosR2 = ThornsBanditCombatTuning.ImmediateNoticeNoLosDistance
		              * ThornsBanditCombatTuning.ImmediateNoticeNoLosDistance;

		GameObject candidate = default;
		var bestScore = float.MinValue;

		director.HostQueryPlayersNearPlanar( selfFlat, noticeR, PlayerScratch );
		for ( var i = 0; i < PlayerScratch.Count; i++ )
		{
			var p = PlayerScratch[i];
			if ( !IsValidCombatTarget( p, brain, cfg ) )
				continue;

			var pFlat = p.WorldPosition.WithZ( 0 );
			var dsq = ( pFlat - selfFlat ).LengthSquared;
			if ( dsq > noticeR2 )
				continue;

			if ( dsq > noLosR2 && !HasClearLos( brain.GameObject, p, noticeR ) )
				continue;

			var score = 1200f - dsq;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			candidate = p;
		}

		var wildlifeN = 0;
		foreach ( var animal in ThornsAnimalManager.AnimalRegistry )
		{
			if ( wildlifeN++ > 64 )
				break;

			if ( !animal.IsValid() || animal.IsDead )
				continue;

			if ( animal.IsTamed
			     && !animal.IsMounted
			     && animal.AiState is not (ThornsAnimalState.Chase or ThornsAnimalState.Attack) )
				continue;

			var root = animal.GameObject;
			if ( !IsValidCombatTarget( root, brain, cfg ) )
				continue;

			var aFlat = root.WorldPosition.WithZ( 0 );
			var dsq = ( aFlat - selfFlat ).LengthSquared;
			if ( dsq > noticeR2 )
				continue;

			if ( dsq > noLosR2 && !HasClearLos( brain.GameObject, root, noticeR ) )
				continue;

			var score = animal.IsMounted ? (1150f - dsq) : (1050f - dsq);
			if ( score <= bestScore )
				continue;

			bestScore = score;
			candidate = root;
		}

		if ( !candidate.IsValid() )
			return false;

		best = candidate;
		return true;
	}

	public static bool TryAcquireVisibleTarget(
		ThornsBanditBrain brain,
		ThornsBanditDirector director,
		Vector3 selfFlat,
		ThornsBanditArchetypeConfig cfg,
		GameObject priorityAttacker,
		out GameObject best )
	{
		best = default;
		if ( brain is null || !brain.IsValid() || director is null )
			return false;

		var acquireRange = VisionAcquireRangeWorld( cfg );
		var visionR2 = acquireRange * acquireRange;

		if ( priorityAttacker.IsValid() && IsValidCombatTarget( priorityAttacker, brain, cfg ) )
		{
			var pFlat = priorityAttacker.WorldPosition.WithZ( 0 );
			var pDsq = ( pFlat - selfFlat ).LengthSquared;
			if ( pDsq > visionR2 )
			{
				var loseR = cfg.LoseTargetRangeWorld;
				if ( brain.HasRecentDamageAttacker && pDsq <= loseR * loseR )
				{
					best = priorityAttacker;
					return true;
				}

				goto ScanCandidates;
			}

			var cone = brain.HasRecentDamageAttacker
				? 360f
				: VisionConeForState( brain, cfg, pDsq );
			if ( IsInVisionCone( selfFlat, brain.GameObject.WorldRotation, pFlat, cone )
			     && HasClearLos( brain.GameObject, priorityAttacker, acquireRange ) )
			{
				best = priorityAttacker;
				return true;
			}
		}

		ScanCandidates:
		GameObject candidate = default;
		var bestScore = float.MinValue;

		director.HostQueryPlayersNearPlanar( selfFlat, acquireRange, PlayerScratch );

		var wildlifeN = 0;
		foreach ( var animal in ThornsAnimalManager.AnimalRegistry )
		{
			if ( wildlifeN++ > 64 )
				break;

			if ( !animal.IsValid() || animal.IsDead )
				continue;

			if ( animal.IsTamed
			     && !animal.IsMounted
			     && animal.AiState is not (ThornsAnimalState.Chase or ThornsAnimalState.Attack) )
				continue;

			var root = animal.GameObject;
			if ( !IsValidCombatTarget( root, brain, cfg ) )
				continue;

			var aFlat = root.WorldPosition.WithZ( 0 );
			var dsq = ( aFlat - selfFlat ).LengthSquared;
			if ( dsq > visionR2 )
				continue;

			var flatDist = MathF.Sqrt( dsq );
			var cone = flatDist <= ThornsBanditCombatTuning.CloseNoticeDistance
				? 360f
				: VisionConeForState( brain, cfg, dsq );
			if ( !IsInVisionCone( selfFlat, brain.GameObject.WorldRotation, aFlat, cone ) )
				continue;

			if ( !HasClearLos( brain.GameObject, root, acquireRange ) )
				continue;

			// Prefer wild prey/predators over players at similar distance — bandits should hunt wildlife proactively.
			var score = animal.IsTamed ? (980f - dsq) : (1050f - dsq);
			if ( score <= bestScore )
				continue;

			bestScore = score;
			candidate = root;
		}

		for ( var i = 0; i < PlayerScratch.Count; i++ )
		{
			var p = PlayerScratch[i];
			if ( !IsValidCombatTarget( p, brain, cfg ) )
				continue;

			var pFlat = p.WorldPosition.WithZ( 0 );
			var dsq = ( pFlat - selfFlat ).LengthSquared;
			var flatDist = MathF.Sqrt( dsq );
			var cone = flatDist <= ThornsBanditCombatTuning.CloseNoticeDistance
				? 360f
				: VisionConeForState( brain, cfg, dsq );
			if ( !IsInVisionCone( selfFlat, brain.GameObject.WorldRotation, pFlat, cone ) )
				continue;

			if ( dsq > visionR2 )
				continue;

			if ( !HasClearLos( brain.GameObject, p, acquireRange ) )
				continue;

			var score = 1000f - dsq;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			candidate = p;
		}

		foreach ( var ally in QueryNearbyBandits( selfFlat, cfg.VisionRangeWorld ) )
		{
			if ( !ally.IsValid() || ally == brain || ally.IsDead )
				continue;

			if ( brain.GroupId != 0 && ally.GroupId == brain.GroupId )
				continue;

			var root = ally.GameObject;
			var aFlat = root.WorldPosition.WithZ( 0 );
			var dsq = ( aFlat - selfFlat ).LengthSquared;
			if ( dsq > visionR2 )
				continue;

			if ( !IsInVisionCone( selfFlat, brain.GameObject.WorldRotation, aFlat, cfg.VisionConeDegrees ) )
				continue;

			if ( !HasClearLos( brain.GameObject, root, cfg.VisionRangeWorld ) )
				continue;

			var score = 400f - dsq;
			if ( score <= bestScore )
				continue;

			bestScore = score;
			candidate = root;
		}

		if ( !candidate.IsValid() )
			return false;

		best = candidate;
		return true;
	}

	public static bool IsValidCombatTarget( GameObject root, ThornsBanditBrain brain, ThornsBanditArchetypeConfig cfg )
	{
		if ( !root.IsValid() || root == brain.GameObject )
			return false;

		root = ThornsLocalPlayer.ResolvePawnRoot( root );

		var playerHp = root.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent );
		if ( playerHp is not null && playerHp.IsValid() )
		{
			if ( !playerHp.IsAlive || playerHp.IsDeadState )
				return false;

			var ally = root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
			return !ally.IsValid() || ally != brain;
		}

		var animal = root.Components.GetInAncestorsOrSelf<ThornsAnimalBrain>( true );
		if ( animal.IsValid() )
			return !animal.IsDead;

		var banditAlly = root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( banditAlly.IsValid() && banditAlly == brain )
			return false;

		return true;
	}

	static IEnumerable<ThornsBanditBrain> QueryNearbyBandits( Vector3 selfFlat, float radiusWorld )
	{
		ThornsBanditSpatialGrid.QueryPlanarScratch( selfFlat, radiusWorld );
		return ThornsBanditSpatialGrid.ScratchResults;
	}

	public static bool TryRefreshHearing(
		Vector3 selfFlat,
		ThornsBanditArchetypeConfig cfg,
		out Vector3 heardPoint,
		out ThornsBanditCommunication.SoundType soundType )
	{
		heardPoint = default;
		soundType = default;
		if ( !ThornsBanditCommunication.TryGetNearestHeardEvent( selfFlat, cfg, 10.0, out var ev, out _ )
		     || ev.Time <= 0 )
			return false;

		heardPoint = ev.World;
		soundType = ev.Type;
		return heardPoint != default;
	}

	static bool TraceHitIsRoot( GameObject hitGo, GameObject targetRoot )
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
}
