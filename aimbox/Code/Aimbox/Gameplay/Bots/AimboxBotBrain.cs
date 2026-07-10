namespace Sandbox;

public enum AimboxBotState
{
	Idle,
	AdvanceToCenter,
	Hunt,
	Investigate,
	Engage,
	Reload,
	Dead
}

public sealed class AimboxBotBrain
{
	public AimboxBotState State { get; private set; } = AimboxBotState.Idle;

	readonly AimboxBotPerception _perception = new();
	Vector3 _wanderAnchor;
	Vector3 _wanderGoal;
	Vector3 _centerGoal;
	Vector3 _centerEgressGoal;
	Vector3 _centerLegGoal;
	Vector3 _centerLookPoint;
	Vector3 _investigateGoal;
	Vector3 _investigateSearchAnchor;
	bool _investigateSearching;
	TimeSince _investigateSearchTime;
	Vector3 _steerMemory;
	float _aimErrorYaw;
	float _aimErrorPitch;
	float _strafeSignSmoothed = 1f;
	TimeSince _reactionDelay;
	TimeSince _strafeFlipTimer;
	TimeSince _stationaryTimer;
	TimeSince _burstPauseTimer;
	TimeUntil _wanderResumeTime;
	TimeSince _stateEntered;
	int _burstShotsRemaining;
	int _strafeSign = 1;
	bool _hasReactionTarget;
	bool _wanderHasGoal;
	bool _wanderPaused;
	bool _centerObjectiveActive;
	bool _centerNeedsSpawnEgress;
	bool _centerHasLegGoal;
	bool _centerPaused;
	Vector3 _wanderLastProgressPos;
	Vector3 _centerLastProgressPos;
	TimeSince _wanderProgressTimer;
	TimeSince _centerProgressTimer;
	TimeUntil _centerResumeTime;
	int _centerSideSign = 1;

	public AimboxBotPerception Perception => _perception;

	public void Tick( AimboxBotController bot )
	{
		if ( bot is null )
			return;

		if ( !bot.IsAlive )
		{
			State = AimboxBotState.Dead;
			return;
		}

		_perception.Tick( bot );
		UpdateState( bot );

		switch ( State )
		{
			case AimboxBotState.AdvanceToCenter:
				TickAdvanceToCenter( bot );
				break;
			case AimboxBotState.Hunt:
				TickHunt( bot );
				break;
			case AimboxBotState.Investigate:
				TickInvestigate( bot );
				break;
			case AimboxBotState.Engage:
				TickEngage( bot );
				break;
			case AimboxBotState.Reload:
				TickReload( bot );
				break;
			default:
				TickIdle( bot );
				break;
		}
	}

	void UpdateState( AimboxBotController bot )
	{
		if ( State == AimboxBotState.Dead )
			return;

		if ( _perception.Target is not null && _perception.Target.IsAlive )
		{
			AbandonCenterObjective( bot );

			if ( State != AimboxBotState.Engage && State != AimboxBotState.Reload )
			{
				_hasReactionTarget = false;
				_reactionDelay = 0;
			}

			if ( ShouldReload( bot ) && State != AimboxBotState.Reload )
			{
				SetState( bot, AimboxBotState.Reload );
				bot.StartReload();
				return;
			}

			if ( State == AimboxBotState.Reload )
			{
				if ( bot.CurrentWeapon is { IsReloading: true } )
					return;

				SetState( bot, AimboxBotState.Engage );
				return;
			}

			SetState( bot, AimboxBotState.Engage );
			return;
		}

		if ( State == AimboxBotState.Reload && bot.CurrentWeapon is { IsReloading: false } )
			BeginInvestigate( bot, _perception.LastKnownPosition );

		if ( State == AimboxBotState.Engage )
		{
			if ( _stateEntered < AimboxBotTuning.EngageMinHoldSeconds )
				return;

			if ( _perception.RemembersThreat )
			{
				BeginInvestigate( bot, _perception.LastKnownPosition );
				return;
			}

			SetState( bot, AimboxBotState.Hunt );
			return;
		}

		if ( State == AimboxBotState.Investigate )
		{
			if ( _perception.MemoryExpired )
			{
				SetState( bot, AimboxBotState.Hunt );
				return;
			}

			if ( _perception.RemembersThreat && !_investigateSearching )
				_investigateGoal = _perception.LastKnownPosition;

			if ( !_investigateSearching && ReachedGoal( bot.WorldPosition, _investigateGoal, AimboxBotTuning.InvestigateArrivalDistance ) )
				BeginInvestigateSearch( bot );

			if ( _investigateSearching && _investigateSearchTime >= AimboxBotTuning.MemorySearchSeconds )
				SetState( bot, AimboxBotState.Hunt );

			return;
		}

		if ( _perception.RemembersThreat )
		{
			BeginInvestigate( bot, _perception.LastKnownPosition );
			return;
		}

		if ( AimboxCombatNoiseBus.TryGetLoudestGunfireHeard( bot, out var gunfire ) )
		{
			BeginInvestigate( bot, gunfire.Origin );
			return;
		}

		if ( _centerObjectiveActive )
		{
			SetState( bot, AimboxBotState.AdvanceToCenter );
			return;
		}

		SetState( bot, AimboxBotState.Hunt );
	}

