namespace Sandbox;

/// <summary>Derived metrics from a layout + wall plan for validation and quality scoring.</summary>
public sealed class ThornsProcBuildingAnalysis
{
	public int TotalWalkableCells { get; init; }
	public int ReachableWalkableCells { get; init; }
	public int UnreachableWalkableCells => Math.Max( 0, TotalWalkableCells - ReachableWalkableCells );
	public int RoomRegionCount { get; init; }
	public int TinyRoomCount { get; init; }
	public int HugeRoomCount { get; init; }
	public int DeadEndCellCount { get; init; }
	public int MaxGroundSightlineCells { get; init; }
	public IReadOnlyList<int> RoomSizes { get; init; } = Array.Empty<int>();

	public static ThornsProcBuildingAnalysis Analyze(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		int doorSide,
		int doorIndex )
	{
		var total = CountWalkable( layout );
		var reachable = FloodReachableCount( layout, walls, doorSide, doorIndex );
		var roomSizes = CollectRoomSizes( layout, walls );
		var tiny = roomSizes.Count( s => s < ThornsProcBuildingRules.MinPreferredRoomCells );
		var huge = roomSizes.Count( s => s > ThornsProcBuildingRules.MaxPreferredRoomCells );

		return new ThornsProcBuildingAnalysis
		{
			TotalWalkableCells = total,
			ReachableWalkableCells = reachable,
			RoomRegionCount = roomSizes.Count,
			RoomSizes = roomSizes,
			TinyRoomCount = tiny,
			HugeRoomCount = huge,
			DeadEndCellCount = CountDeadEnds( layout, walls, doorSide, doorIndex ),
			MaxGroundSightlineCells = MaxStraightRunGround( layout, walls )
		};
	}

	static int CountWalkable( ThornsProcBuildingLayout layout )
	{
		var n = 0;
		for ( var s = 0; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( layout.HasWalkableFloorAt( s, x, y ) )
				n++;
		}

		return n;
	}

	static int FloodReachableCount( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls, int doorSide, int doorIndex )
	{
		if ( !ThornsProcBuildingInteriorSample.TryGetDoorInteriorCell(
			     doorSide, doorIndex, layout.WidthCells, layout.DepthCells, out var sx, out var sy ) )
			return 0;

		if ( !layout.HasWalkableFloorAt( 0, sx, sy ) )
			return 0;

		var stride = layout.WidthCells * layout.DepthCells;
		var visited = new bool[layout.Stories * stride];
		var q = new Queue<(int s, int x, int y)>();
		var count = 0;

		void Enqueue( int s, int x, int y )
		{
			var i = s * stride + y * layout.WidthCells + x;
			if ( visited[i] )
				return;

			visited[i] = true;
			q.Enqueue( (s, x, y) );
			count++;
		}

		Enqueue( 0, sx, sy );

		while ( q.Count > 0 )
		{
			var (s, x, y) = q.Dequeue();
			TryMove( s, x, y, x - 1, y );
			TryMove( s, x, y, x + 1, y );
			TryMove( s, x, y, x, y - 1 );
			TryMove( s, x, y, x, y + 1 );

			if ( s < layout.Stories - 1 )
			{
				foreach ( var ramp in layout.GetRampsOnStory( s ) )
				{
					if ( !ThornsProcBuildingRampTraversal.IsOnRampLanding( layout, s, x, y, ramp ) )
						continue;

					foreach ( var (tx, ty) in ThornsProcBuildingRampTraversal.EnumerateUpperEntryCellsForRamp( layout, ramp ) )
						Enqueue( s + 1, tx, ty );
				}
			}
		}

		return count;

		void TryMove( int s, int fx, int fy, int tx, int ty )
		{
			if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
				return;

			if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
				return;

			if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
				return;

			if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
				return;

			if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
				return;

			Enqueue( s, tx, ty );
		}
	}

	static List<int> CollectRoomSizes( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls )
	{
		var sizes = new List<int>( 12 );

		for ( var s = 0; s < layout.Stories; s++ )
		{
			var stride = layout.WidthCells * layout.DepthCells;
			var region = new int[stride];
			for ( var i = 0; i < stride; i++ )
				region[i] = -1;

			for ( var x = 0; x < layout.WidthCells; x++ )
			for ( var y = 0; y < layout.DepthCells; y++ )
			{
				if ( !layout.HasWalkableFloorAt( s, x, y ) )
					continue;

				var idx = y * layout.WidthCells + x;
				if ( region[idx] >= 0 )
					continue;

				sizes.Add( FloodRoomSize( layout, walls, s, x, y, region ) );
			}
		}

		return sizes;
	}

