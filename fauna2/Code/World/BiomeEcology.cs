namespace Fauna2;

/// <summary>Real-world-inspired biome adjacency and which flora/fauna belong where.</summary>
public static class BiomeEcology
{
	private static readonly Biome[] ForestNeighbors = [Biome.Rainforest, Biome.Grassland, Biome.Alpine, Biome.Swamp];
	private static readonly Biome[] RainforestNeighbors = [Biome.Forest, Biome.Swamp, Biome.Grassland];
	private static readonly Biome[] GrasslandNeighbors = [Biome.Forest, Biome.Rainforest, Biome.Desert, Biome.Coastal, Biome.Swamp];
	private static readonly Biome[] DesertNeighbors = [Biome.Grassland, Biome.Alpine];
	private static readonly Biome[] ArcticNeighbors = [Biome.Alpine];
	private static readonly Biome[] AlpineNeighbors = [Biome.Arctic, Biome.Forest, Biome.Grassland, Biome.Desert];
	private static readonly Biome[] SwampNeighbors = [Biome.Rainforest, Biome.Forest, Biome.Grassland, Biome.Coastal];
	private static readonly Biome[] CoastalNeighbors = [Biome.Grassland, Biome.Swamp, Biome.Rainforest];

	public static bool CanWildSpawn( Biome regional, Biome speciesHome )
	{
		if ( regional == speciesHome )
			return true;

		return IsAdjacent( regional, speciesHome );
	}

	/// <summary>Whether a wild animal can stand/roam at this world position.</summary>
	public static bool CanWildAnimalAt( AnimalDefinition def, Vector3 position, Biome starterBiome )
	{
		if ( def is null )
			return false;

		var regional = WildernessBiomeMap.BiomeAtWorld( position, starterBiome );
		if ( !CanWildSpawn( regional, def.Biome ) )
			return false;

		var inWater = WildernessBiomeMap.IsWaterAt( position, starterBiome );
		return def.Locomotion switch
		{
			AnimalLocomotion.Marine => inWater,
			AnimalLocomotion.Swimmer => inWater || CanSwimmerHaulOut( regional, inWater ),
			AnimalLocomotion.Bird => true,
			_ => !inWater,
		};
	}

	/// <summary>Scatter props never belong on open water.</summary>
	public static bool CanScatterPropAt( Vector3 position, Biome starterBiome, string prop ) =>
		!string.IsNullOrEmpty( prop ) && WildernessBiomeMap.IsDryLandAt( position, starterBiome );

	private static bool CanSwimmerHaulOut( Biome regional, bool inWater )
	{
		if ( inWater )
			return true;

		return regional is Biome.Swamp or Biome.Coastal or Biome.Arctic or Biome.Rainforest;
	}

	public static bool IsAdjacent( Biome a, Biome b )
	{
		if ( a == b ) return true;

		foreach ( var neighbor in NeighborsOf( a ) )
		{
			if ( neighbor == b )
				return true;
		}

		return false;
	}

	public static IReadOnlyList<Biome> NeighborsOf( Biome biome ) => biome switch
	{
		Biome.Forest => ForestNeighbors,
		Biome.Rainforest => RainforestNeighbors,
		Biome.Grassland => GrasslandNeighbors,
		Biome.Desert => DesertNeighbors,
		Biome.Arctic => ArcticNeighbors,
		Biome.Alpine => AlpineNeighbors,
		Biome.Swamp => SwampNeighbors,
		Biome.Coastal => CoastalNeighbors,
		_ => [],
	};

	public static bool AllowsTrees( Biome biome ) => biome switch
	{
		Biome.Desert or Biome.Coastal => false,
		_ => true,
	};

	public static bool AllowsBushes( Biome biome ) => biome switch
	{
		Biome.Arctic => false,
		_ => true,
	};

	public static bool AllowsRocks( Biome biome ) => biome is
		Biome.Desert or Biome.Alpine or Biome.Arctic or Biome.Coastal or Biome.Grassland
		or Biome.Swamp or Biome.Forest or Biome.Rainforest;

	public static bool AllowsCactus( Biome biome ) => biome == Biome.Desert;

