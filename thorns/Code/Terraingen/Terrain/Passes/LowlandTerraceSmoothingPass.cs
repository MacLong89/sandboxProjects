namespace Terraingen.TerrainGen;

/// <summary>
/// Removes terracing spikes in foothills after vertical exaggeration (8-bit height steps amplified).
/// </summary>
public static class LowlandTerraceSmoothingPass
{
	public static void Apply( HeightmapField field, ThornsTerrainConfig config )
	{
		var strength = config.LowlandSmoothingStrength;
		if ( strength <= 0.001f )
			return;

		var bandTop = config.SeaLevelNormalized + config.LowlandBandHeight;
		var blurred = HeightmapBlur.BoxBlur( field, radius: config.LowlandBlurRadius );

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			if ( h > bandTop )
				continue;

			var bandT = ((h - config.SeaLevelNormalized) / Math.Max( 0.001f, bandTop - config.SeaLevelNormalized )).Clamp( 0f, 1f );
			var mask = (1f - bandT) * strength;
			field.Heights[i] = MathX.Lerp( field.Heights[i], blurred[i], mask );
		}
	}
}