	void BeginInvestigate( AimboxBotController bot, Vector3 lastKnownPosition )
	{
		_investigateGoal = lastKnownPosition;
		_investigateSearching = false;
		_investigateSearchTime = 0;
		SetState( bot, AimboxBotState.Investigate );
	}

	void BeginInvestigateSearch( AimboxBotController bot )
	{
		_investigateSearching = true;
		_investigateSearchTime = 0;
		_investigateSearchAnchor = _investigateGoal;
		_investigateGoal = PickInvestigateSearchPoint( bot );
	}

	Vector3 PickInvestigateSearchPoint( AimboxBotController bot )
	{
		for ( var attempt = 0; attempt < 6; attempt++ )
		{
			var angle = Game.Random.Float( 0f, MathF.Tau );
			var radius = Game.Random.Float( AimboxBotTuning.MemorySearchRadius * 0.35f, AimboxBotTuning.MemorySearchRadius );
			var offset = new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
			var goal = AimboxCitizenMovementMotor.SnapToGround( bot.Scene, bot.GameObject, _investigateSearchAnchor + offset );
			if ( bot.WorldPosition.Distance( goal ) > AimboxBotTuning.WanderMinGoalDistance * 0.25f )
				return goal;
		}

		return _investigateSearchAnchor;
	}

	void SetState( AimboxBotController bot, AimboxBotState state )
	{
		if ( State == state )
			return;

		var previousState = State;
		State = state;
		_stateEntered = 0;
		_steerMemory = Vector3.Zero;

		if ( state == AimboxBotState.Hunt )
		{
			_investigateSearching = false;
			if ( !_centerObjectiveActive && previousState is AimboxBotState.Engage or AimboxBotState.Investigate or AimboxBotState.Reload )
				_wanderAnchor = bot.WorldPosition;

			ResetWanderGoal();
		}

		if ( state == AimboxBotState.AdvanceToCenter )
			ResetCenterLeg();

		if ( state == AimboxBotState.Investigate )
		{
			_investigateSearching = false;
			_investigateSearchTime = 0;
		}

		if ( state is AimboxBotState.AdvanceToCenter or AimboxBotState.Hunt or AimboxBotState.Investigate or AimboxBotState.Idle )
			EnterLocomotionPresentation( bot );
	}

	void EnterLocomotionPresentation( AimboxBotController bot )
	{
		_aimErrorPitch = 0f;
		_aimErrorYaw = 0f;
		bot.ResetLocomotionPresentation();
	}

	void TickIdle( AimboxBotController bot )
	{
		bot.WantsAds = false;
		bot.SetMovement( Vector3.Zero, false, false );
	}

