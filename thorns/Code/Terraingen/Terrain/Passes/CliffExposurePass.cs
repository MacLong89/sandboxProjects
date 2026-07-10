namespace Terraingen.TerrainGen;

/// <summary>
/// Strengthens exposed cliff faces on steep terrain.
/// </summary>
public sealed class CliffExposurePass : ITerrainSculptPass
{
	public string Name => "Cliff Exposure";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.CliffExposureStrength;
		if ( strength <= 0.001f )
			return;

		var cliffSlope = TerrainAnalysis.Percentile( slope, 0.88f );
		var cliffMinHeight = TerrainAnalysis.Percentile( field.Heights, config.CliffMinHeightPercentile );

		var w = field.Width;
		var h = field.Height;

		for ( int y = 0; y < h; y++ )
		{
			for ( int x = 0; x < w; x++ )
			{
				var i = field.Index( x, y );
				if ( field.Heights[i] < cliffMinHeight || slope[i] < cliffSlope )
					continue;

				var cliffMask = Math.Clamp( (slope[i] - cliffSlope) / Math.Max( 0.0001f, 0.25f - cliffSlope ), 0f, 1f );
				var carve = cliffMask * strength * 0.085f;
				field.Heights[i] = Math.Clamp( field.Heights[i] - carve, 0f, 1f );

				// Crest on uphill neighbors only — same-cell carve + raise caused mesh-breaking vertical tears.
				var shoulder = cliffMask * strength * 0.05f;
				if ( shoulder <= 0.0001f )
					continue;

				TryRaise( field, x + 1, y, shoulder );
				TryRaise( field, x - 1, y, shoulder );
				TryRaise( field, x, y + 1, shoulder );
				TryRaise( field, x, y - 1, shoulder );
			}
		}

		static void TryRaise( HeightmapField field, int x, int y, float amount )
		{
			if ( amount <= 0f || x < 0 || y < 0 || x >= field.Width || y >= field.Height )
				return;

			field.Set( x, y, field.Get( x, y ) + amount );
		}
	}
}
