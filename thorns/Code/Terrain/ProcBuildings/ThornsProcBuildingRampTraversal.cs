using System.Collections.Generic;

namespace Sandbox;

/// <summary>Per-ramp landing zones and upper-floor entry cells (multi-stairwell safe).</summary>
public static class ThornsProcBuildingRampTraversal
{
	public static IEnumerable<(int x, int y)> EnumerateRampLandingCells( ThornsProcBuildingLayout layout, int story )
	{
		foreach ( var cell in CollectRampLandingCells( layout, story ) )
			yield return cell;
	}

	public static IEnumerable<(int x, int y)> EnumerateUpperEntryCellsFromRamp( ThornsProcBuildingLayout layout, int rampStory )
	{
		foreach ( var ramp in layout.GetRampsOnStory( rampStory ) )
		foreach ( var cell in EnumerateUpperEntryCellsForRamp( layout, ramp ) )
			yield return cell;
	}

	public static IEnumerable<(int x, int y)> EnumerateUpperEntryCellsForRamp(
		ThornsProcBuildingLayout layout,
		ThornsProcRampSpec ramp )
	{
		foreach ( var cell in CollectUpperEntryCellsForRamp( layout, ramp ) )
			yield return cell;
	}

	public static bool IsOnRampLanding( ThornsProcBuildingLayout layout, int story, int x, int y, ThornsProcRampSpec ramp )
	{
		foreach ( var (lx, ly) in CollectLandingCellsForRamp( layout, story, ramp ) )
		{
			if ( lx == x && ly == y )
				return true;
		}

		return false;
	}

	public static bool HasWalkableLandingExit( ThornsProcBuildingLayout layout, int story, ThornsProcRampSpec ramp )
	{
		var landing = CollectLandingCellsForRamp( layout, story, ramp );
		if ( landing.Count == 0 )
			return false;

		foreach ( var (lx, ly) in landing )
		{
			if ( CountWalkableNeighborsOnStory( layout, story, lx, ly ) > 1 )
				return true;
		}

		return landing.Count >= 1 && story == layout.Stories - 1;
	}

	static int CountWalkableNeighborsOnStory( ThornsProcBuildingLayout layout, int story, int x, int y )
	{
		var n = 0;
		if ( layout.HasWalkableFloorAt( story, x - 1, y ) ) n++;
		if ( layout.HasWalkableFloorAt( story, x + 1, y ) ) n++;
		if ( layout.HasWalkableFloorAt( story, x, y - 1 ) ) n++;
		if ( layout.HasWalkableFloorAt( story, x, y + 1 ) ) n++;
		return n;
	}

	static List<(int x, int y)> CollectRampLandingCells( ThornsProcBuildingLayout layout, int story )
	{
		var list = new List<(int x, int y)>( 16 );
		var seen = new HashSet<(int x, int y)>();

		foreach ( var ramp in layout.GetRampsOnStory( story ) )
			AddLandingForRamp( layout, story, ramp, seen, list );

		if ( list.Count == 0 )
			ThornsProcBuildingRampGeometry.CollectLandingCells( layout, story, list );

		return list;
	}

	static List<(int x, int y)> CollectLandingCellsForRamp(
		ThornsProcBuildingLayout layout,
		int story,
		ThornsProcRampSpec ramp )
	{
		var list = new List<(int x, int y)>( 8 );
		var seen = new HashSet<(int x, int y)>();
		AddLandingForRamp( layout, story, ramp, seen, list );
		return list;
	}

	static void AddLandingForRamp(
		ThornsProcBuildingLayout layout,
		int story,
		ThornsProcRampSpec ramp,
		HashSet<(int x, int y)> seen,
		List<(int x, int y)> list )
	{
		TryAddWalkable( layout, story, ramp.X, ramp.Y, seen, list );
		ThornsProcTileRampHeadroom.GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
		if ( ramp.Direction == ThornsProcRampDirection.None )
			ThornsProcBuildingRampGeometry.GetRiseDirection( layout, ramp, out riseDx, out riseDy );

		var ix = riseDx < 0 ? 1 : riseDx > 0 ? -1 : 0;
		var iy = riseDy < 0 ? 1 : riseDy > 0 ? -1 : 0;

		if ( ix != 0 )
			TryAddWalkable( layout, story, ramp.X + ix, ramp.Y, seen, list );
		if ( iy != 0 )
			TryAddWalkable( layout, story, ramp.X, ramp.Y + iy, seen, list );
		if ( ix != 0 && iy != 0 )
			TryAddWalkable( layout, story, ramp.X + ix, ramp.Y + iy, seen, list );

		TryAddWalkable( layout, story, ramp.X - riseDx, ramp.Y - riseDy, seen, list );
		TryAddWalkable( layout, story, ramp.X - riseDx + ix, ramp.Y - riseDy + iy, seen, list );
	}

	static List<(int x, int y)> CollectUpperEntryCellsForRamp( ThornsProcBuildingLayout layout, ThornsProcRampSpec ramp )
	{
		var list = new List<(int x, int y)>( 8 );
		var upper = ramp.Story + 1;
		if ( ramp.Story < 0 || upper >= layout.Stories )
			return list;

		var seen = new HashSet<(int x, int y)>();
		var headroom = new List<(int x, int y)>( 4 );
		ThornsProcTileRampHeadroom.CollectHeadroomCells( ramp.Story, ramp.X, ramp.Y, ramp.Direction, headroom );

		if ( ramp.Direction == ThornsProcRampDirection.None )
		{
			ThornsProcBuildingRampGeometry.GetRiseDirection( layout, ramp, out var riseDx, out var riseDy );
			var dir = riseDx switch
			{
				< 0 => ThornsProcRampDirection.West,
				> 0 => ThornsProcRampDirection.East,
				_ => riseDy switch
				{
					< 0 => ThornsProcRampDirection.South,
					> 0 => ThornsProcRampDirection.North,
					_ => ThornsProcRampDirection.None
				}
			};
			if ( dir != ThornsProcRampDirection.None )
				ThornsProcTileRampHeadroom.CollectHeadroomCells( ramp.Story, ramp.X, ramp.Y, dir, headroom );
		}

		foreach ( var (sx, sy) in headroom )
		{
			TryAddWalkable( layout, upper, sx - 1, sy, seen, list );
			TryAddWalkable( layout, upper, sx + 1, sy, seen, list );
			TryAddWalkable( layout, upper, sx, sy - 1, seen, list );
			TryAddWalkable( layout, upper, sx, sy + 1, seen, list );
		}

		return list;
	}

	static void TryAddWalkable(
		ThornsProcBuildingLayout layout,
		int story,
		int nx,
		int ny,
		HashSet<(int x, int y)> seen,
		List<(int x, int y)> list )
	{
		if ( nx < 0 || nx >= layout.WidthCells || ny < 0 || ny >= layout.DepthCells )
			return;

		if ( !layout.HasWalkableFloorAt( story, nx, ny ) )
			return;

		if ( !seen.Add( (nx, ny) ) )
			return;

		list.Add( (nx, ny) );
	}
}
