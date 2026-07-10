namespace Terraingen.TerrainGen;

/// <summary>
/// Flattens broad lowlands and reduces terrain chatter for readability.
/// </summary>
public sealed class PlainsSmoothingPass : ITerrainSculptPass
{
	public string Name => "Plains Smoothing";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.PlainsSmoothingStrength;
		if ( strength <= 0.001f )
			return;

		var lowThreshold = TerrainAnalysis.Percentile( field.Heights, 0.42f );
		var blurred = HeightmapBlur.BoxBlur( field, radius: 8 );

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			if ( field.Heights[i] > lowThreshold || slope[i] > 0.06f )
				continue;

			var mask = 1f - Math.Clamp( slope[i] / 0.06f, 0f, 1f );
			field.Heights[i] = MathX.Lerp( field.Heights[i], blurred[i], strength * mask );
		}
	}

}
