namespace Offshore;

public static class Catalog
{
	public static IReadOnlyList<FishDefinition> Fish { get; private set; }
	public static IReadOnlyList<BaitDefinition> Baits { get; private set; }
	public static IReadOnlyList<RodDefinition> Rods { get; private set; }
	public static IReadOnlyList<ReelDefinition> Reels { get; private set; }
	public static IReadOnlyList<HookDefinition> Hooks { get; private set; }
	public static IReadOnlyList<LineDefinition> Lines { get; private set; }
	public static IReadOnlyList<BoatDefinition> Boats { get; private set; }
	public static IReadOnlyList<ObjectiveDefinition> Objectives { get; private set; }

	static Catalog() => Reload();

	/// <summary>Rebuild defs (call on game start so hot-reload / new builds pick up balance changes).</summary>
	public static void Reload()
	{
		Baits = BuildBaits();
		Rods = BuildRods();
		Reels = BuildReels();
		Hooks = BuildHooks();
		Lines = BuildLines();
		Boats = BuildBoats();
		Fish = BuildFish();
		Objectives = BuildObjectives();
	}

	public static FishDefinition FishById( string id ) => Fish.FirstOrDefault( f => f.Id == id );
	public static BaitDefinition BaitById( string id ) => Baits.FirstOrDefault( b => b.Id == id );
	public static RodDefinition RodById( string id ) => Rods.FirstOrDefault( r => r.Id == id );
	public static ReelDefinition ReelById( string id ) => Reels.FirstOrDefault( r => r.Id == id );
	public static HookDefinition HookById( string id ) => Hooks.FirstOrDefault( h => h.Id == id );
	public static LineDefinition LineById( string id ) => Lines.FirstOrDefault( l => l.Id == id );
	public static BoatDefinition BoatById( string id ) => Boats.FirstOrDefault( b => b.Id == id );

	static List<BaitDefinition> BuildBaits() => new()
	{
		B( "worm", "Worm", "Common bait for small and medium near-shore fish.", "icon_bait_worm", 15, 10, 0, 40, 1.2f, 1.3f, 1f, 0.1f, 0, 0, 0, 0.1f, new[] { "sardine", "bluegill", "mackerel" }, new[] { "flounder", "seabass" } ),
		B( "minnow", "Minnow", "Live bait that draws active midwater feeders.", "icon_bait_minnow", 25, 8, 2, 60, 1.1f, 1.1f, 0.9f, 0.05f, 0.1f, 0, 0.2f, 0.15f, new[] { "mackerel", "seabass", "kingmackerel" }, new[] { "tuna", "barracuda" } ),
		B( "shrimp", "Shrimp", "Excellent near rocks, reefs, and structure.", "icon_bait_shrimp", 40, 5, 5, 80, 1f, 1f, 1f, 0.1f, 0, 0.1f, 0.1f, 0.2f, new[] { "redsnapper", "grouper", "seabass" }, new[] { "cobia", "flounder" } ),
		B( "squid", "Squid", "Deep predator bait with slower, heavier bites.", "icon_bait_squid", 60, 5, 20, 180, 1.3f, 0.75f, 1.2f, 0.05f, 0.15f, 0, 0.5f, 0.45f, new[] { "tuna", "swordfish", "marlin" }, new[] { "cobia", "gianttrevally" } ),
		B( "crab", "Crab", "Bottom presentations for flounder and grouper.", "icon_bait_crab", 45, 4, 8, 90, 0.9f, 0.85f, 1.1f, 0, 0, 0.1f, 0.15f, 0.2f, new[] { "flounder", "grouper", "redsnapper" }, new[] { "cobia" } ),
		B( "sardine", "Sardine", "Oily baitfish strip for aggressive hunters.", "icon_bait_sardine", 50, 5, 10, 120, 1.2f, 1.05f, 0.95f, 0.1f, 0.05f, 0, 0.4f, 0.3f, new[] { "barracuda", "kingmackerel", "mahi" }, new[] { "tuna", "gianttrevally" } ),
		B( "mackerel_bait", "Mackerel Chunk", "Chunk bait for large offshore hunters.", "icon_bait_mackerel", 70, 4, 30, 200, 1.4f, 0.8f, 1.15f, 0, 0.1f, 0, 0.55f, 0.4f, new[] { "tuna", "marlin", "swordfish" }, new[] { "gianttrevally", "oarfish" } ),
		B( "luminous_jelly", "Luminous Jelly", "Glows in deep water and at night.", "icon_bait_jelly", 120, 2, 40, 400, 1.5f, 0.7f, 1.3f, 0.2f, 0.8f, -0.1f, 0.3f, 0.7f, new[] { "anglerfish", "oarfish", "giantsquid" }, new[] { "swordfish", "marlin" } ),
	};

