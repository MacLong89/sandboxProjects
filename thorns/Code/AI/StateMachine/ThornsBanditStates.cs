namespace Sandbox;

public static class ThornsBanditStates
{
	public static void RegisterAll( ThornsBanditStateMachine machine )
	{
		machine.Register( new ThornsBanditIdleState() );
		machine.Register( new ThornsBanditPatrolState() );
		machine.Register( new ThornsBanditRoamState() );
		machine.Register( new ThornsBanditInvestigateState() );
		machine.Register( new ThornsBanditAlertState() );
		machine.Register( new ThornsBanditSeekCoverState() );
		machine.Register( new ThornsBanditAttackState() );
		machine.Register( new ThornsBanditChaseState() );
		machine.Register( new ThornsBanditSearchState() );
		machine.Register( new ThornsBanditReturnHomeState() );
		machine.Register( new ThornsBanditFleeState() );
		machine.Register( new ThornsBanditDeadState() );
	}
}

public sealed class ThornsBanditIdleState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Idle;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineStopMovement( ctx );

		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		if ( ThornsBanditDetectionSystem.TryRefreshHearing( ctx, selfFlat, out _ ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Investigate, "idle->investigate" );
			return;
		}

		var next = ctx.Archetype.Type switch
		{
			ThornsBanditType.Scavenger => ThornsBanditAiState.Roam,
			_ => ThornsBanditAiState.Patrol,
		};
		ctx.Brain.StateMachine.TryTransition( ctx, next, "idle->default" );
	}
}

public sealed class ThornsBanditPatrolState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Patrol;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineShouldSeekCover( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.SeekCover, "patrol->cover" );
			return;
		}

		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		if ( ThornsBanditDetectionSystem.TryRefreshHearing( ctx, selfFlat, out _ ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Investigate, "patrol->investigate" );
			return;
		}

		if ( ctx.Brain.StateMachineIsTooFarFromHome( ctx, selfFlat ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.ReturnHome, "patrol->return" );
			return;
		}

		ctx.Brain.StateMachineTickPatrolMovement( ctx, selfFlat );
	}
}

public sealed class ThornsBanditRoamState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Roam;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineShouldFlee( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Flee, "roam->flee" );
			return;
		}

		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		if ( ThornsBanditDetectionSystem.TryRefreshHearing( ctx, selfFlat, out _ ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Investigate, "roam->investigate" );
			return;
		}

		if ( ctx.Brain.StateMachineIsTooFarFromHome( ctx, selfFlat ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.ReturnHome, "roam->return" );
			return;
		}

		ctx.Brain.StateMachineTickRoamMovement( ctx, selfFlat );
	}
}

public sealed class ThornsBanditInvestigateState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Investigate;

	public override void OnEnter( ThornsBanditBrainContext ctx )
	{
		ctx.InvestigateUntilRealtime = Time.Now + ctx.Archetype.InvestigateTimeoutSeconds;
		ctx.AlertLevel = Math.Max( ctx.AlertLevel, 1 );
	}

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		var toPoint = ctx.InvestigatePoint.WithZ( 0 ) - selfFlat;
		if ( toPoint.Length > 48f )
			ctx.Brain.StateMachineMoveToward( ctx, ctx.InvestigatePoint, ctx.Brain.ChaseSpeed * 0.75f );
		else
			ctx.Brain.StateMachineStopMovement( ctx );

		if ( Time.Now > ctx.InvestigateUntilRealtime )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Search, "investigate->search" );
	}
}

