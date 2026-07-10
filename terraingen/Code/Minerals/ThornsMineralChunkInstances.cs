namespace Terraingen.Minerals;

/// <summary>GPU-instanced mineral transforms for one populate chunk.</summary>
public sealed class ThornsMineralChunkInstances
{
	public Vector2Int Cell { get; init; }
	public Vector3 Center { get; init; }
	public List<Transform> Stone { get; } = new( 48 );
	public List<Transform> Ore { get; } = new( 16 );
	public List<int> StoneNodeIds { get; } = new( 48 );
	public List<int> OreNodeIds { get; } = new( 16 );

	public int TotalCount => Stone.Count + Ore.Count;

	public bool Culled { get; set; }
	public bool CastShadows { get; set; } = true;
}