	static List<RodDefinition> BuildRods() => new()
	{
		new() { Id = "starter_rod", Name = "Starter Rod", Description = "Short cast, forgiving tension, tutorial friendly.", Icon = "icon_rod", Price = 0, CastDistance = 0.75f, CastPower = 0.8f, CastAccuracy = 0.85f, MaxTension = 45f, PullControl = 0.85f, TensionRecovery = 0.9f, StaminaDrain = 0.85f, MaxFishWeight = 12f, BendSpeed = 1.1f, HookPower = 0.8f, Responsiveness = 1f, WeatherStability = 0.7f },
		new() { Id = "precision_rod", Name = "Light Precision Rod", Description = "Fast response and hotspot accuracy. Lower max weight.", Icon = "icon_rod", Price = 150, CastDistance = 0.95f, CastPower = 0.9f, CastAccuracy = 1.35f, MaxTension = 50f, PullControl = 1.25f, TensionRecovery = 1.1f, StaminaDrain = 1.05f, MaxFishWeight = 16f, BendSpeed = 1.3f, HookPower = 0.95f, Responsiveness = 1.4f, WeatherStability = 0.85f },
		new() { Id = "heavy_rod", Name = "Heavy Rod", Description = "High tension and hook set. Slower cast.", Icon = "icon_rod", Price = 320, CastDistance = 0.85f, CastPower = 1.2f, CastAccuracy = 0.75f, MaxTension = 85f, PullControl = 1.1f, TensionRecovery = 0.85f, StaminaDrain = 1.2f, MaxFishWeight = 60f, BendSpeed = 0.7f, HookPower = 1.4f, Responsiveness = 0.7f, WeatherStability = 1.1f },
		new() { Id = "carbon_rod", Name = "Advanced Carbon Rod", Description = "Long cast, strong recovery, broad usefulness.", Icon = "icon_rod", Price = 750, CastDistance = 1.35f, CastPower = 1.15f, CastAccuracy = 1.15f, MaxTension = 75f, PullControl = 1.3f, TensionRecovery = 1.35f, StaminaDrain = 1.25f, MaxFishWeight = 45f, BendSpeed = 1.05f, HookPower = 1.2f, Responsiveness = 1.2f, WeatherStability = 1.25f },
	};

	static List<ReelDefinition> BuildReels() => new()
	{
		new() { Id = "starter_reel", Name = "Starter Reel", Description = "Reliable beginner reel with modest drag.", Icon = "icon_reel", Price = 0, ReelSpeed = 0.85f, ReelAccel = 0.8f, SlackRecovery = 0.85f, MaxDrag = 40f, StaminaDrain = 0.9f, TensionRecovery = 0.9f, RetrievalDepth = 0.8f, Smoothness = 0.8f },
		new() { Id = "torque_reel", Name = "Power Torque Reel", Description = "Slow and powerful for heavy fish.", Icon = "icon_reel", Price = 175, ReelSpeed = 0.7f, ReelAccel = 0.75f, SlackRecovery = 1f, MaxDrag = 75f, StaminaDrain = 1.25f, TensionRecovery = 1.1f, RetrievalDepth = 1.1f, Smoothness = 0.9f },
		new() { Id = "speed_reel", Name = "Speed Runner Reel", Description = "Fast retrieves with lower torque.", Icon = "icon_reel", Price = 210, ReelSpeed = 1.4f, ReelAccel = 1.3f, SlackRecovery = 1.2f, MaxDrag = 45f, StaminaDrain = 0.95f, TensionRecovery = 0.95f, RetrievalDepth = 0.95f, Smoothness = 1.1f },
		new() { Id = "deep_reel", Name = "Abyss Drum Reel", Description = "Heavy deep-water drum for extreme depths.", Icon = "icon_reel", Price = 680, ReelSpeed = 0.95f, ReelAccel = 1f, SlackRecovery = 1.15f, MaxDrag = 95f, StaminaDrain = 1.35f, TensionRecovery = 1.2f, RetrievalDepth = 1.5f, Smoothness = 1.2f },
	};

