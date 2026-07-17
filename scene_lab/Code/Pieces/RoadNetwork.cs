namespace SceneLab;

/// <summary>
/// Builds street segments that stop at junction clearances, plus intersection pads that own the crossing.
/// </summary>
public static class RoadNetwork
{
	public static float RowHalf( RoadCorridorPiece.Spec spec, bool includeBank )
	{
		var h = spec.RoadWidth * 0.5f + spec.SidewalkWidth;
		if ( includeBank && spec.BuildEmbankments )
			h += spec.EmbankmentWidth;
		return h;
	}

	/// <summary>
	/// Build corridor segments along local +X from xMin..xMax, cutting gaps around each block center.
	/// Corridors stop at <paramref name="junctionHalf"/> so sidewalks never cross the junction;
	/// <see cref="IntersectionPiece"/> asphalt (+ DeckPad) covers the seam.
	/// </summary>
	public static void BuildAxisSegments(
		GameObject parent,
		float yaw,
		RoadCorridorPiece.Spec template,
		float axisMin,
		float axisMax,
		IReadOnlyList<float> junctionCenters,
		float junctionHalf,
		float alongY = 0f,
		float alongZ = 0f )
	{
		var cutHalf = MathF.Max( 48f, junctionHalf );
		var gaps = new List<(float a, float b)>();
		foreach ( var c in junctionCenters )
			gaps.Add( (c - cutHalf, c + cutHalf) );

		gaps.Sort( ( u, v ) => u.a.CompareTo( v.a ) );

		var cursor = axisMin;
		foreach ( var (a, b) in gaps )
		{
			var gapA = MathF.Max( a, axisMin );
			var gapB = MathF.Min( b, axisMax );
			if ( gapA >= gapB )
				continue;

			TrySegment( parent, yaw, template, cursor, gapA, alongY, alongZ );
			cursor = MathF.Max( cursor, gapB );
		}

		TrySegment( parent, yaw, template, cursor, axisMax, alongY, alongZ );
	}

	private static void TrySegment(
		GameObject parent,
		float yaw,
		RoadCorridorPiece.Spec template,
		float start,
		float end,
		float alongY,
		float alongZ )
	{
		var len = end - start;
		if ( len < 64f )
			return;

		var mid = (start + end) * 0.5f;
		var spec = Clone( template );
		spec.Length = len;

		// Position along the corridor's local +X (parent may already be rotated)
		RoadCorridorPiece.Build( parent, new Vector3( mid, alongY, alongZ ), yaw, spec );
	}

	public static RoadCorridorPiece.Spec Clone( RoadCorridorPiece.Spec s ) => new()
	{
		Length = s.Length,
		RoadWidth = s.RoadWidth,
		SidewalkWidth = s.SidewalkWidth,
		EmbankmentWidth = s.EmbankmentWidth,
		RoadThickness = s.RoadThickness,
		SidewalkThickness = s.SidewalkThickness,
		CurbHeight = s.CurbHeight,
		EmbankmentThickness = s.EmbankmentThickness,
		CenterLine = s.CenterLine,
		BaseLift = s.BaseLift,
		BuildEmbankments = s.BuildEmbankments,
	};
}
