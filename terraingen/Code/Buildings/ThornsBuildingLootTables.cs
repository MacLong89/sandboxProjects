namespace Terraingen.Buildings;

using Terraingen.Combat.Attachments;
using Terraingen.GameData;

public readonly struct ThornsBuildingLoot
{
	public readonly string ItemId;
	public readonly int Count;

	public ThornsBuildingLoot( string itemId, int count )
	{
		ItemId = itemId;
		Count = count;
	}
}

/// <summary>Building and furniture loot pools — each table is themed for a specific interior context.</summary>
public static class ThornsBuildingLootTables
{
	static readonly string[] LongGuns = { "m4", "mp5", "shotgun", "sniper" };
	static readonly string[] Sidearms = { "usp", "mp5" };
	static readonly string[] AllGuns = { "m4", "mp5", "shotgun", "sniper", "usp" };
	static readonly string[] MilitarySidearms = { "m4", "mp5", "usp" };
	static readonly string[] StoneTools = { "stone_hatchet", "stone_pickaxe" };
	static readonly string[] MetalTools = { "iron_hatchet", "iron_pickaxe" };
	static readonly string[] ArmorPieces = { "scrap_chest", "scrap_head", "scrap_legs", "kevlar_head", "kevlar_chest", "kevlar_legs" };
	static readonly string[] BuildKits = { "campfire_kit", "bed_kit", "storage_chest_kit", "workbench_kit", "research_kit" };
	static readonly string[] WoodBuildParts = { "wood_foundation", "wood_wall", "wood_doorframe", "wood_window", "wood_ramp" };

	public static readonly string[] AllTableNames =
	[
		"kitchen_fridge", "store_fridge", "breakroom_snacks", "office_desk", "military_intel", "home_desk",
		"store_office", "bedroom_cabinet", "military_locker", "worker_locker", "office_supply", "apartment_closet",
		"store_shelves", "office_conference", "military_briefing", "home_dining", "military_mess", "warehouse_pallets",
		"factory_pallets", "barn_storage", "military_crate", "salvage_pile", "factory_floor", "barn_loft", "radio_cache",
		"ruin_scrap", "cabin_survival", "home_clutter", "military_armory", "military_ammo", "military_medical", "military_gear"
	];

	/// <summary>Every item id that can appear from building or bandit loot rolls.</summary>
	public static HashSet<string> CollectAllPossibleLootItemIds()
	{
		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		AddMany( ids, LongGuns );
		AddMany( ids, Sidearms );
		AddMany( ids, AllGuns );
		AddMany( ids, StoneTools );
		AddMany( ids, MetalTools );
		AddMany( ids, ArmorPieces );
		AddMany( ids, BuildKits );
		AddMany( ids, WoodBuildParts );

		foreach ( var attachmentId in ThornsAttachmentItemIds.EnabledInGame )
			ids.Add( attachmentId );

		foreach ( var table in AllTableNames )
		{
			foreach ( var token in ResolvePool( table ) )
				AddTokenPossibilities( token, ids );
		}

		return ids;
	}

	static void AddMany( HashSet<string> ids, IEnumerable<string> items )
	{
		foreach ( var item in items )
			ids.Add( item );
	}

	static void AddTokenPossibilities( string token, HashSet<string> ids )
	{
		switch ( token )
		{
			case "long_gun":
				AddMany( ids, LongGuns );
				return;
			case "sidearm":
				AddMany( ids, Sidearms );
				return;
			case "weapon":
				AddMany( ids, AllGuns );
				return;
			case "attachment":
				AddMany( ids, ThornsAttachmentItemIds.EnabledInGame );
				return;
			case "stone_tool":
				AddMany( ids, StoneTools );
				return;
			case "metal_tool":
				AddMany( ids, MetalTools );
				return;
			case "armor_piece":
				AddMany( ids, ArmorPieces );
				return;
			case "build_kit":
				AddMany( ids, BuildKits );
				return;
			case "wood_build":
				AddMany( ids, WoodBuildParts );
				return;
		}

		if ( IsValidItem( token ) )
			ids.Add( token );
	}

