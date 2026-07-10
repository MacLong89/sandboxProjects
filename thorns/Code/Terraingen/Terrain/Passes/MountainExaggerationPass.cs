namespace Terraingen.TerrainGen;

/// <summary>
/// Selectively amplifies mountain mass for stronger silhouettes.
/// </summary>
public sealed class MountainExaggerationPass : ITerrainSculptPass
{
	public string Name => "Mountain Exaggeration";

	public void Apply( HeightmapField field, ThornsTerrainConfig config, float[] slope, float[] curvature )
	{
		var strength = config.MountainExaggerationStrength;
		if ( strength <= 0.001f )
			return;

		var mountainStart = TerrainAnalysis.Percentile( field.Heights, 0.72f );

		for ( int i = 0; i < field.Heights.Length; i++ )
		{
			var h = field.Heights[i];
			if ( h < mountainStart )
				continue;

			var t = Math.Clamp( (h - mountainStart) / Math.Max( 0.001f, 1f - mountainStart ), 0f, 1f );
			var boost = t * t * strength * 0.58f;
			field.Heights[i] = h + boost;
		}
	}
}
