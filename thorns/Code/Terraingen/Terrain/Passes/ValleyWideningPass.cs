namespace Terraingen.TerrainGen;

/// <summary>
/// Broadens valley floors for traversal while preserving drainage flow.
/// </summary>
public sealed class ValleyWideningPass : ITerrainSculptPass
{
	public string Name => "Valley Widening";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.ValleyWideningStrength;
		if ( strength <= 0.001f )
			return;

		var valleyThreshold = TerrainAnalysis.Percentile( field.Heights, 0.38f );
		var widened = HeightmapBlur.BoxBlur( field, radius: 5 );

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			if ( field.Heights[i] > valleyThreshold + 0.06f )
				continue;

			if ( curvature[i] > 0.001f )
				continue;

			var valleyMask = 1f - Math.Clamp( (field.Heights[i] - valleyThreshold) / 0.12f, 0f, 1f );
			var blend = strength * valleyMask;
			field.Heights[i] = MathX.Lerp( field.Heights[i], widened[i], blend * 0.72f );
		}
	}
}
