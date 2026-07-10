namespace Sandbox;

/// <summary>World-gen toggles for procedural building height.</summary>
public static class ThornsProcBuildingPoc
{
	/// <summary>When true, every building is clamped to one walkable storey (debug / perf).</summary>
	public const bool SingleStoryOnly = false;

	public static int EffectiveStories( int stories ) =>
		SingleStoryOnly ? 1 : Math.Clamp( stories, 1, 8 );

	/// <summary>Skyline types always compile every blueprint layer.</summary>
	public static bool IsVerticalLandmark( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.ApartmentTower
			or ThornsProcBuildingType.OfficeBuilding;

	/// <summary>
	/// Storey count for a tile blueprint — landmarks use all layers; others roll between
	/// <see cref="ThornsProcBuildingTypeDefinition.StoriesMin"/> and <c>StoriesMax</c> (capped by layer count).
	/// </summary>
	public static int RollStoriesForBlueprint( ThornsProcTileBlueprint blueprint, Random rnd )
	{
		if ( blueprint?.Layers is null || blueprint.Layers.Count == 0 )
			return 1;

		var layerCount = EffectiveStories( blueprint.Layers.Count );
		if ( SingleStoryOnly || layerCount <= 1 )
			return 1;

		if ( IsVerticalLandmark( blueprint.Type ) )
			return layerCount;

		var def = ThornsProcBuildingIdentityRegistry.Get( blueprint.Type );
		var min = Math.Clamp( def.StoriesMin, 1, layerCount );
		var max = Math.Clamp( def.StoriesMax, min, layerCount );
		if ( !IsVerticalLandmark( blueprint.Type ) )
			max = Math.Min( max, ThornsInteriorFurnitureFloorplanAscii.MaxSettlementAsciiStories );

		if ( max <= 1 )
			return 1;

		return rnd.Next( min, max + 1 );
	}
}
