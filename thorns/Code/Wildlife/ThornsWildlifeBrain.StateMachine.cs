namespace Sandbox;



public sealed partial class ThornsWildlifeBrain

{

	ThornsAnimalBrainContext _stateMachineContext;

	ThornsAnimalStateMachine _stateMachine;



	public ThornsAnimalBrainContext StateMachineContext => _stateMachineContext;

	public ThornsAnimalStateMachine StateMachine => _stateMachine;



	void InitStateMachine()

	{

		_stateMachineContext = new ThornsAnimalBrainContext { Brain = this };

		_stateMachine = new ThornsAnimalStateMachine();

		ThornsAnimalStates.RegisterAll( _stateMachine );

		_stateMachine.Register( new ThornsWildlifeReturnToLeashState() );

		SyncContextFromBrainFields();

		_stateMachineContext.BindComponents();

		_stateMachineContext.RefreshBehaviorModeFromDefinition();

		_stateMachine.Initialize( _stateMachineContext, _state );

	}



	void SyncContextFromBrainFields()

	{

		if ( _stateMachineContext is null )

			return;



		_stateMachineContext.CurrentState = _state;

		_stateMachineContext.SpawnFlat = _spawnFlat;
		_stateMachineContext.HomePosition = _spawnFlat;

		_stateMachineContext.WanderGoalFlat = _wanderGoalFlat;

		_stateMachineContext.FocusTarget = _focusTarget;

		_stateMachineContext.PreyFocusBrain = _preyFocus;

		_stateMachineContext.FleeThreatRoot = _fleeThreatRoot;

		_stateMachineContext.FleeWishPlanar = _fleeWishPlanar;

		_stateMachineContext.FleeUntilRealtime = _fleeUntil;

		_stateMachineContext.HuntAbandonAfterRealtime = _huntAbandonAfterRealtime;

		_stateMachineContext.PredatorPeaceUntilRealtime = _predatorPeaceUntilRealtime;

		_stateMachineContext.RecentAttackerRoot = _recentAnimalAttackerRoot;

		_stateMachineContext.RecentAttackerUntilRealtime = _recentAnimalAttackerUntilRealtime;

		_stateMachineContext.DormantPassiveHold = _dormantPassiveHold;

		_stateMachineContext.NextTameOwnerNearUnstickRealtime = _nextTameOwnerNearUnstickRealtime;

		_stateMachineContext.TameWanderGoalPickedAtRealtime = _tameWanderGoalPickedAtRealtime;

		_stateMachineContext.AnimSync = _animSync;

	}



	void SyncBrainFieldsFromContext()

	{

		if ( _stateMachineContext is null )

			return;



		_state = _stateMachineContext.CurrentState;

		_spawnFlat = _stateMachineContext.SpawnFlat;

		_wanderGoalFlat = _stateMachineContext.WanderGoalFlat;

		_focusTarget = _stateMachineContext.FocusTarget;

		_preyFocus = _stateMachineContext.PreyFocusBrain;

		_fleeThreatRoot = _stateMachineContext.FleeThreatRoot;

		_fleeWishPlanar = _stateMachineContext.FleeWishPlanar;

		_fleeUntil = _stateMachineContext.FleeUntilRealtime;

		_huntAbandonAfterRealtime = _stateMachineContext.HuntAbandonAfterRealtime;

		_predatorPeaceUntilRealtime = _stateMachineContext.PredatorPeaceUntilRealtime;

		_recentAnimalAttackerRoot = _stateMachineContext.RecentAttackerRoot;

		_recentAnimalAttackerUntilRealtime = _stateMachineContext.RecentAttackerUntilRealtime;

		_dormantPassiveHold = _stateMachineContext.DormantPassiveHold;

		_nextTameOwnerNearUnstickRealtime = _stateMachineContext.NextTameOwnerNearUnstickRealtime;

		_tameWanderGoalPickedAtRealtime = _stateMachineContext.TameWanderGoalPickedAtRealtime;

	}



	internal void StateMachineHostPrepareContextForState( ThornsAnimalBrainContext ctx, ThornsWildlifeAiState next )

	{

		SyncBrainFieldsFromContext();

		HostPrepareContextForState( next );

		SyncContextFromBrainFields();

	}



	internal void StateMachineHostSyncMotorWishDefault(

		ThornsAnimalBrainContext ctx,

		ThornsWildlifeMotor motor,

		Vector3 selfFlat,

		ThornsWildlifeAiState stateId )

