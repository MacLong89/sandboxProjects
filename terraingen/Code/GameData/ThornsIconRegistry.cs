namespace Terraingen.GameData;

/// <summary>Central UI icon paths — flat PNGs under <c>Assets/ui/iconsv8/</c> (mounted as <c>ui/iconsv8/</c>).</summary>
public static class ThornsIconRegistry
{
	public const string Root = "ui/iconsv8";

	public static string Creature( string speciesKey )
	{
		if ( string.IsNullOrWhiteSpace( speciesKey ) )
			return Path( "wolf" );

		return Path( speciesKey );
	}

	public static string TameStat( string statKey ) => Path( statKey switch
	{
		"attack" => "damage",
		_ => statKey
	} );

	public static string TameCommand( ThornsTameCommand command ) => Path( command.ToString() );

	public static string TameTrait( string traitId ) => Path( traitId );

	public static string Skill( string skillId ) => Path( ResolveSkillStem( skillId ) );

	public static string SkillCategory( ThornsSkillCategory category ) => Path( SkillCategoryStem( category ) );

	public static string JournalCategory( ThornsJourneyCategory category ) => Path( category.ToString() );

	public static string JournalSection( ThornsJournalSection section ) => Path( section switch
	{
		ThornsJournalSection.Discoveries => "discoveries",
		ThornsJournalSection.Events => "events",
		ThornsJournalSection.Achievements => "achievements",
		ThornsJournalSection.VictoryPaths => "dominion",
		_ => "quest"
	} );

	public static string MapMarker( ThornsMapMarkerKind kind ) => Path( kind.ToString() );

	public static string GuildEmblem() => Path( "guild" );
	public static string GuildMember() => Path( "you" );
	public static string GuildActivity( string kind ) => Path( kind );
	public static string GuildActivityDefault() => Path( "notification" );
	public static string Guild( string key ) => Path( key );

	public static string Victory( string pathId ) => Path( pathId );

	public static string Hud( string key ) => Path( key );

	public static string MenuTab( string tabId ) => Path( tabId );

	public static string InventoryUi( string key ) => Path( key );

	/// <summary>Crafting menu category tab icons (uses item art where noted).</summary>
	public static string CraftCategoryIcon( string categoryId )
	{
		if ( string.IsNullOrWhiteSpace( categoryId )
		     || string.Equals( categoryId, "all", StringComparison.OrdinalIgnoreCase ) )
			return InventoryUi( "inventory" );

		return Normalize( categoryId ) switch
		{
			"food" => ItemInventoryIconPath( "apple" ),
			"weapons" => ItemInventoryIconPath( "m4" ),
			"attachments" => ThornsItemIdAliases.AttachmentLegacyIconPath( "attachment_red_dot" ),
			_ => InventoryUi( $"craft_{Normalize( categoryId )}" )
		};
	}

	public static string Item( string itemId ) => Path( itemId );

	/// <summary>Inventory/craft item art — uses the item id stem, not craft-category aliases.</summary>
	public static string ItemInventoryIconPath( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return Path( "special" );

		return $"{Root}/{Normalize( itemId )}.png";
	}

	public static string Path( string logicalKey )
	{
		var stem = ResolveStem( logicalKey );
		return $"{Root}/{stem}.png";
	}

	internal static string ResolveStem( string logicalKey )
	{
		if ( string.IsNullOrWhiteSpace( logicalKey ) )
			return "special";

		var normalized = Normalize( logicalKey );
		if ( StemAliases.TryGetValue( normalized, out var alias ) )
			return alias;

		return normalized;
	}

	internal static string ResolveSkillStem( string skillId )
	{
		if ( string.IsNullOrWhiteSpace( skillId ) )
			return "special";

		var normalized = Normalize( skillId );
		if ( SkillStemAliases.TryGetValue( normalized, out var alias ) )
			return alias;

		return normalized;
	}

	static string SkillCategoryStem( ThornsSkillCategory category ) => category switch
	{
		ThornsSkillCategory.Persistence => "survival",
		ThornsSkillCategory.Instinct => "combat",
		ThornsSkillCategory.Industry => "building",
		_ => "special"
	};