public sealed class ThornsBanditAlertState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Alert;

	public override void OnEnter( ThornsBanditBrainContext ctx )
	{
		var min = ctx.Archetype.ReactionTimeMinSeconds;
		var max = ctx.Archetype.ReactionTimeMaxSeconds;
		ctx.AlertReactionUntilRealtime = Time.Now + Random.Shared.NextDouble() * ( max - min ) + min;
		ctx.AlertLevel = Math.Max( ctx.AlertLevel, 2 );

		if ( ctx.CurrentTarget.IsValid() )
		{
			ctx.LastKnownTargetPosition = ctx.CurrentTarget.WorldPosition;
			ThornsBanditGroupAlertSystem.BroadcastAlert( ctx, ctx.CurrentTarget, ctx.LastKnownTargetPosition );
		}
	}

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		ctx.Brain.StateMachineStopMovement( ctx );

		if ( ctx.CurrentTarget.IsValid() )
			ctx.Brain.HostFaceTarget( ctx.CurrentTarget );

		if ( Time.Now < ctx.AlertReactionUntilRealtime )
			return;

		if ( !ctx.CurrentTarget.IsValid() )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Search, "alert->search" );
			return;
		}

		if ( ctx.Brain.StateMachineShouldSeekCover( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.SeekCover, "alert->cover" );
			return;
		}

		if ( ctx.Brain.StateMachineCanAttackTarget( ctx, ctx.CurrentTarget ) )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Attack, "alert->attack" );
		else
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Chase, "alert->chase" );
	}
}

public sealed class ThornsBanditSeekCoverState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.SeekCover;

	public override void OnEnter( ThornsBanditBrainContext ctx ) =>
		ctx.CoverPoint = ctx.Brain.StateMachinePickCoverPoint( ctx );

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Archetype.CanFlee && ctx.Brain.StateMachineShouldFlee( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Flee, "cover->flee" );
			return;
		}

		var toCover = ctx.CoverPoint.WithZ( 0 ) - selfFlat;
		if ( toCover.Length > 36f )
		{
			ctx.Brain.StateMachineMoveToward( ctx, ctx.CoverPoint, ctx.Brain.ChaseSpeed );
			return;
		}

		if ( ctx.CurrentTarget.IsValid() && ctx.Brain.StateMachineCanAttackTarget( ctx, ctx.CurrentTarget ) )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Attack, "cover->attack" );
		else if ( !ctx.CurrentTarget.IsValid() )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Search, "cover->search" );
	}
}

public sealed class ThornsBanditAttackState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Attack;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( !ctx.CurrentTarget.IsValid() || !ctx.Brain.StateMachineIsTargetInteresting( ctx, ctx.CurrentTarget ) )
		{
			ctx.CurrentTarget = default;
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Search, "attack->search" );
			return;
		}

		ctx.LastKnownTargetPosition = ctx.CurrentTarget.WorldPosition;
		ctx.LastSeenTargetRealtime = Time.Now;

		if ( ctx.Brain.StateMachineShouldSeekCover( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.SeekCover, "attack->cover" );
			return;
		}

		if ( ctx.Brain.StateMachineShouldFlee( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Flee, "attack->flee" );
			return;
		}

		if ( !ctx.Brain.StateMachineCanAttackTarget( ctx, ctx.CurrentTarget ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Chase, "attack->chase" );
			return;
		}

		if ( ctx.Brain.StateMachineExceededChaseLimit( ctx, selfFlat ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.ReturnHome, "attack->return" );
			return;
		}

		ctx.Brain.StateMachineStopMovement( ctx );
		ctx.Brain.HostFaceTarget( ctx.CurrentTarget );
		if ( ctx.Combat.IsValid() && ctx.Combat.HostTryShootToward( ctx.CurrentTarget, ctx ) )
			ctx.LastShotRealtime = Time.Now;
	}
}

public sealed class ThornsBanditChaseState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Chase;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( !ctx.CurrentTarget.IsValid() || !ctx.Brain.StateMachineIsTargetInteresting( ctx, ctx.CurrentTarget ) )
		{
			ctx.CurrentTarget = default;
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Search, "chase->search" );
			return;
		}

		if ( ctx.Brain.StateMachineShouldFlee( ctx ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Flee, "chase->flee" );
			return;
		}

		if ( ctx.Brain.StateMachineExceededChaseLimit( ctx, selfFlat ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.ReturnHome, "chase->return" );
			return;
		}

		if ( ctx.Brain.StateMachineCanAttackTarget( ctx, ctx.CurrentTarget ) )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Attack, "chase->attack" );
			return;
		}

		ctx.LastKnownTargetPosition = ctx.CurrentTarget.WorldPosition;
		ctx.Brain.StateMachineMoveToward( ctx, ctx.CurrentTarget.WorldPosition, ctx.Brain.ChaseSpeed, ctx.CurrentTarget );
	}
}

