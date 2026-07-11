namespace FinalOutpost;

/// <summary>Shared ground movement for workers and defender recruits.</summary>
public static class UnitLocomotion
{
	public const float ArrivalDistance = 10f;
	public const float StuckMoveThreshold = 0.35f;
	public const float StuckRepathDelay = 0.4f;

	public struct WanderState
	{
		public Vector3 Waypoint;
		public bool HasWaypoint;
		public float RepathTimer;
		public float StuckTimer;
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

		state.RepathTimer -= dt;

		var pos = go.WorldPosition;
		var toWp = (state.Waypoint - pos).WithZ( 0f );
		var arrived = state.HasWaypoint && toWp.Length <= ArrivalDistance;
		var needWaypoint = !state.HasWaypoint || state.RepathTimer <= 0f || arrived;

		if ( needWaypoint )
			PickRoamWaypoint( ref state, roamCenter, minRadius, maxRadius );

		var moved = leashRadius is > 0f
			? MoveHumanoidLeashed( go, state.Waypoint, roamCenter, leashRadius.Value, dt, speed, ref aim, character, ref state.StuckTimer )
			: MoveHumanoid( go, state.Waypoint, dt, speed, ref aim, character, ref state.StuckTimer );

		if ( !moved && state.StuckTimer >= StuckRepathDelay )
		{
			state.StuckTimer = 0f;
			state.RepathTimer = 0f;
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
		ref float stuckTimer )
	{
		if ( !go.IsValid() ) return false;

		var pos = go.WorldPosition;
		var to = (targetXY - pos).WithZ( 0f );
		var dist = to.Length;

		if ( dist < 1f )
		{
			stuckTimer = 0f;
			character?.Tick( Vector3.Zero, aim );
			return false;
		}

		var dir = to / dist;
		aim = Rotation.LookAt( dir );

		var step = MathF.Min( dist, speed * dt );
		var resolved = BuildingCollision.ResolveStep( pos, targetXY, step );

		var movedDist = (resolved - pos.WithZ( 0f )).Length;
		if ( movedDist <= 0.01f )
		{
			stuckTimer += dt;
			character?.Tick( Vector3.Zero, aim );
			return false;
		}

		stuckTimer = 0f;

		var next = resolved;
		next.z = SmoothHeight( pos.z, next.x, next.y, dt );
		go.WorldPosition = next;
		go.WorldRotation = aim;
		character?.Tick( dir * speed, aim );
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
		ref float stuckTimer )
	{
		if ( !go.IsValid() ) return false;

		var pos = go.WorldPosition;
		var to = (targetXY - pos).WithZ( 0f );
		var dist = to.Length;

		if ( dist < 1f )
		{
			stuckTimer = 0f;
			character?.Tick( Vector3.Zero, aim );
			return false;
		}

		var dir = to / dist;
		aim = Rotation.LookAt( dir );

		var step = MathF.Min( dist, speed * dt );
		var resolved = BuildingCollision.ResolveStep( pos, targetXY, step );

		var offset = (resolved - roamCenter).WithZ( 0f );
		if ( offset.Length > roamRadius )
			resolved = BuildingCollision.ResolveMove( pos, roamCenter + offset.Normal * roamRadius );

		var movedDist = (resolved - pos.WithZ( 0f )).Length;
		if ( movedDist <= 0.01f )
		{
			stuckTimer += dt;
			character?.Tick( Vector3.Zero, aim );
			return false;
		}

		stuckTimer = 0f;

		var next = resolved;
		next.z = SmoothHeight( pos.z, next.x, next.y, dt );
		go.WorldPosition = next;
		go.WorldRotation = aim;
		character?.Tick( dir * speed, aim );
		return true;
	}

	public static void PickRoamWaypoint( ref WanderState state, Vector3 center, float minRadius, float maxRadius )
	{
		center = center.WithZ( 0f );
		minRadius = MathF.Max( 0f, minRadius );
		maxRadius = MathF.Max( minRadius + 8f, maxRadius );

		if ( BuildingCollision.TryFindClearPoint( center, minRadius, maxRadius, out state.Waypoint, 24 ) )
		{
			state.HasWaypoint = true;
			state.RepathTimer = Game.Random.Float( 3f, 6f );
			state.StuckTimer = 0f;
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
				state.HasWaypoint = true;
				state.RepathTimer = Game.Random.Float( 3f, 6f );
				state.StuckTimer = 0f;
				return;
			}
		}

		if ( BuildingCollision.TryFindClearPoint( center, 0f, minRadius, out state.Waypoint, 24 ) )
		{
			state.HasWaypoint = true;
			state.RepathTimer = Game.Random.Float( 2f, 4f );
			state.StuckTimer = 0f;
			return;
		}

		state.Waypoint = center;
		state.HasWaypoint = true;
		state.RepathTimer = Game.Random.Float( 1.5f, 3f );
		state.StuckTimer = 0f;
	}

	private static float SmoothHeight( float currentZ, float x, float y, float dt )
	{
		var targetZ = OutpostTerrain.SampleHeight( x, y );
		if ( MathF.Abs( currentZ - targetZ ) < 0.5f )
			return targetZ;

		return currentZ + (targetZ - currentZ) * MathF.Min( 1f, dt * 10f );
	}
}
