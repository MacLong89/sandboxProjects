namespace OffshoreFishing.Core;

/// <summary>Canonical game content. Portable and used by simulators/tests.</summary>
public static class ContentCatalog
{
	public static GameContent Create()
	{
		var c = new GameContent
		{
			Economy = new EconomyDef(),
			Tutorial = new TutorialDef()
		};

		c.Zones.AddRange( CreateZones() );
		c.Fish.AddRange( CreateFish() );
		c.Items.AddRange( CreateItems() );
		c.Boats.AddRange( CreateBoats() );
		c.HiredBoats.AddRange( CreateHired() );
		c.Objectives.AddRange( CreateObjectives() );

		foreach ( var zone in c.Zones )
			zone.FishIds = c.Fish.Where( f => f.ZoneId == zone.Id ).Select( f => f.Id ).ToArray();

		Validate( c );
		return c;
	}

	public static void Validate( GameContent c )
	{
		var ids = new HashSet<string>();
		void Add( string id, string kind )
		{
			if ( string.IsNullOrWhiteSpace( id ) ) throw new InvalidOperationException( $"{kind} missing id" );
			if ( !ids.Add( kind + ":" + id ) ) throw new InvalidOperationException( $"Duplicate {kind} id {id}" );
		}

		foreach ( var z in c.Zones ) Add( z.Id, "zone" );
		ids.Clear();
		foreach ( var f in c.Fish )
		{
			Add( f.Id, "fish" );
			if ( c.Zones.All( z => z.Id != f.ZoneId ) )
				throw new InvalidOperationException( $"Fish {f.Id} bad zone {f.ZoneId}" );
		}
		ids.Clear();
		foreach ( var i in c.Items ) Add( i.Id, "item" );
		ids.Clear();
		foreach ( var b in c.Boats ) Add( b.Id, "boat" );
	}

	private static List<ZoneDef> CreateZones() => new()
	{
		Z( "harbor", "Harbor", "Shallow piers and gentle water.", 0, 180, 18, 0, 0, 0, "bg_harbor" ),
		Z( "kelp", "Kelp Coast", "Green forests beneath the waves.", 180, 550, 17, 1, 0, 180, "bg_kelp" ),
		Z( "bluewater", "Bluewater", "Open pelagic hunting grounds.", 550, 1400, 16, 2, 1, 550, "bg_bluewater" ),
		Z( "shelf", "Continental Shelf", "Drop-offs and strong currents.", 1400, 3200, 12, 3, 2, 1400, "bg_shelf" ),
		Z( "trench", "Midnight Trench", "Where light forgets to follow.", 3200, 5000, 6, 4, 3, 3200, "bg_trench" ),
	};

	private static ZoneDef Z( string id, string name, string desc, float minD, float maxD, float temp, int order, int boatTier, float reqRange, string bg )
		=> new()
		{
			Id = id, Name = name, Description = desc, MinDistanceM = minD, MaxDistanceM = maxD,
			WaterTempC = temp, UnlockOrder = order, RequiredBoatTier = boatTier, RequiredRangeM = reqRange, BackgroundId = bg
		};

