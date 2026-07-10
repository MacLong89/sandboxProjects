#nullable disable

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.TerrainGen;

namespace Sandbox;

/// <summary>
/// Host (or offline) spawns <see cref="ThornsTerrainChunk"/> with deterministic mesh rebuild on every peer from synced JSON.
/// Static world composition — terrain heights, scattered resource props, procedural buildings (macro settlement layout: one main city, three towns, wilderness isolated structures), interior crates — RNG is salted only from <see cref="TerrainSeed"/> (stored as <see cref="ThornsTerrainNetSpec.Seed"/>).
/// Use <see cref="GenerateProceduralBuildings"/>, <see cref="GenerateResourceNodes"/>, <see cref="GenerateFoliageFluff"/>, and <see cref="ScatterTerrainBoulders"/> as master inspector toggles; each feature also has a detailed sub-flag (e.g. <see cref="ScatterProceduralSites"/>).
/// Enable <see cref="RandomizeSeedOnHost"/> to roll a fresh seed each boot; wildlife and dynamic supply POIs use nondeterministic RNG elsewhere.
/// </summary>
[Title( "Thorns — Procedural terrain (system)" )]
[Category( "Thorns/World" )]
[Icon( "terrain" )]
public sealed class ThornsTerrainSystem : Component
{
	/// <summary>Proc-building roots are sunk this far so door thresholds meet terrain; zone pads must use the same offset for <see cref="ThornsTerrainProcBuildingPad.TargetZ"/>.</summary>
	public const float ProcBuildingRootVerticalDownShiftWorld = 15f;

	/// <summary>Deterministic world-generation seed heightmap/resources/buildings/interior crates (passed through <see cref="ThornsTerrainChunk"/>).</summary>
	[Property] public int TerrainSeed { get; set; } = 42069;

	[Property, Group( "Terraingen world" )] public ThornsTerrainConfig TerraingenConfig { get; set; } = new();

	[Property, Group( "Terraingen world" )] public ThornsFoliageConfig TerraingenFoliageConfig { get; set; } = new();

	[Property, Group( "Terraingen world" )] public ThornsClutterConfig TerraingenClutterConfig { get; set; } = new();

	/// <summary>
	/// Pawn camera far clip + terraingen foliage/clutter draw caps.
	/// Buildings and terrain ridges need <see cref="ThornsVisibilityTier.Balanced"/> or <see cref="ThornsVisibilityTier.Scenic"/> — Performance uses a ~300 m clip.
	/// </summary>
	[Property, Group( "Visibility" )] public ThornsVisibilityTier VisibilityTier { get; set; } = ThornsVisibilityTier.Balanced;

	/// <summary>Listen/dedicated host: pick a random integer seed at terrain spawn so layout changes every fresh world; clears when disabled so <see cref="TerrainSeed"/> is used verbatim.</summary>
	[Property] public bool RandomizeSeedOnHost { get; set; }

	/// <summary>Set when this system spawns the chunk (host only) — resolves <see cref="TerrainSeed"/> vs <see cref="RandomizeSeedOnHost"/> for debugging.</summary>
	public int ResolvedWorldGenerationSeed => _orchestration.ResolvedWorldGenerationSeed;

	readonly ThornsTerrainOrchestrationState _orchestration = new();

	/// <summary>True while deferred pre-chunk settlement/world-gen phases are still running on the host.</summary>
	public bool IsHostWorldGenPending => _orchestration.AwaitingPreChunkWorldGen;

	[Property] public float WaterLevelWorldZ { get; set; } = 88f;

	[Property] public string WaterMaterialPath { get; set; } = "materials/water.vmat";

	[Property, Title( "Water UV tiles (min; also scales with map size)" )]
	public float WaterSurfaceUvRepeat { get; set; } = 144f;

	/// <summary>Horizontal water mesh at <see cref="WaterLevelWorldZ"/> (no collider — you walk on terrain and swim through this plane).</summary>
	[Property]
	public bool EnableSeaLevelWaterSheet { get; set; } = true;

	[Property] public bool CenterTerrainOnWorldOrigin { get; set; } = true;

