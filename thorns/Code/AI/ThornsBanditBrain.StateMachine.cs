namespace Sandbox;

public sealed partial class ThornsBanditBrain
{
	ThornsBanditBrainContext _stateMachineContext;
	ThornsBanditStateMachine _stateMachine;

	[Property] public ThornsBanditType BanditType { get; set; } = ThornsBanditType.Scavenger;
	[Property] public int GroupId { get; set; }

	public ThornsBanditBrainContext StateMachineContext => _stateMachineContext;
	public ThornsBanditStateMachine StateMachine => _stateMachine;

	GameObject _recentDamager;
	double _recentDamagerUntilRealtime;

	void InitStateMachine()
	{
		_stateMachineContext = new ThornsBanditBrainContext { Brain = this };
		_stateMachine = new ThornsBanditStateMachine();
		ThornsBanditStates.RegisterAll( _stateMachine );

		ApplyArchetypeConfig( ResolveArchetypeConfig() );
		SyncContextFromBrainFields();

		var initial = BanditType switch
		{
			ThornsBanditType.Scavenger => ThornsBanditAiState.Roam,
			_ => ThornsBanditAiState.Patrol,
		};
		_stateMachine.Initialize( _stateMachineContext, initial );
	}

	public void ApplyArchetypeConfig( ThornsBanditArchetypeConfig cfg )
	{
		_stateMachineContext ??= new ThornsBanditBrainContext { Brain = this };
		_stateMachineContext.Archetype = cfg;
		BanditType = cfg.Type;

		AggroRadius = cfg.VisionRangeWorld;
		AttackRange = cfg.VisionRangeWorld;
		LoseRadius = MathF.Max( cfg.VisionRangeWorld * 1.15f, cfg.ChaseMaxDistanceWorld * 1.05f );
		WanderRadius = cfg.Type == ThornsBanditType.Scavenger ? cfg.RoamRadiusWorld : cfg.PatrolRadiusWorld;
		LeashRadius = cfg.LeashRadiusWorld;

		var combat = Components.Get<ThornsBanditCombat>();
		if ( combat.IsValid() )
		{
			combat.HitChance = cfg.HitChance;
			combat.ExtraSpreadHalfAngleDegrees = cfg.ExtraSpreadHalfAngleDegrees;
		}
	}

	ThornsBanditArchetypeConfig ResolveArchetypeConfig() => BanditType switch
	{
		ThornsBanditType.CityDefender => ThornsBanditArchetypeConfig.CityDefender(),
		ThornsBanditType.AirdropDefender => ThornsBanditArchetypeConfig.AirdropDefender(),
		_ => ThornsBanditArchetypeConfig.Scavenger(),
	};

	void SyncContextFromBrainFields()
	{
		if ( _stateMachineContext is null )
			return;

		_stateMachineContext.CurrentState = _state;
		_stateMachineContext.WanderGoal = _wanderGoal;
		_stateMachineContext.CurrentTarget = _target;
		_stateMachineContext.SpawnPosition = _spawnWorld;
		_stateMachineContext.HomePosition = UseLeashAnchor ? LeashAnchorWorld : _spawnWorld;
		_stateMachineContext.GroupId = GroupId;
	}

	void SyncBrainFieldsFromContext()
	{
		if ( _stateMachineContext is null )
			return;

		_state = _stateMachineContext.CurrentState;
		_wanderGoal = _stateMachineContext.WanderGoal;
		_target = _stateMachineContext.CurrentTarget;
	}

	internal void StateMachineHostPrepareContextForState( ThornsBanditBrainContext ctx, ThornsBanditAiState next )
	{
		if ( next is ThornsBanditAiState.Search or ThornsBanditAiState.ReturnHome or ThornsBanditAiState.Roam or ThornsBanditAiState.Patrol )
		{
			if ( next != ThornsBanditAiState.Search )
				ctx.CurrentTarget = default;
		}
	}

