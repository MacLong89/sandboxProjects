namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.World;

[Title( "Thorns — Bandit Brain" )]
[Category( "Thorns/AI" )]
[Icon( "smart_toy" )]
public sealed class ThornsBanditBrain : Component, IThornsNpcBrain
{
	[Property] public ThornsBanditType BanditType { get; set; } = ThornsBanditType.Scavenger;
	[Property] public int GroupId { get; set; }
	[Property] public bool UseLeashAnchor { get; set; }
	[Property] public Vector3 LeashAnchorWorld { get; set; }
	[Property] public float LeashRadius { get; set; } = 420f;
	[Property] public float WanderRadius { get; set; } = 550f;
	[Property] public bool AnchorWanderGoalsToCurrentPosition { get; set; } = true;

	[Property] public float PatrolSpeed { get; set; } = 145f;
	[Property] public float CombatMoveSpeed { get; set; } = 230f;
	[Property] public float RetreatSpeed { get; set; } = 290f;

	public ThornsBanditArchetypeConfig Archetype { get; private set; } = ThornsBanditArchetypeConfig.Scavenger();
	public ThornsBanditAiState State { get; private set; } = ThornsBanditAiState.Patrol;
	public ThornsBanditMotor Motor { get; private set; }
	public ThornsBanditCombat Combat { get; private set; }

	public string DebugTargetLabel => _target.IsValid() ? _target.Name : "—";

	/// <summary>Current combat focus — used by tamed companion passive defense scans.</summary>
	public GameObject HostCombatTarget => _target;
	public Vector3 DebugLastKnownPosition => _lastKnownPosition;
	public Vector3 DebugCoverGoal => _coverGoal;

	ThornsBanditAiState _state = ThornsBanditAiState.Patrol;
	Vector3 _spawnWorld;
	Vector3 _patrolGoal;
	Vector3 _investigatePoint;
	Vector3 _coverGoal;
	Vector3 _retreatGoal;
	Vector3 _lastKnownPosition;
	GameObject _target;
	GameObject _recentAttacker;
	readonly Vector3[] _recentCover = new Vector3[4];
	int _recentCoverWrite;

	double _targetLockUntilRealtime;
	double _investigateUntilRealtime;
	double _retreatUntilRealtime;
	double _patrolPauseUntilRealtime;
	double _chaseUntilRealtime;
	double _nextVisionTickRealtime;
	double _nextDecisionTickRealtime;
	double _nextPathTickRealtime;
	double _recentDamageUntilRealtime;
	double _lastSeenTargetRealtime;
	double _lastPathTickRealtime;
	Vector3 _smoothFaceTarget;
	bool _wantsSmoothFace;
	float _combatFaceSmoothSpeed = 12f;
	float _engagementRangeWorld;
	float _loseTargetRangeWorld;
	bool _spawnConfigured;
	double _reacquireBlockedUntilRealtime;
	double _nextHearingReactRealtime;
	Vector3 _pendingHearingInvestigatePoint;
	double _pendingHearingInvestigateUntilRealtime;
	float _patrolHeadingBiasDegrees;
	float _lastChaseDistanceSample;
	float _combatStrafeSign = 1f;
	float _strafeSignSmoothed = 1f;
	double _strafeFlipUntilRealtime;
	double _stationarySinceRealtime;
	float _aimErrorYaw;
	float _aimErrorPitch;
	bool _hasReactionTarget;
	bool _combatWantsAds;
	float _patrolLastGoalDist;
	int _patrolNoProgressTicks;
	float _investigateLastGoalDist;
	int _investigateNoProgressTicks;
	bool _investigateSearching;
	Vector3 _investigateSearchAnchor;
	double _investigateSearchStartedRealtime;
	double _stateEnteredRealtime;
	float _engageLastGoalDist;
	int _engageNoProgressTicks;
	int _engageFlankSign = 1;
	ThornsBanditHealth _health;

	const float PatrolArrivalDistance = 40f;
	const float PatrolNearGoalDistance = 52f;
	const float PatrolPauseSeconds = 1f;

	GameObject _pendingAcquireTarget;
	Vector3 _pendingAcquireLastKnown;
	double _pendingAcquireUntilRealtime;
	bool _pendingAcquireBroadcast;

	const float ShootRangeFactor = 0.94f;
	const float EnterChaseRangeFactor = 0.93f;
	const float ExitChaseRangeFactor = 0.82f;
	const float ChaseLeashScale = 2f;

	public ThornsNpcBrainKind Kind => ThornsNpcBrainKind.Bandit;
	public bool IsDead => _state == ThornsBanditAiState.Dead || (_health.IsValid() && _health.IsDeadState);
	public bool IsValidBrain => IsValid;
	public new Vector3 WorldPosition => GameObject.WorldPosition;
	public float BodyRadius => HostGetSelfPlanarCollisionRadius();
	public ThornsNpcLodTier LodTier { get; internal set; } = ThornsNpcLodTier.Full;

	public ThornsBanditBrain()
	{
		_thinkJitter = Random.Shared.NextDouble() * 0.2;
	}

	readonly double _thinkJitter;

	protected override void OnStart()
	{
		if ( UseLeashAnchor && LeashAnchorWorld == default )
			LeashAnchorWorld = GameObject.WorldPosition;

		if ( !UseLeashAnchor )
		{
			UseLeashAnchor = true;
			LeashAnchorWorld = GameObject.WorldPosition;
		}

		_spawnWorld = LeashAnchorWorld;
		_health = Components.Get<ThornsBanditHealth>();
		Motor = Components.Get<ThornsBanditMotor>();
		Combat = Components.Get<ThornsBanditCombat>();

		if ( !_spawnConfigured )
			ApplyArchetypeConfig( ResolveArchetypeConfig() );

		PickPatrolGoal();
		_combatStrafeSign = ( GameObject.Id.GetHashCode() & 1 ) == 0 ? 1f : -1f;
		_strafeSignSmoothed = _combatStrafeSign;
		ThornsBanditPopulation.HostRegister( this );
		if ( Components.Get<ThornsNpcVisualLod>() is null )
			Components.Create<ThornsNpcVisualLod>();
		ThornsCitizenCombatHitboxes.EnsureOnCitizenPawn( GameObject );
	}

	public void HostCompleteSpawnSetup( ThornsNpcHumanBanditSpawn.Config cfg ) =>
		ApplySpawnOverrides( cfg );

	/// <summary>Spread initial patrol, vision, and hearing reactions so a group does not move as one blob.</summary>
	public void HostApplySpawnPatrolStagger( int spawnIndex, int spawnCount )
	{
		var now = Time.Now;
		spawnCount = Math.Max( 1, spawnCount );
		var slotT = spawnIndex / (float)spawnCount;
		_patrolHeadingBiasDegrees = slotT * 360f + (float)_thinkJitter * 40f;
		_patrolPauseUntilRealtime = now + PatrolPauseSeconds + slotT * 2.2 + _thinkJitter * 1.5;
		_nextPathTickRealtime = now + 0.12 + spawnIndex * 0.11 + _thinkJitter * 0.35;
		_nextVisionTickRealtime = now + slotT * 0.35 + _thinkJitter * 0.25;
		_nextDecisionTickRealtime = now + slotT * 0.9 + _thinkJitter * 0.45;
		_nextHearingReactRealtime = now + slotT * 0.25;
	}

	protected override void OnDestroy()
	{
		ThornsBanditPopulation.HostUnregister( this );
	}

	public void HostTickAi( float delta, ThornsNpcLodTier lodTier )
	{
		_ = delta;
		_ = lodTier;
	}

	public void HostNotifyKilled( GameObject attackerRoot )
	{
		SetState( ThornsBanditAiState.Dead );
		ThornsBanditPopulation.HostUnregister( this );

		if ( attackerRoot.IsValid() )
		{
			var gameplay = attackerRoot.Components.GetInAncestorsOrSelf<ThornsPlayerGameplay>( true );
			gameplay?.HostNotifyBanditKill();
		}

		if ( ThornsMultiplayer.IsHostOrOffline )
		{
			var (lootTable, lootSeed, title) = ThornsEnemyLootTables.ResolveBandit( this );
			ThornsDeathCrateWorldService.Instance?.HostTrySpawnEnemyLootCrate(
				GameObject.WorldPosition,
				lootTable,
				lootSeed,
				title );
		}

		GameObject.Destroy();
	}

	public void ApplySpawnOverrides( ThornsNpcHumanBanditSpawn.Config cfg )
	{
		if ( cfg is null )
			return;

		_spawnConfigured = true;

		if ( cfg.Archetype is not null )
			ApplyArchetypeConfig( cfg.Archetype );

		if ( cfg.UseLeashAnchor && cfg.LeashAnchorWorld != default )
		{
			UseLeashAnchor = true;
			LeashAnchorWorld = cfg.LeashAnchorWorld;
			_spawnWorld = LeashAnchorWorld;
		}

		if ( cfg.LeashRadius > 4f )
			LeashRadius = cfg.LeashRadius;

		if ( cfg.WanderRadius > 4f )
			WanderRadius = cfg.WanderRadius;

		AnchorWanderGoalsToCurrentPosition = cfg.AnchorWanderGoalsToCurrentPosition;

		if ( cfg.AttackRange > 4f )
			_engagementRangeWorld = cfg.AttackRange;

		if ( cfg.LoseRadius > 4f )
			_loseTargetRangeWorld = cfg.LoseRadius;
	}

