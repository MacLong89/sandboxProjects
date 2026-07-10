namespace Sandbox;

/// <summary>
/// State-machine decision layer — updates context and requests transitions (no direct SetState).
/// </summary>
public static class ThornsAnimalDecisionService
{
	public static void ThinkPrey(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		if ( ThornsAnimalTargetingService.TryResolveRecentAttacker( ctx, out var attacker )
		     && ThornsAnimalThreatPipeline.TryBeginFleeFromAttacker(
			     ctx,
			     selfFlat,
			     attacker,
			     machine,
			     "prey-revenge-flee" ) )
			return;

		var threat = ThornsAnimalTargetingService.FindPreyFleeThreat( ctx, director, selfFlat );
		if ( threat.IsValid() )
		{
			ThornsAnimalThreatPipeline.TryBeginFleeFromThreat( ctx, selfFlat, threat, machine, "prey-flee" );
			return;
		}

		if ( ctx.CurrentState == ThornsWildlifeAiState.Flee && Time.Now < ctx.FleeUntilRealtime )
			return;

		if ( ctx.CurrentState == ThornsWildlifeAiState.Flee )
			ctx.Brain.HostRequestPassiveLocomotion( ctx, machine, ctx.Definition );

		ctx.Brain.RunPassiveLocomotionThink( ctx, ctx.Definition, selfFlat, machine );
	}

	public static void ThinkPredator(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		var def = ctx.Definition;
		ctx.RefreshPackAndProfileStats();
		var profile = ctx.BehaviorProfile;

		var hasRevenge = ThornsAnimalTargetingService.TryResolveRecentAttacker( ctx, out _ );
		if ( hasRevenge )
			ctx.PredatorPeaceUntilRealtime = 0;

		if ( Time.Now < ctx.PredatorPeaceUntilRealtime && !hasRevenge )
		{
			ctx.Brain.RunPassiveLocomotionThink( ctx, def, selfFlat, machine );
			return;
		}

		var prevFocus = ctx.FocusTarget;
		var hunt = ThornsAnimalTargetingService.ResolvePredatorHuntTarget( ctx, director, selfFlat );
		ctx.FocusTarget = hunt.Root;
		ctx.PreyFocusBrain = hunt.PreyBrain;

		if ( hunt.IsValid && hunt.Root != prevFocus )
		{
			var label = hunt.IsRevenge
				? $"revenge:{hunt.Root.Name}"
				: hunt.PreyBrain.IsValid()
					? $"prey:{hunt.Root.Name}"
					: hunt.Root.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true ).IsValid()
						? $"bandit:{hunt.Root.Name}"
						: $"player:{hunt.Root.Name}";
			ThornsWildlifeLog.Target( ctx.Self.Name, label );
			ctx.HuntAbandonAfterRealtime = Time.Now + def.HuntCommitSeconds;
			ThornsAnimalPackCoordinator.TryBroadcastPackHunt( ctx.Self, hunt.Root );
		}

		if ( !hunt.IsValid )
		{
			if ( ctx.CurrentState is ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Attack
			     or ThornsWildlifeAiState.Chase or ThornsWildlifeAiState.Stalk )
				ctx.Brain.HostRequestPassiveLocomotion( ctx, machine, def );

			ctx.Brain.RunPassiveLocomotionThink( ctx, def, selfFlat, machine );
			return;
		}

		var huntDist = ( hunt.Root.WorldPosition.WithZ( 0 ) - selfFlat ).Length;

		if ( huntDist > def.LoseRadius )
		{
			ctx.Brain.HostRequestPassiveLocomotion( ctx, machine, def );
			ctx.Brain.RunPassiveLocomotionThink( ctx, def, selfFlat, machine );
			return;
		}

		if ( ctx.CurrentState == ThornsWildlifeAiState.Attack && huntDist <= def.AttackRange * 1.35f )
			ctx.HuntAbandonAfterRealtime = Time.Now + def.HuntCommitSeconds;

		if ( Time.Now > ctx.HuntAbandonAfterRealtime )
		{
			ctx.PredatorPeaceUntilRealtime =
				Time.Now + Math.Clamp( def.HuntCommitSeconds * 0.42, 2.1, 6.5 );
			ctx.Brain.HostRequestPassiveLocomotion( ctx, machine, def );
			ctx.Brain.RunPassiveLocomotionThink( ctx, def, selfFlat, machine );
			return;
		}

