namespace PawnShop;

/// <summary>Static definition of an item type. Instances add condition/authenticity/etc.</summary>
public sealed class ItemDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	/// <summary>Fictional brand / maker.</summary>
	public string Brand { get; init; } = "";
	public ItemCategory Category { get; init; }
	/// <summary>Market value at Good condition, genuine, common rarity.</summary>
	public int BaseValue { get; init; }
	/// <summary>Chance a spawned instance is counterfeit (before archetype modifiers).</summary>
	public float CounterfeitChance { get; init; }
	/// <summary>Highest rarity this item can roll.</summary>
	public Rarity MaxRarity { get; init; } = Rarity.Uncommon;
	/// <summary>Pool of defect ids this item can spawn with.</summary>
	public string[] DefectPool { get; init; } = Array.Empty<string>();
	/// <summary>Material icon shown in UI.</summary>
	public string Icon { get; init; } = "inventory_2";
	/// <summary>Accent color for the 3D prop and UI card.</summary>
	public Color Tint { get; init; } = Color.White;
	public string Blurb { get; init; } = "";

	public bool HasElectronics => Category is ItemCategory.Electronics or ItemCategory.Gaming or ItemCategory.Cameras or ItemCategory.Appliances;
	public bool HasMetal => Category is ItemCategory.Jewelry or ItemCategory.Watches or ItemCategory.Antiques or ItemCategory.Tools;
	public bool HasGem => Category is ItemCategory.Jewelry or ItemCategory.Watches;
}

/// <summary>The full item catalog (44 definitions across all categories).</summary>
public static class ItemCatalog
{
	private static readonly Color Gold = new( 0.95f, 0.78f, 0.25f );
	private static readonly Color Silver = new( 0.80f, 0.83f, 0.88f );
	private static readonly Color Tech = new( 0.35f, 0.40f, 0.48f );
	private static readonly Color WoodTone = new( 0.65f, 0.42f, 0.20f );
	private static readonly Color ToolRed = new( 0.85f, 0.30f, 0.22f );
	private static readonly Color ArtBlue = new( 0.35f, 0.55f, 0.85f );

