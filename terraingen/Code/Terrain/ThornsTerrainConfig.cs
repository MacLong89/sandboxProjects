namespace Terraingen.TerrainGen;

// Tunable parameters for the Thorns heightmap terrain prototype.
public sealed class ThornsTerrainConfig
{
	[Property, Group( "Source" )] public string HeightmapPath { get; set; } = "map/co_height.png";

	[Property, Group( "Source" )] public int WorldSeed { get; set; } = 42069;

	[Property, Group( "Source" ), Title( "World build version (bump to invalidate caches)" )]
	public int WorldBuildVersion { get; set; } = 1;

	[Property, Group( "Source" ), Range( 0.15f, 0.35f )]
	public float CropMinFraction { get; set; } = 0.18f;

	[Property, Group( "Source" ), Range( 0.15f, 0.35f )]
	public float CropMaxFraction { get; set; } = 0.32f;

	[Property, Group( "Source" )] public int RegionCandidateCount { get; set; } = 12;

	[Property, Group( "Terrain" )] public int TerrainResolution { get; set; } = 1024;

	[Property, Group( "Terrain" )] public int TerrainWorldResolution { get; set; } = 2048;

	[Property, Group( "Terrain" )] public float WorldScaleInches { get; set; } = 39f;

	[Property, Group( "Terrain" )] public float MaxTerrainHeightInches { get; set; } = 12000f;

	[Property, Group( "Terrain" ), Range( 0.85f, 1f ), Title( "Peak height cap (normalized)" )]
	public float PeakHeightNormalized { get; set; } = 0.98f;

	[Property, Group( "Terrain" ), Range( 0.5f, 5f ), Title( "Global vertical exaggeration" )]
	public float VerticalExaggeration { get; set; } = 3.4f;

	[Property, Group( "Terrain" ), Range( 1f, 3f ), Title( "Extra peak lift (multiplier at summits)" )]
	public float PeakExaggerationMultiplier { get; set; } = 2.45f;

	[Property, Group( "Terrain" ), Range( 1f, 3f ), Title( "Valley depth (multiplier below sea anchor)" )]
	public float ValleyDepthMultiplier { get; set; } = 1.85f;

	[Property, Group( "Terrain" ), Range( 1f, 2f ), Title( "Foothill vertical cap (lowlands)" )]
	public float LowlandVerticalCap { get; set; } = 1.28f;

	[Property, Group( "Terrain" ), Range( 0.08f, 0.45f ), Title( "Foothill band height above sea" )]
	public float LowlandBandHeight { get; set; } = 0.28f;

	[Property, Group( "Terrain" ), Range( 0.02f, 0.2f ), Title( "Blend into full exaggeration" )]
	public float LowlandBlendHeight { get; set; } = 0.1f;

	[Property, Group( "Terrain" ), Range( 0f, 1f ), Title( "Post-exaggeration lowland smooth" )]
	public float LowlandSmoothingStrength { get; set; } = 0.78f;

	[Property, Group( "Terrain" ), Range( 1, 8 ), Title( "Lowland blur radius (texels)" )]
	public int LowlandBlurRadius { get; set; } = 4;

	[Property, Group( "Terrain" ), Range( 0.25f, 2f )]
	public float HorizontalScale { get; set; } = 1f;

	[Property, Group( "Water" ), Range( 0f, 0.5f )]
	public float SeaLevelNormalized { get; set; } = 0.06f;

	[Property, Group( "Water" )] public float LakeBedDepth { get; set; } = 0.02f;

	[Property, Group( "Water" )]
	public bool CreateWaterSheet { get; set; } = true;

	[Property, Group( "Water" )]
	public string WaterSurfaceMaterial { get; set; } = "materials/water.vmat";

	[Property, Group( "Water" )]
	public string WaterPlaneModel { get; set; } = "models/dev/plane.vmdl";

	[Property, Group( "Water" ), Title( "Dev plane mesh size (inches)" )]
	public float WaterPlaneBaseSizeInches { get; set; } = 100f;

	[Property, Group( "Water" ), Range( 0.02f, 0.2f ), Title( "Ocean margin beyond map (fraction of size)" )]
	public float WaterPlaneMarginFraction { get; set; } = 0.085f;

	[Property, Group( "Stylization" ), Range( 0f, 1f )]
	public float PlainsSmoothingStrength { get; set; } = 0.72f;

	[Property, Group( "Stylization" ), Range( 0f, 1f )]
	public float ValleyWideningStrength { get; set; } = 0.88f;

	[Property, Group( "Stylization" ), Range( 0f, 3f )]
	public float MountainExaggerationStrength { get; set; } = 2.5f;

	[Property, Group( "Stylization" ), Range( 0f, 2f )]
	public float RidgeSharpeningStrength { get; set; } = 1.45f;

	[Property, Group( "Stylization" ), Range( 0f, 2f )]
	public float CliffExposureStrength { get; set; } = 1.5f;

	[Property, Group( "Stylization" ), Range( 0.35f, 0.75f ), Title( "Cliff sculpt min height (percentile)" )]
	public float CliffMinHeightPercentile { get; set; } = 0.52f;

	[Property, Group( "Stylization" ), Range( 0f, 1f )]
	public float MicroNoiseReductionStrength { get; set; } = 0.8f;

	[Property, Group( "Materials" ), Title( "Grass (assign in editor if magenta)" )]
	public TerrainMaterial MaterialGrass { get; set; }

	[Property, Group( "Materials" )]
	public TerrainMaterial MaterialDirt { get; set; }

	[Property, Group( "Materials" )]
	public TerrainMaterial MaterialRock { get; set; }

	[Property, Group( "Materials" )]
	public TerrainMaterial MaterialSnow { get; set; }

	[Property, Group( "Materials" ), Range( 0.35f, 0.85f ), Title( "Grass upper range (fraction above sea)" )]
	public float GrassUpperRangeFraction { get; set; } = 0.72f;

	[Property, Group( "Materials" ), Range( 0.04f, 0.22f ), Title( "Max slope for grass (normalized)" )]
	public float GrassMaxSlope { get; set; } = 0.12f;

	[Property, Group( "Materials" ), Range( 0.005f, 0.06f ), Title( "Dirt band (fraction of elev range at grass line)" )]
	public float DirtBandRangeFraction { get; set; } = 0.022f;

	[Property, Group( "Materials" ), Range( 0.25f, 0.65f ), Title( "Rock lower range (fraction above sea)" )]
	public float RockLowerRangeFraction { get; set; } = 0.5f;

	[Property, Group( "Materials" ), Range( 0.08f, 0.45f ), Title( "Snow upper range (top fraction above sea)" )]
	public float SnowUpperRangeFraction { get; set; } = 0.35f;

	[Property, Group( "Placement" )]
	public bool CenterAtWorldOrigin { get; set; } = true;

	[Property, Group( "Placement" )]
	public bool FlipHeightmapY { get; set; } = false;

	[Property, Group( "Placement" )]
	public bool FrameCameraOnGenerate { get; set; } = true;

	[Property, Group( "Generation" )] public bool GenerateOnStart { get; set; } = true;

	[Property, Group( "Generation" )] public bool HostAuthoritative { get; set; } = true;

	[Property, Group( "Generation" ), Title( "Clients re-sculpt terrain (legacy; prefer cache)" )]
	public bool ClientsGenerateDeterministic { get; set; }

	[Property, Group( "Generation" ), Title( "Defer foliage/minerals/grass until local player spawns" )]
	public bool DeferCosmeticsUntilLocalPlayer { get; set; } = true;
}
