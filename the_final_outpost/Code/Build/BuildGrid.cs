namespace FinalOutpost;

/// <summary>
/// Global placement grid over the whole world. Cells are <see cref="GameConstants.CellSize"/> wide
/// and signed (cell 0 sits just past the origin), so the grid tiles perfectly across the large
/// plots that surround the base. Which cells are actually buildable is decided by <see cref="BuildManager"/>
/// (plot ownership / clearing), not by this coordinate helper.
/// </summary>
public static class BuildGrid
{
	public static Vector3 CellToWorld( int cellX, int cellY )
	{
		var x = (cellX + 0.5f) * GameConstants.CellSize;
		var y = (cellY + 0.5f) * GameConstants.CellSize;
		var z = OutpostTerrain.SampleHeight( x, y );
		return new Vector3( x, y, z );
	}

	public static bool WorldToCell( Vector3 world, out int cellX, out int cellY )
	{
		cellX = (int)MathF.Floor( world.x / GameConstants.CellSize );
		cellY = (int)MathF.Floor( world.y / GameConstants.CellSize );
		return true;
	}

	public static bool WorldToCell( Vector2 world, out int cellX, out int cellY )
	{
		cellX = (int)MathF.Floor( world.x / GameConstants.CellSize );
		cellY = (int)MathF.Floor( world.y / GameConstants.CellSize );
		return true;
	}

	/// <summary>Command post footprint used to block building overlap near the core.</summary>
	public static Vector3 CommandPostFootprint => new( GameConstants.CellSize * 2f, GameConstants.CellSize * 2f, 0f );

	/// <summary>Tighter command-post blocker for unit pathing.</summary>
	public static Vector3 CommandPostCollisionFootprint => new(
		CommandPostFootprint.x * GameConstants.CommandPostCollisionScale,
		CommandPostFootprint.y * GameConstants.CommandPostCollisionScale,
		0f );

	/// <summary>XY footprint size for command-post approach (matches 2×2 core cells).</summary>
	public static Vector3 CommandPostZombieCollisionFootprint => CommandPostFootprint;

	/// <summary>True when two XY footprints intersect (centers + full visual sizes).</summary>
	public static bool FootprintsOverlap( Vector3 centerA, Vector3 sizeA, Vector3 centerB, Vector3 sizeB )
	{
		var halfA = sizeA.WithZ( 0 ) * 0.5f;
		var halfB = sizeB.WithZ( 0 ) * 0.5f;
		return MathF.Abs( centerA.x - centerB.x ) < halfA.x + halfB.x
		       && MathF.Abs( centerA.y - centerB.y ) < halfA.y + halfB.y;
	}

	/// <summary>Southwest (min) corner of the command post's 2×2 footprint.</summary>
	public static void GetCoreAnchor( out int anchorX, out int anchorY )
	{
		var o = OutpostManager.Instance;
		anchorX = o?.CoreAnchorCellX ?? -1;
		anchorY = o?.CoreAnchorCellY ?? -1;
	}

	/// <summary>World center of a command-post 2×2 whose southwest cell is <paramref name="anchorX"/>,<paramref name="anchorY"/>.</summary>
	public static Vector3 CoreWorldFromAnchor( int anchorX, int anchorY )
	{
		var a = CellToWorld( anchorX, anchorY );
		var b = CellToWorld( anchorX + 1, anchorY + 1 );
		var mid = (a + b) * 0.5f;
		mid.z = OutpostTerrain.SampleHeight( mid.x, mid.y );
		return mid;
	}

	/// <summary>Command post occupies a 2×2 block from the live (or default origin) anchor.</summary>
	public static bool IsCoreCell( int cellX, int cellY )
	{
		GetCoreAnchor( out var ax, out var ay );
		return (cellX == ax || cellX == ax + 1) && (cellY == ay || cellY == ay + 1);
	}

	public static bool IsCoreCellAt( int cellX, int cellY, int anchorX, int anchorY ) =>
		(cellX == anchorX || cellX == anchorX + 1) && (cellY == anchorY || cellY == anchorY + 1);

	/// <summary>Visits every grid cell whose area intersects an axis-aligned XY footprint.</summary>
	public static void ForEachCellInFootprint( Vector3 center, Vector3 footprintSize, Action<int, int> visit )
	{
		center = center.WithZ( 0f );
		var half = footprintSize.WithZ( 0f ) * 0.5f;
		var cellSize = GameConstants.CellSize;
		var minCellX = (int)MathF.Floor( (center.x - half.x) / cellSize );
		var maxCellX = (int)MathF.Floor( (center.x + half.x - 0.001f) / cellSize );
		var minCellY = (int)MathF.Floor( (center.y - half.y) / cellSize );
		var maxCellY = (int)MathF.Floor( (center.y + half.y - 0.001f) / cellSize );

		for ( var cellX = minCellX; cellX <= maxCellX; cellX++ )
		for ( var cellY = minCellY; cellY <= maxCellY; cellY++ )
			visit( cellX, cellY );
	}

	/// <summary>Visits every grid cell a circular agent footprint could overlap.</summary>
	public static void ForEachCellInRadius( Vector3 worldPos, float radius, Action<int, int> visit )
	{
		worldPos = worldPos.WithZ( 0f );
		var cellSize = GameConstants.CellSize;
		var minCellX = (int)MathF.Floor( (worldPos.x - radius) / cellSize );
		var maxCellX = (int)MathF.Floor( (worldPos.x + radius) / cellSize );
		var minCellY = (int)MathF.Floor( (worldPos.y - radius) / cellSize );
		var maxCellY = (int)MathF.Floor( (worldPos.y + radius) / cellSize );

		for ( var cellX = minCellX; cellX <= maxCellX; cellX++ )
		for ( var cellY = minCellY; cellY <= maxCellY; cellY++ )
			visit( cellX, cellY );
	}
}
