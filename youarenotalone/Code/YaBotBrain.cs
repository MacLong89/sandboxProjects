using System;
using System.Linq;

namespace Sandbox;

[Title( "YouAreNotAlone — Bot brain" )]
[Category( "YouAreNotAlone" )]
[Icon( "robot_2" )]
public sealed class YaBotBrain : Component
{
	enum HunterState
	{
		Patrol,
		Engage,
		Reposition,
		Recover
	}

	[Property] public YaPlayerRole BotRole { get; set; } = YaPlayerRole.NotAlone;
	[Property] public float MoveSpeed { get; set; } = 250f;
	[Property] public float AloneMeleeDamage { get; set; } = 34f;
	[Property] public float HunterGunDamage { get; set; } = 9f;

	CharacterController _controller;
	Vector3 _homePos;
	Vector3 _roamTarget;
	double _nextRoamPickAt;
	double _nextShotAt;
	double _nextMeleeAt;
	double _aloneAttackCycleStart;
	double _nextPracticeParanoiaAt;
	double _nextBurstStartAt;
	double _stateEndsAt;
	int _burstShotsRemaining;
	bool _despawnQueued;
	HunterState _hunterState;
	Vector3 _recoverTarget;
	Vector3 _lastPos;
	float _stuckSeconds;

	// --- Hunter (Not Alone) smoothing: LOS hysteresis, sticky strafe/flank, slewed move + aim ---
	Vector3 _smoothedHunterMoveDir;
	Vector3 _smoothedHunterLookDir;
	float _hunterLosFalseAcc;
	float _hunterLosTrueAcc;
	Vector3 _hunterStickyNavWorld;
	double _hunterStickyNavUntil;
	Vector3 _hunterRepositionFlank;
	Vector3 _hunterMoveGoal;

	const float NavProbeMaxDist = 130f;
	const float NavProbeLateralOffset = 14f;
	const float WallPadding = 10f;

	protected override void OnAwake()
	{
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.UseCollisionRules = true;
		_controller.Height = 72f;
		_controller.Radius = 20f;
		_controller.StepHeight = 18f;
		_homePos = GameObject.WorldPosition;
		_roamTarget = _homePos;
		_aloneAttackCycleStart = Time.Now + Random.Shared.Float( 0f, 3f );
		_nextPracticeParanoiaAt = Time.Now + Random.Shared.Float( 8f, 14f );
		_hunterState = HunterState.Patrol;
		_lastPos = GameObject.WorldPosition;
		var fwd = GameObject.WorldRotation.Forward.WithZ( 0f );
		_smoothedHunterMoveDir = fwd.LengthSquared > 0.0001f ? fwd.Normal : Vector3.Forward;
		_smoothedHunterLookDir = _smoothedHunterMoveDir;
		_hunterStickyNavUntil = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		var practice = YaPracticeModeSystem.Instance;
		if ( practice is null || !practice.IsValid() || !practice.PracticeActive )
			return;

		var hp = Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		if ( hp is not { IsValid: true, IsAlive: true } || hp.IsDeadState )
		{
			if ( !_despawnQueued )
			{
				_despawnQueued = true;
				GameObject.Destroy();
			}
			return;
		}

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( BotRole == YaPlayerRole.NotAlone )
			TickHunterBot( scene );
		else if ( BotRole == YaPlayerRole.Alone )
			TickAloneBot( scene );
	}