	static List<HookDefinition> BuildHooks() => new()
	{
		new() { Id = "starter_hook", Name = "Starter Hook", Description = "Small all-purpose hook.", Icon = "icon_hook", Price = 0, HookSuccess = 0.7f, HookWindow = 0.75f, EscapeChance = 0.28f, MaxFishSize = 14f, AggressiveRetention = 0.4f, SnagResistance = 0.5f, BaitRetention = 0.7f, SmallFishPenalty = 0f, LargeFishBonus = -0.2f },
		new() { Id = "widegap_hook", Name = "Wide-Gap Predator", Description = "Holds aggressive mouths. Spooks tiny fish.", Icon = "icon_hook", Price = 100, HookSuccess = 0.72f, HookWindow = 0.65f, EscapeChance = 0.2f, MaxFishSize = 40f, AggressiveRetention = 0.85f, SnagResistance = 0.55f, BaitRetention = 0.75f, SmallFishPenalty = 0.25f, LargeFishBonus = 0.25f },
		new() { Id = "circle_hook", Name = "Circle Hook", Description = "Excellent retention, slightly slower sets.", Icon = "icon_hook", Price = 140, HookSuccess = 0.8f, HookWindow = 0.9f, EscapeChance = 0.12f, MaxFishSize = 30f, AggressiveRetention = 0.7f, SnagResistance = 0.65f, BaitRetention = 0.9f, SmallFishPenalty = 0.05f, LargeFishBonus = 0.1f },
		new() { Id = "offshore_hook", Name = "Reinforced Offshore", Description = "Built for giants and deep fights.", Icon = "icon_hook", Price = 260, HookSuccess = 0.78f, HookWindow = 0.7f, EscapeChance = 0.15f, MaxFishSize = 90f, AggressiveRetention = 0.9f, SnagResistance = 0.8f, BaitRetention = 0.8f, SmallFishPenalty = 0.35f, LargeFishBonus = 0.4f },
	};

	static List<LineDefinition> BuildLines() => new()
	{
		new() { Id = "mono_line", Name = "Monofilament", Description = "Balanced stretch and visibility. Beginner friendly.", Icon = "icon_line", Price = 0, MaxTension = 50f, Abrasion = 0.5f, Stretch = 0.65f, CastDistance = 1f, Visibility = 0.7f, SlackRecovery = 0.9f, Sensitivity = 0.85f, SnagResistance = 0.55f, DeepCapability = 0.5f },
		new() { Id = "fluoro_line", Name = "Fluorocarbon", Description = "Low visibility for skittish fish.", Icon = "icon_line", Price = 125, MaxTension = 55f, Abrasion = 0.7f, Stretch = 0.35f, CastDistance = 0.95f, Visibility = 0.25f, SlackRecovery = 1f, Sensitivity = 1.1f, SnagResistance = 0.6f, DeepCapability = 0.7f },
		new() { Id = "braid_line", Name = "Braided Line", Description = "High strength and cast distance. More visible.", Icon = "icon_line", Price = 180, MaxTension = 80f, Abrasion = 0.4f, Stretch = 0.15f, CastDistance = 1.35f, Visibility = 0.85f, SlackRecovery = 1.25f, Sensitivity = 1.35f, SnagResistance = 0.35f, DeepCapability = 0.85f },
		new() { Id = "heavy_line", Name = "Heavy Offshore Line", Description = "Extreme strength for deep water. Shorter casts.", Icon = "icon_line", Price = 300, MaxTension = 110f, Abrasion = 0.85f, Stretch = 0.25f, CastDistance = 0.75f, Visibility = 0.75f, SlackRecovery = 1.05f, Sensitivity = 1f, SnagResistance = 0.8f, DeepCapability = 1.4f },
	};

