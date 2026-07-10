namespace Sandbox;

/// <summary>Soft-rule quality scoring (0–100). Used to pick the best of several generation attempts.</summary>
public static class ThornsProcBuildingQuality
{
	public const int ExcellentThreshold = 78;
	public const int GoodThreshold = 62;

	public static int Score(
		ThornsProcBuildingLayout layout,
		ThornsProcBuildingWallPlan walls,
		ThornsProcBuildingAnalysis analysis )
	{
		var score = 72;

		if ( analysis.UnreachableWalkableCells > 0 )
			return 0;

		// Room variety
		if ( analysis.RoomRegionCount >= 2 )
			score += 6;

		var distinctSizes = analysis.RoomSizes.Distinct().Count();
		if ( distinctSizes >= 2 )
			score += 4;

		score -= analysis.TinyRoomCount * 4;
		score -= analysis.HugeRoomCount * 5;

		// Navigation / PvP
		score -= Math.Min( 18, analysis.DeadEndCellCount * 2 );
		if ( analysis.MaxGroundSightlineCells > 6 )
			score -= ( analysis.MaxGroundSightlineCells - 6 ) * 2;

		// Multi-floor buildings should use vertical space
		if ( layout.Stories >= 2 )
		{
			var upperWalkable = 0;
			for ( var s = 1; s < layout.Stories; s++ )
			for ( var x = 0; x < layout.WidthCells; x++ )
			for ( var y = 0; y < layout.DepthCells; y++ )
			{
				if ( layout.HasWalkableFloorAt( s, x, y ) )
					upperWalkable++;
			}

			if ( upperWalkable >= 3 )
				score += 5;
			else
				score -= 6;
		}

		// Footprint efficiency — not too sparse
		var ground = 0;
		for ( var x = 0; x < layout.WidthCells; x++ )
		for ( var y = 0; y < layout.DepthCells; y++ )
		{
			if ( layout.HasWalkableFloorAt( 0, x, y ) )
				ground++;
		}

		var gridArea = layout.WidthCells * layout.DepthCells;
		var fill = ground / (float)Math.Max( 1, gridArea );
		if ( fill is >= 0.35f and <= 0.82f )
			score += 4;
		else if ( fill < 0.25f )
			score -= 8;

		// Interior walls add believable rooming without breaking paths
		var wallCount = CountInteriorWalls( walls );
		if ( wallCount > 0 && wallCount <= layout.Stories * 6 )
			score += 3;
		else if ( wallCount > layout.Stories * 10 )
			score -= 4;

		_ = walls;
		return Math.Clamp( score, 0, 100 );
	}

	static int CountInteriorWalls( ThornsProcBuildingWallPlan walls )
	{
		var n = 0;
		for ( var s = 0; s < walls.Stories; s++ )
		for ( var x = 0; x < walls.WidthCells; x++ )
		for ( var y = 0; y < walls.DepthCells; y++ )
		{
			if ( walls.HasInteriorWallEast( s, x, y ) )
				n++;

			if ( walls.HasInteriorWallNorth( s, x, y ) )
				n++;
		}

		return n;
	}
}