	void StateMachineTickActive( ThornsBanditDirector director, Vector3 selfFlat )
	{
		_stateMachineContext.BindComponents();
		SyncContextFromBrainFields();

		if ( _stateMachineContext.Health.IsValid()
		     && ( !_stateMachineContext.Health.IsAlive || _stateMachineContext.Health.IsDeadState ) )
		{
			_stateMachine.TryTransition( _stateMachineContext, ThornsBanditAiState.Dead, "health-dead" );
			SyncBrainFieldsFromContext();
			return;
		}

		var nearestDsq = HostNearestCombatInterestDistSq( director, selfFlat, LoseRadius );
		if ( nearestDsq > LoseRadius * LoseRadius )
		{
			_stateMachineContext.CurrentTarget = default;
			_target = default;
			var passive = BanditType == ThornsBanditType.Scavenger
				? ThornsBanditAiState.Roam
				: ThornsBanditAiState.Patrol;
			if ( _state is not (ThornsBanditAiState.Roam or ThornsBanditAiState.Patrol or ThornsBanditAiState.ReturnHome) )
				_stateMachine.TryTransition( _stateMachineContext, passive, "dormant" );

			if ( BanditType == ThornsBanditType.Scavenger )
				StateMachineTickRoamMovement( _stateMachineContext, selfFlat );
			else
				StateMachineTickPatrolMovement( _stateMachineContext, selfFlat );

			SyncBrainFieldsFromContext();
			return;
		}

		_stateMachine.Tick( _stateMachineContext, director, selfFlat );
		SyncBrainFieldsFromContext();
	}

	internal void StateMachineStopMovement( ThornsBanditBrainContext ctx ) =>
		ctx.Motor?.HostSetWishWorld( Vector3.Zero );

	internal void StateMachineMoveToward(
		ThornsBanditBrainContext ctx,
		Vector3 goalWorld,
		float speed,
		GameObject chaseTargetRoot = null )
	{
		SyncBrainFieldsFromContext();
		HostMoveTowardFlat( ClampPlanarToLeash( goalWorld ), speed, chaseTargetRoot );
		SyncContextFromBrainFields();
	}

	internal void StateMachineTickPatrolMovement( ThornsBanditBrainContext ctx, Vector3 selfFlat )
	{
		if ( ctx.PatrolPoints.Count > 0 )
		{
			ctx.PatrolIndex = Math.Clamp( ctx.PatrolIndex, 0, ctx.PatrolPoints.Count - 1 );
			ctx.WanderGoal = ctx.PatrolPoints[ctx.PatrolIndex];
			if ( ( ctx.WanderGoal.WithZ( 0 ) - selfFlat ).Length < 55f )
				ctx.PatrolIndex = ( ctx.PatrolIndex + 1 ) % ctx.PatrolPoints.Count;
		}
		else
		{
			var toW = ctx.WanderGoal.WithZ( 0 ) - selfFlat;
			if ( toW.LengthSquared < 60f * 60f )
			{
				SyncBrainFieldsFromContext();
				PickNewWanderGoal();
				SyncContextFromBrainFields();
			}
		}

		StateMachineMoveToward( ctx, ctx.WanderGoal, WanderSpeed );
	}

	internal void StateMachineTickRoamMovement( ThornsBanditBrainContext ctx, Vector3 selfFlat )
	{
		var toW = ctx.WanderGoal.WithZ( 0 ) - selfFlat;
		if ( toW.LengthSquared < 60f * 60f )
		{
			SyncBrainFieldsFromContext();
			PickNewWanderGoal();
			SyncContextFromBrainFields();
		}

		StateMachineMoveToward( ctx, ctx.WanderGoal, WanderSpeed );
	}

	internal bool StateMachineTryEnterCombatFromDetection(
		ThornsBanditBrainContext ctx,
		ThornsBanditDirector director,
		Vector3 selfFlat )
	{
		if ( !StateMachineTryAcquireVisibleTarget( ctx, director, selfFlat, out var seen ) || !seen.IsValid() )
			return false;

		ctx.CurrentTarget = seen;
		ctx.LastKnownTargetPosition = seen.WorldPosition;
		ctx.LastSeenTargetRealtime = Time.Now;
		return StateMachine.TryTransition( ctx, ThornsBanditAiState.Alert, "detected-target" );
	}

	internal bool StateMachineTryAcquireVisibleTarget(
		ThornsBanditBrainContext ctx,
		ThornsBanditDirector director,
		Vector3 selfFlat,
		out GameObject best )
	{
		best = default;
		SyncBrainFieldsFromContext();
		TryAcquireTarget( director, selfFlat );
		SyncContextFromBrainFields();
		best = ctx.CurrentTarget;

		if ( !best.IsValid() )
			return false;

		var targetFlat = best.WorldPosition.WithZ( 0 );
		if ( !ThornsBanditDetectionSystem.IsInVisionCone(
			     selfFlat,
			     GameObject.WorldRotation,
			     targetFlat,
			     ctx.Archetype.VisionConeDegrees ) )
			return false;

		return StateMachineIsTargetInteresting( ctx, best );
	}

