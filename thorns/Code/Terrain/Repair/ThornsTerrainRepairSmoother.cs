namespace Sandbox;

/// <summary>Localized falloff smoothing applied only near repaired cells.</summary>
public static class ThornsTerrainRepairSmoother
{
	public static int SmoothRepairedRegions(
		int width,
		int height,
		Span<float> heights,
		ReadOnlySpan<ThornsTerrainCellFault> faults,
		ThornsTerrainRepairConfig config,
		float[] scratch )
	{
		if ( scratch is null || scratch.Length < heights.Length )
			return 0;

		var radius = Math.Clamp( config.LocalSmoothRadius, 1, 6 );
		var strength = Math.Clamp( config.LocalSmoothStrength, 0.05f, 0.95f );
		heights.CopyTo( scratch );

		var influence = 0f;
		var adjusted = 0;

		for ( var y = 1; y < height - 1; y++ )
		{
			var row = y * width;
			for ( var x = 1; x < width - 1; x++ )
			{
				var i = row + x;
				influence = SampleRepairInfluence( faults, width, height, x, y, radius );
				if ( influence <= 0.001f )
					continue;

				var avg = (
					scratch[i - 1] + scratch[i + 1] +
					scratch[i - width] + scratch[i + width] +
					scratch[i] * 0.5f ) / 4.5f;

				var t = influence * strength;
				var nh = scratch[i] + (avg - scratch[i]) * t;
				if ( MathF.Abs( nh - heights[i] ) > 0.25f )
				{
					heights[i] = nh;
					adjusted++;
				}
			}
		}

		return adjusted;
	}

	static float SampleRepairInfluence(
		ReadOnlySpan<ThornsTerrainCellFault> faults,
		int width,
		int height,
		int cx,
		int cy,
		int radius )
	{
		var peak = 0f;
		for ( var dy = -radius; dy <= radius; dy++ )
		{
			for ( var dx = -radius; dx <= radius; dx++ )
			{
				var nx = cx + dx;
				var ny = cy + dy;
				if ( nx < 0 || ny < 0 || nx >= width || ny >= height )
					continue;

				var f = faults[ny * width + nx];
				if ( f == ThornsTerrainCellFault.None )
					continue;

				var dist = MathF.Sqrt( dx * dx + dy * dy );
				var w = 1f - dist / (radius + 1f);
				if ( w > peak )
					peak = w;
			}
		}

		return Math.Clamp( peak, 0f, 1f );
	}
}
