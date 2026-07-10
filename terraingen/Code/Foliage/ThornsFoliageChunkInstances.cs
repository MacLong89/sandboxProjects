namespace Terraingen.Foliage;

/// <summary>GPU-instanced tree transforms for one foliage chunk (per species list).</summary>
public sealed class ThornsFoliageChunkInstances
{
	public Vector2Int Cell { get; init; }
	public Vector3 Center { get; init; }
	public List<Transform> Pine { get; } = new( 64 );
	public List<Transform> Aspen { get; } = new( 32 );
	public List<Transform> Oak { get; } = new( 32 );

	public int TotalCount => Pine.Count + Aspen.Count + Oak.Count;

	public bool Culled { get; set; }
	public bool Visible { get; set; } = true;
	public bool CastShadows { get; set; } = true;

	public List<Transform> GetList( FoliageSpecies species ) => species switch
	{
		FoliageSpecies.Pine => Pine,
		FoliageSpecies.Aspen => Aspen,
		FoliageSpecies.Oak => Oak,
		_ => Pine
	};

	public void Clear()
	{
		Pine.Clear();
		Aspen.Clear();
		Oak.Clear();
	}
}
