namespace Sandbox;

/// <summary>
/// Deterministic interior divider edges between adjacent walkable cells (east / north).
/// Perimeter walls, doorframes, windows, and ramps are handled separately at spawn time.
/// </summary>
public sealed class ThornsProcBuildingWallPlan
{
	readonly bool[] _wallEast;
	readonly bool[] _wallNorth;

	public int Stories { get; }
	public int WidthCells { get; }
	public int DepthCells { get; }

	ThornsProcBuildingWallPlan( int stories, int widthCells, int depthCells, bool[] wallEast, bool[] wallNorth )
	{
		Stories = stories;
		WidthCells = widthCells;
		DepthCells = depthCells;
		_wallEast = wallEast;
		_wallNorth = wallNorth;
	}

	public static ThornsProcBuildingWallPlan Empty( int stories, int widthCells, int depthCells ) =>
		new( stories, widthCells, depthCells,
			new bool[stories * Math.Max( 0, widthCells - 1 ) * depthCells],
			new bool[stories * widthCells * Math.Max( 0, depthCells - 1 )] );

	public bool HasInteriorWallEast( int story, int x, int y )
	{
		if ( x < 0 || x >= WidthCells - 1 || y < 0 || y >= DepthCells || story < 0 || story >= Stories )
			return false;

		return _wallEast[IndexEast( story, x, y )];
	}

	public bool HasInteriorWallNorth( int story, int x, int y )
	{
		if ( x < 0 || x >= WidthCells || y < 0 || y >= DepthCells - 1 || story < 0 || story >= Stories )
			return false;

		return _wallNorth[IndexNorth( story, x, y )];
	}

	public void SetInteriorWallEast( int story, int x, int y, bool value )
	{
		if ( x < 0 || x >= WidthCells - 1 || y < 0 || y >= DepthCells || story < 0 || story >= Stories )
			return;

		_wallEast[IndexEast( story, x, y )] = value;
	}

	public void SetInteriorWallNorth( int story, int x, int y, bool value )
	{
		if ( x < 0 || x >= WidthCells || y < 0 || y >= DepthCells - 1 || story < 0 || story >= Stories )
			return;

		_wallNorth[IndexNorth( story, x, y )] = value;
	}

	int IndexEast( int story, int x, int y ) =>
		story * ( WidthCells - 1 ) * DepthCells + y * ( WidthCells - 1 ) + x;

	int IndexNorth( int story, int x, int y ) =>
		story * WidthCells * ( DepthCells - 1 ) + y * WidthCells + x;

	/// <summary>
	/// Seed-driven interior dividers. Each candidate edge is rejected if it breaks door reachability.
	/// </summary>
	public static ThornsProcBuildingWallPlan Generate(
		ThornsProcBuildingLayout layout,
		Random rnd,
		int doorSide,
		int doorIndex )
	{
		var plan = Empty( layout.Stories, layout.WidthCells, layout.DepthCells );
		if ( layout.Stories <= 0 || layout.WidthCells < 2 && layout.DepthCells < 2 )
			return plan;

		var candidates = new List<(int story, bool east, int x, int y)>( 64 );
		for ( var s = 0; s < layout.Stories; s++ )
		{
			for ( var x = 0; x < layout.WidthCells; x++ )
			for ( var y = 0; y < layout.DepthCells; y++ )
			{
				if ( !layout.HasWalkableFloorAt( s, x, y ) )
					continue;

				if ( x + 1 < layout.WidthCells && layout.HasWalkableFloorAt( s, x + 1, y ) )
					candidates.Add( (s, true, x, y) );

				if ( y + 1 < layout.DepthCells && layout.HasWalkableFloorAt( s, x, y + 1 ) )
					candidates.Add( (s, false, x, y) );
			}
		}

		Shuffle( candidates, rnd );

		var wallsPlaced = new int[layout.Stories];
		foreach ( var c in candidates )
		{
			if ( wallsPlaced[c.story] >= ThornsProcBuildingRules.MaxInteriorWallsPerStory )
				continue;

			if ( rnd.NextDouble() > ThornsProcBuildingRules.InteriorWallCandidateRate )
				continue;

			if ( c.east )
				plan.SetInteriorWallEast( c.story, c.x, c.y, true );
			else
				plan.SetInteriorWallNorth( c.story, c.x, c.y, true );

			if ( !ThornsProcBuildingConnectivity.IsFullyReachableFromDoor( layout, plan, doorSide, doorIndex ) )
			{
				if ( c.east )
					plan.SetInteriorWallEast( c.story, c.x, c.y, false );
				else
					plan.SetInteriorWallNorth( c.story, c.x, c.y, false );
				continue;
			}

			wallsPlaced[c.story]++;
		}

		return plan;
	}

	static void Shuffle<T>( List<T> list, Random rnd )
	{
		for ( var i = list.Count - 1; i > 0; i-- )
		{
			var j = rnd.Next( i + 1 );
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