	void TickAdvanceToCenter( AimboxBotController bot )
	{
		bot.WantsAds = false;

		if ( !_centerObjectiveActive )
		{
			SetState( bot, AimboxBotState.Hunt );
			return;
		}

		if ( FlatDistance( bot.WorldPosition, _centerGoal ) <= AimboxBotTuning.CenterObjectiveArrivalDistance )
		{
			CompleteCenterObjective( bot );
			return;
		}

		if ( _centerNeedsSpawnEgress )
		{
			if ( ReachedGoal( bot.WorldPosition, _centerEgressGoal, AimboxBotTuning.CenterLegArrivalDistance ) )
			{
				_centerNeedsSpawnEgress = false;
				ResetCenterLeg();
				_centerPaused = true;
				_centerResumeTime = Game.Random.Float(
					AimboxBotTuning.CenterPauseMinSeconds,
					AimboxBotTuning.CenterPauseMaxSeconds );
				_centerLookPoint = PickCenterLookPoint( bot );
				bot.SetMovement( Vector3.Zero, false, false );
				LookAt( bot, _centerLookPoint, AimboxBotTuning.WanderLookSmoothSpeed );
				return;
			}

			TickCenterProgress( bot );
			MoveLocomote(
				bot,
				_centerEgressGoal,
				sprint: false,
				speedMultiplier: AimboxBotTuning.CenterAdvanceSpeedMultiplier,
				allowSteer: true,
				steerProbeDistance: AimboxBotTuning.CenterSteerProbeDistance );
			return;
		}

		if ( _centerPaused )
		{
			if ( _centerResumeTime > 0f )
			{
				bot.SetMovement( Vector3.Zero, false, false );
				LookAt( bot, _centerLookPoint, AimboxBotTuning.WanderLookSmoothSpeed );
				return;
			}

			_centerPaused = false;
			ResetCenterLeg();
		}

		if ( !_centerHasLegGoal || ReachedGoal( bot.WorldPosition, _centerLegGoal, AimboxBotTuning.CenterLegArrivalDistance ) )
		{
			if ( _centerHasLegGoal )
			{
				_centerPaused = true;
				_centerResumeTime = Game.Random.Float(
					AimboxBotTuning.CenterPauseMinSeconds,
					AimboxBotTuning.CenterPauseMaxSeconds );
				_centerLookPoint = PickCenterLookPoint( bot );
				bot.SetMovement( Vector3.Zero, false, false );
				LookAt( bot, _centerLookPoint, AimboxBotTuning.WanderLookSmoothSpeed );
				return;
			}

			_centerLegGoal = PickCenterLegGoal( bot );
			_centerHasLegGoal = true;
			_centerLastProgressPos = bot.WorldPosition;
			_centerProgressTimer = 0;
		}

		TickCenterProgress( bot );
		MoveLocomote(
			bot,
			_centerLegGoal,
			sprint: false,
			speedMultiplier: AimboxBotTuning.CenterAdvanceSpeedMultiplier,
			allowSteer: true,
			steerProbeDistance: AimboxBotTuning.CenterSteerProbeDistance,
			forceSteer: true );
	}

	void TickHunt( AimboxBotController bot )
	{
		bot.WantsAds = false;

		if ( _wanderPaused )
		{
			if ( _wanderResumeTime > 0f )
			{
				bot.SetMovement( Vector3.Zero, false, false );
				FaceWalkDirection( bot, bot.WorldRotation.Forward.WithZ( 0 ), 0f );
				return;
			}

			_wanderPaused = false;
			ResetWanderGoal();
		}

		if ( !_wanderHasGoal || ReachedGoal( bot.WorldPosition, _wanderGoal, AimboxBotTuning.WanderArrivalDistance ) )
		{
			if ( _wanderHasGoal )
			{
				_wanderPaused = true;
				_wanderResumeTime = Game.Random.Float(
					AimboxBotTuning.WanderPauseMinSeconds,
					AimboxBotTuning.WanderPauseMaxSeconds );
				bot.SetMovement( Vector3.Zero, false, false );
				FaceWalkDirection( bot, bot.WorldRotation.Forward.WithZ( 0 ), 0f );
				return;
			}

			_wanderGoal = PickWanderGoal( bot );
			_wanderHasGoal = true;
			_wanderLastProgressPos = bot.WorldPosition;
			_wanderProgressTimer = 0;
		}

		TickWanderProgress( bot );
		MoveLocomote(
			bot,
			_wanderGoal,
			sprint: false,
			speedMultiplier: AimboxBotTuning.WanderSpeedMultiplier,
			allowSteer: false );
	}

