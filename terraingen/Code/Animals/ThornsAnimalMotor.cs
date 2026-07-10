namespace Terraingen.Animals;

using Terraingen.TerrainGen;

public enum ThornsAnimalMotorMode : byte
{
	Idle = 0,
	NavFollowing = 1,
	DirectWalking = 2,
	Sidestepping = 3,
	Recovering = 4,
}

/// <summary>Simple terrain-following locomotion for animals.</summary>
public sealed class ThornsAnimalMotor
{
	public const float WanderArrivalInches = 32f;

	const float TurnRate = 6f;

	readonly ThornsAnimalBrain _brain;

	public ThornsAnimalMotorMode Mode { get; private set; } = ThornsAnimalMotorMode.Idle;
	public Vector3 IntentDestination { get; private set; }
	public bool HasIntent { get; private set; }

	Vector3 _activeGoal;
	string _debugStatus = "new";
	string _debugGroundStatus = "none";
	float _debugLastMoveInches;
	float _lastFramePlanarSpeed;
	double _nextMoveLogAt;

	internal float LastFramePlanarSpeed => _lastFramePlanarSpeed;

	public ThornsAnimalMotor( ThornsAnimalBrain brain ) => _brain = brain;

	public string DebugSummary =>
		$"goal={_activeGoal.WithZ( 0f ).Distance( _brain.GameObject.WorldPosition.WithZ( 0f ) ):F0} " +
		$"move={_debugLastMoveInches:F1} ground={_debugGroundStatus} {_debugStatus}";

	public void SetIntent( Vector3 destination )
	{
		IntentDestination = destination;
		HasIntent = true;
		_activeGoal = destination;
		_debugLastMoveInches = 0f;
		_debugGroundStatus = "pending";
		Mode = ThornsAnimalMotorMode.DirectWalking;
		StopAgent();
		SetDebugStatus( $"terrain direct {FlatDistanceTo( destination ):F0}" );
	}

	public void Stop()
	{
		HasIntent = false;
		Mode = ThornsAnimalMotorMode.Idle;
		_debugStatus = "stopped";
		_debugGroundStatus = "none";
		_debugLastMoveInches = 0f;
		_lastFramePlanarSpeed = 0f;
		StopAgent();
	}

	public void Tick( float delta )
	{
		if ( !_brain.IsValid() || _brain.IsDead || _brain.IsMounted )
			return;

		if ( !HasIntent || _brain.AiState is ThornsAnimalState.Idle or ThornsAnimalState.Dead )
		{
			if ( Mode != ThornsAnimalMotorMode.Idle )
				Stop();
			return;
		}

		UpdateActiveGoal();
		Mode = ThornsAnimalMotorMode.DirectWalking;
		TickTerrainDirect( MathF.Max( 0f, delta ) );
	}

	public bool HasReachedGoal()
	{
		if ( !HasIntent || _brain.AiState != ThornsAnimalState.Wander )
			return false;

		return _brain.GameObject.WorldPosition.WithZ( 0f ).Distance( IntentDestination.WithZ( 0f ) ) <= WanderArrivalInches;
	}

	void UpdateActiveGoal()
	{
		if ( _brain.ShouldHoldCombatPosition() )
		{
			_activeGoal = _brain.GameObject.WorldPosition;
			return;
		}

		if ( _brain.AiState == ThornsAnimalState.Chase && _brain.Target.IsValid() )
			_activeGoal = _brain.ResolveChaseApproachPoint( _brain.Target );
		else if ( _brain.AiState == ThornsAnimalState.Flee && _brain.Target.IsValid() )
			_activeGoal = _brain.ResolveFleePoint( _brain.Target );
		else
			_activeGoal = IntentDestination;
	}

	void TickTerrainDirect( float delta )
	{
		if ( _brain.ShouldHoldCombatPosition() )
		{
			_debugLastMoveInches = 0f;
			_lastFramePlanarSpeed = 0f;
			_debugStatus = "combat_hold";
			return;
		}

		var current = _brain.GameObject.WorldPosition;
		var flatToGoal = new Vector3( _activeGoal.x - current.x, _activeGoal.y - current.y, 0f );
		var distance = flatToGoal.Length;

		if ( _brain.AiState == ThornsAnimalState.Wander
		     && distance <= WanderArrivalInches
		     && !_brain.ShouldTickTamedFollow() )
		{
			_debugLastMoveInches = 0f;
			_lastFramePlanarSpeed = 0f;
			_debugStatus = "arrived";
			return;
		}

		if ( distance <= 0.001f || delta <= 0f )
		{
			_debugLastMoveInches = 0f;
			_lastFramePlanarSpeed = 0f;
			_debugStatus = "waiting";
			return;
		}

		var moveDir = flatToGoal / distance;
		var moveDistance = MathF.Min( _brain.GetMoveSpeed() * delta, distance );
		var next = current + moveDir * moveDistance;

		SnapToTerrain( ref next );

		_brain.GameObject.WorldPosition = next;
		_debugLastMoveInches = current.WithZ( 0f ).Distance( next.WithZ( 0f ) );
		_lastFramePlanarSpeed = delta > 0f ? _debugLastMoveInches / delta : 0f;
		SetDebugStatus( "terrain direct" );

		if ( moveDir.LengthSquared <= 0.01f )
			return;

		var face = Rotation.LookAt( moveDir );
		_brain.GameObject.WorldRotation = Rotation.Slerp( _brain.GameObject.WorldRotation, face, delta * TurnRate );
	}

	void SnapToTerrain( ref Vector3 position )
	{
		var terrain = ThornsTerrainCache.Resolve( _brain.Scene );
		if ( !terrain.IsValid() )
		{
			_debugGroundStatus = "no terrain";
			return;
		}

		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, position, out var snapped ) )
		{
			position = snapped;
			_debugGroundStatus = "terrain";
			return;
		}

		_debugGroundStatus = "snap failed";
	}

	float FlatDistanceTo( Vector3 point )
		=> _brain.GameObject.WorldPosition.WithZ( 0f ).Distance( point.WithZ( 0f ) );

	void SetDebugStatus( string status )
	{
		_debugStatus = status;
		if ( !ThornsAnimalDebug.MoveLog || Time.Now < _nextMoveLogAt )
			return;

		_nextMoveLogAt = Time.Now + 0.75f;
		Log.Info(
			$"[Thorns Animals][Move] {_brain.GameObject.Name}: mode={Mode} state={_brain.AiState} " +
			$"status={_debugStatus} ground={_debugGroundStatus} " +
			$"goal={FlatDistanceTo( _activeGoal ):F0} moved={_debugLastMoveInches:F1} " +
			$"target={_brain.GetTargetMoveSpeed():F0} ramp={_brain.GetMoveSpeed():F0} meas={_brain.MeasuredPlanarSpeed:F0}" );
	}

	void StopAgent()
	{
		var agent = _brain.Components.Get<NavMeshAgent>( FindMode.EverythingInSelf );
		if ( !agent.IsValid() )
			return;

		agent.Stop();
		agent.UpdatePosition = false;
		agent.Enabled = false;
	}
}