	/// <summary>Master toggle for procedural POI buildings + ring crates (see <see cref="ScatterProceduralSites"/>).</summary>
	[Property, Group( "World content — master switches" )]
	public bool GenerateProceduralBuildings { get; set; } = true;

	[Property, Group( "World content — master switches" )]
	public bool GenerateResourceNodes { get; set; } = true;

	[Property, Group( "World content — master switches" )]
	public bool GenerateFoliageFluff { get; set; } = true;

	[Property] public bool ScatterResourceNodes { get; set; } = true;

	/// <summary>When fluff is enabled, hides distant grass/mushroom renderers locally.</summary>
	[Property] public bool EnableFoliageDistanceCulling { get; set; } = true;

	/// <summary>Harvest wood nodes across the heightfield inset — higher reads denser forest on large <see cref="WorldWidth"/> maps.</summary>
	[Property] public int ScatterTreeCount { get; set; } = 5250;

	/// <summary>
	/// When true, wood picks placements where <see cref="ThornsWorldNoise.SampleFoliageProps01"/> is high (macro-scale woodland affinity).
	/// With <see cref="ScatterWoodForestClusterCount"/> &gt; 0, this gates <b>forest patch centers</b>; with uniform wood scatter it gates each tree.
	/// When false, placement tries are uniform on the heightfield inset (trees can appear anywhere valid).
	/// </summary>
	[Property] public bool ScatterWoodUsesBiomeNoise { get; set; }

	/// <summary>
	/// Number of forest patches (cluster anchors). Trees spawn in a disk around a random anchor — creates dense groves and natural gaps between them.
	/// Set to <b>0</b> for legacy independent random trees (same as older builds).
	/// </summary>
	[Property] public int ScatterWoodForestClusterCount { get; set; } = 108;

	/// <summary>Per-patch spawn disk — inner radius (uniform disk sampling scales tree spacing ~ with radius).</summary>
	[Property] public float ScatterWoodForestClusterRadiusMin { get; set; } = 520f;

	/// <summary>Per-patch spawn disk — outer radius; widen with same <see cref="ScatterTreeCount"/> to loosen groves.</summary>
	[Property] public float ScatterWoodForestClusterRadiusMax { get; set; } = 1680f;

	/// <summary>
	/// When true, patch centers are rejected when local height sits clearly below a ring sample (valley floors — keeps clearer valleys between forests).
	/// </summary>
	[Property] public bool ScatterWoodSkipValleyAnchors { get; set; } = true;

	/// <summary>Planar radius for neighbor height average when testing valley floors.</summary>
	[Property] public float ScatterWoodValleyNeighborRadius { get; set; } = 380f;

	/// <summary>Anchor rejected when center Z is below neighbor mean minus this (world units).</summary>
	[Property] public float ScatterWoodValleyDepthBelowNeighbors { get; set; } = 28f;

	/// <summary>
	/// Uniform scale on foliage2 wood harvest nodes. Leave at <b>0</b> for auto height from model bounds (matches terraingen tree scale).
	/// Each tree multiplies by a deterministic factor in <b>[1, 2)</b> from spawn position.
	/// </summary>
	[Property] public float ScatterWoodTreeUniformScale { get; set; }

	/// <summary>Host resource/boulder spawns per frame (spreads load hitch across frames).</summary>
	[Property] public int DeferredHostSpawnsPerFrame { get; set; } = 24;

	/// <summary>When true, pre-chunk settlement world-gen runs one phase per frame instead of blocking boot.</summary>
	[Property, Group( "Performance" )] public bool TimeSlicePreChunkWorldGen { get; set; } = true;

	/// <summary>Graphics and streaming quality preset applied on host boot.</summary>
	[Property, Group( "Performance" )] public ThornsPerformanceQuality PerformanceQuality { get; set; } = ThornsPerformanceQuality.Medium;

	/// <summary>Stone harvest nodes at a 32768×32768 reference map; scaled up on larger terraingen worlds (capped — see <see cref="MaxScatterStoneCountAfterAreaScale"/>).</summary>
	[Property] public int ScatterStoneCount { get; set; } = 1400;

	/// <summary>Metal ore harvest nodes at a 32768×32768 reference map; scaled up on larger terraingen worlds (capped — see <see cref="MaxScatterMetalOreCountAfterAreaScale"/>).</summary>
	[Property] public int ScatterMetalOreCount { get; set; } = 600;

