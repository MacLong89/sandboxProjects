using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Ramp facing, shaft cells, and landings — all operations are per <see cref="ThornsProcRampSpec"/>
/// so multiple stairwells per storey are supported.
/// </summary>
public static class ThornsProcBuildingRampGeometry
{
	public static float YawFromRampDirection( ThornsProcRampDirection dir ) =>
		dir switch
		{
			ThornsProcRampDirection.North => 0f,
			ThornsProcRampDirection.South => 180f,
			ThornsProcRampDirection.East => 90f,
			ThornsProcRampDirection.West => 270f,
			_ => 90f
		};

	public static void GetRiseDirection( ThornsProcRampSpec ramp, out int riseDx, out int riseDy )
	{
		if ( ramp.Direction != ThornsProcRampDirection.None )
		{
			ThornsProcTileRampHeadroom.GetRiseDelta( ramp.Direction, out riseDx, out riseDy );
			return;
		}

		riseDx = 0;
		riseDy = 0;
	}

	/// <summary>
	/// Grid cell for <c>wood_ramp</c> mesh — same tile as the logical ramp anchor (ASCII <c>R</c> / blueprint ramp cell).
	/// Shaft and headroom openings still use anchor + ascent from <see cref="ThornsProcTileRampHeadroom"/>.
	/// </summary>
	public static void GetRampSpawnCell(
		ThornsProcBuildingLayout layout,
		ThornsProcRampSpec ramp,
		out int spawnX,
		out int spawnY )
	{
		spawnX = Math.Clamp( ramp.X, 0, layout.WidthCells - 1 );
		spawnY = Math.Clamp( ramp.Y, 0, layout.DepthCells - 1 );
	}

	public static Vector3 GetRampSpawnLocalPosition(
		ThornsProcBuildingLayout layout,
		ThornsProcRampSpec ramp,
		float localZ )
	{
		GetRampSpawnCell( layout, ramp, out var sx, out var sy );
		return new Vector3( layout.GridAxisLocalX( sx ), layout.GridAxisLocalY( sy ), localZ );
	}

	public static void GetRiseDirection(
		ThornsProcBuildingLayout layout,
		int story,
		out int riseDx,
		out int riseDy )
	{
		if ( layout.TryGetPrimaryRamp( story, out var ramp ) )
		{
			GetRiseDirection( ramp, out riseDx, out riseDy );
			if ( riseDx != 0 || riseDy != 0 )
				return;

			InferRiseFromOccupancy( layout, story, ramp.X, ramp.Y, out riseDx, out riseDy );
			return;
		}

		riseDx = 0;
		riseDy = 0;
	}

	public static void GetRiseDirection(
		ThornsProcBuildingLayout layout,
		ThornsProcRampSpec ramp,
		out int riseDx,
		out int riseDy )
	{
		GetRiseDirection( ramp, out riseDx, out riseDy );
		if ( riseDx != 0 || riseDy != 0 )
			return;

		InferRiseFromOccupancy( layout, ramp.Story, ramp.X, ramp.Y, out riseDx, out riseDy );
	}

	static void InferRiseFromOccupancy(
		ThornsProcBuildingLayout layout,
		int story,
		int rx,
		int ry,
		out int riseDx,
		out int riseDy )
	{
		var west = rx == 0 || !layout.IsCellOccupied( story, rx - 1, ry );
		var east = rx >= layout.WidthCells - 1 || !layout.IsCellOccupied( story, rx + 1, ry );
		var south = ry == 0 || !layout.IsCellOccupied( story, rx, ry - 1 );
		var north = ry >= layout.DepthCells - 1 || !layout.IsCellOccupied( story, rx, ry + 1 );

		riseDx = west && !east ? -1 : east && !west ? 1 : 0;
		riseDy = south && !north ? -1 : north && !south ? 1 : 0;

		if ( riseDx == 0 && riseDy == 0 )
		{
			riseDx = rx < layout.WidthCells / 2 ? -1 : 1;
			riseDy = ry < layout.DepthCells / 2 ? -1 : 1;
		}
	}

	public static float GetRampYawDegrees( ThornsProcRampSpec ramp )
	{
		if ( ramp.Direction != ThornsProcRampDirection.None )
			return YawFromRampDirection( ramp.Direction );

		return 90f;
	}

	public static float GetRampYawDegrees( ThornsProcBuildingLayout layout, int rampStory )
	{
		if ( !layout.TryGetPrimaryRamp( rampStory, out var ramp ) )
			return 90f;

		if ( ramp.Direction != ThornsProcRampDirection.None )
			return YawFromRampDirection( ramp.Direction );

		GetRiseDirection( layout, ramp, out var riseDx, out var riseDy );
		return (riseDx, riseDy) switch
		{
			(-1, -1) => 270f,
			(1, -1) => 180f,
			(-1, 1) => 0f,
			(1, 1) => 90f,
			(-1, 0) => 270f,
			(1, 0) => 90f,
			(0, -1) => 180f,
			(0, 1) => 0f,
			_ => 90f
		};
	}

	public static bool IsShaftCell( ThornsProcBuildingLayout layout, ThornsProcRampSpec ramp, int x, int y )
	{
		if ( ramp.Story < 0 || ramp.Story >= layout.Stories )
			return false;

		if ( x == ramp.X && y == ramp.Y )
			return true;

		GetRiseDirection( layout, ramp, out var riseDx, out var riseDy );

		if ( riseDx < 0 && x == ramp.X + 1 && y == ramp.Y )
			return true;
		if ( riseDx > 0 && x == ramp.X - 1 && y == ramp.Y )
			return true;
		if ( riseDy < 0 && x == ramp.X && y == ramp.Y + 1 )
			return true;
		if ( riseDy > 0 && x == ramp.X && y == ramp.Y - 1 )
			return true;

		return false;
	}