		if ( huntDist <= def.AttackRange )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Attack, "predator-attack" );
			ctx.Combat?.HostTryMeleeAttack( def, hunt.Root );
			return;
		}

		if ( profile.StalkPreference > 0.65f
		     && huntDist > def.AttackRange * 1.85f
		     && huntDist < def.LoseRadius * 0.92f )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Stalk, "predator-stalk" );
			return;
		}

		machine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "predator-hunt" );
	}

	public static void ThinkTamed(
		ThornsAnimalBrainContext ctx,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		var id = ctx.Identity;
		var def = ctx.Definition;

		if ( !ThornsAnimalTargetingService.TryResolveTameOwner( ctx, out var ownerRoot ) || !ownerRoot.IsValid() )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Idle, "tamed-no-owner" );
			return;
		}

		ctx.OwnerPlayer = ownerRoot;

		if ( !def.AllowPlayerMount )
		{
			var ownDistSq = ( ownerRoot.WorldPosition.WithZ( 0 ) - selfFlat ).LengthSquared;
			if ( ownDistSq < 360f * 360f && Time.Now >= ctx.NextTameOwnerNearUnstickRealtime )
			{
				ctx.NextTameOwnerNearUnstickRealtime = Time.Now + 0.28;
				ThornsWildlifeMountHost.HostUnstickPawnOrphanedWildlifeParent( ownerRoot );
			}
		}

		var ownerHp = ownerRoot.Components.Get<ThornsHealth>();
		if ( ownerHp.IsValid() && ( !ownerHp.IsAlive || ownerHp.IsDeadState ) )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Idle, "tamed-owner-dead" );
			return;
		}

		var ownerFlat = ownerRoot.WorldPosition.WithZ( 0 );
		var reach = Math.Max( 95f, def.AttackRange );

		if ( !def.IsPredator
		     && ThornsAnimalTargetingService.TryResolveRecentAttacker( ctx, out var fleeAttacker )
		     && ThornsAnimalThreatPipeline.TryBeginFleeFromAttacker(
			     ctx,
			     selfFlat,
			     fleeAttacker,
			     machine,
			     "tamed-prey-flee" ) )
			return;

		var threat = ThornsAnimalTargetingService.ResolveTamedCombatTarget( ctx, ownerFlat, selfFlat );
		if ( ctx.Combat.IsValid() && threat.IsValid()
		     && !ThornsAnimalTargetingService.HostIsForbiddenTameCombatTarget( id, threat ) )
		{
			ctx.FocusTarget = threat;
			ctx.CurrentTarget = threat;
			var distThreat = ( threat.WorldPosition.WithZ( 0 ) - selfFlat ).Length;

			if ( distThreat <= reach )
			{
				machine.TryTransition( ctx, ThornsWildlifeAiState.Attack, "tamed-attack" );
				if ( def.MeleeDamage > 0.01f )
					ctx.Combat.HostTryMeleeAttack( def, threat );
				else
					ctx.Combat.HostTryTamedAssistBite( def, threat, ThornsWildlifeCombat.TamedAssistFallbackDamage );
				return;
			}

			machine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "tamed-hunt" );
			return;
		}

		if ( !id.TameFollowOwnerSync )
		{
			ctx.Brain.HostRequestPassiveLocomotion( ctx, machine, def );
			ctx.Brain.RunPassiveLocomotionThink( ctx, def, selfFlat, machine );
			return;
		}

		var followRadius = ctx.Brain.HostPreferredTameFollowRadius( def, ownerRoot );
		ctx.Brain.UpdateTameFollowLocomotionState( ctx, id, def, selfFlat, ownerRoot, followRadius, machine );
	}

	public static void ThinkTamedDefensive(
		ThornsAnimalBrainContext ctx,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		if ( !ThornsAnimalTargetingService.TryResolveTameOwner( ctx, out var ownerRoot ) || !ownerRoot.IsValid() )
			return;

		ctx.OwnerPlayer = ownerRoot;
		var ownerFlat = ownerRoot.WorldPosition.WithZ( 0 );
		var reach = Math.Max( 95f, ctx.Definition.AttackRange );
		var threat = ThornsAnimalTargetingService.ResolveGuardCombatTarget( ctx, ownerFlat, selfFlat );

		if ( !ctx.Combat.IsValid() || !threat.IsValid()
		     || ThornsAnimalTargetingService.HostIsForbiddenTameCombatTarget( ctx.Identity, threat ) )
			return;

		ctx.FocusTarget = threat;
		ctx.CurrentTarget = threat;
		var distThreat = ( threat.WorldPosition.WithZ( 0 ) - selfFlat ).Length;

		if ( distThreat <= reach )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Attack, "guard-attack" );
			if ( ctx.Definition.MeleeDamage > 0.01f )
				ctx.Combat.HostTryMeleeAttack( ctx.Definition, threat );
			else
				ctx.Combat.HostTryTamedAssistBite(
					ctx.Definition,
					threat,
					ThornsWildlifeCombat.TamedAssistFallbackDamage );
		}
		else
			machine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "guard-hunt" );
	}

	public static bool TryBeginPredatorHuntFromWander(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeDirector director,
		Vector3 selfFlat,
		ThornsAnimalStateMachine machine )
	{
		if ( !ctx.Definition.IsPredator || ctx.Identity.HostIsTamed )
			return false;

		ThinkPredator( ctx, director, selfFlat, machine );
		return ctx.CurrentState is ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Attack
		       or ThornsWildlifeAiState.Chase or ThornsWildlifeAiState.Stalk;
	}
}
