namespace Terraingen.Clutter;

/// <summary>
/// Client-side cosmetic ground clutter tuning.
/// Distances are in Source/s&box inches. 50m is roughly 1968 inches (39.37 in/m).
/// </summary>
public sealed class ThornsClutterConfig
{
	public const float InchesPerMeter = 39.37f;

	/// <summary>10 meters in Source inches — default clutter bubble radius.</summary>
	public const float TenMetersInches = 1968f / 5f;

	[Property, Group( "Streaming" )] public bool Enabled { get; set; } = true;
	[Property, Group( "Streaming" )] public int WorldSeed { get; set; } = 42069;
	[Property, Group( "Streaming" ), Range( 128f, 4096f )] public float ClutterRadius { get; set; } = TenMetersInches;
	[Property, Group( "Streaming" ), Range( 256f, 2048f )] public float ChunkSize { get; set; } = 400f;
	[Property, Group( "Streaming" ), Range( 16, 10240 )] public int MaxInstancesPerChunk { get; set; } = 6400;
	[Property, Group( "Streaming" ), Range( 0f, 6f )] public float DensityMultiplier { get; set; } = 4f;
	[Property, Group( "Streaming" ), Range( 1f, 12f ), Title( "Grass density multiplier" )]
	public float GrassDensityMultiplier { get; set; } = 7f;

	[Property, Group( "Streaming" ), Range( 1f, 8f ), Title( "Grass instance target multiplier" )]
	public float GrassInstanceMultiplier { get; set; } = 3.5f;

	[Property, Group( "Streaming" ), Range( 1f, 4f ), Title( "Near-player grass density boost" )]
	public float NearPlayerGrassBoost { get; set; } = 2.4f;

	[Property, Group( "Streaming" ), Range( 1, 4 ), Title( "Grass blades per accepted placement" )]
	public int GrassPlacementClusterSize { get; set; } = 2;

	[Property, Group( "Streaming" ), Range( 0.05f, 0.55f ), Title( "Rock instance mix (of clutter budget)" )]
	public float RockInstanceMix { get; set; } = 0.06f;
	[Property, Group( "Streaming" ), Range( 0f, 4096f )] public float DistanceFadeStart { get; set; } = 300f;
	[Property, Group( "Streaming" ), Range( 0f, 4096f )] public float DistanceFadeEnd { get; set; } = TenMetersInches;

	[Property, Group( "Streaming" ), Title( "Parent clutter to local pawn (moves with player)" )]
	public bool FollowLocalPawn { get; set; } = true;

	[Property, Group( "Streaming" ), Range( 1, 16 ), Title( "Chunks built per refresh tick" )]
	public int ChunksBuiltPerRefresh { get; set; } = 6;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float SlopeReject { get; set; } = 0.38f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinHeightNormalized { get; set; } = 0.07f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxHeightNormalized { get; set; } = 0.92f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float MeadowDensity { get; set; } = 2.1f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float ForestDensity { get; set; } = 0.95f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float AlpineDensity { get; set; } = 0.95f;
	[Property, Group( "Placement" ), Range( 0f, 2f )] public float SlopeRockDensity { get; set; } = 1.2f;
	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinRockChance { get; set; } = 0.032f;

	[Property, Group( "Models" )] public string GrassModel { get; set; } = "models/clutter/grass_common_short.vmdl";
	[Property, Group( "Models" )] public string RockModelA { get; set; } = "models/clutter/rock1.vmdl";
	[Property, Group( "Models" )] public string RockModelB { get; set; } = "models/clutter/rock2.vmdl";

	[Property, Group( "Scale" ), Title( "Grass target height (inches)" )]
	public float GrassTargetHeightInches { get; set; } = 9f;

	[Property, Group( "Scale" ), Range( 0.1f, 500f ), Title( "Grass scale multiplier" )]
	public float GrassScaleMultiplier { get; set; } = 1f;

	[Property, Group( "Scale" ), Range( 0.05f, 32f ), Title( "Grass max uniform scale (safety clamp)" )]
	public float GrassMaxUniformScale { get; set; } = 4f;

	[Property, Group( "Scale" ), Title( "Grass surface offset (inches)" )]
	public float GrassSurfaceOffset { get; set; } = 0f;

	[Property, Group( "Scale" ), Title( "Grass embed below surface (inches)" )]
	public float GrassGroundEmbedInches { get; set; } = 6f;

	[Property, Group( "Scale" ), Range( 0f, 1f ), Title( "Grass pivot lift (center pivots only)" )]
	public float GrassPivotLiftFraction { get; set; } = 0.15f;

	[Property, Group( "Scale" ), Title( "Rock target height (inches)" )]
	public float RockTargetHeightInches { get; set; } = 14f;

	[Property, Group( "Scale" ), Range( 0.1f, 500f ), Title( "Rock scale multiplier" )]
	public float RockScaleMultiplier { get; set; } = 0.5f;

	[Property, Group( "Scale" ), Title( "Rock surface offset (inches)" )]
	public float RockSurfaceOffset { get; set; } = 4f;

	[Property, Group( "Scale" ), Title( "Rock embed below terrain (inches)" )]
	public float RockGroundEmbedInches { get; set; } = 6f;

	[Property, Group( "Scale" ), Range( 0f, 1f ), Title( "Rock pivot lift (center pivots)" )]
	public float RockPivotLiftFraction { get; set; } = 0.35f;

	[Property, Group( "Scale" ), Range( 0f, 20f )] public float RandomTiltDegrees { get; set; } = 5f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; } = false;
	[Property, Group( "Debug" )] public bool DrawChunkBounds { get; set; } = false;
	[Property, Group( "Debug" )] public bool DrawDensitySamples { get; set; } = false;

	[Property, Group( "Client Grass" ), Range( 256f, 4096f )] public float GrassRenderRadius { get; set; } = 70f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFullDensityRadius { get; set; } = 28f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 128f, 1024f )] public float GrassCellSize { get; set; } = 8f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellNear { get; set; } = 270;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellMid { get; set; } = 135;
	[Property, Group( "Client Grass" ), Range( 1, 768 )] public int BladesPerCellFar { get; set; } = 45;
	[Property, Group( "Client Grass" ), Range( 0.01f, 4f )] public float GrassMinScale { get; set; } = 0.75f;
	[Property, Group( "Client Grass" ), Range( 0.01f, 4f )] public float GrassMaxScale { get; set; } = 1.35f;
	[Property, Group( "Client Grass" ), Range( 1f, 75f )] public float GrassSlopeCutoffDegrees { get; set; } = 32f;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFadeStart { get; set; } = 48f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 64f, 4096f )] public float GrassFadeEnd { get; set; } = 70f * InchesPerMeter;
	[Property, Group( "Client Grass" ), Range( 1000, 75000 )] public int GrassMaxVisibleInstances { get; set; } = 48000;
	[Property, Group( "Client Grass" )] public int GrassSeedSalt { get; set; } = 17391;
}
