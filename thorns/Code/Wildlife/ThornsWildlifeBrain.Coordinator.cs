namespace Sandbox;

public sealed partial class ThornsWildlifeBrain
{
	internal bool HostTryResolveTameOwnerInternal( ThornsWildlifeIdentity id, out GameObject ownerRoot ) =>
		TryResolveTameOwnerRoot( id.TameOwnerConnectionId, out ownerRoot )
		|| TryResolveTameOwnerRootByAccountKey( id.TameOwnerAccountKeySync, out ownerRoot );

	internal void HostRequestPassiveLocomotion(
		ThornsAnimalBrainContext ctx,
		ThornsAnimalStateMachine machine,
		ThornsWildlifeSpeciesDefinition def )
	{
		if ( machine is null || ctx is null )
		{
			HostTransitionToPassiveLocomotion( def );
			return;
		}

		var id = ctx.Identity;
		ThornsWildlifeAiState next;
		if ( id.IsValid() && id.HostIsTamed && id.TameFollowOwnerSync && id.TameRiderConnectionId == Guid.Empty )
			next = ThornsWildlifeAiState.Follow;
		else if ( id.IsValid() && id.HostIsTamed )
			next = ThornsWildlifeAiState.Stay;
		else
			next = ThornsWildlifeAiState.Wander;

		machine.TryTransition( ctx, next, "passive-locomotion" );
		PickWanderGoal( def, ctx.SpawnFlat );
	}

	internal void RunPassiveLocomotionThink(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		ThornsAnimalStateMachine machine )
	{
		switch ( ctx.CurrentState )
		{
			case ThornsWildlifeAiState.Hunt:
			case ThornsWildlifeAiState.Attack:
			case ThornsWildlifeAiState.Chase:
			case ThornsWildlifeAiState.Stalk:
				if ( ctx.FocusTarget is null || !ctx.FocusTarget.IsValid() )
					HostRequestPassiveLocomotion( ctx, machine, def );
				break;

			case ThornsWildlifeAiState.ReturnToLeash:
			{
				HostResolveWanderLeashAnchor( ctx.Identity, def, flat, out var leashAnchor, out var leashR );
				if ( !HostOutsideLeash( flat, leashAnchor, leashR ) )
				{
					HostRequestPassiveLocomotion( ctx, machine, def );
					HostMaybeRepickWanderGoal( def, flat, leashAnchor );
				}

				break;
			}

			case ThornsWildlifeAiState.Idle:
				HostRequestPassiveLocomotion( ctx, machine, def );
				break;

			case ThornsWildlifeAiState.Wander:
			case ThornsWildlifeAiState.Follow:
			case ThornsWildlifeAiState.Stay:
			case ThornsWildlifeAiState.GuardArea:
			case ThornsWildlifeAiState.Patrol:
			{
				HostResolveWanderLeashAnchor( ctx.Identity, def, flat, out var leashAnchor, out _ );
				HostMaybeRepickWanderGoal( def, flat, leashAnchor );
				break;
			}
		}
	}