	void TickHunterBot( Scene scene )
	{
		var aloneTarget = FindAliveRoleTarget( scene, YaPlayerRole.Alone );
		if ( aloneTarget is null || !aloneTarget.IsValid() )
		{
			_hunterState = HunterState.Patrol;
			_hunterLosFalseAcc = 0f;
			_hunterLosTrueAcc = 0f;
			_hunterStickyNavUntil = 0;
			MoveTowardSmart( PickOrKeepRoamTarget(), allowFacing: true, null );
			return;
		}

		var targetPos = aloneTarget.WorldPosition;
		var toTarget = targetPos - GameObject.WorldPosition;
		var dist = toTarget.WithZ( 0f ).Length;
		var hasLos = HasLineOfSightTo( aloneTarget );
		var lookAt = targetPos + Vector3.Up * 40f;

		var hunterStateBefore = _hunterState;

		if ( hasLos )
		{
			_hunterLosTrueAcc += Time.Delta;
			_hunterLosFalseAcc = Math.Max( 0f, _hunterLosFalseAcc - (float)Time.Delta * 2.2f );
		}
		else
		{
			_hunterLosFalseAcc += Time.Delta;
			_hunterLosTrueAcc = Math.Max( 0f, _hunterLosTrueAcc - (float)Time.Delta * 3f );
		}

		switch ( _hunterState )
		{
			case HunterState.Patrol:
				if ( dist < 480f || (hasLos && dist < 880f) )
					_hunterState = HunterState.Engage;
				break;
			case HunterState.Engage:
				if ( _hunterLosFalseAcc > 0.34f && dist < 760f )
				{
					_hunterState = HunterState.Reposition;
					_stateEndsAt = Time.Now + Random.Shared.Float( 1.05f, 2.05f );
				}
				break;
			case HunterState.Reposition:
				if ( Time.Now >= _stateEndsAt || _hunterLosTrueAcc > 0.22f )
				{
					_hunterState = HunterState.Engage;
					_hunterStickyNavUntil = 0;
				}
				break;
			case HunterState.Recover:
				if ( Time.Now >= _stateEndsAt )
					_hunterState = HunterState.Patrol;
				break;
		}

		if ( _hunterState == HunterState.Reposition && hunterStateBefore != HunterState.Reposition )
			PickHunterRepositionFlankOnce( toTarget.WithZ( 0f ).Normal );

		if ( _hunterState == HunterState.Recover )
		{
			MoveTowardSmart( _recoverTarget, allowFacing: true, lookAt, _recoverTarget );
			TickStuckRecovery( true, aloneTarget?.WorldPosition );
			return;
		}

		if ( _hunterState == HunterState.Reposition )
		{
			MoveTowardSmart( _hunterRepositionFlank, allowFacing: true, lookAt, aloneTarget.WorldPosition );
			TickStuckRecovery( true, aloneTarget.WorldPosition );
			return;
		}

		// Engage / Patrol: hold a strafe point for a while instead of re-rolling every tick (removes jitter).
		if ( dist > 380f )
		{
			_hunterStickyNavUntil = 0;
			MoveTowardSmart( targetPos, allowFacing: true, lookAt, targetPos );
		}
		else
		{
			if ( Time.Now >= _hunterStickyNavUntil )
			{
				var side = Vector3.Cross( Vector3.Up, toTarget.WithZ( 0f ).Normal ).Normal;
				if ( Random.Shared.Float( 0f, 1f ) < 0.5f )
					side *= -1f;
				if ( !TryPickReachableWorldPoint( GameObject.WorldPosition, side, 72f, 148f, out _hunterStickyNavWorld ) )
					_hunterStickyNavWorld = PickSafeNearbyPoint( side, 72f, 148f );
				_hunterStickyNavUntil = Time.Now + Random.Shared.Float( 0.65f, 1.25f );
			}

			MoveTowardSmart( _hunterStickyNavWorld, allowFacing: true, lookAt, targetPos );
		}

		TickStuckRecovery( true, aloneTarget.WorldPosition );

		var targetController = aloneTarget.Components.Get<CharacterController>( FindMode.EnabledInSelf );
		var targetMoving = targetController.IsValid() && targetController.Velocity.WithZ( 0f ).Length > 45f;
		if ( !targetMoving || !hasLos )
			return;

		// Burst cadence + ~12% hit chance per LOS shot attempt (still gated by LOS after roll).
		if ( _burstShotsRemaining <= 0 )
		{
			if ( Time.Now < _nextBurstStartAt )
				return;
			_burstShotsRemaining = Random.Shared.Int( 2, 5 );
			_nextBurstStartAt = Time.Now + Random.Shared.Float( 0.75f, 1.6f );
		}

		if ( Time.Now < _nextShotAt )
			return;

		// Fire-attempt cadence tuned to 3x previous rate while preserving low hit probability.
		_nextShotAt = Time.Now + Random.Shared.Float( 0.043f, 0.08f );
		_burstShotsRemaining--;
		SendHunterWorldShotSound();

		if ( Random.Shared.Float( 0f, 1f ) > 0.12f )
			return;
		if ( !HasLineOfSightTo( aloneTarget ) )
			return;

		var targetHealth = aloneTarget.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		if ( targetHealth.IsValid() )
		{
			targetHealth.TakeDamage( HunterGunDamage, new DamageContext
			{
				AttackerRoot = GameObject,
				Headshot = false,
				Kind = "bot_rifle"
			} );
		}
	}

