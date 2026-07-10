namespace Terraingen.GameData;

/// <summary>15 spendable upgrade skills — 3 trees × 5 skills (legacy ThornsUpgradeCategory).</summary>
public static class ThornsUpgradeDefinitions
{
	public static IReadOnlyList<ThornsSkillDefinition> All { get; } = BuildAll();

	static List<ThornsSkillDefinition> BuildAll()
	{
		var list = new List<ThornsSkillDefinition>
		{
			// SURVIVAL (Persistence)
			Skill( "hydration", "Hydration", ThornsSkillCategory.Persistence, 10, 2, 1, "",
				"Slower thirst drain; more thirst restored from liquids.",
				BonusCurve( 2, 20, "thirst efficiency" ) ),
			Skill( "iron_gut", "Iron Gut", ThornsSkillCategory.Persistence, 10, 2, 1, "hydration",
				"Slower hunger drain; more hunger restored from food.",
				BonusCurve( 2, 20, "hunger efficiency" ) ),
			Skill( "strong_stomach", "Strong Stomach", ThornsSkillCategory.Persistence, 10, 2, 2, "iron_gut",
				"Less poison from bad food and water.",
				BonusCurve( 3, 15, "poison resist" ) ),
			Skill( "weathered", "Weathered", ThornsSkillCategory.Persistence, 10, 2, 2, "strong_stomach",
				"Less environmental survival damage.",
				BonusCurve( 3, 12, "weather resist" ) ),
			Skill( "thick_hide", "Thick Hide", ThornsSkillCategory.Persistence, 10, 2, 3, "weathered",
				"Less damage from wildlife (up to ~45% at max rank).",
				BonusCurve( 4, 5, "wildlife damage reduction" ) ),

			// COMBAT (Instinct)
			Skill( "endurance", "Endurance", ThornsSkillCategory.Instinct, 10, 3, 1, "",
				"+8 max stamina per rank; slower sprint stamina drain.",
				BonusCurve( 8, 1, "max stamina" ) ),
			Skill( "ghost", "Ghost", ThornsSkillCategory.Instinct, 10, 3, 1, "endurance",
				"Wildlife detects you from shorter range.",
				BonusCurve( 4, 5, "harder to detect" ) ),
			Skill( "beastmaster", "Beastmaster", ThornsSkillCategory.Instinct, 10, 3, 2, "ghost",
				"Tame at higher HP (+5% per rank, cap 85%).",
				BonusCurve( 5, 1, "tame threshold" ) ),
			Skill( "hardened", "Hardened", ThornsSkillCategory.Instinct, 10, 3, 2, "beastmaster",
				"Less damage from bandit NPCs (up to ~45% at max).",
				BonusCurve( 4, 5, "bandit damage reduction" ) ),
			Skill( "lucky_chamber", "Lucky Chamber", ThornsSkillCategory.Instinct, 10, 3, 3, "hardened",
				"Chance to not consume ammo when firing (cap ~22%).",
				BonusCurve( 2, 2, "ammo save chance" ) ),

			// BUILDING (Industry)
			Skill( "lumberjack", "Lumberjack", ThornsSkillCategory.Industry, 10, 3, 1, "",
				"+10% wood harvest yield per rank.",
				BonusCurve( 10, 1, "wood yield" ) ),
			Skill( "miner", "Miner", ThornsSkillCategory.Industry, 10, 3, 1, "lumberjack",
				"+10% stone and ore harvest yield per rank.",
				BonusCurve( 10, 1, "stone/ore yield" ) ),
			Skill( "scavenger_skill", "Scavenger", ThornsSkillCategory.Industry, 10, 3, 2, "miner",
				"~5.5% chance per rank for bonus loot on first crate open.",
				BonusCurve( 5, 1, "bonus crate loot chance" ) ),
			Skill( "reinforced", "Reinforced", ThornsSkillCategory.Industry, 10, 3, 2, "scavenger_skill",
				"Weapons lose durability slower.",
				BonusCurve( 5, 2, "slower weapon wear" ) ),
			Skill( "technician", "Technician", ThornsSkillCategory.Industry, 12, 3, 3, "reinforced",
				"+1 crafting tier per rank (unlocks higher-tier recipes).",
				BonusCurve( 1, 1, "crafting tier" ) )
		};

		return list;
	}

	static List<string> BonusCurve( int perRank, int start, string suffix )
	{
		var list = new List<string>();
		for ( var r = 1; r <= 12; r++ )
			list.Add( $"+{start + (r - 1) * perRank}% {suffix}" );

		return list;
	}

	static ThornsSkillDefinition Skill( string id, string name, ThornsSkillCategory cat, int maxRank, int baseCost,
		int tier, string prereq, string desc, List<string> bonuses ) =>
		new()
		{
			Id = id,
			DisplayName = name,
			Description = desc,
			Category = cat,
			Tier = tier,
			MaxRank = maxRank,
			BasePointCost = baseCost,
			PrerequisiteSkillId = prereq,
			IconPath = ThornsIconRegistry.Skill( id ),
			RankBonuses = bonuses.Count > maxRank ? bonuses.Take( maxRank ).ToList() : bonuses
		};
}
