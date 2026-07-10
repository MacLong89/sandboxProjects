namespace Sandbox;

/// <summary>Central target acquisition for wildlife AI — single path for prey, predator, and tamed combat.</summary>
public static class ThornsAnimalTargetingService
{
	public readonly struct PredatorHuntTarget
	{
		public GameObject Root { get; init; }
		public ThornsWildlifeBrain PreyBrain { get; init; }
		public bool IsRevenge { get; init; }
		public bool IsValid => Root.IsValid();
	}

	public static bool TryResolveRecentAttacker( ThornsAnimalBrainContext ctx, out GameObject attackerRoot )
	{
		attackerRoot = default;
		if ( Time.Now >= ctx.RecentAttackerUntilRealtime )
			return false;

		if ( ctx.RecentAttackerRoot is null || !ctx.RecentAttackerRoot.IsValid() )
			return false;

		var atkWild = ctx.RecentAttackerRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var atkBandit = ctx.RecentAttackerRoot.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( !atkWild.IsValid() && !atkBandit.IsValid() )
			return false;

		if ( ctx.RecentAttackerRoot == ctx.Self )
			return false;

		if ( ctx.Identity.IsValid() && atkWild.IsValid()
		     && ThornsWildlifeIdentity.HostTamesShareOwner( ctx.Identity, atkWild ) )
			return false;

		if ( ctx.Identity.IsValid() && ctx.Identity.HostIsTamed
		     && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( ctx.Identity, ctx.RecentAttackerRoot ) )
			return false;

		var hp = ctx.RecentAttackerRoot.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
			return false;

		attackerRoot = ctx.RecentAttackerRoot;
		return true;
	}

	public static GameObject FindPreyFleeThreat(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat )
	{
		ctx.RefreshPackAndProfileStats();
		var profile = ctx.BehaviorProfile;
		var fearRadius = ctx.Definition.FearRadius * profile.DetectionRadiusMul;

		return ThornsWildlifePerception.HostFindNearestThreatForPrey(
			ctx.Self,
			director,
			selfFlat,
			fearRadius,
			1100f * profile.ThreatDetectionMul,
			ctx.Identity.Species );
	}