	void SendHunterWorldShotSound()
	{
		if ( !Networking.IsHost )
			return;

		var pos = GameObject.WorldPosition + Vector3.Up * 48f;
		RpcPlayHunterWorldShotSound( pos, 0.95f );
	}

	[Rpc.Broadcast]
	void RpcPlayHunterWorldShotSound( Vector3 worldPos, float volume )
	{
		var h = Sound.Play( YaWeapon.M4FireSoundResource, worldPos );
		if ( h is { IsValid: true } snd )
			snd.Volume *= Math.Clamp( volume, 0f, 2f );
	}

	void TickPracticeAloneAutoParanoia()
	{
		var practice = YaPracticeModeSystem.Instance;
		if ( practice is not { IsValid: true, PracticeActive: true, HumanChosenRole: YaPlayerRole.NotAlone } )
			return;

		var flow = YaGameStateSystem.Instance;
		if ( flow is null || !flow.IsValid() || flow.CurrentState != YaGameState.InRound )
			return;

		if ( Time.Now < _nextPracticeParanoiaAt )
			return;

		_nextPracticeParanoiaAt = Time.Now + Random.Shared.Float( 27f, 33f );

		var mech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		if ( !mech.IsValid() )
			return;

		mech.HostTryPracticeAutoParanoia();
	}

	void TickAloneBot( Scene scene )
	{
		TickPracticeAloneAutoParanoia();

		var target = FindNearestAliveNotAlone( scene );
		if ( target is null || !target.IsValid() )
		{
			MoveTowardSmart( PickOrKeepRoamTarget(), allowFacing: true );
			TickStuckRecovery( true );
			return;
		}

		var cycle = (float)( (Time.Now - _aloneAttackCycleStart) % 20.0 );
		var attackWindow = cycle < 5f;

		var targetPos = target.WorldPosition;
		var toTarget = targetPos - GameObject.WorldPosition;
		var dist = toTarget.Length;

		if ( attackWindow )
		{
			MoveTowardSmart( targetPos, allowFacing: true );
			if ( dist <= 95f && Time.Now >= _nextMeleeAt )
			{
				_nextMeleeAt = Time.Now + 0.9;
				var hp = target.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
				if ( hp.IsValid() )
				{
					hp.TakeDamage( AloneMeleeDamage, new DamageContext
					{
						AttackerRoot = GameObject,
						Headshot = false,
						Kind = "bot_melee"
					} );
				}
			}
		}
		else
		{
			var retreatBias = -toTarget.WithZ( 0f );
			var retreatPoint = retreatBias.LengthSquared > 0.02f
				? PickSafeNearbyPoint( retreatBias.Normal, 180f, 360f )
				: PickOrKeepRoamTarget();
			MoveTowardSmart( retreatPoint, allowFacing: true );
		}
		TickStuckRecovery( true );
	}

	GameObject FindAliveRoleTarget( Scene scene, YaPlayerRole role )
	{
		foreach ( var root in scene.GetAllComponents<YaPlayerRoleComponent>()
			         .Where( r => r.IsValid() && r.Role == role )
			         .Select( r => r.GameObject ) )
		{
			if ( role == YaPlayerRole.Alone && YaAloneMechanics.IsMimicActive( root ) )
				continue;

			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( h is { IsValid: true, IsAlive: true } && !h.IsDeadState )
				return root;
		}

		return null;
	}

