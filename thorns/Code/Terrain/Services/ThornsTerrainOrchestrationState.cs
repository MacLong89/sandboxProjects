namespace Sandbox;

/// <summary>Mutable terrain boot/scatter/chunk state — owned by <see cref="ThornsTerrainSystem"/> orchestration.</summary>
public sealed class ThornsTerrainOrchestrationState
{
	public GameObject ChunkRoot;
	public bool Spawned;
	public int ResolvedWorldGenerationSeed;

	public bool ResourceScatterDone;
	public bool InteriorLootScatterDone;
	public bool InteriorFurnitureScatterDone;
	public int FurnitureCatalogScaleRevisionApplied = -1;
	public bool InteriorDefenderScatterDone;
	public bool BoulderScatterDone;
	public bool ProceduralSiteScatterDone;

	public readonly List<Vector2> SiteFootprintsChunkLocal = new();
	public readonly List<ThornsWorldGenProcBuildingFootprint> ProcBuildingFootprintsChunk = new();
	public readonly List<ThornsWorldGenProcBuildingInteriorLoot> ProcBuildingsForLoot = new();
	public readonly Dictionary<long, List<int>> FootprintSpatialIndex = new();

	public float[] WorldGenScatterHeights;

	public ThornsDeferredWorldGenerationSession DeferredPreChunkWorldGen;
	public ThornsTerrainNetSpec PendingChunkSpec;
	public bool AwaitingPreChunkWorldGen;
}
