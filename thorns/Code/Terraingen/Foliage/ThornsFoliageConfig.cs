namespace Terraingen.Foliage;

using Terraingen.TerrainGen;

/// <summary>
/// Full-world foliage composition — ecosystems, treelines, and restrained density.
/// </summary>
public sealed class ThornsFoliageConfig
{
	[Property, Group( "Spawn" )] public bool SpawnOnTerrainReady { get; set; } = true;

	/// <summary>When true (Thorns + foliage2), each placed tree gets <see cref="Sandbox.ThornsResourceNode"/> + physics on the host.</summary>
	[Property, Group( "Spawn" )] public bool SpawnAsHarvestableWoodTrees { get; set; }

	[Property, Group( "Debug" )] public bool VerboseDebug { get; set; } = false;

	[Property, Group( "Debug" )] public bool DebugForceCenterRing { get; set; } = false;

	[Property, Group( "Debug" )]
	public bool SpawnGuaranteedTreesAtOrigin { get; set; } = false;

	[Property, Group( "Debug" )] public int DebugCenterRingCount { get; set; } = 6;

	[Property, Group( "Debug" )] public bool LimitSpawnToCenterRadius { get; set; } = false;

	[Property, Group( "Debug" )]
	public float SpawnRadiusFromCenterInches { get; set; } = 16000f;

	[Property, Group( "Spawn" )] public int FoliageSeed { get; set; } = 42069;

	[Property, Group( "Spawn" ), Range( 0.05f, 1f )]
	public float GlobalDensity { get; set; } = 0.72f;

	[Property, Group( "Ecosystem" )]
	public int ForestMassBlurRadius { get; set; } = 6;

	[Property, Group( "Ecosystem" )]
	public int ForestMassBlurPasses { get; set; } = 1;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float ForestMassThreshold { get; set; } = 0.28f;

	[Property, Group( "Ecosystem" )]
	public int MaxForestMassesPerChunk { get; set; } = 3;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float OpeningForestReduction { get; set; } = 0.62f;

	[Property, Group( "Ecosystem" )]
	public float OpeningNoiseScale { get; set; } = 4.2f;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float OpeningNoiseLow { get; set; } = 0.62f;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float OpeningNoiseHigh { get; set; } = 0.88f;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float TreelineStartNormalized { get; set; } = 0.52f;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float TreelineEndNormalized { get; set; } = 0.74f;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float RiverCorridorBoost { get; set; } = 0.55f;

	[Property, Group( "Ecosystem" )]
	public float RiverLineSpacingInches { get; set; } = 1400f;

	[Property, Group( "Ecosystem" )]
	public int MaxRiverLineClustersPerChunk { get; set; } = 6;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float HeroTreeChance { get; set; } = 0.22f;

	[Property, Group( "Models" )]
	public string PineModel { get; set; } = "models/foliage2/pine_tree.vmdl";

	[Property, Group( "Models" )]
	public string AspenModel { get; set; } = "models/foliage2/aspen_tree.vmdl";

	[Property, Group( "Models" )]
	public string OakModel { get; set; } = "models/foliage2/oak_tree.vmdl";

	[Property, Group( "Chunks" )]
	public float ChunkSizeInches { get; set; } = 8000f;

	[Property, Group( "Chunks" )]
	public int ChunksPerFrame { get; set; } = 2;

	/// <summary>Hard cap on new tree instances per populate frame (spread load; see <see cref="ThornsFoliageSpawnBudget"/>).</summary>
	[Property, Group( "Chunks" )]
	public int MaxInstancesPerPopulateFrame { get; set; } = 18;

	[Property, Group( "Chunks" )]
	public float ClusterSpacingInches { get; set; } = 2400f;

	[Property, Group( "Chunks" )]
	public int MaxTreeClustersPerChunk { get; set; } = 12;

	[Property, Group( "Culling" )]
	public float CullDistanceInches { get; set; } = 110000f;

