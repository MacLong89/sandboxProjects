namespace Terraingen.Clutter;

using Terraingen.Minerals;

/// <summary>
/// Client-side cosmetic ground clutter tuning.
/// Distances are in Source/s&box inches. 50m is roughly 1968 inches (39.37 in/m).
/// </summary>
public sealed class ThornsClutterConfig
{
	public const float InchesPerMeter = 39.37f;

	// 10 meters in Source inches — default clutter bubble radius.
	public const float TenMetersInches = 1968f / 5f;

	[Property, Group( "Streaming" )] public bool Enabled { get; set; } = true;

	[Property, Group( "Streaming" ), Title( "Place local grass mesh (grass_common_short)" )]
	public bool PlaceGrass { get; set; } = true;

	[Property, Group( "Streaming" ), Title( "Place decorative rock clutter" )]
	public bool PlaceRocks { get; set; }

	[Property, Group( "Streaming" ), Range( 0.000001f, 1.5f ), Title( "Rock clutter budget (rocks-only mode)" )]
	public float RockBudgetMultiplier { get; set; } = 0.00008f;
	[Property, Group( "Streaming" )] public int WorldSeed { get; set; } = 42069;
	[Property, Group( "Streaming" ), Range( 128f, 4096f )] public float ClutterRadius { get; set; } = 70f * InchesPerMeter;
	[Property, Group( "Streaming" ), Range( 256f, 2048f )] public float ChunkSize { get; set; } = 400f;
	[Property, Group( "Streaming" ), Range( 16, 10240 )] public int MaxInstancesPerChunk { get; set; } = 6400;

	[Property, Group( "Streaming" ), Title( "GPU-instanced mesh clutter (grass/ferns/rocks)" )]
	public bool UseInstancedMeshDetail { get; set; } = true;

	[Property, Group( "Streaming" ), Range( 4, 256 ), Title( "Mesh detail instances per chunk (uniform)" )]
	public int DetailInstancesPerChunk { get; set; } = 144;

	[Property, Group( "Streaming" ), Range( 0f, 6f )] public float DensityMultiplier { get; set; } = 8f;
	[Property, Group( "Streaming" ), Range( 1f, 12f ), Title( "Grass density multiplier" )]
	public float GrassDensityMultiplier { get; set; } = 14f;

	[Property, Group( "Streaming" ), Range( 1f, 8f ), Title( "Grass instance target multiplier (legacy — use DetailInstancesPerChunk)" )]
	public float GrassInstanceMultiplier { get; set; } = 7f;

	[Property, Group( "Streaming" ), Range( 0.05f, 0.85f ), Title( "Minimum biome accept rate for mesh detail" )]
	public float MinDetailAcceptRate { get; set; } = 0.24f;

	[Property, Group( "Streaming" ), Range( 1f, 4f ), Title( "Near-player grass density boost (unused — kept for scenes)" )]
	public float NearPlayerGrassBoost { get; set; } = 1f;

	[Property, Group( "Streaming" ), Range( 1, 4 ), Title( "Grass blades per accepted placement" )]
	public int GrassPlacementClusterSize { get; set; } = 2;

	[Property, Group( "Streaming" ), Range( 0.05f, 0.65f ), Title( "Rock instance mix (legacy — unused, models pick evenly)" )]
	public float RockInstanceMix { get; set; } = 0.22f;

	[Property, Group( "Streaming" ), Range( 0f, 0.85f ), Title( "Local grass mix (legacy — unused, models pick evenly)" )]
	public float LocalGrassDetailMix { get; set; } = 0.28f;
	[Property, Group( "Streaming" ), Range( 0f, 4096f )] public float DistanceFadeStart { get; set; } = 48f * InchesPerMeter;
	[Property, Group( "Streaming" ), Range( 0f, 4096f )] public float DistanceFadeEnd { get; set; } = 70f * InchesPerMeter;

	[Property, Group( "Streaming" ), Range( 1, 16 ), Title( "Chunks built per refresh tick" )]
	public int ChunksBuiltPerRefresh { get; set; } = 6;

	[Property, Group( "Streaming" ), Range( 0, 2 ), Title( "Preload chunk rings beyond clutter radius" )]
	public int PreloadChunkRings { get; set; } = 1;

	[Property, Group( "Streaming" ), Title( "Fade in newly placed clutter instances" )]
	public bool EnableInstanceReveal { get; set; } = true;

	[Property, Group( "Streaming" ), Range( 0.05f, 2f ), Title( "Instance reveal duration (seconds)" )]
	public float InstanceRevealDuration { get; set; } = 0.45f;

	[Property, Group( "Streaming" ), Range( 0f, 1.5f ), Title( "Random reveal stagger near player (seconds)" )]
	public float InstanceRevealSpread { get; set; } = 0.3f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float SlopeReject { get; set; } = 0.38f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinHeightNormalized { get; set; } = 0.07f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxHeightNormalized { get; set; } = 0.92f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float MeadowDensity { get; set; } = 2.1f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float ForestDensity { get; set; } = 0.95f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float AlpineDensity { get; set; } = 0.95f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float SlopeRockDensity { get; set; } = 1.2f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinRockChance { get; set; } = 0.000256f;

