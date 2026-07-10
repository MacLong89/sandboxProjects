using Terraingen.Clutter;
using Terraingen.Foliage;

namespace Sandbox;

public enum ThornsPerformanceQuality
{
	/// <summary>Lowest grass/clutter density and shortest draw distances.</summary>
	Low,

	/// <summary>Default gameplay balance between look and performance.</summary>
	Medium,

	/// <summary>Higher foliage density and longer draw distances.</summary>
	High,

	/// <summary>Maximum visual quality — scenic visibility tier.</summary>
	Ultra,
}

/// <summary>
/// Graphics/performance presets — drives visibility tier, streaming budgets, and grass/foliage caps.
/// Default: <see cref="ThornsPerformanceQuality.Medium"/>.
/// </summary>
public static class ThornsPerformanceQualityPresets
{
	public static ThornsPerformanceQuality ActiveQuality { get; set; } = ThornsPerformanceQuality.Medium;

	public readonly struct Settings
	{
		public ThornsVisibilityTier VisibilityTier { get; init; }
		public int DeferredHostSpawnsPerFrame { get; init; }
		public int FoliageChunksPerFrame { get; init; }
		public int FoliageInstancesPerFrame { get; init; }
		public int GrassTilesPerFrame { get; init; }
		public float GrassBuildBudgetMs { get; init; }
		public float GrassRadiusMeters { get; init; }
		public float GrassFullRadiusMeters { get; init; }
		public int GrassDensityNear { get; init; }
		public int GrassDensityMid { get; init; }
		public int GrassDensityFar { get; init; }
		public int GrassMaxVisibleInstances { get; init; }
		public float FoliageGlobalDensityCap { get; init; }
		public int MaxTreeClustersPerChunk { get; init; }
		public int FoliageLodChunksPerFrame { get; init; }
		public float CelestialVisualHz { get; init; }
		public int FoliageCullMaxProcessedPerStep { get; init; }
		public float FoliageCullUpdateIntervalSeconds { get; init; }
	}

	public static Settings Get( ThornsPerformanceQuality quality ) => quality switch
	{
		ThornsPerformanceQuality.Low => new Settings
		{
			VisibilityTier = ThornsVisibilityTier.Performance,
			DeferredHostSpawnsPerFrame = 10,
			FoliageChunksPerFrame = 1,
			FoliageInstancesPerFrame = 10,
			GrassTilesPerFrame = 1,
			GrassBuildBudgetMs = 1.5f,
			GrassRadiusMeters = 45f,
			GrassFullRadiusMeters = 18f,
			GrassDensityNear = 120,
			GrassDensityMid = 55,
			GrassDensityFar = 0,
			GrassMaxVisibleInstances = 24000,
			FoliageGlobalDensityCap = 0.55f,
			MaxTreeClustersPerChunk = 5,
			FoliageLodChunksPerFrame = 2,
			CelestialVisualHz = 8f,
			FoliageCullMaxProcessedPerStep = 1600,
			FoliageCullUpdateIntervalSeconds = 0.24f,
		},
		ThornsPerformanceQuality.High => new Settings
		{
			VisibilityTier = ThornsVisibilityTier.Balanced,
			DeferredHostSpawnsPerFrame = 48,
			FoliageChunksPerFrame = 2,
			FoliageInstancesPerFrame = 20,
			GrassTilesPerFrame = 1,
			GrassBuildBudgetMs = 2.5f,
			GrassRadiusMeters = 70f,
			GrassFullRadiusMeters = 28f,
			GrassDensityNear = 270,
			GrassDensityMid = 135,
			GrassDensityFar = 45,
			GrassMaxVisibleInstances = 48000,
			FoliageGlobalDensityCap = 0.78f,
			MaxTreeClustersPerChunk = 8,
			FoliageLodChunksPerFrame = 3,
			CelestialVisualHz = 15f,
			FoliageCullMaxProcessedPerStep = 3200,
			FoliageCullUpdateIntervalSeconds = 0.16f,
		},
		ThornsPerformanceQuality.Ultra => new Settings
		{
			VisibilityTier = ThornsVisibilityTier.Scenic,
			DeferredHostSpawnsPerFrame = 96,
			FoliageChunksPerFrame = 2,
			FoliageInstancesPerFrame = 24,
			GrassTilesPerFrame = 2,
			GrassBuildBudgetMs = 3.0f,
			GrassRadiusMeters = 85f,
			GrassFullRadiusMeters = 35f,
			GrassDensityNear = 320,
			GrassDensityMid = 160,
			GrassDensityFar = 55,
			GrassMaxVisibleInstances = 64000,
			FoliageGlobalDensityCap = 0.85f,
			MaxTreeClustersPerChunk = 10,
			FoliageLodChunksPerFrame = 4,
			CelestialVisualHz = 20f,
			FoliageCullMaxProcessedPerStep = 4000,
			FoliageCullUpdateIntervalSeconds = 0.14f,
		},
		_ => new Settings
		{
			VisibilityTier = ThornsVisibilityTier.Balanced,
			DeferredHostSpawnsPerFrame = 24,
			FoliageChunksPerFrame = 1,
			FoliageInstancesPerFrame = 16,
			GrassTilesPerFrame = 1,
			GrassBuildBudgetMs = 2.0f,
			GrassRadiusMeters = 55f,
			GrassFullRadiusMeters = 25f,
			GrassDensityNear = 180,
			GrassDensityMid = 90,
			GrassDensityFar = 30,
			GrassMaxVisibleInstances = 36000,
			FoliageGlobalDensityCap = 0.68f,
			MaxTreeClustersPerChunk = 6,
			FoliageLodChunksPerFrame = 3,
			CelestialVisualHz = 12f,
			FoliageCullMaxProcessedPerStep = 2400,
			FoliageCullUpdateIntervalSeconds = 0.2f,
		},
	};

