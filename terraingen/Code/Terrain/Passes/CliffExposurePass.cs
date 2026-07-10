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

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			if ( field.Heights[i] < cliffMinHeight || slope[i] < cliffSlope )
				continue;

			var cliffMask = Math.Clamp( (slope[i] - cliffSlope) / Math.Max( 0.0001f, 0.25f - cliffSlope ), 0f, 1f );
			var carve = cliffMask * strength * 0.105f;
			field.Heights[i] = Math.Clamp( field.Heights[i] - carve, 0f, 1f );

			// Slight upward shoulder above cliff for silhouette read.
			if ( field.Heights[i] > config.SeaLevelNormalized + 0.05f )
				field.Heights[i] = Math.Clamp( field.Heights[i] + cliffMask * strength * 0.058f, 0f, 1f );
		}
	}
}