	private static List<FishDef> CreateFish()
	{
		var list = new List<FishDef>();

		// Harbor (6)
		list.Add( F( "harbor_minnow", "Harbor Minnow", "Tiny silver flash near the pilings.", "harbor", Rarity.Common, 8, 14, 0.05f, 0.2f, 12, 8f, 2, 12, 0.7f, 0.55f, 0.08f, 0.45f, 0, 0, "bait_worms" ) );
		list.Add( F( "harbor_flounder", "Sand Flounder", "Flat and patient on the bottom.", "harbor", Rarity.Common, 20, 40, 0.4f, 1.8f, 22, 6f, 4, 25, 0.8f, 0.7f, 0.12f, 0.55f, 0, 0, "bait_worms" ) );
		list.Add( F( "harbor_mackerel", "Dock Mackerel", "Striped speedster of the harbor.", "harbor", Rarity.Common, 25, 45, 0.5f, 1.6f, 28, 5f, 3, 30, 1.0f, 0.8f, 0.15f, 0.7f, 0, 0, "bait_minnows" ) );
		list.Add( F( "harbor_perch", "Piling Perch", "A chunky perch with attitude.", "harbor", Rarity.Uncommon, 22, 38, 0.6f, 2.0f, 32, 3f, 5, 28, 1.0f, 1.0f, 0.22f, 1.0f, 0, 0, "bait_worms" ) );
		list.Add( F( "harbor_ray", "Shallow Ray", "Glides like a kite over sand.", "harbor", Rarity.Uncommon, 40, 70, 2.0f, 6.0f, 48, 2f, 6, 35, 0.9f, 1.2f, 0.18f, 1.1f, 1, 0, "bait_squid" ) );
		list.Add( F( "harbor_seabass", "Harbor Seabass", "Local legend of the bait shop wall.", "harbor", Rarity.Rare, 45, 75, 2.5f, 7.5f, 90, 1f, 8, 40, 1.2f, 1.3f, 0.25f, 1.2f, 1, 1, "bait_minnows" ) );

		// Kelp (6)
		list.Add( F( "kelp_garibaldi", "Garibaldi", "Bright orange kelp guardian.", "kelp", Rarity.Common, 20, 35, 0.5f, 1.5f, 28, 6f, 5, 40, 0.9f, 0.9f, 0.15f, 0.8f, 1, 0, "bait_worms" ) );
		list.Add( F( "kelp_sheephead", "California Sheephead", "Thick jaws for crushing shells.", "kelp", Rarity.Common, 35, 60, 1.5f, 5.0f, 40, 5f, 8, 50, 1.0f, 1.1f, 0.2f, 1.0f, 1, 1, "bait_shrimp" ) );
		list.Add( F( "kelp_rockfish", "Kelp Rockfish", "Hides in the green cathedral.", "kelp", Rarity.Uncommon, 30, 55, 1.2f, 4.0f, 55, 4f, 10, 55, 1.1f, 1.15f, 0.22f, 1.05f, 1, 1, "bait_squid" ) );
		list.Add( F( "kelp_lingcod", "Lingcod", "Ambush predator with a mean face.", "kelp", Rarity.Uncommon, 50, 90, 4.0f, 12f, 85, 3f, 12, 70, 1.3f, 1.4f, 0.28f, 1.3f, 2, 1, "bait_minnows" ) );
		list.Add( F( "kelp_halibut", "Coastal Halibut", "A door of a fish on the sand.", "kelp", Rarity.Rare, 70, 120, 8f, 25f, 140, 1.5f, 15, 80, 1.0f, 1.6f, 0.2f, 1.4f, 2, 2, "bait_squid" ) );
		list.Add( F( "kelp_wolf", "Wolf Eel", "Looks fearsome, fights harder.", "kelp", Rarity.Rare, 80, 140, 6f, 18f, 160, 1f, 20, 85, 1.4f, 1.7f, 0.3f, 1.5f, 2, 2, "bait_shrimp" ) );

		// Bluewater (6)
		list.Add( F( "blue_bonito", "Ocean Bonito", "Chrome bullet under open sky.", "bluewater", Rarity.Common, 40, 70, 2f, 6f, 55, 6f, 10, 60, 1.4f, 1.1f, 0.25f, 1.1f, 2, 1, "bait_minnows" ) );
		list.Add( F( "blue_mahi", "Mahi Mahi", "Green-gold acrobat of the blue.", "bluewater", Rarity.Uncommon, 60, 110, 4f, 14f, 85, 4f, 8, 70, 1.5f, 1.3f, 0.3f, 1.3f, 2, 2, "bait_shrimp" ) );
		list.Add( F( "blue_yellowfin", "Yellowfin Tuna", "Fast and powerful pelagic prize.", "bluewater", Rarity.Rare, 90, 160, 15f, 55f, 140, 2.5f, 20, 120, 1.6f, 1.8f, 0.35f, 1.6f, 3, 2, "bait_squid" ) );
		list.Add( F( "blue_wahoo", "Wahoo", "Toothy streak of the current.", "bluewater", Rarity.Rare, 80, 150, 10f, 35f, 130, 2f, 15, 100, 1.8f, 1.7f, 0.4f, 1.7f, 3, 2, "bait_minnows" ) );
		list.Add( F( "blue_marlin", "Striped Marlin", "A billfish for the bold.", "bluewater", Rarity.Epic, 140, 240, 40f, 120f, 260, 1f, 25, 160, 1.9f, 2.2f, 0.45f, 2.0f, 3, 3, "bait_premium" ) );
		list.Add( F( "blue_sunfish", "Ocean Sunfish", "Impossible disc of the open sea.", "bluewater", Rarity.Epic, 120, 220, 50f, 200f, 240, 0.8f, 10, 80, 0.7f, 2.0f, 0.15f, 1.4f, 3, 2, "bait_jellyfish" ) );

		// Shelf (6)
		list.Add( F( "shelf_cod", "Deep Cod", "Cold-water classic.", "shelf", Rarity.Common, 50, 90, 3f, 12f, 70, 5f, 30, 120, 1.0f, 1.2f, 0.2f, 1.1f, 3, 2, "bait_squid" ) );
		list.Add( F( "shelf_grouper", "Shelf Grouper", "Heavy shoulders, heavier fight.", "shelf", Rarity.Uncommon, 70, 130, 8f, 30f, 110, 3.5f, 40, 160, 1.2f, 1.6f, 0.28f, 1.5f, 3, 3, "bait_shrimp" ) );
		list.Add( F( "shelf_swordfish", "Swordfish", "Night hunter of the shelf edge.", "shelf", Rarity.Rare, 150, 280, 50f, 180f, 280, 1.8f, 50, 220, 1.7f, 2.1f, 0.4f, 1.9f, 4, 3, "bait_squid" ) );
		list.Add( F( "shelf_oilfish", "Oilfish", "Slick and strange from deep rock.", "shelf", Rarity.Uncommon, 80, 140, 10f, 35f, 120, 3f, 60, 200, 1.1f, 1.5f, 0.22f, 1.3f, 3, 3, "bait_jellyfish" ) );
		list.Add( F( "shelf_crabking", "King Crab", "Not a fish, still worth the haul.", "shelf", Rarity.Rare, 40, 80, 5f, 14f, 160, 2f, 70, 180, 0.6f, 1.4f, 0.1f, 1.2f, 3, 2, "bait_premium" ) );
		list.Add( F( "shelf_sleeper", "Pacific Sleeper", "A dark shark of cold water.", "shelf", Rarity.Epic, 180, 320, 80f, 300f, 360, 1f, 80, 250, 1.3f, 2.4f, 0.35f, 2.1f, 4, 4, "bait_premium" ) );

		// Trench (6)
		list.Add( F( "trench_hatchetfish", "Hatchetfish", "Tiny lanterns in endless dark.", "trench", Rarity.Common, 6, 12, 0.02f, 0.1f, 80, 5f, 120, 220, 0.8f, 1.0f, 0.15f, 1.0f, 4, 3, "bait_jellyfish" ) );
		list.Add( F( "trench_viper", "Viperfish", "Needles and night.", "trench", Rarity.Uncommon, 25, 50, 0.4f, 1.5f, 130, 3.5f, 150, 280, 1.4f, 1.6f, 0.35f, 1.6f, 4, 3, "bait_squid" ) );
		list.Add( F( "trench_gulper", "Gulper Eel", "Mostly mouth.", "trench", Rarity.Rare, 60, 120, 2f, 8f, 200, 2.5f, 180, 320, 1.2f, 1.9f, 0.3f, 1.7f, 4, 4, "bait_premium" ) );
		list.Add( F( "trench_angler", "Abyss Angler", "A lure for the curious.", "trench", Rarity.Rare, 30, 70, 1f, 5f, 220, 2f, 200, 360, 1.1f, 2.0f, 0.28f, 1.8f, 4, 4, "bait_jellyfish" ) );
		list.Add( F( "trench_phantom", "Phantom Coelacanth", "A living fossil.", "trench", Rarity.Epic, 100, 180, 20f, 70f, 420, 1.2f, 160, 300, 1.0f, 2.3f, 0.25f, 2.0f, 4, 4, "bait_premium" ) );
		list.Add( F( "trench_leviathan", "Trench Leviathan", "You should not have come this far.", "trench", Rarity.Legendary, 300, 600, 400f, 2000f, 750, 0.35f, 220, 420, 2.2f, 3.0f, 0.55f, 2.6f, 4, 4, "bait_premium" ) );

		return list;
	}