	void TickInvestigate( AimboxBotController bot )
	{
		bot.WantsAds = false;

		if ( _investigateSearching
		     && ReachedGoal( bot.WorldPosition, _investigateGoal, AimboxBotTuning.InvestigateArrivalDistance * 0.85f )
		     && _investigateSearchTime < AimboxBotTuning.MemorySearchSeconds )
			_investigateGoal = PickInvestigateSearchPoint( bot );

		MoveLocomote(
			bot,
			_investigateGoal,
			sprint: bot.WorldPosition.Distance( _investigateGoal ) > AimboxBotTuning.HuntSprintDistance,
			allowSteer: true );

		var lookPoint = _perception.LastKnownPosition;
		if ( lookPoint != Vector3.Zero )
			LookAt( bot, lookPoint, AimboxBotTuning.WanderLookSmoothSpeed );
	}

	void TickWanderProgress( AimboxBotController bot )
	{
		if ( bot.WorldPosition.Distance( _wanderLastProgressPos ) >= AimboxBotTuning.WanderStuckMinProgress )
		{
			_wanderLastProgressPos = bot.WorldPosition;
			_wanderProgressTimer = 0;
			return;
		}

		if ( _wanderProgressTimer < AimboxBotTuning.WanderStuckSeconds )
			return;

		_wanderGoal = PickWanderGoal( bot );
		_wanderHasGoal = true;
		_wanderLastProgressPos = bot.WorldPosition;
		_wanderProgressTimer = 0;
	}

	void TickCenterProgress( AimboxBotController bot )
	{
		if ( FlatDistance( bot.WorldPosition, _centerLastProgressPos ) >= AimboxBotTuning.CenterStuckMinProgress )
		{
			_centerLastProgressPos = bot.WorldPosition;
			_centerProgressTimer = 0;
			return;
		}

		if ( _centerProgressTimer < AimboxBotTuning.CenterStuckSeconds )
			return;

		_centerSideSign *= -1;
		if ( _centerNeedsSpawnEgress && TryPickSpawnPocketEgressGoal( bot, out var egressGoal, _centerSideSign ) )
		{
			_centerEgressGoal = egressGoal;
			_centerHasLegGoal = false;
		}
		else
		{
			_centerLegGoal = PickCenterLegGoal( bot );
			_centerHasLegGoal = true;
		}

		_centerLastProgressPos = bot.WorldPosition;
		_centerProgressTimer = 0;
	}