	/// <summary>Hard cap after area scaling — prevents ~25k+ networked stone nodes on 80k terraingen maps.</summary>
	[Property] public int MaxScatterStoneCountAfterAreaScale { get; set; } = 2500;

	/// <summary>Hard cap after area scaling — keeps ore nodes proportional to stone without tanking FPS.</summary>
	[Property] public int MaxScatterMetalOreCountAfterAreaScale { get; set; } = 1070;

	[Property] public int ScatterFiberCount { get; set; }

	/// <summary>Decorative mushroom props (clustered); not harvest nodes.</summary>
	[Property] public bool ScatterMushroomFoliage { get; set; }

	[Property] public int ScatterMushroomClusterCount { get; set; } = 80;

	[Property] public int ScatterMushroomsPerClusterMin { get; set; } = 2;

	[Property] public int ScatterMushroomsPerClusterMax { get; set; } = 7;

	[Property] public float ScatterMushroomClusterRadiusMin { get; set; } = 24f;

	[Property] public float ScatterMushroomClusterRadiusMax { get; set; } = 118f;

	[Property] public float ScatterMushroomUniformScaleMin { get; set; } = 220f;

	[Property] public float ScatterMushroomUniformScaleMax { get; set; } = 460f;

	/// <summary>
	/// Extra world +Z after <see cref="ThornsFoliageScatter.AlignPivotWorldPositionMeshBottomOnGround"/> (usually leave at <b>0</b>).
	/// Previously defaulted large to sink-compensate before bottom alignment — that stacked with alignment and floated props.
	/// </summary>
	[Property] public float ScatterMushroomGroundOffset { get; set; }

	/// <summary>Logs a few world positions/sizes after scattering so visibility issues are easy to debug.</summary>
	[Property] public int ScatterMushroomDebugSampleCount { get; set; } = 0;

	[Property]
	public string ScatterMushroomModelPath { get; set; } = ThornsFoliageScatter.DefaultMushroomModelPath;

	/// <summary>Decorative grass clutter (small fluff).</summary>
	[Property] public bool ScatterGrassFoliage { get; set; } = true;

	/// <summary>Number of grass patches distributed over terrain.</summary>
	[Property] public int ScatterGrassPatchCount { get; set; } = 350;

	[Property] public int ScatterGrassPerPatchMin { get; set; } = 2;

	[Property] public int ScatterGrassPerPatchMax { get; set; } = 6;

	[Property] public float ScatterGrassPatchRadiusMin { get; set; } = 12f;

	[Property] public float ScatterGrassPatchRadiusMax { get; set; } = 64f;

	[Property] public float ScatterGrassUniformScaleMin { get; set; } = 18f;

	[Property] public float ScatterGrassUniformScaleMax { get; set; } = 42f;

	[Property] public float ScatterGrassGroundOffset { get; set; } = 5f;

	/// <summary>
	/// Decor grass model path. When <see cref="ScatterGrassVariantCount"/> is 1, loads <c>{prefix}.vmdl</c> (default <see cref="ThornsFoliageScatter.ClutterGrassDecorPrefix"/>).
	/// With count &gt; 1, loads numbered <c>{prefix}1.vmdl</c> … <c>{prefix}N.vmdl</c>.
	/// <c>grass_common_short</c> is not scattered here — use terraingen <see cref="Terraingen.Clutter.ClientGrassRenderer"/> only.
	/// </summary>
	[Property] public string ScatterGrassModelPathPrefix { get; set; } = ThornsFoliageScatter.ClutterGrassDecorPrefix;

	/// <summary>Single clutter grass blade mesh by default; raise for legacy numbered <c>grassN</c> variants.</summary>
	[Property] public int ScatterGrassVariantCount { get; set; } = 1;

	[Property] public int ScatterGrassDebugSampleCount { get; set; } = 0;

	/// <summary>Large decorative rocks (<c>models/clutter/rock1–2.vmdl</c>) — runs after resources + foliage so placements avoid trees/nodes/fluff.</summary>
	[Property, Group( "World content — terrain boulders" )]
	public bool ScatterTerrainBoulders { get; set; } = true;