	private static FishDef F( string id, string name, string desc, string zone, Rarity rarity,
		float minCm, float maxCm, float minKg, float maxKg, int value, float weight,
		float minD, float maxD, float fightSpd, float stamina, float surge, float escape,
		int rodTier, int hookTier, params string[] bait )
		=> new()
		{
			Id = id, Name = name, Description = desc, ZoneId = zone, Rarity = rarity,
			MinCm = minCm, MaxCm = maxCm, MinKg = minKg, MaxKg = maxKg, BaseValue = value,
			SpawnWeight = weight, MinDepth = minD, MaxDepth = maxD,
			FightSpeed = fightSpd, FightStamina = stamina, SurgeChance = surge, EscapePressure = escape,
			RequiredRodTier = rodTier, RequiredHookTier = hookTier, PreferredBait = bait, SpriteId = id
		};

	private static List<ItemDef> CreateItems() => new()
	{
		I( "rod_starter", "Starter Rod", "A reliable hand-me-down.", ItemCategory.Rod, 0, 0, 1.0f, 0, 0, 0, 0, 0 ),
		I( "rod_fiberglass", "Fiberglass Rod", "Bends but won't break.", ItemCategory.Rod, 1, 120, 1.25f, 0, 0, 0, 0, 0, "rod_starter" ),
		I( "rod_carbon", "Carbon Rod", "Light, strong, eager.", ItemCategory.Rod, 2, 420, 1.55f, 0, 0, 0.05f, 0, 0, "rod_fiberglass" ),
		I( "rod_titanium", "Titanium Rod", "Built for monsters.", ItemCategory.Rod, 3, 1400, 1.9f, 0, 0, 0.1f, 0, 0, "rod_carbon" ),
		I( "rod_abyss", "Abyss Rod", "Forged for the trench.", ItemCategory.Rod, 4, 4200, 2.3f, 0, 0, 0.15f, 0.05f, 0, "rod_titanium" ),

		I( "spool_basic", "Basic Spool", "Short and sweet.", ItemCategory.Spool, 0, 0, 0, 1.0f, 1.0f, 0, 0, 0 ),
		I( "spool_braided", "Braided Spool", "More line, less snap.", ItemCategory.Spool, 1, 40, 0, 1.35f, 1.15f, 0, 0, 0, "spool_basic" ),
		I( "spool_deep", "Deep Sea Spool", "Holds the long fight.", ItemCategory.Spool, 2, 280, 0, 1.7f, 1.35f, 0, 0, 0, "spool_braided" ),
		I( "spool_spectra", "Spectra Spool", "Whispers under tension.", ItemCategory.Spool, 3, 900, 0, 2.1f, 1.6f, 0, 0, 0, "spool_deep" ),
		I( "spool_void", "Void Spool", "Line that loves the dark.", ItemCategory.Spool, 4, 2800, 0, 2.6f, 1.9f, 0, 0, 0, "spool_spectra" ),

		I( "hook_basic", "Basic Hook", "Pointy enough.", ItemCategory.Hook, 0, 0, 0, 0, 0, 1.0f, 0, 0 ),
		I( "hook_better", "Better Hooks", "Sets cleaner.", ItemCategory.Hook, 1, 60, 0, 0, 0, 1.3f, 0, 0.05f, "hook_basic" ),
		I( "hook_barbed", "Barbed Hooks", "Harder to shake.", ItemCategory.Hook, 2, 240, 0, 0, 0, 1.65f, 0, 0.08f, "hook_better" ),
		I( "hook_circle", "Circle Hooks", "Tournament favorite.", ItemCategory.Hook, 3, 800, 0, 0, 0, 2.0f, 0, 0.12f, "hook_barbed" ),
		I( "hook_abyss", "Abyss Hooks", "Bite steel.", ItemCategory.Hook, 4, 2400, 0, 0, 0, 2.5f, 0, 0.18f, "hook_circle" ),

		Bait( "bait_worms", "Worms", "Classic and wriggly.", 0, 15, 0.05f, 0f ),
		Bait( "bait_minnows", "Minnows", "Flash that predators notice.", 1, 35, 0.08f, 0.03f ),
		Bait( "bait_squid", "Squid", "Deep-water invitation.", 2, 70, 0.1f, 0.06f ),
		Bait( "bait_shrimp", "Shrimp", "Sweet scent in the kelp.", 2, 70, 0.12f, 0.05f ),
		Bait( "bait_jellyfish", "Jelly Strips", "Strange glow bait.", 3, 160, 0.08f, 0.1f ),
		Bait( "bait_premium", "Premium Bait", "Whatever they want.", 4, 320, 0.18f, 0.15f ),
	};

