namespace FinalOutpost;

/// <summary>
/// Tracks which grid cells are occupied by static structures (command post, buildings, walls).
/// Occupancy matches visuals 1:1 — one placement cell per building / wall segment, 2×2 for the core.
/// </summary>
public static class TileOccupancy
{
	static readonly Dictionary<(int X, int Y), int> BuildingCells = new();
	/// <summary>Cells blocked for pathing (same cells as wall mounts).</summary>
	static readonly Dictionary<(int X, int Y), int> WallPathCells = new();
	/// <summary>One cell per perimeter segment — mount / detection.</summary>
	static readonly Dictionary<(int X, int Y), int> WallMountCells = new();

	public static bool IsCellBlocked(
		int cellX,
		int cellY,
		bool ignoreWalls = false,
		int? passThroughCellX = null,
		int? passThroughCellY = null,
		bool ignoreCore = false,
		bool ignoreBuildings = false )
	{
		if ( passThroughCellX == cellX && passThroughCellY == cellY )
			return false;

		if ( !ignoreCore && BuildGrid.IsCoreCell( cellX, cellY ) )
			return true;

		if ( !ignoreBuildings && BuildingCells.ContainsKey( (cellX, cellY) ) )
			return true;

		return !ignoreWalls && WallPathCells.ContainsKey( (cellX, cellY) );
	}

	public static bool IsWallCell( int cellX, int cellY ) =>
		WallMountCells.ContainsKey( (cellX, cellY) );

	public static bool IsWallPathCell( int cellX, int cellY ) =>
		WallPathCells.ContainsKey( (cellX, cellY) );

	public static float WallMountHeight( int cellX, int cellY ) =>
		IsWallCell( cellX, cellY )
			? GameConstants.WallHeight + GameConstants.WallMountDeckClearance
			: 0f;

	public static Vector3 WallMountWorldPosition( int cellX, int cellY )
	{
		var cellCenter = BuildGrid.CellToWorld( cellX, cellY );
		if ( !IsWallCell( cellX, cellY ) )
			return cellCenter;

		var wall = FindMountWall( cellX, cellY, cellCenter );
		if ( wall is not null )
		{
			var topZ = wall.Center.z + GameConstants.WallHeight * 0.5f + GameConstants.WallMountDeckClearance;
			return new Vector3( wall.Center.x, wall.Center.y, topZ );
		}

		var side = WallApproach.FromWorldPosition( cellCenter, Vector3.Zero );
		var snapped = cellCenter;
		switch ( side )
		{
			case WallApproachSide.North: snapped.y = GameConstants.ArenaHalf; break;
			case WallApproachSide.South: snapped.y = -GameConstants.ArenaHalf; break;
			case WallApproachSide.East: snapped.x = GameConstants.ArenaHalf; break;
			default: snapped.x = -GameConstants.ArenaHalf; break;
		}

		snapped.z = OutpostTerrain.SampleHeight( snapped.x, snapped.y )
			+ GameConstants.WallHeight
			+ GameConstants.WallMountDeckClearance;
		return snapped;
	}

	public static WallSegment FindMountWall( int cellX, int cellY, Vector3 near )
	{
		var outpost = OutpostManager.Instance;
		if ( outpost is null ) return null;

		WallSegment best = null;
		var bestDist = float.MaxValue;
		near = near.WithZ( 0f );

		foreach ( var wall in outpost.Walls )
		{
			if ( wall is null || wall.IsBroken ) continue;
			if ( !BuildGrid.WorldToCell( wall.Center, out var wx, out var wy ) )
				continue;

			var dist = (wall.Center.WithZ( 0f ) - near).LengthSquared;
			if ( wx == cellX && wy == cellY )
				dist *= 0.01f;

			if ( dist >= bestDist ) continue;
			bestDist = dist;
			best = wall;
		}

		return best;
	}

	public static bool IsBuildingCell( int cellX, int cellY ) =>
		BuildGrid.IsCoreCell( cellX, cellY ) || BuildingCells.ContainsKey( (cellX, cellY) );