	static string Normalize( string value ) =>
		(value ?? "").Trim().ToLowerInvariant().Replace( ' ', '_' );

	/// <summary>Maps logical ids (tabs, HUD keys, map kinds, items) to <c>iconsv8</c> filenames.</summary>
	static readonly Dictionary<string, string> StemAliases = new( StringComparer.OrdinalIgnoreCase )
	{
		// Menu tabs
		["inventory"] = "inventory",
		["journal"] = "journal",
		["tames"] = "tames",
		["taming"] = "tames",
		["skills"] = "special",
		["map"] = "map",
		["guild"] = "guild",
		["settings"] = "settings",
		["exit"] = "home",
		["close"] = "settings",

		// Skill categories (enum names)
		["persistence"] = "survival",
		["instinct"] = "combat",
		["industry"] = "building",

		// HUD / vitals
		["food"] = "hunger",
		["water"] = "thirst",
		["energy"] = "stamina",
		["stamina"] = "stamina",
		["xp"] = "special",
		["alert"] = "notification",

		// Guild overview glyphs
		["leader"] = "you",
		["rank"] = "special",
		["emblem"] = "guild",
		["activity_default"] = "notification",
		["victory_shift"] = "dominion",
		["member"] = "you",

		// Tame commands
		["guard"] = "combat",
		["passive"] = "stay",
		["attack"] = "combat",

		// Tame stats / traits
		["genetics"] = "lineage",
		["crossbreed"] = "lineage",
		["mutation"] = "special",
		["unknown"] = "wolf",

		// Map markers (enum → flat filename)
		["guildmember"] = "guild",
		["ruralpoi"] = "exploration",
		["cabinsite"] = "home",
		["farmstead"] = "home",
		["town"] = "city",
		["settlement"] = "town",
		["npcguildoutpost"] = "guild",
		["boss"] = "combat",
		["specialevent"] = "events",
		["goal"] = "quest",
		["customwaypoint"] = "quest",
		["bloomseed"] = "events",

		// Inventory UI
		["lock"] = "notification",

		// Legacy item ids (no dedicated art in iconsv8 yet)
		["kevlar_head"] = "craft_armor",
		["kevlar_chest"] = "craft_armor",
		["kevlar_legs"] = "craft_armor",
		["iron_pickaxe"] = "craft_tools",
		["iron_hatchet"] = "craft_tools",
		["stone_hatchet"] = "craft_tools",
		["stone_pickaxe"] = "craft_tools",
		["food"] = "hunger",
		["water"] = "thirst",
		["water_bottle"] = "thirst",
		["clean_water"] = "thirst",
		["scrap_chest"] = "craft_armor",
		["scrap_head"] = "craft_armor",
		["scrap_legs"] = "craft_armor",
		["smelt_metal"] = "craft_forge",
		["leather_scrap"] = "craft_armor",
		["bed_kit"] = "home",
		["campfire_kit"] = "home",
		["workbench_kit"] = "craft_build",
		["storage_chest_kit"] = "inventory",
		["wood_foundation"] = "craft_build",
		["wood_wall"] = "craft_build",
		["wood_doorframe"] = "craft_build",
		["wood"] = "craft_build",
		["stone"] = "craft_build",
		["cloth"] = "craft_armor",
		["metal_ore"] = "craft_forge",
		["animal_hide"] = "craft_armor",
		["bandage"] = "craft_medical",
		["medkit"] = "craft_medical",
		["m4"] = "craft_ammo",
		["mp5"] = "craft_ammo",
		["shotgun"] = "craft_ammo",
		["sniper"] = "craft_ammo",
		["rifle_ammo"] = "craft_ammo",
		["smg_ammo"] = "craft_ammo",
		["shotgun_ammo"] = "craft_ammo",
		["sniper_ammo"] = "craft_ammo",
		["apple"] = "hunger",
	};

	/// <summary>Skill id → PNG stem when the filename differs from the skill id.</summary>
	static readonly Dictionary<string, string> SkillStemAliases = new( StringComparer.OrdinalIgnoreCase )
	{
		["scavenger_skill"] = "scavenger"
	};
}