	private static ItemDef I( string id, string name, string desc, ItemCategory cat, int tier, int price,
		float cast, float line, float reel, float hook, float value, float rarity, string unlock = null )
		=> new()
		{
			Id = id, Name = name, Description = desc, Category = cat, Tier = tier, Price = price,
			CastPower = cast, LineStrength = line, ReelSpeed = reel, HookPower = hook,
			ValueBonus = value, RarityBonus = rarity, UnlockAfterItemId = unlock, IconId = id,
			Consumable = false, StackLimit = 1
		};

	private static ItemDef Bait( string id, string name, string desc, int tier, int price, float value, float rarity )
		=> new()
		{
			Id = id, Name = name, Description = desc, Category = ItemCategory.Bait, Tier = tier, Price = price,
			ValueBonus = value, RarityBonus = rarity, BiteBonus = 0.05f + tier * 0.02f,
			Consumable = true, StackLimit = 99, IconId = id
		};

	private static List<BoatDef> CreateBoats() => new()
	{
		new() { Id = "boat_skiff", Name = "Small Skiff", Description = "Just enough boat.", Tier = 0, Price = 0, Speed = 1.35f, MaxDepthM = 100, MaxRangeM = 260, GasCapacityL = 30, StorageSlots = 10, SpriteId = "boat_skiff" },
		new() { Id = "boat_fisher", Name = "Fisherman", Description = "A proper day boat.", Tier = 1, Price = 900, Speed = 1.55f, MaxDepthM = 180, MaxRangeM = 800, GasCapacityL = 60, StorageSlots = 16, SpriteId = "boat_fisher", UnlockAfterBoatId = "boat_skiff" },
		new() { Id = "boat_explorer", Name = "Explorer", Description = "Built for longer trips.", Tier = 2, Price = 2800, Speed = 1.75f, MaxDepthM = 280, MaxRangeM = 2000, GasCapacityL = 100, StorageSlots = 24, SpriteId = "boat_explorer", UnlockAfterBoatId = "boat_fisher" },
		new() { Id = "boat_oceanic", Name = "Oceanic", Description = "Go until the map ends.", Tier = 3, Price = 7000, Speed = 2.0f, MaxDepthM = 450, MaxRangeM = 5200, GasCapacityL = 160, StorageSlots = 36, SpriteId = "boat_oceanic", UnlockAfterBoatId = "boat_explorer" },
	};

