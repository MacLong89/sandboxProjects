using System.Collections.Generic;

namespace Sandbox;

/// <summary>Host-only services and mutable outputs wired from <see cref="ThornsTerrainSystem"/>.</summary>
public sealed class ThornsWorldGenerationHostBridge
{
	public ThornsTerrainSystem Terrain { get; }
	public GameObject ChunkRoot { get; }
	public Scene Scene => ChunkRoot?.Scene;

	public List<Vector2> SiteFootprintsLocal { get; }
	public List<ThornsWorldGenProcBuildingFootprint> BuildingFootprints { get; }
	public List<ThornsWorldGenProcBuildingInteriorLoot> BuildingsForLoot { get; }

	public bool GenerateProceduralBuildings => Terrain.GenerateProceduralBuildings;
	public bool ScatterProceduralSites => Terrain.ScatterProceduralSites;
	public bool DrawSettlementLayoutDebug => Terrain.DrawSettlementLayoutDebug;
	public bool DebugBuildingTypeColors => Terrain.DebugBuildingTypeColors;
	public bool OrganicClusterPlacement => Terrain.OrganicClusterPlacement;
	public int OrganicBuildingCount => Terrain.OrganicBuildingCount;
	public float OrganicClusterBias => Terrain.OrganicClusterBias;
	public float OrganicBuildingBufferCells => Terrain.OrganicBuildingBufferCells;
	public float OrganicBuildingVerticalLift => Terrain.OrganicBuildingVerticalLift;
	public bool TerrainFirstClusterPlacement => Terrain.TerrainFirstClusterPlacement;
	public bool SkipLegacySettlementLayout =>
		Terrain.OrganicClusterPlacement || Terrain.TerrainFirstClusterPlacement;
	public float ScatterEdgeInsetFraction => Terrain.ScatterEdgeInsetFraction;

	public ThornsWorldGenerationHostBridge(
		ThornsTerrainSystem terrain,
		GameObject chunkRoot,
		List<Vector2> siteFootprintsLocal,
		List<ThornsWorldGenProcBuildingFootprint> buildingFootprints,
		List<ThornsWorldGenProcBuildingInteriorLoot> buildingsForLoot )
	{
		Terrain = terrain;
		ChunkRoot = chunkRoot;
		SiteFootprintsLocal = siteFootprintsLocal;
		BuildingFootprints = buildingFootprints;
		BuildingsForLoot = buildingsForLoot;
	}

	public ThornsWorldSettlementConfig BuildSettlementConfig( float worldWidth, float worldDepth ) =>
		Terrain.BuildSettlementConfig( worldWidth, worldDepth );

	public void FinalizePreChunkGeneration( ThornsWorldGenerationContext context )
	{
		context.Spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();
		Terrain.CopyRoadTuningToSpec( context.Spec );
		ThornsTerrainDecorScatter.CopyHostDecorTuning( context.Spec, Terrain );
		Terrain.AdoptWorldGenScatterHeightmap( context );
		Terrain.RebuildProcBuildingFootprintIndex();
		Terrain.PushSpecToChunk( context.Spec );

		if ( Networking.IsActive && Networking.IsHost )
		{
			ThornsPoiAuthority.SpawnHostSingleton();
			ThornsPoiAuthority.Instance?.HostRebuildFromSceneMarkers( force: true );
			ThornsPoiAuthority.DelayedHostRebuildFromSceneMarkers();
		}
	}
}