	public static IEnumerable<ThornsBuildingLoot> Roll( string table, Random rng, bool excludePlayerBuildingItems = false )
	{
		table = NormalizeLegacyTable( table );
		var pool = ResolvePool( table );
		if ( pool.Length == 0 )
			pool = ResolvePool( "home_clutter" );

		if ( excludePlayerBuildingItems )
			pool = FilterPlayerBuildingTokens( pool );

		var rollCount = IsPremiumTable( table ) ? rng.Next( 2, 5 ) : rng.Next( 2, 4 );
		for ( var i = 0; i < rollCount; i++ )
		{
			var token = pool[rng.Next( pool.Length )];
			if ( TryResolveToken( token, table, rng, out var loot ) )
			{
				if ( excludePlayerBuildingItems && IsPlayerBuildingLootItem( loot.ItemId ) )
					continue;

				yield return loot;
			}
		}

		if ( table is "military_armory" or "military_briefing" && rng.NextSingle() < 0.30f )
		{
			var gun = MilitarySidearms[rng.Next( MilitarySidearms.Length )];
			if ( IsValidItem( gun ) )
				yield return new ThornsBuildingLoot( gun, 1 );
		}
		else if ( table is "military_armory" && rng.NextSingle() < 0.12f )
		{
			var gun = LongGuns[rng.Next( LongGuns.Length )];
			if ( IsValidItem( gun ) )
				yield return new ThornsBuildingLoot( gun, 1 );
		}
	}

	static string NormalizeLegacyTable( string table )
	{
		if ( string.IsNullOrWhiteSpace( table ) )
			return "home_clutter";

		return table.Trim().ToLowerInvariant() switch
		{
			"provisions" or "kitchen" => "kitchen_fridge",
			"bedroom" => "bedroom_cabinet",
			"workshop" => "factory_floor",
			"office" => "office_desk",
			"retail" => "store_shelves",
			"military" => "military_armory",
			"industrial" => "factory_pallets",
			"salvage" => "salvage_pile",
			"weapons" => "military_armory",
			"armor" => "military_gear",
			"ammo" => "military_ammo",
			"domestic" => "home_clutter",
			_ => table
		};
	}

