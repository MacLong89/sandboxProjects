namespace Terraingen.Clutter;

public sealed class ThornsClutterChunk
{
	public Vector2Int Cell { get; init; }
	public Vector3 Center { get; init; }
	public GameObject Root { get; init; }
	public int InstanceCount { get; set; }
	public int GrassCount { get; set; }
	public int RockCount { get; set; }
	public float LastBuildMilliseconds { get; set; }
	public ThornsClutterChunkInstances Instances { get; set; }
}
