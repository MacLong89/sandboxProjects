namespace Terraingen.TerrainGen;

/// <summary>
/// Removes high-frequency procedural chatter while preserving macro landforms.
/// </summary>
public sealed class MicroNoiseReductionPass : ITerrainSculptPass
{
	public string Name => "Micro-Noise Reduction";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.MicroNoiseReductionStrength;
		if ( strength <= 0.001f )
			return;

		var blurred = HeightmapBlur.BoxBlur( field, radius: 3 );

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var hf = MathF.Abs( curvature[i] );
			var blend = Math.Clamp( hf * 18f, 0f, 1f ) * strength;
			field.Heights[i] = MathX.Lerp( blurred[i], field.Heights[i], 1f - blend );
		}
	}

}
