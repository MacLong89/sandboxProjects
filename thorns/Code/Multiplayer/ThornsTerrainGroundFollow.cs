namespace Sandbox;

/// <summary>
/// Smooths pawn feet against generated terrain when the CharacterController is already close to the terrain surface.
/// This is intentionally terrain-only; curbs, foundations, doors, and props stay under CharacterController / step-move.
/// </summary>
public static class ThornsTerrainGroundFollow
{
	public readonly struct Result
	{
		public readonly bool Applied;
		public readonly float DeltaZ;
		public readonly bool HardSnap;
		public readonly string Reason;

		public Result( bool applied, float deltaZ, bool hardSnap, string reason )
		{
			Applied = applied;
			DeltaZ = deltaZ;
			HardSnap = hardSnap;
			Reason = reason;
		}
	}

	public static Result TryApply(
		GameObject pawnRoot,
		CharacterController controller,
		float terrainZ,
		float surfaceOffset,
		float maxFollowDelta,
		float smoothSpeed,
		float hardSnapDelta,
		float deadband )
	{
		if ( !pawnRoot.IsValid() || !controller.IsValid() )
			return new Result( false, 0f, false, "invalid" );

		var pos = pawnRoot.WorldPosition;
		var targetZ = terrainZ + MathF.Max( 0f, surfaceOffset );
		var desiredDelta = targetZ - pos.z;
		var absDelta = MathF.Abs( desiredDelta );

		if ( absDelta <= MathF.Max( 0.001f, deadband ) )
			return new Result( false, 0f, false, "deadband" );

		if ( absDelta > MathF.Max( 1f, maxFollowDelta ) )
			return new Result( false, 0f, false, "too-far" );

		var hardSnap = absDelta >= MathF.Max( 1f, hardSnapDelta );
		var dz = desiredDelta;
		if ( !hardSnap )
		{
			var maxStep = MathF.Max( 1f, smoothSpeed ) * Time.Delta;
			dz = Math.Clamp( desiredDelta, -maxStep, maxStep );
		}

		if ( MathF.Abs( dz ) <= 0.001f )
			return new Result( false, 0f, false, "tiny" );

		pawnRoot.WorldPosition = pos.WithZ( pos.z + dz );

		if ( controller.Velocity.z < 0f || hardSnap )
			controller.Velocity = controller.Velocity.WithZ( 0f );

		return new Result( true, dz, hardSnap, "terrain-follow" );
	}

	/// <summary>Same terrain-only Z follow for stock <see cref="PlayerController"/> pawns.</summary>
	public static Result TryApplyForPlayer(
		GameObject pawnRoot,
		PlayerController player,
		float terrainZ,
		float surfaceOffset,
		float maxFollowDelta,
		float smoothSpeed,
		float hardSnapDelta,
		float deadband )
	{
		if ( !pawnRoot.IsValid() || !player.IsValid() )
			return new Result( false, 0f, false, "invalid" );

		var pos = pawnRoot.WorldPosition;
		var targetZ = terrainZ + MathF.Max( 0f, surfaceOffset );
		var desiredDelta = targetZ - pos.z;
		var absDelta = MathF.Abs( desiredDelta );

		if ( absDelta <= MathF.Max( 0.001f, deadband ) )
			return new Result( false, 0f, false, "deadband" );

		if ( absDelta > MathF.Max( 1f, maxFollowDelta ) )
			return new Result( false, 0f, false, "too-far" );

		var hardSnap = absDelta >= MathF.Max( 1f, hardSnapDelta );
		var dz = desiredDelta;
		if ( !hardSnap )
		{
			var maxStep = MathF.Max( 1f, smoothSpeed ) * Time.Delta;
			dz = Math.Clamp( desiredDelta, -maxStep, maxStep );
		}

		if ( MathF.Abs( dz ) <= 0.001f )
			return new Result( false, 0f, false, "tiny" );

		pawnRoot.WorldPosition = pos.WithZ( pos.z + dz );

		if ( player.Body.IsValid() )
		{
			var bodyVel = player.Body.Velocity;
			if ( bodyVel.z < 0f || hardSnap )
				player.Body.Velocity = bodyVel.WithZ( 0f );
		}

		return new Result( true, dz, hardSnap, "terrain-follow" );
	}

	public static bool IsTerrainLikeHit( in SceneTraceResult tr )
	{
		var go = tr.GameObject;
		if ( !go.IsValid() )
			return false;

		return go.Components.GetInAncestorsOrSelf<Terrain>( true ).IsValid()
		       || go.Components.GetInAncestorsOrSelf<ThornsTerrainChunk>( true ).IsValid();
	}

	public static bool IsMovementPassthroughHit( in SceneTraceResult tr )
	{
		var go = tr.GameObject;
		if ( !go.IsValid() )
			return false;

		return go.Tags.Has( ThornsCollisionTags.ResourceNode )
		       || go.Tags.Has( ThornsCollisionTags.WildlifeHull )
		       || go.Tags.Has( "creature" )
		       || go.Tags.Has( "player" );
	}

	/// <summary>
	/// True when a downward movement probe hits a walk surface above terraingen (building floors, props, foundations).
	/// Terrain-only Z follow must skip in that case — otherwise feet get pulled toward terrain through geometry.
	/// </summary>
	public static bool IsStandingOnNonTerrainSurface(
		Scene scene,
		GameObject pawnRoot,
		float terrainZ,
		float probeUp = 16f,
		float probeDown = 112f )
	{
		if ( scene is null || !scene.IsValid() || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var feet = pawnRoot.WorldPosition;
		var tr = ThornsTraceUtility.RunRay(
			scene,
			new Ray( feet + Vector3.Up * MathF.Max( 2f, probeUp ), Vector3.Down ),
			MathF.Max( 8f, probeDown ),
			ThornsTraceProfile.MovementProbe,
			pawnRoot );

		if ( !tr.Hit )
			return false;
		if ( IsTerrainLikeHit( tr ) || IsMovementPassthroughHit( tr ) )
			return false;

		return tr.HitPosition.z > terrainZ + 2f;
	}
}