	[Property, Group( "World content — terrain boulders" )]
	public int ScatterBoulderCount { get; set; } = 96;

	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderMinSeparation { get; set; } = 280f;

	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderResourceClearance { get; set; } = 165f;

	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderFoliageClearance { get; set; } = 85f;

	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderUniformScaleMin { get; set; } = 260f;

	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderUniformScaleMax { get; set; } = 440f;

	/// <summary>Reject when corner samples of the rock footprint exceed this height delta (world units).</summary>
	[Property, Group( "World content — terrain boulders" )]
	public float ScatterBoulderMaxSlopeDelta { get; set; } = 42f;

	[Property, Group( "World content — terrain boulders" )]
	public int ScatterBoulderMaxAttemptsPerRock { get; set; } = 52;

	[Property] public bool ScatterLootCrates { get; set; } = true;

	[Property] public int ScatterLootCrateCount { get; set; } = 240;

	[Property] public bool ScatterProceduralSites { get; set; } = true;

	/// <summary>Legacy total — superseded by settlement layout counts below (kept for old scenes).</summary>
	[Property] public int ProceduralSiteCount { get; set; } = 28;

	[Property] public float ProceduralSiteMinSeparation { get; set; } = 2350f;

	/// <summary>Fixed manifest: 12 city + 15 town + 3 isolated = 30 buildings (see <see cref="ThornsWorldSettlementPlan.TotalBuildingCount"/>).</summary>
	[Property, Group( "World settlement layout" )]
	public float SettlementMainCityRadiusFraction { get; set; } = 0.065f;

	[Property, Group( "World settlement layout" )]
	public float SettlementTownRadiusFraction { get; set; } = 0.052f;

	/// <summary>
	/// Organic map scatter: buildings cluster into towns, full footprint separation, natural terrain.
	/// </summary>
	[Property, Group( "World settlement layout" )]
	public bool OrganicClusterPlacement { get; set; } = true;

	[Property, Group( "World settlement layout" )]
	public int OrganicBuildingCount { get; set; } = 30;

	[Property, Group( "World settlement layout" )]
	public float OrganicClusterBias { get; set; } = 0.86f;

	/// <summary>Minimum edge gap between building footprints (in <see cref="ThornsBuildingModule.Cell"/> units).</summary>
	[Property, Group( "World settlement layout" )]
	public float OrganicBuildingBufferCells { get; set; } = 1.45f;

	/// <summary>Legacy offset — prefer per-building terrain scrape pads; keep at 0 unless debugging z-fight.</summary>
	[Property, Group( "World settlement layout" )]
	public float OrganicBuildingVerticalLift { get; set; }

	/// <summary>
	/// When true: legacy city/town zones with optional block layout (requires <see cref="OrganicClusterPlacement"/> false).
	/// </summary>
	[Property, Group( "World settlement layout" )]
	public bool TerrainFirstClusterPlacement { get; set; } = false;

	[Property, Group( "World settlement layout" )]
	public bool DrawSettlementLayoutDebug { get; set; }

	/// <summary>Colorize proc buildings + minimap blips by <see cref="ThornsProcBuildingType"/> (worldgen tuning).</summary>
	[Property, Group( "World settlement layout" )]
	public bool DebugBuildingTypeColors { get; set; } = true;

	[Property, Group( "World content — roads" )]
	public float RoadCityFlattenStrength { get; set; } = 0.74f;

	[Property, Group( "World content — roads" )]
	public float RoadTownFlattenStrength { get; set; } = 0.56f;

	[Property, Group( "World content — roads" )]
	public float RoadTrailFlattenStrength { get; set; } = 0.26f;

	[Property, Group( "World content — roads" )]
	public float RoadCityEdgeFalloff { get; set; } = 52f;

	[Property, Group( "World content — roads" )]
	public float RoadTownEdgeFalloff { get; set; } = 38f;

	[Property, Group( "World content — roads" )]
	public float RoadTrailEdgeFalloff { get; set; } = 30f;

	[Property, Group( "World content — roads" )]
	public float RoadFoliageClearanceRadius { get; set; } = 44f;

	[Property, Group( "World content — roads" )]
	public float RoadBoulderClearanceRadius { get; set; } = 36f;

