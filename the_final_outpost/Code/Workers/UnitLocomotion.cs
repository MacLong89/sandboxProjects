namespace FinalOutpost;

/// <summary>Shared ground movement for workers and defender recruits.</summary>
public static class UnitLocomotion
{
	public const float ArrivalDistance = 10f;
	public const float StuckMoveThreshold = 0.35f;
	public const float StuckRepathDelay = 0.55f;
	public const float StuckEscapeDelay = 0.85f;
	public const float TurnRate = 9f;
	public const float SteerBlend = 6.5f;

	public struct WanderState
	{
		public Vector3 Waypoint;
		public bool HasWaypoint;
		public float RepathTimer;
		public float StuckTimer;
		public SteerState Steer;
	}

	/// <summary>Persistent heading so agents ease into slides instead of snapping sideways.</summary>
	public struct SteerState
	{
		public Vector3 Dir;
		public int SideBias;
	}

	public static void TickWander(
		ref WanderState state,
		GameObject go,
		Vector3 roamCenter,
		float minRadius,
		float maxRadius,
		float dt,
		float speed,
		ref Rotation aim,
		CharacterModel character,
		float? leashRadius = null )
	{
		if ( !go.IsValid() ) return;

		NudgeOutIfTrapped( go, speed, dt, hard: false );

		state.RepathTimer -= dt;

		var pos = go.WorldPosition;
		var toWp = (state.Waypoint - pos).WithZ( 0f );
		var arrived = state.HasWaypoint && toWp.Length <= ArrivalDistance;
		var needWaypoint = !state.HasWaypoint || state.RepathTimer <= 0f || arrived
			|| BuildingCollision.BlocksUnit( state.Waypoint );

		if ( needWaypoint )
			PickRoamWaypoint( ref state, roamCenter, minRadius, maxRadius );

		var moved = leashRadius is > 0f
			? MoveHumanoidLeashed( go, state.Waypoint, roamCenter, leashRadius.Value, dt, speed, ref aim, character, ref state.StuckTimer, ref state.Steer )
			: MoveHumanoid( go, state.Waypoint, dt, speed, ref aim, character, ref state.StuckTimer, ref state.Steer );

		if ( !moved && state.StuckTimer >= StuckRepathDelay )
		{
			if ( state.StuckTimer >= StuckEscapeDelay )
				NudgeOutIfTrapped( go, speed, dt, hard: true );

			state.StuckTimer = 0f;
			state.RepathTimer = 0f;
			state.Steer.SideBias = 0;
			PickRoamWaypoint( ref state, roamCenter, minRadius, maxRadius );
		}
	}