	[Property, Group( "Models" )] public string GrassModel { get; set; } = "models/clutter/grass_common_short.vmdl";

	[Property, Group( "Models" ), Title( "Optional extra mesh detail model paths (local assets only)" )]
	public string[] CloudDetailModels { get; set; } = Array.Empty<string>();

	[Property, Group( "Models" ), Title( "Scatter extra mesh detail models as clutter" )]
	public bool PlaceCloudDetail { get; set; }

	[Property, Group( "Models" ), Range( 1f, 8f ), Title( "Stylized grass spawn multiplier (additive — fern/lavender unchanged)" )]
	public float StylizedGrassSpawnMultiplier { get; set; } = 2f;
	[Property, Group( "Models" ), Title( "Decorative rock model (stone harvest node mesh)" )]
	public string RockModelA { get; set; } = ThornsMineralConfig.DefaultScatterModel;
	[Property, Group( "Models" ), Title( "Optional second rock variant (empty = stone node only)" )]
	public string RockModelB { get; set; } = "";

	[Property, Group( "Scale" ), Range( 0.1f, 8f ), Title( "Global scale multiplier (all clutter)" )]
	public float GlobalScaleMultiplier { get; set; } = 1.5f;

	[Property, Group( "Scale" ), Title( "Grass target height (inches)" )]
	public float GrassTargetHeightInches { get; set; } = 9f;

	[Property, Group( "Scale" ), Range( 0.1f, 500f ), Title( "Grass scale multiplier" )]
	public float GrassScaleMultiplier { get; set; } = 1f;

	[Property, Group( "Scale" ), Range( 0.05f, 32f ), Title( "Grass max uniform scale (safety clamp)" )]
	public float GrassMaxUniformScale { get; set; } = 4f;

	[Property, Group( "Scale" ), Title( "Foliage surface offset above terrain (inches)" )]
	public float GrassSurfaceOffset { get; set; }

	[Property, Group( "Scale" ), Title( "Foliage embed below surface (inches)" )]
	public float GrassGroundEmbedInches { get; set; }

	[Property, Group( "Scale" ), Title( "Rock target height (inches, 1/3 of harvest stone node)" )]
	public float RockTargetHeightInches { get; set; } = 40f / 3f;

	[Property, Group( "Scale" ), Range( 0.1f, 500f ), Title( "Rock scale multiplier (matches mineral stone nodes)" )]
	public float RockScaleMultiplier { get; set; } = 0.64f;

	[Property, Group( "Scale" ), Title( "Rock surface offset (inches)" )]
	public float RockSurfaceOffset { get; set; } = 4f;

	[Property, Group( "Scale" ), Title( "Rock embed below terrain (inches)" )]
	public float RockGroundEmbedInches { get; set; } = 6f;

	[Property, Group( "Scale" ), Range( 0f, 1f ), Title( "Rock pivot lift (center pivots)" )]
	public float RockPivotLiftFraction { get; set; } = 0.35f;

	[Property, Group( "Scale" ), Range( 0f, 20f )] public float RandomTiltDegrees { get; set; } = 5f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" ), Title( "Log mesh clutter model pick/placement mix" )]
	public bool LogDetailMixDebug { get; set; }
	[Property, Group( "Debug" )] public bool DrawChunkBounds { get; set; } = false;
	[Property, Group( "Debug" )] public bool DrawDensitySamples { get; set; } = false;

	[Property, Group( "Client Grass" ), Title( "GPU grass blades (off when mesh clutter handles ground detail)" )]
	public bool UseGpuGrassBlades { get; set; }

	[Property, Group( "Client Grass" ), Range( 256f, 4096f )] public float GrassRenderRadius { get; set; } = 58f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFullDensityRadius { get; set; } = 22f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 128f, 1024f )] public float GrassCellSize { get; set; } = 9f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellNear { get; set; } = 210;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellMid { get; set; } = 105;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellFar { get; set; } = 24;
	[Property, Group( "Client Grass" ), Range( 0.01f, 4f )] public float GrassMinScale { get; set; } = 0.75f;
	[Property, Group( "Client Grass" ), Range( 0.01f, 4f )] public float GrassMaxScale { get; set; } = 1.35f;
	[Property, Group( "Client Grass" ), Range( 1f, 75f )] public float GrassSlopeCutoffDegrees { get; set; } = 32f;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFadeStart { get; set; } = 38f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFadeEnd { get; set; } = 58f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 1000, 75000 )] public int GrassMaxVisibleInstances { get; set; } = 30000;
	[Property, Group( "Client Grass" )] public int GrassSeedSalt { get; set; } = 17391;
}
