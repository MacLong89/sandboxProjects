namespace Sandbox;

/// <summary>All registered animal AI states (wild + tamed).</summary>
public static class ThornsAnimalStates
{
	public static void RegisterAll( ThornsAnimalStateMachine machine )
	{
		machine.Register( new ThornsWildlifeIdleState() );
		machine.Register( new ThornsWildlifeWanderState() );
		machine.Register( new ThornsWildlifeAlertState() );
		machine.Register( new ThornsWildlifeFleeState() );
		machine.Register( new ThornsWildlifeHuntState() );
		machine.Register( new ThornsWildlifeChaseState() );
		machine.Register( new ThornsWildlifeStalkState() );
		machine.Register( new ThornsWildlifeAttackState() );
		machine.Register( new ThornsWildlifeFollowLeaderState() );
		machine.Register( new ThornsWildlifeFollowOwnerState() );
		machine.Register( new ThornsWildlifeLeashedState() );
		machine.Register( new ThornsWildlifeStayState() );
		machine.Register( new ThornsWildlifeGuardOwnerState() );
		machine.Register( new ThornsWildlifeGuardAreaState() );
		machine.Register( new ThornsWildlifePatrolState() );
		machine.Register( new ThornsWildlifeHuntForOwnerState() );
		machine.Register( new ThornsWildlifeMountedState() );
		machine.Register( new ThornsWildlifeDeadState() );
	}
}

public sealed class ThornsWildlifeIdleState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Idle;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunPassiveThink( ctx, selfFlat );
		if ( Random.Shared.NextDouble() < 0.22 )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "idle->wander" );
	}
}

public sealed class ThornsWildlifeWanderState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Wander;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineTryEnterAlertFromPerception( ctx, director, selfFlat ) )
			return;

		if ( ctx.Identity is not null && !ctx.Identity.HostIsTamed && ctx.Definition.IsPredator
		     && ctx.Brain.StateMachineTryBeginPredatorHunt( ctx, director, selfFlat ) )
			return;

		if ( ThornsAnimalPackSystem.TryGetPackLeader( ctx.Brain, out var leader ) )
		{
			ctx.LeaderAnimal = leader;
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.FollowLeader, "wander->pack" );
			return;
		}

		ctx.Brain.StateMachineRunPassiveThink( ctx, selfFlat );
	}
}

public sealed class ThornsWildlifeAlertState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Alert;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		var threat = ctx.CurrentTarget;
		if ( !threat.IsValid() )
			threat = ctx.FocusTarget;

		if ( !threat.IsValid() )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "alert->wander" );
			return;
		}

		ctx.Brain.StateMachineFaceThreat( ctx, selfFlat, threat );
		ctx.RefreshPackAndProfileStats();
		var profile = ctx.BehaviorProfile;
		var dist = ( threat.WorldPosition.WithZ( 0 ) - selfFlat ).Length;

		var threatWild = threat.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		if ( threatWild.IsValid() )
		{
			ctx.LastRelationship = ThornsAnimalRelationshipTable.Resolve(
				ctx.Identity.Species,
				threatWild.Species,
				ctx.NearbyPackMembers );
			ctx.LastRelationshipLabel = ctx.LastRelationship.ToString();
		}

		if ( profile.StandGroundRadius > 1f
		     && ThornsAnimalRelationshipTable.ShouldStandGround( ctx.LastRelationship )
		     && dist <= profile.StandGroundRadius * 1.15f )
		{
			ctx.FocusTarget = threat;
			if ( dist <= profile.ChargeRange )
			{
				ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Attack, "alert->charge" );
				ctx.Combat?.HostTryChargeAttack( ctx.Definition, threat, profile.ChargeDamage );
				return;
			}

			return;
		}

		if ( ctx.Definition.IsPredator && ctx.BehaviorMode is ThornsAnimalBehaviorMode.Predator or ThornsAnimalBehaviorMode.Aggressive )
		{
			ctx.FocusTarget = threat;
			if ( profile.StalkPreference > 0.65f && dist > ctx.Definition.AttackRange * 2.1f )
			{
				ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Stalk, "alert->stalk" );
				return;
			}

			ThornsAnimalPackCoordinator.TryBroadcastPackHunt( ctx.Self, threat );
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "alert->hunt" );
			return;
		}

		if ( !ctx.Definition.IsPredator )
		{
			if ( ThornsAnimalRelationshipTable.ShouldFlee( ctx.LastRelationship )
			     || ( ctx.LastRelationship == ThornsAnimalRelationshipKind.Avoid && dist < ctx.Definition.FearRadius * 0.35f ) )
			{
				ctx.FleeThreatRoot = threat;
				ctx.FleeUntilRealtime = Time.Now + MathF.Max( 2.8f, 2.6f + profile.Fearfulness * 2.2f );
				ThornsAnimalHerdCoordinator.BroadcastFleePanic( ctx.Self, threat );
				ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Flee, "alert->flee" );
				return;
			}
		}

		ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "alert->wander" );
	}
}

public sealed class ThornsWildlifeFleeState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Flee;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineRunPreyThink( ctx, director, selfFlat );
}

public sealed class ThornsWildlifeHuntState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Hunt;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunPredatorThink( ctx, director, selfFlat );

		if ( ctx.FocusTarget is { IsValid: true } t )
		{
			var dist = ( t.WorldPosition.WithZ( 0 ) - selfFlat ).Length;
			if ( dist > ctx.Definition.AttackRange * 1.65f )
				ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Chase, "hunt->chase" );
		}
	}
}

