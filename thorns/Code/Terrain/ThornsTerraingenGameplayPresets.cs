#nullable disable

using Terraingen.Clutter;
using Terraingen.Foliage;

namespace Sandbox;

/// <summary>
/// Thorns-specific tuning for terraingen foliage/clutter (performance + no duplicate harvest trees).
/// Applied when <see cref="ThornsTerraingenTerrainRuntime"/> starts foliage/clutter on a terraingen chunk.
/// </summary>
static class ThornsTerraingenGameplayPresets
{
	public static void ApplyFoliage( ThornsFoliageConfig config )
	{
		if ( config is null )
			return;

		config.SpawnGuaranteedTreesAtOrigin = false;
		config.DebugForceCenterRing = false;
		config.LimitSpawnToCenterRadius = false;
		config.VerboseDebug = false;

		config.PineModel = "models/foliage2/pine_tree.vmdl";
		config.AspenModel = "models/foliage2/aspen_tree.vmdl";
		config.OakModel = "models/foliage2/oak_tree.vmdl";
		config.SpawnAsHarvestableWoodTrees = true;

		config.GlobalDensity = Math.Min( config.GlobalDensity, 0.78f );
		config.MaxTreeClustersPerChunk = Math.Min( config.MaxTreeClustersPerChunk, 8 );
		config.ChunksPerFrame = Math.Min( config.ChunksPerFrame, 2 );
		config.MaxInstancesPerPopulateFrame = Math.Min( config.MaxInstancesPerPopulateFrame, 20 );
		config.InstanceLodIntervalSeconds = Math.Max( config.InstanceLodIntervalSeconds, 0.2f );
		config.LodChunksUpdatedPerFrame = Math.Min( config.LodChunksUpdatedPerFrame, 2 );
		config.ForestMassBlurPasses = Math.Min( config.ForestMassBlurPasses, 1 );
		config.MaxForestMassesPerChunk = Math.Min( config.MaxForestMassesPerChunk, 2 );
	}

	public static void ApplyClutter( ThornsClutterConfig config )
	{
		if ( config is null )
			return;

		config.ShowDebug = false;
		config.GrassModel = "models/clutter/grass_common_short.vmdl";
		config.RockModelA = "models/clutter/rock1.vmdl";
		config.RockModelB = "models/clutter/rock2.vmdl";
		config.GrassRenderRadius = Math.Max( config.GrassRenderRadius, 70f * ThornsClutterConfig.InchesPerMeter );
		config.GrassFullDensityRadius = Math.Max( config.GrassFullDensityRadius, 28f * ThornsClutterConfig.InchesPerMeter );
		config.GrassCellSize = 8f * ThornsClutterConfig.InchesPerMeter;
		config.BladesPerCellNear = 270;
		config.BladesPerCellMid = 135;
		config.BladesPerCellFar = 45;
		config.GrassMinScale = 0.75f;
		config.GrassMaxScale = 1.35f;
		config.GrassMaxVisibleInstances = Math.Max( config.GrassMaxVisibleInstances, 48000 );

		// ~10 m bubble centered on the local pawn — dense inside, none beyond.
		config.ClutterRadius = Math.Min( config.ClutterRadius, ThornsClutterConfig.TenMetersInches );
		config.DistanceFadeEnd = Math.Min( config.DistanceFadeEnd, config.ClutterRadius );
		config.DistanceFadeStart = Math.Min( config.DistanceFadeStart, config.ClutterRadius * 0.78f );
		config.FollowLocalPawn = true;

		config.DensityMultiplier = Math.Max( config.DensityMultiplier, 3.5f );
		config.GrassDensityMultiplier = Math.Max( config.GrassDensityMultiplier, 6f );
		config.GrassInstanceMultiplier = Math.Max( config.GrassInstanceMultiplier, 3f );
		config.NearPlayerGrassBoost = Math.Max( config.NearPlayerGrassBoost, 2f );
		config.MaxInstancesPerChunk = Math.Max( config.MaxInstancesPerChunk, 5120 );
		config.GrassPlacementClusterSize = Math.Max( config.GrassPlacementClusterSize, 2 );
		config.ChunksBuiltPerRefresh = Math.Max( config.ChunksBuiltPerRefresh, 4 );
		config.RockInstanceMix = Math.Clamp( config.RockInstanceMix, 0.04f, 0.1f );
		config.RockScaleMultiplier = Math.Min( config.RockScaleMultiplier, 0.5f );
	}
}
