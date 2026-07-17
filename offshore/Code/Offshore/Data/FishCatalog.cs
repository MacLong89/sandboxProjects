namespace Offshore;

/// <summary>Built-in fish catalog for early locations. Expand per zone in later phases.</summary>
public static class FishCatalog
{
	private static List<FishDefinition> _all;

	public static IReadOnlyList<FishDefinition> All => _all ??= Build();

	public static FishDefinition Get( string id )
	{
		foreach ( var fish in All )
		{
			if ( string.Equals( fish.Id, id, StringComparison.OrdinalIgnoreCase ) )
				return fish;
		}

		return null;
	}

	public static IEnumerable<FishDefinition> ForLocation( string locationId )
	{
		foreach ( var fish in All )
		{
			if ( string.Equals( fish.RequiredLocationId, locationId, StringComparison.OrdinalIgnoreCase ) )
				yield return fish;
		}
	}

	private static List<FishDefinition> Build() =>
	[
		new FishDefinition
		{
			Id = "bluegill",
			DisplayName = "Bluegill",
			Description = "A common dock fish.",
			Rarity = FishRarity.Common,
			BaseValue = 4f,
			MinSize = 0.35f,
			MaxSize = 0.7f,
			MinWeight = 0.3f,
			MaxWeight = 0.9f,
			RequiredLocationId = "old_dock",
			MinDepth = 0.4f,
			MaxDepth = 8f,
			SpawnWeight = 3.2f,
			BiteSpeed = 1.15f,
			BiteCaution = 0.1f,
			Strength = 0.55f,
			Stamina = 0.5f,
			Speed = 0.8f,
			EscapeDifficulty = 0.15f,
			CapacityCost = 1f,
			SpritePath = OffshoreSprites.Paths.FishBluegill,
			Tags = ["surface", "panfish"]
		},
		new FishDefinition
		{
			Id = "perch",
			DisplayName = "Perch",
			Description = "Striped and eager near the pilings.",
			Rarity = FishRarity.Common,
			BaseValue = 6f,
			MinSize = 0.4f,
			MaxSize = 0.85f,
			MinWeight = 0.4f,
			MaxWeight = 1.2f,
			RequiredLocationId = "old_dock",
			MinDepth = 0.5f,
			MaxDepth = 10f,
			SpawnWeight = 2.6f,
			BiteSpeed = 1.05f,
			BiteCaution = 0.15f,
			Strength = 0.65f,
			Stamina = 0.6f,
			Speed = 0.9f,
			EscapeDifficulty = 0.2f,
			CapacityCost = 1f,
			SpritePath = OffshoreSprites.Paths.FishPerch,
			Tags = ["surface", "panfish"]
		},
		new FishDefinition
		{
			Id = "catfish",
			DisplayName = "Small Catfish",
			Description = "Bottom feeder with a stubborn pull.",
			Rarity = FishRarity.Common,
			BaseValue = 8f,
			MinSize = 0.5f,
			MaxSize = 1.0f,
			MinWeight = 0.6f,
			MaxWeight = 1.8f,
			RequiredLocationId = "old_dock",
			MinDepth = 2f,
			MaxDepth = 14f,
			SpawnWeight = 1.8f,
			BiteSpeed = 0.9f,
			BiteCaution = 0.2f,
			Strength = 0.85f,
			Stamina = 0.85f,
			Speed = 0.7f,
			EscapeDifficulty = 0.28f,
			CapacityCost = 1.2f,
			SpritePath = OffshoreSprites.Paths.FishBass,
			Tags = ["bottom", "nocturnal"]
		},
		new FishDefinition
		{
			Id = "bass",
			DisplayName = "Bass",
			Description = "A heavier dock visitor.",
			Rarity = FishRarity.Uncommon,
			BaseValue = 16f,
			MinSize = 0.7f,
			MaxSize = 1.3f,
			MinWeight = 1.0f,
			MaxWeight = 3.2f,
			RequiredLocationId = "old_dock",
			MinDepth = 1.5f,
			MaxDepth = 16f,
			SpawnWeight = 1.1f,
			BiteSpeed = 0.85f,
			BiteCaution = 0.35f,
			Strength = 1.15f,
			Stamina = 1.2f,
			Speed = 1.1f,
			EscapeDifficulty = 0.4f,
			CapacityCost = 1.5f,
			SpritePath = OffshoreSprites.Paths.FishLargemouthBass,
			Tags = ["predator", "ambush", "surface"]
		},
		new FishDefinition
		{
			Id = "trout",
			DisplayName = "Trout",
			Description = "Quick water with a feisty fight.",
			Rarity = FishRarity.Uncommon,
			BaseValue = 18f,
			MinSize = 0.6f,
			MaxSize = 1.2f,
			MinWeight = 0.8f,
			MaxWeight = 2.6f,
			RequiredLocationId = "old_dock",
			MinDepth = 1f,
			MaxDepth = 12f,
			SpawnWeight = 0.9f,
			BiteSpeed = 1.0f,
			BiteCaution = 0.3f,
			Strength = 1.0f,
			Stamina = 1.0f,
			Speed = 1.35f,
			EscapeDifficulty = 0.38f,
			CapacityCost = 1.4f,
			SpritePath = OffshoreSprites.Paths.FishTrout,
			Tags = ["surface", "predator"]
		},
		// Unique finds further from the old dock pier
		new FishDefinition
		{
			Id = "golden_perch",
			DisplayName = "Golden Perch",
			Description = "A bright stray that only shows past the pier tip.",
			Rarity = FishRarity.Uncommon,
			BaseValue = 32f,
			MinSize = 0.65f,
			MaxSize = 1.25f,
			MinWeight = 0.9f,
			MaxWeight = 2.8f,
			RequiredLocationId = "old_dock",
			MinDepth = 1.2f,
			MaxDepth = 14f,
			MinOffshore01 = 0.35f,
			SpawnWeight = 0.7f,
			BiteSpeed = 1.05f,
			BiteCaution = 0.32f,
			Strength = 1.05f,
			Stamina = 1.05f,
			Speed = 1.2f,
			EscapeDifficulty = 0.36f,
			CapacityCost = 1.5f,
			SpritePath = OffshoreSprites.Paths.FishPerch,
			Tags = ["surface", "panfish"]
		},
		new FishDefinition
		{
			Id = "silver_king",
			DisplayName = "Silver King",
			Description = "Rare offshore visitor off the old dock.",
			Rarity = FishRarity.Rare,
			BaseValue = 85f,
			MinSize = 1.0f,
			MaxSize = 1.9f,
			MinWeight = 2.0f,
			MaxWeight = 6.5f,
			RequiredLocationId = "old_dock",
			MinDepth = 3f,
			MaxDepth = 20f,
			MinOffshore01 = 0.62f,
			SpawnWeight = 0.4f,
			BiteSpeed = 0.8f,
			BiteCaution = 0.48f,
			Strength = 1.55f,
			Stamina = 1.45f,
			Speed = 1.4f,
			EscapeDifficulty = 0.52f,
			CapacityCost = 2.2f,
			SpritePath = OffshoreSprites.Paths.FishLargemouthBass,
			Tags = ["predator", "pelagic"]
		},
		// Quiet Bay
		new FishDefinition
		{
			Id = "carp", DisplayName = "Carp", Description = "Slow bay fish.", Rarity = FishRarity.Common,
			BaseValue = 10f, MinSize = 0.8f, MaxSize = 1.5f, MinWeight = 1.2f, MaxWeight = 4f,
			RequiredLocationId = "quiet_bay", MinDepth = 1f, MaxDepth = 14f, SpawnWeight = 2.4f,
			BiteSpeed = 0.9f, BiteCaution = 0.2f, Strength = 0.9f, Stamina = 1.0f, Speed = 0.7f,
			EscapeDifficulty = 0.3f, CapacityCost = 1.4f, SpritePath = OffshoreSprites.Paths.FishBass,
			Tags = ["bottom"]
		},
		new FishDefinition
		{
			Id = "pike", DisplayName = "Pike", Description = "Ambusher of the bay.", Rarity = FishRarity.Uncommon,
			BaseValue = 28f, MinSize = 0.9f, MaxSize = 1.8f, MinWeight = 1.5f, MaxWeight = 5f,
			RequiredLocationId = "quiet_bay", MinDepth = 2f, MaxDepth = 16f, SpawnWeight = 1.2f,
			BiteSpeed = 0.95f, BiteCaution = 0.35f, Strength = 1.3f, Stamina = 1.2f, Speed = 1.3f,
			EscapeDifficulty = 0.45f, CapacityCost = 1.7f, SpritePath = OffshoreSprites.Paths.FishTrout,
			Tags = ["predator", "ambush", "nocturnal"]
		},
		new FishDefinition
		{
			Id = "walleye", DisplayName = "Walleye", Description = "Bay favorite.", Rarity = FishRarity.Uncommon,
			BaseValue = 24f, MinSize = 0.7f, MaxSize = 1.4f, MinWeight = 1.0f, MaxWeight = 3.5f,
			RequiredLocationId = "quiet_bay", MinDepth = 1.5f, MaxDepth = 15f, SpawnWeight = 1.4f,
			BiteSpeed = 1.0f, BiteCaution = 0.3f, Strength = 1.1f, Stamina = 1.05f, Speed = 1.1f,
			EscapeDifficulty = 0.4f, CapacityCost = 1.5f, SpritePath = OffshoreSprites.Paths.FishPerch,
			Tags = ["predator", "nocturnal"]
		},
		new FishDefinition
		{
			Id = "bay_ghost", DisplayName = "Bay Ghost", Description = "Pale bay prize past the mooring.",
			Rarity = FishRarity.Rare, BaseValue = 70f, MinSize = 0.95f, MaxSize = 1.7f,
			MinWeight = 1.8f, MaxWeight = 5.5f, RequiredLocationId = "quiet_bay",
			MinDepth = 3f, MaxDepth = 18f, MinOffshore01 = 0.5f, SpawnWeight = 0.45f,
			BiteSpeed = 0.85f, BiteCaution = 0.45f, Strength = 1.4f, Stamina = 1.35f, Speed = 1.25f,
			EscapeDifficulty = 0.5f, CapacityCost = 2f, SpritePath = OffshoreSprites.Paths.FishTrout,
			Tags = ["ambush", "nocturnal", "predator"]
		},
		// Coastal
		new FishDefinition
		{
			Id = "redsnapper", DisplayName = "Red Snapper", Description = "Reef color.", Rarity = FishRarity.Rare,
			BaseValue = 55f, MinSize = 0.9f, MaxSize = 1.6f, MinWeight = 1.5f, MaxWeight = 5f,
			RequiredLocationId = "coastal", MinDepth = 3f, MaxDepth = 20f, SpawnWeight = 1.5f,
			BiteSpeed = 0.9f, BiteCaution = 0.4f, Strength = 1.4f, Stamina = 1.35f, Speed = 1.15f,
			EscapeDifficulty = 0.5f, CapacityCost = 2f, SpritePath = OffshoreSprites.Paths.FishRedSnapper,
			Tags = ["reef", "bottom"]
		},
		new FishDefinition
		{
			Id = "tuna", DisplayName = "Small Tuna", Description = "Speed in blue water.", Rarity = FishRarity.Rare,
			BaseValue = 70f, MinSize = 1.0f, MaxSize = 2.0f, MinWeight = 2f, MaxWeight = 8f,
			RequiredLocationId = "coastal", MinDepth = 4f, MaxDepth = 22f, SpawnWeight = 1.0f,
			BiteSpeed = 0.85f, BiteCaution = 0.45f, Strength = 1.6f, Stamina = 1.5f, Speed = 1.5f,
			EscapeDifficulty = 0.55f, CapacityCost = 2.4f, SpritePath = OffshoreSprites.Paths.FishTuna,
			Tags = ["pelagic", "predator"]
		},
		new FishDefinition
		{
			Id = "mahi", DisplayName = "Mahi Mahi", Description = "Bright coastal prize.", Rarity = FishRarity.Epic,
			BaseValue = 110f, MinSize = 1.1f, MaxSize = 2.2f, MinWeight = 2.5f, MaxWeight = 9f,
			RequiredLocationId = "coastal", MinDepth = 5f, MaxDepth = 24f, MinOffshore01 = 0.25f, SpawnWeight = 0.55f,
			BiteSpeed = 0.8f, BiteCaution = 0.5f, Strength = 1.7f, Stamina = 1.6f, Speed = 1.45f,
			EscapeDifficulty = 0.6f, CapacityCost = 2.6f, SpritePath = OffshoreSprites.Paths.FishTuna,
			Tags = ["pelagic", "surface"]
		},
		// Open ocean
		new FishDefinition
		{
			Id = "ocean_tuna", DisplayName = "Ocean Tuna", Description = "Open-water runner.", Rarity = FishRarity.Rare,
			BaseValue = 95f, MinSize = 1.2f, MaxSize = 2.4f, MinWeight = 3f, MaxWeight = 12f,
			RequiredLocationId = "open_ocean", MinDepth = 6f, MaxDepth = 28f, SpawnWeight = 1.2f,
			BiteSpeed = 0.8f, BiteCaution = 0.45f, Strength = 1.8f, Stamina = 1.7f, Speed = 1.6f,
			EscapeDifficulty = 0.62f, CapacityCost = 3f, SpritePath = OffshoreSprites.Paths.FishTuna,
			Tags = ["pelagic", "predator", "deep"]
		},
		new FishDefinition
		{
			Id = "swordfish_juv", DisplayName = "Juvenile Swordfish", Description = "Billfish apprentice.", Rarity = FishRarity.Epic,
			BaseValue = 160f, MinSize = 1.4f, MaxSize = 2.6f, MinWeight = 4f, MaxWeight = 14f,
			RequiredLocationId = "open_ocean", MinDepth = 8f, MaxDepth = 30f, MinOffshore01 = 0.35f, SpawnWeight = 0.5f,
			BiteSpeed = 0.75f, BiteCaution = 0.55f, Strength = 2.0f, Stamina = 1.9f, Speed = 1.7f,
			EscapeDifficulty = 0.7f, CapacityCost = 3.5f, SpritePath = OffshoreSprites.Paths.FishMarlin,
			Tags = ["pelagic", "deep", "predator"]
		},
		// Legendary waters
		new FishDefinition
		{
			Id = "marlin", DisplayName = "Blue Marlin", Description = "A legend of the deep.", Rarity = FishRarity.Legendary,
			BaseValue = 400f, MinSize = 2.0f, MaxSize = 3.5f, MinWeight = 8f, MaxWeight = 25f,
			RequiredLocationId = "legendary_waters", MinDepth = 10f, MaxDepth = 40f, MinOffshore01 = 0.3f, SpawnWeight = 0.35f,
			BiteSpeed = 0.65f, BiteCaution = 0.7f, Strength = 2.6f, Stamina = 2.5f, Speed = 1.8f,
			EscapeDifficulty = 0.85f, CapacityCost = 5f, SpritePath = OffshoreSprites.Paths.FishMarlin, IsLegendary = true,
			Tags = ["pelagic", "deep", "predator"]
		},
		new FishDefinition
		{
			Id = "great_white", DisplayName = "Great White", Description = "Apex encounter.", Rarity = FishRarity.Legendary,
			BaseValue = 550f, MinSize = 2.4f, MaxSize = 4.0f, MinWeight = 12f, MaxWeight = 40f,
			RequiredLocationId = "legendary_waters", MinDepth = 12f, MaxDepth = 45f, MinOffshore01 = 0.45f, SpawnWeight = 0.25f,
			BiteSpeed = 0.6f, BiteCaution = 0.75f, Strength = 3.0f, Stamina = 2.8f, Speed = 1.5f,
			EscapeDifficulty = 0.9f, CapacityCost = 6f, SpritePath = OffshoreSprites.Paths.FishMarlin, IsLegendary = true,
			Tags = ["predator", "deep", "nocturnal"]
		}
	];
}