	private static List<HiredBoatDef> CreateHired() => new()
	{
		new() { Id = "crew_second", Name = "Second Boat Crew", Description = "A friendly captain who knows the kelp.", Price = 2500, TripMinutes = 4.5f, GoldPerTripMin = 18, GoldPerTripMax = 32, RequiredBoatId = "boat_fisher", UnlockOrder = 1 },
		new() { Id = "crew_third", Name = "Third Boat Crew", Description = "Night shift specialists.", Price = 6500, TripMinutes = 4f, GoldPerTripMin = 40, GoldPerTripMax = 65, RequiredBoatId = "boat_explorer", UnlockOrder = 2 },
	};

	private static List<ObjectiveDef> CreateObjectives() => new()
	{
		new() { Id = "obj_first_catch", Title = "First Catch", Description = "Land any fish.", Type = ObjectiveType.CatchCount, TargetCount = 1, RewardGold = 15, SortOrder = 0, Tutorial = true },
		new() { Id = "obj_sell", Title = "Make a Sale", Description = "Sell your catch at the shop.", Type = ObjectiveType.EarnGold, TargetCount = 10, RewardGold = 10, SortOrder = 1, Tutorial = true },
		new() { Id = "obj_spool", Title = "Better Line", Description = "Buy the Braided Spool.", Type = ObjectiveType.BuyItem, TargetId = "spool_braided", TargetCount = 1, RewardGold = 20, SortOrder = 2, Tutorial = true },
		new() { Id = "obj_two_fish", Title = "Fill the Bucket", Description = "Catch 2 more fish.", Type = ObjectiveType.CatchCount, TargetCount = 2, RewardGold = 25, SortOrder = 3, Tutorial = true },
		new() { Id = "obj_kelp", Title = "Reach Kelp Coast", Description = "Travel 180m offshore.", Type = ObjectiveType.ReachDistance, TargetCount = 180, RewardGold = 40, UnlockZoneId = "kelp", SortOrder = 4 },
		new() { Id = "obj_boat1", Title = "Upgrade the Boat", Description = "Buy the Fisherman boat.", Type = ObjectiveType.BuyItem, TargetId = "boat_fisher", TargetCount = 1, RewardGold = 80, SortOrder = 5 },
		new() { Id = "obj_blue", Title = "Bluewater Bound", Description = "Travel 550m offshore.", Type = ObjectiveType.ReachDistance, TargetCount = 550, RewardGold = 100, UnlockZoneId = "bluewater", SortOrder = 6 },
		new() { Id = "obj_tuna", Title = "Tuna Time", Description = "Catch a Yellowfin Tuna.", Type = ObjectiveType.CatchSpecies, TargetId = "blue_yellowfin", TargetCount = 1, RewardGold = 150, SortOrder = 7 },
		new() { Id = "obj_hire", Title = "Hire Help", Description = "Hire a second boat crew.", Type = ObjectiveType.HireBoat, TargetId = "crew_second", TargetCount = 1, RewardGold = 200, SortOrder = 8 },
		new() { Id = "obj_shelf", Title = "Shelf Edge", Description = "Travel 1400m offshore.", Type = ObjectiveType.ReachDistance, TargetCount = 1400, RewardGold = 250, UnlockZoneId = "shelf", SortOrder = 9 },
		new() { Id = "obj_oceanic", Title = "Oceanic Class", Description = "Buy the Oceanic boat.", Type = ObjectiveType.BuyItem, TargetId = "boat_oceanic", TargetCount = 1, RewardGold = 400, SortOrder = 10 },
		new() { Id = "obj_trench", Title = "Into the Dark", Description = "Travel 3200m offshore.", Type = ObjectiveType.ReachDistance, TargetCount = 3200, RewardGold = 600, UnlockZoneId = "trench", SortOrder = 11 },
		new() { Id = "obj_leviathan", Title = "Leviathan", Description = "Catch the Trench Leviathan.", Type = ObjectiveType.CatchSpecies, TargetId = "trench_leviathan", TargetCount = 1, RewardGold = 2000, SortOrder = 12 },
	};
}