	void TickEngage( AimboxBotController bot )
	{
		var target = _perception.Target;
		if ( target is null || !target.IsAlive )
			return;

		var toTarget = target.WorldPosition - bot.WorldPosition;
		var flatDistance = toTarget.WithZ( 0 ).Length;
		var isLongRange = flatDistance > AimboxBotTuning.LongRangeEngageDistance;
		var wantsAds = flatDistance > AimboxBotTuning.AdsMinDistance && !bot.CurrentWeapon.Definition.IsMelee;
		bot.WantsAds = wantsAds;

		var aimPoint = target.EyePosition.LerpTo( target.WorldPosition + Vector3.Up * 48f, isLongRange ? 0.28f : 0.35f );
		var aimSmooth = isLongRange
			? AimboxBotTuning.LongRangeAimSmoothSpeed
			: AimboxBotTuning.AimSmoothSpeed;
		LookAt( bot, aimPoint, aimSmooth );

		if ( !_hasReactionTarget && _reactionDelay < AimboxBotTuning.ReactionDelaySeconds )
		{
			bot.SetMovement( Vector3.Zero, false, false );
			bot.WantsFire = false;
			return;
		}

		if ( !_hasReactionTarget )
		{
			_hasReactionTarget = true;
			RefreshAimError( wantsAds, flatDistance );
		}

		if ( isLongRange )
		{
			bot.SetMovement( Vector3.Zero, false, _stationaryTimer > AimboxBotTuning.CrouchStationarySeconds );
		}
		else
		{
			if ( _strafeFlipTimer <= 0f )
			{
				_strafeSign = Game.Random.Int( 0, 1 ) == 0 ? -1 : 1;
				_strafeFlipTimer = Game.Random.Float( AimboxBotTuning.StrafeFlipMinSeconds, AimboxBotTuning.StrafeFlipMaxSeconds );
			}

			var strafeBlend = Math.Clamp( Time.Delta * AimboxBotTuning.StrafeSignSmoothSpeed, 0f, 1f );
			_strafeSignSmoothed = MathX.Lerp( _strafeSignSmoothed, _strafeSign, strafeBlend );

			var toTargetFlat = toTarget.WithZ( 0 );
			var toTargetNorm = flatDistance > 0.01f ? toTargetFlat / flatDistance : bot.WorldRotation.Forward.WithZ( 0 ).Normal;
			var strafeDir = bot.WorldRotation.Right * _strafeSignSmoothed;

			var forwardBias = 0f;
			if ( flatDistance > AimboxBotTuning.EngageTooFarDistance )
				forwardBias = 0.4f;
			else if ( flatDistance < AimboxBotTuning.EngageTooCloseDistance )
				forwardBias = -0.35f;
			else
			{
				var rangeError = ( flatDistance - AimboxBotTuning.EngageIdealDistance ) / AimboxBotTuning.EngageIdealDistance;
				forwardBias = Math.Clamp( rangeError * 0.35f, -0.25f, 0.25f );
			}

			var wish = ( strafeDir + toTargetNorm * forwardBias ).WithZ( 0 );
			if ( wish.Length > 0.01f )
				wish = wish.Normal;

			wish = SteerWish( bot, wish );
			var moving = wish.Length > 0.08f;
			if ( moving )
				_stationaryTimer = 0;

			var crouch = _stationaryTimer > AimboxBotTuning.CrouchStationarySeconds;
			bot.SetMovement( wish, false, crouch );
		}

		if ( _burstShotsRemaining <= 0 )
		{
			if ( _burstPauseTimer < Game.Random.Float( AimboxBotTuning.BurstPauseMinSeconds, AimboxBotTuning.BurstPauseMaxSeconds ) )
			{
				bot.WantsFire = false;
				return;
			}

			RefreshAimError( wantsAds, flatDistance );
			_burstShotsRemaining = Game.Random.Int( AimboxBotTuning.BurstMinRounds, AimboxBotTuning.BurstMaxRounds );
			_burstPauseTimer = 0;
		}

		bot.WantsFire = _perception.TargetHasLineOfSight( bot );
		if ( bot.WantsFire && bot.TryFireWeapon() )
			_burstShotsRemaining--;
	}

	void TickReload( AimboxBotController bot )
	{
		bot.SetMovement( Vector3.Zero, false, true );
		bot.WantsFire = false;

		var lookTarget = _perception.Target ?? _perception.RememberedTarget;
		if ( lookTarget is not null )
			LookAt( bot, lookTarget.EyePosition );
	}

	bool ShouldReload( AimboxBotController bot )
	{
		var weapon = bot.CurrentWeapon;
		if ( weapon is null || weapon.IsReloading )
			return false;

		if ( weapon.Ammo <= 0 )
			return true;

		var magFraction = weapon.Ammo / (float)Math.Max( 1, weapon.EffectiveMagazineSize );
		return magFraction < AimboxBotTuning.ReloadMagFractionThreshold && weapon.Reserve > 0;
	}

