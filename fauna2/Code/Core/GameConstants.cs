namespace Fauna2;

/// <summary>
/// Central tuning values for the vertical slice. Anything gameplay-feel related
/// lives here so designers can iterate without hunting through systems.
/// </summary>
public static class GameConstants
{
	// ── Economy ─────────────────────────────────────────────
	public const int StartingMoney = 8_500;
	/// <summary>Guest spending before satisfaction and difficulty modifiers (see GuestRevenue).</summary>
	public const float IncomePerGuestPerSecond = 0.052f;
	/// <summary>Default guest revenue multiplier when settings have no override.</summary>
	public const float DefaultGuestRevenueMultiplier = 1.45f;

	// Recurring operating costs ($/min). Early zoos run negative until guest revenue scales.
	public const float BaseOperatingCostPerMinute = 145f;
	public const float OperatingCostPerAnimalPerMinute = 13f;
	public const float OperatingCostPerHabitatPerMinute = 36f;
	public const float OperatingCostPerPathPerMinute = 1.4f;
	public const float OperatingCostPerUtilityPerMinute = 24f;
	public const float OperatingCostPerEntrancePerMinute = 10f;
	public const float OperatingCostPerDecorationPerMinute = 1.8f;
	public const float OperatingCostPerNaturePerMinute = 1f;
	public const float OperatingCostPerGuestPerMinute = 0.38f;
	public const float OperatingCostPerPlotPerMinute = 9f;

	public const float DemolishRefundFraction = 0.5f;
	public const float SellAnimalRefundFraction = 0.6f;
	public const int VisitorGiftAmount = 250;

	/// <summary>World grid step for trees/rocks.</summary>
	public const float ObstacleCellSize = TileSize * 7f;
	public const float ObstacleBlockRadius = 80f;
	/// <summary>How close the cursor ground point can be to pick a tree/rock.</summary>
	public const float ObstaclePickRadius = 180f;
	/// <summary>
	/// How close the player must stand (feet → cell center) to clear a tree/rock.
	/// Larger than pick radius: sprites/colliders are big and click-select can
	/// target obstacles whose centers sit well past 180u.
	/// </summary>
	public const float ObstacleClearRadius = ObstacleCellSize * 0.75f;
	public const float ObstacleClearSeconds = 3f;
	public const int ObstacleClearReward = 35;
	public const int XpClearObstacle = 10;

	// ── Plots / expansion ───────────────────────────────────
	/// <summary>World size of one land plot (starter plot is a single cell).</summary>
	public const float PlotSize = 6144f;
	public const int PlotGridRadius = 2;            // 5x5 grid of purchasable plots
	public const int PlotBaseCost = 4_000;
	public const float PlotCostGrowth = 1.55f;
	public const int AnimalsPerPlot = 12;
	public const int GuestsPerPlot = 250;

	/// <summary>Half-width of the purchasable plot grid in world units.</summary>
	public static float PlayableHalfExtent => PlotSize * PlotGridRadius + PlotSize * 0.5f;

	/// <summary>Half-width of the rendered world including wilderness buffer and cliffs.</summary>
	public static float WorldHalfExtent => PlotSize * (PlotGridRadius + 1.5f);

	/// <summary>How far the camera focus may pan from the origin — reaches the wilderness rim.</summary>
	public static float CameraPanHalfExtent => PlotSize * (PlotGridRadius + 1.0f);

	/// <summary>Farthest zoom-out so the bordered world fills the view instead of empty void.</summary>
	public const float CameraMaxZoomDistance = PlotSize * (PlotGridRadius + 1.05f) * 1.2f;

