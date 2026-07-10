namespace Terraingen.Boulders;

using Terraingen.Rendering;

/// <summary>Large solid terrain boulders used as natural sightline blockers.</summary>
public sealed class ThornsBoulderConfig
{
	[Property, Group( "Spawn" )] public bool SpawnOnTerrainReady { get; set; } = true;

	[Property, Group( "Spawn" )] public int WorldSeed { get; set; } = 42069;

	[Property, Group( "Models" )] public string Boulder1Model { get; set; } = "models/boulders/boulder1.vmdl";

	[Property, Group( "Models" )] public string Boulder2Model { get; set; } = "models/boulders/boulder2.vmdl";

	[Property, Group( "Models" )] public string Boulder3Model { get; set; } = "models/boulders/boulder3.vmdl";

	[Property, Group( "Chunks" )] public float ChunkSizeInches { get; set; } = 8000f;

	[Property, Group( "Chunks" ), Range( 0, 24 )] public int MaxBouldersPerChunk { get; set; } = 12;

	[Property, Group( "Density" ), Range( 0f, 1f )] public float GlobalDensity { get; set; } = 0.72f;

	[Property, Group( "Density" ), Range( 0f, 1f )] public float OpeningBias { get; set; } = 0.62f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MinHeightNormalized { get; set; } = 0.075f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxHeightNormalized { get; set; } = 0.82f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxSlope { get; set; } = 0.10f;

	[Property, Group( "Placement" )] public float FootprintSampleRadiusInches { get; set; } = 320f;

	[Property, Group( "Placement" ), Range( 0f, 1f )] public float MaxFootprintSlopeDelta { get; set; } = 0.08f;

	[Property, Group( "Placement" )] public float MaxFootprintHeightDeltaInches { get; set; } = 48f;

	[Property, Group( "Placement" )] public float MinBoulderSpacingInches { get; set; } = 900f;

	[Property, Group( "Placement" )] public float TownExtraMarginInches { get; set; } = 900f;

	[Property, Group( "Clustering" ), Range( 0f, 1f )] public float BunchChance { get; set; } = 0.68f;

	[Property, Group( "Clustering" ), Range( 0, 8 )] public int MinBunchExtraBoulders { get; set; } = 1;

	[Property, Group( "Clustering" ), Range( 0, 8 )] public int MaxBunchExtraBoulders { get; set; } = 4;

	[Property, Group( "Clustering" )] public float BunchRadiusMinInches { get; set; } = 360f;

	[Property, Group( "Clustering" )] public float BunchRadiusMaxInches { get; set; } = 1250f;

	[Property, Group( "Clustering" )] public float MinBunchMemberSpacingInches { get; set; } = 220f;

	[Property, Group( "Scale" )] public float MinTargetHeightInches { get; set; } = 432f;

	[Property, Group( "Scale" )] public float MaxTargetHeightInches { get; set; } = 648f;

	[Property, Group( "Scale" ), Range( 1f, 4f ), Title( "Random size multiplier (min)" )]
	public float MinSizeMultiplier { get; set; } = 1f;

	[Property, Group( "Scale" ), Range( 1f, 4f ), Title( "Random size multiplier (max)" )]
	public float MaxSizeMultiplier { get; set; } = 3f;

	[Property, Group( "Scale" ), Range( 0f, 1f )] public float GroundEmbedFraction { get; set; } = 0.08f;

	[Property, Group( "Scale" ), Title( "Sink below terrain (inches)" )]
	public float GroundSinkOffsetInches { get; set; } = 40f;

	[Property, Group( "Collision" ), Range( 0f, 0.75f ), Title( "Initial radius scale (0 = code default)" )]
	public float CollisionRadiusScaleOverride { get; set; }

	[Property, Group( "LOD" ), Title( "Boulders cast shadows within (inches)" )]
	public float BoulderLodShadowDistanceInches { get; set; } = ThornsVisualPerformanceDistances.BoulderShadowInches;

	[Property, Group( "LOD" )] public float LodDistanceHysteresisInches { get; set; } = 4500f;

	[Property, Group( "LOD" ), Range( 1, 32 )] public int LodBouldersUpdatedPerFrame { get; set; } = 12;

	[Property, Group( "LOD" ), Range( 0.05f, 1f ), Title( "Shadow LOD interval (seconds)" )]
	public float ShadowLodIntervalSeconds { get; set; } = 0.18f;

	[Property, Group( "LOD" ), Range( 0f, 8000f ), Title( "Shadow LOD min move (inches)" )]
	public float ShadowLodMinMoveInches { get; set; } = 1200f;

	[Property, Group( "Debug" )] public bool VerboseDebug { get; set; }

	public float ResolveCollisionRadiusScale() => ThornsBoulderSphereCollision.ResolveRadiusScale();

	public string[] ModelPaths()
		=> new[]
		{
			Boulder1Model,
			Boulder2Model,
			Boulder3Model
		};
}