	public void ApplyArchetypeConfig( ThornsBanditArchetypeConfig cfg )
	{
		Archetype = cfg ?? ThornsBanditArchetypeConfig.Scavenger();
		BanditType = Archetype.Type;
		LeashRadius = Archetype.LeashRadiusWorld;
		WanderRadius = Archetype.PatrolRadiusWorld;
		_engagementRangeWorld = Archetype.EngagementRangeWorld;
		_loseTargetRangeWorld = Archetype.LoseTargetRangeWorld;

		if ( Combat.IsValid() )
			Combat.ApplySkill( Archetype.Skill );
	}

	float EngagementRange => _engagementRangeWorld > 1f ? _engagementRangeWorld : Archetype.EngagementRangeWorld;
	float LoseTargetRange => _loseTargetRangeWorld > 1f ? _loseTargetRangeWorld : Archetype.LoseTargetRangeWorld;
	float LosCheckRange => Math.Max( EngagementRange, Archetype.VisionRangeWorld );

	float MaxTargetDistanceFromAnchorForEngagement()
	{
		var leashGrace = LeashRadius + Archetype.ChaseGraceBeyondLeashWorld;

		// Outpost defenders and other spawn-tuned bandits pursue intruders toward gun range, not just leash+grace.
		if ( LeashRadius > Archetype.LeashRadiusWorld + 80f || _engagementRangeWorld > Archetype.EngagementRangeWorld + 25f )
		{
			var pursueRadius = Math.Max(
				EngagementRange + Archetype.ChaseGraceBeyondLeashWorld,
				LeashRadius + EngagementRange * 0.82f );
			return Math.Min( LoseTargetRange, Math.Max( leashGrace, pursueRadius ) );
		}

		return leashGrace;
	}

	float MaxChaseDistanceFromBandit() =>
		EngagementRange * ShootRangeFactor + Archetype.ChaseOvershootBeyondGunRangeWorld * ChaseLeashScale;

	float MaxSelfDistanceFromAnchorDuringChase() =>
		LeashRadius
		+ Archetype.ChaseGraceBeyondLeashWorld * ChaseLeashScale
		+ Archetype.ChaseOvershootBeyondGunRangeWorld * 0.9f * ChaseLeashScale;

	float MaxChaseRadiusFromAnchor() =>
		Math.Max( MaxTargetDistanceFromAnchorForEngagement(), MaxSelfDistanceFromAnchorDuringChase() );

	float ComputeGroupAlertDelaySeconds( Vector3 alertOrigin )
	{
		var dist = ( GameObject.WorldPosition.WithZ( 0 ) - alertOrigin.WithZ( 0 ) ).Length;
		var commR = Math.Max( 80f, Archetype.CommunicationRadiusWorld );
		var spreadT = Math.Clamp( dist / commR, 0f, 1f );
		return spreadT * Archetype.GroupAlertMaxSpreadSeconds + (float)_thinkJitter * 0.12f;
	}

	float ComputeDirectVisionStaggerSeconds( float targetDistance )
	{
		var visionR = Math.Max( 120f, Archetype.VisionRangeWorld );
		var nearT = Math.Clamp( 1f - targetDistance / visionR, 0f, 1f );
		return ( 1f - nearT ) * Archetype.GroupAlertMaxSpreadSeconds * 0.28f;
	}

	float ComputeHearingInvestigateDelaySeconds( Vector3 heardOrigin )
	{
		var dist = ( GameObject.WorldPosition.WithZ( 0 ) - heardOrigin.WithZ( 0 ) ).Length;
		var hearR = Math.Max( 220f, Archetype.HearGunshotRangeWorld * 0.42f );
		var spreadT = Math.Clamp( dist / hearR, 0f, 1f );
		var idSpread = ( GameObject.Id.GetHashCode() & 0x7fff ) / 32767f * 0.35f;
		return spreadT * 1.25f + idSpread + (float)_thinkJitter * 0.2f;
	}

	Vector3 HostJitterInvestigatePoint( Vector3 heardWorld, float spreadScale = 1f )
	{
		var flat = heardWorld.WithZ( 0 );
		var hash = GameObject.Id.GetHashCode();
		var yaw = ( hash & 1023 ) / 1023f * 360f;
		var radius = ( 48f + ( hash >> 10 & 255 ) / 255f * 92f ) * spreadScale;
		return flat + Rotation.FromYaw( yaw ).Forward * radius;
	}

	Vector3 HostFinalizeInvestigatePoint( Vector3 heardWorld, float spreadScale = 1f )
	{
		var point = HostJitterInvestigatePoint( heardWorld, spreadScale );
		return HostNudgePatrolGoalFromPeers( point, Math.Max( 120f, Archetype.CommunicationRadiusWorld * 0.28f ) );
	}

	bool HostOverlappingPeers( Vector3 selfFlat )
	{
		var selfR = HostGetSelfPlanarCollisionRadius();
		ThornsBanditSpatialGrid.QueryPlanarScratch( selfFlat, selfR + 88f );
		foreach ( var other in ThornsBanditSpatialGrid.ScratchResults )
		{
			if ( !other.IsValid() || other == this || other.IsDead )
				continue;

			var dist = ( selfFlat - other.GameObject.WorldPosition.WithZ( 0 ) ).Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + 20f;
			if ( dist < minDist )
				return true;
		}

		return false;
	}

	bool HostIsAmongClosestGroupHearers( Vector3 heardOrigin, int maxImmediate )
	{
		if ( GroupId == 0 )
			return true;

		var selfDistSq = ( GameObject.WorldPosition.WithZ( 0 ) - heardOrigin.WithZ( 0 ) ).LengthSquared;
		var closer = 0;
		foreach ( var other in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !other.IsValid() || other.IsDead || other == this || other.GroupId != GroupId )
				continue;

			if ( other.State is not (ThornsBanditAiState.Patrol or ThornsBanditAiState.Investigate) )
				continue;

			var otherDistSq = ( other.GameObject.WorldPosition.WithZ( 0 ) - heardOrigin.WithZ( 0 ) ).LengthSquared;
			if ( otherDistSq + 36f < selfDistSq )
				closer++;
		}