public sealed class ThornsBanditSearchState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Search;

	public override void OnEnter( ThornsBanditBrainContext ctx ) =>
		ctx.SearchUntilRealtime = Time.Now + ctx.Archetype.SearchTimeoutSeconds;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		var searchGoal = ctx.LastKnownTargetPosition;
		if ( searchGoal == default )
			searchGoal = ctx.InvestigatePoint;

		var toGoal = searchGoal.WithZ( 0 ) - selfFlat;
		if ( toGoal.Length > 40f )
			ctx.Brain.StateMachineMoveToward( ctx, searchGoal, ctx.Brain.WanderSpeed );
		else
			ctx.Brain.StateMachineStopMovement( ctx );

		if ( Time.Now > ctx.SearchUntilRealtime )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.ReturnHome, "search->return" );
	}
}

public sealed class ThornsBanditReturnHomeState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.ReturnHome;

	public override void OnEnter( ThornsBanditBrainContext ctx )
	{
		ctx.CurrentTarget = default;
		ctx.AlertLevel = 0;
	}

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( ctx.Brain.StateMachineTryEnterCombatFromDetection( ctx, director, selfFlat ) )
			return;

		var home = ctx.HomePosition != default ? ctx.HomePosition : ctx.SpawnPosition;
		var toHome = home.WithZ( 0 ) - selfFlat;
		var speed = toHome.Length > 400f ? ctx.Brain.ChaseSpeed : ctx.Brain.WanderSpeed;

		if ( toHome.Length > 55f )
		{
			ctx.Brain.StateMachineMoveToward( ctx, home, speed );
			return;
		}

		var next = ctx.Archetype.Type switch
		{
			ThornsBanditType.Scavenger => ThornsBanditAiState.Roam,
			_ => ThornsBanditAiState.Patrol,
		};
		ctx.Brain.StateMachine.TryTransition( ctx, next, "return->default" );
	}
}

public sealed class ThornsBanditFleeState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Flee;

	public override bool CanTransition( ThornsBanditBrainContext ctx, ThornsBanditAiState next ) =>
		ctx.Archetype.CanFlee || next == ThornsBanditAiState.Dead;

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( !ctx.CurrentTarget.IsValid() )
		{
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Roam, "flee->roam" );
			return;
		}

		var away = selfFlat - ctx.CurrentTarget.WorldPosition.WithZ( 0 );
		if ( away.LengthSquared < 4f )
			away = ctx.Brain.GameObject.WorldRotation.Forward.WithZ( 0 );

		var fleeGoal = selfFlat + away.Normal * 600f;
		ctx.Brain.StateMachineMoveToward( ctx, fleeGoal, ctx.Brain.ChaseSpeed );

		if ( ctx.Brain.StateMachineDistanceFlat( ctx.CurrentTarget ) > ctx.Archetype.ChaseMaxDistanceWorld * 1.2f )
			ctx.Brain.StateMachine.TryTransition( ctx, ThornsBanditAiState.Roam, "flee->safe" );
	}
}

public sealed class ThornsBanditDeadState : ThornsBanditStateBase
{
	public override ThornsBanditAiState StateId => ThornsBanditAiState.Dead;

	public override void OnEnter( ThornsBanditBrainContext ctx )
	{
		ctx.IsDead = true;
		ctx.CurrentTarget = default;
		ctx.Brain.StateMachineStopMovement( ctx );
	}

	public override void Tick( ThornsBanditBrainContext ctx, ThornsBanditDirector director, Vector3 selfFlat ) { }
}
