namespace Sandbox;

/// <summary>Interior furniture plans — delegates to <see cref="ThornsInteriorFurnitureFloorLayouts"/>.</summary>
public static class ThornsInteriorFurnitureProfiles
{
	/// <summary>Interior radio on ground floor for select commercial / military types.</summary>
	public static bool ShouldSpawnInteriorRadioShop( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Store
			or ThornsProcBuildingType.OfficeBuilding
			or ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.MilitaryComplex
			or ThornsProcBuildingType.RadioOutpost;

	public static ThornsInteriorFurnitureFloorLayouts.BuildingLayouts LayoutsForBuilding( ThornsProcBuildingType type ) =>
		ThornsInteriorFurnitureFloorLayouts.ForBuildingType( type );
}
