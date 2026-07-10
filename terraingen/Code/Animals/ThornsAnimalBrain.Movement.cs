namespace Terraingen.Animals;



using Terraingen.AI;



/// <summary>

/// Motor wiring — brain FSM sets intent here; <see cref="ThornsAnimalMotor"/> owns all locomotion.

/// NavMeshAgent is only driven from the motor (never from behavior states directly).

/// </summary>

public sealed partial class ThornsAnimalBrain

{

	ThornsAnimalMotor _motor;



	internal ThornsAnimalMotorMode MotorMode => _motor?.Mode ?? ThornsAnimalMotorMode.Idle;

	internal bool HasMoveIntent => _motor?.HasIntent ?? false;

	internal string MovementDebugSummary => _motor?.DebugSummary ?? "motor=none";

	internal void SyncAgentMoveSpeed()

	{

		if ( !_agent.IsValid() )

			return;



		var speed = GetMoveSpeed();

		_agent.MaxSpeed = speed;

		_agent.Acceleration = Math.Max( speed, speed * 1.5f );

	}



	void EnsureMotor() => _motor ??= new ThornsAnimalMotor( this );



	void SetMotorIntent( Vector3 destination )

	{

		EnsureMotor();

		_motor.SetIntent( destination );

	}



	void StopMotor() => _motor?.Stop();



	internal void SyncAgentToPosition( Vector3 position )

	{

		if ( !_agent.IsValid() || !_agent.Enabled )

			return;



		_agent.SetAgentPosition( position );

	}



	internal void OnMotorStuck()

	{

		switch ( AiState )

		{

			case ThornsAnimalState.Wander when ShouldTickTamedFollow():

				var owner = ResolveTamedOwner();

				if ( owner.IsValid() )

					SetMotorIntent( ResolveTrailFollowSlot( owner ) );

				else

					TryMoveToWanderPoint();

				break;

			case ThornsAnimalState.Wander:

				TryMoveToWanderPoint();

				break;

			case ThornsAnimalState.Flee when _target.IsValid():

				SetMotorIntent( ResolveFleePoint( _target ) );

				break;

			case ThornsAnimalState.Chase when _target.IsValid():

				SetMotorIntent( ResolveChaseApproachPoint( _target ) );

				break;

		}

	}



	internal Vector3 ResolveChaseApproachPoint( GameObject target )

	{

		if ( !target.IsValid() )

			return HasMoveIntent ? _motor.IntentDestination : GameObject.WorldPosition;



		var self = GameObject.WorldPosition;

		var toTarget = target.WorldPosition - self;

		toTarget.z = 0f;

		var dist = toTarget.Length;

		var approachDir = dist > 0.001f ? toTarget.Normal : ResolveFallbackApproachDirection( target );

		if ( CanBeginAttack( target ) )
			return self;

		var stopDist = ResolveMeleeReachTo( target ) * (IsTamed ? TamedChaseStopReachFraction : 0.9f);
		if ( dist <= stopDist )
			return self;

		var desired = target.WorldPosition - approachDir * stopDist;
		if ( !IsTamed )
			desired += ResolveCombatRingOffset( target, approachDir, stopDist );
		desired.z = target.WorldPosition.z;
		SnapCombatGoalToTerrain( ref desired );

		return desired;

	}

	Vector3 ResolveFallbackApproachDirection( GameObject target )
	{
		var forward = -GameObject.WorldRotation.Forward.WithZ( 0f );
		if ( forward.LengthSquared >= 0.01f )
			return forward.Normal;

		var seed = GameObject.Id.GetHashCode() ^ target.Id.GetHashCode();
		var yaw = (seed & 1023) / 1023f * 360f;
		return (Rotation.FromYaw( yaw ) * Vector3.Forward).WithZ( 0f ).Normal;
	}

	Vector3 ResolveCombatRingOffset( GameObject target, Vector3 approachDir, float stopDist )
	{
		var lateral = new Vector3( -approachDir.y, approachDir.x, 0f );
		if ( lateral.LengthSquared < 0.01f )
			lateral = Vector3.Right;

		var seed = GameObject.Id.GetHashCode() ^ (target.Id.GetHashCode() * 397);
		var side = (seed & 1) == 0 ? 1f : -1f;
		var slot = Math.Abs( seed % 3 );
		var lateralAmount = MathF.Min( 72f, MathF.Max( 28f, stopDist * 0.28f ) ) * side * (slot + 1) / 2f;
		return lateral.Normal * lateralAmount;
	}

	void SnapCombatGoalToTerrain( ref Vector3 goal )
	{
		var terrain = Terraingen.TerrainGen.ThornsTerrainCache.Resolve( Scene );
		if ( terrain.IsValid() && ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, goal, out var snapped ) )
			goal = snapped;
	}



	internal Vector3 ResolveFleePoint( GameObject threat )

	{

		RefreshFleeDirection( threat );

		return GameObject.WorldPosition + _fleeDirection * MathF.Max( 400f, _species.FleeSafeDistanceOrDefault * 0.5f );

	}



	internal Vector3 ResolveTrailFollowSlot( GameObject owner )

	{

		var forward = owner.WorldRotation.Forward.WithZ( 0f );

		if ( forward.LengthSquared < 0.01f )

			forward = Vector3.Forward;

		else

			forward = forward.Normal;

		var back = -forward;
		var right = owner.WorldRotation.Right.WithZ( 0f );
		if ( right.LengthSquared < 0.01f )
			right = new Vector3( -back.y, back.x, 0f );
		else
			right = right.Normal;

		var slot = Math.Abs( GameObject.Id.GetHashCode() ) % 8;
		var row = slot / 3;
		var col = slot % 3;
		var lateral = (col - 1) * 92f;
		if ( row > 0 )
			lateral += (slot % 2 == 0 ? 46f : -46f);

		var followDistance = 120f + row * 92f;
		return owner.WorldPosition + back * followDistance + right * lateral;

	}



	void RefreshFleeDirection( GameObject threat )

	{

		if ( !threat.IsValid() )

			return;



		var origin = GameObject.WorldPosition;

		var away = origin - threat.WorldPosition;

		away.z = 0f;

		if ( away.LengthSquared < 1f )

			away = Vector3.Random.WithZ( 0f );



		var scene = Scene;

		var bodyRadius = GetBodyRadius();

		var bodyHeight = GetBodyHeight();

		var bestDir = away.WithZ( 0f ).Normal;

		var bestScore = ScoreFleeDirection( scene, origin, bestDir, bodyRadius, bodyHeight );



		ReadOnlySpan<float> yawOffsets = stackalloc float[] { -45f, 45f, -90f, 90f, -135f, 135f, 180f };

		for ( var i = 0; i < yawOffsets.Length; i++ )

		{

			var dir = Rotation.FromYaw( yawOffsets[i] ) * bestDir;

			var score = ScoreFleeDirection( scene, origin, dir, bodyRadius, bodyHeight );

			if ( score <= bestScore )

				continue;



			bestScore = score;

			bestDir = dir;

		}



		_fleeDirection = bestDir.Normal;

	}



	float ScoreFleeDirection( Scene scene, Vector3 origin, Vector3 dir, float bodyRadius, float bodyHeight )

	{

		var probeLen = MathF.Max( 280f, _species.FleeSafeDistanceOrDefault * 0.4f );

		var end = origin + dir.Normal * probeLen;

		if ( ThornsAiSolidMovementBlocker.SegmentHitsStructure(

			     scene,

			     GameObject,

			     origin,

			     end,

			     bodyRadius,

			     bodyHeight,

			     out _ ) )

			return probeLen * 0.2f;



		return probeLen;

	}

}