	GameObject FindNearestAliveNotAlone( Scene scene )
	{
		GameObject best = null;
		var bestDist = float.MaxValue;
		foreach ( var root in scene.GetAllComponents<YaPlayerRoleComponent>()
			         .Where( r => r.IsValid() && r.Role == YaPlayerRole.NotAlone )
			         .Select( r => r.GameObject ) )
		{
			if ( root == GameObject )
				continue;

			var h = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
			if ( h is not { IsValid: true, IsAlive: true } || h.IsDeadState )
				continue;

			var d = (root.WorldPosition - GameObject.WorldPosition).LengthSquared;
			if ( d < bestDist )
			{
				bestDist = d;
				best = root;
			}
		}

		return best;
	}

	Vector3 PickOrKeepRoamTarget()
	{
		if ( Time.Now >= _nextRoamPickAt || ( _roamTarget - GameObject.WorldPosition ).WithZ( 0f ).Length < 70f )
		{
			_nextRoamPickAt = Time.Now + Random.Shared.Float( 1.7f, 3.9f );
			var bias = GameObject.WorldRotation.Forward.WithZ( 0f );
			if ( bias.LengthSquared < 0.0001f )
				bias = Vector3.Forward;
			for ( var i = 0; i < 10; i++ )
			{
				var wander = RotatePlanar( bias, Random.Shared.Float( -140f, 140f ) );
				if ( TryPickReachableWorldPoint( _homePos, wander, 120f, 420f, out var cand ) )
				{
					_roamTarget = cand;
					break;
				}
			}
		}

		return _roamTarget;
	}

	void PickHunterRepositionFlankOnce( Vector3 toEnemyPlanarNormal )
	{
		var n = toEnemyPlanarNormal;
		if ( n.LengthSquared < 0.0001f )
			n = GameObject.WorldRotation.Forward.WithZ( 0f );
		n = n.Normal;

		var side = Vector3.Cross( Vector3.Up, n ).Normal;
		if ( Random.Shared.Float( 0f, 1f ) < 0.5f )
			side *= -1f;

		if ( !TryPickReachableWorldPoint( GameObject.WorldPosition, side, 125f, 235f, out _hunterRepositionFlank ) )
			_hunterRepositionFlank = PickSafeNearbyPoint( side, 125f, 235f );
	}

	void MoveTowardSmart( Vector3 worldTarget, bool allowFacing, Vector3? lookAtWorld = null, Vector3? navGoal = null )
	{
		_hunterMoveGoal = navGoal ?? worldTarget;

		var to = worldTarget - GameObject.WorldPosition;
		to = to.WithZ( 0f );
		if ( to.LengthSquared < 1f )
			return;

		var desired = to.Normal;
		var dir = ComputeSteeredMoveDir( desired, _hunterMoveGoal );

		var moveDir = dir;
		if ( BotRole == YaPlayerRole.NotAlone )
		{
			if ( _smoothedHunterMoveDir.LengthSquared < 0.001f )
				_smoothedHunterMoveDir = dir;
			else
			{
				_smoothedHunterMoveDir = Vector3.Slerp( _smoothedHunterMoveDir, dir, Math.Clamp( (float)Time.Delta * 7.5f, 0f, 1f ) )
					.WithZ( 0f );
				_smoothedHunterMoveDir = _smoothedHunterMoveDir.LengthSquared > 0.0001f
					? _smoothedHunterMoveDir.Normal
					: dir;
			}

			moveDir = _smoothedHunterMoveDir;
		}

		if ( allowFacing )
		{
			if ( BotRole == YaPlayerRole.NotAlone )
			{
				Vector3 wantLookDir;
				if ( lookAtWorld.HasValue )
				{
					wantLookDir = (lookAtWorld.Value - GameObject.WorldPosition).WithZ( 0f );
					wantLookDir = wantLookDir.LengthSquared > 0.0001f ? wantLookDir.Normal : moveDir;
				}
				else
					wantLookDir = moveDir;

				_smoothedHunterLookDir = Vector3.Slerp( _smoothedHunterLookDir, wantLookDir, Math.Clamp( (float)Time.Delta * 5.2f, 0f, 1f ) )
					.WithZ( 0f );
				_smoothedHunterLookDir = _smoothedHunterLookDir.LengthSquared > 0.0001f
					? _smoothedHunterLookDir.Normal
					: wantLookDir;

				GameObject.WorldRotation = Rotation.Slerp(
					GameObject.WorldRotation,
					Rotation.LookAt( _smoothedHunterLookDir, Vector3.Up ),
					Math.Clamp( (float)Time.Delta * 5.8f, 0f, 1f ) );
			}
			else
			{
				GameObject.WorldRotation = Rotation.Slerp(
					GameObject.WorldRotation,
					Rotation.LookAt( dir, Vector3.Up ),
					Math.Clamp( (float)Time.Delta * 6f, 0f, 1f ) );
			}
		}

		_controller.Accelerate( moveDir * MoveSpeed );
		_controller.ApplyFriction( 6f );
		_controller.Accelerate( Vector3.Down * 800f );
		_controller.Move();
		ClampOutOfGeometry();
	}

