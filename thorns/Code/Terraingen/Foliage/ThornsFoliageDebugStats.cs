namespace Terraingen.Foliage;

public sealed class ThornsFoliageDebugStats
{
	public int ChunksTotal;
	public int ChunksProcessed;
	public int InstancesSpawned;
	public int ClustersAttempted;
	public int ClustersPlaced;
	public int RayMisses;
	public int BiomeRejected;
	public int SlopeFlatRejected;
	public int WeightRejected;
	public int TreesSpawned;
	public int EnabledChunksNearPlayer;
	public float NearestInstanceDistance = -1f;
	public Vector3? NearestInstancePosition;
	public Vector3? LastSpawnPosition;
	public string LastSpawnSpecies = "";
	public Vector3 LastModelBoundsSize;
	public float LastSpawnScale;
	public bool ModelsLoaded;
	public bool PopulateStarted;
	public bool PopulateComplete;
	public string LastError = "";
}
