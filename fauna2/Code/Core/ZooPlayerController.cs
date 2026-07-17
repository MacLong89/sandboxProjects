namespace Fauna2;

/// <summary>
/// Local WASD movement with collision. Syncs feet position to other clients.
/// </summary>
public sealed class ZooPlayerController : Component
{
	[Sync] public Vector3 NetPosition { get; set; }
	[Sync] public int NetFacing { get; set; }
	[Sync] public int NetAnimFrame { get; set; }

	public PlayerFacing Facing => (PlayerFacing)NetFacing;
	public Vector3 FeetPosition => GameObject.WorldPosition;

	private Vector3 _proxyTarget;
	private float _animTimer;
	private int _localAnimFrame;
	private bool _spawned;
	private TimeUntil _nextFootstep;

	private static readonly Vector3[] UnstickDirs =
	{
		Vector3.Forward, Vector3.Backward, Vector3.Left, Vector3.Right,
		(Vector3.Forward + Vector3.Left).Normal, (Vector3.Forward + Vector3.Right).Normal,
		(Vector3.Backward + Vector3.Left).Normal, (Vector3.Backward + Vector3.Right).Normal,
	};

	protected override void OnStart()
	{
		if ( IsProxy )
		{
			_proxyTarget = NetPosition;
			GameObject.WorldPosition = NetPosition;
			return;
		}

		GameObject.Tags.Add( "player" );
		TrySpawnAtEntrance();
	}

	protected override void OnUpdate()
	{
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
			return;

		if ( !IsProxy && !_spawned )
			TrySpawnAtEntrance();

		if ( IsProxy )
		{
			_proxyTarget = NetPosition;
			GameObject.WorldPosition = Vector3.Lerp( GameObject.WorldPosition, _proxyTarget, Time.Delta * 14f );
			return;
		}

		// AUDIT FIX B11: block WASD while intro / catch / loading overlays own focus.
		if ( !Fauna2.UI.UiState.CanWorldInput )
		{
			NetPosition = GameObject.WorldPosition;
			NetAnimFrame = 0;
			return;
		}

		var move = Input.AnalogMove;
		var wish = Vector3.Zero;

		if ( move.Length > 0.01f )
			wish = new Vector3( move.x, move.y, 0 ).Normal;

		var speed = GameConstants.PlayerWalkSpeed * (Input.Down( "Run" ) ? GameConstants.PlayerRunMultiplier : 1f);
		var delta = wish * speed * Time.Delta;

		if ( delta.Length > 0.01f )
		{
			NetFacing = (int)PlayerFacingExtensions.FromMove( wish );
			_animTimer += Time.Delta * 10f;
			if ( _animTimer >= 1f )
			{
				_animTimer = 0f;
				_localAnimFrame = (_localAnimFrame + 1) % 4;
				NetAnimFrame = _localAnimFrame;
			}
		}
		else
		{
			_localAnimFrame = 0;
			NetAnimFrame = 0;
			_animTimer = 0f;
		}

		var pos = GameObject.WorldPosition;
		var before = pos;
		pos = ResolveMove( pos, delta );
		// Only unstick when fully surrounded — not when simply blocked by a wall/fence/tree.
		if ( wish.Length > 0.01f && IsSurrounded( pos ) )
			pos = TryUnstick( pos );
		pos = ClampToPlayable( pos );
		GameObject.WorldPosition = pos.WithZ( PlayerSpawnPoint.WalkHeight );
		NetPosition = GameObject.WorldPosition;

		TickWalkSound( wish, pos.Distance( before ) );
	}

	private void TickWalkSound( Vector3 wish, float movedDistance )
	{
		var walking = wish.Length > 0.01f && movedDistance > 0.5f;
		if ( !walking )
		{
			_nextFootstep = 0f;
			return;
		}

		if ( !_nextFootstep )
			return;

		var running = Input.Down( "Run" );
		_nextFootstep = running ? 0.30f : 0.44f;
		ZooSoundEffects.PlayWalkGrass( running ? 0.225f : 0.19f );
	}

	private static Vector3 ClampToPlayable( Vector3 pos )
	{
		var bound = GameConstants.PlayableHalfExtent - GameConstants.TileSize;
		return new Vector3(
			pos.x.Clamp( -bound, bound ),
			pos.y.Clamp( -bound, bound ),
			pos.z );
	}