	[Property, Group( "Culling" )]
	public float CullHysteresisInches { get; set; } = 12000f;

	[Property, Group( "LOD" ), Title( "Trees cast shadows within (inches)" )]
	public float TreeLodShadowDistanceInches { get; set; } = 36000f;

	[Property, Group( "LOD" ), Title( "Trees visible within (inches)" )]
	public float TreeLodHideDistanceInches { get; set; } = 64000f;

	[Property, Group( "LOD" )]
	public float LodDistanceHysteresisInches { get; set; } = 4500f;

	[Property, Group( "LOD" ), Range( 1, 24 )]
	public int LodChunksUpdatedPerFrame { get; set; } = 3;

	[Property, Group( "LOD" ), Range( 0.05f, 1f ), Title( "Instance LOD interval (seconds)" )]
	public float InstanceLodIntervalSeconds { get; set; } = 0.12f;

	[Property, Group( "LOD" ), Range( 0f, 8000f ), Title( "Instance LOD min move (inches)" )]
	public float InstanceLodMinMoveInches { get; set; } = 800f;

	[Property, Group( "Trees" ), Range( 0f, 45f ), Title( "Max slope for trees (degrees)" )]
	public float MaxTreeSlopeDegrees { get; set; } = ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees;

	/// <summary>Legacy heightmap gradient cap; used only when <see cref="MaxTreeSlopeDegrees"/> is not positive.</summary>
	public float MaxSlopeForTrees { get; set; } = 0.09f;

	public float GetMaxTreeHeightmapSlope( HeightmapField field, Terrain terrain )
	{
		if ( MaxTreeSlopeDegrees > 0.01f && field is not null && terrain.IsValid() )
		{
			return ThornsTerrainSlope.HeightmapGradientFromDegrees(
				MaxTreeSlopeDegrees,
				field.Width,
				field.Height,
				terrain.TerrainSize,
				terrain.TerrainHeight );
		}

		return MaxSlopeForTrees;
	}

	// Used only by the biome sampler so the cosmetic clutter system can decide where grass-like clutter belongs.
	public float MaxSlopeForGrass { get; set; } = 0.28f;

	[Property, Group( "Trees" ), Title( "Min height above sea (normalized)" )]
	public float MinHeightAboveSea { get; set; } = 0.008f;

	[Property, Group( "Trees" )]
	public float TreeFootprintSampleRadiusInches { get; set; } = 420f;

	[Property, Group( "Trees" )]
	public bool RequireTreeFootprintFlatness { get; set; } = false;

	[Property, Group( "Trees" )]
	public float MaxTreeFootprintSlopeDelta { get; set; } = 0.12f;

	[Property, Group( "Trees" )]
	public float MaxTreeFootprintHeightDeltaInches { get; set; } = 72f;

	[Property, Group( "Scale" )]
	public float ScaleMultiplier { get; set; } = 2f;

	[Property, Group( "Scale" )]
	public float TreeSizeMultiplier { get; set; } = 2f;

	/// <summary>Tree vmdl meshes are ~1m tall; world space is inches.</summary>
	[Property, Group( "Scale" )]
	public float InchesPerMeter { get; set; } = 39.37f;

	[Property, Group( "Scale" )]
	public float PineTargetHeightInches { get; set; } = 4800f;

	[Property, Group( "Scale" )]
	public float AspenTargetHeightInches { get; set; } = 4200f;

	[Property, Group( "Scale" )]
	public float OakTargetHeightInches { get; set; } = 3600f;

	[Property, Group( "Scale" )]
	public float MinTreeRenderScale { get; set; } = 80f;

	[Property, Group( "Scale" )]
	public float SurfaceOffsetInches { get; set; } = 4f;

	[Property, Group( "Scale" ), Title( "Tree embed below surface (inches)" )]
	public float TreeGroundEmbedInches { get; set; } = 20f;

	[Property, Group( "Scale" ), Range( 0f, 1f )]
	public float TreePivotLiftFraction { get; set; } = 0.5f;
}
