namespace Terraingen.Foliage;

public sealed class ThornsFoliageChunkData
{
	public Vector2Int Cell { get; init; }
	public Vector3 Center { get; init; }
	public GameObject Root { get; init; }
	public int InstanceCount { get; init; }
	public ThornsFoliageChunkInstances Instances { get; init; }

	/// <summary>Cached LOD tags for scene-tree chunks — avoids per-frame GetComponent scans.</summary>
	public List<ThornsFoliageInstance> LodInstances { get; init; }
}
