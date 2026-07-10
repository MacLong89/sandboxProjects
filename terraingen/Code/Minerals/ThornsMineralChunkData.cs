namespace Terraingen.Minerals;

public sealed class ThornsMineralChunkData
{
	public Vector2Int Cell;
	public Vector3 Center;
	public GameObject Root;
	public int InstanceCount;
	public int StoneCount;
	public int OreCount;
	public ThornsMineralChunkInstances Instances { get; init; }
	public bool ShadowsEnabled { get; set; }
}
