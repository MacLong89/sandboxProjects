namespace Sandbox;

/// <summary>Manages AIM trainer spheres in the private booth.</summary>
[Title( "Aimbox Aim Drill Controller" )]
[Category( "Aimbox" )]
public sealed class AimboxAimDrillController : Component
{
	public static AimboxAimDrillController Instance { get; private set; }

	public AimboxAimDrill Level { get; private set; }

	readonly List<AimboxDummyTarget> _targets = [];
	readonly Dictionary<AimboxDummyTarget, int> _trackingHits = [];
	readonly HashSet<Vector3> _recentPositions = [];

	AimboxDummyTarget _movingTarget;
	Vector3 _moveDirection;
	TimeUntil _nextDirectionChange;

	const int TripleTargetCount = 3;
	const int TrackingHitsRequired = 5;
	const float TrackingMoveSpeed = 42f;
	const float MicroRadiusScale = 0.58f;
	const float MinSpawnSeparation = 18f;
	const int FlickTargetHealth = 1;
	const int TrackingTargetHealth = 99999;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void Initialize( AimboxAimDrill level )
	{
		Instance = this;
		SetLevel( level );
	}

	public void SetLevel( AimboxAimDrill level )
	{
		Level = level;
		ClearTargets();
		SpawnLevelTargets();
		Log.Info( $"[Aimbox AIM] {AimboxAimDrillLabels.Long( level )} ready." );
	}

	protected override void OnUpdate()
	{
		if ( !IsBounceDrill( Level ) || _movingTarget is null || !_movingTarget.IsAlive )
			return;

		TickBounceMovement();
	}

	public void OnTargetDamaged( AimboxDummyTarget target, AimboxPlayerController attacker, bool headshot, bool killed )
	{
		if ( attacker is null || target is null || !_targets.Contains( target ) )
			return;

		if ( !AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default ) )
			return;

		if ( IsBounceDrill( Level ) )
		{
			var hits = _trackingHits.GetValueOrDefault( target ) + 1;
			_trackingHits[target] = hits;
			RegisterScore( attacker, 1 );
			attacker.ConfirmAimDrillHit( 1 );

			if ( hits < TrackingHitsRequired )
				return;

			_trackingHits[target] = 0;
			RespawnTarget( target );
			return;
		}

		if ( !killed )
			return;

