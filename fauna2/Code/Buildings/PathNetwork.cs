namespace Fauna2;

/// <summary>
/// Tracks the zoo entrance and which path tiles are connected to it.
/// Guests can only enter when an entrance exists and at least one path
/// links to it; ambient walkers stay on connected paths only.
/// </summary>
public static class PathNetwork
{
	public static float ConnectDistance => GameConstants.PathConnectDistance;

	private static HashSet<PlaceableComponent> _connectedCache;
	private static HashSet<(int x, int y)> _connectedPathCellsCache;
	private static bool _cacheValid;
	private static bool _pathCellsCacheValid;

	public static void Invalidate()
	{
		_cacheValid = false;
		_connectedCache = null;
		_pathCellsCacheValid = false;
		_connectedPathCellsCache = null;
	}

	public static bool HasEntrance => PlaceableRegistry.Entrance.IsValid();

	public static bool HasGuestAccess => HasEntrance && GetConnectedPaths().Count > 0;

	public static PlaceableComponent Entrance => PlaceableRegistry.Entrance;

	public static bool AreAdjacent( Vector3 a, Vector3 b ) =>
		a.WithZ( 0 ).Distance( b.WithZ( 0 ) ) <= ConnectDistance;

	public static bool AreAdjacent( PlaceableComponent a, PlaceableComponent b ) =>
		a.IsValid() && b.IsValid()
		&& AreAdjacent( a.GameObject.WorldPosition, b.GameObject.WorldPosition );

	/// <summary>All path tiles reachable from the entrance through adjacent paths.</summary>
	public static HashSet<PlaceableComponent> GetConnectedPaths()
	{
		if ( _cacheValid && _connectedCache is not null )
			return _connectedCache;

		_connectedCache = ComputeConnectedPaths();
		_cacheValid = true;
		return _connectedCache;
	}

	private static HashSet<PlaceableComponent> ComputeConnectedPaths()
	{
		var connected = new HashSet<PlaceableComponent>();
		var entrance = Entrance;
		if ( !entrance.IsValid() ) return connected;

		var queue = new Queue<PlaceableComponent>();

		foreach ( var path in PlaceableRegistry.PathList )
		{
			if ( !SharesAdjacentBuildCell( entrance, path ) ) continue;
			connected.Add( path );
			queue.Enqueue( path );
		}

		while ( queue.Count > 0 )
		{
			var current = queue.Dequeue();

			foreach ( var path in PlaceableRegistry.PathList )
			{
				if ( connected.Contains( path ) ) continue;
				if ( !SharesAdjacentBuildCell( current, path ) ) continue;

				connected.Add( path );
				queue.Enqueue( path );
			}
		}

		return connected;
	}

	public static bool IsConnectedPath( PlaceableComponent path ) =>
		path.IsValid() && GetConnectedPaths().Contains( path );

	/// <summary>Would a new path tile at this position link to the entrance network?</summary>
	public static bool WouldPathConnect( Vector3 position, Vector2 footprint )
	{
		if ( !HasEntrance ) return false;

		var snapped = BuildSnap.SnapPlacement( position, footprint );
		var newCells = GetOccupiedBuildCells( snapped, footprint );
		if ( newCells.Count == 0 ) return false;

		var entrance = Entrance;
		if ( entrance.IsValid() )
		{
			var entranceFootprint = entrance.Definition?.EffectiveFootprint ?? GameConstants.EntranceFootprint;
			var entranceCells = GetOccupiedBuildCells( entrance.GameObject.WorldPosition, entranceFootprint );
			if ( CellsAreAdjacent( newCells, entranceCells ) )
				return true;
		}

		foreach ( var path in GetConnectedPaths() )
		{
			var pathFootprint = path.Definition?.EffectiveFootprint ?? footprint;
			var pathCells = GetOccupiedBuildCells( path.GameObject.WorldPosition, pathFootprint );
			if ( CellsAreAdjacent( newCells, pathCells ) )
				return true;
		}

		return false;
	}

	public static bool WouldPathConnect( Vector3 position ) =>
		WouldPathConnect( position, new Vector2( GameConstants.TileSize, GameConstants.TileSize ) );

