namespace Terraingen;

/// <summary>Mounted .vmdl paths and default bounds for player-placed and world-spawned placeables.</summary>
public static class ThornsPlaceableModels
{
	public const string Campfire = "models/placeables/campfire.vmdl";
	public const string Chest = "models/placeables/chest.vmdl";
	public const string Cabinet = "models/placeables/cabinet.vmdl";
	public const string Workbench = "models/placeables/workbench.vmdl";
	public const string Bed = "models/placeables/bed.vmdl";
	public const string Couch = "models/placeables/couch.vmdl";
	public const string Chair = "models/placeables/chair.vmdl";
	public const string KitchenFridge = "models/placeables/kitchen_fridge.vmdl";
	public const string Fridge = "models/placeables/fridge.vmdl";
	public const string Desk = "models/placeables/desk.vmdl";
	public const string DiningTable = "models/placeables/dining_table.vmdl";
	public const string Conference = "models/placeables/conference.vmdl";
	public const string MilitarySupply = "models/placeables/military_supply.vmdl";
	public const string Pallets = "models/placeables/pallets.vmdl";
	public const string Retail = "models/placeables/retail.vmdl";
	public const string Radio = "models/placeables/radio.vmdl";
	public const string Research = "models/placeables/research.vmdl";
	public const string C4 = "models/placeables/c4.vmdl";

	public const string ResearchStructureId = "research";

	public static string ModelForStructureId( string structureId ) => structureId switch
	{
		"storage_chest" => Chest,
		"campfire" => Campfire,
		"workbench" => Workbench,
		"bed" => Bed,
		ResearchStructureId => Research,
		_ => ThornsModelResourceLoad.DevBoxPath
	};

	public static string ModelForKitItemId( string itemId ) =>
		TryGetKitStructureId( itemId, out var structureId )
			? ModelForStructureId( structureId )
			: "";

	public static bool TryGetKitStructureId( string itemId, out string structureId )
	{
		structureId = itemId switch
		{
			"storage_chest_kit" => "storage_chest",
			"campfire_kit" => "campfire",
			"workbench_kit" => "workbench",
			"bed_kit" => "bed",
			"research_kit" => ResearchStructureId,
			_ => ""
		};

		return !string.IsNullOrWhiteSpace( structureId );
	}

	public static Model LoadStructureModel( string structureId ) =>
		ThornsModelResourceLoad.LoadOrFallback( ModelForStructureId( structureId ) );

	public static Model LoadPlaceableModel( string modelPath ) =>
		ThornsModelResourceLoad.LoadOrFallback( modelPath );
}
