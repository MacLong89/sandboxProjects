namespace Terraingen.Buildings;

/// <summary>Ramp ascent deltas and upper-floor opening cells.</summary>
public static class ThornsProcTileRampHeadroom
{
	public static void GetRiseDelta( ThornsProcRampDirection ramp, out int riseDx, out int riseDy )
	{
		riseDx = 0;
		riseDy = 0;
		switch ( ramp )
		{
			case ThornsProcRampDirection.North: riseDy = 1; break;
			case ThornsProcRampDirection.South: riseDy = -1; break;
			case ThornsProcRampDirection.East: riseDx = 1; break;
			case ThornsProcRampDirection.West: riseDx = -1; break;
		}
	}

	public static bool TryGetUpperOpeningCells(
		ThornsProcRampSpec ramp,
		int widthCells,
		int depthCells,
		out int shaftX,
		out int shaftY,
		out int headX,
		out int headY )
	{
		shaftX = ramp.X;
		shaftY = ramp.Y;
		GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
		headX = ramp.X + riseDx;
		headY = ramp.Y + riseDy;
		return IsInsideGrid( shaftX, shaftY, widthCells, depthCells )
		       && IsInsideGrid( headX, headY, widthCells, depthCells );
	}

	public static void MarkFloorCuts(
		bool[,,] skipFloor,
		IReadOnlyList<ThornsProcRampSpec>[] rampsByStory,
		int stories,
		int widthCells,
		int depthCells )
	{
		if ( skipFloor is null || rampsByStory is null )
			return;

		for ( var rampStory = 0; rampStory < stories - 1 && rampStory < rampsByStory.Length; rampStory++ )
		{
			var ramps = rampsByStory[rampStory];
			if ( ramps is null )
				continue;

			for ( var i = 0; i < ramps.Count; i++ )
			{
				var ramp = ramps[i];
				// Match thorns ApplyRequiredOpenings — only the storey *above* the ramp gets shaft/head
				// openings. Do not cut the ramp anchor cell on rampStory (e.g. F1 NE (2,2) above kitchen_fridge).
				if ( !TryGetUpperOpeningCells( ramp, widthCells, depthCells, out var shaftX, out var shaftY, out var headX, out var headY ) )
					continue;

				var upper = rampStory + 1;
				if ( upper >= stories )
					continue;

				skipFloor[upper, shaftX, shaftY] = true;
				skipFloor[upper, headX, headY] = true;
			}
		}
	}

	static bool IsInsideGrid( int x, int y, int widthCells, int depthCells ) =>
		x >= 0 && x < widthCells && y >= 0 && y < depthCells;

	public static bool HasInteriorFloorTile( bool[,,] skipFloor, int story, int gridX, int gridY )
	{
		if ( skipFloor is null )
			return true;

		if ( story < 0 || story >= skipFloor.GetLength( 0 ) )
			return false;

		if ( gridX < 0 || gridX >= skipFloor.GetLength( 1 ) )
			return false;

		if ( gridY < 0 || gridY >= skipFloor.GetLength( 2 ) )
			return false;

		return !skipFloor[story, gridX, gridY];
	}
}