	public static bool IsWorldBlocked(
		Vector3 worldPos,
		float agentRadius,
		bool ignoreWalls = false,
		int? passThroughCellX = null,
		int? passThroughCellY = null,
		bool ignoreCore = false,
		bool ignoreBuildings = false )
	{
		worldPos = worldPos.WithZ( 0f );
		var blocked = false;

		BuildGrid.ForEachCellInRadius( worldPos, agentRadius, ( cellX, cellY ) =>
		{
			if ( IsCellBlocked( cellX, cellY, ignoreWalls, passThroughCellX, passThroughCellY, ignoreCore, ignoreBuildings ) )
				blocked = true;
		} );

		return blocked;
	}

	public static void MarkBuilding( PlacedBuilding building )
	{
		if ( building is null )
			return;

		AddCell( BuildingCells, building.CellX, building.CellY );
	}

	public static void UnmarkBuilding( PlacedBuilding building )
	{
		if ( building is null )
			return;

		RemoveCell( BuildingCells, building.CellX, building.CellY );
	}

	public static void RefreshBuilding( PlacedBuilding building )
	{
		UnmarkBuilding( building );
		MarkBuilding( building );
	}

	public static void MarkWall( WallSegment wall )
	{
		if ( wall is null || wall.IsBroken || wall.FootprintSize.Length <= 0f )
			return;

		// Occupancy strip sits just inside ArenaHalf so we don't block the empty outer cell.
		var pathCenter = WallPathOccupancyCenter( wall );
		BuildGrid.ForEachCellInFootprint( pathCenter, wall.PathFootprintSize, ( cx, cy ) =>
		{
			AddCell( WallPathCells, cx, cy );
		} );

		if ( BuildGrid.WorldToCell( pathCenter, out var mx, out var my ) )
		{
			AddCell( WallPathCells, mx, my );
			AddCell( WallMountCells, mx, my );
		}
	}

	public static void UnmarkWall( WallSegment wall )
	{
		if ( wall is null || wall.FootprintSize.Length <= 0f )
			return;

		var pathCenter = WallPathOccupancyCenter( wall );
		BuildGrid.ForEachCellInFootprint( pathCenter, wall.PathFootprintSize, ( cx, cy ) =>
		{
			RemoveCell( WallPathCells, cx, cy );
		} );

		if ( BuildGrid.WorldToCell( pathCenter, out var mx, out var my ) )
		{
			RemoveCell( WallPathCells, mx, my );
			RemoveCell( WallMountCells, mx, my );
		}
	}

	/// <summary>
	/// Center the occupancy strip just inside the ring so it rasterizes to the timber cell,
	/// not the outer cell of open ground beyond the bars.
	/// </summary>
	static Vector3 WallPathOccupancyCenter( WallSegment wall )
	{
		var c = wall.Center.WithZ( 0f );
		var inward = GameConstants.WallPathDepth * 0.5f;
		var side = WallApproach.FromWorldPosition( c, Vector3.Zero );
		return side switch
		{
			WallApproachSide.North => new Vector3( c.x, GameConstants.ArenaHalf - inward, 0f ),
			WallApproachSide.South => new Vector3( c.x, -GameConstants.ArenaHalf + inward, 0f ),
			WallApproachSide.East => new Vector3( GameConstants.ArenaHalf - inward, c.y, 0f ),
			_ => new Vector3( -GameConstants.ArenaHalf + inward, c.y, 0f )
		};
	}

	public static void ClearBuildings() => BuildingCells.Clear();

	public static void ClearWalls()
	{
		WallPathCells.Clear();
		WallMountCells.Clear();
	}

	public static void RebuildAll()
	{
		ClearBuildings();
		ClearWalls();

		var build = BuildManager.Instance;
		if ( build is not null )
		{
			foreach ( var building in build.Buildings )
				MarkBuilding( building );
		}

		var outpost = OutpostManager.Instance;
		if ( outpost is not null )
		{
			foreach ( var wall in outpost.Walls )
				MarkWall( wall );
		}
	}

	static void AddCell( Dictionary<(int X, int Y), int> map, int cellX, int cellY )
	{
		var key = (cellX, cellY);
		map[key] = map.GetValueOrDefault( key ) + 1;
	}

	static void RemoveCell( Dictionary<(int X, int Y), int> map, int cellX, int cellY )
	{
		var key = (cellX, cellY);
		if ( !map.TryGetValue( key, out var count ) )
			return;

		if ( count <= 1 )
			map.Remove( key );
		else
			map[key] = count - 1;
	}
}
