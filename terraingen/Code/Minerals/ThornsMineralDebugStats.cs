namespace Terraingen.Minerals;

public sealed class ThornsMineralDebugStats
{
	public bool PopulateStarted;
	public bool PopulateComplete;
	public bool ModelsLoaded;
	public string LastError = "";
	public string LoadedModelPath = "";
	public int ChunksTotal;
	public int ChunksProcessed;
	public int InstancesSpawned;
	public int StoneSpawned;
	public int OreSpawned;
	public int BiomeRejected;
	public int MaterialRejected;
	public int RayMisses;
	public int EnabledChunksNearPlayer;
}