	[Property] public int LootCratesPerSiteMin { get; set; } = 3;

	[Property] public int LootCratesPerSiteMax { get; set; } = 7;

	/// <summary>Per occupied floor in a proc building: chance to spawn one interior loot crate (0–1).</summary>
	[Property, Range( 0f, 1f )] public float InteriorLootCrateFloorChance { get; set; } = 0.30f;

	[Property] public bool ScatterInteriorFurniture { get; set; } = true;

	[Property] public int InteriorFurnitureMinPerFloor { get; set; } = 3;

	[Property] public int InteriorFurnitureMaxPerFloor { get; set; } = 3;

	[Property] public bool ScatterCityDefenders { get; set; } = true;

	/// <summary>Per floor: chance to spawn a city defender inside that proc building (0–1).</summary>
	[Property, Range( 0f, 1f )] public float InteriorCityDefenderFloorChance { get; set; } = 0.01f;

	[Property] public int InteriorCityDefenderMaxPerBuilding { get; set; } = 2;

	[Property] public float AmbientLootMinDistanceFromSite { get; set; } = 2100f;

	/// <summary>
	/// Guaranteed spawns for crafting/salvage and hunter-themed crates (in addition to <see cref="ThornsLootGenerator.PickRandomKind"/> ambient rolls).
	/// </summary>
	[Property] public bool ScatterThemeCrates { get; set; } = true;

	[Property] public int ScatterSalvageCrateCount { get; set; } = 84;

	[Property] public int ScatterHunterCrateCount { get; set; } = 84;

	// Shrink scatter placement toward the map interior so props avoid cliff edges (0–0.45).
	[Property] public float ScatterEdgeInsetFraction { get; set; } = 0.06f;

	internal static bool IsSpawnableLandHeight( in ThornsTerrainNetSpec spec, float localHeightZ ) =>
		localHeightZ >= spec.WaterLevelWorldZ;

	public static float CombinedPlanarYawRadians( Rotation chunkWorldRot, float localYawDegrees ) =>
		(chunkWorldRot * Rotation.FromYaw( localYawDegrees )).Angles().yaw * (MathF.PI / 180f);

	internal void RebuildProcBuildingFootprintIndex() =>
		ThornsWorldScatterService.RebuildProcBuildingFootprintIndex( _orchestration );

	internal void AdoptWorldGenScatterHeightmap( ThornsWorldGenerationContext context ) =>
		ThornsTerrainHeightmapService.AdoptWorldGenScatterHeightmap( _orchestration, context );

	internal void RentScatterHeightmapOrFill( ThornsTerrainNetSpec spec, out float[] heights, out int cells ) =>
		ThornsTerrainHeightmapService.RentScatterHeightmapOrFill( in spec, out heights, out cells );

	internal float ChunkLocalFoundationBottomZ( Vector3 buildingRootWorldPos )
	{
		var ft = ThornsBuildingModule.FloorThickness;
		var slabBottomWorld = buildingRootWorldPos - Vector3.Up * (ft * 0.5f);
		var local = _orchestration.ChunkRoot.WorldRotation.Inverse * (slabBottomWorld - _orchestration.ChunkRoot.WorldPosition);
		return local.z;
	}

	internal void CopyRoadTuningToSpec( ThornsTerrainNetSpec spec ) =>
		ThornsTerrainChunkLifecycleService.CopyRoadTuningToSpec( this, spec );

	internal void PushSpecToChunk( ThornsTerrainNetSpec spec ) =>
		ThornsTerrainChunkLifecycleService.PushSpecToChunk( _orchestration, spec );

	/// <summary>
	/// Resolves sea-level Z in world space from the first enabled <see cref="ThornsTerrainSystem" /> (matches the water sheet / swim plane).
	/// </summary>
	public static bool TryResolveWaterPlaneWorldZ( Scene scene, out float waterPlaneWorldZ )
	{
		waterPlaneWorldZ = 0f;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			waterPlaneWorldZ = ts.WorldPosition.z + ts.WaterLevelWorldZ;
			return true;
		}

