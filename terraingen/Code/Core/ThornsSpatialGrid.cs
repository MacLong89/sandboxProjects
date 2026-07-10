namespace Terraingen.Core;

/// <summary>Uniform grid for O(1) average neighbor queries (animals, NPCs).</summary>
public sealed class ThornsSpatialGrid<T> where T : class
{
	readonly float _cellSize;
	readonly Dictionary<long, List<T>> _cells = new();
	readonly Dictionary<T, long> _cellByItem = new();

	public ThornsSpatialGrid( float cellSize )
	{
		_cellSize = Math.Max( 64f, cellSize );
	}

	public void Clear()
	{
		_cells.Clear();
		_cellByItem.Clear();
	}

	public void Insert( T item, Vector3 worldPosition )
	{
		if ( item is null )
			return;

		Remove( item );
		var key = CellKey( worldPosition );
		if ( !_cells.TryGetValue( key, out var list ) )
		{
			list = new List<T>( 8 );
			_cells[key] = list;
		}

		list.Add( item );
		_cellByItem[item] = key;
	}

	public void Remove( T item )
	{
		if ( item is null || !_cellByItem.TryGetValue( item, out var key ) )
			return;

		if ( _cells.TryGetValue( key, out var list ) )
			list.Remove( item );

		_cellByItem.Remove( item );
	}

	public void Update( T item, Vector3 worldPosition )
	{
		if ( item is null )
			return;

		var newKey = CellKey( worldPosition );
		if ( _cellByItem.TryGetValue( item, out var oldKey ) && oldKey == newKey )
			return;

		Insert( item, worldPosition );
	}

	public void QueryRadius( Vector3 center, float radius, List<T> results, bool planar = false )
	{
		results.Clear();
		if ( radius <= 0f )
			return;

		var cellRadius = (int)Math.Ceiling( radius / _cellSize );
		var cx = (int)MathF.Floor( center.x / _cellSize );
		var cy = (int)MathF.Floor( center.y / _cellSize );
		var radiusSq = radius * radius;

		for ( var ox = -cellRadius; ox <= cellRadius; ox++ )
		{
			for ( var oy = -cellRadius; oy <= cellRadius; oy++ )
			{
				var key = PackCell( cx + ox, cy + oy );
				if ( !_cells.TryGetValue( key, out var list ) )
					continue;

				for ( var i = 0; i < list.Count; i++ )
				{
					var item = list[i];
					if ( item is null )
						continue;

					if ( item is Component component && component.IsValid() )
					{
						var pos = component.GameObject.WorldPosition;
						var distSq = planar
							? (pos.WithZ( 0 ) - center.WithZ( 0 )).LengthSquared
							: (pos - center).LengthSquared;
						if ( distSq > radiusSq )
							continue;
					}

					results.Add( item );
				}
			}
		}
	}

	long CellKey( Vector3 p ) => PackCell(
		(int)MathF.Floor( p.x / _cellSize ),
		(int)MathF.Floor( p.y / _cellSize ) );

	static long PackCell( int x, int y ) => ((long)x << 32) ^ (uint)y;
}