	internal void UpdateTameFollowLocomotionState(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		GameObject ownerRoot,
		float followRadius,
		ThornsAnimalStateMachine machine )
	{
		if ( ownerRoot is null || !ownerRoot.IsValid() )
			return;

		var ownerFlat = ownerRoot.WorldPosition.WithZ( 0 );
		var planarDist = ( ownerFlat - flat ).Length;
		var preferredRadius = followRadius > 48f ? followRadius : HostPreferredTameFollowRadius( def, ownerRoot );
		var standoff = HostGetMeleeStandoffPlanarDistance( ownerRoot );
		var ownerSpeed = HostGetOwnerPlanarSpeed( ownerRoot );
		var ownerMoving = ownerSpeed > 45f;
		var slot = HostComputeTameFollowSlotGoal( ownerRoot, ownerFlat, flat, preferredRadius, id, ownerSpeed );
		var slotDist = ( slot - flat ).Length;
		var closeIdleRadius = preferredRadius * 1.55f;

		if ( id.HostBondedAtRealtime > 0 && Time.Now - id.HostBondedAtRealtime < 6.5f
		     && planarDist < MathF.Max( standoff * 1.15f, 140f ) )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "tame-bond-follow" );
			return;
		}

		if ( planarDist < standoff * 1.08f )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Idle, "tame-close-idle" );
			return;
		}

		if ( !ownerMoving && planarDist < closeIdleRadius )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Idle, "tame-near-idle" );
			return;
		}

		if ( !ownerMoving && slotDist < 72f && planarDist < closeIdleRadius * 1.08f )
		{
			machine.TryTransition( ctx, ThornsWildlifeAiState.Idle, "tame-slot-idle" );
			return;
		}

		machine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "tame-follow" );
	}

	internal void ApplyFleeMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		float spdMul )
	{
		if ( ctx.FleeThreatRoot is not null && ctx.FleeThreatRoot.IsValid() )
		{
			var away = flat - ctx.FleeThreatRoot.WorldPosition.WithZ( 0 );
			motor.HostSetWishPlanarVelocity(
				away.LengthSquared > 4f ? away.Normal * def.ChaseSpeed * spdMul : Vector3.Zero );
			return;
		}

		if ( Time.Now < ctx.FleeUntilRealtime && ctx.FleeWishPlanar.LengthSquared > 4f )
			motor.HostSetWishPlanarVelocity( ctx.FleeWishPlanar );
		else
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
	}

	internal void ApplyFocusChaseMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		float spdMul )
	{
		if ( ctx.FocusTarget is null || !ctx.FocusTarget.IsValid() )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var huntDist = ( ctx.FocusTarget.WorldPosition.WithZ( 0 ) - flat ).Length;
		if ( def.LoseRadius > 1f && huntDist > def.LoseRadius )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		HostApplyPredatorMeleeMotorWish( motor, def, flat, ctx.FocusTarget, spdMul, huntDist, id );
		MaybeLogChaseDiag( id, def, motor );
	}

	internal void ApplyStalkMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		float spdMul )
	{
		if ( ctx.FocusTarget is null || !ctx.FocusTarget.IsValid() )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var profile = ThornsAnimalBehaviorProfile.Get( id.Species );
		var goalFlat = ctx.FocusTarget.WorldPosition.WithZ( 0 );
		if ( profile.PackPreference > 0.45f )
			goalFlat = ThornsAnimalPackCoordinator.ComputeFlankGoal( GameObject, ctx.FocusTarget, id.Species );

		var toGoal = goalFlat - flat;
		if ( toGoal.LengthSquared <= 4f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var speed = def.WanderSpeed * spdMul * MathF.Max( 0.62f, 1f - profile.StalkPreference * 0.28f );
		var wish = toGoal.Normal * speed;
		motor.HostSetWishPlanarVelocity( ThornsAnimalCollisionAvoidance.ComputePeerSeparationWish( GameObject, wish ) );
	}

	internal void ApplyWanderMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		float spdMul )
	{
		HostResolveWanderLeashAnchor( id, def, flat, out var leashAnchor, out var leashR );
		HostMaybeRepickWanderGoal( def, flat, leashAnchor );
		ctx.WanderGoalFlat = _wanderGoalFlat;

		var herdBias = ThornsAnimalHerdCoordinator.ComputeHerdWanderBias( GameObject, id.Species );
		if ( herdBias.LengthSquared > 0.01f )
			ctx.WanderGoalFlat += herdBias * 180f;

		var wanderSpeed = id.HostIsTamed ? def.WanderSpeed * spdMul : def.WanderSpeed;
		if ( HostTryApplyPlanarOrbitBreak( motor, flat, leashAnchor, ctx.WanderGoalFlat, wanderSpeed, leashR * 0.92f ) )
			return;

		HostSteerPlanarWithArrival( motor, flat, ctx.WanderGoalFlat, wanderSpeed, TameWanderArrivalSlowDist, leashAnchor, leashR );
	}

	internal void ApplyReturnToLeashMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		float spdMul )
	{
		HostResolveWanderLeashAnchor( id, def, flat, out var leashAnchor, out var leashR );
		if ( HostOutsideLeash( flat, leashAnchor, leashR ) )
		{
			var returnGoal = HostLeashReturnInteriorGoal( flat, leashAnchor, leashR );
			var returnSpeed = def.WanderSpeed * 0.9f;
			if ( HostTryApplyPlanarOrbitBreak( motor, flat, leashAnchor, returnGoal, returnSpeed, leashR * 0.96f ) )
				return;

			HostSteerPlanarWithArrival(
				motor,
				flat,
				returnGoal,
				returnSpeed,
				TameWanderArrivalSlowDist,
				leashAnchor,
				leashR );
			return;
		}

		motor.HostSetWishPlanarVelocity( Vector3.Zero );
	}

	internal void ApplyFollowMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		GameObject ownerRoot,
		float spdMul )
	{
		if ( motor is null || !motor.IsValid() || ownerRoot is null || !ownerRoot.IsValid() )
			return;

		var ownerFlat = ownerRoot.WorldPosition.WithZ( 0 );
		var planarDist = ( ownerFlat - flat ).Length;
		var preferredRadius = HostPreferredTameFollowRadius( def, ownerRoot );
		var ownerSpeed = HostGetOwnerPlanarSpeed( ownerRoot );
		var speed = HostComputeTameFollowPlanarSpeed( def, id, spdMul, planarDist, preferredRadius, ownerSpeed );
		var standoff = HostGetMeleeStandoffPlanarDistance( ownerRoot );
		var slot = HostComputeTameFollowSlotGoal( ownerRoot, ownerFlat, flat, preferredRadius, id, ownerSpeed );

		if ( id.HostBondedAtRealtime > 0 && Time.Now - id.HostBondedAtRealtime < 6.5f
		     && planarDist < MathF.Max( standoff * 1.15f, 140f ) )
		{
			var away = flat - ownerFlat;
			motor.HostSetWishPlanarVelocity(
				away.LengthSquared > 4f ? away.Normal * speed * 0.65f : Vector3.Zero );
			return;
		}

		if ( HostTryApplyPlanarOrbitBreak( motor, flat, ownerFlat, slot, speed, preferredRadius * 3.6f ) )
			return;

		var arrivalDist = speed >= def.ChaseSpeed * spdMul * 0.65f ? 48f : TameWanderArrivalSlowDist;
		HostSteerPlanarWithArrival(
			motor,
			flat,
			slot,
			speed,
			arrivalDist,
			applyLeashEdgeClamps: false );
	}

	internal void ApplyFollowTargetMotorWish(
		ThornsAnimalBrainContext ctx,
		ThornsWildlifeMotor motor,
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		ThornsAnimalFollowTarget follow,
		float spdMul )
	{
		if ( !follow.IsValid )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var targetFlat = follow.DesiredSlotWorld( flat );
		var leaderFlat = follow.Root.WorldPosition.WithZ( 0 );
		var toTarget = targetFlat - flat;
		var planarDist = toTarget.Length;
		if ( planarDist < follow.DesiredDistanceMin * 0.55f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var speed = def.WanderSpeed * spdMul * 1.22f;
		if ( planarDist > follow.SprintCatchUpDistance )
			speed = MathF.Min( def.ChaseSpeed * spdMul * 0.72f, speed * 1.55f );

		if ( HostTryApplyPlanarOrbitBreak( motor, flat, leaderFlat, targetFlat, speed, follow.DesiredDistanceMax * 2.4f ) )
			return;

		HostSteerPlanarWithArrival( motor, flat, targetFlat, speed, TameWanderArrivalSlowDist, applyLeashEdgeClamps: false );
	}
}