public sealed class ThornsWildlifeChaseState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Chase;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunPredatorThink( ctx, director, selfFlat );

		if ( ctx.FocusTarget is not { IsValid: true } )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "chase->wander" );
			return;
		}

		var dist = ( ctx.FocusTarget.WorldPosition.WithZ( 0 ) - selfFlat ).Length;
		if ( dist <= ctx.Definition.AttackRange * 1.1f )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Attack, "chase->attack" );
		else if ( dist <= ctx.Definition.AttackRange * 1.65f )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "chase->hunt" );
	}
}

public sealed class ThornsWildlifeStalkState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Stalk;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunPredatorThink( ctx, director, selfFlat );

		if ( ctx.FocusTarget is not { IsValid: true } )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "stalk->wander" );
			return;
		}

		var dist = ( ctx.FocusTarget.WorldPosition.WithZ( 0 ) - selfFlat ).Length;
		if ( dist <= ctx.Definition.AttackRange * 1.75f )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Hunt, "stalk->hunt" );
		else if ( dist > ctx.Definition.LoseRadius * 0.95f )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "stalk->wander" );
	}
}

public sealed class ThornsWildlifeAttackState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Attack;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineRunPredatorThink( ctx, director, selfFlat );
}

public sealed class ThornsWildlifeFollowLeaderState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.FollowLeader;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		if ( !ctx.LeaderAnimal.IsValid() )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Wander, "followLeader->wander" );
			return;
		}

		ThornsAnimalPackSystem.InheritLeaderTarget( ctx, ctx.LeaderAnimal );

		var leaderState = ctx.LeaderAnimal.Components.Get<ThornsWildlifeBrain>()?.State;
		if ( leaderState is ThornsWildlifeAiState.Flee )
		{
			ctx.FleeThreatRoot = ctx.LeaderAnimal.Components.Get<ThornsWildlifeBrain>()?.StateMachineContext?.FleeThreatRoot;
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Flee, "followLeader->flee" );
			return;
		}

		if ( leaderState is ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Chase
		     or ThornsWildlifeAiState.Attack or ThornsWildlifeAiState.Stalk )
		{
			var next = leaderState == ThornsWildlifeAiState.Stalk
				? ThornsWildlifeAiState.Stalk
				: ThornsWildlifeAiState.Hunt;
			ctx.Brain.StateMachine.TryTransition( ctx, next, "followLeader->hunt" );
			return;
		}
	}
}

public sealed class ThornsWildlifeFollowOwnerState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Follow;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineRunTamedThink( ctx, selfFlat );
}

public sealed class ThornsWildlifeLeashedState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Leashed;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunPassiveThink( ctx, selfFlat );
		if ( !ctx.Brain.StateMachineIsOutsideLeash( ctx, selfFlat ) )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "leashed->follow" );
	}

	public override void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineHostSyncMotorWishDefault( ctx, motor, selfFlat, StateId );
}

public sealed class ThornsWildlifeStayState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Stay;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		if ( ctx.Identity.TameFollowOwnerSync )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "stay->follow" );
			return;
		}

		ctx.Brain.StateMachineRunTamedDefensiveThink( ctx, selfFlat );
	}

	public override void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat ) =>
		motor.HostSetWishPlanarVelocity( Vector3.Zero );
}

public sealed class ThornsWildlifeGuardOwnerState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.GuardOwner;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) =>
		ctx.Brain.StateMachineRunTamedThink( ctx, selfFlat );
}

public sealed class ThornsWildlifeGuardAreaState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.GuardArea;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineRunTamedDefensiveThink( ctx, selfFlat );
		var home = ctx.HomePosition;
		if ( home == default )
			home = ctx.SpawnFlat;

		if ( ( selfFlat - home ).Length > ctx.Definition.WanderRadius * 1.2f )
			ctx.Brain.StateMachinePickWanderGoalNear( ctx, home );
		else
			ctx.Brain.StateMachineRunPassiveThink( ctx, selfFlat );
	}
}

public sealed class ThornsWildlifePatrolState : ThornsAnimalStateBase
{
	int _patrolIndex;
	int _patrolDir = 1;

	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Patrol;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		if ( ctx.PatrolPoints.Count == 0 )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Stay, "patrol->stay" );
			return;
		}

		_patrolIndex = Math.Clamp( _patrolIndex, 0, ctx.PatrolPoints.Count - 1 );
		var goal = ctx.PatrolPoints[_patrolIndex];
		ctx.WanderGoalFlat = goal;

		if ( ( goal - selfFlat ).Length < 48f )
		{
			if ( ctx.PatrolPoints.Count == 1 )
				return;

			_patrolIndex += _patrolDir;
			if ( _patrolIndex >= ctx.PatrolPoints.Count )
			{
				_patrolIndex = ctx.PatrolPoints.Count - 2;
				_patrolDir = -1;
			}
			else if ( _patrolIndex < 0 )
			{
				_patrolIndex = 1;
				_patrolDir = 1;
			}
		}

		ctx.Brain.StateMachineRunTamedDefensiveThink( ctx, selfFlat );
	}
}

public sealed class ThornsWildlifeHuntForOwnerState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.HuntForOwner;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )
	{
		if ( !ctx.Definition.IsPredator )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsWildlifeAiState.Follow, "huntForOwner->follow" );
			return;
		}

		ctx.Brain.StateMachineRunPredatorThink( ctx, director, selfFlat );
	}
}

public sealed class ThornsWildlifeMountedState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Mounted;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) { }

	public override void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat ) { }
}

public sealed class ThornsWildlifeDeadState : ThornsAnimalStateBase
{
	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.Dead;

	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) { }

	public override void SyncMotorWish( ThornsAnimalBrainContext ctx, ThornsWildlifeMotor motor, Vector3 selfFlat ) =>
		motor.HostSetWishPlanarVelocity( Vector3.Zero );
}