	internal bool StateMachineIsTargetInteresting( ThornsBanditBrainContext ctx, GameObject targetRoot )
	{
		if ( !targetRoot.IsValid() )
			return false;

		var dist = StateMachineDistanceFlat( targetRoot );
		if ( ctx.Archetype.AggressionIgnoreDistanceWorld > 1f
		     && dist > ctx.Archetype.AggressionIgnoreDistanceWorld
		     && _recentDamager != targetRoot )
			return false;

		SyncBrainFieldsFromContext();
		var ok = IsTargetInteresting( targetRoot );
		SyncContextFromBrainFields();
		return ok;
	}

	internal bool StateMachineCanAttackTarget( ThornsBanditBrainContext ctx, GameObject targetRoot )
	{
		if ( !StateMachineIsTargetInteresting( ctx, targetRoot ) )
			return false;

		var dist = StateMachineDistanceFlat( targetRoot );
		return dist <= AttackRange
		       && ThornsBanditPerception.HasClearLos( GameObject, targetRoot, AggroRadius * 1.1f );
	}

	internal bool StateMachineExceededChaseLimit( ThornsBanditBrainContext ctx, Vector3 selfFlat )
	{
		if ( !ctx.CurrentTarget.IsValid() )
			return false;

		var home = ctx.HomePosition != default ? ctx.HomePosition.WithZ( 0 ) : selfFlat;
		var distFromHome = ( selfFlat - home ).Length;
		if ( UseLeashAnchor && distFromHome > LeashRadius * 1.05f )
			return true;

		return StateMachineDistanceFlat( ctx.CurrentTarget ) > ctx.Archetype.ChaseMaxDistanceWorld;
	}

	internal bool StateMachineIsTooFarFromHome( ThornsBanditBrainContext ctx, Vector3 selfFlat )
	{
		var home = ctx.HomePosition != default ? ctx.HomePosition.WithZ( 0 ) : ctx.SpawnPosition.WithZ( 0 );
		var maxR = UseLeashAnchor ? LeashRadius : ctx.Archetype.RoamRadiusWorld;
		return ( selfFlat - home ).LengthSquared > maxR * maxR * 1.08f * 1.08f;
	}

	internal bool StateMachineShouldSeekCover( ThornsBanditBrainContext ctx )
	{
		if ( !ctx.Health.IsValid() || ctx.Health.MaxHealth <= 0.01f )
			return false;

		var frac = ctx.Health.CurrentHealth / ctx.Health.MaxHealth;
		return frac <= ctx.Archetype.SeekCoverHealthFraction;
	}

	internal bool StateMachineShouldFlee( ThornsBanditBrainContext ctx )
	{
		if ( !ctx.Archetype.CanFlee || !ctx.Health.IsValid() || ctx.Health.MaxHealth <= 0.01f )
			return false;

		return ctx.Health.CurrentHealth / ctx.Health.MaxHealth <= ctx.Archetype.FleeHealthFraction;
	}

	internal float StateMachineDistanceFlat( GameObject other ) => DistanceFlat( other );

	internal Vector3 StateMachinePickCoverPoint( ThornsBanditBrainContext ctx )
	{
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		Vector3 away = Vector3.Right;
		if ( ctx.CurrentTarget.IsValid() )
		{
			away = selfFlat - ctx.CurrentTarget.WorldPosition.WithZ( 0 );
			if ( away.LengthSquared < 4f )
				away = GameObject.WorldRotation.Forward.WithZ( 0 );
		}

		var side = Random.Shared.NextDouble() < 0.5 ? 1f : -1f;
		var perp = new Vector3( -away.y, away.x, 0f ).Normal * side;
		var coverDist = Random.Shared.NextSingle() * 80f + 40f;
		return ClampPlanarToLeash( selfFlat + away.Normal * coverDist * 0.35f + perp * coverDist * 0.65f );
	}

	public void HostNotifyRecentDamager( GameObject attackerRoot )
	{
		_recentDamager = attackerRoot;
		_recentDamagerUntilRealtime = Time.Now + 18.0;
	}
}