	public static PredatorHuntTarget ResolvePredatorHuntTarget(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat )
	{
		if ( TryResolveRecentAttacker( ctx, out var revenge ) )
		{
			return new PredatorHuntTarget
			{
				Root = revenge,
				IsRevenge = true,
			};
		}

		ctx.RefreshPackAndProfileStats();
		var profile = ctx.BehaviorProfile;
		var def = ctx.Definition;
		var packMembers = ctx.NearbyPackMembers;

		var preyBrain = ThornsWildlifePerception.HostFindNearestPreyBrain(
			ctx.Self,
			selfFlat,
			def.AggroRadius,
			ctx.Identity.Species,
			packMembers );

		if ( preyBrain.IsValid() && profile.StalkPreference > 0.65f )
		{
			var wolvesNearPrey = ThornsAnimalPackCoordinator.CountSpeciesNear(
				preyBrain.GameObject,
				ThornsWildlifeSpeciesKind.Wolf,
				ThornsAnimalPackCoordinator.PackRadius * 0.85f );
			if ( wolvesNearPrey >= 2 )
				preyBrain = default;
		}

		var playerGo = ThornsWildlifePerception.HostFindNearestPlayerInRadius(
			ctx.Self,
			director,
			selfFlat,
			def.AggroRadius,
			def.UseLineOfSight,
			def.LoseRadius,
			def.SenseHeightOffset );

		if ( playerGo.IsValid() && profile.PackPreference > 0.55f
		     && !ThornsAnimalPackCoordinator.PackReadyToHunt( profile, packMembers ) )
			playerGo = null;

		var banditGo = ThornsWildlifePerception.HostFindNearestBanditInRadius(
			ctx.Self,
			selfFlat,
			def.AggroRadius,
			def.UseLineOfSight,
			def.LoseRadius,
			def.SenseHeightOffset );

		var bestDistSq = float.MaxValue;
		GameObject huntGo = null;
		ThornsWildlifeBrain preyChosen = default;

		if ( preyBrain.IsValid() )
		{
			var preyId = preyBrain.Components.Get<ThornsWildlifeIdentity>();
			var rel = ThornsAnimalRelationshipTable.Resolve(
				ctx.Identity.Species,
				preyId.Species,
				packMembers );
			ctx.LastRelationship = rel;
			ctx.LastRelationshipLabel = rel.ToString();

			if ( ThornsAnimalRelationshipTable.ShouldHunt( rel ) )
			{
				var preyDistSq = ( preyBrain.GameObject.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
				if ( preyDistSq < bestDistSq )
				{
					bestDistSq = preyDistSq;
					huntGo = preyBrain.GameObject;
					preyChosen = preyBrain;
				}
			}
		}

		if ( playerGo.IsValid() )
		{
			var playerDistSq = ( playerGo.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
			if ( playerDistSq < bestDistSq )
			{
				bestDistSq = playerDistSq;
				huntGo = playerGo;
				preyChosen = default;
				ctx.LastRelationship = ThornsAnimalRelationshipKind.Hunt;
				ctx.LastRelationshipLabel = "Hunt:player";
			}
		}

		if ( banditGo.IsValid() )
		{
			var banditDistSq = ( banditGo.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
			if ( banditDistSq < bestDistSq )
			{
				huntGo = banditGo;
				preyChosen = default;
				ctx.LastRelationship = ThornsAnimalRelationshipKind.Attack;
				ctx.LastRelationshipLabel = "Attack:bandit";
			}
		}

		return new PredatorHuntTarget
		{
			Root = huntGo,
			PreyBrain = preyChosen,
		};
	}

	public static bool TryResolveTameOwner( ThornsAnimalBrainContext ctx, out GameObject ownerRoot ) =>
		ctx.Brain.TryResolveTameOwnerRoot( ctx.Identity.TameOwnerConnectionId, out ownerRoot )
		|| ctx.Brain.TryResolveTameOwnerRootByAccountKey( ctx.Identity.TameOwnerAccountKeySync, out ownerRoot );

	public static GameObject ResolveTamedCombatTarget( ThornsAnimalBrainContext ctx, Vector3 ownerFlat, Vector3 selfFlat )
	{
		var id = ctx.Identity;
		var def = ctx.Definition;

		if ( def.IsPredator && TryResolveRecentAttacker( ctx, out var revenge )
		     && !HostIsForbiddenTameCombatTarget( ctx.Identity, revenge ) )
			return revenge;

		if ( TryResolveTameOwner( ctx, out var ownerRoot ) && ownerRoot.IsValid()
		     && ThornsTameHostIntel.HostTryResolveAssistTarget(
			     ownerRoot,
			     id,
			     ctx.Self,
			     ownerFlat,
			     selfFlat,
			     out var intelThreat )
		     && intelThreat.IsValid()
		     && !HostIsForbiddenTameCombatTarget( ctx.Identity, intelThreat ) )
			return intelThreat;

		if ( !id.TameFollowOwnerSync )
			return ctx.Brain.HostFindNearestHostilePredatorNearOwner(
				ownerFlat,
				id,
				Math.Max( def.AggroRadius, 2200f ) );

		return null;
	}

	public static bool HostIsForbiddenTameCombatTarget( ThornsWildlifeIdentity tame, GameObject candidate ) =>
		tame is null || !tame.IsValid() || candidate is null || !candidate.IsValid()
		|| ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( tame, candidate );

	public static GameObject ResolveGuardCombatTarget( ThornsAnimalBrainContext ctx, Vector3 ownerFlat, Vector3 selfFlat )
	{
		if ( ctx.Definition.IsPredator && TryResolveRecentAttacker( ctx, out var revenge )
		     && !HostIsForbiddenTameCombatTarget( ctx.Identity, revenge ) )
			return revenge;

		if ( TryResolveTameOwner( ctx, out var ownerRoot ) && ownerRoot.IsValid()
		     && ThornsTameHostIntel.HostTryResolveAssistTarget(
			     ownerRoot,
			     ctx.Identity,
			     ctx.Self,
			     ownerFlat,
			     selfFlat,
			     out var intelThreat )
		     && intelThreat.IsValid()
		     && !HostIsForbiddenTameCombatTarget( ctx.Identity, intelThreat ) )
			return intelThreat;

		return ctx.Brain.HostFindNearestHostilePredatorNearOwner(
			ownerFlat,
			ctx.Identity,
			Math.Max( ctx.Definition.AggroRadius, 2200f ) );
	}

	public static void BuildAlertCandidates(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		List<ThornsAnimalThreatSystem.ThreatCandidate> candidates )
	{
		candidates.Clear();
		if ( ctx.Identity.HostIsTamed )
			return;

		ctx.RefreshPackAndProfileStats();

		if ( !ctx.Definition.IsPredator )
		{
			var threat = FindPreyFleeThreat( ctx, director, selfFlat );
			if ( threat.IsValid() )
			{
				var dist = ( threat.WorldPosition.WithZ( 0 ) - selfFlat ).Length;
				var threatId = threat.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
				if ( threatId.IsValid() )
				{
					ctx.LastRelationship = ThornsAnimalRelationshipTable.Resolve(
						ctx.Identity.Species,
						threatId.Species );
					ctx.LastRelationshipLabel = ctx.LastRelationship.ToString();
				}

				candidates.Add( new ThornsAnimalThreatSystem.ThreatCandidate
				{
					Root = threat,
					Score = ThornsAnimalThreatSystem.ScoreByDistance(
						dist,
						ctx.Definition.FearRadius * ctx.BehaviorProfile.DetectionRadiusMul,
						1.2f + ctx.BehaviorProfile.Fearfulness * 0.35f ),
				} );
			}
		}
		else
		{
			var prey = ThornsWildlifePerception.HostFindNearestPreyBrain(
				ctx.Self,
				selfFlat,
				ctx.Definition.AggroRadius,
				ctx.Identity.Species,
				ctx.NearbyPackMembers );
			if ( prey.IsValid() )
			{
				var dist = ( prey.GameObject.WorldPosition.WithZ( 0 ) - selfFlat ).Length;
				candidates.Add( new ThornsAnimalThreatSystem.ThreatCandidate
				{
					Root = prey.GameObject,
					Score = ThornsAnimalThreatSystem.ScoreByDistance( dist, ctx.Definition.AggroRadius, 1f ),
				} );
			}
		}

		if ( TryResolveRecentAttacker( ctx, out var attacker ) )
		{
			candidates.Add( new ThornsAnimalThreatSystem.ThreatCandidate
			{
				Root = attacker,
				Score = ThornsAnimalThreatSystem.ScoreRecentDamage( true ),
			} );
		}
	}
}