		return false;
	}

	/// <summary>
	/// True when terrain at <paramref name="worldTerrainSurfaceZ"/> is above the resolved water sheet (plus clearance).
	/// Use world-space Z of the ground under the spawn (after snap), not heightmap-only values. If no water plane exists in the scene, returns true.
	/// </summary>
	public static bool IsWorldTerrainSurfaceDryAccessible( Scene scene, float worldTerrainSurfaceZ, float clearanceAboveWaterWorldZ = 8f )
	{
		if ( !TryResolveWaterPlaneWorldZ( scene, out var waterZ ) )
			return true;

		return worldTerrainSurfaceZ >= waterZ + clearanceAboveWaterWorldZ;
	}

	/// <summary>
	/// True when the pawn root is close enough to the resolved sea level to drink (swimming, wading, or shoreline).
	/// Matches the water sheet / <see cref="ThornsPawnMovement"/> swim threshold in spirit; uses a modest band above the plane for shore sips.
	/// </summary>
	public static bool IsPawnInOpenWaterDrinkZone( Scene scene, GameObject pawnRoot, out float waterPlaneWorldZ )
	{
		waterPlaneWorldZ = 0f;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !TryResolveWaterPlaneWorldZ( scene, out waterPlaneWorldZ ) )
			return false;

		const float maxAboveWaterPlaneZ = 72f;
		return pawnRoot.WorldPosition.z <= waterPlaneWorldZ + maxAboveWaterPlaneZ;
	}

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		ThornsPerformanceQualityPresets.ApplyToTerrainSystem( this, PerformanceQuality );
		ThornsPerfDebugHost.EnsureOn( GameObject );

		ThornsHostWorldGenHandoff.ApplyOneShotIfArmed( this );

		ThornsTerrainVisibilityService.ApplyBootVisibility( Scene, VisibilityTier );
		ThornsTerrainVisibilityService.EnsureFoliageDistanceCuller( this, EnableFoliageDistanceCulling, GenerateFoliageFluff );

		ThornsTerrainChunkLifecycleService.TrySpawnChunk( this, _orchestration );
		ThornsWorldScatterService.HostRefreshInteriorFurnitureScalesIfNeeded( this, _orchestration );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		ThornsWorldGenRunnerService.TickDeferredPreChunkWorldGen( this, _orchestration );
	}

	internal void RunEnvironmentScatter( ThornsTerrainNetSpec spec ) =>
		ThornsWorldScatterService.RunEnvironmentScatter( this, _orchestration, spec );

	internal void RunInteriorLootScatter( ThornsTerrainNetSpec spec ) =>
		ThornsWorldScatterService.RunInteriorLootScatter( this, _orchestration, spec );

	internal void RunInteriorFurnitureScatter( ThornsTerrainNetSpec spec ) =>
		ThornsWorldScatterService.RunInteriorFurnitureScatter( this, _orchestration, spec );

	internal void RunInteriorCityDefenderScatter( ThornsTerrainNetSpec spec ) =>
		ThornsWorldScatterService.RunInteriorCityDefenderScatter( this, _orchestration, spec );

	internal void HostRefreshInteriorFurnitureScalesIfNeeded( bool commitRevisionWhenNoProps = false ) =>
		ThornsWorldScatterService.HostRefreshInteriorFurnitureScalesIfNeeded( this, _orchestration, commitRevisionWhenNoProps );

	internal ThornsWorldSettlementConfig BuildSettlementConfig( float worldWidth, float worldDepth )
	{
		const float refArea = 32768f * 32768f;
		var scale = MathF.Sqrt( Math.Max( 1f, worldWidth * worldDepth ) / refArea );
		scale = Math.Clamp( scale, 0.55f, 1.75f );

		return new ThornsWorldSettlementConfig
		{
			MainCityRadiusFraction = SettlementMainCityRadiusFraction * scale,
			TownRadiusFraction = SettlementTownRadiusFraction * scale,
			TownOrbitFractionMin = 0.4f,
			TownOrbitFractionMax = 0.58f,
			MinCityTownSeparationFraction = 0.28f,
			MinTownTownSeparationFraction = 0.22f,
			IsolatedSettlementClearanceFraction = 0.2f
		};
	}

	protected override void OnDestroy() =>
		ThornsTerrainChunkLifecycleService.DestroyChunk( _orchestration );
}
