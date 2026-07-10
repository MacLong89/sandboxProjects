namespace Terraingen.Minerals;

using Terraingen.Rendering;

/// <summary>World scatter tuning for decorative / harvestable stone and ore props.</summary>
public sealed class ThornsMineralConfig
{
	public const string DefaultScatterModel = "models/clutter/rock1.vmdl";

	[Property, Group( "Spawn" ), Title( "GPU-instanced mineral draws" )]
	public bool UseInstancedMinerals { get; set; } = true;

	[Property, Group( "Spawn" )] public bool SpawnOnTerrainReady { get; set; } = true;

	[Property, Group( "Spawn" )] public int WorldSeed { get; set; } = 42069;

	[Property, Group( "Models" )]
	public string ScatterModel { get; set; } = DefaultScatterModel;

	/// <summary>Upgrades legacy scene values and empty paths to <see cref="DefaultScatterModel"/>.</summary>
	public void NormalizeScatterModel()
	{
		if ( string.IsNullOrWhiteSpace( ScatterModel )
		     || ScatterModel.Contains( "rock_scatter_01", StringComparison.OrdinalIgnoreCase ) )
		{
			ScatterModel = DefaultScatterModel;
		}
	}

	[Property, Group( "Models" ), Title( "Stone tint (cool gray)" )]
	public Color StoneTint { get; set; } = new Color( 0.72f, 0.76f, 0.84f, 1f );

	[Property, Group( "Models" ), Title( "Ore tint (copper-gold)" )]
	public Color OreTint { get; set; } = new Color( 1.05f, 0.48f, 0.12f, 1f );

	[Property, Group( "Chunks" )] public float ChunkSizeInches { get; set; } = 8000f;

	[Property, Group( "Chunks" ), Range( 1, 24 )] public int ChunksPerFrame { get; set; } = 5;

	[Property, Group( "Chunks" ), Range( 0, 96 )] public int MaxStonePerChunk { get; set; } = 18;

	[Property, Group( "Chunks" ), Range( 0, 72 )] public int MaxOrePerChunk { get; set; } = 0;

	[Property, Group( "Density" ), Range( 0.05f, 1f )] public float GlobalStoneDensity { get; set; } = 0.9f;

	[Property, Group( "Density" ), Range( 0.01f, 1f )] public float GlobalOreDensity { get; set; } = 0.72f;

	[Property, Group( "Density" ), Range( 1f, 12f )] public float StoneScatterMultiplier { get; set; } = 3f;

	[Property, Group( "Density" ), Range( 1f, 8f )] public float OreScatterMultiplier { get; set; } = 7.05f;

	[Property, Group( "Density" ), Range( 0f, 0.5f ), Title( "Stone nodes promoted to ore" )]
	public float StoneToOreFraction { get; set; } = 0.20f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinHeightNormalized { get; set; } = 0.07f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxHeightNormalized { get; set; } = 0.94f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxSlopeForStone { get; set; } = 0.42f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxSlopeForOre { get; set; } = 0.48f;

	[Property, Group( "Placement" ), Range( 0f, 1f ), Title( "Min slope for meadow stone (grass tmat)" )]
	public float MinGrassSlopeForStone { get; set; } = 0.04f;

	[Property, Group( "Placement" ), Range( 0f, 1f ), Title( "Meadow stone chance on flat grass" )]
	public float MeadowStoneChance { get; set; } = 0.55f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinAlpineForOre { get; set; } = 0.04f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MeadowStoneReduction { get; set; } = 0.10f;

	[Property, Group( "Player Pocket" ), Range( 0, 96 )]
	public int PlayerPocketStoneCount { get; set; } = 48;

	[Property, Group( "Player Pocket" ), Range( 0, 48 )]
	public int PlayerPocketOreCount { get; set; } = 0;

	[Property, Group( "Player Pocket" ), Range( 8f, 120f ), Title( "Radius (meters)" )]
	public float PlayerPocketRadiusMeters { get; set; } = 42f;

	[Property, Group( "Scale" )] public float InchesPerMeter { get; set; } = 39.37f;

	[Property, Group( "Scale" )] public float StoneTargetHeightInches { get; set; } = 40f;

	[Property, Group( "Scale" )] public float OreTargetHeightInches { get; set; } = 44f;

	[Property, Group( "Scale" ), Range( 0.1f, 12f )] public float ScaleMultiplier { get; set; } = 0.64f;

	[Property, Group( "Scale" )] public float SurfaceOffsetInches { get; set; } = 4f;

	[Property, Group( "Scale" )] public float GroundEmbedInches { get; set; } = 3f;

	[Property, Group( "Scale" ), Range( 0f, 1f )] public float PivotLiftFraction { get; set; } = 0.35f;

	[Property, Group( "Interest" ), Title( "Populate radius override (inches, 0 = auto full map)" )]
	public float InterestPopulateRadiusInches { get; set; }

	[Property, Group( "Interest" ), Range( 1, 12 )]
	public int DeferredChunksPerFrame { get; set; } = 4;

	[Property, Group( "Culling" )] public float CullDistanceInches { get; set; } = 40000f;

	[Property, Group( "Culling" )] public float CullHysteresisInches { get; set; } = 12000f;

	[Property, Group( "Culling" ), Title( "Harvest colliders active within (inches)" )]
	public float HarvestCollisionDistanceInches { get; set; } = 3200f;

	[Property, Group( "Culling" ), Range( 4, 64 ), Title( "Chunks updated per cull pass" )]
	public int ChunksUpdatedPerCullPass { get; set; } = 12;

	[Property, Group( "LOD" ), Title( "Stones cast shadows within (inches)" )]
	public float MineralShadowDistanceInches { get; set; } = ThornsVisualPerformanceDistances.MineralShadowInches;

	[Property, Group( "LOD" )] public float ShadowLodHysteresisInches { get; set; } = ThornsVisualPerformanceDistances.ShadowLodHysteresisInches;

	[Property, Group( "LOD" ), Title( "Stone shadow off within (inches) — stops ground acne when mining" )]
	public float MineralStoneShadowCloseOffInches { get; set; } = 420f;

	[Property, Group( "Debug" )] public bool VerboseDebug { get; set; }
}
