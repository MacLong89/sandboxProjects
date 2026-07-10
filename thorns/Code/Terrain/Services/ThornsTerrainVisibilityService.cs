namespace Sandbox;

/// <summary>Visibility presets and foliage distance culling bootstrap.</summary>
public static class ThornsTerrainVisibilityService
{
	public static void ApplyBootVisibility( Scene scene, ThornsVisibilityTier tier ) =>
		ThornsVisibilityPresets.ApplyToLocalPawnCamera( scene, tier );

	public static void EnsureFoliageDistanceCuller( Component host, bool enableCulling, bool generateFoliageFluff )
	{
		if ( !enableCulling || !generateFoliageFluff )
			return;

		var c = host.Components.Get<ThornsFoliageDistanceCullSystem>( FindMode.EverythingInSelfAndDescendants );
		if ( c.IsValid() )
			return;

		_ = host.Components.Create<ThornsFoliageDistanceCullSystem>();
	}
}
