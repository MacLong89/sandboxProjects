namespace Sandbox;

/// <summary>Flood-fill pathing on walkable floor cells, respecting interior wall edges and ramp landings.</summary>
public static class ThornsProcBuildingConnectivity
{
	public static bool IsFullyReachableFromDoor(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		int doorSide,
		int doorIndex )
	{
		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     doorSide,
			     doorIndex,
			     layout.WidthCells,
			     layout.DepthCells,
			     out var startX,
			     out var startY ) )
			return false;

		if ( !layout.HasWalkableFloorAt( 0, startX, startY ) )
			return false;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		var queue = new Queue<(int s, int x, int y)>();

		void Visit( int s, int x, int y )
		{
			var idx = s * stride + y * layout.WidthCells + x;
			if ( visited[idx] )
				return;

			visited[idx] = true;
			queue.Enqueue( (s, x, y) );
		}

		Visit( 0, startX, startY );

		while ( queue.Count > 0 )
		{
			var (s, x, y) = queue.Dequeue();

			TryMove( s, x, y, x - 1, y );
			TryMove( s, x, y, x + 1, y );
			TryMove( s, x, y, x, y - 1 );
			TryMove( s, x, y, x, y + 1 );

			if ( s >= layout.Stories - 1 )
				continue;

			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
					continue;

				foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
					Visit( s + 1, tx, ty );
			}
		}

		for ( var s = 0; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( s, x, y ) )
				continue;

			if ( !visited[s * stride + y * layout.WidthCells + x] )
				return false;
		}

		return true;

		void TryMove( int s, int fx, int fy, int tx, int ty )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return;

			if ( walls is not null )
			{
				if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
					return;

				if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
					return;

				if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
					return;

				if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
					return;
			}

			Visit( s, tx, ty );
		}
	}

	public static int FloodFillReachableCount(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		int doorSide,
		int doorIndex )
	{
		var total = 0;
		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];

		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     doorSide,
			     doorIndex,
			     layout.WidthCells,
			     layout.DepthCells,
			     out var startX,
			     out var startY ) )
			return 0;

		if ( !layout.HasWalkableFloorAt( 0, startX, startY ) )
			return 0;

		var queue = new Queue<(int s, int x, int y)>();
		visited[0 * stride + startY * layout.WidthCells + startX] = true;
		queue.Enqueue( (0, startX, startY) );
		var count = 1;

		while ( queue.Count > 0 )
		{
			var (s, x, y) = queue.Dequeue();
			count += ProcessNeighbor( s, x - 1, y, x, y, x, y );
			count += ProcessNeighbor( s, x + 1, y, x, y, x, y );
			count += ProcessNeighbor( s, x, y - 1, x, y, x, y );
			count += ProcessNeighbor( s, x, y + 1, x, y, x, y );

			if ( s >= layout.Stories - 1 )
				continue;

			foreach ( var ramp in layout.GetRampsOnStory( s ) )
			{
				if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
					continue;

				foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
					count += ProcessNeighbor( s + 1, tx, ty, tx, ty, tx, ty );
			}
		}

		for ( var s = 0; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( layout.HasWalkableFloorAt( s, x, y ) )
				total++;
		}

		_ = total;
		return count;

		int ProcessNeighbor( int s, int tx, int ty, int fx, int fy, int wx, int wy )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return 0;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return 0;

			if ( walls is not null )
			{
				if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
					return 0;

				if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
					return 0;

				if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
					return 0;

				if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
					return 0;
			}

			var idx = s * stride + ty * layout.WidthCells + tx;
			if ( visited[idx] )
				return 0;

			visited[idx] = true;
			queue.Enqueue( (s, tx, ty) );
			return 1;
		}
	}
}
