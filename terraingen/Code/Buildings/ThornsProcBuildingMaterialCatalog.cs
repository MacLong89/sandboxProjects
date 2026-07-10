namespace Terraingen.Buildings;

/// <summary>
/// Per-archetype facade palette from <c>materials/building_materials/*.vmat</c>
/// (all building materials except <c>glass_panes_*</c>).
/// </summary>
public static class ThornsProcBuildingMaterialCatalog
{
	public const string PathPrefix = "materials/building_materials/";

	public static readonly string[] AllSlugs =
	{
		"airdrop",
		"barn_wood",
		"brick",
		"cobblestone_brick",
		"concrete",
		"concrete_dark",
		"light_wood_siding",
		"metal",
		"sheet_metal",
		"stone",
		"stone_brick",
		"stucco",
		"wood"
	};

	public readonly record struct Profile(
		string Wall,
		string Floor,
		string Trim,
		string Ramp,
		string Roof = null,
		string Foundation = null )
	{
		public string RoofSlug => string.IsNullOrWhiteSpace( Roof ) ? Floor : Roof;
		public string FoundationSlug => string.IsNullOrWhiteSpace( Foundation ) ? Floor : Foundation;
	}

	public static Profile Resolve( ThornsProcBuildingType buildingType, int buildingIndex = 0 )
	{
		var profile = buildingType switch
		{
			ThornsProcBuildingType.Skyscraper => new Profile(
				Wall: "concrete",
				Floor: "concrete",
				Trim: "metal",
				Ramp: "concrete",
				Roof: "concrete_dark" ),
			ThornsProcBuildingType.ApartmentTower => new Profile(
				Wall: "concrete_dark",
				Floor: "concrete",
				Trim: "metal",
				Ramp: "concrete",
				Roof: "concrete_dark" ),
			ThornsProcBuildingType.OfficeBuilding => new Profile(
				Wall: "stone_brick",
				Floor: "concrete_dark",
				Trim: "stone",
				Ramp: "concrete",
				Roof: "concrete" ),
			ThornsProcBuildingType.Apartment => new Profile(
				Wall: "brick",
				Floor: "wood",
				Trim: "stone_brick",
				Ramp: "wood",
				Roof: "stucco" ),
			ThornsProcBuildingType.House => new Profile(
				Wall: "light_wood_siding",
				Floor: "wood",
				Trim: "barn_wood",
				Ramp: "wood",
				Roof: "light_wood_siding" ),
			ThornsProcBuildingType.Store => new Profile(
				Wall: "stucco",
				Floor: "wood",
				Trim: "brick",
				Ramp: "wood",
				Roof: "stucco" ),
			ThornsProcBuildingType.Warehouse => new Profile(
				Wall: "sheet_metal",
				Floor: "concrete",
				Trim: "metal",
				Ramp: "sheet_metal",
				Roof: "sheet_metal",
				Foundation: "concrete" ),
			ThornsProcBuildingType.Factory => new Profile(
				Wall: "metal",
				Floor: "concrete",
				Trim: "sheet_metal",
				Ramp: "metal",
				Roof: "metal",
				Foundation: "concrete_dark" ),
			ThornsProcBuildingType.MilitaryComplex => new Profile(
				Wall: "concrete_dark",
				Floor: "concrete",
				Trim: "metal",
				Ramp: "concrete",
				Roof: "sheet_metal",
				Foundation: "concrete_dark" ),
			ThornsProcBuildingType.RadioOutpost => new Profile(
				Wall: "airdrop",
				Floor: "concrete",
				Trim: "metal",
				Ramp: "sheet_metal",
				Roof: "sheet_metal",
				Foundation: "concrete" ),
			ThornsProcBuildingType.Barn => new Profile(
				Wall: "barn_wood",
				Floor: "wood",
				Trim: "barn_wood",
				Ramp: "wood",
				Roof: "barn_wood" ),
			ThornsProcBuildingType.Cabin => new Profile(
				Wall: "light_wood_siding",
				Floor: "wood",
				Trim: "wood",
				Ramp: "wood",
				Roof: "wood" ),
			ThornsProcBuildingType.Ruin => new Profile(
				Wall: "cobblestone_brick",
				Floor: "stone",
				Trim: "stone",
				Ramp: "cobblestone_brick",
				Roof: "stone",
				Foundation: "stone" ),
			_ => new Profile(
				Wall: "brick",
				Floor: "wood",
				Trim: "stone_brick",
				Ramp: "wood",
				Roof: "wood" )
		};

		if ( buildingIndex <= 0 )
			return profile;

		return MaybeSwapWallForVariant( profile, buildingType, buildingIndex );
	}

	/// <summary>Second building of the same type gets a related wall finish so blocks are not identical.</summary>
	static Profile MaybeSwapWallForVariant( Profile profile, ThornsProcBuildingType type, int buildingIndex )
	{
		if ( buildingIndex % 3 != 0 )
			return profile;

		var altWall = type switch
		{
			ThornsProcBuildingType.Skyscraper => "concrete_dark",
			ThornsProcBuildingType.ApartmentTower => "concrete",
			ThornsProcBuildingType.OfficeBuilding => "brick",
			ThornsProcBuildingType.Apartment => "stucco",
			ThornsProcBuildingType.House => "stucco",
			ThornsProcBuildingType.Store => "brick",
			ThornsProcBuildingType.Warehouse => "metal",
			ThornsProcBuildingType.Factory => "sheet_metal",
			ThornsProcBuildingType.MilitaryComplex => "sheet_metal",
			ThornsProcBuildingType.RadioOutpost => "sheet_metal",
			ThornsProcBuildingType.Barn => "light_wood_siding",
			ThornsProcBuildingType.Cabin => "barn_wood",
			ThornsProcBuildingType.Ruin => "stone_brick",
			_ => profile.Wall
		};

		return profile with { Wall = altWall };
	}
}