		return closer < maxImmediate;
	}

	void TickPendingHearingInvestigate( double now )
	{
		if ( _pendingHearingInvestigateUntilRealtime <= 0 || now < _pendingHearingInvestigateUntilRealtime )
			return;

		_pendingHearingInvestigateUntilRealtime = 0;
		var point = _pendingHearingInvestigatePoint;
		_pendingHearingInvestigatePoint = default;

		if ( IsDead || _state != ThornsBanditAiState.Patrol )
			return;

		_investigatePoint = HostFinalizeInvestigatePoint( point );
		ThornsBanditDebug.LogVision( this, $"heard sound (delayed) -> investigate @ {_investigatePoint:F0}", force: true );
		SetState( ThornsBanditAiState.Investigate, "heard sound" );
	}

	void TickPendingAcquire( double now )
	{
		if ( _pendingAcquireUntilRealtime <= 0 || now < _pendingAcquireUntilRealtime )
			return;

		_pendingAcquireUntilRealtime = 0;
		var target = _pendingAcquireTarget;
		var lastKnown = _pendingAcquireLastKnown;
		var broadcast = _pendingAcquireBroadcast;
		_pendingAcquireTarget = null;
		_pendingAcquireLastKnown = default;

		if ( IsDead || _state is ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Retreat )
			return;

		_lastKnownPosition = lastKnown;
		if ( target.IsValid() )
			HostAcquireTarget( target, broadcast );
		else if ( _state == ThornsBanditAiState.Patrol )
		{
			_investigatePoint = HostFinalizeInvestigatePoint( lastKnown, 1.2f );
			SetState( ThornsBanditAiState.Investigate, "ally alert" );
		}
	}

	void ScheduleAcquireTarget( GameObject target, Vector3 lastKnown, bool broadcast, float delaySeconds )
	{
		if ( !target.IsValid() )
			return;

		if ( _pendingAcquireUntilRealtime > 0 && _pendingAcquireTarget == target )
			return;

		if ( delaySeconds <= 0.04f )
		{
			HostAcquireTarget( target, broadcast );
			return;
		}

		_pendingAcquireTarget = target;
		_pendingAcquireLastKnown = lastKnown;
		_pendingAcquireBroadcast = broadcast;
		_pendingAcquireUntilRealtime = Time.Now + delaySeconds;
	}

	ThornsBanditArchetypeConfig ResolveArchetypeConfig() => BanditType switch
	{
		ThornsBanditType.CityDefender => ThornsBanditArchetypeConfig.CityDefender(),
		ThornsBanditType.AirdropDefender => ThornsBanditArchetypeConfig.AirdropDefender(),
		_ => ThornsBanditArchetypeConfig.Scavenger(),
	};

	public void HostRunSimulationTick()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var hp = Components.Get<ThornsBanditHealth>();
		if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
		{
			SetState( ThornsBanditAiState.Dead );
			ThornsBanditMovement.Stop( this );
			return;
		}

		var now = Time.Now;
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		var director = ThornsBanditDirector.Instance;

		TickPendingAcquire( now );
		TickPendingHearingInvestigate( now );

		if ( _state == ThornsBanditAiState.Combat && _target.IsValid() && CanFaceTargetThisFrame() )
			HostFaceCombatTarget();
		else if ( _state == ThornsBanditAiState.Chase && _target.IsValid() )
			HostFaceWorldPoint( ThornsBanditPerception.ResolveAimPoint( _target ) );
		else if ( _state == ThornsBanditAiState.Reposition && _target.IsValid() )
			HostFaceWorldPoint( ThornsBanditPerception.ResolveAimPoint( _target ) );

		if ( !ThornsNpcLod.ShouldRunBanditAi( EffectiveLodTier( director, selfFlat ) ) )
		{
			ThornsBanditDebug.LogEvent(
				this,
				"LOD",
				$"sleeping-tier sim state={_state} pathDue={now >= _nextPathTickRealtime} anchorDist={DistanceFromAnchor():F0}" );

			if ( director is not null
			     && _state is not ThornsBanditAiState.Dead and not ThornsBanditAiState.Retreat
			     && now >= _nextVisionTickRealtime )
			{
				var hearRadius = Archetype.HearGunshotRangeWorld;
				if ( director.HostNearestAlivePlayerDistSqWithin( selfFlat, hearRadius ) < float.MaxValue )
				{
					var detectScale = ThornsNpcLod.DetectionIntervalScale( EffectiveLodTier( director, selfFlat ) );
					_nextVisionTickRealtime = now + ( Archetype.VisionTickSeconds * detectScale ) + _thinkJitter * 0.05;
					TickVision( director, selfFlat );
				}
			}

			if ( _state == ThornsBanditAiState.Patrol && now >= _nextPathTickRealtime )
			{
				_nextPathTickRealtime = now + Archetype.PathTickSeconds * 0.55f;
				var pathDelta = (float)Math.Clamp( now - _lastPathTickRealtime, 0.033, 0.35 );
				_lastPathTickRealtime = now;
				TickPath( selfFlat, now, pathDelta );
			}
			else if ( _state != ThornsBanditAiState.Patrol )
				ThornsBanditMovement.Stop( this );

			return;
		}

		if ( director is null )
			return;

		var tickScale = ThornsNpcLod.TickIntervalScale( EffectiveLodTier( director, selfFlat ) );

		if ( now >= _nextVisionTickRealtime )
		{
			var visionInterval = Archetype.VisionTickSeconds * tickScale;
			var closePlayer = director.HostNearestAlivePlayerDistSqWithin(
				selfFlat,
				ThornsBanditCombatTuning.CloseNoticeDistance ) < float.MaxValue;
			visionInterval = closePlayer
				? Math.Min( visionInterval, ThornsBanditCombatTuning.LosCheckCloseInterval )
				: Math.Min( visionInterval, ThornsBanditCombatTuning.LosCheckInterval );
			_nextVisionTickRealtime = now + visionInterval + _thinkJitter * 0.05;
			TickVision( director, selfFlat );
		}

		if ( now >= _nextDecisionTickRealtime )
		{
			_nextDecisionTickRealtime = now + (Archetype.DecisionTickSeconds * tickScale) + _thinkJitter * 0.05;
			TickDecision( director, selfFlat, now );
		}

		if ( now >= _nextPathTickRealtime )
		{
			var pathInterval = Archetype.PathTickSeconds * tickScale;
			if ( _state is ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Investigate or ThornsBanditAiState.Reposition )
				pathInterval *= 0.4f;

			_nextPathTickRealtime = now + pathInterval;
			var pathDelta = (float)Math.Clamp( now - _lastPathTickRealtime, 0.033, 0.5 );
			_lastPathTickRealtime = now;
			TickPath( selfFlat, now, pathDelta );
		}

		ThornsBanditDebug.DrawForBrain( this );
	}

	protected override void OnUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead )
			return;

		TickSmoothFacing( Time.Delta );
	}

	void TickSmoothFacing( float delta )
	{
		if ( !_wantsSmoothFace || delta <= 0f )
			return;

		if ( !ThornsLocalPlayer.TryGetAuthoritativeEye( GameObject, out var eye, out _ ) )
			eye = GameObject.WorldPosition + Vector3.Up * 64f;

		var dir = ( _smoothFaceTarget - eye ).Normal;
		if ( dir.LengthSquared < 1e-6f )
			return;

		var flat = dir.WithZ( 0 );
		if ( flat.LengthSquared > 1e-10f )
		{
			var bodyTarget = Rotation.LookAt( flat.Normal );
			var bodyT = Math.Clamp( delta * 9f, 0.04f, 1f );
			GameObject.WorldRotation = Rotation.Slerp( GameObject.WorldRotation, bodyTarget, bodyT );
		}

		var view = ThornsBanditUtil.FindChild( GameObject, "View" );
		if ( !view.IsValid() )
			return;

		var viewTarget = Rotation.LookAt( dir );
		if ( _state == ThornsBanditAiState.Combat )
		{
			var angles = viewTarget.Angles();
			angles.pitch += _aimErrorPitch;
			angles.yaw += _aimErrorYaw;
			viewTarget = Rotation.From( angles );
		}

		var viewT = Math.Clamp( delta * _combatFaceSmoothSpeed, 0.05f, 1f );
		view.WorldRotation = Rotation.Slerp( view.WorldRotation, viewTarget, viewT );
	}

	internal void HostDrawDebugOverlay()
	{
		if ( !ThornsBanditDebug.DebugEnabled || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var pos = GameObject.WorldPosition + Vector3.Up * 72f;
		DebugOverlay.Text( pos, $"{State} | {DebugTargetLabel}", duration: 0.12f );

		if ( ThornsBanditDebug.ShowPerception )
		{
			DebugOverlay.Sphere( new Sphere( GameObject.WorldPosition, Archetype.VisionRangeWorld ), duration: 0.12f );
			DebugOverlay.Sphere( new Sphere( GameObject.WorldPosition, Archetype.CommunicationRadiusWorld ), duration: 0.12f );
		}

		if ( ThornsBanditDebug.ShowTargets && DebugLastKnownPosition != default )
			DebugOverlay.Line( pos, DebugLastKnownPosition + Vector3.Up * 48f, duration: 0.12f );

		if ( ThornsBanditDebug.ShowCover && DebugCoverGoal != default )
			DebugOverlay.Line( pos, DebugCoverGoal + Vector3.Up * 24f, duration: 0.12f );
	}

	void TickVision( ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( _state is ThornsBanditAiState.Dead or ThornsBanditAiState.Retreat )
			return;

		if ( ThornsBanditPerception.TryRefreshHearing( selfFlat, Archetype, out var heard, out var soundType ) )
		{
			var isCombatSound = soundType is ThornsBanditCommunication.SoundType.Gunshot
				or ThornsBanditCommunication.SoundType.Explosion
				or ThornsBanditCommunication.SoundType.AllyCombat
				or ThornsBanditCommunication.SoundType.AnimalAttack;
			var spread = isCombatSound ? 1.35f : 1f;
			var investigatePoint = HostFinalizeInvestigatePoint( heard, spread );

			if ( _state == ThornsBanditAiState.Patrol && Time.Now >= _nextHearingReactRealtime )
			{
				var delay = ComputeHearingInvestigateDelaySeconds( heard );
				if ( isCombatSound )
					delay *= 0.35f;

				if ( !HostIsAmongClosestGroupHearers( heard, maxImmediate: 6 ) )
					delay = Math.Max( delay, isCombatSound ? 0.3f : 0.7f );

				_nextHearingReactRealtime = Time.Now + ( isCombatSound ? 3.2f : 5.0f ) + delay * 0.35f;

				if ( delay <= 0.08f )
				{
					_investigatePoint = investigatePoint;
					ThornsBanditDebug.LogVision( this, $"heard sound -> investigate @ {investigatePoint:F0}", force: true );
					SetState( ThornsBanditAiState.Investigate, "heard sound" );
				}
				else if ( delay < ( isCombatSound ? 6f : 10f ) )
				{
					_pendingHearingInvestigatePoint = investigatePoint;
					_pendingHearingInvestigateUntilRealtime = Time.Now + delay;
				}
			}
		}

		if ( _target.IsValid() && Time.Now < _targetLockUntilRealtime )
		{
			if ( ThornsBanditPerception.IsValidCombatTarget( _target, this, Archetype )
			     && DistanceFlat( _target ) <= LoseTargetRange
			     && ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange ) )
			{
				_lastKnownPosition = _target.WorldPosition;
				_lastSeenTargetRealtime = Time.Now;
				ThornsBanditDebug.LogVision(
					this,
					$"maintain lock on {_target.Name} dist={DistanceFlat( _target ):F0} state={_state}" );
				return;
			}

			ThornsBanditDebug.LogVision(
				this,
				$"lock lapse on {_target.Name} dist={DistanceFlat( _target ):F0} los={ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange )}",
				force: true );
		}

		if ( Time.Now < _reacquireBlockedUntilRealtime )
		{
			if ( !TryAcquireFromProximityOrVision( director, selfFlat, immediateOnly: true ) )
				return;

			return;
		}

		TryAcquireFromProximityOrVision( director, selfFlat, immediateOnly: false );
	}

	bool TryAcquireFromProximityOrVision( ThornsBanditDirector director, Vector3 selfFlat, bool immediateOnly )
	{
		GameObject seen = default;
		if ( immediateOnly )
		{
			if ( !ThornsBanditPerception.TryAcquireImmediateProximityThreat( this, director, selfFlat, Archetype, out seen )
			     || !seen.IsValid() )
				return false;
		}
		else if ( ThornsBanditPerception.TryAcquireImmediateProximityThreat( this, director, selfFlat, Archetype, out seen )
		          && seen.IsValid() )
		{
			// Close threats bypass the normal vision cone / tick schedule.
		}
		else if ( !ThornsBanditPerception.TryAcquireVisibleTarget( this, director, selfFlat, Archetype, _recentAttacker, out seen )
		          || !seen.IsValid() )
		{
			return false;
		}

		var seenDist = DistanceFlat( seen );
		ThornsBanditDebug.LogVision( this, $"acquired {( immediateOnly ? "proximity" : "visible" )} {seen.Name} dist={seenDist:F0} from={_state}", force: true );

		if ( _state is ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Reposition )
		{
			_target = seen;
			_lastKnownPosition = seen.WorldPosition;
			_lastSeenTargetRealtime = Time.Now;
			_targetLockUntilRealtime = Time.Now + Archetype.TargetLockSeconds;
			return true;
		}

		var stagger = ComputeDirectVisionStaggerSeconds( seenDist );
		var broadcast = _state == ThornsBanditAiState.Patrol && stagger <= 0.12f;
		_pendingAcquireUntilRealtime = 0;
		_pendingAcquireTarget = null;
		HostAcquireTarget( seen, broadcast, stagger );
		return true;
	}

	ThornsNpcLodTier EffectiveLodTier( ThornsBanditDirector director, Vector3 selfFlat )
	{
		if ( director is null )
			return LodTier;

		if ( director.HostNearestAlivePlayerDistSqWithin( selfFlat, ThornsBanditCombatTuning.CloseNoticeDistance ) < float.MaxValue )
			return ThornsNpcLodTier.Full;

		return LodTier;
	}

	void TickDecision( ThornsBanditDirector director, Vector3 selfFlat, double now )
	{
		switch ( _state )
		{
			case ThornsBanditAiState.Patrol:
				if ( ShouldRetreat( hpFraction: GetHealthFraction() ) )
					EnterRetreat();
				break;

			case ThornsBanditAiState.Investigate:
				if ( HostMemoryExpired() && !_investigateSearching )
				{
					SetState( ThornsBanditAiState.Patrol, "memory expired" );
					break;
				}

				if ( _investigateSearching
				     && _investigateSearchStartedRealtime > 0
				     && now - _investigateSearchStartedRealtime >= ThornsBanditCombatTuning.MemorySearchSeconds )
				{
					SetState( ThornsBanditAiState.Patrol, "search done" );
					break;
				}

				if ( now >= _investigateUntilRealtime && !HostRemembersThreat() )
					SetState( ThornsBanditAiState.Patrol, "investigate timeout" );
				break;

			case ThornsBanditAiState.Combat:
				if ( ShouldRetreat( GetHealthFraction() ) )
				{
					EnterRetreat();
					break;
				}

				if ( !_target.IsValid() || !ThornsBanditPerception.IsValidCombatTarget( _target, this, Archetype ) )
				{
					HostAbandonEngagement( "invalid target" );
					break;
				}

				var combatLos = ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange );
				if ( combatLos )
				{
					_lastKnownPosition = _target.WorldPosition;
					_lastSeenTargetRealtime = Time.Now;
				}
				else if ( HostRemembersThreat()
				          && now - _stateEnteredRealtime >= ThornsBanditCombatTuning.EngageMinHoldSeconds )
				{
					BeginInvestigateFromThreat( _lastKnownPosition, "lost los" );
					break;
				}

				if ( ShouldAbandonEngagement( out var abandonReason ) )
				{
					HostAbandonEngagement( abandonReason );
					break;
				}

				break;

			case ThornsBanditAiState.Chase:
				if ( ShouldRetreat( GetHealthFraction() ) )
				{
					EnterRetreat();
					break;
				}

				if ( _target.IsValid()
				     && DistanceFlat( _target ) <= EngagementRange * ExitChaseRangeFactor
				     && ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange ) )
				{
					SetState( ThornsBanditAiState.Combat, $"in range dist={DistanceFlat( _target ):F0}" );
				}
				else if ( ShouldEndChase( out var chaseEndReason ) )
				{
					HostAbandonEngagement( chaseEndReason );
				}

				break;

			case ThornsBanditAiState.Reposition:
				if ( !_target.IsValid() )
				{
					HostAbandonEngagement( "reposition no target" );
					break;
				}

				if ( ShouldAbandonEngagement( out var repoAbandon ) )
					HostAbandonEngagement( repoAbandon );
				break;

			case ThornsBanditAiState.Retreat:
				if ( now >= _retreatUntilRealtime && GetHealthFraction() > Archetype.RetreatHealthFraction + 0.12f )
				{
					if ( _target.IsValid() )
						SetState( ThornsBanditAiState.Combat );
					else
						SetState( ThornsBanditAiState.Patrol );
				}
				break;
		}
	}

	void TickPath( Vector3 selfFlat, double now, float pathDelta )
	{
		switch ( _state )
		{
			case ThornsBanditAiState.Patrol:
				if ( DistanceFromAnchor() > LeashRadius * 1.02f )
				{
					_patrolGoal = HostClampToLeash( FlatAnchor() );
					ThornsBanditDebug.LogPath( this, "return-anchor", $"anchorDist={DistanceFromAnchor():F0} leash={LeashRadius:F0}" );
					ThornsBanditMovement.MoveToward( this, _patrolGoal, PatrolSpeed * 1.05f, turnDeltaSeconds: pathDelta );
					break;
				}

				if ( HostIsStandingInWater() )
				{
					ThornsBanditDebug.LogPath( this, "repick", "standing in water" );
					PickPatrolGoal();
					break;
				}

				if ( now < _patrolPauseUntilRealtime )
				{
					ThornsBanditDebug.LogPath( this, "pause", $"until={_patrolPauseUntilRealtime - now:F1}s" );
					ThornsBanditMovement.Stop( this );
					break;
				}

				var goalDist = ( _patrolGoal.WithZ( 0 ) - selfFlat ).Length;
				if ( ThornsBanditMovement.IsNear( this, _patrolGoal, PatrolNearGoalDistance ) )
				{
					_patrolPauseUntilRealtime = now + PatrolPauseSeconds;
					PickPatrolGoal();
					_patrolNoProgressTicks = 0;
					_patrolLastGoalDist = 0f;
				}
				else if ( goalDist > PatrolNearGoalDistance + 8f )
				{
					if ( goalDist >= _patrolLastGoalDist - 6f )
						_patrolNoProgressTicks++;
					else
						_patrolNoProgressTicks = 0;

					_patrolLastGoalDist = goalDist;

					if ( _patrolNoProgressTicks >= 2 )
					{
						ThornsBanditDebug.LogPath( this, "unstuck", $"goalDist={goalDist:F0} peers" );
						PickPatrolGoal( preferAwayFromPeers: true );
						_patrolNoProgressTicks = 0;
						_patrolLastGoalDist = 0f;
					}
				}

				ThornsBanditDebug.LogPath(
					this,
					"wander",
					$"goalDist={goalDist:F0} patrolR={HostGetPatrolWanderRadius():F0} anchorDist={DistanceFromAnchor():F0}" );
				ThornsBanditMovement.MoveToward(
					this,
					_patrolGoal,
					PatrolSpeed,
					arrivalDistance: PatrolArrivalDistance,
					turnDeltaSeconds: pathDelta );
				break;

			case ThornsBanditAiState.Investigate:
				var investigateDist = ( _investigatePoint.WithZ( 0 ) - selfFlat ).Length;
				if ( ThornsBanditMovement.IsNear( this, _investigatePoint, ThornsBanditCombatTuning.InvestigateArrivalDistance ) )
				{
					if ( _investigateSearching
					     && _investigateSearchStartedRealtime > 0
					     && now - _investigateSearchStartedRealtime < ThornsBanditCombatTuning.MemorySearchSeconds )
					{
						_investigatePoint = PickInvestigateSearchPoint();
						ThornsBanditMovement.MoveToward(
							this,
							_investigatePoint,
							PatrolSpeed * 0.9f,
							arrivalDistance: ThornsBanditCombatTuning.InvestigateArrivalDistance,
							turnDeltaSeconds: pathDelta );
						break;
					}

					if ( !_investigateSearching && HostRemembersThreat() )
					{
						BeginInvestigateSearch();
						_investigatePoint = PickInvestigateSearchPoint();
						ThornsBanditMovement.MoveToward(
							this,
							_investigatePoint,
							PatrolSpeed * 0.9f,
							arrivalDistance: ThornsBanditCombatTuning.InvestigateArrivalDistance,
							turnDeltaSeconds: pathDelta );
						break;
					}

					if ( HostOverlappingPeers( selfFlat ) )
					{
						_investigatePoint = HostNudgePatrolGoalFromPeers( _investigatePoint, 160f );
						ThornsBanditMovement.MoveToward(
							this,
							_investigatePoint,
							PatrolSpeed * 0.55f,
							arrivalDistance: 72f,
							turnDeltaSeconds: pathDelta );
						break;
					}

					var toCenter = _investigatePoint.WithZ( 0 ) - selfFlat;
					var baseYaw = toCenter.LengthSquared > 16f
						? Rotation.LookAt( toCenter.Normal ).Yaw()
						: GameObject.WorldRotation.Yaw();
					var sweepYaw = baseYaw + MathF.Sin( (float)Time.Now * 1.35f + (float)_thinkJitter * 8f ) * 75f;
					ThornsBanditMovement.TurnBodyToward( this, Rotation.FromYaw( sweepYaw ).Forward, pathDelta );
					ThornsBanditMovement.Stop( this );
					break;
				}

				if ( investigateDist > 80f )
				{
					if ( investigateDist >= _investigateLastGoalDist - 6f )
						_investigateNoProgressTicks++;
					else
						_investigateNoProgressTicks = 0;

					_investigateLastGoalDist = investigateDist;

					if ( _investigateNoProgressTicks >= 2 || HostOverlappingPeers( selfFlat ) )
					{
						ThornsBanditDebug.LogPath( this, "investigate-unstuck", $"goalDist={investigateDist:F0}" );
						_investigatePoint = HostNudgePatrolGoalFromPeers( _investigatePoint, 180f );
						if ( !HostPatrolGoalIsAcceptable( _investigatePoint, 40f ) )
							_investigatePoint = HostFinalizeInvestigatePoint( _investigatePoint, 1.1f );
						_investigateNoProgressTicks = 0;
						_investigateLastGoalDist = 0f;
					}
				}

				ThornsBanditMovement.MoveToward(
					this,
					_investigatePoint,
					investigateDist > ThornsBanditCombatTuning.HuntSprintDistance ? CombatMoveSpeed : PatrolSpeed * 0.85f,
					faceTarget: null,
					arrivalDistance: ThornsBanditCombatTuning.InvestigateArrivalDistance,
					turnDeltaSeconds: pathDelta );
				break;

			case ThornsBanditAiState.Combat:
				TickCombatPath( pathDelta );
				break;

			case ThornsBanditAiState.Chase:
				if ( !_target.IsValid() )
				{
					HostAbandonEngagement( "chase no target" );
					break;
				}

				var hasLos = ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange );
				var chaseGoal = hasLos
					? _target.WorldPosition
					: _lastKnownPosition != default ? _lastKnownPosition : _target.WorldPosition;
				chaseGoal = HostResolveEngageMoveGoal( chaseGoal );

				if ( ThornsBanditMovement.IsNear( this, chaseGoal, 96f ) )
				{
					ThornsBanditDebug.LogPath( this, "chase-arrived", $"dist={DistanceFlat( _target ):F0} los={hasLos}" );
					ThornsBanditMovement.Stop( this );
					break;
				}

				ThornsBanditDebug.LogPath( this, "chase", $"dist={DistanceFlat( _target ):F0} goal={chaseGoal:F0} los={hasLos}" );
				ThornsBanditMovement.MoveToward( this, chaseGoal, CombatMoveSpeed, _target, 96f, pathDelta, rotateBodyTowardMovement: false );
				break;

			case ThornsBanditAiState.Reposition:
				if ( _coverGoal == default
				     && !ThornsBanditCoverSystem.TryFindCover( this, _target, Archetype.CoverSearchRadiusWorld, _recentCover, out _coverGoal ) )
				{
					_coverGoal = _lastKnownPosition != default ? _lastKnownPosition : _patrolGoal;
				}

				if ( ThornsBanditMovement.IsNear( this, _coverGoal, 64f ) )
				{
					RememberCover( _coverGoal );
					SetState( ThornsBanditAiState.Combat );
					break;
				}

				ThornsBanditMovement.MoveToward( this, _coverGoal, CombatMoveSpeed, _target, 64f, pathDelta, rotateBodyTowardMovement: false );
				break;

			case ThornsBanditAiState.Retreat:
				if ( _retreatGoal == default
				     && !ThornsBanditCoverSystem.TryFindCover( this, _target, Archetype.CoverSearchRadiusWorld * 1.2f, _recentCover, out _retreatGoal ) )
				{
					_retreatGoal = HostClampToLeash( _spawnWorld + GameObject.WorldRotation.Backward.WithZ( 0 ) * 280f );
				}

				ThornsBanditMovement.MoveToward( this, _retreatGoal, RetreatSpeed, arrivalDistance: 80f, turnDeltaSeconds: pathDelta );
				break;

			case ThornsBanditAiState.Dead:
				ThornsBanditMovement.Stop( this );
				break;
		}
	}

	void TickCombatPath( float pathDelta )
	{
		if ( !_target.IsValid() )
			return;

		var dist = DistanceFlat( _target );
		var hasLos = ThornsBanditPerception.HasClearLos( GameObject, _target, LosCheckRange );
		var engage = EngagementRange;
		var inGunRange = dist <= engage * ShootRangeFactor;
		var isLongRange = dist > ThornsBanditCombatTuning.LongRangeEngageDistance;
		_combatWantsAds = dist > ThornsBanditCombatTuning.AdsMinDistance;

		if ( hasLos && inGunRange && Combat.IsValid() && !Combat.HostIsReactionReady )
		{
			ThornsBanditMovement.Stop( this );
			return;
		}

		if ( hasLos && inGunRange && Combat.IsValid() && Combat.HostIsReactionReady && !_hasReactionTarget )
		{
			_hasReactionTarget = true;
			RefreshAimError( _combatWantsAds, dist );
		}

		if ( !hasLos || !inGunRange )
		{
			ThornsBanditDebug.LogPath( this, "engage-approach", FormatCombatDist( dist, engage, hasLos ) );
			TickEngageApproach( pathDelta, dist, hasLos );
		}
		else if ( Combat.IsValid() && Combat.HostIsInBurstPause )
		{
			ThornsBanditDebug.LogPath( this, "combat-hold", FormatCombatDist( dist, engage, hasLos ) );
			TickEngageMovement( pathDelta, dist, isLongRange, speedScale: 0.38f );
		}
		else
		{
			if ( Combat.IsValid() && Combat.HostWillStartBurstThisShot )
				RefreshAimError( _combatWantsAds, dist );

			ThornsBanditDebug.LogPath( this, "combat-strafe", FormatCombatDist( dist, engage, hasLos ) );
			TickEngageMovement( pathDelta, dist, isLongRange, speedScale: 0.55f );
		}

		if ( hasLos && inGunRange && Combat.IsValid() && Combat.HostIsReactionReady && !Combat.HostIsInBurstPause )
		{
			ThornsBanditDebug.LogPath( this, "shoot", FormatCombatDist( dist, engage, hasLos ) );
			Combat.HostTryShootToward( _target, _combatWantsAds );
		}
	}

	void TickEngageApproach( float pathDelta, float flatDistance, bool hasLos )
	{
		if ( !_target.IsValid() )
		{
			ThornsBanditMovement.Stop( this );
			return;
		}

		var goal = hasLos
			? _target.WorldPosition
			: _lastKnownPosition != default ? _lastKnownPosition : _target.WorldPosition;
		goal = HostResolveEngageMoveGoal( goal );

		var speed = flatDistance > ThornsBanditCombatTuning.EngageTooFarDistance
			? CombatMoveSpeed
			: CombatMoveSpeed * 0.88f;

		ThornsBanditMovement.MoveToward(
			this,
			goal,
			speed,
			_target,
			96f,
			pathDelta,
			rotateBodyTowardMovement: false );
	}

	void TickEngageMovement( float pathDelta, float flatDistance, bool isLongRange, float speedScale )
	{
		if ( !_target.IsValid() )
		{
			ThornsBanditMovement.Stop( this );
			return;
		}

		if ( isLongRange )
		{
			if ( _stationarySinceRealtime <= 0 )
				_stationarySinceRealtime = Time.Now;

			ThornsBanditMovement.Stop( this );
			return;
		}

		_stationarySinceRealtime = 0;
		var now = Time.Now;
		if ( now >= _strafeFlipUntilRealtime )
		{
			_combatStrafeSign = Game.Random.Int( 0, 1 ) == 0 ? -1f : 1f;
			_strafeFlipUntilRealtime = now + Game.Random.Float(
				ThornsBanditCombatTuning.StrafeFlipMinSeconds,
				ThornsBanditCombatTuning.StrafeFlipMaxSeconds );
		}

		var strafeBlend = Math.Clamp( pathDelta * ThornsBanditCombatTuning.StrafeSignSmoothSpeed, 0f, 1f );
		_strafeSignSmoothed = MathX.Lerp( _strafeSignSmoothed, _combatStrafeSign, strafeBlend );

		var self = GameObject.WorldPosition.WithZ( 0 );
		var toTarget = _target.WorldPosition.WithZ( 0 ) - self;
		if ( toTarget.LengthSquared < 1f )
		{
			ThornsBanditMovement.Stop( this );
			return;
		}

		var flatDist = toTarget.Length;
		var toTargetNorm = toTarget / flatDist;
		var strafeDir = GameObject.WorldRotation.Right * _strafeSignSmoothed;

		var forwardBias = 0f;
		if ( flatDistance > ThornsBanditCombatTuning.EngageTooFarDistance )
			forwardBias = 0.4f;
		else if ( flatDistance < ThornsBanditCombatTuning.EngageTooCloseDistance )
			forwardBias = -0.35f;
		else
		{
			var rangeError = ( flatDistance - ThornsBanditCombatTuning.EngageIdealDistance )
			                 / ThornsBanditCombatTuning.EngageIdealDistance;
			forwardBias = Math.Clamp( rangeError * 0.35f, -0.25f, 0.25f );
		}

		var wishPlanar = ( strafeDir + toTargetNorm * forwardBias ).WithZ( 0 );
		if ( wishPlanar.Length <= 0.08f )
		{
			ThornsBanditMovement.Stop( this );
			return;
		}

		var strafeGoal = self + wishPlanar.Normal * 160f;
		var moveSpeed = CombatMoveSpeed * speedScale * (_combatWantsAds ? 0.72f : 1f);
		ThornsBanditMovement.MoveToward(
			this,
			HostResolveEngageMoveGoal( strafeGoal ),
			moveSpeed,
			_target,
			36f,
			pathDelta,
			rotateBodyTowardMovement: false );
	}

	void HostFaceCombatTarget()
	{
		if ( !_target.IsValid() )
			return;

		var dist = DistanceFlat( _target );
		var isLongRange = dist > ThornsBanditCombatTuning.LongRangeEngageDistance;
		var aimPoint = ThornsBanditPerception.ResolveAimPoint( _target );
		var bodyPoint = _target.WorldPosition + Vector3.Up * 48f;
		var lookPoint = Vector3.Lerp( aimPoint, bodyPoint, isLongRange ? 0.28f : 0.35f );
		HostFaceWorldPoint( lookPoint, isLongRange
			? ThornsBanditCombatTuning.LongRangeAimSmoothSpeed
			: ThornsBanditCombatTuning.AimSmoothSpeed );
	}

	void RefreshAimError( bool ads, float distance )
	{
		var spread = ads ? ThornsBanditCombatTuning.AdsAimErrorDegrees : ThornsBanditCombatTuning.HipAimErrorDegrees;
		var distanceScale = Math.Clamp(
			ThornsBanditCombatTuning.AimErrorReferenceDistance / MathF.Max( distance, 1f ),
			ThornsBanditCombatTuning.MinAimErrorScale,
			1f );
		spread *= distanceScale;
		if ( Combat.IsValid() )
		{
			spread *= Combat.Skill switch
			{
				ThornsBanditSkillLevel.Poor => 1.22f,
				ThornsBanditSkillLevel.Veteran => 0.78f,
				_ => 1f
			};
		}

		_aimErrorYaw = Game.Random.Float( -spread, spread );
		_aimErrorPitch = Game.Random.Float( -spread * 0.6f, spread * 0.6f );
	}

	bool HostRemembersThreat() =>
		_lastKnownPosition != default
		&& _lastSeenTargetRealtime > 0
		&& Time.Now - _lastSeenTargetRealtime < ThornsBanditCombatTuning.MemoryKeepSeconds;

	bool HostMemoryExpired() =>
		_lastSeenTargetRealtime <= 0
		|| Time.Now - _lastSeenTargetRealtime > ThornsBanditCombatTuning.MemoryForgetSeconds;

	void BeginInvestigateFromThreat( Vector3 lastKnown, string reason )
	{
		_investigatePoint = lastKnown != default ? lastKnown : _lastKnownPosition;
		_investigateSearching = false;
		_investigateSearchStartedRealtime = 0;
		_coverGoal = default;
		Combat?.HostCancelReposition();
		SetState( ThornsBanditAiState.Investigate, reason );
	}

	void BeginInvestigateSearch()
	{
		_investigateSearching = true;
		_investigateSearchStartedRealtime = Time.Now;
		_investigateSearchAnchor = _investigatePoint;
	}

	Vector3 PickInvestigateSearchPoint()
	{
		for ( var attempt = 0; attempt < 6; attempt++ )
		{
			var angle = Game.Random.Float( 0f, MathF.Tau );
			var radius = Game.Random.Float(
				ThornsBanditCombatTuning.MemorySearchRadius * 0.35f,
				ThornsBanditCombatTuning.MemorySearchRadius );
			var offset = new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
			var goal = _investigateSearchAnchor + offset;
			if ( GameObject.WorldPosition.Distance( goal ) > 48f )
				return HostClampToLeash( goal );
		}

		return _investigateSearchAnchor;
	}

	void ResetCombatPresentation()
	{
		_aimErrorPitch = 0f;
		_aimErrorYaw = 0f;
		_hasReactionTarget = false;
		_combatWantsAds = false;
		_stationarySinceRealtime = 0;
		_strafeSignSmoothed = _combatStrafeSign;
	}

	string FormatCombatDist( float dist, float engage, bool hasLos ) =>
		$"dist={dist:F0} engage={engage:F0} los={hasLos} shoot<{engage * ShootRangeFactor:F0} chase>{engage * EnterChaseRangeFactor:F0}";

	void EnterChase( string reason = "out of gun range" )
	{
		if ( _state == ThornsBanditAiState.Chase )
			return;

		_chaseUntilRealtime = Time.Now + HostChaseDurationSeconds();
		_lastChaseDistanceSample = _target.IsValid() ? DistanceFlat( _target ) : 0f;
		_targetLockUntilRealtime = Math.Max( _targetLockUntilRealtime, Time.Now + Archetype.TargetLockSeconds * 1.35f );
		Combat?.HostCancelReposition();
		SetState( ThornsBanditAiState.Chase, reason );
		_coverGoal = default;
	}

	bool ShouldAbandonEngagement( out string reason )
	{
		reason = "";

		if ( !_target.IsValid() )
		{
			reason = "no target";
			return true;
		}

		if ( _state == ThornsBanditAiState.Chase )
			return ShouldAbandonChase( out reason );

		var useExtendedLeash = LeashRadius > Archetype.LeashRadiusWorld + 80f
		                     || _engagementRangeWorld > Archetype.EngagementRangeWorld + 25f;
		var selfLeashLimit = useExtendedLeash
			? MaxSelfDistanceFromAnchorDuringChase()
			: LeashRadius * 1.06f;

		if ( DistanceFromAnchor() > selfLeashLimit )
		{
			reason = $"self beyond leash ({DistanceFromAnchor():F0}>{selfLeashLimit:F0})";
			return true;
		}

		var maxFromAnchor = MaxTargetDistanceFromAnchorForEngagement();
		if ( DistanceFromAnchor( _target.WorldPosition ) > maxFromAnchor )
		{
			reason = $"target beyond leash grace ({DistanceFromAnchor( _target.WorldPosition ):F0}>{maxFromAnchor:F0} leash={LeashRadius:F0})";
			return true;
		}

		if ( DistanceFlat( _target ) > LoseTargetRange )
		{
			reason = $"target too far ({DistanceFlat( _target ):F0}>{LoseTargetRange:F0})";
			return true;
		}

		if ( !ThornsBanditPerception.IsValidCombatTarget( _target, this, Archetype ) )
		{
			reason = "target invalid";
			return true;
		}

		return false;
	}

	bool ShouldAbandonChase( out string reason )
	{
		reason = "";
		var distToTarget = DistanceFlat( _target );
		var maxChaseDist = Math.Max( MaxChaseDistanceFromBandit(), LoseTargetRange * 1.02f );

		if ( distToTarget > maxChaseDist )
		{
			reason = $"chase target too far ({distToTarget:F0}>{maxChaseDist:F0})";
			return true;
		}

		var selfLimit = MaxSelfDistanceFromAnchorDuringChase();
		if ( DistanceFromAnchor() > selfLimit )
		{
			reason = $"self beyond chase leash ({DistanceFromAnchor():F0}>{selfLimit:F0})";
			return true;
		}

		if ( !ThornsBanditPerception.IsValidCombatTarget( _target, this, Archetype ) )
		{
			reason = "target invalid";
			return true;
		}

		return false;
	}

	bool ShouldEndChase( out string reason )
	{
		if ( _chaseUntilRealtime > 0 && Time.Now >= _chaseUntilRealtime )
		{
			reason = "chase timer expired";
			return true;
		}

		if ( ShouldAbandonEngagement( out reason ) )
			return true;

		reason = "";
		return false;
	}

	void HostAbandonEngagement( string reason = "abandon" )
	{
		ThornsBanditDebug.LogEvent(
			this,
			"ENGAGE",
			$"abandon: {reason} target={DebugTargetLabel} anchorDist={DistanceFromAnchor():F0}",
			force: true );

		var lastKnown = _lastKnownPosition;
		var remembers = HostRemembersThreat();
		_target = null;
		_targetLockUntilRealtime = 0;
		_chaseUntilRealtime = 0;
		_coverGoal = default;
		_wantsSmoothFace = false;
		ResetCombatPresentation();
		_reacquireBlockedUntilRealtime = Time.Now + 1.0;
		_nextHearingReactRealtime = Time.Now + 1.0;
		_lastChaseDistanceSample = 0f;
		_pendingAcquireUntilRealtime = 0;
		_pendingAcquireTarget = null;

		if ( remembers && lastKnown != default )
			BeginInvestigateFromThreat( lastKnown, reason );
		else
		{
			SetState( ThornsBanditAiState.Patrol, reason );
			PickPatrolGoalTowardAnchor();
		}
	}

	public void HostNotifyDamagedByHostile( GameObject attackerRoot )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !attackerRoot.IsValid() )
			return;

		if ( !ThornsBanditUtil.TryResolveCombatAttackerRoot( attackerRoot, out var atk ) || !atk.IsValid() || atk == GameObject )
			return;

		_recentAttacker = atk;
		_recentDamageUntilRealtime = Time.Now + 6.0;
		_reacquireBlockedUntilRealtime = 0;
		_targetLockUntilRealtime = Time.Now + Archetype.TargetLockSeconds;
		HostAcquireTarget( atk, broadcast: true );
	}

	internal bool HasRecentDamageAttacker => Time.Now < _recentDamageUntilRealtime && _recentAttacker.IsValid();

	public void HostReceiveAllyCombatAlert( GameObject target, Vector3 lastKnown, Vector3 alertOrigin )
	{
		if ( IsDead || _state is ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Retreat )
			return;

		if ( _pendingAcquireUntilRealtime > 0 )
			return;

		var delay = ComputeGroupAlertDelaySeconds( alertOrigin );
		if ( target.IsValid() && delay <= 0.04f )
		{
			HostAcquireTarget( target, broadcast: false );
			return;
		}

		_pendingAcquireTarget = target;
		_pendingAcquireLastKnown = lastKnown;
		_pendingAcquireBroadcast = false;
		_pendingAcquireUntilRealtime = Time.Now + delay;
	}

	void HostAcquireTarget( GameObject target, bool broadcast, float extraReactionDelaySeconds = 0f )
	{
		if ( !target.IsValid() )
			return;

		_target = target;
		_lastKnownPosition = target.WorldPosition;
		_lastSeenTargetRealtime = Time.Now;
		_targetLockUntilRealtime = Time.Now + Archetype.TargetLockSeconds;

		if ( _state is not (ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Reposition or ThornsBanditAiState.Retreat) )
		{
			SetState( ThornsBanditAiState.Combat, $"acquired {target.Name}" );
			Combat?.HostPrepareCombatReaction( Archetype, extraReactionDelaySeconds );
		}
		else if ( _state == ThornsBanditAiState.Chase )
		{
			SetState( ThornsBanditAiState.Combat, $"re-engaged {target.Name}" );
		}

		if ( broadcast )
			ThornsBanditCommunication.HostBroadcastCombatAlert( this, target, _lastKnownPosition );
	}

	bool ShouldRetreat( float hpFraction )
	{
		if ( !Archetype.CanRetreat )
			return false;

		if ( hpFraction <= Archetype.RetreatHealthFraction )
			return true;

		return Time.Now < _recentDamageUntilRealtime && hpFraction <= Archetype.RetreatHealthFraction + 0.15f;
	}

	void EnterRetreat()
	{
		_retreatGoal = default;
		_retreatUntilRealtime = Time.Now + Archetype.RetreatRecoverSeconds;
		SetState( ThornsBanditAiState.Retreat, "low health" );
		ThornsBanditCommunication.HostBroadcastCombatAlert( this, _target, GameObject.WorldPosition );
	}

	void SetState( ThornsBanditAiState next, string reason = "transition" )
	{
		if ( _state == next )
			return;

		ThornsBanditDebug.LogState( this, _state, next, reason );
		_state = next;
		State = next;
		_stateEnteredRealtime = Time.Now;

		if ( next == ThornsBanditAiState.Reposition )
			_coverGoal = default;

		if ( next == ThornsBanditAiState.Investigate )
		{
			_investigateUntilRealtime = Time.Now + Math.Max(
				Archetype.InvestigateTimeoutSeconds,
				ThornsBanditCombatTuning.MemoryForgetSeconds );
			_investigateNoProgressTicks = 0;
			_investigateLastGoalDist = 0f;
			_investigateSearching = false;
			_investigateSearchStartedRealtime = 0;
		}

		if ( next == ThornsBanditAiState.Patrol )
		{
			_coverGoal = default;
			_wantsSmoothFace = false;
			ResetCombatPresentation();
			HostResetEngageStuckTracking();
		}

		if ( next is ThornsBanditAiState.Investigate or ThornsBanditAiState.Dead )
		{
			HostResetEngageStuckTracking();
			if ( next != ThornsBanditAiState.Combat )
				ResetCombatPresentation();
		}

		if ( next == ThornsBanditAiState.Patrol )
		{
			_investigateSearching = false;
			_investigateSearchStartedRealtime = 0;
		}

		if ( next == ThornsBanditAiState.Combat )
		{
			_hasReactionTarget = false;
			_investigateSearching = false;
			_investigateSearchStartedRealtime = 0;
		}

		if ( next == ThornsBanditAiState.Dead )
			ThornsBanditMovement.Stop( this );
	}

	void PickPatrolGoal( bool preferAwayFromPeers = false )
	{
		var scene = Scene;
		var patrolRadius = HostGetPatrolWanderRadius();
		var minGoalDist = preferAwayFromPeers ? Math.Max( 80f, patrolRadius * 0.22f ) : 56f;

		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			Vector3 candidate;
			if ( UseLeashAnchor && LeashRadius > 4f )
			{
				var center = HostGetPatrolGoalCenter( patrolRadius );
				if ( ThornsAnimalWorldUtil.TryPickDryLandPoint( scene, center, patrolRadius, out var dryGoal ) )
					candidate = HostClampToLeash( dryGoal );
				else
				{
					var yaw = Random.Shared.NextSingle() * 360f + _patrolHeadingBiasDegrees;
					var rad = patrolRadius * MathF.Sqrt( Random.Shared.NextSingle() );
					candidate = HostClampToLeash( center + Rotation.FromYaw( yaw ).Forward * rad );
				}
			}
			else
			{
				var center = AnchorWanderGoalsToCurrentPosition ? GameObject.WorldPosition : _spawnWorld;
				if ( ThornsAnimalWorldUtil.TryPickDryLandPoint( scene, center, patrolRadius, out var wanderGoal ) )
					candidate = wanderGoal;
				else
				{
					var wanderYaw = Random.Shared.NextSingle() * 360f;
					var wanderR = patrolRadius * MathF.Sqrt( Random.Shared.NextSingle() );
					candidate = center + Rotation.FromYaw( wanderYaw ).Forward * wanderR;
				}
			}

			if ( preferAwayFromPeers )
				candidate = HostNudgePatrolGoalFromPeers( candidate, patrolRadius );

			if ( !HostPatrolGoalIsAcceptable( candidate, minGoalDist, preferAwayFromPeers ) )
				continue;

			if ( HostCanWalkToward( candidate ) )
			{
				_patrolGoal = candidate;
				ThornsBanditDebug.LogEvent(
					this,
					"PATROL",
					$"new goal dist={( _patrolGoal.WithZ( 0 ) - GameObject.WorldPosition.WithZ( 0 ) ).Length:F0} r={patrolRadius:F0}" );
				return;
			}
		}

		_patrolGoal = HostNudgePatrolGoalFromPeers( HostClampToLeash( FlatAnchor() ), patrolRadius );
	}

	bool HostPatrolGoalIsAcceptable( Vector3 candidate, float minGoalDist, bool skipPeerClearance = false )
	{
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		var goalFlat = candidate.WithZ( 0 );
		if ( ( goalFlat - selfFlat ).Length < minGoalDist )
			return false;

		if ( skipPeerClearance )
			return true;

		const float peerClearance = 64f;
		ThornsBanditSpatialGrid.QueryPlanarScratch( goalFlat, peerClearance + 48f );
		foreach ( var other in ThornsBanditSpatialGrid.ScratchResults )
		{
			if ( !other.IsValid() || other == this || other.IsDead )
				continue;

			if ( other.State is not ThornsBanditAiState.Patrol and not ThornsBanditAiState.Investigate )
				continue;

			var dist = ( goalFlat - other.GameObject.WorldPosition.WithZ( 0 ) ).Length;
			if ( dist < peerClearance )
				return false;
		}

		return true;
	}

	Vector3 HostNudgePatrolGoalFromPeers( Vector3 candidate, float patrolRadius )
	{
		var goalFlat = candidate.WithZ( 0 );
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		ThornsBanditSpatialGrid.QueryPlanarScratch( selfFlat, 140f );
		var push = Vector3.Zero;

		foreach ( var other in ThornsBanditSpatialGrid.ScratchResults )
		{
			if ( !other.IsValid() || other == this || other.IsDead )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = goalFlat - otherFlat;
			var dist = delta.Length;
			var minDist = HostGetSelfPlanarCollisionRadius() + other.HostGetSelfPlanarCollisionRadius() + 72f;
			if ( dist >= minDist )
				continue;

			var away = dist > 1e-3f ? delta / dist : ( goalFlat - selfFlat ).Normal;
			push += away * ( minDist - Math.Max( dist, 0f ) );
		}

		if ( push.LengthSquared < 4f )
			return candidate;

		var nudged = HostClampToLeash( goalFlat + push.Normal * Math.Min( patrolRadius * 0.45f, push.Length + 48f ) );
		return nudged;
	}

	bool HostCanWalkToward( Vector3 goalWorld )
	{
		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return true;

		var cc = Components.Get<CharacterController>();
		var radius = cc.IsValid() ? cc.Radius : 20f;
		var height = cc.IsValid() ? cc.Height : 72f;
		var from = GameObject.WorldPosition;
		var flatDelta = ( goalWorld - from ).WithZ( 0f );
		var len = flatDelta.Length;
		if ( len < 1f )
			return true;

		const float maxProbe = 220f;
		var probeTo = len <= maxProbe ? goalWorld : from + flatDelta / len * maxProbe;
		return !ThornsAiSolidMovementBlocker.SegmentHitsStructure(
			scene,
			GameObject,
			from,
			probeTo,
			radius,
			height,
			out _ );
	}

	float HostGetPatrolWanderRadius() =>
		UseLeashAnchor && LeashRadius > 4f
			? Math.Min( WanderRadius, LeashRadius * 0.92f )
			: WanderRadius;

	Vector3 HostGetPatrolGoalCenter( float patrolRadius )
	{
		if ( !UseLeashAnchor || LeashRadius <= 4f )
			return AnchorWanderGoalsToCurrentPosition ? GameObject.WorldPosition : _spawnWorld;

		var anchorDist = DistanceFromAnchor();
		if ( anchorDist <= patrolRadius * 0.38f )
			return FlatAnchor();

		var anchor = FlatAnchor();
		var self = GameObject.WorldPosition.WithZ( 0 );
		var t = Math.Clamp( ( anchorDist - patrolRadius * 0.38f ) / MathF.Max( 80f, patrolRadius * 0.55f ), 0f, 0.72f );
		return Vector3.Lerp( anchor, self, t );
	}

	float HostChaseDurationSeconds()
	{
		var seconds = Archetype.ChaseDurationSeconds;
		if ( LeashRadius > Archetype.LeashRadiusWorld + 80f || _engagementRangeWorld > Archetype.EngagementRangeWorld + 25f )
			seconds *= 2.4f;

		return seconds;
	}

	void PickPatrolGoalTowardAnchor()
	{
		if ( DistanceFromAnchor() > LeashRadius * 0.55f )
		{
			var anchor = FlatAnchor();
			var yaw = Random.Shared.NextSingle() * 360f;
			var rad = LeashRadius * 0.25f * MathF.Sqrt( Random.Shared.NextSingle() );
			_patrolGoal = HostClampToLeash( anchor + Rotation.FromYaw( yaw ).Forward * rad );
			return;
		}

		PickPatrolGoal();
	}

	float DistanceFromAnchor( Vector3? world = null )
	{
		var pt = world ?? GameObject.WorldPosition;
		return ( pt.WithZ( 0 ) - FlatAnchor() ).Length;
	}

	Vector3 FlatAnchor() => UseLeashAnchor ? LeashAnchorWorld.WithZ( 0 ) : _spawnWorld.WithZ( 0 );

	public Vector3 HostClampToLeash( Vector3 goalWorld ) =>
		HostClampToLeashRadius( goalWorld, LeashRadius );

	public Vector3 HostResolveMoveGoal( Vector3 goalWorld )
	{
		if ( _state == ThornsBanditAiState.Chase )
		{
			var chaseRadius = MaxChaseRadiusFromAnchor() * 0.96f;
			return HostClampToLeashRadius( goalWorld, chaseRadius );
		}

		return HostClampToLeash( goalWorld );
	}

	Vector3 HostResolveEngageMoveGoal( Vector3 goalWorld )
	{
		var resolved = HostResolveMoveGoal( goalWorld );
		var selfFlat = GameObject.WorldPosition.WithZ( 0 );
		var goalDist = ( resolved.WithZ( 0 ) - selfFlat ).Length;
		if ( goalDist >= _engageLastGoalDist - 10f )
			_engageNoProgressTicks++;
		else
			_engageNoProgressTicks = 0;

		_engageLastGoalDist = goalDist;
		if ( _engageNoProgressTicks < 3 )
			return resolved;

		_engageNoProgressTicks = 0;
		var flank = ThornsAiSolidMovementBlocker.BuildFlankGoal(
			GameObject.WorldPosition,
			resolved,
			ref _engageFlankSign );
		ThornsBanditDebug.LogPath( this, "engage-unstuck", $"goalDist={goalDist:F0} flank={flank:F0}" );
		return HostResolveMoveGoal( flank );
	}

	void HostResetEngageStuckTracking()
	{
		_engageNoProgressTicks = 0;
		_engageLastGoalDist = 0f;
	}

	Vector3 HostClampToLeashRadius( Vector3 goalWorld, float maxRadius )
	{
		if ( !UseLeashAnchor || maxRadius < 4f )
			return goalWorld;

		var anchor = FlatAnchor();
		var g = goalWorld.WithZ( 0 );
		var delta = g - anchor;
		if ( delta.LengthSquared <= maxRadius * maxRadius )
			return goalWorld;

		return anchor.WithZ( goalWorld.z ) + delta.Normal * maxRadius;
	}

	bool HostIsStandingInWater()
	{
		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Resolve( scene );
		var config = ThornsAnimalWorldUtil.ResolveTerrainConfig( scene );
		if ( !terrain.IsValid() || config is null )
			return false;

		return ThornsAnimalWorldUtil.IsUnderSeaLevel( scene, terrain, config, GameObject.WorldPosition );
	}

	void RememberCover( Vector3 cover )
	{
		if ( cover == default )
			return;

		_recentCover[_recentCoverWrite % _recentCover.Length] = cover.WithZ( 0 );
		_recentCoverWrite++;
	}

	float GetHealthFraction()
	{
		var hp = Components.Get<ThornsBanditHealth>();
		if ( !hp.IsValid() || hp.MaxHealth <= 0.01f )
			return 1f;

		return hp.CurrentHealth / hp.MaxHealth;
	}

	float DistanceFlat( GameObject other )
	{
		var a = GameObject.WorldPosition.WithZ( 0 );
		var b = other.WorldPosition.WithZ( 0 );
		return ( b - a ).Length;
	}

	bool CanFaceTargetThisFrame()
	{
		if ( !_target.IsValid() )
			return false;

		return DistanceFlat( _target ) <= LoseTargetRange;
	}

	static bool IsTargetMoving( GameObject target )
	{
		var cc = target.Components.GetInAncestorsOrSelf<CharacterController>( true );
		return cc.IsValid() && cc.Velocity.WithZ( 0 ).Length > 40f;
	}

	public void HostFaceWorldPoint( Vector3 worldPoint, float smoothSpeed = 12f )
	{
		_smoothFaceTarget = worldPoint;
		_combatFaceSmoothSpeed = smoothSpeed;
		_wantsSmoothFace = true;
	}

	public float HostGetSelfPlanarCollisionRadius()
	{
		var cc = Components.Get<CharacterController>();
		return cc.IsValid() ? Math.Max( 8f, cc.Radius ) : 20f;
	}

	public bool HostTryGetBanditPeerSeparationWish( out Vector3 separationPlanar )
	{
		separationPlanar = Vector3.Zero;
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return false;

		var flat = GameObject.WorldPosition.WithZ( 0 );
		var selfR = HostGetSelfPlanarCollisionRadius();
		const float gap = 16f;
		var maxPush = _state == ThornsBanditAiState.Patrol ? 160f : 240f;
		const float maxSepDistSq = 140f * 140f;
		var pushScale = _state == ThornsBanditAiState.Patrol ? 0.72f : 1f;

		ThornsBanditSpatialGrid.QueryPlanarScratch( flat, 140f );
		foreach ( var other in ThornsBanditSpatialGrid.ScratchResults )
		{
			if ( !other.IsValid() || other == this || other.IsDead )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var distSq = delta.LengthSquared;
			if ( distSq > maxSepDistSq )
				continue;

			var dist = MathF.Sqrt( distSq );
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + gap;
			if ( dist >= minDist - 0.5f )
				continue;

			var away = dist > 1e-3f ? delta / dist : Vector3.Right;
			separationPlanar += away * Math.Min( maxPush, ( minDist - Math.Max( dist, 0f ) ) * 10f * pushScale );
		}

		if ( separationPlanar.LengthSquared < 4f )
			return false;

		if ( separationPlanar.Length > maxPush )
			separationPlanar = separationPlanar.Normal * maxPush;

		return true;
	}

	public void HostDepenetrateFromBanditPeers()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || IsDead )
			return;

		for ( var pass = 0; pass < 2; pass++ )
			HostDepenetrateFromBanditPeersOnce();
	}

	void HostDepenetrateFromBanditPeersOnce()
	{
		var flat = GameObject.WorldPosition.WithZ( 0 );
		var selfR = HostGetSelfPlanarCollisionRadius();
		const float gap = 16f;
		var push = Vector3.Zero;

		ThornsBanditSpatialGrid.QueryPlanarScratch( flat, selfR + 104f );
		foreach ( var other in ThornsBanditSpatialGrid.ScratchResults )
		{
			if ( !other.IsValid() || other == this || other.IsDead )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + gap;
			if ( dist >= minDist - 0.35f )
				continue;

			var away = dist > 1e-3f ? delta / dist : Vector3.Random.WithZ( 0 ).Normal;
			push += away * ( minDist - Math.Max( dist, 0f ) );
		}

		if ( push.LengthSquared < 0.25f )
			return;

		GameObject.WorldPosition += push.WithZ( 0 );
	}

	public static bool HostTryResolveSpawnClearOfBanditPeers( ref Vector3 worldPos, float extraGapUnits = 14f )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return true;

		const int maxAttempts = 14;
		var flat = worldPos.WithZ( 0 );

		for ( var attempt = 0; attempt < maxAttempts; attempt++ )
		{
			if ( !HostPlanarOverlapsBanditPeer( flat, extraGapUnits, out var pushAway, out var minDist ) )
			{
				worldPos = flat.WithZ( worldPos.z );
				return true;
			}

			if ( pushAway.LengthSquared > 1e-4f )
				flat += pushAway.Normal * ( minDist + extraGapUnits * 0.35f );
			else
			{
				var ang = Random.Shared.NextSingle() * MathF.PI * 2f;
				flat += new Vector3( MathF.Cos( ang ), MathF.Sin( ang ), 0f ) * ( minDist + extraGapUnits );
			}
		}

		worldPos = flat.WithZ( worldPos.z );
		return !HostPlanarOverlapsBanditPeer( flat, extraGapUnits * 0.5f, out _, out _ );
	}

	static bool HostPlanarOverlapsBanditPeer(
		Vector3 flat,
		float extraGapUnits,
		out Vector3 pushAway,
		out float requiredSeparation )
	{
		pushAway = Vector3.Zero;
		requiredSeparation = 0f;
		const float selfR = 20f;

		foreach ( var other in ThornsBanditPopulation.HostBrainsReadOnly )
		{
			if ( !other.IsValid() || other.IsDead )
				continue;

			var otherFlat = other.GameObject.WorldPosition.WithZ( 0 );
			var delta = flat - otherFlat;
			var dist = delta.Length;
			var minDist = selfR + other.HostGetSelfPlanarCollisionRadius() + extraGapUnits;
			if ( dist >= minDist )
				continue;

			pushAway = delta;
			requiredSeparation = Math.Max( requiredSeparation, minDist - dist );
			return true;
		}

		return false;
	}
}
