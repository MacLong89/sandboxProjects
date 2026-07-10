namespace Sandbox;

/// <summary>Replaces invalid height samples using nearby valid terrain.</summary>
public static class ThornsTerrainHeightReconstructor
{
	public static int ReconstructInvalidCells(
		int width,
		int height,
		Span<float> heights,
		ReadOnlySpan<ThornsTerrainCellFault> faults,
		ThornsTerrainRepairConfig config )
	{
		if ( faults.Length < heights.Length )
			return 0;

		var radius = Math.Clamp( config.ReconstructSearchRadius, 1, 16 );
		var fixedCount = 0;

		for ( var y = 0; y < height; y++ )
		{
			var row = y * width;
			for ( var x = 0; x < width; x++ )
			{
				var i = row + x;
				var fault = faults[i];
				if ( fault == ThornsTerrainCellFault.None )
					continue;

				var needsReplace = (fault & (ThornsTerrainCellFault.NonFinite | ThornsTerrainCellFault.OutOfRange)) != 0
					|| (fault & ThornsTerrainCellFault.IsolatedSpike) != 0;

				if ( !needsReplace && (fault & (ThornsTerrainCellFault.ExcessiveStep | ThornsTerrainCellFault.StretchedQuad)) != 0 )
					needsReplace = true;

				if ( !needsReplace )
					continue;

				if ( TrySampleValidNeighbors( width, height, heights, faults, x, y, radius, out var rebuilt ) )
				{
					heights[i] = rebuilt;
					fixedCount++;
				}
				else if ( TryGlobalFallback( width, height, heights, faults, out var fallback ) )
				{
					heights[i] = fallback;
					fixedCount++;
				}
			}
		}

		return fixedCount;
	}

	static bool TrySampleValidNeighbors(
		int width,
		int height,
		ReadOnlySpan<float> heights,
		ReadOnlySpan<ThornsTerrainCellFault> faults,
		int cx,
		int cy,
		int maxRadius,
		out float result )
	{
		result = 0f;
		var weightSum = 0f;

		for ( var r = 1; r <= maxRadius; r++ )
		{
			for ( var dy = -r; dy <= r; dy++ )
			{
				for ( var dx = -r; dx <= r; dx++ )
				{
					if ( Math.Max( Math.Abs( dx ), Math.Abs( dy ) ) != r )
						continue;

					var nx = cx + dx;
					var ny = cy + dy;
					if ( nx < 0 || ny < 0 || nx >= width || ny >= height )
						continue;

					var ni = ny * width + nx;
					if ( !IsValidSample( heights[ni], faults[ni] ) )
						continue;

					var w = 1f / MathF.Max( 1f, MathF.Sqrt( dx * dx + dy * dy ) );
					result += heights[ni] * w;
					weightSum += w;
				}
			}

			if ( weightSum > 0f )
			{
				result /= weightSum;
				return true;
			}
		}

		return false;
	}

	static bool TryGlobalFallback(
		int width,
		int height,
		ReadOnlySpan<float> heights,
		ReadOnlySpan<ThornsTerrainCellFault> faults,
		out float result )
	{
		var sum = 0f;
		var count = 0;
		for ( var i = 0; i < heights.Length; i++ )
		{
			if ( !IsValidSample( heights[i], faults[i] ) )
				continue;

			sum += heights[i];
			count++;
		}

		if ( count == 0 )
		{
			result = 0f;
			return false;
		}

		result = sum / count;
		return true;
	}

	static bool IsValidSample( float h, ThornsTerrainCellFault fault ) =>
		!float.IsNaN( h )
		&& !float.IsInfinity( h )
		&& fault != ThornsTerrainCellFault.NonFinite
		&& fault != ThornsTerrainCellFault.OutOfRange;
}