	static List<BoatDefinition> BuildBoats() => new()
	{
		new() { Id = "dinghy", Name = "Dinghy", Description = "Your first boat. Short range, lively in waves — buy it at the shop when you're ready to leave the pier.", Sprite = "boat_dinghy", Price = 450, TopSpeed = 63f, Acceleration = 42f, Braking = 35f, FuelCapacity = 200f, FuelEfficiency = 0.85f, MaxRange = 2500f, Storage = 6, MaxDepth = 25f, Stability = 0.45f, WaveResponse = 1.4f, WindResponse = 1.35f, CastAccuracy = 0.75f, CastDistanceMod = 0.85f, WeatherResistance = 0.4f, TowPenalty = 40, Size = new( 200, 96 ), PlayerAnchor = new( 14, 20 ), RodAnchor = new( 22, 22 ), LureLaunch = new( 28, 6 ), WakeAnchor = new( -30, 0 ), EngineAnchor = new( -26, 2 ) },
		new() { Id = "fisher17", Name = "Fisher 17", Description = "Workhorse coastal boat with better range and storage.", Sprite = "boat_fisher17", Price = 3500, TopSpeed = 105f, Acceleration = 60f, Braking = 48f, FuelCapacity = 400f, FuelEfficiency = 1.05f, MaxRange = 5500f, Storage = 12, MaxDepth = 60f, Stability = 0.75f, WaveResponse = 1f, WindResponse = 0.95f, CastAccuracy = 1f, CastDistanceMod = 1f, WeatherResistance = 0.7f, TowPenalty = 80, Size = new( 220, 106 ), PlayerAnchor = new( 16, 22 ), RodAnchor = new( 28, 24 ), LureLaunch = new( 34, 8 ), WakeAnchor = new( -36, 0 ), EngineAnchor = new( -30, 4 ) },
		new() { Id = "seawolf", Name = "Seawolf", Description = "Fast deep runner with strong storm resistance.", Sprite = "boat_seawolf", Price = 7500, TopSpeed = 157.5f, Acceleration = 87f, Braking = 62f, FuelCapacity = 700f, FuelEfficiency = 1.2f, MaxRange = 10000f, Storage = 20, MaxDepth = 140f, Stability = 1.05f, WaveResponse = 0.7f, WindResponse = 0.65f, CastAccuracy = 1.2f, CastDistanceMod = 1.15f, WeatherResistance = 1.15f, TowPenalty = 120, Size = new( 240, 114 ), PlayerAnchor = new( 18, 24 ), RodAnchor = new( 32, 26 ), LureLaunch = new( 40, 10 ), WakeAnchor = new( -42, 0 ), EngineAnchor = new( -36, 5 ) },
		new() { Id = "triton", Name = "Triton", Description = "Premier offshore vessel. Maximum range and depth.", Sprite = "boat_triton", Price = 15000, TopSpeed = 202.5f, Acceleration = 108f, Braking = 78f, FuelCapacity = 1100f, FuelEfficiency = 1.4f, MaxRange = 18000f, Storage = 32, MaxDepth = 320f, Stability = 1.35f, WaveResponse = 0.45f, WindResponse = 0.4f, CastAccuracy = 1.35f, CastDistanceMod = 1.3f, WeatherResistance = 1.45f, TowPenalty = 180, Size = new( 260, 124 ), PlayerAnchor = new( 20, 26 ), RodAnchor = new( 38, 30 ), LureLaunch = new( 48, 12 ), WakeAnchor = new( -50, 0 ), EngineAnchor = new( -42, 6 ) },
	};