	public static bool MoveHumanoid(
		GameObject go,
		Vector3 targetXY,
		float dt,
		float speed,
		ref Rotation aim,
		CharacterModel character,
		ref float stuckTimer,
		ref SteerState steer,
		string debugName = null )
	{
		if ( !go.IsValid() ) return false;

		var agent = debugName ?? $"Unit#{(go.IsValid() ? go.Id.ToString()[..8] : "?")}";
		NudgeOutIfTrapped( go, speed, dt, hard: false, agent );

		var pos = go.WorldPosition;
		var rawTarget = targetXY;
		targetXY = BuildingCollision.EnsureClearStand( targetXY, pos );

		var to = (targetXY - pos).WithZ( 0f );
		var dist = to.Length;

		if ( dist < 1f )
		{
			stuckTimer = 0f;
			steer.SideBias = 0;
			character?.Tick( Vector3.Zero, aim );
			return false;
		}

		var desiredDir = to / dist;
		UpdateSteer( ref steer, desiredDir, dt );

		var step = MathF.Min( dist, speed * dt );
		var steerTarget = pos + steer.Dir * MathF.Max( dist, step );
		var resolved = BuildingCollision.ResolveStep( pos, steerTarget, step );

		var moveDelta = (resolved - pos.WithZ( 0f )).WithZ( 0f );
		var movedDist = moveDelta.Length;
		if ( movedDist <= step * 0.12f )
		{
			if ( steer.SideBias == 0 )
				steer.SideBias = Game.Random.Int( 0, 1 ) == 0 ? -1 : 1;

			var perp = new Vector3( -desiredDir.y, desiredDir.x, 0f ) * steer.SideBias;
			var slideDir = (desiredDir * 0.35f + perp).Normal;
			steer.Dir = slideDir;

			var slideTarget = pos + slideDir * MathF.Max( dist, step );
			resolved = BuildingCollision.ResolveStep( pos, slideTarget, step );
			moveDelta = (resolved - pos.WithZ( 0f )).WithZ( 0f );
			movedDist = moveDelta.Length;

			if ( movedDist <= step * 0.12f )
			{
				steer.SideBias = -steer.SideBias;
				stuckTimer += dt;
				PathDebug.Warn( agent, "stuck",
					$"t={stuckTimer:0.0}s pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} target={PathDebug.Fmt( targetXY )}{PathDebug.Cell( targetXY )} raw={PathDebug.Fmt( rawTarget )}" );
				if ( stuckTimer >= StuckEscapeDelay )
					NudgeOutIfTrapped( go, speed, dt, hard: true, agent );
				BlendAim( ref aim, desiredDir, dt );
				go.WorldRotation = aim;
				character?.Tick( Vector3.Zero, aim );
				return false;
			}

			PathDebug.Event( agent, "slide",
				$"bias={steer.SideBias} moved={movedDist:0.0} pos={PathDebug.Cell( pos )} → {PathDebug.Cell( resolved )}" );
		}

		stuckTimer = 0f;
		if ( movedDist > step * 0.45f )
			steer.SideBias = 0;

		var moveDir = moveDelta / movedDist;
		steer.Dir = Vector3.Lerp( steer.Dir, moveDir, MathF.Min( 1f, dt * SteerBlend ) ).Normal;
		BlendAim( ref aim, moveDir, dt );

		var next = resolved;
		next.z = SmoothHeight( pos.z, next.x, next.y, dt );
		go.WorldPosition = next;
		go.WorldRotation = aim;
		character?.Tick( moveDir * speed, aim );
		return true;
	}

	public static bool MoveHumanoidLeashed(
		GameObject go,
		Vector3 targetXY,
		Vector3 roamCenter,
		float roamRadius,
		float dt,
		float speed,
		ref Rotation aim,
		CharacterModel character,
		ref float stuckTimer,
		ref SteerState steer )
	{
		if ( !go.IsValid() ) return false;

		var moved = MoveHumanoid( go, targetXY, dt, speed, ref aim, character, ref stuckTimer, ref steer );
		if ( !moved || !go.IsValid() ) return moved;

		var pos = go.WorldPosition.WithZ( 0f );
		var offset = (pos - roamCenter.WithZ( 0f )).WithZ( 0f );
		if ( offset.Length <= roamRadius )
			return moved;

		var pull = roamCenter.WithZ( 0f ) + offset.Normal * roamRadius;
		var step = MathF.Min( speed * dt, (pull - pos).Length );
		var resolved = BuildingCollision.ResolveStep( go.WorldPosition, pull, step );
		resolved.z = SmoothHeight( go.WorldPosition.z, resolved.x, resolved.y, dt );
		go.WorldPosition = resolved;
		return true;
	}

