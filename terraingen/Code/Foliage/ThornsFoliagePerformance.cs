namespace Terraingen.Foliage;

using Terraingen.Rendering;

/// <summary>Terrain-scaled foliage LOD/cull bands — keeps distant trees invisible and out of physics.</summary>
public static class ThornsFoliagePerformance
{
	/// <summary>Full mesh visibility radius as a fraction of terrain span (≈1/6 map).</summary>
	public const float HideDistanceTerrainFraction = 1f / 6f;

	/// <summary>Chunk root cull slightly beyond hide so hysteresis can re-enable cleanly.</summary>
	public const float CullDistanceTerrainFraction = 1f / 5.5f;

	public const float ShadowDistanceTerrainFraction = 1f / 12f;
	public const float BillboardDistanceTerrainFraction = 1f / 9f;

	public static void ApplyTerrainScaledDistances( ThornsFoliageConfig config, float terrainSizeInches )
	{
		if ( config is null || terrainSizeInches <= 0f )
			return;

		var hide = terrainSizeInches * HideDistanceTerrainFraction;
		config.TreeLodHideDistanceInches = hide;
		config.TreeLodBillboardDistanceInches = MathF.Min(
			config.TreeLodBillboardDistanceInches,
			terrainSizeInches * BillboardDistanceTerrainFraction );
		config.TreeLodShadowDistanceInches = MathF.Min(
			ThornsVisualPerformanceDistances.TreeShadowInches,
			terrainSizeInches * ShadowDistanceTerrainFraction );
		config.CullDistanceInches = terrainSizeInches * CullDistanceTerrainFraction;
		config.CullHysteresisInches = MathF.Max( 2400f, hide * 0.12f );
	}
}