	public const float PathConnectDistance = 150f;
	/// <summary>Outer owned band where scatter props appear — matches WorldEnvironment inset.</summary>
	public const float OwnedOutskirtsBand = TileSize * 6f;
	public const float EntranceEdgeBand = OwnedOutskirtsBand;
	/// <summary>Entrance footprint — 2 tiles deep × 3 tiles wide (same snap as utility buildings).</summary>
	public static Vector2 EntranceFootprint => StandardBuildingFootprint;
	/// <summary>Guest amenities may touch a path (0 gap) or sit one build cell away.</summary>
	public const int AmenityPathGapTiles = 1;
	/// <summary>Guests one restroom comfortably supports (higher = fewer required).</summary>
	public const int GuestsPerRestroom = 50;
	/// <summary>Guests one restaurant/food stand comfortably supports.</summary>
	public const int GuestsPerRestaurant = 60;
	/// <summary>Guests one shop/kiosk comfortably supports.</summary>
	public const int GuestsPerShop = 140;
	public const float RestaurantCollectIncomePerMinute = 52f;
	public const float ShopCollectIncomePerMinute = 48f;
	public const float RestaurantMaxStored = 900f;
	/// <summary>Click radius for collecting from a restaurant with the RTS camera.</summary>
	public const float RestaurantPickRadius = 160f;
	public const int StaffHireCost = 850;
	public const int StaffCostPerMinute = 22;
	public const int ResearchBaseCost = 1_200;
	public const int ResearchCostGrowth = 900;
	public const int ResearchMaxRank = 5;
	public const float SanctuaryDaySeconds = 600f;
	public const int SeasonLengthDays = 7;

	/// <summary>Global slowdown — 2/3 ≈ 33% slower guests, economy, hunger, breeding, and timed events.</summary>
	public const float GamePaceMultiplier = 2f / 3f;

	public static float AtGamePace( float ratePerSecond ) => ratePerSecond * GamePaceMultiplier;

	public static float GamePaceDuration( float seconds ) => seconds / GamePaceMultiplier;

	public static float ObstacleClearDuration => GamePaceDuration( ObstacleClearSeconds );

	public static float BreedCooldownDuration => GamePaceDuration( BreedCooldownSeconds );

	public static float SanctuaryDayDuration => GamePaceDuration( SanctuaryDaySeconds );

	public static float WildAnimalRespawnDuration => GamePaceDuration( WildAnimalRespawnSeconds );

	public static float WildAnimalTopUpDuration => GamePaceDuration( WildAnimalTopUpInterval );

	public static float BreedScanDuration => GamePaceDuration( BreedScanInterval );