	public static void PickRoamWaypoint( ref WanderState state, Vector3 center, float minRadius, float maxRadius )
	{
		center = center.WithZ( 0f );
		minRadius = MathF.Max( 0f, minRadius );
		maxRadius = MathF.Max( minRadius + 8f, maxRadius );

		if ( BuildingCollision.TryFindClearPoint( center, minRadius, maxRadius, out state.Waypoint, 24 ) )
		{
			CommitWaypoint( ref state );
			return;
		}

		for ( var i = 0; i < 16; i++ )
		{
			var angle = Game.Random.Float( 0f, MathF.PI * 2f );
			var r = Game.Random.Float( minRadius, maxRadius );
			var candidate = center + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
			if ( !BuildingCollision.BlocksUnit( candidate ) )
			{
				state.Waypoint = candidate;
				CommitWaypoint( ref state );
				return;
			}
		}

		if ( BuildingCollision.TryFindClearPoint( center, 0f, minRadius, out state.Waypoint, 24 ) )
		{
			CommitWaypoint( ref state, shortTimer: true );
			return;
		}

		if ( BuildingCollision.TryEscape( center, out state.Waypoint ) )
		{
			CommitWaypoint( ref state, shortTimer: true );
			return;
		}

		state.Waypoint = center;
		CommitWaypoint( ref state, shortTimer: true );
	}

	static void CommitWaypoint( ref WanderState state, bool shortTimer = false )
	{
		state.HasWaypoint = true;
		state.RepathTimer = shortTimer ? Game.Random.Float( 1.5f, 3.5f ) : Game.Random.Float( 3f, 6f );
		state.StuckTimer = 0f;
		state.Steer.SideBias = 0;
	}

	static void UpdateSteer( ref SteerState steer, Vector3 desiredDir, float dt )
	{
		desiredDir = desiredDir.WithZ( 0f );
		if ( desiredDir.Length < 0.001f ) return;
		desiredDir = desiredDir.Normal;

		if ( steer.Dir.Length < 0.001f )
		{
			steer.Dir = desiredDir;
			return;
		}

		var blend = MathF.Min( 1f, dt * SteerBlend );
		steer.Dir = Vector3.Lerp( steer.Dir.Normal, desiredDir, blend ).Normal;
	}

	static void BlendAim( ref Rotation aim, Vector3 faceDir, float dt )
	{
		faceDir = faceDir.WithZ( 0f );
		if ( faceDir.Length < 0.001f ) return;
		var target = Rotation.LookAt( faceDir.Normal );
		aim = Rotation.Slerp( aim, target, MathF.Min( 1f, dt * TurnRate ) );
	}

	static void NudgeOutIfTrapped( GameObject go, float speed, float dt, bool hard = false, string agent = null )
	{
		if ( !go.IsValid() ) return;

		var pos = go.WorldPosition;
		if ( !BuildingCollision.BlocksUnit( pos ) ) return;
		if ( !BuildingCollision.TryEscape( pos, out var clear ) )
		{
			PathDebug.Warn( agent ?? UnitTag( go ), "trapped-no-escape",
				$"pos={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )}" );
			return;
		}

		var maxStep = hard
			? MathF.Max( speed * dt * 2f, BuildingCollision.UnitRadius )
			: MathF.Max( speed * dt, BuildingCollision.UnitRadius * 0.75f );
		var delta = (clear - pos).WithZ( 0f );
		if ( delta.Length > maxStep )
			clear = pos + delta.Normal * maxStep;

		PathDebug.Warn( agent ?? UnitTag( go ), hard ? "nudge-hard" : "nudge",
			$"from={PathDebug.Fmt( pos )}{PathDebug.Cell( pos )} to={PathDebug.Fmt( clear )}{PathDebug.Cell( clear )}" );

		var next = BuildingCollision.ResolveStep( pos, clear, maxStep );
		next.z = SmoothHeight( pos.z, next.x, next.y, dt );
		go.WorldPosition = next;
	}

	static string UnitTag( GameObject go ) =>
		$"Unit#{(go.IsValid() ? go.Id.ToString()[..8] : "?")}";

	private static float SmoothHeight( float currentZ, float x, float y, float dt )
	{
		var targetZ = OutpostTerrain.SampleHeight( x, y );
		if ( MathF.Abs( currentZ - targetZ ) < 0.5f )
			return targetZ;

		return currentZ + (targetZ - currentZ) * MathF.Min( 1f, dt * 10f );
	}
}