	private Vector3 ResolveMove( Vector3 pos, Vector3 delta )
	{
		if ( delta.Length <= 0.001f )
			return pos;

		var nextX = TryAxisMove( pos, new Vector3( delta.x, 0, 0 ) );
		var next = TryAxisMove( nextX, new Vector3( 0, delta.y, 0 ) );
		return next;
	}

	private Vector3 TryAxisMove( Vector3 from, Vector3 axisDelta )
	{
		if ( axisDelta.Length <= 0.001f )
			return from;

		var to = from + axisDelta;
		if ( !IsBlocked( from, to ) )
			return to;

		return from;
	}

	private bool IsBlocked( Vector3 from, Vector3 to )
	{
		var start = from + Vector3.Up * 24f;
		var end = to + Vector3.Up * 24f;
		var trace = Scene.Trace.Ray( start, end )
			.Size( GameConstants.PlayerRadius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "ground", "player" )
			.Run();

		return trace.Hit;
	}

	private Vector3 TryUnstick( Vector3 pos )
	{
		for ( var radius = GameConstants.TileSize * 0.25f; radius <= GameConstants.TileSize; radius += GameConstants.TileSize * 0.25f )
		{
			foreach ( var dir in UnstickDirs )
			{
				var candidate = pos + dir * radius;
				if ( !IsBlocked( pos, candidate ) )
					return candidate;
			}
		}

		return pos;
	}

	private void TrySpawnAtEntrance()
	{
		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
			return;

		TeleportToSpawnPoint();
	}

	public void TeleportToSpawnPoint()
	{
		var desired = ClampToPlayable( PlayerSpawnPoint.GetSpawnPosition() )
			.WithZ( PlayerSpawnPoint.WalkHeight );
		var spawn = FindSafeStandingPosition( desired );
		GameObject.WorldPosition = spawn;
		NetPosition = spawn;
		_spawned = true;
		PlayerSpawnPoint.ConsumeRestoredPosition();
		Log.Info( $"[Fauna2 Scale] Player spawned at {spawn}; radius={GameConstants.PlayerRadius / GameConstants.TileSize:0.##} tiles, height={GameConstants.PlayerHeight / GameConstants.TileSize:0.##} tiles." );
	}

	/// <summary>After load, unstick the local player if restored position landed inside colliders.</summary>
	public void EnsureMovableAfterLoad()
	{
		if ( IsProxy || GameManager.Instance is null || !GameManager.Instance.GameStarted )
			return;

		_spawned = true;

		var pos = GameObject.WorldPosition.WithZ( PlayerSpawnPoint.WalkHeight );
		if ( !IsSurrounded( pos ) )
			return;

		var safe = FindSafeStandingPosition( pos );
		GameObject.WorldPosition = safe;
		NetPosition = safe;
		Log.Warning( $"[Fauna2 Scale] Player was stuck after load — moved to {safe}." );
	}

	private Vector3 FindSafeStandingPosition( Vector3 desired )
	{
		desired = ClampToPlayable( desired ).WithZ( PlayerSpawnPoint.WalkHeight );
		if ( !IsSurrounded( desired ) )
			return desired;

		var tile = GameConstants.TileSize;

		for ( var ring = 1; ring <= 16; ring++ )
		{
			Vector3? best = null;
			var bestDist = float.MaxValue;

			for ( var gx = -ring; gx <= ring; gx++ )
			{
				for ( var gy = -ring; gy <= ring; gy++ )
				{
					if ( Math.Max( Math.Abs( gx ), Math.Abs( gy ) ) != ring )
						continue;

					var candidate = ClampToPlayable( desired + new Vector3( gx * tile, gy * tile, 0 ) )
						.WithZ( PlayerSpawnPoint.WalkHeight );

					if ( IsSurrounded( candidate ) )
						continue;

					var dist = candidate.WithZ( 0 ).Distance( desired.WithZ( 0 ) );
					if ( dist >= bestDist )
						continue;

					bestDist = dist;
					best = candidate;
				}
			}

			if ( best.HasValue )
				return best.Value;
		}

		Log.Warning( $"[Fauna2 Scale] No safe spawn found near {desired} — using requested position." );
		return desired;
	}

	private bool IsSurrounded( Vector3 pos )
	{
		pos = pos.WithZ( PlayerSpawnPoint.WalkHeight );
		var step = GameConstants.TileSize * 0.5f;

		foreach ( var dir in UnstickDirs )
		{
			if ( !IsBlocked( pos, pos + dir * step ) )
				return false;
		}

		return true;
	}
}