	void MoveLocomote(
		AimboxBotController bot,
		Vector3 goal,
		bool sprint,
		float speedMultiplier = 1f,
		bool allowSteer = true,
		float steerProbeDistance = 0f,
		bool forceSteer = false )
	{
		var wish = ( goal - bot.WorldPosition ).WithZ( 0 );
		if ( wish.Length <= 0.01f )
		{
			bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
			return;
		}

		wish = wish.Normal;
		if ( forceSteer )
		{
			wish = SteerWish( bot, wish, steerProbeDistance );
			if ( wish.Length <= 0.01f )
			{
				bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
				return;
			}

			wish = wish.Normal;
		}

		if ( IsWalkBlocked( bot, wish ) )
		{
			if ( !allowSteer )
			{
				_wanderGoal = PickWanderGoal( bot );
				_wanderHasGoal = true;
				wish = ( _wanderGoal - bot.WorldPosition ).WithZ( 0 );
				if ( wish.Length <= 0.01f )
				{
					bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
					return;
				}

				wish = wish.Normal;
				if ( IsWalkBlocked( bot, wish ) )
				{
					bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
					return;
				}
			}
			else
			{
				wish = SteerWish( bot, wish, steerProbeDistance );
				if ( wish.Length <= 0.01f )
				{
					bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
					return;
				}

				wish = wish.Normal;
			}
		}

		var forward = bot.WorldRotation.Forward.WithZ( 0 );
		if ( forward.Length <= 0.01f )
			forward = wish;
		else
			forward = forward.Normal;

		var facingDelta = Vector3.GetAngle( forward, wish );
		if ( facingDelta > AimboxBotTuning.WanderMaxWalkAngle )
		{
			bot.SetMovement( Vector3.Zero, sprint, false, speedMultiplier );
			FaceWalkDirection( bot, wish, AimboxBotTuning.WanderTurnSpeed );
			return;
		}

		FaceWalkDirection( bot, wish, AimboxBotTuning.WanderLookSmoothSpeed );
		forward = bot.WorldRotation.Forward.WithZ( 0 ).Normal;
		bot.SetMovement( forward, sprint, false, speedMultiplier );
	}

	void FaceWalkDirection( AimboxBotController bot, Vector3 walkDirection, float smoothSpeed )
	{
		walkDirection = walkDirection.WithZ( 0 );
		if ( walkDirection.Length <= 0.01f )
		{
			bot.SetLookAngles( 0f, bot.WorldRotation.Angles().yaw );
			return;
		}

		var targetYaw = Rotation.LookAt( walkDirection.Normal ).Angles().yaw;
		var currentYaw = bot.WorldRotation.Angles().yaw;
		if ( smoothSpeed <= 0f )
		{
			bot.SetLookAngles( 0f, targetYaw );
			return;
		}

		var delta = ( targetYaw - currentYaw ).NormalizeDegrees();
		var t = Math.Clamp( Time.Delta * smoothSpeed, 0f, 1f );
		bot.SetLookAngles( 0f, currentYaw + delta * t );
	}

	static bool IsWalkBlocked( AimboxBotController bot, Vector3 direction )
	{
		direction = direction.WithZ( 0 );
		if ( direction.Length <= 0.01f )
			return true;

		var start = bot.WorldPosition + Vector3.Up * 32f;
		var end = start + direction.Normal * AimboxBotTuning.WanderWalkProbeDistance;
		var tr = bot.Scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( bot.GameObject )
			.Run();
		return tr.Hit && tr.Distance < AimboxBotTuning.WanderWalkProbeDistance * 0.85f;
	}

	Vector3 SteerWish( AimboxBotController bot, Vector3 wish, float probeDistance = 0f )
	{
		if ( probeDistance <= 0f )
			probeDistance = AimboxBotTuning.WanderWalkProbeDistance;

		_steerMemory = AimboxCitizenMovementMotor.SteerAroundObstacles(
			bot.Scene,
			bot.GameObject,
			bot.WorldPosition,
			wish,
			probeDistance,
			_steerMemory,
			Time.Delta );
		return _steerMemory;
	}

	void LookAt( AimboxBotController bot, Vector3 worldPoint, float smoothSpeed = AimboxBotTuning.AimSmoothSpeed )
	{
		var to = ( worldPoint - bot.EyePosition ).Normal;
		var targetAngles = Rotation.LookAt( to ).Angles();
		targetAngles.pitch += _aimErrorPitch;
		targetAngles.yaw += _aimErrorYaw;

		var current = bot.EyeRotation.Angles();
		var t = Math.Clamp( Time.Delta * smoothSpeed, 0f, 1f );
		var pitch = MathX.Lerp( current.pitch, targetAngles.pitch, t );
		var yaw = MathX.Lerp( current.yaw, targetAngles.yaw, t );
		bot.SetLookAngles( pitch, yaw );
	}

	void RefreshAimError( bool ads, float distance )
	{
		var spread = ads ? AimboxBotTuning.AdsAimErrorDegrees : AimboxBotTuning.HipAimErrorDegrees;
		var distanceScale = Math.Clamp(
			AimboxBotTuning.AimErrorReferenceDistance / MathF.Max( distance, 1f ),
			AimboxBotTuning.MinAimErrorScale,
			1f );
		spread *= distanceScale;
		_aimErrorYaw = Game.Random.Float( -spread, spread );
		_aimErrorPitch = Game.Random.Float( -spread * 0.6f, spread * 0.6f );
	}