	public static float ObstacleDensityMultiplier( Biome biome ) => biome switch
	{
		Biome.Desert => 0.42f,
		Biome.Coastal => 0.55f,
		Biome.Grassland => 0.72f,
		Biome.Arctic => 0.48f,
		Biome.Alpine => 0.58f,
		Biome.Swamp => 0.88f,
		Biome.Forest => 1.05f,
		Biome.Rainforest => 1.12f,
		_ => 0.8f,
	};

	public static TerrainObstacleType? PickObstacleType( Biome biome, Random rng )
	{
		if ( !AllowsTrees( biome ) && !AllowsRocks( biome ) )
			return null;

		if ( !AllowsTrees( biome ) )
			return AllowsRocks( biome ) ? TerrainObstacleType.Rock : null;

		var rockChance = biome switch
		{
			Biome.Arctic or Biome.Alpine or Biome.Desert => 0.62f,
			Biome.Swamp or Biome.Coastal => 0.2f,
			Biome.Grassland => 0.28f,
			Biome.Forest => 0.12f,
			Biome.Rainforest => 0.08f,
			_ => 0.22f,
		};

		return rng.NextSingle() < rockChance ? TerrainObstacleType.Rock : TerrainObstacleType.Tree;
	}

	public static string PickTreeProp( Biome biome, Random rng ) => biome switch
	{
		Biome.Arctic or Biome.Alpine => "pine_tree",
		Biome.Forest => rng.NextSingle() < 0.5f ? "pine_tree" : "aspen_tree",
		Biome.Rainforest => rng.NextSingle() < 0.65f ? "oak_tree" : "aspen_tree",
		Biome.Swamp => "oak_tree",
		Biome.Grassland => rng.NextSingle() < 0.72f ? "oak_tree" : "aspen_tree",
		_ => "oak_tree",
	};

	public static float TreeScatterChance( Biome biome, float treeDensity )
	{
		if ( !AllowsTrees( biome ) )
			return 0f;

		return biome switch
		{
			Biome.Grassland => 0.04f + treeDensity * 0.14f,
			Biome.Arctic => 0.03f + treeDensity * 0.12f,
			Biome.Alpine => 0.05f + treeDensity * 0.18f,
			Biome.Swamp => 0.06f + treeDensity * 0.22f,
			Biome.Coastal => 0f,
			Biome.Forest => 0.05f + treeDensity * 0.46f,
			Biome.Rainforest => 0.08f + treeDensity * 0.5f,
			_ => 0.04f + treeDensity * 0.24f,
		};
	}

	public static float BushScatterChance( Biome biome, float treeDensity )
	{
		if ( !AllowsBushes( biome ) )
			return 0f;

		return biome switch
		{
			Biome.Desert => 0f,
			Biome.Coastal => 0.1f,
			Biome.Grassland => 0.14f,
			Biome.Swamp => 0.16f,
			Biome.Forest => 0.1f + (1f - treeDensity) * 0.14f,
			Biome.Rainforest => 0.12f + (1f - treeDensity) * 0.1f,
			Biome.Arctic or Biome.Alpine => 0.03f,
			_ => 0.08f,
		};
	}

	public static float CactusScatterChance( Biome biome ) =>
		AllowsCactus( biome ) ? 0.07f : 0f;

	public static float RockScatterChance( Biome biome ) => biome switch
	{
		Biome.Desert => 0.12f,
		Biome.Alpine or Biome.Arctic => 0.1f,
		Biome.Coastal => 0.08f,
		_ => 0f,
	};

	public static string PickScatterProp( Biome biome, Random rng, float roll, float treeDensity )
	{
		var cactusChance = CactusScatterChance( biome );
		if ( roll < cactusChance )
			return "cactus";

		var treeChance = TreeScatterChance( biome, treeDensity );
		if ( roll < cactusChance + treeChance )
			return PickTreeProp( biome, rng );

		var bushChance = BushScatterChance( biome, treeDensity );
		if ( roll < cactusChance + treeChance + bushChance )
			return "bush";

		var rockChance = RockScatterChance( biome );
		if ( roll < cactusChance + treeChance + bushChance + rockChance )
			return "rock";

		return null;
	}
}
