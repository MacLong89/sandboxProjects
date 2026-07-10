namespace Terraingen.Buildings;

/// <summary>Which proc-interior placeables are loot containers vs decor, and how loot tables are chosen.</summary>
public static class ThornsFurnitureLootPolicy
{
	static readonly HashSet<string> NonLootDecor = new( StringComparer.OrdinalIgnoreCase )
	{
		"chair", "couch", "bed", "radio", "workbench"
	};

	static readonly HashSet<string> ProcLootFurniture = new( StringComparer.OrdinalIgnoreCase )
	{
		"desk", "cabinet", "fridge", "kitchen_fridge", "military_supply",
		"conference", "dining_table", "pallets", "retail"
	};

	public static bool IsNonLootDecor( string structureDefId ) =>
		!string.IsNullOrWhiteSpace( structureDefId ) && NonLootDecor.Contains( structureDefId );

	public static bool IsProcLootFurniture( string structureDefId ) =>
		!string.IsNullOrWhiteSpace( structureDefId )
		&& ProcLootFurniture.Contains( structureDefId )
		&& !IsNonLootDecor( structureDefId );

	public static bool ShouldSpawnProcLootContainer( string structureDefId ) => IsProcLootFurniture( structureDefId );

	public static string PickLootTable( string structureDefId, ThornsProcBuildingType buildingType, Random rng )
	{
		var id = structureDefId?.Trim().ToLowerInvariant() ?? "";
		return id switch
		{
			"kitchen_fridge" => "kitchen_fridge",
			"fridge" => PickFridgeTable( buildingType ),
			"desk" => PickDeskTable( buildingType ),
			"cabinet" => PickCabinetTable( buildingType ),
			"retail" => "store_shelves",
			"conference" => PickConferenceTable( buildingType ),
			"dining_table" => PickDiningTable( buildingType ),
			"pallets" => PickPalletsTable( buildingType ),
			"military_supply" => Sample( rng,
				"military_armory", "military_armory",
				"military_ammo", "military_medical", "military_gear" ),
			_ => PickBuildingAffinity( buildingType, rng )
		};
	}

	static string PickFridgeTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.Store => "store_fridge",
		ThornsProcBuildingType.Warehouse or ThornsProcBuildingType.Factory => "breakroom_snacks",
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_mess",
		_ => "kitchen_fridge"
	};

	static string PickDeskTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.OfficeBuilding or ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.ApartmentTower => "office_desk",
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_intel",
		ThornsProcBuildingType.Store => "store_office",
		ThornsProcBuildingType.House or ThornsProcBuildingType.Cabin
			or ThornsProcBuildingType.Apartment or ThornsProcBuildingType.Ruin => "home_desk",
		_ => "home_desk"
	};

	static string PickCabinetTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_locker",
		ThornsProcBuildingType.Warehouse or ThornsProcBuildingType.Factory
			or ThornsProcBuildingType.Barn => "worker_locker",
		ThornsProcBuildingType.OfficeBuilding or ThornsProcBuildingType.Skyscraper => "office_supply",
		ThornsProcBuildingType.ApartmentTower or ThornsProcBuildingType.Apartment => "apartment_closet",
		ThornsProcBuildingType.House or ThornsProcBuildingType.Cabin
			or ThornsProcBuildingType.Ruin => "bedroom_cabinet",
		_ => "bedroom_cabinet"
	};

	static string PickConferenceTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_briefing",
		_ => "office_conference"
	};

	static string PickDiningTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_mess",
		_ => "home_dining"
	};

	static string PickPalletsTable( ThornsProcBuildingType buildingType ) => buildingType switch
	{
		ThornsProcBuildingType.Warehouse => "warehouse_pallets",
		ThornsProcBuildingType.Factory => "factory_pallets",
		ThornsProcBuildingType.Barn => "barn_storage",
		ThornsProcBuildingType.MilitaryComplex or ThornsProcBuildingType.RadioOutpost => "military_crate",
		_ => "salvage_pile"
	};

	static string PickBuildingAffinity( ThornsProcBuildingType buildingType, Random rng ) => buildingType switch
	{
		ThornsProcBuildingType.Warehouse => "warehouse_pallets",
		ThornsProcBuildingType.Factory => Sample( rng, "factory_floor", "factory_pallets" ),
		ThornsProcBuildingType.Barn => Sample( rng, "barn_loft", "barn_storage" ),
		ThornsProcBuildingType.MilitaryComplex => Sample( rng, "military_armory", "military_ammo", "military_gear" ),
		ThornsProcBuildingType.RadioOutpost => Sample( rng, "radio_cache", "military_medical", "military_locker" ),
		ThornsProcBuildingType.Store => Sample( rng, "store_shelves", "store_fridge" ),
		ThornsProcBuildingType.OfficeBuilding => Sample( rng, "office_desk", "office_supply", "office_conference" ),
		ThornsProcBuildingType.Skyscraper => Sample( rng, "office_desk", "office_conference", "office_supply" ),
		ThornsProcBuildingType.ApartmentTower => Sample( rng, "apartment_closet", "home_clutter", "kitchen_fridge" ),
		ThornsProcBuildingType.Ruin => Sample( rng, "ruin_scrap", "salvage_pile", "home_clutter" ),
		ThornsProcBuildingType.Cabin => Sample( rng, "cabin_survival", "bedroom_cabinet", "kitchen_fridge" ),
		ThornsProcBuildingType.House or ThornsProcBuildingType.Apartment => Sample( rng, "home_clutter", "bedroom_cabinet", "kitchen_fridge" ),
		_ => Sample( rng, "home_clutter", "bedroom_cabinet" )
	};

	static string Sample( Random rng, params string[] tables ) =>
		tables is { Length: > 0 } ? tables[rng.Next( tables.Length )] : "home_clutter";
}