		RespawnTarget( target );
		RegisterScore( attacker, 1 );
		attacker.ConfirmAimDrillKill( 1 );
	}

	void SpawnLevelTargets()
	{
		var radiusScale = RadiusScaleFor( Level );

		if ( IsTripleDrill( Level ) )
		{
			for ( var i = 0; i < TripleTargetCount; i++ )
				_targets.Add( SpawnSphereTarget( radiusScale: radiusScale ) );
			return;
		}

		if ( IsFlickDrill( Level ) )
		{
			_targets.Add( SpawnSphereTarget( radiusScale: radiusScale ) );
			return;
		}

		_movingTarget = SpawnSphereTarget( radiusScale: radiusScale );
		_targets.Add( _movingTarget );
		_trackingHits[_movingTarget] = 0;
		_nextDirectionChange = 1f;
		PickNewMoveDirection();
	}

	AimboxDummyTarget SpawnSphereTarget( Vector3? position = null, float radiusScale = 1f )
	{
		var spawnPosition = position ?? PickRandomWallPosition();
		var rotation = AimboxAimRoomLayout.FacePlayerFrom( spawnPosition );
		var go = new GameObject( true, "AIM Sphere" );
		go.SetParent( GameObject );
		go.WorldPosition = spawnPosition;
		go.WorldRotation = rotation;

		var target = go.Components.Create<AimboxDummyTarget>();
		target.ConfigureAimSphere( radiusScale );
		target.MaxHealth = IsBounceDrill( Level ) ? TrackingTargetHealth : FlickTargetHealth;
		target.RespawnSeconds = float.MaxValue;
		Log.Info( $"[Aimbox AIM] Circle at {spawnPosition}" );
		return target;
	}

	void RespawnTarget( AimboxDummyTarget target )
	{
		var position = PickRandomWallPosition();
		var rotation = AimboxAimRoomLayout.FacePlayerFrom( position );
		target.InstantRespawnAt( position, rotation );

		if ( IsBounceDrill( Level ) && target == _movingTarget )
		{
			_trackingHits[target] = 0;
			_nextDirectionChange = 1f;
			PickNewMoveDirection();
		}
	}

	void TickBounceMovement()
	{
		if ( _nextDirectionChange )
		{
			_nextDirectionChange = 1f;
			PickNewMoveDirection();
		}

		var position = _movingTarget.WorldPosition + _moveDirection * TrackingMoveSpeed * Time.Delta;
		BounceOnWallBounds( ref position );
		_movingTarget.WorldPosition = position;
		_movingTarget.WorldRotation = AimboxAimRoomLayout.FacePlayerFrom( position );
	}

	Vector3 PickRandomWallPosition()
	{
		for ( var attempt = 0; attempt < 12; attempt++ )
		{
			var candidate = AimboxAimRoomLayout.RandomBackWallPosition();
			if ( IsFarEnoughFromExisting( candidate ) )
			{
				RememberPosition( candidate );
				return candidate;
			}
		}

		var fallback = AimboxAimRoomLayout.RandomBackWallPosition();
		RememberPosition( fallback );
		return fallback;
	}

	bool IsFarEnoughFromExisting( Vector3 candidate )
	{
		foreach ( var target in _targets )
		{
			if ( target is null || !target.IsAlive )
				continue;

			if ( target.WorldPosition.Distance( candidate ) < MinSpawnSeparation )
				return false;
		}

		foreach ( var recent in _recentPositions )
		{
			if ( recent.Distance( candidate ) < MinSpawnSeparation * 0.65f )
				return false;
		}

		return true;
	}

	void RememberPosition( Vector3 position )
	{
		_recentPositions.Add( position );
		while ( _recentPositions.Count > 8 )
			_recentPositions.Remove( _recentPositions.First() );
	}

	void PickNewMoveDirection()
	{
		var angle = Game.Random.Float( 0f, MathF.PI * 2f );
		_moveDirection = new Vector3( MathF.Cos( angle ), 0f, MathF.Sin( angle ) ).Normal;
	}

	void BounceOnWallBounds( ref Vector3 position )
	{
		var layout = AimboxAimRoomLayout.Layout;
		var halfWidth = layout.ArenaHalfWidth * 0.72f;
		var minZ = AimboxAimRoomLayout.FeetZ + 48f;
		var maxZ = AimboxAimRoomLayout.FeetZ + layout.WallHeight * 0.68f;

		if ( position.x < -halfWidth )
		{
			position.x = -halfWidth;
			_moveDirection = new Vector3( MathF.Abs( _moveDirection.x ), 0f, _moveDirection.z ).Normal;
		}
		else if ( position.x > halfWidth )
		{
			position.x = halfWidth;
			_moveDirection = new Vector3( -MathF.Abs( _moveDirection.x ), 0f, _moveDirection.z ).Normal;
		}

		if ( position.z < minZ )
		{
			position.z = minZ;
			_moveDirection = new Vector3( _moveDirection.x, 0f, MathF.Abs( _moveDirection.z ) ).Normal;
		}
		else if ( position.z > maxZ )
		{
			position.z = maxZ;
			_moveDirection = new Vector3( _moveDirection.x, 0f, -MathF.Abs( _moveDirection.z ) ).Normal;
		}

		position = position.WithY( AimboxAimRoomLayout.TargetPlaneY );
	}

	static bool IsTripleDrill( AimboxAimDrill level ) =>
		level is AimboxAimDrill.Triple or AimboxAimDrill.MicroTriple;

	static bool IsFlickDrill( AimboxAimDrill level ) =>
		level is AimboxAimDrill.Flick or AimboxAimDrill.MicroFlick;

	static bool IsBounceDrill( AimboxAimDrill level ) =>
		level is AimboxAimDrill.Bounce or AimboxAimDrill.MicroBounce;

	static float RadiusScaleFor( AimboxAimDrill level ) =>
		level is AimboxAimDrill.MicroTriple or AimboxAimDrill.MicroFlick or AimboxAimDrill.MicroBounce
			? MicroRadiusScale
			: 1f;

	void ClearTargets()
	{
		foreach ( var target in _targets )
		{
			if ( target?.GameObject is { IsValid: true } go )
				go.Destroy();
		}

		_targets.Clear();
		_trackingHits.Clear();
		_recentPositions.Clear();
		_movingTarget = null;
		_moveDirection = Vector3.Zero;
	}

	void RegisterScore( AimboxPlayerController attacker, int points ) =>
		AimboxGame.Instance?.Match.RegisterAimScore( attacker.AccountId, points );
}