	static int FloodRoomSize( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls, int story, int startX, int startY, int[] region )
	{
		var w = layout.WidthCells;
		var q = new Queue<(int x, int y)>();
		var size = 0;

		void Enqueue( int x, int y )
		{
			var idx = y * w + x;
			if ( region[idx] >= 0 )
				return;

			region[idx] = 1;
			q.Enqueue( (x, y) );
			size++;
		}

		Enqueue( startX, startY );

		while ( q.Count > 0 )
		{
			var (x, y) = q.Dequeue();
			Try( x - 1, y, x, y );
			Try( x + 1, y, x, y );
			Try( x, y - 1, x, y );
			Try( x, y + 1, x, y );
		}

		return size;

		void Try( int tx, int ty, int fx, int fy )
		{
			if ( tx < 0 || tx >= w || ty < 0 || ty >= layout.DepthCells )
				return;

			if ( !layout.HasWalkableFloorAt( story, tx, ty ) )
				return;

			if ( tx == fx + 1 && walls.HasInteriorWallEast( story, fx, fy ) )
				return;

			if ( tx == fx - 1 && walls.HasInteriorWallEast( story, tx, ty ) )
				return;

			if ( ty == fy + 1 && walls.HasInteriorWallNorth( story, fx, fy ) )
				return;

			if ( ty == fy - 1 && walls.HasInteriorWallNorth( story, fx, ty ) )
				return;

			Enqueue( tx, ty );
		}
	}

	static int CountDeadEnds( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls, int doorSide, int doorIndex )
	{
		if ( !ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, walls, doorSide, doorIndex ) )
			return 0;

		var dead = 0;
		for ( var s = 0; s < layout.Stories; s++ )
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( !layout.HasWalkableFloorAt( s, x, y ) )
				continue;

			if ( CountDegree( layout, walls, s, x, y ) <= 1 )
				dead++;
		}

		return dead;
	}

	static int CountDegree( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls, int s, int x, int y )
	{
		var n = 0;
		if ( CanStep( layout, walls, s, x, y, x - 1, y ) ) n++;
		if ( CanStep( layout, walls, s, x, y, x + 1, y ) ) n++;
		if ( CanStep( layout, walls, s, x, y, x, y - 1 ) ) n++;
		if ( CanStep( layout, walls, s, x, y, x, y + 1 ) ) n++;
		return n;
	}

	static bool CanStep( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls, int s, int fx, int fy, int tx, int ty )
	{
		if ( tx < 0 || tx >= layout.WidthCells || ty < 0 || ty >= layout.DepthCells )
			return false;

		if ( !layout.HasWalkableFloorAt( s, tx, ty ) )
			return false;

		if ( tx == fx + 1 && walls.HasInteriorWallEast( s, fx, fy ) )
			return false;

		if ( tx == fx - 1 && walls.HasInteriorWallEast( s, tx, ty ) )
			return false;

		if ( ty == fy + 1 && walls.HasInteriorWallNorth( s, fx, fy ) )
			return false;

		if ( ty == fy - 1 && walls.HasInteriorWallNorth( s, fx, ty ) )
			return false;

		return true;
	}

	static int MaxStraightRunGround( ThornsProcBuildingLayout layout, ThornsProcBuildingWallPlan walls )
	{
		var best = 0;
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			var run = 0;
			for ( var x = 0; x < layout.WidthCells; x++ )
			{
				if ( layout.HasWalkableFloorAt( 0, x, y ) && !IsBlockedEast( walls, 0, x, y ) )
					run++;
				else
				{
					best = Math.Max( best, run );
					run = 0;
				}
			}

			best = Math.Max( best, run );
		}

		for ( var x = 0; x < layout.WidthCells; x++ )
		{
			var run = 0;
			for ( var y = 0; y < layout.DepthCells; y++ )
			{
				if ( layout.HasWalkableFloorAt( 0, x, y ) && !IsBlockedNorth( walls, 0, x, y ) )
					run++;
				else
				{
					best = Math.Max( best, run );
					run = 0;
				}
			}

			best = Math.Max( best, run );
		}

		return best;
	}

	static bool IsBlockedEast( ThornsProcBuildingWallPlan walls, int s, int x, int y ) =>
		walls.HasInteriorWallEast( s, x, y );

	static bool IsBlockedNorth( ThornsProcBuildingWallPlan walls, int s, int x, int y ) =>
		walls.HasInteriorWallNorth( s, x, y );
}
