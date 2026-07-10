namespace Sandbox;

/// <summary>
/// Server-only wildlife state machine — orchestrates motor/perception/combat (THORNS_EVERYTHING_DOCUMENT §8).
/// </summary>
[Title( "Thorns — Wildlife brain" )]
[Category( "Thorns/Wildlife" )]
[Icon( "psychology" )]
[Order( 12 )]
public sealed partial class ThornsWildlifeBrain : Component
{
	ThornsWildlifeAiState _state = ThornsWildlifeAiState.Wander;
	Vector3 _spawnFlat;
	Vector3 _wanderGoalFlat;
	GameObject _focusTarget;
	ThornsWildlifeBrain _preyFocus;
	/// <summary>Prey flee: last threat root — refreshed in <see cref="OnFixedUpdate"/> so motor keeps full chase speed every tick.</summary>
	GameObject _fleeThreatRoot;

	double _huntAbandonAfterRealtime;
	double _predatorPeaceUntilRealtime;

	Vector3 _fleeWishPlanar;

	double _fleeUntil;
	float _thinkAccum;
	readonly float _thinkJitter;

	ThornsWildlifeLodTier _lastLod = ThornsWildlifeLodTier.Near;
	double _nextLodLogTime;

	double _nextTameOwnerNearUnstickRealtime;
	double _tameWanderGoalPickedAtRealtime;
	bool _dormantPassiveHold;

	GameObject _recentAnimalAttackerRoot;
	double _recentAnimalAttackerUntilRealtime;

	ThornsWildlifeAnimSync _animSync;

	/// <summary>Extra planar gap beyond combined <see cref="CharacterController"/> radii — bite range stays large but motor stops short of overlap.</summary>
	const float MeleeStandoffGapUnits = 16f;

	const float WildlifePeerSeparationGapUnits = 14f;
	const float WildlifePeerSeparationSpeedPerOverlapUnit = 18f;
	const float WildlifePeerSeparationMaxPlanarSpeed = 420f;

	const float RecentAnimalAttackerMemorySeconds = 14f;
	const float FleeFromAnimalAttackerSeconds = 5f;

	[Property] public bool LogChaseDiagnostics { get; set; }

	double _nextChaseDiagLogRealtime;

	public ThornsWildlifeAiState State => _state;

	public ThornsWildlifeBrain()
	{
		_thinkJitter = Random.Shared.NextSingle() * 0.22f;
	}

	protected override void OnStart()
	{
		_spawnFlat = GameObject.WorldPosition.WithZ( 0 );
		_animSync = Components.Get<ThornsWildlifeAnimSync>();
		_animSync?.HostSetAiState( _state );

		InitStateMachine();

		if ( _stateMachineContext is not null )
		{
			_stateMachineContext.SpawnFlat = _spawnFlat;
			_stateMachineContext.HomePosition = _spawnFlat;
		}

		ThornsPopulationDirector.HostRegisterWildlife( this );

		if ( Networking.IsHost )
		{
			var id = Components.Get<ThornsWildlifeIdentity>();
			if ( id.IsValid() && !id.HostIsTamed )
				HostTransitionToPassiveLocomotion( id.Definition );
		}
	}

	protected override void OnDestroy() =>
		ThornsPopulationDirector.HostUnregisterWildlife( this );

