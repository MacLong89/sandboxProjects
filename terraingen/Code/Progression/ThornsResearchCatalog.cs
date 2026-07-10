namespace Terraingen.GameData;

/// <summary>Sequential Ascension research ladder used by placed Research Stations.</summary>
public static class ThornsResearchCatalog
{
	public const int MaxLevel = 30;

	static readonly List<ThornsResearchLevelDefinition> Levels = BuildLevels();

	public static IReadOnlyList<ThornsResearchLevelDefinition> All => Levels;

	public static bool TryGet( int level, out ThornsResearchLevelDefinition def )
	{
		def = level >= 1 && level <= Levels.Count ? Levels[level - 1] : null;
		return def is not null;
	}

	public static HashSet<string> CollectRewardItemIds()
	{
		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var level in Levels )
		{
			if ( level is null || string.IsNullOrWhiteSpace( level.RewardItemId ) || level.RewardCount <= 0 )
				continue;

			ids.Add( ThornsItemIdAliases.Canonicalize( level.RewardItemId ) );
		}

		return ids;
	}

	static List<ThornsResearchLevelDefinition> BuildLevels()
	{
		var names = new[]
		{
			"Surveying", "Stoneworking", "Field Medicine", "Preserved Food", "Basic Metallurgy",
			"Leatherworking", "Workshop Methods", "Ballistics", "Signal Theory", "Hydraulics",
			"Fortified Storage", "Refined Alloys", "Applied Chemistry", "Optics", "Power Cells",
			"Radio Networks", "Precision Tools", "Composite Armor", "Automated Milling", "Advanced Ammunition",
			"Thermal Control", "Mobile Fabrication", "Biofiltration", "Terrain Scanning", "Energy Storage",
			"Long Range Comms", "Prototype Weapons", "Bloom Countermeasures", "Ancient Systems", "Ascension Protocol"
		};

		var list = new List<ThornsResearchLevelDefinition>( MaxLevel );
		for ( var i = 1; i <= MaxLevel; i++ )
		{
			var (rewardItemId, rewardCount) = RewardForLevel( i );
			list.Add( new ThornsResearchLevelDefinition
			{
				Level = i,
				Title = names[i - 1],
				Description = BuildDescription( i, rewardItemId, rewardCount ),
				ResearchSeconds = 30f + i * 10f,
				Costs = BuildCosts( i ),
				RewardItemId = rewardItemId,
				RewardCount = rewardCount
			} );
		}

		return list;
	}

	static string BuildDescription( int level, string rewardItemId, int rewardCount )
	{
		if ( string.IsNullOrWhiteSpace( rewardItemId ) || rewardCount <= 0 )
			return $"Ascension research level {level}.";

		return $"Ascension research level {level}. Completing this tier grants a {rewardItemId} reward.";
	}

	static (string ItemId, int Count) RewardForLevel( int level ) => level switch
	{
		1 => ("bandage", 5),
		2 => ("stone_hatchet", 1),
		3 => ("workbench_kit", 1),
		4 => ("food", 4),
		5 => ("pistol_ammo", 24),
		6 => ("leather_scrap", 8),
		7 => ("storage_chest_kit", 1),
		8 => ("smelt_metal", 6),
		9 => ("pistol_ammo", 15),
		10 => ("field_rations", 4),
		11 => ("stone_pickaxe", 1),
		12 => ("iron_pickaxe", 1),
		13 => ("canned_stew", 3),
		14 => ("attachment_red_dot", 1),
		15 => ("usp", 1),
		16 => ("scrap_chest", 1),
		17 => ("scrap_head", 1),
		18 => ("scrap_legs", 1),
		19 => ("morphine_pen", 2),
		20 => ("research_kit", 1),
		21 => ("kevlar_legs", 1),
		22 => ("smg_ammo", 40),
		23 => ("attachment_suppressor", 1),
		24 => ("wood_ramp", 2),
		25 => ("kevlar_head", 1),
		26 => ("usp", 1),
		27 => ("attachment_extended_mag", 1),
		28 => ("c4", 1),
		29 => ("kevlar_chest", 1),
		30 => ("sniper", 1),
		_ => ("", 0)
	};

	static List<ThornsResearchIngredientDto> BuildCosts( int level )
	{
		var costs = new List<ThornsResearchIngredientDto>
		{
			Cost( "wood", 15 + level * 4 ),
			Cost( "stone", 12 + level * 5 )
		};

		if ( level >= 4 )
			costs.Add( Cost( "metal_ore", 4 + level * 2 ) );
		if ( level >= 8 )
			costs.Add( Cost( "smelt_metal", 2 + level ) );
		if ( level >= 12 )
			costs.Add( Cost( "cloth", 4 + level ) );
		if ( level >= 18 )
			costs.Add( Cost( "leather_scrap", 2 + level / 2 ) );

		return costs;
	}

	static ThornsResearchIngredientDto Cost( string itemId, int count ) => new()
	{
		ItemId = itemId,
		Count = Math.Max( 1, count )
	};
}