	static List<FishDefinition> BuildFish() => new()
	{
		F( "sardine", "Sardine", "Tiny schooling baitfish near the dock.", "sardine", Rarity.Common, FishBehavior.Schooling, 0, 120, 30, 1, 18, 4, DayPhase.Morning, WeatherType.Clear, 18, 0.8f, "worm", "minnow", "sand", "open", 0.7f, 0.2f, 0.3f, 0.4f, 1.2f, 0.7f, 8, 0.2f, 8, 8, 14, 0.1f, 0.4f, 18, 12, "flutter", 0.25f, 0.6f ),
		F( "mackerel", "Mackerel", "Fast silver runners that chase minnows.", "mackerel", Rarity.Common, FishBehavior.ActivePredator, 10, 220, 80, 2, 35, 10, DayPhase.Afternoon, WeatherType.PartlyCloudy, 17, 0.75f, "minnow", "worm", "sand", "open", 0.55f, 0.25f, 0.4f, 0.35f, 1.1f, 0.55f, 5, 0.55f, 14, 18, 32, 0.4f, 1.2f, 28, 22, "zigzag", 0.35f, 0.55f ),
		F( "bluegill", "Bluegill", "Stubborn little panfish around pilings.", "bluegill", Rarity.Common, FishBehavior.Territorial, 0, 90, 20, 1, 12, 3, DayPhase.Midday, WeatherType.Clear, 20, 0.7f, "worm", "shrimp", "sand", "dock", 0.3f, 0.1f, 0.2f, 0.2f, 0.8f, 0.4f, 1, 0.35f, 10, 10, 18, 0.2f, 0.7f, 22, 18, "steady", 0.3f, 0.5f ),
		F( "flounder", "Flounder", "Flat bottom dweller that hugs the sand.", "flounder", Rarity.Common, FishBehavior.BottomFeeder, 20, 200, 70, 4, 30, 22, DayPhase.Dusk, WeatherType.Cloudy, 16, 0.55f, "crab", "shrimp", "sand", "open", 0.1f, 0.4f, 0.2f, 0.45f, 0.9f, 0.35f, 1, 0.25f, 18, 20, 40, 0.6f, 2.5f, 35, 28, "dive", 0.4f, 0.4f ),
		F( "seabass", "Sea Bass", "Structure-minded coastal fighter.", "seabass", Rarity.Uncommon, FishBehavior.StructureOriented, 40, 280, 120, 5, 45, 18, DayPhase.GoldenHour, WeatherType.Cloudy, 17, 0.65f, "shrimp", "minnow", "rock", "reef", 0.35f, 0.35f, 0.3f, 0.4f, 1.15f, 0.5f, 2, 0.5f, 28, 28, 55, 1.2f, 4f, 45, 35, "burst", 0.45f, 0.55f ),
		F( "redsnapper", "Red Snapper", "Reef beauty with a sharp pull.", "redsnapper", Rarity.Uncommon, FishBehavior.StructureOriented, 60, 320, 160, 8, 55, 28, DayPhase.Morning, WeatherType.Clear, 19, 0.7f, "shrimp", "crab", "rock", "reef", 0.25f, 0.2f, 0.25f, 0.3f, 1.25f, 0.45f, 2, 0.45f, 36, 30, 60, 1.5f, 5f, 48, 38, "circle", 0.5f, 0.5f ),
		F( "grouper", "Grouper", "Heavy reef bruiser that dives for cover.", "grouper", Rarity.Uncommon, FishBehavior.AmbushPredator, 100, 450, 220, 15, 80, 45, DayPhase.Afternoon, WeatherType.Overcast, 18, 0.6f, "crab", "shrimp", "rock", "reef", 0.15f, 0.25f, 0.2f, 0.35f, 1.3f, 0.4f, 1, 0.6f, 55, 40, 90, 3f, 14f, 70, 55, "dive", 0.55f, 0.45f ),
		F( "cobia", "Cobia", "Curious offshore cruiser near flotsam.", "cobia", Rarity.Rare, FishBehavior.Solitary, 160, 600, 320, 12, 90, 35, DayPhase.Midday, WeatherType.PartlyCloudy, 20, 0.75f, "squid", "sardine", "sand", "wreck", 0.4f, 0.3f, 0.35f, 0.25f, 1.4f, 0.5f, 1, 0.55f, 70, 50, 110, 5f, 22f, 75, 60, "run", 0.55f, 0.5f ),
		F( "barracuda", "Barracuda", "Toothy ambusher that strikes silver bait.", "barracuda", Rarity.Rare, FishBehavior.Aggressive, 140, 550, 280, 5, 50, 12, DayPhase.Afternoon, WeatherType.Clear, 21, 0.85f, "sardine", "minnow", "sand", "open", 0.65f, 0.2f, 0.45f, 0.2f, 1.2f, 0.65f, 1, 0.85f, 65, 45, 100, 3f, 12f, 60, 70, "slash", 0.6f, 0.7f ),
		F( "mahi", "Mahi-Mahi", "Colorful surface acrobat far from shore.", "mahi", Rarity.Rare, FishBehavior.SurfaceFeeder, 220, 700, 400, 3, 40, 8, DayPhase.Morning, WeatherType.Clear, 22, 0.9f, "sardine", "minnow", "sand", "open", 0.9f, 0.15f, 0.4f, 0.3f, 1.5f, 0.55f, 3, 0.7f, 90, 50, 120, 4f, 18f, 65, 55, "jump", 0.55f, 0.6f ),
		F( "tuna", "Tuna", "Deep-shouldered speedster of the blue water.", "tuna", Rarity.Epic, FishBehavior.ActivePredator, 300, 900, 520, 20, 140, 55, DayPhase.Sunrise, WeatherType.PartlyCloudy, 16, 0.8f, "squid", "mackerel_bait", "deep", "open", 0.35f, 0.3f, 0.35f, 0.25f, 1.45f, 0.5f, 4, 0.75f, 140, 70, 160, 12f, 45f, 100, 85, "steam", 0.7f, 0.55f ),
		F( "kingmackerel", "King Mackerel", "Long coastal rocket with sudden runs.", "kingmackerel", Rarity.Uncommon, FishBehavior.ActivePredator, 180, 500, 280, 5, 45, 15, DayPhase.GoldenHour, WeatherType.Windy, 19, 0.7f, "minnow", "sardine", "sand", "open", 0.6f, 0.25f, 0.55f, 0.2f, 1.2f, 0.6f, 2, 0.7f, 60, 55, 120, 4f, 16f, 58, 62, "run", 0.5f, 0.6f ),
		F( "swordfish", "Swordfish", "Night-hunting deep gladiator.", "swordfish", Rarity.Epic, FishBehavior.DeepWater, 450, 1100, 700, 40, 220, 90, DayPhase.Night, WeatherType.Clear, 12, 0.7f, "squid", "luminous_jelly", "deep", "open", 0.2f, 0.85f, 0.3f, 0.2f, 1.35f, 0.4f, 1, 0.8f, 220, 120, 280, 30f, 120f, 130, 110, "deep_run", 0.75f, 0.45f ),
		F( "marlin", "Marlin", "Legendary billfish of far blue water.", "marlin", Rarity.Legendary, FishBehavior.ActivePredator, 550, 1400, 900, 25, 180, 40, DayPhase.Morning, WeatherType.Clear, 18, 0.85f, "mackerel_bait", "squid", "deep", "open", 0.7f, 0.35f, 0.4f, 0.2f, 1.6f, 0.45f, 1, 0.9f, 400, 160, 340, 40f, 180f, 150, 130, "jump", 0.8f, 0.5f ),
		F( "gianttrevally", "Giant Trevally", "Brutal reef predator that never gives slack.", "gianttrevally", Rarity.Epic, FishBehavior.Aggressive, 380, 850, 560, 10, 70, 25, DayPhase.Afternoon, WeatherType.Windy, 21, 0.65f, "sardine", "squid", "rock", "reef", 0.45f, 0.25f, 0.5f, 0.35f, 1.4f, 0.55f, 1, 0.95f, 180, 80, 160, 15f, 55f, 120, 100, "bull", 0.75f, 0.55f ),
		F( "oarfish", "Oarfish", "Rare ribbon of the deep twilight.", "oarfish", Rarity.Legendary, FishBehavior.DeepWater, 700, 1600, 1100, 80, 320, 160, DayPhase.Night, WeatherType.LightFog, 8, 0.4f, "luminous_jelly", "mackerel_bait", "deep", "ravine", 0.05f, 0.9f, 0.15f, 0.4f, 1.1f, 0.3f, 1, 0.2f, 350, 200, 500, 20f, 80f, 90, 40, "glide", 0.65f, 0.35f ),
		F( "anglerfish", "Anglerfish", "Living lantern of the abyss.", "anglerfish", Rarity.Epic, FishBehavior.Nocturnal, 650, 1500, 1000, 100, 350, 200, DayPhase.Night, WeatherType.HeavyFog, 6, 0.3f, "luminous_jelly", "squid", "deep", "wreck", 0.05f, 0.95f, 0.1f, 0.35f, 1.2f, 0.25f, 1, 0.5f, 260, 40, 90, 8f, 30f, 85, 70, "lunge", 0.7f, 0.3f ),
		F( "giantsquid", "Giant Squid", "Mythic deep-sea titan. Bring your best gear.", "giantsquid", Rarity.Legendary, FishBehavior.DeepWater, 900, 2000, 1400, 150, 400, 250, DayPhase.Night, WeatherType.Thunderstorm, 4, 0.25f, "luminous_jelly", "mackerel_bait", "deep", "ravine", 0.0f, 0.8f, 0.2f, 0.5f, 1.7f, 0.35f, 1, 0.85f, 600, 250, 600, 80f, 400f, 180, 150, "thrash", 0.9f, 0.4f ),
	};