	{

		SyncBrainFieldsFromContext();

		ThornsAnimalMotorWishService.SyncForState( ctx, this, motor, selfFlat, stateId );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunPassiveThink( ThornsAnimalBrainContext ctx, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		RunPassiveLocomotionThink( ctx, ctx.Definition, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunPreyThink( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		ThornsAnimalDecisionService.ThinkPrey( ctx, director, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunPredatorThink( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		ThornsAnimalDecisionService.ThinkPredator( ctx, director, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunTamedThink( ThornsAnimalBrainContext ctx, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		ThornsAnimalDecisionService.ThinkTamed( ctx, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunTamedDefensiveThink( ThornsAnimalBrainContext ctx, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		ThornsAnimalDecisionService.ThinkTamedDefensive( ctx, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

	}



	internal void StateMachineRunFollowTargetThink( ThornsAnimalBrainContext ctx, Vector3 selfFlat, ThornsAnimalFollowTarget follow )
	{
		_ = ctx;
		_ = selfFlat;
		_ = follow;
		// Locomotion is applied only from the active state's SyncMotorWish (fixed tick) — no motor writes during Think.
	}



	internal bool StateMachineIsOutsideLeash( ThornsAnimalBrainContext ctx, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		HostResolveWanderLeashAnchor( ctx.Identity, ctx.Definition, selfFlat, out var anchor, out var r );

		return HostOutsideLeash( selfFlat, anchor, r );

	}



	internal void StateMachinePickWanderGoalNear( ThornsAnimalBrainContext ctx, Vector3 anchor )

	{

		SyncBrainFieldsFromContext();

		PickWanderGoal( ctx.Definition, anchor );

		SyncContextFromBrainFields();

	}



	internal void StateMachineFaceThreat( ThornsAnimalBrainContext ctx, Vector3 selfFlat, GameObject threat )

	{

		var threatFlat = threat.WorldPosition.WithZ( 0 );

		var toThreat = threatFlat - selfFlat;

		if ( toThreat.LengthSquared < 4f )

			return;



		GameObject.WorldRotation = Rotation.LookAt( toThreat.Normal, Vector3.Up );

	}



	internal bool StateMachineTryEnterAlertFromPerception( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		var entered = ThornsAnimalThreatPipeline.TryEnterAlertFromPerception( ctx, director, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

		return entered;

	}



	internal bool StateMachineTryBeginPredatorHunt( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat )

	{

		SyncBrainFieldsFromContext();

		var began = ThornsAnimalDecisionService.TryBeginPredatorHuntFromWander( ctx, director, selfFlat, _stateMachine );

		SyncContextFromBrainFields();

		return began;

	}



	void StateMachineTickActive( ThornsWildlifeDirector director, ThornsWildlifeIdentity id, ThornsWildlifeSpeciesDefinition def, Vector3 flat )

	{

		_stateMachineContext.BindComponents();

		_stateMachineContext.Identity = id;

		_stateMachineContext.Definition = def;

		_stateMachineContext.Motor = Components.Get<ThornsWildlifeMotor>();

		_stateMachineContext.Combat = Components.Get<ThornsWildlifeCombat>();

		_stateMachineContext.SpeedMultiplier = id.GetEffectiveSpeedMultiplier();

		SyncContextFromBrainFields();



		if ( id.HostIsTamed && id.TameRiderConnectionId == Guid.Empty )

		{

			if ( _stateMachine.CurrentStateId is ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Chase

			     or ThornsWildlifeAiState.Attack or ThornsWildlifeAiState.HuntForOwner )

			{

				var focus = _stateMachineContext.FocusTarget;

				if ( !focus.IsValid()

				     || ThornsAnimalTargetingService.HostIsForbiddenTameCombatTarget( id, focus ) )

					_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Follow, "tamed-abort-hostile" );

			}

			if ( id.TameFollowOwnerSync

			     && _stateMachine.CurrentStateId is not (ThornsWildlifeAiState.Follow or ThornsWildlifeAiState.GuardOwner

				     or ThornsWildlifeAiState.Hunt or ThornsWildlifeAiState.Chase or ThornsWildlifeAiState.Attack

				     or ThornsWildlifeAiState.HuntForOwner) )

				_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Follow, "tamed-sync-follow" );

			else if ( !id.TameFollowOwnerSync

			          && _stateMachine.CurrentStateId is ThornsWildlifeAiState.Follow or ThornsWildlifeAiState.Wander )

				_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Stay, "tamed-sync-stay" );

		}



		HostResolveWanderLeashAnchor( id, def, flat, out var leashAnchor, out var leashR );

		if ( HostOutsideLeash( flat, leashAnchor, leashR ) )

		{

			var leashState = id.HostIsTamed ? ThornsWildlifeAiState.Leashed : ThornsWildlifeAiState.ReturnToLeash;

			_stateMachine.TryTransition( _stateMachineContext, leashState, "outside-leash" );

		}



		_stateMachine.Think( _stateMachineContext, director, flat );

		SyncBrainFieldsFromContext();

	}

}



public sealed class ThornsWildlifeReturnToLeashState : ThornsAnimalStateBase

{

	public override ThornsWildlifeAiState StateId => ThornsWildlifeAiState.ReturnToLeash;



	public override void Think( ThornsAnimalBrainContext ctx, ThornsWildlifeDirector director, Vector3 selfFlat ) =>

		ctx.Brain.StateMachineRunPassiveThink( ctx, selfFlat );

}