	static string[] ResolvePool( string table ) => table switch
	{
		"kitchen_fridge" => Tok( "food", "water", "raw_meat", "canned_stew", "field_rations", "cloth" ),
		"store_fridge" => Tok( "food", "water", "canned_stew", "bandage", "field_rations" ),
		"breakroom_snacks" => Tok( "food", "water", "canned_stew" ),

		"office_desk" => Tok( "pistol_ammo", "bandage", "morphine_pen", "field_rations", "water", "research_kit" ),
		"military_intel" => Tok( "pistol_ammo", "rifle_ammo", "field_rations", "morphine_pen", "research_kit", "c4" ),
		"home_desk" => Tok( "wood", "cloth", "bandage", "food", "pistol_ammo" ),
		"store_office" => Tok( "bandage", "cloth", "storage_chest_kit", "food", "water" ),

		"bedroom_cabinet" => Tok( "cloth", "bandage", "leather_scrap", "bed_kit", "animal_hide", "bed" ),
		"military_locker" => Tok( "kevlar_chest", "kevlar_head", "bandage", "field_rations", "medkit", "scrap_chest", "scrap_head", "scrap_legs" ),
		"worker_locker" => Tok( "cloth", "bandage", "workbench_kit", "stone_hatchet", "food", "stone_pickaxe" ),
		"office_supply" => Tok( "cloth", "bandage", "medkit", "research_kit", "storage_chest_kit" ),
		"apartment_closet" => Tok( "cloth", "bandage", "bed_kit", "food", "water", "leather_scrap" ),

		"store_shelves" => Tok( "food", "water", "canned_stew", "cloth", "storage_chest_kit", "bandage", "field_rations" ),
		"office_conference" => Tok( "medkit", "water", "field_rations", "pistol_ammo", "morphine_pen", "research_kit" ),
		"military_briefing" => Tok( "medkit", "rifle_ammo", "field_rations", "attachment", "long_gun", "m9_bayonet" ),

		"home_dining" => Tok( "food", "water", "wood", "cloth", "canned_stew", "campfire_kit" ),
		"military_mess" => Tok( "field_rations", "canned_stew", "water", "bandage", "medkit" ),

		"warehouse_pallets" => Tok( "wood", "stone", "metal_ore", "smelt_metal", "stone_pickaxe", "storage_chest_kit" ),
		"factory_pallets" => Tok( "metal_ore", "smelt_metal", "stone", "iron_pickaxe", "iron_hatchet", "workbench_kit" ),
		"barn_storage" => Tok( "animal_hide", "raw_meat", "leather_scrap", "cloth", "stone_hatchet" ),
		"military_crate" => Tok( "rifle_ammo", "smg_ammo", "bandage", "smelt_metal", "attachment", "pistol_ammo" ),
		"salvage_pile" => Tok( "metal_ore", "cloth", "stone", "wood", "leather_scrap", "scrap_chest", "scrap_head", "scrap_legs", "stone_hatchet" ),

		"factory_floor" => Tok( "metal_ore", "smelt_metal", "iron_hatchet", "workbench_kit", "stone", "workbench" ),
		"barn_loft" => Tok( "animal_hide", "raw_meat", "leather_scrap", "cloth", "pistol_ammo" ),
		"radio_cache" => Tok( "research_kit", "field_rations", "rifle_ammo", "attachment", "medkit", "research" ),
		"ruin_scrap" => Tok( "stone_hatchet", "stone_pickaxe", "pistol_ammo", "cloth", "bandage", "wood" ),
		"cabin_survival" => Tok( "stone_hatchet", "campfire_kit", "food", "water", "animal_hide", "pistol_ammo" ),
		"home_clutter" => Tok( "wood", "cloth", "food", "water", "campfire_kit", "bandage", "wood_build" ),

		"military_armory" => Tok( "long_gun", "sidearm", "m9_bayonet", "rifle_ammo", "smg_ammo", "attachment", "bandage", "c4" ),
		"military_ammo" => Tok( "rifle_ammo", "smg_ammo", "pistol_ammo", "shotgun_ammo", "sniper_ammo", "smelt_metal", "metal_ore" ),
		"military_medical" => Tok( "medkit", "bandage", "morphine_pen", "field_rations", "water", "canned_stew" ),
		"military_gear" => Tok( "armor_piece", "kevlar_head", "kevlar_chest", "kevlar_legs", "scrap_chest", "scrap_head", "scrap_legs", "cloth", "metal_ore", "attachment" ),

		_ => Array.Empty<string>()
	};

	static string[] Tok( params string[] tokens ) => tokens;

	static bool IsPremiumTable( string table ) =>
		table is "military_armory" or "military_briefing" or "military_gear" or "radio_cache";

