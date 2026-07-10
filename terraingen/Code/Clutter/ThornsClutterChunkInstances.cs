namespace Terraingen.Clutter;

/// <summary>GPU-instanced mesh clutter transforms for one streaming chunk.</summary>
public sealed class ThornsClutterChunkInstances
{
	public Vector2Int Cell { get; init; }
	public Vector3 Center { get; init; }
	public Dictionary<int, List<Transform>> ByModelIndex { get; } = new();
	public bool Culled { get; set; }

	public int TotalCount
	{
		get
		{
			var total = 0;
			foreach ( var list in ByModelIndex.Values )
				total += list.Count;

			return total;
		}
	}

	public List<Transform> GetList( int modelIndex )
	{
		if ( !ByModelIndex.TryGetValue( modelIndex, out var list ) )
		{
			list = new List<Transform>( 32 );
			ByModelIndex[modelIndex] = list;
		}

		return list;
	}
}