	/// <summary>Host: fauna damaged by wildlife, tames, or human NPCs — predators hunt back, herbivores flee.</summary>
	public void HostNotifyDamagedByHostile( GameObject attackerRoot )
	{
		if ( !Networking.IsHost || attackerRoot is null || !attackerRoot.IsValid() )
			return;

		var atk = ThornsTameHostIntel.HostResolveCombatChaseRoot( attackerRoot );
		if ( !atk.IsValid() || atk == GameObject )
			return;

		var atkWild = atk.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var atkBandit = atk.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( !atkWild.IsValid() && !atkBandit.IsValid() )
			return;

		var selfId = Components.Get<ThornsWildlifeIdentity>();
		if ( !selfId.IsValid() )
			return;

		if ( atkWild.IsValid() && ThornsWildlifeIdentity.HostTamesShareOwner( selfId, atkWild ) )
			return;

		if ( selfId.HostIsTamed && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( selfId, atk ) )
			return;

		_recentAnimalAttackerRoot = atk;
		_recentAnimalAttackerUntilRealtime = Time.Now + RecentAnimalAttackerMemorySeconds;
		_dormantPassiveHold = false;

		var def = selfId.Definition;
		var flat = GameObject.WorldPosition.WithZ( 0 );
		var atkFlat = atk.WorldPosition.WithZ( 0 );
		var away = flat - atkFlat;
		_fleeThreatRoot = atk;
		_fleeUntil = Time.Now + FleeFromAnimalAttackerSeconds;
		if ( away.LengthSquared > 4f )
			_fleeWishPlanar = away.Normal * def.ChaseSpeed;
		else
			_fleeWishPlanar = Vector3.Zero;

		if ( def.IsPredator )
		{
			_focusTarget = atk;
			_predatorPeaceUntilRealtime = 0;
			_huntAbandonAfterRealtime = Time.Now + def.HuntCommitSeconds;
			if ( _stateMachine is not null && _stateMachineContext is not null )
			{
				SyncContextFromBrainFields();
				_stateMachineContext.FocusTarget = atk;
				_stateMachineContext.PredatorPeaceUntilRealtime = 0;
				_stateMachineContext.HuntAbandonAfterRealtime = _huntAbandonAfterRealtime;
				_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Hunt, "damage-revenge" );
				SyncBrainFieldsFromContext();
			}
			else
				SetState( ThornsWildlifeAiState.Hunt );

			ThornsWildlifeLog.Target( GameObject.Name, $"revenge:{atk.Name}" );
		}
		else if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			ThornsAnimalThreatPipeline.TryBeginFleeFromAttacker(
				_stateMachineContext,
				flat,
				atk,
				_stateMachine,
				"damage-flee" );
			SyncBrainFieldsFromContext();
		}
		else
			SetState( ThornsWildlifeAiState.Flee );
	}

	/// <summary>Host: drop hunt/revenge/flee memory when bonding completes so taming damage does not carry over.</summary>
	public void HostClearCombatHostilityOnTameBond()
	{
		if ( !Networking.IsHost )
			return;

		_recentAnimalAttackerRoot = null;
		_recentAnimalAttackerUntilRealtime = 0;
		_focusTarget = null;
		_preyFocus = default;
		_fleeThreatRoot = null;
		_fleeWishPlanar = Vector3.Zero;
		_fleeUntil = 0;
		_huntAbandonAfterRealtime = 0;
		_predatorPeaceUntilRealtime = Time.Now + 8f;

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			_stateMachineContext.RecentAttackerRoot = null;
			_stateMachineContext.RecentAttackerUntilRealtime = 0;
			_stateMachineContext.FocusTarget = null;
			_stateMachineContext.CurrentTarget = null;
			_stateMachineContext.FleeThreatRoot = null;
			_stateMachineContext.FleeWishPlanar = Vector3.Zero;
			_stateMachineContext.FleeUntilRealtime = 0;
			_stateMachineContext.PredatorPeaceUntilRealtime = _predatorPeaceUntilRealtime;
			_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.Follow, "tame-bond-clear" );
			SyncBrainFieldsFromContext();
		}
		else
		{
			HostPrepareContextForState( ThornsWildlifeAiState.Follow );
			_state = ThornsWildlifeAiState.Follow;
			_animSync?.HostSetAiState( ThornsWildlifeAiState.Follow );
		}
	}

	bool HostTryResolveRecentHostileAttacker( ThornsWildlifeIdentity selfId, out GameObject attackerRoot )
	{
		attackerRoot = default;
		if ( Time.Now >= _recentAnimalAttackerUntilRealtime )
			return false;

		if ( _recentAnimalAttackerRoot is null || !_recentAnimalAttackerRoot.IsValid() )
			return false;

		var atkWild = _recentAnimalAttackerRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
		var atkBandit = _recentAnimalAttackerRoot.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( !atkWild.IsValid() && !atkBandit.IsValid() )
			return false;

		if ( _recentAnimalAttackerRoot == GameObject )
			return false;

		if ( selfId.IsValid() && atkWild.IsValid() && ThornsWildlifeIdentity.HostTamesShareOwner( selfId, atkWild ) )
			return false;

		if ( selfId.IsValid() && selfId.HostIsTamed
		     && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( selfId, _recentAnimalAttackerRoot ) )
			return false;

		var hp = _recentAnimalAttackerRoot.Components.GetInAncestorsOrSelf<ThornsHealth>( true );
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
			return false;

		attackerRoot = _recentAnimalAttackerRoot;
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var hp = Components.Get<ThornsHealth>();
		// During Destroy teardown ThornsHealth may be torn down before this brain — treat missing hp like dead (don't run TickActive).
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
		{
			SetState( ThornsWildlifeAiState.Dead );
			HostApplyDeathHold( Components.Get<ThornsWildlifeMotor>() );
			return;
		}

		var idStun = Components.Get<ThornsWildlifeIdentity>();
		if ( idStun.IsValid() && ThornsWildlifeMotor.IsInTamingStun( hp, idStun ) )
		{
			HostApplyTamingStunHold( Components.Get<ThornsWildlifeMotor>() );
			return;
		}

		var idMountEarly = Components.Get<ThornsWildlifeIdentity>();
		if ( idMountEarly.IsValid() && idMountEarly.HostIsTamed
		     && !idMountEarly.Definition.AllowPlayerMount && idMountEarly.TameRiderConnectionId != Guid.Empty )
		{
			// Rider sync must only apply to mountable species — otherwise follow AI is skipped and the pawn can stay parented with no dismount path.
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( idMountEarly );
			return;
		}

		if ( idMountEarly.IsValid() && idMountEarly.HostIsTamed && idMountEarly.Definition.AllowPlayerMount
		     && idMountEarly.TameRiderConnectionId != Guid.Empty )
		{
			HostTickMountedRiderMotorOnly( idMountEarly, idMountEarly.Definition );
			return;
		}

		var director = ThornsWildlifeDirector.Instance;
		if ( director is null || !director.IsValid() )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		var def = id.Definition;
		var flat = GameObject.WorldPosition.WithZ( 0 );
		var nearestPlayerSq = director.HostNearestPlayerDistSq( flat );

		var lod = ThornsWildlifeLOD.ComputeTier( nearestPlayerSq );
		if ( lod != _lastLod && Time.Now >= _nextLodLogTime )
		{
			_nextLodLogTime = Time.Now + 1.6;
			ThornsWildlifeLog.Lod( GameObject.Name, lod, nearestPlayerSq );
			_lastLod = lod;
		}

		var baseTick = BaseThinkSeconds();
		var interval = baseTick * ThornsWildlifeLOD.ThinkIntervalMultiplier( lod );
		interval = Math.Max( 0.08f, interval );

		_thinkAccum += Time.Delta;
		if ( _thinkAccum + _thinkJitter < interval )
			return;

		_thinkAccum = 0;

		if ( lod == ThornsWildlifeLodTier.Dormant && !HostBypassDormantForCommittedChaseOrFlee( id, def ) )
		{
			TickDormant( def, flat );
			return;
		}

		TickActive( director, id, def, flat );
	}

	/// <summary>
	/// Dormant LOD zeros motor wish — but sprinting players quickly exceed Far-tier distance while a hunt/flee is still active.
	/// Keep full AI so lose-radius / abandon logic still runs and motor stays driven (see <see cref="OnFixedUpdate"/>).
	/// </summary>
	bool HostBypassDormantForCommittedChaseOrFlee( ThornsWildlifeIdentity id, ThornsWildlifeSpeciesDefinition def )
	{
		if ( id is null || !id.IsValid() || id.HostIsTamed )
			return false;

		if ( def.IsPredator
		     && (_state == ThornsWildlifeAiState.Hunt || _state == ThornsWildlifeAiState.Attack
		         || _state == ThornsWildlifeAiState.Chase || _state == ThornsWildlifeAiState.Stalk)
		     && _focusTarget is not null
		     && _focusTarget.IsValid() )
			return true;

		if ( !def.IsPredator && _state == ThornsWildlifeAiState.Flee
		     && ( Time.Now < _fleeUntil
		          || (_fleeThreatRoot is not null && _fleeThreatRoot.IsValid()) ) )
			return true;

		return false;
	}

	void MaybeLogChaseDiag( ThornsWildlifeIdentity id, ThornsWildlifeSpeciesDefinition def, ThornsWildlifeMotor motor )
	{
		if ( !LogChaseDiagnostics || !def.IsPredator || id.HostIsTamed || !motor.IsValid() )
			return;
		if ( Time.Now < _nextChaseDiagLogRealtime )
			return;

		_nextChaseDiagLogRealtime = Time.Now + 1.15;
		var sprintRef = ThornsWildlifeVsPlayerBalance.HumanNominalSprintSpeed;
		var mul = sprintRef > 1f ? def.ChaseSpeed / sprintRef : 0f;
		Log.Info(
			$"[Thorns][WildlifeChase] {GameObject.Name} ai={_state} wish={motor.HostDebugWishPlanarLength:F0} vel={motor.HostDebugPlanarVelocityLength:F0} chaseVsSprint={mul:F2} sprintRef={sprintRef:F0}" );
	}

	/// <summary>
	/// Called from <see cref="ThornsWildlifeMotor.OnFixedUpdate"/> before <see cref="CharacterController.Move"/> so chase/flee wishes always apply on the same tick as locomotion (component <see cref="Order"/> cannot desync brain vs motor).
	/// </summary>
	public void HostSyncMotorWishForPhysicsStep( ThornsWildlifeMotor motor )
	{
		if ( !Networking.IsHost || motor is null || !motor.IsValid() )
			return;

		var hp = Components.Get<ThornsHealth>();
		if ( !hp.IsValid() || !hp.IsAlive || hp.IsDeadState )
		{
			HostApplyDeathHold( motor );
			return;
		}

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() )
			return;

		if ( id.HostIsTamed && id.Definition.AllowPlayerMount && id.TameRiderConnectionId != Guid.Empty )
		{
			// Same tick as <see cref="ThornsWildlifeMotor.OnFixedUpdate"/> — mount wish must not rely on <see cref="OnUpdate"/> alone (fixed ticks can run before/outnumber render ticks).
			HostTickMountedRiderMotorOnly( id, id.Definition );
			return;
		}

		if ( !id.HostIsTamed && ThornsWildlifeMotor.IsInTamingStun( hp, id ) )
		{
			HostApplyTamingStunHold( motor );
			return;
		}

		var def = id.Definition;
		var flat = GameObject.WorldPosition.WithZ( 0 );

		if ( _dormantPassiveHold
		     && !( id.HostIsTamed && id.TameFollowOwnerSync && id.TameRiderConnectionId == Guid.Empty ) )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		SyncContextFromBrainFields();
		HostPullAuthoritativeStateFromMachine();
		_stateMachineContext.BindComponents();
		_stateMachine.SyncMotorWish( _stateMachineContext, motor, flat );
		SyncBrainFieldsFromContext();
	}

	void HostPullAuthoritativeStateFromMachine()
	{
		if ( _stateMachine is null )
			return;

		_state = _stateMachine.CurrentStateId;
		if ( _stateMachineContext is not null )
			_stateMachineContext.CurrentState = _state;
	}

	bool HostTryResolveTameOwner( ThornsWildlifeIdentity id, out GameObject ownerRoot ) =>
		TryResolveTameOwnerRoot( id.TameOwnerConnectionId, out ownerRoot )
		|| TryResolveTameOwnerRootByAccountKey( id.TameOwnerAccountKeySync, out ownerRoot );

	/// <summary>Host: taming stun — idle AI, zero wish/velocity so clients play idle locomotion.</summary>
	public void HostApplyTamingStunHold( ThornsWildlifeMotor motor )
	{
		SetState( ThornsWildlifeAiState.Idle );
		_focusTarget = null;
		_fleeThreatRoot = null;
		_fleeWishPlanar = Vector3.Zero;
		motor?.HostSetWishPlanarVelocity( Vector3.Zero );
		motor?.HostHaltPlanarLocomotion();
	}

	/// <summary>Host: death — freeze AI/motor until <see cref="ThornsWildlifeDeathHost"/> destroys the corpse.</summary>
	public void HostApplyDeathHold( ThornsWildlifeMotor motor )
	{
		HostNotifyNearbyAnimalsOfDeathEvent();
		SetState( ThornsWildlifeAiState.Idle );
		_focusTarget = null;
		_fleeThreatRoot = null;
		_fleeWishPlanar = Vector3.Zero;
		motor?.HostSetWishPlanarVelocity( Vector3.Zero );
		motor?.HostHaltPlanarLocomotion();
	}

	internal static Vector3 HostGetOwnerPlanarForward( GameObject ownerRoot )
	{
		if ( ownerRoot is null || !ownerRoot.IsValid() )
			return Vector3.Right;

		var move = ownerRoot.Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
		{
			var vel = move.Velocity.WithZ( 0 );
			if ( vel.Length > 48f )
				return vel.Normal;
		}

		var pc = ownerRoot.Components.Get<PlayerController>();
		if ( pc.IsValid() )
		{
			var vel = pc.Velocity.WithZ( 0 );
			if ( vel.Length > 48f )
				return vel.Normal;
		}

		var fwd = ownerRoot.WorldRotation.Forward.WithZ( 0 );
		return fwd.LengthSquared > 1e-4f ? fwd.Normal : Vector3.Right;
	}

	static float HostGetOwnerPlanarSpeed( GameObject ownerRoot )
	{
		if ( ownerRoot is null || !ownerRoot.IsValid() )
			return 0f;

		var move = ownerRoot.Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
			return move.Velocity.WithZ( 0 ).Length;

		var pc = ownerRoot.Components.Get<PlayerController>();
		return pc.IsValid() ? pc.Velocity.WithZ( 0 ).Length : 0f;
	}

	static float HostGetTameFollowLateralSign( ThornsWildlifeIdentity id )
	{
		if ( id is null || !id.IsValid() || id.WildlifeId == Guid.Empty )
			return 1f;

		return ( id.WildlifeId.GetHashCode() & 1 ) == 0 ? 1f : -1f;
	}

	/// <summary>Trail behind the owner's facing — not a radial slot from the tame (that reads as sprint orbits).</summary>
	static Vector3 HostComputeTameFollowSlotGoal(
		GameObject ownerRoot,
		Vector3 ownerFlat,
		Vector3 selfFlat,
		float preferredRadius,
		ThornsWildlifeIdentity id,
		float ownerPlanarSpeed = 0f )
	{
		var forward = HostGetOwnerPlanarForward( ownerRoot );
		var right = new Vector3( -forward.y, forward.x, 0f );
		if ( right.LengthSquared < 1e-4f )
			right = Vector3.Right;
		else
			right = right.Normal;

		var lateral = right * ( preferredRadius * 0.2f * HostGetTameFollowLateralSign( id ) );
		var toSelf = selfFlat - ownerFlat;
		var distToOwner = toSelf.Length;
		var ownerSprinting = ownerPlanarSpeed > ThornsWildlifeVsPlayerBalance.HumanNominalSprintSpeed * 0.42f;
		var ownerJogging = ownerPlanarSpeed > 52f;

		// Catch-up: steer toward the owner (not a rear slot that swings with facing) when behind or owner is moving.
		if ( distToOwner > preferredRadius * 1.12f
		     || ( ownerJogging && distToOwner > preferredRadius * 0.92f )
		     || ( ownerSprinting && distToOwner > preferredRadius * 0.75f ) )
			return ownerFlat - forward * MathF.Max( 36f, preferredRadius * 0.42f ) + lateral * 0.12f;

		var goal = ownerFlat - forward * MathF.Max( 48f, preferredRadius ) + lateral;

		// Back-slot on the far side of a moving owner — blend closer so followers do not arc around at sprint speed.
		if ( distToOwner > preferredRadius * preferredRadius * 2.25f
		     && Vector3.Dot( toSelf.Normal, forward ) < -0.15f )
			goal = ownerFlat - forward * ( preferredRadius * 0.65f ) + lateral * 0.35f;

		return goal;
	}

	internal float HostPreferredTameFollowRadius( ThornsWildlifeSpeciesDefinition def, GameObject ownerRoot )
	{
		var standoff = HostGetMeleeStandoffPlanarDistance( ownerRoot );
		var bulky = HostTameUsesBulkyFollowSpacing( def.Kind );
		return MathF.Max( standoff * ( bulky ? 1.55f : 1.22f ), bulky ? 130f : 92f );
	}

	/// <summary>Suppress chase velocity snap while following on foot — snap + owner capsule reads as endless sprint orbits.</summary>
	public bool HostShouldSuppressChaseVelocitySnapForTameFollow() =>
		_state == ThornsWildlifeAiState.Follow;

	/// <summary>Followers near the owner used to skip peer separation (caused merge); hull collision handles blocking now.</summary>
	public bool HostShouldSuppressPeerSeparationForTameFollow() => false;

	/// <summary>Peer separation stays on during locomotion so fauna do not merge when hull collision misses a frame.</summary>
	public bool HostShouldSuppressPeerSeparationForMotor() => false;

	/// <summary>Only snap chase velocity when closing from distance — not during wander/leash/flee or melee ring.</summary>
	public bool HostShouldSuppressChaseVelocitySnapForWildlife()
	{
		switch ( _state )
		{
			case ThornsWildlifeAiState.Wander:
			case ThornsWildlifeAiState.Follow:
			case ThornsWildlifeAiState.ReturnToLeash:
			case ThornsWildlifeAiState.Leashed:
			case ThornsWildlifeAiState.Idle:
			case ThornsWildlifeAiState.Stay:
			case ThornsWildlifeAiState.Alert:
			case ThornsWildlifeAiState.Stalk:
			case ThornsWildlifeAiState.Flee:
				return true;
			case ThornsWildlifeAiState.Hunt:
			case ThornsWildlifeAiState.Chase:
			case ThornsWildlifeAiState.Attack:
				if ( _focusTarget is null || !_focusTarget.IsValid() )
					return true;

				var dist = ( _focusTarget.WorldPosition.WithZ( 0 ) - GameObject.WorldPosition.WithZ( 0 ) ).Length;
				var standoff = HostGetMeleeStandoffPlanarDistance( _focusTarget );
				return dist < standoff * 1.85f;
			default:
				return false;
		}
	}

	/// <summary>When closer than follow radius × this and the owner is idle, tames may walk.</summary>
	const float TameFollowWalkOnlyRadiusMul = 1.05f;

	/// <summary>Run catch-up when separation exceeds follow radius × this or the owner is moving faster than walk.</summary>
	const float TameFollowRunCatchUpRadiusMul = 1.08f;

	/// <summary>Owner-centered disk patrol radius (world units) — tames wander anywhere inside this circle.</summary>
	const float TameOwnerDiskWanderRadius = 860f;

	const float TameWanderGoalRepickDist = 60f;
	const float TameWanderArrivalSlowDist = 90f;
	/// <summary>Stay at a wander goal briefly before repicking — stops tight orbit loops from rapid goal churn.</summary>
	const float WanderGoalMinDwellSeconds = 2.4f;
	/// <summary>Sample wander goals below this fraction of leash radius so units do not hug the rim.</summary>
	const float TameWanderInnerDiskBias = 0.88f;
	const float TameLeashEdgeSlowBandFrac = 0.09f;

	float BaseThinkSeconds()
	{
		var idQuick = Components.Get<ThornsWildlifeIdentity>();
		if ( idQuick.IsValid() && idQuick.HostIsTamed )
			return 0.14f;

		return _state switch
		{
			ThornsWildlifeAiState.Attack => 0.11f,
			ThornsWildlifeAiState.Hunt => 0.16f,
			ThornsWildlifeAiState.Chase => 0.14f,
			ThornsWildlifeAiState.Stalk => 0.17f,
			ThornsWildlifeAiState.Flee => 0.13f,
			ThornsWildlifeAiState.Follow => 0.14f,
			ThornsWildlifeAiState.GuardOwner => 0.14f,
			ThornsWildlifeAiState.Alert => 0.18f,
			ThornsWildlifeAiState.Wander => defThinkWander(),
			ThornsWildlifeAiState.ReturnToLeash => 0.2f,
			ThornsWildlifeAiState.Leashed => 0.2f,
			ThornsWildlifeAiState.Patrol => 0.18f,
			_ => 0.85f
		};
	}

	float defThinkWander()
	{
		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( id.IsValid() && !id.HostIsTamed )
			return ThornsAnimalBehaviorProfile.Get( id.Species ).ScanIntervalSeconds;

		var d = id.Definition;
		return (d.IdleSecondsMin + d.IdleSecondsMax) * 0.35f;
	}

	void TickDormant( ThornsWildlifeSpeciesDefinition def, Vector3 flat )
	{
		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( id.IsValid() && id.HostIsTamed && id.TameFollowOwnerSync && id.TameRiderConnectionId == Guid.Empty )
			return;

		_dormantPassiveHold = true;
		HostResolveWanderLeashAnchor( id, def, flat, out var leashAnchor, out var leashR );
		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			if ( HostOutsideLeash( flat, leashAnchor, leashR ) )
			{
				_stateMachine.TryTransition( _stateMachineContext, ThornsWildlifeAiState.ReturnToLeash, "dormant-leash" );
				SyncBrainFieldsFromContext();
				return;
			}

			if ( _state != ThornsWildlifeAiState.Wander && _state != ThornsWildlifeAiState.Follow )
				HostRequestPassiveLocomotion( _stateMachineContext, _stateMachine, def );

			RunPassiveLocomotionThink( _stateMachineContext, def, flat, _stateMachine );
			SyncBrainFieldsFromContext();
			return;
		}

		if ( HostOutsideLeash( flat, leashAnchor, leashR ) )
		{
			SetState( ThornsWildlifeAiState.ReturnToLeash );
			return;
		}

		if ( _state != ThornsWildlifeAiState.Wander && _state != ThornsWildlifeAiState.Follow )
			HostTransitionToPassiveLocomotion( def );

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			RunPassiveLocomotionThink( _stateMachineContext, def, flat, _stateMachine );
			SyncBrainFieldsFromContext();
		}
	}

	void TickActive( ThornsWildlifeDirector director, ThornsWildlifeIdentity id, ThornsWildlifeSpeciesDefinition def, Vector3 flat )
	{
		_dormantPassiveHold = false;
		StateMachineTickActive( director, id, def, flat );
	}

	bool OutsideLeash( ThornsWildlifeSpeciesDefinition def, Vector3 flat ) =>
		HostOutsideLeash( flat, _spawnFlat, def.LeashRadius );

	static bool HostOutsideLeash( Vector3 flat, Vector3 anchorFlat, float leashRadius ) =>
		(flat - anchorFlat).LengthSquared > leashRadius * leashRadius;

	/// <summary>When outside leash, steer toward an interior point — not the rim tangent — to avoid orbit loops.</summary>
	static Vector3 HostLeashReturnInteriorGoal( Vector3 flat, Vector3 anchorFlat, float leashRadius )
	{
		var from = flat - anchorFlat;
		if ( from.LengthSquared < 1e-4f )
			return anchorFlat + Vector3.Right * ( leashRadius * 0.55f );

		return anchorFlat + from.Normal * ( leashRadius * 0.72f );
	}

	bool HostShouldRepickWanderGoal( Vector3 flat )
	{
		if ( (_wanderGoalFlat - flat).LengthSquared >= TameWanderGoalRepickDist * TameWanderGoalRepickDist )
			return false;

		return Time.Now - _tameWanderGoalPickedAtRealtime >= WanderGoalMinDwellSeconds;
	}

	void HostMaybeRepickWanderGoal( ThornsWildlifeSpeciesDefinition def, Vector3 flat, Vector3 leashAnchor )
	{
		if ( !HostShouldRepickWanderGoal( flat ) )
			return;

		PickWanderGoal( def, leashAnchor );
	}

	void HostResolveWanderLeashAnchor(
		ThornsWildlifeIdentity id,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		out Vector3 anchorFlat,
		out float leashRadius )
	{
		anchorFlat = _spawnFlat;
		leashRadius = def.LeashRadius;

		if ( !id.IsValid() || !id.HostIsTamed || !id.TameFollowOwnerSync )
			return;

		if ( TryResolveTameOwnerRoot( id.TameOwnerConnectionId, out var ownerRoot )
		     || TryResolveTameOwnerRootByAccountKey( id.TameOwnerAccountKeySync, out ownerRoot ) )
		{
			anchorFlat = ownerRoot.WorldPosition.WithZ( 0 );
			leashRadius = GetTameOwnerDiskRadius( def );
		}
	}

	/// <summary>
	/// While ridden, drive motor from rider steer (throttled mount steer RPCs + host timeout decay) — also invoked from <see cref="HostSyncMotorWishForPhysicsStep"/> so velocity matches <see cref="ThornsWildlifeSpeciesDefinition.ChaseSpeed"/> in <see cref="ThornsWildlifeMotor.OnFixedUpdate"/>.
	/// </summary>
	void HostTickMountedRiderMotorOnly( ThornsWildlifeIdentity id, ThornsWildlifeSpeciesDefinition def )
	{
		var motor = Components.Get<ThornsWildlifeMotor>();
		if ( !def.AllowPlayerMount )
		{
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( id );
			motor?.HostSetWishPlanarVelocity( Vector3.Zero );
			SetState( ThornsWildlifeAiState.Idle );
			return;
		}

		if ( !ThornsWildlifeMountHost.HostTryResolvePawnRootForConnection( GameObject.Scene, id.TameRiderConnectionId, out var riderRoot )
		     || !riderRoot.IsValid() )
		{
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( id );
			motor?.HostSetWishPlanarVelocity( Vector3.Zero );
			SetState( ThornsWildlifeAiState.Idle );
			return;
		}

		var riderHp = riderRoot.Components.Get<ThornsHealth>();
		if ( riderHp.IsValid() && ( !riderHp.IsAlive || riderHp.IsDeadState ) )
		{
			ThornsWildlifeMountHost.HostDismountRiderFromWildlife( id );
			motor?.HostSetWishPlanarVelocity( Vector3.Zero );
			SetState( ThornsWildlifeAiState.Idle );
			return;
		}

		var steer = id.HostMountSteerPlanar;
		if ( Time.Now - id.HostLastMountSteerReceiveTime > ThornsPerformanceBudgets.MountInputHostReceiveTimeoutSeconds )
			steer = Vector3.Zero;

		ThornsWildlifeMountHost.LocalSyncRiderMountPresentation( riderRoot, id );

		// Mount uses species top run speed (chase/flee design speed), not tame level speed multipliers.
		var spd = def.ChaseSpeed;
		var wishVel = steer.LengthSquared > 1e-6f ? steer.Normal * spd : Vector3.Zero;
		motor?.HostSetWishPlanarVelocity( wishVel );
		SetState( wishVel.LengthSquared > 100f ? ThornsWildlifeAiState.Mounted : ThornsWildlifeAiState.Mounted );
	}

	/// <summary>Host: make a stay command patrol around the creature's current position, not its original spawn.</summary>
	public void HostResetPassiveLeashAnchorToCurrentPosition()
	{
		if ( !Networking.IsHost )
			return;

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( !id.IsValid() )
			return;

		_spawnFlat = GameObject.WorldPosition.WithZ( 0 );
		PickWanderGoal( id.Definition, _spawnFlat );
	}

	/// <summary>Walk only when parked in the follow slot; run at chase/sprint speed when catching up.</summary>
	static float HostComputeTameFollowPlanarSpeed(
		ThornsWildlifeSpeciesDefinition def,
		ThornsWildlifeIdentity id,
		float spdMul,
		float planarDistFromOwner,
		float preferredFollowRadius,
		float ownerPlanarSpeed )
	{
		var packMul = ThornsTameBondPerks.FollowPackStrideSpeedMul( id, id.GameObject.Scene );
		var walk = Math.Clamp(
			def.WanderSpeed * 1.12f,
			80f * ThornsWildlifeVsPlayerBalance.WildlifeLocomotionGlobalSpeedMul,
			def.WanderSpeed * 1.28f ) * spdMul * packMul;
		var run = def.ChaseSpeed * spdMul * packMul;
		var ownerSprint = ThornsWildlifeVsPlayerBalance.HumanNominalSprintSpeed;
		var catchUp = MathF.Max( run, ownerSprint * 1.06f * spdMul * packMul );

		var closeRadius = MathF.Max( 72f, preferredFollowRadius * TameFollowWalkOnlyRadiusMul );
		var ownerIdle = ownerPlanarSpeed < 32f;

		if ( planarDistFromOwner <= closeRadius && ownerIdle )
			return walk;

		if ( planarDistFromOwner <= preferredFollowRadius * TameFollowRunCatchUpRadiusMul && ownerIdle )
			return MathF.Max( walk * 1.35f, catchUp * 0.7f );

		return catchUp;
	}

	public static bool HostTameUsesBulkyFollowSpacing( ThornsWildlifeSpeciesKind species ) =>
		species is ThornsWildlifeSpeciesKind.Panther
			or ThornsWildlifeSpeciesKind.Wolf
			or ThornsWildlifeSpeciesKind.Cougar
			or ThornsWildlifeSpeciesKind.Bear
			or ThornsWildlifeSpeciesKind.Bison
			or ThornsWildlifeSpeciesKind.Moose
			or ThornsWildlifeSpeciesKind.Elk;

	static float GetTameOwnerDiskRadius( ThornsWildlifeSpeciesDefinition def ) =>
		MathF.Min( TameOwnerDiskWanderRadius, MathF.Max( 120f, def.WanderRadius ) );

	/// <summary>Arrival steering + optional owner-disk rim slowdown (steer to rim, not through owner when outside leash).</summary>
	void HostSteerPlanarWithArrival(
		ThornsWildlifeMotor motor,
		Vector3 flat,
		Vector3 goalFlat,
		float speed,
		float arrivalDist,
		Vector3? leashAnchor = null,
		float leashRadius = 0f,
		bool applyLeashEdgeClamps = true )
	{
		if ( motor is null || !motor.IsValid() )
			return;

		if ( applyLeashEdgeClamps && leashAnchor is { } anchor && leashRadius > 4f )
		{
			var fromAnchor = flat - anchor;
			var distFromAnchor = fromAnchor.Length;
			var maxR = leashRadius;
			var goalFromAnchor = goalFlat - anchor;
			if ( distFromAnchor > maxR && fromAnchor.LengthSquared > 4f )
				goalFlat = HostLeashReturnInteriorGoal( flat, anchor, maxR );
			else if ( goalFromAnchor.Length > maxR * 0.94f )
				goalFlat = HostLeashReturnInteriorGoal( flat, anchor, maxR );
			else if ( distFromAnchor > maxR * ( 1f - TameLeashEdgeSlowBandFrac ) )
			{
				var band = maxR * TameLeashEdgeSlowBandFrac;
				var edgeT = ( distFromAnchor - maxR * ( 1f - TameLeashEdgeSlowBandFrac ) ) / Math.Max( 1f, band );
				speed *= 1f - 0.78f * Math.Clamp( edgeT, 0f, 1f );
			}
		}

		var delta = goalFlat - flat;
		var dist = delta.Length;
		if ( dist < 28f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var slowOuter = MathF.Max( arrivalDist * 2.1f, arrivalDist + 45f );
		if ( dist < slowOuter )
		{
			var t = Math.Clamp(
				( dist - arrivalDist * 0.35f ) / MathF.Max( 14f, slowOuter - arrivalDist * 0.35f ),
				0f,
				1f );
			speed *= t;
		}

		if ( speed < 8f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		motor.HostSetWishPlanarVelocity( delta / dist * speed );
	}

	/// <summary>Orbit breaker disabled — radial/inward pulses fought arrival steering and amplified CC orbit loops.</summary>
	bool HostTryApplyPlanarOrbitBreak(
		ThornsWildlifeMotor motor,
		Vector3 flat,
		Vector3 anchorFlat,
		Vector3 goalFlat,
		float speed,
		float breakRadius ) => false;

	/// <summary>Untamed predator wildlife near the owner — valid protect targets for tamed fighters.</summary>
	internal GameObject HostFindNearestHostilePredatorNearOwner( Vector3 ownerFlat, ThornsWildlifeIdentity selfId, float radiusFromOwner )
	{
		var best = float.MaxValue;
		GameObject pick = null;
		var r2 = radiusFromOwner * radiusFromOwner;

		foreach ( var brain in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
		{
			if ( !brain.IsValid() || brain.GameObject == GameObject )
				continue;

			var oid = brain.Components.Get<ThornsWildlifeIdentity>();
			if ( !oid.IsValid() || !oid.Definition.IsPredator )
				continue;

			if ( oid.HostIsTamed && ThornsWildlifeIdentity.HostTamesShareOwner( selfId, oid ) )
				continue;

			var oh = brain.Components.Get<ThornsHealth>();
			if ( oh.IsValid() && ( !oh.IsAlive || oh.IsDeadState ) )
				continue;

			var d = ( brain.GameObject.WorldPosition.WithZ( 0 ) - ownerFlat ).LengthSquared;
			if ( d > r2 || d >= best )
				continue;

			best = d;
			pick = brain.GameObject;
		}

		return pick;
	}

	internal bool TryResolveTameOwnerRoot( Guid ownerConnectionId, out GameObject root )
	{
		root = default;
		if ( ownerConnectionId == Guid.Empty )
			return false;

		if ( !ThornsPawnConnectionIndex.TryGetByOwnerId( ownerConnectionId, out var pawn ) || !pawn.IsValid() )
			return false;

		var go = pawn.GameObject;
		if ( !go.IsValid() )
			return false;

		root = go;
		return true;
	}

	internal bool TryResolveTameOwnerRootByAccountKey( string accountKey, out GameObject root )
	{
		root = default;
		if ( string.IsNullOrEmpty( accountKey ) )
			return false;

		if ( !ThornsPawnConnectionIndex.TryGetByAccountKey( accountKey, out var pawn ) || !pawn.IsValid() )
			return false;

		var go = pawn.GameObject;
		if ( !go.IsValid() )
			return false;

		root = go;
		return true;
	}

	static float HostGetTargetPlanarCollisionRadius( GameObject targetRoot )
	{
		if ( targetRoot is null || !targetRoot.IsValid() )
			return 12.8f;

		var move = targetRoot.Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
			return Math.Max( 8f, move.CollisionRadius );

		var cc = targetRoot.Components.Get<CharacterController>();
		if ( cc.IsValid() )
			return Math.Max( 8f, cc.Radius );

		return 12.8f;
	}

	float HostGetSelfPlanarCollisionRadius()
	{
		var cc = Components.Get<CharacterController>();
		return cc.IsValid() ? Math.Max( 8f, cc.Radius ) : ThornsWildlifeMotor.DefaultCapsuleRadius;
	}

	/// <summary>Another fauna capsule is intersecting ours in the horizontal plane.</summary>
	public bool HostIsPenetratingWildlifePeer()
	{
		HostEvaluateWildlifePeerSeparation( out _, out var penetrating );
		return penetrating;
	}

	/// <summary>Planar push added to motor wish so fauna do not visually merge when sharing paths.</summary>
	public bool HostTryGetWildlifePeerSeparationWish( out Vector3 separationPlanar )
	{
		HostEvaluateWildlifePeerSeparation( out separationPlanar, out _ );
		return separationPlanar.LengthSquared >= 4f;
	}

	/// <summary>Single spatial query for motor physics — separation wish + overlap flag.</summary>
	public void HostGetPeerSeparationForMotor( out Vector3 separationPlanar, out bool penetratingPeer ) =>
		HostEvaluateWildlifePeerSeparation( out separationPlanar, out penetratingPeer );

	void HostEvaluateWildlifePeerSeparation( out Vector3 separationPlanar, out bool penetrating )
	{
		separationPlanar = Vector3.Zero;
		penetrating = false;
		if ( !Networking.IsHost )
			return;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var selfR = HostGetSelfPlanarCollisionRadius();
		var queryRadius = selfR
		                  + ThornsPerformanceBudgets.HostWildlifePeerMaxPlanarRadius
		                  + WildlifePeerSeparationGapUnits;

		var peers = ThornsPopulationDirector.HostBorrowWildlifePeerQueryScratch();
		ThornsPopulationDirector.HostQueryWildlifePeersNearPlanar( flat, queryRadius, peers, this );

		for ( var pi = 0; pi < peers.Count; pi++ )
		{
			var other = peers[pi];
			if ( !other.IsValid() )
				continue;

			var otherHp = other.Components.Get<ThornsHealth>();
			if ( otherHp.IsValid() && ( !otherHp.IsAlive || otherHp.IsDeadState ) )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + WildlifePeerSeparationGapUnits;
			if ( dist >= minDist - 0.5f )
				continue;

			penetrating = true;

			Vector3 away;
			if ( dist > 1e-3f )
				away = delta / dist;
			else
			{
				var jitter = new Vector3(
					(float)Math.Cos( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.71 ),
					(float)Math.Sin( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.71 ),
					0f );
				away = jitter.LengthSquared > 1e-4f ? jitter.Normal : Vector3.Right;
			}

			var overlap = minDist - Math.Max( dist, 0f );
			var push = Math.Min(
				WildlifePeerSeparationMaxPlanarSpeed,
				overlap * WildlifePeerSeparationSpeedPerOverlapUnit );
			separationPlanar += away * push;
		}

		if ( separationPlanar.Length > WildlifePeerSeparationMaxPlanarSpeed )
			separationPlanar = separationPlanar.Normal * WildlifePeerSeparationMaxPlanarSpeed;
	}

	/// <summary>Hard positional depenetration after CC move when hull blocking still leaves overlap.</summary>
	public Vector3 HostComputeWildlifePeerDepenetrationStep()
	{
		if ( !Networking.IsHost )
			return Vector3.Zero;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var selfR = HostGetSelfPlanarCollisionRadius();
		var queryRadius = selfR
		                  + ThornsPerformanceBudgets.HostWildlifePeerMaxPlanarRadius
		                  + WildlifePeerSeparationGapUnits;

		var peers = ThornsPopulationDirector.HostBorrowWildlifePeerQueryScratch();
		ThornsPopulationDirector.HostQueryWildlifePeersNearPlanar( flat, queryRadius, peers, this );

		var total = Vector3.Zero;
		for ( var pi = 0; pi < peers.Count; pi++ )
		{
			var other = peers[pi];
			if ( !other.IsValid() )
				continue;

			var otherHp = other.Components.Get<ThornsHealth>();
			if ( otherHp.IsValid() && ( !otherHp.IsAlive || otherHp.IsDeadState ) )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + WildlifePeerSeparationGapUnits;
			if ( dist >= minDist - 0.25f )
				continue;

			Vector3 away;
			if ( dist > 1e-3f )
				away = delta / dist;
			else
			{
				var jitter = new Vector3(
					(float)Math.Cos( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.53 ),
					(float)Math.Sin( ( GameObject.Id.GetHashCode() ^ other.GameObject.Id.GetHashCode() ) * 0.53 ),
					0f );
				away = jitter.LengthSquared > 1e-4f ? jitter.Normal : Vector3.Right;
			}

			var overlap = minDist - Math.Max( dist, 0f );
			total += away * MathF.Min( overlap * 0.55f, 28f );
		}

		return total;
	}

	float HostGetMeleeStandoffPlanarDistance( GameObject targetRoot ) =>
		HostGetSelfPlanarCollisionRadius() + HostGetTargetPlanarCollisionRadius( targetRoot ) + MeleeStandoffGapUnits;

	/// <summary>Chase with arrival slowdown; hold or push away inside standoff so predators do not ram into the player capsule.</summary>
	void HostApplyPredatorMeleeMotorWish(
		ThornsWildlifeMotor motor,
		ThornsWildlifeSpeciesDefinition def,
		Vector3 flat,
		GameObject targetRoot,
		float spdMul,
		float planarDist,
		ThornsWildlifeIdentity selfId = null )
	{
		if ( motor is null || !motor.IsValid() || targetRoot is null || !targetRoot.IsValid() )
			return;

		var targetFlat = targetRoot.WorldPosition.WithZ( 0 );
		if ( selfId.IsValid() )
		{
			var profile = ThornsAnimalBehaviorProfile.Get( selfId.Species );
			if ( profile.PackPreference > 0.45f )
				targetFlat = ThornsAnimalPackCoordinator.ComputeFlankGoal( GameObject, targetRoot, selfId.Species );
		}

		var toTarget = targetFlat - flat;
		var standoff = HostGetMeleeStandoffPlanarDistance( targetRoot );

		if ( planarDist <= def.AttackRange || planarDist < standoff * 1.08f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		if ( toTarget.LengthSquared <= 4f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		var speed = def.ChaseSpeed * spdMul;
		var slowOuter = standoff * 1.5f;
		if ( planarDist < slowOuter )
		{
			var t = Math.Clamp( (planarDist - standoff) / Math.Max( 10f, slowOuter - standoff ), 0f, 1f );
			speed *= t;
		}

		if ( planarDist < standoff * 2.35f && HostTryGetTargetPlanarVelocity( targetRoot, out var targetVel )
		     && targetVel.Length > 42f )
		{
			var toNorm = toTarget.Normal;
			var targetDir = targetVel.Normal;
			var closing = Vector3.Dot( targetDir, -toNorm );
			if ( closing < 0.3f )
			{
				speed *= 0.35f;
				if ( planarDist < standoff * 1.45f )
				{
					motor.HostSetWishPlanarVelocity( Vector3.Zero );
					return;
				}
			}
		}

		if ( speed < 8f )
		{
			motor.HostSetWishPlanarVelocity( Vector3.Zero );
			return;
		}

		motor.HostSetWishPlanarVelocity( toTarget.Normal * speed );
	}

	static bool HostTryGetTargetPlanarVelocity( GameObject targetRoot, out Vector3 planarVel )
	{
		planarVel = Vector3.Zero;
		if ( targetRoot is null || !targetRoot.IsValid() )
			return false;

		var move = targetRoot.Components.Get<ThornsPawnMovement>();
		if ( move.IsValid() )
		{
			planarVel = move.Velocity.WithZ( 0 );
			if ( planarVel.LengthSquared > 16f )
				return true;
		}

		var pc = targetRoot.Components.Get<PlayerController>();
		if ( pc.IsValid() )
		{
			planarVel = pc.Velocity.WithZ( 0 );
			return planarVel.LengthSquared > 16f;
		}

		return false;
	}

	void HostTransitionToPassiveLocomotion( ThornsWildlifeSpeciesDefinition def )
	{
		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			HostRequestPassiveLocomotion( _stateMachineContext, _stateMachine, def );
			SyncBrainFieldsFromContext();
			return;
		}

		var id = Components.Get<ThornsWildlifeIdentity>();
		if ( id.IsValid() && id.HostIsTamed && id.TameFollowOwnerSync && id.TameRiderConnectionId == Guid.Empty )
			SetState( ThornsWildlifeAiState.Follow );
		else if ( id.IsValid() && id.HostIsTamed )
			SetState( ThornsWildlifeAiState.Stay );
		else
			SetState( ThornsWildlifeAiState.Wander );

		PickWanderGoal( def );
	}

	void HostPrepareContextForState( ThornsWildlifeAiState next )
	{
		switch ( next )
		{
			case ThornsWildlifeAiState.Idle:
			case ThornsWildlifeAiState.Wander:
			case ThornsWildlifeAiState.Follow:
			case ThornsWildlifeAiState.ReturnToLeash:
			case ThornsWildlifeAiState.Stay:
			case ThornsWildlifeAiState.Leashed:
			case ThornsWildlifeAiState.GuardArea:
			case ThornsWildlifeAiState.Patrol:
				_focusTarget = null;
				_preyFocus = default;
				_fleeThreatRoot = null;
				_fleeWishPlanar = Vector3.Zero;
				_fleeUntil = 0;
				break;
			case ThornsWildlifeAiState.Flee:
				_focusTarget = null;
				_preyFocus = default;
				break;
			case ThornsWildlifeAiState.Hunt:
			case ThornsWildlifeAiState.Chase:
			case ThornsWildlifeAiState.Stalk:
			case ThornsWildlifeAiState.Attack:
			case ThornsWildlifeAiState.HuntForOwner:
			case ThornsWildlifeAiState.GuardOwner:
				_fleeThreatRoot = null;
				_fleeWishPlanar = Vector3.Zero;
				_fleeUntil = 0;
				break;
			case ThornsWildlifeAiState.Alert:
				break;
			case ThornsWildlifeAiState.Mounted:
			case ThornsWildlifeAiState.Dead:
				break;
		}
	}

	/// <summary>Clears stale targets/goals that belong to a previous state so only the active state drives locomotion.</summary>
	internal void HostClearConflictingLocomotionContext( ThornsWildlifeAiState activeState )
	{
		if ( _stateMachineContext is null )
			return;

		if ( activeState != ThornsWildlifeAiState.FollowLeader )
			_stateMachineContext.LeaderAnimal = default;

		if ( activeState is ThornsWildlifeAiState.Idle or ThornsWildlifeAiState.Stay or ThornsWildlifeAiState.Wander
		     or ThornsWildlifeAiState.Follow or ThornsWildlifeAiState.ReturnToLeash or ThornsWildlifeAiState.Leashed
		     or ThornsWildlifeAiState.Patrol or ThornsWildlifeAiState.GuardArea )
			_stateMachineContext.CurrentTarget = null;

		SyncContextFromBrainFields();
	}

	void PickWanderGoal( ThornsWildlifeSpeciesDefinition def, Vector3? anchorFlat = null )
	{
		var anchor = anchorFlat ?? _spawnFlat;
		var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
		var id = Components.Get<ThornsWildlifeIdentity>();
		var leashR = def.LeashRadius;
		if ( id.IsValid() && id.HostIsTamed && id.TameFollowOwnerSync )
			leashR = MathF.Min( GetTameOwnerDiskRadius( def ), leashR );

		var innerBias = id.IsValid() && id.HostIsTamed ? TameWanderInnerDiskBias * 0.82f : 0.72f;
		var rMax = MathF.Min( def.WanderRadius, leashR * innerBias );
		var r = rMax * MathF.Sqrt( Random.Shared.NextSingle() );
		var offset = new Vector3( MathF.Cos( ang ) * r, MathF.Sin( ang ) * r, 0f );

		_wanderGoalFlat = anchor + offset;
		_tameWanderGoalPickedAtRealtime = Time.Now;
	}

	void SetState( ThornsWildlifeAiState next )
	{
		if ( _state == next )
			return;

		if ( _stateMachine is not null && _stateMachineContext is not null )
		{
			SyncContextFromBrainFields();
			if ( _stateMachine.TryTransition( _stateMachineContext, next ) )
				SyncBrainFieldsFromContext();
			return;
		}

		ThornsWildlifeLog.Transition( GameObject.Name, _state, next );
		HostPrepareContextForState( next );
		_state = next;
		_animSync?.HostSetAiState( next );
	}
}