	// ── Stardew-style world ─────────────────────────────────
	public const float TileSize = 64f;
	/// <summary>Coarse ground texture cell (4×4 build tiles). Half of the old 8×8 cells for finer biome edges.</summary>
	public const float GroundTileSize = TileSize * 4f;
	/// <summary>Each ground sprite spans this many logical ground cells (2 = 512 world units, ~4× fewer sprites).</summary>
	public const int GroundRenderChunkCells = 2;
	public static float GroundRenderTileSize => GroundTileSize * GroundRenderChunkCells;
	public static int BuildTilesPerGroundTile => (int)(GroundTileSize / TileSize);
	/// <summary>Viewport padding when enabling ground tiles (load before visible).</summary>
	public const float GroundCullEnableMargin = 1.72f;
	/// <summary>Smaller padding for disabling — hysteresis prevents pop-in when panning/zooming.</summary>
	public const float GroundCullDisableMargin = 1.48f;
	/// <summary>Extra chunk rings beyond the padded viewport to preload ground.</summary>
	public const int GroundCullPreloadChunkRing = 2;
	public const int GroundCullRetainChunkRing = 1;
	public const float GroundCullChunkSize = 2048f;
	public const float PlayerSpriteTiles = 3f;
	/// <summary>Average adult human height — player sprite scale is anchored to this.</summary>
	public const float HumanReferenceHeightMeters = 1.7f;
	public const float PlayerWalkTilesPerSecond = 4f;
	public const float PlayerRunTilesPerSecond = 6.2f;
	public const float AnimalSpriteBaseTiles = 1.4f;
	/// <summary>Global multiplier applied to all animal sprite and pick-collider sizes.</summary>
	public const float AnimalScaleMultiplier = 2f;
	public const float TreeSpriteTiles = 6.5f;
	public const float BushSpriteTiles = 1.5f;
	public const float RockSpriteTiles = 1.35f;
	public const float DecorationSpriteTiles = 1.5f;
	/// <summary>One fence rail sprite spans this many path tiles (128 world units).</summary>
	public const float FenceSegmentTiles = 2f;
	/// <summary>Smallest utility building footprint — 4×4 build tiles.</summary>
	public const float MinBuildingTiles = 4f;
	public static float MinBuildingFootprint => Tiles( MinBuildingTiles );
	/// <summary>Default utility footprint — 6 tiles wide (Y) × 4 tiles deep (X, screen height).</summary>
	public const float StandardBuildingWidthTiles = 6f;
	public const float StandardBuildingDepthTiles = 4f;
	public static Vector2 StandardBuildingFootprint =>
		new( Tiles( StandardBuildingDepthTiles ), Tiles( StandardBuildingWidthTiles ) );
	public const float InteractionRange = TileSize * 2f;
	/// <summary>Wild animals are easier to start a catch on while roaming.</summary>
	public static float WildAnimalInteractRange => InteractionRange * 1.35f;
	/// <summary>Distance where dangerous wild animals can start a fend-off encounter.</summary>
	public static float WildAnimalAttackRange => TileSize * 2.4f;
	public const float WildAnimalAttackCheckInterval = 0.65f;
	public const float WildAnimalAttackCooldownSeconds = 18f;
	public const float WildAnimalAttackBasePenaltyFraction = 0.12f;
	public const int WildAnimalAttackMinPenalty = 150;
	public const int WildAnimalAttackMaxPenalty = 2500;
	public const float PlayerWalkSpeed = TileSize * PlayerWalkTilesPerSecond;
	public const float PlayerRunMultiplier = PlayerRunTilesPerSecond / PlayerWalkTilesPerSecond;
	public const float PlayerRadius = TileSize * 0.28f;
	public const float PlayerHeight = TileSize;
	public const float CameraYaw = 0f;
	public const float CameraPitch = 68f;
	public const float CameraOrthoHeight = 3800f;
	public const float CameraMinOrthoHeight = 1200f;
	/// <summary>Max zoom-out — quarter of the old full-world view so the camera stays closer.</summary>
	public const float CameraMaxOrthoHeight = PlotSize * 3.5f * 1.08f * 0.25f;

	public static float Tiles( float count ) => TileSize * count;

	public static string FormatTiles( float worldUnits ) => $"{worldUnits / TileSize:0.##}";

	public static string FormatTiles( Vector2 worldSize ) =>
		$"{FormatTiles( worldSize.x )}x{FormatTiles( worldSize.y )}";

	// ── Progression ─────────────────────────────────────────
	public const int MaxLevel = 10;
	public const int XpPlaceHabitat = 15;
	public const int XpPlaceDecoration = 5;
	public const int XpPlacePath = 3;
	public const int XpBuyAnimal = 25;
	public const int XpCatchAnimal = 35;
	public const int XpBreedAnimal = 60;
	public const int XpDiscoverSpecies = 100;
	public const int XpDiscoverVariant = 150;
	public const int XpBuyPlot = 200;
	public const float XpPerDollarEarned = 1f / 20f;

	public const int PrestigeSpeciesDiscovered = 5;
	public const int PrestigeVariantDiscovered = 15;
	public const int PrestigeLevelUp = 2;
	public const int PrestigePlotPurchased = 3;

