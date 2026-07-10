namespace Terraingen.Victory;

using Terraingen.GameData;

/// <summary>Data-driven victory path definitions. Add paths here without touching UI or manager logic.</summary>
public static class ThornsVictoryPathCatalog
{
	public const float BloomSeedPurificationPathFraction = 0.05f;

	static readonly List<ThornsVictoryPathDefinition> Paths = BuildAll();
	static readonly Dictionary<string, ThornsVictoryPathDefinition> ById = Paths.ToDictionary( p => p.PathId, StringComparer.OrdinalIgnoreCase );

	public static IReadOnlyList<ThornsVictoryPathDefinition> All => Paths;

	public static bool TryGet( string pathId, out ThornsVictoryPathDefinition def )
	{
		if ( string.IsNullOrWhiteSpace( pathId ) )
		{
			def = null;
			return false;
		}

		return ById.TryGetValue( pathId, out def );
	}

	public static IEnumerable<(string PathId, int Points)> ResolveSources( string sourceKey )
	{
		if ( string.IsNullOrWhiteSpace( sourceKey ) )
			yield break;

		foreach ( var path in Paths )
		{
			if ( path.SourceWeights.TryGetValue( sourceKey, out var points ) && points > 0 )
				yield return (path.PathId, points);
		}
	}

	/// <summary>Progress awarded when a Bloom Seed is purified — always 5% of the Purification path cap.</summary>
	public static int BloomSeedPurificationPoints =>
		TryGet( ThornsVictoryPathIds.Purification, out var def )
			? ResolvePathSourcePoints( def.TargetProgress, BloomSeedPurificationPathFraction )
			: ResolvePathSourcePoints( 10_000, BloomSeedPurificationPathFraction );

	public static int ResolvePathSourcePoints( long targetProgress, float pathFraction ) =>
		Math.Max( 1, (int)MathF.Round( Math.Max( 1L, targetProgress ) * pathFraction ) );

	static List<ThornsVictoryPathDefinition> BuildAll()
	{
		return new List<ThornsVictoryPathDefinition>
		{
			BuildDominion(),
			BuildAscension(),
			BuildPurification(),
			BuildApex()
		};
	}

	static ThornsVictoryPathDefinition BuildDominion() => new()
	{
		PathId = ThornsVictoryPathIds.Dominion,
		DisplayName = "Dominion",
		Summary = "Control the wasteland through territory, settlements, guild power, and conquest.",
		IconPath = ThornsIconRegistry.Victory( ThornsVictoryPathIds.Dominion ),
		TargetProgress = 10_000,
		SourceWeights = SourceMap(
			("territory_controlled", 120),
			("settlement_controlled", 200),
			("guild_structure", 40),
			("pvp_victory", 25),
			("bandit_camp_cleared", 150),
			("npc_guild_destroyed", 500),
			("npc_outpost_destroyed", 50) ),
		Milestones = Milestones(
			(1000, "Outpost Claimed", "Guild banner & title prefix"),
			(2500, "Wasteland Hold", "Territory map overlay"),
			(5000, "Regional Power", "Dominion statue blueprint"),
			(7500, "Iron Rule", "War drum emote"),
			(10_000, "Wasteland Sovereign", "Server recognition plaque" ) )
	};

	static ThornsVictoryPathDefinition BuildAscension() => new()
	{
		PathId = ThornsVictoryPathIds.Ascension,
		DisplayName = "Ascension",
		Summary = "Advance civilization through technology, crafting mastery, and ancient knowledge.",
		IconPath = ThornsIconRegistry.Victory( ThornsVictoryPathIds.Ascension ),
		TargetProgress = 10_000,
		SourceWeights = SourceMap(
			("tech_unlock", 80),
			("research_level_completed", 333),
			("research_capstone_completed", 10),
			("advanced_craft", 35),
			("rare_discovery", 60),
			("ancient_structure", 250),
			("endgame_project", 400) ),
		Milestones = Milestones(
			(1000, "First Breakthrough", "Research journal skin"),
			(2500, "Industrial Spark", "Advanced workbench tint"),
			(5000, "Ancient Insight", "Ascension beacon craft"),
			(7500, "Endgame Forge", "Master artisan title"),
			(10_000, "Age of Wonders", "Server ascension monument" ) )
	};

	static ThornsVictoryPathDefinition BuildPurification()
	{
		const long targetProgress = 10_000;
		var bloomSeedPoints = ResolvePathSourcePoints( targetProgress, BloomSeedPurificationPathFraction );

		return new ThornsVictoryPathDefinition
		{
			PathId = ThornsVictoryPathIds.Purification,
			DisplayName = "Purification",
			Summary = "Push back the Bloom — cleanse hosts, zones, and corrupted life.",
			IconPath = ThornsIconRegistry.Victory( ThornsVictoryPathIds.Purification ),
			TargetProgress = targetProgress,
			SourceWeights = SourceMap(
				("bloom_host_destroyed", 300),
				("bloom_zone_cleared", 500),
				("bloom_seed_purified", bloomSeedPoints),
				("world_event_completed", 120),
				("corrupted_creature", 15),
				("bloom_apex_slay", 800) ),
			Milestones = Milestones(
				(1000, "First Cleansing", "Purifier cloak tint"),
				(2500, "Zone Stabilized", "Bloom ward consumable"),
				(5000, "Host Breaker", "Purification banner"),
				(7500, "Bloom's Bane", "Legendary purifier title"),
				(10_000, "World Renewed", "Server purification memorial" ) )
		};
	}

	static ThornsVictoryPathDefinition BuildApex() => new()
	{
		PathId = ThornsVictoryPathIds.Apex,
		DisplayName = "Apex",
		Summary = "Master the ecosystem — tame, breed, and discover the rarest creatures.",
		IconPath = ThornsIconRegistry.Victory( ThornsVictoryPathIds.Apex ),
		TargetProgress = 10_000,
		SourceWeights = SourceMap(
			("animal_tamed", 50),
			("rare_animal_tamed", 150),
			("legendary_animal_tamed", 400),
			("breeding_milestone", 200),
			("apex_breed_tier_1", 100),
			("apex_breed_tier_2", 250),
			("apex_breed_tier_3", 500),
			("apex_breed_tier_4", 900),
			("apex_breed_tier_5", 1500),
			("apex_crossbreed_2", 500),
			("apex_crossbreed_3", 1200),
			("apex_crossbreed_4", 2500),
			("apex_mutation", 500),
			("apex_super_crossbreed", 10000),
			("animal_discovery", 40) ),
		Milestones = Milestones(
			(1000, "Wild Bond", "Apex tracker map pin"),
			(2500, "Rare Kin", "Breeding pen upgrade"),
			(5000, "Legendary Keeper", "Apex saddle tint"),
			(7500, "Ecosystem Sage", "Master tamer title"),
			(10_000, "Apex Sovereign", "Server beast monument" ) )
	};

	static Dictionary<string, int> SourceMap( params (string Key, int Points)[] entries )
	{
		var map = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var (key, points) in entries )
			map[key] = points;
		return map;
	}

	static List<ThornsVictoryMilestoneDefinition> Milestones( params (long Threshold, string Title, string Reward)[] rows )
	{
		var list = new List<ThornsVictoryMilestoneDefinition>();
		for ( var i = 0; i < rows.Length; i++ )
		{
			var row = rows[i];
			list.Add( new ThornsVictoryMilestoneDefinition
			{
				MilestoneId = $"m{i + 1}",
				Title = row.Title,
				Threshold = row.Threshold,
				RewardPreview = row.Reward
			} );
		}

		return list;
	}
}
