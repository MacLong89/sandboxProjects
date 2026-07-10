namespace Terraingen.TerrainGen;

public readonly struct TerrainRegionMetrics
{
	public float ElevationRange { get; init; }
	public float MeanElevation { get; init; }
	public float MeanSlope { get; init; }
	public float HighFrequencyEnergy { get; init; }
	public float TraversableFraction { get; init; }
	public float BasinScore { get; init; }
	public float Score { get; init; }
}

public static class TerrainAnalysis
{
	public static TerrainRegionMetrics EvaluateRegion( HeightmapField field, int originX, int originY, int cropW, int cropH )
	{
		var heights = new List<float>();
		var slopes = new List<float>();
		var laplacian = new List<float>();

		for ( int y = 1; y < cropH - 1; y++ )
		{
			for ( int x = 1; x < cropW - 1; x++ )
			{
				var h = field.Get( originX + x, originY + y );
				heights.Add( h );

				var dx = field.Get( originX + x + 1, originY + y ) - field.Get( originX + x - 1, originY + y );
				var dy = field.Get( originX + x, originY + y + 1 ) - field.Get( originX + x, originY + y - 1 );
				var slope = MathF.Sqrt( dx * dx + dy * dy );
				slopes.Add( slope );

				var lap = field.Get( originX + x + 1, originY + y )
					+ field.Get( originX + x - 1, originY + y )
					+ field.Get( originX + x, originY + y + 1 )
					+ field.Get( originX + x, originY + y - 1 )
					- 4f * h;
				laplacian.Add( MathF.Abs( lap ) );
			}
		}

		if ( heights.Count == 0 )
			return default;

		var min = heights.Min();
		var max = heights.Max();
		var mean = heights.Average();
		var range = max - min;
		var meanSlope = slopes.Average();
		var hfEnergy = laplacian.Average();

		var traversableCount = 0;
		var basinCount = 0;
		for ( int i = 0; i < heights.Count; i++ )
		{
			if ( heights[i] < 0.72f && slopes[i] < 0.08f )
				traversableCount++;
			if ( heights[i] < mean - range * 0.08f )
				basinCount++;
		}

		var traversable = traversableCount / (float)heights.Count;
		var basin = basinCount / (float)heights.Count;

		var flatPenalty = range < 0.18f ? 2.5f : 0f;
		var chaosPenalty = hfEnergy > 0.035f ? (hfEnergy - 0.035f) * 40f : 0f;
		var mountainOnlyPenalty = mean > 0.78f && range < 0.25f ? 2f : 0f;

		var score = range * 3.2f
			+ basin * 1.4f
			+ traversable * 1.1f
			+ meanSlope * 0.35f
			- flatPenalty
			- chaosPenalty
			- mountainOnlyPenalty;

		return new TerrainRegionMetrics
		{
			ElevationRange = range,
			MeanElevation = mean,
			MeanSlope = meanSlope,
			HighFrequencyEnergy = hfEnergy,
			TraversableFraction = traversable,
			BasinScore = basin,
			Score = score
		};
	}

	public static void ComputeSlopeAndCurvature( HeightmapField field, out float[] slope, out float[] curvature )
	{
		slope = new float[field.Heights.Length];
		curvature = new float[field.Heights.Length];

		for ( int y = 0; y < field.Height; y++ )
		{
			for ( int x = 0; x < field.Width; x++ )
			{
				var dx = field.Get( x + 1, y ) - field.Get( x - 1, y );
				var dy = field.Get( x, y + 1 ) - field.Get( x, y - 1 );
				slope[field.Index( x, y )] = MathF.Sqrt( dx * dx + dy * dy );

				var lap = field.Get( x + 1, y ) + field.Get( x - 1, y ) + field.Get( x, y + 1 ) + field.Get( x, y - 1 ) - 4f * field.Get( x, y );
				curvature[field.Index( x, y )] = lap;
			}
		}
	}

	public static float Percentile( float[] values, float percentile )
	{
		var sorted = values.Order().ToArray();
		var idx = (int)Math.Clamp( percentile * (sorted.Length - 1), 0, sorted.Length - 1 );
		return sorted[idx];
	}
}