	static List<ObjectiveDefinition> BuildObjectives() => new()
	{
		new() { Id = "obj_shop", Title = "Visit the Shop", Description = "Enter the bait and tackle shop.", EventKey = "enter_shop" },
		new() { Id = "obj_bait", Title = "Ready the Bait", Description = "Equip or buy worms.", EventKey = "equip_bait" },
		new() { Id = "obj_cast", Title = "Make a Cast", Description = "Cast your line from the dock.", EventKey = "cast_line" },
		new() { Id = "obj_catch3", Title = "First Haul", Description = "Catch three fish.", EventKey = "catch_fish", Target = 3 },
		new() { Id = "obj_buy_boat", Title = "Buy a Boat", Description = "Purchase the Dinghy so you can leave the pier.", EventKey = "buy_boat" },
		new() { Id = "obj_board", Title = "Board Your Boat", Description = "Hop aboard at the dock.", EventKey = "board_boat" },
		new() { Id = "obj_travel", Title = "Leave the Dock", Description = "Travel away from the dock.", EventKey = "travel_offshore" },
		new() { Id = "obj_return", Title = "Return to Dock", Description = "Dock your boat back at port.", EventKey = "return_dock" },
		new() { Id = "obj_sell", Title = "Sell the Catch", Description = "Sell fish at the dock.", EventKey = "sell_fish" },
		new() { Id = "obj_upgrade", Title = "First Upgrade", Description = "Purchase one equipment upgrade.", EventKey = "buy_upgrade" },
	};

