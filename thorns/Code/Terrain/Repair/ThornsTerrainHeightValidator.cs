using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>Detects broken heightfield cells before mesh build.</summary>
public static class ThornsTerrainHeightValidator
{
	public static int Validate(
		int width,
		int height,
		ReadOnlySpan<float> heights,
		ThornsTerrainRepairConfig config,
		float cellSizeInches,
		Span<ThornsTerrainCellFault> faultsOut,
		ThornsTerrainRepairStats stats )
	{
		if ( faultsOut.Length < heights.Length )
			return 0;

		faultsOut.Clear();
		var count = 0;
		var maxStepLimit = config.MaxNeighborHeightDelta;
		var maxEdge = config.MaxTriangleEdgeLength;
		var maxSlopeTan = MathF.Tan( config.MaxSlopeAngleDegrees * MathF.PI / 180f );

		for ( var y = 0; y < height; y++ )
		{
			var row = y * width;
			for ( var x = 0; x < width; x++ )
			{
				var i = row + x;
				var h = heights[i];
				var fault = ThornsTerrainCellFault.None;

				if ( float.IsNaN( h ) || float.IsInfinity( h ) )
				{
					fault |= ThornsTerrainCellFault.NonFinite;
					stats.NonFiniteCells++;
				}
				else if ( h < config.InvalidHeightMin || h > config.InvalidHeightMax )
				{
					fault |= ThornsTerrainCellFault.OutOfRange;
					stats.OutOfRangeCells++;
				}

				if ( x > 0 && x < width - 1 && y > 0 && y < height - 1 )
				{
					var left = heights[i - 1];
					var right = heights[i + 1];
					var down = heights[i - width];
					var up = heights[i + width];

					var step = MathF.Max(
						MathF.Max( MathF.Abs( h - left ), MathF.Abs( h - right ) ),
						MathF.Max( MathF.Abs( h - down ), MathF.Abs( h - up ) ) );

					if ( step > maxStepLimit )
					{
						fault |= ThornsTerrainCellFault.ExcessiveStep;
						stats.ExcessiveStepCells++;
					}

					var dx = (right - left) * 0.5f / MathF.Max( cellSizeInches, 1e-3f );
					var dy = (up - down) * 0.5f / MathF.Max( cellSizeInches, 1e-3f );
					var slope = MathF.Sqrt( dx * dx + dy * dy );
					if ( slope > maxSlopeTan )
					{
						fault |= ThornsTerrainCellFault.SteepSlope;
						stats.SteepSlopeCells++;
					}

					var spikeThreshold = MathF.Max( cellSizeInches * config.IsolatedSpikeMultiplier, config.MinIsolatedSpikeInches );
					if ( HeightmapSheerSmooth.TryIsIsolatedSpikePublic( h, left, right, down, up, spikeThreshold, 0.4f, out _ ) )
					{
						fault |= ThornsTerrainCellFault.IsolatedSpike;
						stats.IsolatedSpikeCells++;
					}
				}

				if ( x < width - 1 && y < height - 1 )
				{
					var h00 = heights[i];
					var h10 = heights[i + 1];
					var h01 = heights[i + width];
					var h11 = heights[i + width + 1];
					if ( IsStretchedQuad( h00, h10, h01, h11, cellSizeInches, maxEdge, maxStepLimit ) )
					{
						fault |= ThornsTerrainCellFault.StretchedQuad;
						stats.StretchedQuadCells++;
					}
				}

				if ( fault != ThornsTerrainCellFault.None )
				{
					faultsOut[i] = fault;
					count++;
				}
			}
		}

		stats.InvalidCellsDetected = count;
		return count;
	}

	static bool IsStretchedQuad(
		float h00,
		float h10,
		float h01,
		float h11,
		float cell,
		float maxEdge,
		float maxStep )
	{
		var c = cell;
		if ( EdgeLength3( 0, 0, h00, c, 0, h10 ) > maxEdge )
			return true;
		if ( EdgeLength3( c, 0, h10, c, c, h11 ) > maxEdge )
			return true;
		if ( EdgeLength3( c, c, h11, 0, c, h01 ) > maxEdge )
			return true;
		if ( EdgeLength3( 0, c, h01, 0, 0, h00 ) > maxEdge )
			return true;
		if ( EdgeLength3( 0, 0, h00, c, c, h11 ) > maxEdge )
			return true;
		if ( EdgeLength3( c, 0, h10, 0, c, h01 ) > maxEdge )
			return true;

		var maxDh = MathF.Max(
			MathF.Max( MathF.Abs( h00 - h10 ), MathF.Abs( h00 - h01 ) ),
			MathF.Max( MathF.Abs( h11 - h10 ), MathF.Abs( h11 - h01 ) ) );
		return maxDh > maxStep * 1.35f;
	}

	static float EdgeLength3( float x0, float y0, float z0, float x1, float y1, float z1 )
	{
		var dx = x1 - x0;
		var dy = y1 - y0;
		var dz = z1 - z0;
		return MathF.Sqrt( dx * dx + dy * dy + dz * dz );
	}

	public static float MeasureMaxNeighborStep( int width, int height, ReadOnlySpan<float> heights )
	{
		var max = 0f;
		for ( var y = 1; y < height - 1; y++ )
		{
			var row = y * width;
			for ( var x = 1; x < width - 1; x++ )
			{
				var i = row + x;
				var h = heights[i];
				max = MathF.Max( max, MathF.Abs( h - heights[i - 1] ) );
				max = MathF.Max( max, MathF.Abs( h - heights[i + 1] ) );
				max = MathF.Max( max, MathF.Abs( h - heights[i - width] ) );
				max = MathF.Max( max, MathF.Abs( h - heights[i + width] ) );
			}
		}

		return max;
	}
}
