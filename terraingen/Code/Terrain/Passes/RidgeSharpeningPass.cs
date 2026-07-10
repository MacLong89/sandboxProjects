namespace Terraingen.TerrainGen;

/// <summary>
/// Enhances ridgelines and skyline readability.
/// </summary>
public sealed class RidgeSharpeningPass : ITerrainSculptPass
{
	public string Name => "Ridge Sharpening";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.RidgeSharpeningStrength;
		if ( strength <= 0.001f )
			return;

		var ridgeStart = Math.Max(
			TerrainAnalysis.Percentile( field.Heights, 0.62f ),
			config.SeaLevelNormalized + config.LowlandBandHeight + 0.04f );

		for ( int y = 1; y < field.Height - 1; y++ )
		{
			for ( int x = 1; x < field.Width - 1; x++ )
			{
				var i = field.Index( x, y );
				var h = field.Heights[i];

				if ( h < ridgeStart || slope[i] < 0.04f )
					continue;

				if ( curvature[i] <= 0f )
					continue;

				var sharpen = curvature[i] * strength * 0.88f;
				field.Heights[i] = h + sharpen;
			}
		}
	}
}
