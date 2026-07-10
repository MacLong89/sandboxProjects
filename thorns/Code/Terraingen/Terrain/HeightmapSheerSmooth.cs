namespace Terraingen.TerrainGen;

/// <summary>
/// Removes isolated heightmap outliers (8-bit steps × vertical exaggeration). Does not flatten real slopes —
/// that caused pyramid facets and stretched cliff textures.
/// </summary>
public static class HeightmapSheerSmooth
{
	public static int RemoveIsolatedSpikesNormalized( HeightmapField field, int maxPasses = 3 )
	{
		if ( field is null )
			return 0;

		return RemoveIsolatedSpikes(
			field.Width,
			field.Height,
			field.Heights,
			spikeThreshold: 0.011f,
			neighborAgreementRatio: 0.42f,
			maxPasses );
	}

	public static int RemoveIsolatedSpikesWorld(
		int width,
		int height,
		Span<float> heights,
		float cellSizeInches,
		int maxPasses = 3 )
	{
		if ( heights.Length < width * height )
			return 0;

		var spikeThreshold = MathF.Max( cellSizeInches * 2.2f, 96f );
		return RemoveIsolatedSpikes( width, height, heights, spikeThreshold, 0.4f, maxPasses );
	}

	static int RemoveIsolatedSpikes(
		int width,
		int height,
		Span<float> heights,
		float spikeThreshold,
		float neighborAgreementRatio,
		int maxPasses )
	{
		var fixedCount = 0;
		for ( var pass = 0; pass < maxPasses; pass++ )
		{
			var passFixed = 0;
			for ( var y = 1; y < height - 1; y++ )
			{
				var row = y * width;
				for ( var x = 1; x < width - 1; x++ )
				{
					var i = row + x;
					var h = heights[i];
					var left = heights[i - 1];
					var right = heights[i + 1];
					var down = heights[i - width];
					var up = heights[i + width];
					if ( !TryIsIsolatedSpike( h, left, right, down, up, spikeThreshold, neighborAgreementRatio, out var target ) )
						continue;

					heights[i] = target;
					passFixed++;
				}
			}

			fixedCount += passFixed;
			if ( passFixed == 0 )
				break;
		}

		return fixedCount;
	}

	/// <summary>True when center is an outlier but the four neighbors agree with each other (not a real cliff ramp).</summary>
	public static bool TryIsIsolatedSpikePublic(
		float center,
		float left,
		float right,
		float down,
		float up,
		float spikeThreshold,
		float neighborAgreementRatio,
		out float replacement ) =>
		TryIsIsolatedSpike( center, left, right, down, up, spikeThreshold, neighborAgreementRatio, out replacement );

	/// <summary>True when center is an outlier but the four neighbors agree with each other (not a real cliff ramp).</summary>
	static bool TryIsIsolatedSpike(
		float center,
		float left,
		float right,
		float down,
		float up,
		float spikeThreshold,
		float neighborAgreementRatio,
		out float replacement )
	{
		replacement = center;
		var median = Median4( left, right, down, up );
		var centerDelta = MathF.Abs( center - median );
		if ( centerDelta < spikeThreshold )
			return false;

		var neighborSpread = MathF.Max(
			MathF.Max( MathF.Abs( left - right ), MathF.Abs( left - down ) ),
			MathF.Max( MathF.Max( MathF.Abs( left - up ), MathF.Abs( right - down ) ),
				MathF.Max( MathF.Abs( right - up ), MathF.Abs( down - up ) ) ) );

		if ( neighborSpread >= centerDelta * neighborAgreementRatio )
			return false;

		replacement = median;
		return true;
	}

	static float MeasureMaxNeighborStep( int width, int height, ReadOnlySpan<float> heights )
	{
		var max = 0f;
		for ( var y = 1; y < height - 1; y++ )
		{
			var row = y * width;
			for ( var x = 1; x < width - 1; x++ )
			{
				var i = row + x;
				var h = heights[i];
				max = MathF.Max( max, MaxDiff( h, heights[i - 1], heights[i + 1], heights[i - width], heights[i + width] ) );
			}
		}

		return max;
	}

	static float MaxDiff( float h, float a, float b, float c, float d ) =>
		MathF.Max( MathF.Max( MathF.Abs( h - a ), MathF.Abs( h - b ) ),
			MathF.Max( MathF.Abs( h - c ), MathF.Abs( h - d ) ) );

	static float Median4( float a, float b, float c, float d )
	{
		Span<float> s = stackalloc float[4] { a, b, c, d };
		s.Sort();
		return (s[1] + s[2]) * 0.5f;
	}

	/// <summary>
	/// Iteratively caps per-cell grade so single heightmap texels cannot drop thousands of inches (mesh tears).
	/// Preserves macro cliffs as steep stair-steps instead of one vertical smear.
	/// </summary>
	public static int ClampExcessiveNeighborStepsWorld(
		int width,
		int height,
		Span<float> heights,
		float cellSizeInches,
		float maxGradeMultiplier = 6.25f,
		float minMaxStepInches = 300f,
		int maxPasses = 10 )
	{
		if ( heights.Length < width * height )
			return 0;

		var maxStep = MathF.Max( cellSizeInches * maxGradeMultiplier, minMaxStepInches );
		return ClampExcessiveNeighborSteps( width, height, heights, maxStep, maxPasses );
	}

	public static int ClampExcessiveNeighborSteps(
		int width,
		int height,
		Span<float> heights,
		float maxStepInches,
		int maxPasses = 10 )
	{
		if ( heights.Length < width * height )
			return 0;

		var maxStep = MathF.Max( maxStepInches, 1f );
		var total = 0;
		var scratch = new float[heights.Length];

		for ( var pass = 0; pass < maxPasses; pass++ )
		{
			heights.CopyTo( scratch );
			var passAdj = 0;
			for ( var y = 1; y < height - 1; y++ )
			{
				var row = y * width;
				for ( var x = 1; x < width - 1; x++ )
				{
					var i = row + x;
					var h = scratch[i];
					var nh = h;
					nh = ClampToNeighborStep( nh, scratch[i - 1], maxStep );
					nh = ClampToNeighborStep( nh, scratch[i + 1], maxStep );
					nh = ClampToNeighborStep( nh, scratch[i - width], maxStep );
					nh = ClampToNeighborStep( nh, scratch[i + width], maxStep );
					if ( MathF.Abs( nh - h ) > 0.5f )
					{
						heights[i] = nh;
						passAdj++;
					}
				}
			}

			total += passAdj;
			if ( passAdj == 0 )
				break;
		}

		return total;
	}

	static float ClampToNeighborStep( float h, float neighbor, float maxStep )
	{
		var d = h - neighbor;
		if ( d > maxStep )
			return neighbor + maxStep;
		if ( d < -maxStep )
			return neighbor - maxStep;
		return h;
	}
}