	static BaitDefinition B( string id, string name, string desc, string icon, int price, int bundle, float minD, float maxD, float scent, float attract, float dur, float weather, float night, float clarity, float predator, float rare, string[] primary, string[] secondary ) => new()
	{
		Id = id, Name = name, Description = desc, Icon = icon, Price = price, BundleCount = bundle,
		MinDepth = minD, MaxDepth = maxD, ScentRange = scent, AttractionSpeed = attract, Durability = dur,
		WeatherBonus = weather, NightBonus = night, ClarityBonus = clarity, PredatorBonus = predator, RareAffinity = rare,
		PrimaryTargets = primary, SecondaryTargets = secondary
	};

	static FishDefinition F(
		string id, string name, string desc, string sprite, Rarity rarity, FishBehavior behavior,
		float minDist, float maxDist, float prefDist, float minDepth, float maxDepth, float prefLure,
		DayPhase time, WeatherType weather, float temp, float clarity, string bait, string bait2,
		string seabed, string structure, float surface, float moon, float wind, float rain,
		float hotspot, float noise, int school, float aggression, int value,
		float minLen, float maxLen, float minW, float maxW, float stamina, float pull,
		string fight, float hook, float lineVis ) => new()
	{
		Id = id, Name = name, Description = desc, Sprite = sprite, Rarity = rarity, Behavior = behavior,
		MinDistance = minDist, MaxDistance = maxDist, PreferredDistance = prefDist,
		MinDepth = minDepth, MaxDepth = maxDepth, PreferredLureDepth = prefLure,
		PreferredTime = time, PreferredWeather = weather, PreferredTemp = temp, PreferredClarity = clarity,
		PreferredBait = bait, SecondaryBait = bait2, PreferredSeabed = seabed, PreferredStructure = structure,
		SurfaceAffinity = surface, MoonlightAffinity = moon, WindAffinity = wind, RainAffinity = rain,
		HotspotAffinity = hotspot, NoiseSensitivity = noise, SchoolSize = school, Aggression = aggression,
		BaseValue = value, MinLength = minLen, MaxLength = maxLen, MinWeight = minW, MaxWeight = maxW,
		Stamina = stamina, PullForce = pull, FightPattern = fight, HookDifficulty = hook, LineVisibilitySensitivity = lineVis
	};
}
