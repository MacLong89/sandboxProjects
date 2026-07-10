namespace Sandbox;

/// <summary>Distance tests for reserved road strips (blocks/lots must not overlap).</summary>
public static class ThornsWorldSettlementRoadCorridors
{
	/// <summary>
	/// Pass to placement preview when roads are already baked into lot layout — avoids
	/// <c>null</c> coalescing to <see cref="ThornsTerrainNetSpec.RoadCorridors"/> (global mesh roads).
	/// </summary>
	public static readonly IReadOnlyList<ThornsWorldRoadCorridor> SkipPlacementCorridorCheck = [];

	public static bool PointInCorridor( Vector2 p, IReadOnlyList<ThornsWorldRoadCorridor> corridors, float extraClearance = 0f )
	{
		if ( corridors is null )
			return false;

		for ( var i = 0; i < corridors.Count; i++ )
		{
			var c = corridors[i];
			if ( DistancePointToSegment( p, c.A, c.B ) < c.HalfWidth + extraClearance )
				return true;
		}

		return false;
	}

	public static bool FootprintIntersectsCorridor(
		float lx,
		float ly,
		float halfW,
		float halfD,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors,
		float extraClearance = 8f )
	{
		if ( corridors is null || corridors.Count == 0 )
			return false;

		var clearance = extraClearance + MathF.Max( halfW, halfD ) * 0.35f;
		var samples = new (float ox, float oy)[]
		{
			(0f, 0f), (halfW, 0f), (-halfW, 0f), (0f, halfD), (0f, -halfD),
			(halfW, halfD), (halfW, -halfD), (-halfW, halfD), (-halfW, -halfD)
		};

		for ( var i = 0; i < samples.Length; i++ )
		{
			if ( PointInCorridor( new Vector2( lx + samples[i].ox, ly + samples[i].oy ), corridors, clearance ) )
				return true;
		}

		return false;
	}

	public static float DistancePointToSegment( Vector2 p, Vector2 a, Vector2 b )
	{
		var ab = b - a;
		var lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f )
			return ( p - a ).Length;

		var t = Math.Clamp( Vector2.Dot( p - a, ab ) / lenSq, 0f, 1f );
		var closest = a + ab * t;
		return ( p - closest ).Length;
	}

	public static Vector2 DirectionTowardSegment( Vector2 p, Vector2 a, Vector2 b )
	{
		var ab = b - a;
		var lenSq = ab.LengthSquared;
		if ( lenSq < 0.0001f )
			return ( a - p ).Normal;

		var t = Math.Clamp( Vector2.Dot( p - a, ab ) / lenSq, 0f, 1f );
		var closest = a + ab * t;
		var d = closest - p;
		return d.LengthSquared > 0.0001f ? d.Normal : new Vector2( 1f, 0f );
	}

	public static Vector2 NearestCorridorFrontage(
		Vector2 lotCenter,
		IReadOnlyList<ThornsWorldRoadCorridor> corridors )
	{
		if ( corridors is null || corridors.Count == 0 )
			return new Vector2( 1f, 0f );

		var bestDir = new Vector2( 1f, 0f );
		var bestDist = float.MaxValue;
		for ( var i = 0; i < corridors.Count; i++ )
		{
			var c = corridors[i];
			var d = DistancePointToSegment( lotCenter, c.A, c.B );
			if ( d >= bestDist )
				continue;

			bestDist = d;
			bestDir = DirectionTowardSegment( lotCenter, c.A, c.B );
		}

		return bestDir.LengthSquared > 0.0001f ? bestDir.Normal : bestDir;
	}
}