	void BeginCenterObjective( AimboxBotController bot )
	{
		_centerGoal = AimboxSpawnResolve.GetArenaCenter( bot.Scene );
		_centerGoal = AimboxSpawnClearance.ResolveClearFeetPosition(
			bot.Scene,
			bot.GameObject,
			_centerGoal,
			AimboxBotTuning.CenterGoalClearanceSearchRadius );

		_centerObjectiveActive = FlatDistance( bot.WorldPosition, _centerGoal ) > AimboxBotTuning.CenterObjectiveArrivalDistance;
		_centerSideSign = Game.Random.Int( 0, 1 ) == 0 ? -1 : 1;
		_centerNeedsSpawnEgress = _centerObjectiveActive && TryPickSpawnPocketEgressGoal( bot, out _centerEgressGoal, _centerSideSign );
		_centerLookPoint = PickCenterLookPoint( bot );
		ResetCenterLeg();
	}

	void AbandonCenterObjective( AimboxBotController bot )
	{
		if ( !_centerObjectiveActive )
			return;

		_centerObjectiveActive = false;
		_centerNeedsSpawnEgress = false;
		_wanderAnchor = bot.WorldPosition;
		ResetCenterLeg();
	}

	void CompleteCenterObjective( AimboxBotController bot )
	{
		_centerObjectiveActive = false;
		_centerNeedsSpawnEgress = false;
		_wanderAnchor = AimboxCitizenMovementMotor.SnapToGround( bot.Scene, bot.GameObject, _centerGoal );
		ResetCenterLeg();
		SetState( bot, AimboxBotState.Hunt );
	}

	bool TryPickSpawnPocketEgressGoal( AimboxBotController bot, out Vector3 goal, int preferredSideSign = 0 )
	{
		goal = default;

		var cfg = AimboxMapCatalog.Get( AimboxArenaConfig.ActiveMap ).Layout;
		var spawnEdgeX = cfg.ArenaHalfWidth - cfg.SpawnInset;
		var activeMinX = spawnEdgeX - cfg.WallThickness * AimboxBotTuning.CenterSpawnPocketActiveWallThicknesses;
		if ( MathF.Abs( bot.WorldPosition.x ) < activeMinX )
			return false;

		var exitY = cfg.SpawnSpreadY * AimboxBotTuning.CenterSpawnPocketExitYFraction;
		if ( MathF.Abs( bot.WorldPosition.y ) >= exitY - AimboxBotTuning.CenterLegArrivalDistance )
			return false;

		var sideSign = preferredSideSign != 0
			? Math.Sign( preferredSideSign )
			: MathF.Abs( bot.WorldPosition.y ) > AimboxBotTuning.CenterLegArrivalDistance
				? Math.Sign( bot.WorldPosition.y )
				: Game.Random.Int( 0, 1 ) == 0 ? -1 : 1;

		_centerSideSign = sideSign;
		var desired = new Vector3( bot.WorldPosition.x, sideSign * exitY, bot.WorldPosition.z );
		goal = AimboxSpawnClearance.ResolveClearFeetPosition(
			bot.Scene,
			bot.GameObject,
			desired,
			AimboxBotTuning.CenterLegClearanceSearchRadius );
		return true;
	}

