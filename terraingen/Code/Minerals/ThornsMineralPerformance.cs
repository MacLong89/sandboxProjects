namespace Terraingen.Minerals;

using Terraingen.Foliage;

/// <summary>Terrain-scaled mineral cull bands — render instanced, colliders only near the player.</summary>
public static class ThornsMineralPerformance
{
	public static void ApplyTerrainScaledDistances( ThornsMineralConfig config, float terrainSizeInches )
	{
		if ( config is null || terrainSizeInches <= 0f )
			return;

		var hide = terrainSizeInches * ThornsFoliagePerformance.HideDistanceTerrainFraction;
		config.CullDistanceInches = terrainSizeInches * ThornsFoliagePerformance.CullDistanceTerrainFraction;
		config.CullHysteresisInches = MathF.Max( 2400f, hide * 0.12f );
	}
}
