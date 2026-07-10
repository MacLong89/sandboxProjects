namespace Sandbox;

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.TerrainGen;

/// <summary>Terrain chunk spawn, spec build, network replication bootstrap.</summary>
public static class ThornsTerrainChunkLifecycleService
{
	public static ThornsTerrainNetSpec BuildSpec( ThornsTerrainSystem terrain, out int resolvedLayoutSeed )
	{
		var layoutSeed = terrain.TerrainSeed;
		if ( terrain.RandomizeSeedOnHost && ( !Networking.IsActive || Networking.IsHost ) )
			layoutSeed = Random.Shared.Next();

		terrain.TerraingenConfig ??= new ThornsTerrainConfig();
		var mapShapeSeed = terrain.TerraingenConfig.WorldSeed != 0 ? terrain.TerraingenConfig.WorldSeed : terrain.TerrainSeed;
		if ( mapShapeSeed == 0 )
			mapShapeSeed = 42069;
		terrain.TerraingenConfig.WorldSeed = mapShapeSeed;

		var foliage = terrain.TerraingenFoliageConfig ??= new ThornsFoliageConfig();
		foliage.FoliageSeed = mapShapeSeed;
		var clutter = terrain.TerraingenClutterConfig ??= new ThornsClutterConfig();
		clutter.WorldSeed = mapShapeSeed;
		ThornsTerraingenTerrainRuntime.BindConfigs( terrain.TerraingenConfig, foliage, clutter );

		var terraSpec = new ThornsTerrainNetSpec();
		ThornsTerraingenTerrainRuntime.ApplyToNetSpec( terraSpec, terrain.TerraingenConfig, mapShapeSeed );
		terraSpec.Seed = layoutSeed;
		terraSpec.DecorEdgeInsetFraction = terrain.ScatterEdgeInsetFraction;
		terraSpec.DecorEnableFoliageDistanceCulling = terrain.EnableFoliageDistanceCulling;
		resolvedLayoutSeed = layoutSeed;
		return terraSpec;
	}

	public static void TrySpawnChunk( ThornsTerrainSystem terrain, ThornsTerrainOrchestrationState state )
	{
		if ( state.Spawned )
			return;

		state.Spawned = true;
		var spec = BuildSpec( terrain, out var resolvedSeed );
		state.ResolvedWorldGenerationSeed = resolvedSeed;
		ThornsLobbyWorldSeed.PublishIfHost( spec.Seed );

		var go = new GameObject( true, "ThornsTerrainChunk" );
		go.WorldPosition = terrain.GameObject.WorldPosition;
		go.WorldRotation = terrain.GameObject.WorldRotation;
		go.WorldScale = Vector3.One;
		go.Tags.Add( "thorns_terrain" );
		ThornsAnchoredWorldPhysics.EnsureWorldSolidTags( go );
		state.ChunkRoot = go;

		state.PendingChunkSpec = spec;
		if ( ThornsWorldGenRunnerService.BeginPreChunkWorldGen( terrain, state, spec ) )
			return;

		CompleteChunkNetworkSpawn( terrain, state, spec );
		state.PendingChunkSpec = null;
	}

	public static void CompleteChunkNetworkSpawn(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		if ( !state.ChunkRoot.IsValid() )
			return;

		var go = state.ChunkRoot;
		var chunk = go.Components.Get<ThornsTerrainChunk>();
		if ( !chunk.IsValid() )
			chunk = go.Components.Create<ThornsTerrainChunk>();

		PushSpecToChunk( state, spec );

		if ( Networking.IsActive )
		{
			go.NetworkMode = NetworkMode.Object;
			go.NetworkSpawn();
		}
		else
			chunk.ApplySpecLocal( spec );

		Log.Info(
			$"[Thorns] Terrain chunk spawned mode=terraingen network={Networking.IsActive} layoutSeed={spec.Seed} mapSeed={spec.TerraingenWorldSeed} res={spec.HeightmapResolutionX}x{spec.HeightmapResolutionZ} world={spec.WorldWidth:F0}." );

		ThornsWorldGenRunnerService.RunPostChunkPhases( terrain, state, spec );

		if ( Networking.IsHost )
			ThornsPoiAuthority.DelayedHostRebuildFromSceneMarkers( 0.15f );
	}

	public static void PushSpecToChunk( ThornsTerrainOrchestrationState state, ThornsTerrainNetSpec spec ) =>
		ThornsTerrainReplicaService.PushSpecToChunk( state.ChunkRoot, spec );

	public static void CopyRoadTuningToSpec( ThornsTerrainSystem terrain, ThornsTerrainNetSpec spec )
	{
		spec.RoadTuning ??= ThornsTerrainRoadTuningNet.EngineDefaults();
		spec.RoadTuning.CityFlattenStrength = terrain.RoadCityFlattenStrength;
		spec.RoadTuning.TownFlattenStrength = terrain.RoadTownFlattenStrength;
		spec.RoadTuning.TrailFlattenStrength = terrain.RoadTrailFlattenStrength;
		spec.RoadTuning.CityEdgeFalloff = terrain.RoadCityEdgeFalloff;
		spec.RoadTuning.TownEdgeFalloff = terrain.RoadTownEdgeFalloff;
		spec.RoadTuning.TrailEdgeFalloff = terrain.RoadTrailEdgeFalloff;
		spec.RoadTuning.FoliageClearanceRadius = terrain.RoadFoliageClearanceRadius;
		spec.RoadTuning.BoulderClearanceRadius = terrain.RoadBoulderClearanceRadius;
	}

	public static void DestroyChunk( ThornsTerrainOrchestrationState state )
	{
		if ( state.ChunkRoot.IsValid() )
			state.ChunkRoot.Destroy();
		state.ChunkRoot = default;
	}
}