	public static readonly List<ItemDef> All = new()
	{
		// ---------------- Jewelry ----------------
		new ItemDef { Id = "gold_ring", Name = "Gold Band Ring", Brand = "Auric & Sons", Category = ItemCategory.Jewelry, BaseValue = 220, CounterfeitChance = 0.22f, MaxRarity = Rarity.Rare, Icon = "circle", Tint = Gold,
			DefectPool = new[]{ "scratches", "plated", "pure_metal", "fine_crack" }, Blurb = "A classic solid band, if it really is solid." },
		new ItemDef { Id = "silver_chain", Name = "Silver Rope Chain", Brand = "Moonspun", Category = ItemCategory.Jewelry, BaseValue = 140, CounterfeitChance = 0.18f, Icon = "link", Tint = Silver,
			DefectPool = new[]{ "scratches", "plated", "wrong_alloy", "repaired_seam" }, Blurb = "Heavy rope-link chain with a lobster clasp." },
		new ItemDef { Id = "gem_necklace", Name = "Gemstone Necklace", Brand = "Vellora", Category = ItemCategory.Jewelry, BaseValue = 480, CounterfeitChance = 0.25f, MaxRarity = Rarity.VeryRare, Icon = "diamond", Tint = new Color( 0.35f, 0.75f, 0.65f ),
			DefectPool = new[]{ "glass_gem", "lab_gem", "flawless_gem", "repaired_seam", "fine_crack" }, Blurb = "A pendant stone that catches every light in the room." },
		new ItemDef { Id = "tennis_bracelet", Name = "Tennis Bracelet", Brand = "Vellora", Category = ItemCategory.Jewelry, BaseValue = 380, CounterfeitChance = 0.22f, MaxRarity = Rarity.Rare, Icon = "blur_circular", Tint = Silver,
			DefectPool = new[]{ "glass_gem", "lab_gem", "plated", "missing_screw" }, Blurb = "A line of small stones set in a delicate band." },
		new ItemDef { Id = "pearl_earrings", Name = "Pearl Earrings", Brand = "Moonspun", Category = ItemCategory.Jewelry, BaseValue = 190, CounterfeitChance = 0.15f, Icon = "grain", Tint = new Color( 0.95f, 0.92f, 0.86f ),
			DefectPool = new[]{ "scratches", "glass_gem", "repaired_seam" }, Blurb = "Matched pearls on white-metal posts." },

		// ---------------- Watches ----------------
		new ItemDef { Id = "dive_watch", Name = "Diver's Watch", Brand = "Tidemark", Category = ItemCategory.Watches, BaseValue = 650, CounterfeitChance = 0.30f, MaxRarity = Rarity.VeryRare, Icon = "watch", Tint = new Color( 0.15f, 0.35f, 0.55f ),
			DefectPool = new[]{ "scratches", "fake_print", "wrong_font", "fine_crack", "water_damage" }, Blurb = "A serious tool watch — and the most faked model in the city." },
		new ItemDef { Id = "dress_watch", Name = "Dress Watch", Brand = "Calloway & Finch", Category = ItemCategory.Watches, BaseValue = 420, CounterfeitChance = 0.25f, MaxRarity = Rarity.Rare, Icon = "watch", Tint = Gold,
			DefectPool = new[]{ "scratches", "plated", "wrong_font", "repaired_seam" }, Blurb = "Slim, elegant, and easy to counterfeit." },
		new ItemDef { Id = "field_watch", Name = "Field Watch", Brand = "Bramble Co.", Category = ItemCategory.Watches, BaseValue = 180, CounterfeitChance = 0.12f, Icon = "watch", Tint = new Color( 0.45f, 0.48f, 0.35f ),
			DefectPool = new[]{ "scratches", "worn_grip", "fine_crack", "missing_part" }, Blurb = "A rugged everyday watch on a canvas strap." },
		new ItemDef { Id = "pocket_watch", Name = "Antique Pocket Watch", Brand = "Hollis Bros.", Category = ItemCategory.Watches, BaseValue = 520, CounterfeitChance = 0.15f, MaxRarity = Rarity.Legendary, Icon = "schedule", Tint = Gold,
			DefectPool = new[]{ "fine_crack", "maker_mark", "repaired_seam", "uv_repair", "rust" }, Blurb = "Engraved case, chain intact. Could be somebody's heirloom." },

		// ---------------- Electronics ----------------
		new ItemDef { Id = "laptop", Name = "Ultrabook Laptop", Brand = "Novum", Category = ItemCategory.Electronics, BaseValue = 540, CounterfeitChance = 0.08f, MaxRarity = Rarity.Rare, Icon = "laptop", Tint = Tech,
			DefectPool = new[]{ "scratches", "dead_battery", "water_damage", "board_fault", "missing_part", "scratched_serial" }, Blurb = "Thin, light, and possibly full of pond water." },
		new ItemDef { Id = "tablet", Name = "Slate Tablet", Brand = "Novum", Category = ItemCategory.Electronics, BaseValue = 320, CounterfeitChance = 0.12f, Icon = "tablet", Tint = Tech,
			DefectPool = new[]{ "crack", "dead_battery", "gutted", "missing_part" }, Blurb = "A big glossy screen. Check it actually turns on." },
		new ItemDef { Id = "phone", Name = "Smartphone", Brand = "Pebbl", Category = ItemCategory.Electronics, BaseValue = 380, CounterfeitChance = 0.18f, Icon = "smartphone", Tint = new Color( 0.20f, 0.22f, 0.26f ),
			DefectPool = new[]{ "crack", "dead_battery", "water_damage", "gutted", "flagged_serial", "scratched_serial" }, Blurb = "Everyone wants one, and thieves know it." },
		new ItemDef { Id = "headphones", Name = "Studio Headphones", Brand = "EchoLab", Category = ItemCategory.Electronics, BaseValue = 160, CounterfeitChance = 0.20f, Icon = "headphones", Tint = new Color( 0.75f, 0.30f, 0.30f ),
			DefectPool = new[]{ "scratches", "fake_print", "board_fault", "missing_part" }, Blurb = "Fat cushioned cans with a detachable cable." },
		new ItemDef { Id = "speaker", Name = "Bookshelf Speaker Pair", Brand = "EchoLab", Category = ItemCategory.Electronics, BaseValue = 240, CounterfeitChance = 0.08f, Icon = "speaker", Tint = WoodTone,
			DefectPool = new[]{ "dent", "board_fault", "worn_grip", "mold" }, Blurb = "Warm sound, walnut veneer, heavier than they look." },
		new ItemDef { Id = "drone", Name = "Camera Drone", Brand = "SkyPuppy", Category = ItemCategory.Electronics, BaseValue = 450, CounterfeitChance = 0.10f, MaxRarity = Rarity.Rare, Icon = "flight", Tint = Color.White,
			DefectPool = new[]{ "crack", "dead_battery", "board_fault", "missing_part", "recall_model" }, Blurb = "Four props and a gimbal. Crashed at least once, guaranteed." },

		// ---------------- Instruments ----------------
		new ItemDef { Id = "acoustic_guitar", Name = "Acoustic Guitar", Brand = "Bramble Co.", Category = ItemCategory.Instruments, BaseValue = 300, CounterfeitChance = 0.10f, MaxRarity = Rarity.Rare, Icon = "music_note", Tint = WoodTone,
			DefectPool = new[]{ "crack", "fine_crack", "repaired_seam", "maker_mark", "worn_grip" }, Blurb = "Dreadnought body with a warm, worn-in tone." },
		new ItemDef { Id = "electric_guitar", Name = "Electric Guitar", Brand = "Voltaire", Category = ItemCategory.Instruments, BaseValue = 520, CounterfeitChance = 0.20f, MaxRarity = Rarity.VeryRare, Icon = "music_note", Tint = new Color( 0.80f, 0.25f, 0.30f ),
			DefectPool = new[]{ "scratches", "fake_print", "board_fault", "maker_mark", "missing_screw" }, Blurb = "A sunburst solid-body. Fakes are everywhere." },
		new ItemDef { Id = "keyboard_piano", Name = "Stage Keyboard", Brand = "Voltaire", Category = ItemCategory.Instruments, BaseValue = 380, CounterfeitChance = 0.05f, Icon = "piano", Tint = Tech,
			DefectPool = new[]{ "dead_battery", "board_fault", "missing_part", "scratches" }, Blurb = "88 weighted keys and one suspicious power adapter." },
		new ItemDef { Id = "trumpet", Name = "Brass Trumpet", Brand = "Hollis Bros.", Category = ItemCategory.Instruments, BaseValue = 260, CounterfeitChance = 0.06f, Icon = "campaign", Tint = Gold,
			DefectPool = new[]{ "dent", "rust", "repaired_seam", "maker_mark" }, Blurb = "Bright bell, sticky second valve." },
		new ItemDef { Id = "violin", Name = "Violin", Brand = "Casta Workshop", Category = ItemCategory.Instruments, BaseValue = 600, CounterfeitChance = 0.15f, MaxRarity = Rarity.Legendary, Icon = "music_note", Tint = new Color( 0.55f, 0.30f, 0.15f ),
			DefectPool = new[]{ "fine_crack", "repaired_seam", "maker_mark", "uv_repair", "uv_signature" }, Blurb = "Old wood, older varnish. Could be a workshop original." },

		// ---------------- Tools ----------------
		new ItemDef { Id = "drill", Name = "Cordless Drill", Brand = "IronMule", Category = ItemCategory.Tools, BaseValue = 130, CounterfeitChance = 0.10f, Icon = "construction", Tint = ToolRed,
			DefectPool = new[]{ "dead_battery", "worn_grip", "missing_part", "fake_print" }, Blurb = "Torquey little workhorse with a chewed-up chuck." },
		new ItemDef { Id = "table_saw", Name = "Compact Table Saw", Brand = "IronMule", Category = ItemCategory.Tools, BaseValue = 280, CounterfeitChance = 0.04f, Icon = "carpenter", Tint = ToolRed,
			DefectPool = new[]{ "rust", "missing_screw", "worn_grip", "board_fault" }, Blurb = "Folds flat, cuts straight — when the fence isn't bent." },
		new ItemDef { Id = "tool_chest", Name = "Mechanic's Tool Chest", Brand = "IronMule", Category = ItemCategory.Tools, BaseValue = 350, CounterfeitChance = 0.05f, MaxRarity = Rarity.Rare, Icon = "handyman", Tint = new Color( 0.75f, 0.15f, 0.15f ),
			DefectPool = new[]{ "rust", "dent", "missing_part", "scratched_serial" }, Blurb = "Five drawers of sockets — count them before you pay." },
		new ItemDef { Id = "generator", Name = "Portable Generator", Brand = "IronMule", Category = ItemCategory.Tools, BaseValue = 420, CounterfeitChance = 0.03f, Icon = "bolt", Tint = new Color( 0.90f, 0.60f, 0.15f ),
			DefectPool = new[]{ "rust", "board_fault", "dent", "recall_model" }, Blurb = "Starts on the third pull, usually." },

		// ---------------- Sports ----------------
		new ItemDef { Id = "mountain_bike", Name = "Mountain Bike", Brand = "Ridgeline", Category = ItemCategory.Sports, BaseValue = 380, CounterfeitChance = 0.06f, Icon = "pedal_bike", Tint = new Color( 0.25f, 0.65f, 0.35f ),
			DefectPool = new[]{ "rust", "dent", "worn_grip", "scratched_serial", "flagged_serial" }, Blurb = "Full suspension. Bikes walk out of yards around here." },
		new ItemDef { Id = "golf_clubs", Name = "Golf Club Set", Brand = "Fairwind", Category = ItemCategory.Sports, BaseValue = 320, CounterfeitChance = 0.15f, Icon = "golf_course", Tint = Silver,
			DefectPool = new[]{ "scratches", "fake_print", "missing_part", "worn_grip" }, Blurb = "A full bag of irons — check the heads are genuine." },
		new ItemDef { Id = "kayak", Name = "Touring Kayak", Brand = "Ridgeline", Category = ItemCategory.Sports, BaseValue = 260, CounterfeitChance = 0.02f, Icon = "kayaking", Tint = new Color( 0.95f, 0.55f, 0.15f ),
			DefectPool = new[]{ "crack", "repaired_seam", "scratches" }, Blurb = "One careful owner, two rocky landings." },
		new ItemDef { Id = "treadmill", Name = "Folding Treadmill", Brand = "Fairwind", Category = ItemCategory.Sports, BaseValue = 300, CounterfeitChance = 0.02f, Icon = "directions_run", Tint = Tech,
			DefectPool = new[]{ "board_fault", "worn_grip", "dent", "recall_model" }, Blurb = "Barely used, like every treadmill ever sold." },

		// ---------------- Collectibles ----------------
		new ItemDef { Id = "trading_card", Name = "Holo Trading Card", Brand = "Cavern Clash", Category = ItemCategory.Collectibles, BaseValue = 150, CounterfeitChance = 0.35f, MaxRarity = Rarity.Legendary, Icon = "style", Tint = new Color( 0.55f, 0.40f, 0.85f ),
			DefectPool = new[]{ "scratches", "fake_print", "altered_label", "rare_variant" }, Blurb = "A first-print holo. The reprints are very convincing." },
		new ItemDef { Id = "figurine", Name = "Limited Figurine", Brand = "MechaMight", Category = ItemCategory.Collectibles, BaseValue = 180, CounterfeitChance = 0.28f, MaxRarity = Rarity.VeryRare, Icon = "smart_toy", Tint = new Color( 0.90f, 0.45f, 0.60f ),
			DefectPool = new[]{ "crack", "fake_print", "repaired_seam", "rare_variant", "missing_part" }, Blurb = "Boxed mecha figure, numbered run — allegedly." },
		new ItemDef { Id = "vintage_toy", Name = "Vintage Tin Robot", Brand = "Cogsworth Toys", Category = ItemCategory.Collectibles, BaseValue = 240, CounterfeitChance = 0.18f, MaxRarity = Rarity.Legendary, Icon = "smart_toy", Tint = new Color( 0.75f, 0.20f, 0.20f ),
			DefectPool = new[]{ "rust", "dent", "repaired_seam", "maker_mark", "rare_variant" }, Blurb = "Wind-up robot from the golden age of tin." },
		new ItemDef { Id = "signed_ball", Name = "Signed Match Ball", Brand = "", Category = ItemCategory.Collectibles, BaseValue = 350, CounterfeitChance = 0.32f, MaxRarity = Rarity.VeryRare, Icon = "sports_soccer", Tint = Color.White,
			DefectPool = new[]{ "altered_label", "uv_signature", "worn_grip", "fake_print" }, Blurb = "Signed by a championship squad. Or by someone's nephew." },
		new ItemDef { Id = "old_coin", Name = "Old Mint Coin", Brand = "", Category = ItemCategory.Collectibles, BaseValue = 280, CounterfeitChance = 0.25f, MaxRarity = Rarity.Legendary, Icon = "paid", Tint = Gold,
			DefectPool = new[]{ "wrong_alloy", "pure_metal", "scratches", "rare_variant" }, Blurb = "Pre-decimal coinage. Weigh it before you believe it." },
		new ItemDef { Id = "comic_book", Name = "Debut Issue Comic", Brand = "Meteor Comics", Category = ItemCategory.Collectibles, BaseValue = 320, CounterfeitChance = 0.20f, MaxRarity = Rarity.Legendary, Icon = "auto_stories", Tint = new Color( 0.95f, 0.80f, 0.30f ),
			DefectPool = new[]{ "scratches", "altered_label", "fake_print", "rare_variant", "mold" }, Blurb = "First appearance of Captain Meteor. Mind the spine." },

		// ---------------- Art ----------------
		new ItemDef { Id = "oil_painting", Name = "Oil Landscape", Brand = "", Category = ItemCategory.Art, BaseValue = 420, CounterfeitChance = 0.22f, MaxRarity = Rarity.Legendary, Icon = "palette", Tint = ArtBlue,
			DefectPool = new[]{ "uv_repair", "uv_signature", "fine_crack", "mold", "repaired_seam" }, Blurb = "Moody hills in heavy oils. The signature is... optimistic." },
		new ItemDef { Id = "bronze_sculpture", Name = "Bronze Figure", Brand = "Casta Workshop", Category = ItemCategory.Art, BaseValue = 520, CounterfeitChance = 0.18f, MaxRarity = Rarity.VeryRare, Icon = "account_balance", Tint = new Color( 0.55f, 0.42f, 0.25f ),
			DefectPool = new[]{ "wrong_alloy", "maker_mark", "dent", "repaired_seam" }, Blurb = "A dancer mid-turn, heavier than a toolbox." },
		new ItemDef { Id = "ceramic_vase", Name = "Glazed Ceramic Vase", Brand = "", Category = ItemCategory.Art, BaseValue = 260, CounterfeitChance = 0.15f, MaxRarity = Rarity.Rare, Icon = "local_florist", Tint = new Color( 0.35f, 0.70f, 0.75f ),
			DefectPool = new[]{ "crack", "fine_crack", "repaired_seam", "uv_repair", "maker_mark" }, Blurb = "Crackle-glaze vase. Half of these left the kiln last year." },

		// ---------------- Antiques ----------------
		new ItemDef { Id = "mantel_clock", Name = "Mantel Clock", Brand = "Hollis Bros.", Category = ItemCategory.Antiques, BaseValue = 340, CounterfeitChance = 0.12f, MaxRarity = Rarity.VeryRare, Icon = "schedule", Tint = WoodTone,
			DefectPool = new[]{ "fine_crack", "rust", "repaired_seam", "maker_mark", "uv_repair" }, Blurb = "Chimes on the half hour, whether you like it or not." },
		new ItemDef { Id = "decor_sword", Name = "Ceremonial Sword", Brand = "", Category = ItemCategory.Antiques, BaseValue = 450, CounterfeitChance = 0.20f, MaxRarity = Rarity.Legendary, Icon = "swords", Tint = Silver,
			DefectPool = new[]{ "rust", "wrong_alloy", "repaired_seam", "maker_mark", "uv_repair" }, Blurb = "An officer's dress sword — or a very good replica." },
		new ItemDef { Id = "typewriter", Name = "Portable Typewriter", Brand = "Cogsworth", Category = ItemCategory.Antiques, BaseValue = 210, CounterfeitChance = 0.05f, MaxRarity = Rarity.Rare, Icon = "keyboard", Tint = new Color( 0.30f, 0.45f, 0.40f ),
			DefectPool = new[]{ "rust", "missing_screw", "worn_grip", "maker_mark" }, Blurb = "Sticky E key. Writers pay silly money for these." },
		new ItemDef { Id = "gramophone", Name = "Horn Gramophone", Brand = "EchoLab Heritage", Category = ItemCategory.Antiques, BaseValue = 480, CounterfeitChance = 0.15f, MaxRarity = Rarity.VeryRare, Icon = "album", Tint = Gold,
			DefectPool = new[]{ "dent", "rust", "repaired_seam", "uv_repair", "maker_mark" }, Blurb = "Brass horn, oak base, one previous century." },

		// ---------------- Gaming ----------------
		new ItemDef { Id = "game_console", Name = "Game Console", Brand = "Pixelforge", Category = ItemCategory.Gaming, BaseValue = 340, CounterfeitChance = 0.10f, Icon = "sports_esports", Tint = Tech,
			DefectPool = new[]{ "dead_battery", "board_fault", "gutted", "missing_part", "scratched_serial" }, Blurb = "Current-gen console, controller included. Sometimes." },
		new ItemDef { Id = "retro_console", Name = "Retro Console", Brand = "Pixelforge Classic", Category = ItemCategory.Gaming, BaseValue = 260, CounterfeitChance = 0.22f, MaxRarity = Rarity.VeryRare, Icon = "videogame_asset", Tint = new Color( 0.80f, 0.78f, 0.70f ),
			DefectPool = new[]{ "board_fault", "fake_print", "rare_variant", "scratches" }, Blurb = "The 16-bit classic. Clone boards flood the market." },
		new ItemDef { Id = "arcade_stick", Name = "Arcade Fight Stick", Brand = "Pixelforge", Category = ItemCategory.Gaming, BaseValue = 150, CounterfeitChance = 0.12f, Icon = "gamepad", Tint = new Color( 0.85f, 0.30f, 0.55f ),
			DefectPool = new[]{ "worn_grip", "board_fault", "missing_screw", "fake_print" }, Blurb = "Tournament-grade buttons, basement-grade smell." },

		// ---------------- Cameras ----------------
		new ItemDef { Id = "dslr", Name = "DSLR Camera", Brand = "Lumen Optics", Category = ItemCategory.Cameras, BaseValue = 480, CounterfeitChance = 0.10f, MaxRarity = Rarity.Rare, Icon = "photo_camera", Tint = Tech,
			DefectPool = new[]{ "scratches", "dead_battery", "board_fault", "missing_part", "flagged_serial" }, Blurb = "Pro body with a kit lens and a mystery shutter count." },
		new ItemDef { Id = "film_camera", Name = "Vintage Film Camera", Brand = "Lumen Optics", Category = ItemCategory.Cameras, BaseValue = 320, CounterfeitChance = 0.12f, MaxRarity = Rarity.Legendary, Icon = "camera_roll", Tint = Silver,
			DefectPool = new[]{ "fine_crack", "rust", "maker_mark", "rare_variant", "repaired_seam" }, Blurb = "All-mechanical rangefinder. Collectors circle these like gulls." },

		// ---------------- Appliances ----------------
		new ItemDef { Id = "espresso_machine", Name = "Espresso Machine", Brand = "Brewhalla", Category = ItemCategory.Appliances, BaseValue = 300, CounterfeitChance = 0.06f, Icon = "coffee_maker", Tint = new Color( 0.70f, 0.25f, 0.20f ),
			DefectPool = new[]{ "rust", "board_fault", "water_damage", "missing_part" }, Blurb = "Prosumer machine with a scale problem." },
		new ItemDef { Id = "stand_mixer", Name = "Stand Mixer", Brand = "Brewhalla", Category = ItemCategory.Appliances, BaseValue = 220, CounterfeitChance = 0.08f, Icon = "blender", Tint = new Color( 0.55f, 0.75f, 0.85f ),
			DefectPool = new[]{ "dent", "board_fault", "worn_grip", "missing_part" }, Blurb = "Cast-metal mixer in a very fashionable color." },

		// ---------------- Memorabilia ----------------
		new ItemDef { Id = "signed_poster", Name = "Signed Tour Poster", Brand = "", Category = ItemCategory.Memorabilia, BaseValue = 260, CounterfeitChance = 0.30f, MaxRarity = Rarity.VeryRare, Icon = "theater_comedy", Tint = new Color( 0.85f, 0.35f, 0.65f ),
			DefectPool = new[]{ "altered_label", "uv_signature", "fake_print", "scratches", "mold" }, Blurb = "Signed by The Copper Foxes on their farewell tour. Maybe." },
		new ItemDef { Id = "match_jersey", Name = "Match-Worn Jersey", Brand = "", Category = ItemCategory.Memorabilia, BaseValue = 380, CounterfeitChance = 0.28f, MaxRarity = Rarity.Legendary, Icon = "checkroom", Tint = new Color( 0.25f, 0.45f, 0.80f ),
			DefectPool = new[]{ "altered_label", "fake_print", "uv_signature", "worn_grip", "rare_variant" }, Blurb = "Framed jersey from the cup final. Sweat stains not verified." },
		new ItemDef { Id = "movie_prop", Name = "Movie Prop Helmet", Brand = "Meteor Studios", Category = ItemCategory.Memorabilia, BaseValue = 520, CounterfeitChance = 0.25f, MaxRarity = Rarity.Legendary, Icon = "movie", Tint = new Color( 0.60f, 0.60f, 0.65f ),
			DefectPool = new[]{ "crack", "repaired_seam", "altered_label", "rare_variant", "uv_repair" }, Blurb = "Screen-used helmet from 'Star Vandals III'. Certificate looks fresh." },
	};

	private static Dictionary<string, ItemDef> _byId;

	public static ItemDef Get( string id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return id is not null && _byId.TryGetValue( id, out var d ) ? d : null;
	}

	public static ItemDef Random( Func<ItemDef, bool> filter = null )
	{
		var pool = filter is null ? All : All.Where( filter ).ToList();
		if ( pool.Count == 0 ) pool = All;
		return pool[Game.Random.Int( 0, pool.Count - 1 )];
	}
}