	void ClampOutOfGeometry()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( !YaPawnPlacement.TryGetCapsule( GameObject, out var radius, out var height ) )
			return;

		var feet = GameObject.WorldPosition;
		if ( YaPawnPlacement.IsFeetPositionClear( scene, GameObject, feet, radius, height ) )
			return;

		if ( YaPawnPlacement.TryDepenetrate( scene, GameObject, feet, radius, height, out var resolved ) )
		{
			YaPawnPlacement.ApplyFeetPosition( GameObject, resolved );
			_lastPos = resolved;
		}
	}

	Vector3 PickSafeNearbyPoint( Vector3 biasPlanar, float minDist, float maxDist )
	{
		if ( TryPickReachableWorldPoint( GameObject.WorldPosition, biasPlanar, minDist, maxDist, out var point ) )
			return point;

		return GameObject.WorldPosition;
	}

	bool IsForwardBlocked( Vector3 dir, float dist ) =>
		ProbeWalkClearance( dir, dist ) < Math.Min( dist * 0.42f, 26f );

	float ProbeWalkClearanceAtHeight( Vector3 planarDir, float maxDist, float height )
	{
		if ( planarDir.LengthSquared < 0.0001f )
			return 0f;

		planarDir = planarDir.WithZ( 0f ).Normal;
		var lateral = Vector3.Cross( Vector3.Up, planarDir ).Normal * NavProbeLateralOffset;
		var min = maxDist;
		ProbeRayAt( GameObject.WorldPosition + Vector3.Up * height, planarDir, maxDist, ref min );
		ProbeRayAt( GameObject.WorldPosition + Vector3.Up * height + lateral, planarDir, maxDist, ref min );
		ProbeRayAt( GameObject.WorldPosition + Vector3.Up * height - lateral, planarDir, maxDist, ref min );
		return min;
	}

	void ProbeRayAt( Vector3 start, Vector3 planarDir, float maxDist, ref float minClearance )
	{
		var tr = Scene.Trace.Ray( start, start + planarDir * maxDist )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var clear = !tr.Hit ? maxDist : Math.Max( 0f, tr.Distance - (WallPadding + 2f) );
		if ( clear < minClearance )
			minClearance = clear;
	}

	float ProbeWalkClearance( Vector3 planarDir, float maxDist )
	{
		var low = ProbeWalkClearanceAtHeight( planarDir, maxDist, 22f );
		var mid = ProbeWalkClearanceAtHeight( planarDir, maxDist, 40f );
		var high = ProbeWalkClearanceAtHeight( planarDir, maxDist, 56f );
		return Math.Min( low, Math.Min( mid, high ) );
	}

	static Vector3 RotatePlanar( Vector3 planar, float yawDegrees )
	{
		planar = planar.WithZ( 0f );
		if ( planar.LengthSquared < 0.0001f )
			planar = Vector3.Forward;
		return Rotation.From( 0f, yawDegrees, 0f ) * planar.Normal;
	}

	Vector3 ComputeSteeredMoveDir( Vector3 desiredPlanar, Vector3 navGoalWorld )
	{
		desiredPlanar = desiredPlanar.WithZ( 0f );
		if ( desiredPlanar.LengthSquared < 0.0001f )
			desiredPlanar = GameObject.WorldRotation.Forward.WithZ( 0f );
		desiredPlanar = desiredPlanar.Normal;

		var isHunter = BotRole == YaPlayerRole.NotAlone;
		var fanStep = isHunter ? 20f : 28f;
		var fanCount = isHunter ? 8 : 4;
		var stuckBias = _stuckSeconds > 0.2f;

		var bestScore = float.MinValue;
		var bestDir = desiredPlanar;

		void Consider( Vector3 dir, float turnPenalty )
		{
			dir = dir.WithZ( 0f );
			if ( dir.LengthSquared < 0.0001f )
				return;
			dir = dir.Normal;

			var clearance = ProbeWalkClearance( dir, NavProbeMaxDist );
			var minClear = Math.Max( 18f, _controller.Radius + WallPadding );
			if ( clearance < minClear )
				return;

			var align = Vector3.Dot( dir, desiredPlanar );
			var goalAlign = 0f;
			var toGoal = (navGoalWorld - GameObject.WorldPosition).WithZ( 0f );
			if ( toGoal.LengthSquared > 64f )
				goalAlign = Vector3.Dot( dir, toGoal.Normal );

			var score = clearance * 1.35f + align * 52f + goalAlign * 38f - turnPenalty;
			if ( score > bestScore )
			{
				bestScore = score;
				bestDir = dir;
			}
		}

		Consider( desiredPlanar, 0f );
		for ( var i = 1; i <= fanCount; i++ )
		{
			var a = fanStep * i;
			Consider( RotatePlanar( desiredPlanar, a ), i * 2.5f );
			Consider( RotatePlanar( desiredPlanar, -a ), i * 2.5f );
		}

		if ( stuckBias || (isHunter && clearanceTooLow( desiredPlanar )) )
		{
			Consider( -desiredPlanar, 12f );
			Consider( RotatePlanar( desiredPlanar, 110f ), 14f );
			Consider( RotatePlanar( desiredPlanar, -110f ), 14f );
			Consider( RotatePlanar( desiredPlanar, 145f ), 16f );
			Consider( RotatePlanar( desiredPlanar, -145f ), 16f );
		}

		return bestDir;

		bool clearanceTooLow( Vector3 dir ) => ProbeWalkClearance( dir, 64f ) < Math.Max( 24f, _controller.Radius + WallPadding );
	}

	bool TryPickReachableWorldPoint( Vector3 origin, Vector3 biasPlanar, float minDist, float maxDist, out Vector3 worldPoint )
	{
		worldPoint = origin;
		biasPlanar = biasPlanar.WithZ( 0f );
		if ( biasPlanar.LengthSquared < 0.0001f )
			biasPlanar = Vector3.Forward;
		biasPlanar = biasPlanar.Normal;

		var bestScore = float.MinValue;
		var found = false;
		for ( var i = 0; i < 12; i++ )
		{
			var yaw = i == 0 ? 0f : Random.Shared.Float( -95f, 95f );
			var dist = Random.Shared.Float( minDist, maxDist );
			var dir = RotatePlanar( biasPlanar, yaw );
			var clearance = ProbeWalkClearance( dir, dist + 48f );
			if ( clearance < dist * 0.5f )
				continue;

			var cand = origin + dir * dist;
			if ( Scene is { IsValid: true }
			     && !YaPawnPlacement.IsFeetPositionClear( Scene, GameObject, cand, _controller.Radius, _controller.Height ) )
				continue;

			var score = clearance + Vector3.Dot( dir, biasPlanar ) * 24f;
			if ( score > bestScore )
			{
				bestScore = score;
				worldPoint = cand;
				found = true;
			}
		}

		return found;
	}

	void TickStuckRecovery( bool intendedMove, Vector3? chaseGoal = null )
	{
		var moved = (GameObject.WorldPosition - _lastPos).WithZ( 0f ).Length;
		_lastPos = GameObject.WorldPosition;

		var movedMin = BotRole == YaPlayerRole.NotAlone ? 0.85f : 0.8f;
		if ( intendedMove && moved < movedMin )
			_stuckSeconds += Time.Delta;
		else
			_stuckSeconds = Math.Max( 0f, _stuckSeconds - (float)Time.Delta * 2.5f );

		if ( _stuckSeconds < 0.45f )
			return;

		_stuckSeconds = 0f;
		_hunterStickyNavUntil = 0;

		if ( BotRole != YaPlayerRole.NotAlone )
		{
			var fwd = GameObject.WorldRotation.Forward.WithZ( 0f ).Normal;
			var side = Vector3.Cross( Vector3.Up, fwd ).Normal;
			if ( Random.Shared.Float( 0f, 1f ) < 0.5f )
				side *= -1f;
			if ( !TryPickReachableWorldPoint( GameObject.WorldPosition, side, 120f, 220f, out _recoverTarget ) )
				_recoverTarget = PickSafeNearbyPoint( side, 120f, 220f );
			return;
		}

		_hunterState = HunterState.Recover;
		_stateEndsAt = Time.Now + Random.Shared.Float( 0.85f, 1.45f );

		var escapeBias = GameObject.WorldRotation.Forward.WithZ( 0f );
		if ( chaseGoal.HasValue )
		{
			var toChase = (chaseGoal.Value - GameObject.WorldPosition).WithZ( 0f );
			if ( toChase.LengthSquared > 1f )
			{
				var lateral = Vector3.Cross( Vector3.Up, toChase.Normal ).Normal;
				if ( Random.Shared.Float( 0f, 1f ) < 0.5f )
					lateral *= -1f;
				escapeBias = lateral + toChase.Normal * 0.35f;
			}
		}

		if ( escapeBias.LengthSquared < 0.0001f )
			escapeBias = Vector3.Cross( Vector3.Up, GameObject.WorldRotation.Forward.WithZ( 0f ).Normal );

		if ( !TryPickReachableWorldPoint( GameObject.WorldPosition, escapeBias, 140f, 260f, out _recoverTarget ) )
		{
			var fwd = GameObject.WorldRotation.Forward.WithZ( 0f ).Normal;
			var side = Vector3.Cross( Vector3.Up, fwd ).Normal;
			if ( Random.Shared.Float( 0f, 1f ) < 0.5f )
				side *= -1f;
			_recoverTarget = PickSafeNearbyPoint( side, 140f, 240f );
		}
	}

	bool HasLineOfSightTo( GameObject targetRoot )
	{
		if ( targetRoot is null || !targetRoot.IsValid() )
			return false;

		// Use physics-only LOS checks to prevent hitbox-priority traces from allowing wall shots.
		var start = GameObject.WorldPosition + Vector3.Up * 52f;
		return HasUnobstructedRayTo( start, targetRoot.WorldPosition + Vector3.Up * 52f, targetRoot )
		       || HasUnobstructedRayTo( start, targetRoot.WorldPosition + Vector3.Up * 34f, targetRoot );
	}

	bool HasUnobstructedRayTo( Vector3 start, Vector3 end, GameObject targetRoot )
	{
		var tr = Scene.Trace.Ray( start, end )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		// Nothing hit along the segment means no world obstruction.
		if ( !tr.Hit )
			return true;

		// If first hit belongs to target hierarchy, LOS is still valid.
		return IsInHierarchy( tr.GameObject, targetRoot );
	}

	static bool IsInHierarchy( GameObject go, GameObject root )
	{
		for ( var cur = go; cur.IsValid(); cur = cur.Parent )
		{
			if ( cur == root )
				return true;
		}

		return false;
	}
}