	Vector3 PickCenterLegGoal( AimboxBotController bot )
	{
		var toCenter = ( _centerGoal - bot.WorldPosition ).WithZ( 0 );
		var distance = toCenter.Length;
		if ( distance <= AimboxBotTuning.CenterObjectiveArrivalDistance )
			return _centerGoal;

		var forward = distance > 0.01f ? toCenter / distance : bot.WorldRotation.Forward.WithZ( 0 ).Normal;
		if ( forward.Length <= 0.01f )
			forward = Vector3.Forward;

		if ( Game.Random.Float( 0f, 1f ) < 0.38f )
			_centerSideSign *= -1;

		var side = new Vector3( -forward.y, forward.x, 0f ) * _centerSideSign;
		var stride = Math.Clamp(
			Game.Random.Float( AimboxBotTuning.CenterLegMinDistance, AimboxBotTuning.CenterLegMaxDistance ),
			AimboxBotTuning.CenterLegMinDistance,
			MathF.Max( AimboxBotTuning.CenterLegMinDistance, distance - AimboxBotTuning.CenterObjectiveArrivalDistance * 0.35f ) );
		var lateral = Game.Random.Float( AimboxBotTuning.CenterLateralMinOffset, AimboxBotTuning.CenterLateralMaxOffset );
		lateral = MathF.Min( lateral, MathF.Max( AimboxBotTuning.CenterLateralMinOffset, distance * 0.55f ) );

		var goal = bot.WorldPosition + forward * stride + side * lateral;
		if ( FlatDistance( goal, _centerGoal ) < AimboxBotTuning.CenterObjectiveArrivalDistance * 0.65f )
			goal = _centerGoal;

		return AimboxSpawnClearance.ResolveClearFeetPosition(
			bot.Scene,
			bot.GameObject,
			goal,
			AimboxBotTuning.CenterLegClearanceSearchRadius );
	}

	Vector3 PickCenterLookPoint( AimboxBotController bot )
	{
		var toCenter = ( _centerGoal - bot.WorldPosition ).WithZ( 0 );
		var forward = toCenter.Length > 0.01f ? toCenter.Normal : bot.WorldRotation.Forward.WithZ( 0 ).Normal;
		if ( forward.Length <= 0.01f )
			forward = Vector3.Forward;

		var yawOffset = Game.Random.Float( -72f, 72f );
		var lookDir = Rotation.FromYaw( yawOffset ) * forward;
		return bot.EyePosition + lookDir.WithZ( 0 ).Normal * AimboxBotTuning.CenterLookDistance + Vector3.Up * Game.Random.Float( -12f, 18f );
	}

	Vector3 PickWanderGoal( AimboxBotController bot )
	{
		for ( var attempt = 0; attempt < 8; attempt++ )
		{
			var angle = Game.Random.Float( 0f, MathF.Tau );
			var radius = Game.Random.Float(
				AimboxBotTuning.WanderMinGoalDistance,
				AimboxBotTuning.WanderLeashRadius );
			var offset = new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 0f );
			var goal = AimboxCitizenMovementMotor.SnapToGround( bot.Scene, bot.GameObject, _wanderAnchor + offset );
			if ( bot.WorldPosition.Distance( goal ) >= AimboxBotTuning.WanderMinGoalDistance * 0.5f )
				return goal;
		}

		return AimboxCitizenMovementMotor.SnapToGround( bot.Scene, bot.GameObject, _wanderAnchor );
	}

	void ResetWanderGoal()
	{
		_wanderHasGoal = false;
		_wanderPaused = false;
		_wanderResumeTime = 0f;
		_wanderLastProgressPos = Vector3.Zero;
		_wanderProgressTimer = 0f;
	}

	void ResetCenterLeg()
	{
		_centerHasLegGoal = false;
		_centerPaused = false;
		_centerResumeTime = 0f;
		_centerLastProgressPos = Vector3.Zero;
		_centerProgressTimer = 0f;
	}

	static float FlatDistance( Vector3 a, Vector3 b ) =>
		a.WithZ( 0 ).Distance( b.WithZ( 0 ) );

	static bool ReachedGoal( Vector3 position, Vector3 goal, float threshold ) =>
		FlatDistance( position, goal ) <= threshold;

	public void OnSpawned( AimboxBotController bot )
	{
		State = AimboxBotState.Hunt;
		_stateEntered = 0;
		_wanderAnchor = bot.WorldPosition;
		BeginCenterObjective( bot );
		ResetWanderGoal();
		_steerMemory = Vector3.Zero;
		_aimErrorPitch = 0f;
		_aimErrorYaw = 0f;
		bot.ResetLocomotionPresentation();
		_strafeSignSmoothed = 1f;
		_perception.ClearTarget();
		_hasReactionTarget = false;
		_reactionDelay = 0;
		_burstShotsRemaining = 0;
		_burstPauseTimer = 0;
	}
}