	public static bool IsShaftCellForAnyRampOnStory( ThornsProcBuildingLayout layout, int rampStory, int x, int y )
	{
		foreach ( var ramp in layout.GetRampsOnStory( rampStory ) )
		{
			if ( IsShaftCell( layout, ramp, x, y ) )
				return true;
		}

		return false;
	}

	/// <summary>Legacy single-ramp entry — checks all ramps on <paramref name="rampStory"/>.</summary>
	public static bool IsShaftCell( ThornsProcBuildingLayout layout, int rampStory, int x, int y ) =>
		IsShaftCellForAnyRampOnStory( layout, rampStory, x, y );

	public static void CollectShaftCells( ThornsProcBuildingLayout layout, ThornsProcRampSpec ramp, List<(int x, int y)> list )
	{
		list.Clear();
		if ( ramp.Story < 0 || ramp.Story >= layout.Stories )
			return;

		GetRiseDirection( layout, ramp, out var riseDx, out var riseDy );

		TryAdd( list, ramp.X, ramp.Y );
		if ( riseDx < 0 )
			TryAdd( list, ramp.X + 1, ramp.Y );
		else if ( riseDx > 0 )
			TryAdd( list, ramp.X - 1, ramp.Y );

		if ( riseDy < 0 )
			TryAdd( list, ramp.X, ramp.Y + 1 );
		else if ( riseDy > 0 )
			TryAdd( list, ramp.X, ramp.Y - 1 );
	}

	public static void CollectShaftCells( ThornsProcBuildingLayout layout, int rampStory, List<(int x, int y)> list )
	{
		list.Clear();
		var scratch = new List<(int x, int y)>( 4 );
		foreach ( var ramp in layout.GetRampsOnStory( rampStory ) )
		{
			CollectShaftCells( layout, ramp, scratch );
			foreach ( var cell in scratch )
				TryAdd( list, cell.x, cell.y );
		}
	}

	public static void CollectLandingCells( ThornsProcBuildingLayout layout, int story, List<(int x, int y)> list )
	{
		list.Clear();
		var seen = new HashSet<(int x, int y)>();

		foreach ( var ramp in layout.GetRampsOnStory( story ) )
			CollectLandingCellsForRamp( layout, story, ramp, seen, list );

		if ( list.Count > 0 )
			return;

		if ( story < layout.Stories - 1 )
		{
			var shaftScratch = new List<(int x, int y)>( 4 );
			CollectShaftCells( layout, story, shaftScratch );

			foreach ( var (sx, sy) in shaftScratch )
			{
				TryAddWalkable( layout, story, sx, sy, seen, list );
				TryAddWalkable( layout, story, sx - 1, sy, seen, list );
				TryAddWalkable( layout, story, sx + 1, sy, seen, list );
				TryAddWalkable( layout, story, sx, sy - 1, seen, list );
				TryAddWalkable( layout, story, sx, sy + 1, seen, list );
			}
		}
	}

	public static void CollectLandingCellsForRamp(
		ThornsProcBuildingLayout layout,
		int story,
		ThornsProcRampSpec ramp,
		HashSet<(int x, int y)> seen,
		List<(int x, int y)> list )
	{
		if ( story != ramp.Story )
			return;

		if ( story < layout.Stories - 1 )
		{
			var shaftScratch = new List<(int x, int y)>( 4 );
			CollectShaftCells( layout, ramp, shaftScratch );

			foreach ( var (sx, sy) in shaftScratch )
			{
				TryAddWalkable( layout, story, sx, sy, seen, list );
				TryAddWalkable( layout, story, sx - 1, sy, seen, list );
				TryAddWalkable( layout, story, sx + 1, sy, seen, list );
				TryAddWalkable( layout, story, sx, sy - 1, seen, list );
				TryAddWalkable( layout, story, sx, sy + 1, seen, list );
			}

			GetRiseDirection( layout, ramp, out var riseDx, out var riseDy );
			var ix = riseDx < 0 ? 1 : riseDx > 0 ? -1 : 0;
			var iy = riseDy < 0 ? 1 : riseDy > 0 ? -1 : 0;
			if ( ix != 0 )
				TryAddWalkable( layout, story, ramp.X + ix * 2, ramp.Y, seen, list );
			if ( iy != 0 )
				TryAddWalkable( layout, story, ramp.X, ramp.Y + iy * 2, seen, list );
			if ( ix != 0 && iy != 0 )
				TryAddWalkable( layout, story, ramp.X + ix, ramp.Y + iy, seen, list );

			return;
		}

		if ( story != layout.Stories - 1 || ramp.Story != story - 1 )
			return;

		var shaftTop = new List<(int x, int y)>( 4 );
		CollectShaftCells( layout, ramp, shaftTop );
		foreach ( var (sx, sy) in shaftTop )
		{
			TryAddWalkable( layout, story, sx - 1, sy, seen, list );
			TryAddWalkable( layout, story, sx + 1, sy, seen, list );
			TryAddWalkable( layout, story, sx, sy - 1, seen, list );
			TryAddWalkable( layout, story, sx, sy + 1, seen, list );
		}
	}

	static void TryAdd( List<(int x, int y)> list, int x, int y )
	{
		for ( var i = 0; i < list.Count; i++ )
		{
			if ( list[i].x == x && list[i].y == y )
				return;
		}

		list.Add( (x, y) );
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