	private static bool SharesAdjacentBuildCell( PlaceableComponent a, PlaceableComponent b )
	{
		if ( !a.IsValid() || !b.IsValid() ) return false;

		var fpA = a.Definition?.EffectiveFootprint ?? new Vector2( GameConstants.TileSize, GameConstants.TileSize );
		var fpB = b.Definition?.EffectiveFootprint ?? new Vector2( GameConstants.TileSize, GameConstants.TileSize );
		var cellsA = GetOccupiedBuildCells( a.GameObject.WorldPosition, fpA );
		var cellsB = GetOccupiedBuildCells( b.GameObject.WorldPosition, fpB );
		return CellsAreAdjacent( cellsA, cellsB );
	}

	private static bool CellsAreAdjacent( HashSet<(int x, int y)> a, HashSet<(int x, int y)> b )
	{
		foreach ( var cellA in a )
		{
			foreach ( var cellB in b )
			{
				if ( ChebyshevDistance( cellA, cellB ) <= 1 )
					return true;
			}
		}

		return false;
	}

	private static int ChebyshevDistance( (int x, int y) a, (int x, int y) b ) =>
		Math.Max( Math.Abs( a.x - b.x ), Math.Abs( a.y - b.y ) );

	/// <summary>
	/// Whether any footprint cell is on a path cell (gap 0) or separated from one by up to
	/// <paramref name="maxGapTiles"/> empty build cells.
	/// </summary>
	public static bool IsNearConnectedPath( Vector3 position, Vector2 footprint, int maxGapTiles = GameConstants.AmenityPathGapTiles )
	{
		if ( !HasGuestAccess ) return false;

		var buildingCells = GetOccupiedBuildCells( position, footprint );
		if ( buildingCells.Count == 0 ) return false;

		var pathCells = GetConnectedPathCells();
		if ( pathCells.Count == 0 ) return false;

		var minChebyshev = MinChebyshevDistance( buildingCells, pathCells );
		return minChebyshev >= 1 && minChebyshev <= maxGapTiles + 1;
	}

	static HashSet<(int x, int y)> GetOccupiedBuildCells( Vector3 center, Vector2 footprint )
	{
		var snapped = BuildSnap.SnapPlacement( center, footprint );
		var halfX = footprint.x * 0.5f;
		var halfY = footprint.y * 0.5f;

		var cells = new HashSet<(int x, int y)>();
		foreach ( var tile in GroundGrid.TileCentersInRect(
			snapped.x - halfX,
			snapped.y - halfY,
			snapped.x + halfX,
			snapped.y + halfY ) )
		{
			cells.Add( BuildCellIndex( tile.x, tile.y ) );
		}

		return cells;
	}

	static HashSet<(int x, int y)> GetConnectedPathCells()
	{
		if ( _pathCellsCacheValid && _connectedPathCellsCache is not null )
			return _connectedPathCellsCache;

		var cells = new HashSet<(int x, int y)>();

		if ( Entrance.IsValid() )
			AddFootprintCells( cells, Entrance );

		foreach ( var path in GetConnectedPaths() )
			AddFootprintCells( cells, path );

		_connectedPathCellsCache = cells;
		_pathCellsCacheValid = true;
		return cells;
	}

	static void AddFootprintCells( HashSet<(int x, int y)> cells, PlaceableComponent placeable )
	{
		if ( !placeable.IsValid() ) return;

		var footprint = placeable.Definition?.EffectiveFootprint ?? new Vector2( GameConstants.TileSize, GameConstants.TileSize );
		foreach ( var tile in GetOccupiedBuildCells( placeable.GameObject.WorldPosition, footprint ) )
			cells.Add( tile );
	}

	static (int x, int y) BuildCellIndex( float worldX, float worldY )
	{
		var tile = GameConstants.TileSize;
		return (
			(int)MathF.Floor( worldX / tile ),
			(int)MathF.Floor( worldY / tile ) );
	}

	static int MinChebyshevDistance( HashSet<(int x, int y)> a, HashSet<(int x, int y)> b )
	{
		var best = int.MaxValue;

		foreach ( var cellA in a )
		{
			foreach ( var cellB in b )
			{
				var dist = Math.Max(
					Math.Abs( cellA.x - cellB.x ),
					Math.Abs( cellA.y - cellB.y ) );

				if ( dist < best )
					best = dist;
			}
		}

		return best;
	}

	public static PlaceableComponent NearestConnectedPath( Vector3 point, float maxDistance )
	{
		PlaceableComponent best = null;
		var bestDist = maxDistance;

		foreach ( var path in GetConnectedPaths() )
		{
			var d = path.GameObject.WorldPosition.WithZ( 0 ).Distance( point.WithZ( 0 ) );
			if ( d < bestDist )
			{
				bestDist = d;
				best = path;
			}
		}

		return best;
	}
}