	static bool TryResolveToken( string token, string table, Random rng, out ThornsBuildingLoot loot )
	{
		loot = default;
		if ( string.IsNullOrWhiteSpace( token ) )
			return false;

		switch ( token )
		{
			case "long_gun":
				return TryReturnItem( LongGuns[rng.Next( LongGuns.Length )], 1, out loot );
			case "sidearm":
				return TryReturnItem( Sidearms[rng.Next( Sidearms.Length )], 1, out loot );
			case "weapon":
				return TryReturnItem( AllGuns[rng.Next( AllGuns.Length )], 1, out loot );
			case "attachment":
				return TryReturnItem( ThornsWeaponAttachmentRoll.RollLooseAttachmentItemId( rng ), 1, out loot );
			case "stone_tool":
				return TryReturnItem( StoneTools[rng.Next( StoneTools.Length )], 1, out loot );
			case "metal_tool":
				return TryReturnItem( MetalTools[rng.Next( MetalTools.Length )], 1, out loot );
			case "armor_piece":
				return TryReturnItem( ArmorPieces[rng.Next( ArmorPieces.Length )], 1, out loot );
			case "build_kit":
				return TryReturnItem( BuildKits[rng.Next( BuildKits.Length )], 1, out loot );
			case "wood_build":
				return TryReturnItem( WoodBuildParts[rng.Next( WoodBuildParts.Length )], rng.Next( 1, 4 ), out loot );
		}

		if ( !IsValidItem( token ) )
			return false;

		if ( token is "c4" or "research_kit" or "m9_bayonet" or "bed" or "workbench" or "research" or "campfire" or "storage_chest" )
		{
			if ( rng.NextSingle() > RareDropChance( token, table ) )
				return false;
		}

		var count = ResolveStackCount( token, table, rng );
		loot = new ThornsBuildingLoot( token, count );
		return true;
	}

	static float RareDropChance( string itemId, string table ) => itemId switch
	{
		"c4" => table is "military_intel" or "military_armory" ? 0.08f : 0.02f,
		"research_kit" or "research" => table is "radio_cache" or "office_desk" or "office_conference" or "office_supply" ? 0.22f : 0.10f,
		"m9_bayonet" => table is "military_briefing" or "military_armory" ? 0.18f : 0.06f,
		"bed" or "workbench" or "campfire" or "storage_chest" => 0.14f,
		_ => 1f
	};

	static int ResolveStackCount( string itemId, string table, Random rng ) => itemId switch
	{
		"rifle_ammo" or "smg_ammo" or "pistol_ammo" or "shotgun_ammo" or "sniper_ammo" => rng.Next( 6, 18 ),
		"wood" or "stone" or "metal_ore" => table is "warehouse_pallets" ? rng.Next( 12, 28 ) : rng.Next( 6, 20 ),
		"smelt_metal" => rng.Next( 2, 7 ),
		"cloth" or "leather_scrap" or "animal_hide" => rng.Next( 2, 8 ),
		"food" or "water" or "bandage" or "raw_meat" or "canned_stew" or "field_rations" => rng.Next( 1, 4 ),
		_ => 1
	};

	static bool TryReturnItem( string itemId, int count, out ThornsBuildingLoot loot )
	{
		if ( !IsValidItem( itemId ) )
		{
			loot = default;
			return false;
		}

		loot = new ThornsBuildingLoot( itemId, Math.Max( 1, count ) );
		return true;
	}

	static bool IsValidItem( string itemId ) =>
		!string.IsNullOrWhiteSpace( itemId ) && ThornsDefinitionRegistry.GetItem( itemId ) is not null;

	/// <summary>Grid building pieces and portable placeables — excluded from proc-interior furniture containers.</summary>
	public static bool IsPlayerBuildingLootItem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( ThornsPlayerBuildingDefinitions.TryResolveHotbarPlaceable( itemId, out _ ) )
			return true;

		return ThornsPlayerBuildingDefinitions.TryGet( itemId, out var def )
			&& def.PlacementKind == ThornsPlayerBuildPlacementKind.Grid;
	}

	static bool IsPlayerBuildingLootToken( string token ) => token switch
	{
		"build_kit" or "wood_build" => true,
		_ => IsPlayerBuildingLootItem( token )
	};

	static string[] FilterPlayerBuildingTokens( string[] pool )
	{
		var filtered = pool.Where( token => !IsPlayerBuildingLootToken( token ) ).ToArray();
		return filtered.Length > 0 ? filtered : pool;
	}
}
