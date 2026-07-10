namespace Terraingen.Foliage;

using Terraingen.Rendering;

/// <summary>
/// Full-world foliage composition — ecosystems, treelines, and restrained density.
/// </summary>
public sealed class ThornsFoliageConfig
{
	[Property, Group( "Spawn" )] public bool SpawnOnTerrainReady { get; set; } = true;

	[Property, Group( "Spawn" ), Title( "GPU-instanced trees" )]
	public bool UseInstancedTrees { get; set; } = false;

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
	public float GlobalDensity { get; set; } = 0.64f;

	[Property, Group( "Ecosystem" )]
	public int ForestMassBlurRadius { get; set; } = 6;

	[Property, Group( "Ecosystem" )]
	public int ForestMassBlurPasses { get; set; } = 2;

	[Property, Group( "Ecosystem" ), Range( 0f, 1f )]
	public float ForestMassThreshold { get; set; } = 0.28f;

	[Property, Group( "Ecosystem" )]
	public int MaxForestMassesPerChunk { get; set; } = 2;

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
	public int MaxRiverLineClustersPerChunk { get; set; } = 4;

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
	public int ChunksPerFrame { get; set; } = 3;

	[Property, Group( "Chunks" )]
	public float ClusterSpacingInches { get; set; } = 2400f;

	[Property, Group( "Chunks" )]
	public int MaxTreeClustersPerChunk { get; set; } = 8;

	[Property, Group( "Interest" ), Title( "Populate radius override (inches, 0 = auto full map)" )]
	public float InterestPopulateRadiusInches { get; set; }

	[Property, Group( "Interest" ), Range( 1, 12 )]
	public int DeferredChunksPerFrame { get; set; } = 4;

	[Property, Group( "Culling" )]
	public float CullDistanceInches { get; set; } = 14000f;

	[Property, Group( "Culling" )]
	public float CullHysteresisInches { get; set; } = 1800f;

	[Property, Group( "Culling" ), Title( "Tree collision proxies active within (inches)" )]
	public float TreeCollisionDistanceInches { get; set; } = ThornsTreeGatheringRange.CollisionProxyInches;

	[Property, Group( "LOD" ), Title( "Trees cast shadows within (inches)" )]
	public float TreeLodShadowDistanceInches { get; set; } = ThornsVisualPerformanceDistances.TreeShadowInches;

	[Property, Group( "LOD" ), Title( "Trees visible within (inches)" )]
	public float TreeLodHideDistanceInches { get; set; } = 14000f;

	[Property, Group( "LOD" ), Title( "Billboard impostors beyond (inches)" )]
	public float TreeLodBillboardDistanceInches { get; set; } = 9000f;

	[Property, Group( "LOD" ), Title( "Use PNG billboard impostors" )]
	public bool UseTreeBillboardLod { get; set; } = false;

	[Property, Group( "LOD" ), Title( "Billboard plane model" )]
	public string TreeBillboardPlaneModel { get; set; } = "models/dev/plane.vmdl";

	[Property, Group( "LOD" ), Title( "Pine billboard material" )]
	public string TreeBillboardPineMaterial { get; set; } = "materials/foliage/tree_lod_pine.vmat";

	[Property, Group( "LOD" ), Title( "Aspen billboard material" )]
	public string TreeBillboardAspenMaterial { get; set; } = "materials/foliage/tree_lod_aspen.vmat";

	[Property, Group( "LOD" ), Title( "Oak billboard material" )]
	public string TreeBillboardOakMaterial { get; set; } = "materials/foliage/tree_lod_oak.vmat";

	[Property, Group( "LOD" )]
	public float LodDistanceHysteresisInches { get; set; } = ThornsVisualPerformanceDistances.ShadowLodHysteresisInches;

	[Property, Group( "LOD" ), Range( 1, 24 )]
	public int LodChunksUpdatedPerFrame { get; set; } = 4;

	[Property, Group( "LOD" ), Range( 0.05f, 1f ), Title( "Instance LOD interval (seconds)" )]
	public float InstanceLodIntervalSeconds { get; set; } = 0.18f;

	[Property, Group( "LOD" ), Range( 0f, 8000f ), Title( "Instance LOD min move (inches)" )]
	public float InstanceLodMinMoveInches { get; set; } = 1200f;

	[Property, Group( "Trees" )]
	public float MaxSlopeForTrees { get; set; } = 0.09f;

	// Used only by the biome sampler so the cosmetic clutter system can decide where grass-like clutter belongs.
	public float MaxSlopeForGrass { get; set; } = 0.2f;

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

	/// <summary>Target tree height in world inches (~4× the 72 in player body).</summary>
	public const float DefaultTreeTargetHeightInches = 288f;

	[Property, Group( "Scale" )]
	public float ScaleMultiplier { get; set; } = 1f;

	[Property, Group( "Scale" )]
	public float TreeSizeMultiplier { get; set; } = 1f;

	[Property, Group( "Scale" )]
	public float InchesPerMeter { get; set; } = 39.37f;

	[Property, Group( "Scale" )]
	public float PineTargetHeightInches { get; set; } = DefaultTreeTargetHeightInches;

	[Property, Group( "Scale" )]
	public float AspenTargetHeightInches { get; set; } = DefaultTreeTargetHeightInches;

	[Property, Group( "Scale" )]
	public float OakTargetHeightInches { get; set; } = DefaultTreeTargetHeightInches;

	[Property, Group( "Scale" )]
	public float MinTreeRenderScale { get; set; } = 0.2f;

	[Property, Group( "Scale" )]
	public float MaxTreeRenderScale { get; set; } = 8192f;

	[Property, Group( "Scale" )]
	public float SurfaceOffsetInches { get; set; } = 4f;

	/// <summary>Fix legacy scene values (e.g. MinTreeRenderScale=80) that break <see cref="Math.Clamp"/>.</summary>
	public void EnsureScaleLimits()
	{
		if ( MinTreeRenderScale > 8f )
			MinTreeRenderScale = 0.2f;

		if ( MaxTreeRenderScale < MinTreeRenderScale )
			MaxTreeRenderScale = MathF.Max( MinTreeRenderScale, 8192f );

		if ( MinTreeRenderScale > MaxTreeRenderScale )
			MinTreeRenderScale = MaxTreeRenderScale * 0.2f;
	}

	[Property, Group( "Scale" ), Title( "Tree embed below surface (inches)" )]
	public float TreeGroundEmbedInches { get; set; } = 20f;

	[Property, Group( "Scale" ), Range( 0f, 1f )]
	public float TreePivotLiftFraction { get; set; } = 0.5f;
}