	// ── Animals ─────────────────────────────────────────────
	public const float HungerDecayPerSecond = 0.20f;
	public const float EatRestorePerSecond = 6f;
	public const float HungerSeekFoodThreshold = 35f;
	public const float BabyScale = 0.45f;
	/// <summary>Global multiplier on habitat and wild animal locomotion speeds (40% slower than 3.6 baseline).</summary>
	public const float AnimalMoveSpeedMultiplier = 2.16f;
	/// <summary>Extra multiplier for wilderness roaming so field animals visibly patrol at camera scale.</summary>
	public const float WildAnimalRoamSpeedMultiplier = 1.12f;
	public const float WildAnimalRetargetMinSeconds = 7f;
	public const float WildAnimalRetargetMaxSeconds = 13f;
	public const float WildAnimalRoamRadiusMinTiles = 8f;
	public const float WildAnimalRoamRadiusMaxTiles = 18f;
	/// <summary>Multiplies lateral hip offset so procedural legs tuck under the body mesh.</summary>
	public const float AnimalStanceScale = 0.72f;

	// ── Breeding ────────────────────────────────────────────
	public const float BreedScanInterval = 12f;
	public const float BreedBaseChance = 0.35f;
	public const float BreedCooldownSeconds = 240f;
	public const float BreedMinHabitatScore = 55f;
	public const float VariantChanceWild = 0.02f;
	public const float VariantChanceBred = 0.08f;
	public const float RecessiveExpressionChance = 0.12f;

	// ── Guests ──────────────────────────────────────────────
	public const float GuestTickInterval = 2f;
	public const float GuestLerpRate = 0.06f;        // fraction toward target per tick
	public const float VarietyAppealPerSpecies = 15f;
	/// <summary>Cosmetic guest sprites shown per simulated guest (e.g. 10 → one sprite per ten guests).</summary>
	public const int GuestsPerAmbientVisual = 10;
	public const int MaxAmbientGuestVisuals = 40;

	// ── Ticking / saving ────────────────────────────────────
	public const float HabitatRescoreInterval = 5f;
	public const float AnimalThinkInterval = 0.5f;
	public const int MaxAnimalThinksPerTick = 32;
	public const float AutosaveInterval = 90f;
	public const float AutosaveEventDelay = 2f;

	// ── Wilderness / catch ──────────────────────────────────
	/// <summary>After a failed catch, the sprite hides then reappears on the same spot.</summary>
	public const float WildAnimalRespawnSeconds = 35f;
	/// <summary>How many wild animals may share one wilderness plot.</summary>
	public const int WildAnimalsPerPlotCap = 5;
	/// <summary>Initial wilderness fill per plot (non-border).</summary>
	public const int WildAnimalsPerPlotSpawnMin = 2;
	public const int WildAnimalsPerPlotSpawnMax = 4;
	/// <summary>Extra initial spawns on wilderness plots touching owned land.</summary>
	public const int WildAnimalsBorderPlotBonus = 2;
	/// <summary>Extra spawns on outer wilderness biome hotspots (forest/grassland/swamp/arctic rings).</summary>
	public const int WildAnimalsBiomeHotspotBonus = 3;
	/// <summary>Wild animal cap on biome hotspot plots.</summary>
	public const int WildAnimalsPerPlotCapHotspot = 7;
	/// <summary>Host tops up under-cap wilderness plots on this interval.</summary>
	public const float WildAnimalTopUpInterval = 8f;
	public const int WildAnimalTopUpMin = 1;
	public const int WildAnimalTopUpMax = 2;
	/// <summary>Hard cap on live wild animals in the world — keeps FPS stable.</summary>
	public const int WildAnimalsWorldMax = 48;
	/// <summary>Viewport padding when deciding which wilderness plots may spawn wildlife.</summary>
	public const float WildAnimalViewportMargin = 1.35f;
	public const int MaxCarryAnimals = 2;
	public const int BaitCost = 15;
	public const int BaitPackSize = 5;
	public const int NetCost = 50;
	public const int TranquilizerCost = 80;

	public const string SaveDirectory = "fauna2/saves";
	public const string LegacySaveFile = "fauna2/zoo.json";
	public const int SaveSlotCount = 4;

	/// <summary>Soft cap on placed objects (paths, buildings, etc.) to avoid engine limits.</summary>
	public const int MaxPlaceables = 512;

	public static string SaveSlotPath( int slotId ) => $"{SaveDirectory}/slot_{slotId}.json";
}