	public static void ApplyToTerrainSystem( ThornsTerrainSystem terrain, ThornsPerformanceQuality quality )
	{
		if ( terrain is null || !terrain.IsValid() )
			return;

		ActiveQuality = quality;
		var s = Get( quality );
		terrain.VisibilityTier = s.VisibilityTier;
		terrain.DeferredHostSpawnsPerFrame = s.DeferredHostSpawnsPerFrame;

		if ( terrain.TerraingenFoliageConfig is not null )
		{
			ThornsTerraingenGameplayPresets.ApplyFoliage( terrain.TerraingenFoliageConfig );
			terrain.TerraingenFoliageConfig.ChunksPerFrame = Math.Min( terrain.TerraingenFoliageConfig.ChunksPerFrame, s.FoliageChunksPerFrame );
			terrain.TerraingenFoliageConfig.MaxInstancesPerPopulateFrame = Math.Min(
				terrain.TerraingenFoliageConfig.MaxInstancesPerPopulateFrame,
				s.FoliageInstancesPerFrame );
			terrain.TerraingenFoliageConfig.GlobalDensity = Math.Min( terrain.TerraingenFoliageConfig.GlobalDensity, s.FoliageGlobalDensityCap );
			terrain.TerraingenFoliageConfig.MaxTreeClustersPerChunk = Math.Min(
				terrain.TerraingenFoliageConfig.MaxTreeClustersPerChunk,
				s.MaxTreeClustersPerChunk );
			terrain.TerraingenFoliageConfig.LodChunksUpdatedPerFrame = Math.Min(
				terrain.TerraingenFoliageConfig.LodChunksUpdatedPerFrame,
				s.FoliageLodChunksPerFrame );
			terrain.TerraingenFoliageConfig.TreeLodShadowDistanceInches = Math.Min(
				terrain.TerraingenFoliageConfig.TreeLodShadowDistanceInches,
				s.VisibilityTier switch
				{
					ThornsVisibilityTier.Performance => 28000f,
					ThornsVisibilityTier.Scenic => 48000f,
					_ => 36000f
				} );
		}

		if ( terrain.TerraingenClutterConfig is not null )
		{
			ThornsTerraingenGameplayPresets.ApplyClutter( terrain.TerraingenClutterConfig );
			var maxClutterInches = ThornsFoliageStreamingTiers.VisualClutterMaxMeters * ThornsClutterConfig.InchesPerMeter;
			terrain.TerraingenClutterConfig.ClutterRadius = Math.Min( terrain.TerraingenClutterConfig.ClutterRadius, maxClutterInches );
			terrain.TerraingenClutterConfig.DistanceFadeEnd = Math.Min( terrain.TerraingenClutterConfig.DistanceFadeEnd, maxClutterInches );
			terrain.TerraingenClutterConfig.GrassMaxVisibleInstances = Math.Min(
				terrain.TerraingenClutterConfig.GrassMaxVisibleInstances,
				s.GrassMaxVisibleInstances );
		}

		ApplyGrassConVars( s );
		ThornsVisibilityPresets.ApplyToLocalPawnCamera( terrain.Scene, s.VisibilityTier );

		if ( ThornsDeferredHostSpawnQueue.Instance.IsValid() )
			ThornsDeferredHostSpawnQueue.Instance.WorkBudgetPerFrame = s.DeferredHostSpawnsPerFrame;

		ThornsFoliageDistanceCullSystem.ApplyQualityBudget( quality );
	}

	public static void ApplyGrassConVars( Settings s )
	{
		ClientGrassRenderer.GrassRadiusMeters = s.GrassRadiusMeters;
		ClientGrassRenderer.GrassFullRadiusMeters = s.GrassFullRadiusMeters;
		ClientGrassRenderer.GrassDensityNear = s.GrassDensityNear;
		ClientGrassRenderer.GrassDensityMid = s.GrassDensityMid;
		ClientGrassRenderer.GrassDensityFar = s.GrassDensityFar;
		ClientGrassRenderer.ApplyQualityBudget( s.GrassTilesPerFrame, s.GrassBuildBudgetMs );
	}

	public static void ApplyToActiveScene( ThornsPerformanceQuality quality )
	{
		ActiveQuality = quality;
		foreach ( var terrain in Game.ActiveScene?.GetAllComponents<ThornsTerrainSystem>() ?? Array.Empty<ThornsTerrainSystem>() )
		{
			if ( terrain.IsValid() )
				ApplyToTerrainSystem( terrain, quality );
		}

		foreach ( var celestial in Game.ActiveScene?.GetAllComponents<ThornsCelestialSystem>() ?? Array.Empty<ThornsCelestialSystem>() )
		{
			if ( celestial.IsValid() )
				celestial.ApplyPerformanceQuality( quality );
		}

		ThornsFoliageDistanceCullSystem.ApplyQualityBudget( quality );
	}
}
