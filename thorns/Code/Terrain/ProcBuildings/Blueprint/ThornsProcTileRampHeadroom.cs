namespace Sandbox;

/// <summary>
/// Each ramp gets exactly two ceiling openings on the storey above:
/// same grid cell as the ramp, plus one cell in the ramp ascent direction.
/// </summary>
public static class ThornsProcTileRampHeadroom
{
	public static void ApplyRequiredOpenings( bool[] opening, int w, int d, IReadOnlyList<ThornsProcRampSpec> ramps )
	{
		if ( opening is null || ramps is null )
			return;

		foreach ( var ramp in ramps )
		{
			if ( ramp.Story + 1 >= GetStories( opening, w, d ) )
				continue;

			GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
			var upper = ramp.Story + 1;
			var shaftX = ramp.X;
			var shaftY = ramp.Y;
			var headX = ramp.X + riseDx;
			var headY = ramp.Y + riseDy;

			ClearOpeningNeighborhood( opening, w, d, upper, shaftX, shaftY, shaftX, shaftY, headX, headY );
			ClearOpeningNeighborhood( opening, w, d, upper, headX, headY, shaftX, shaftY, headX, headY );

			SetOpening( opening, w, d, upper, shaftX, shaftY, true );
			SetOpening( opening, w, d, upper, headX, headY, true );
		}
	}

	/// <summary>Legacy: derive ramps from blueprint layers before compile list exists.</summary>
	public static void ApplyRequiredOpenings( ThornsProcTileBlueprint blueprint, bool[] opening, int w, int d )
	{
		if ( blueprint?.Layers is null )
			return;

		var ramps = new List<ThornsProcRampSpec>( 4 );
		for ( var s = 0; s < blueprint.Layers.Count - 1; s++ )
		{
			var layer = blueprint.Layers[s];
			for ( var x = 0; x < w; x++ )
			for ( var y = 0; y < d; y++ )
			{
				var ramp = layer.Cell( x, y ).Ramp;
				if ( ramp == ThornsProcRampDirection.None )
					continue;

				ramps.Add( new ThornsProcRampSpec { Story = s, X = x, Y = y, Direction = ramp } );
			}
		}

		ApplyRequiredOpenings( opening, w, d, ramps );
	}

	public static bool HasRequiredOpenings(
		ThornsProcBuildingLayout layout,
		int rampStory,
		int x,
		int y,
		ThornsProcRampDirection ramp )
	{
		if ( ramp == ThornsProcRampDirection.None || rampStory + 1 >= layout.Stories )
			return true;

		GetRiseDelta( ramp, out var riseDx, out var riseDy );
		var upper = rampStory + 1;

		return layout.IsOpeningCell( upper, x, y )
		       && layout.IsOpeningCell( upper, x + riseDx, y + riseDy );
	}

	public static void CollectHeadroomCells(
		int rampStory,
		int x,
		int y,
		ThornsProcRampDirection ramp,
		List<(int x, int y)> upperCells )
	{
		if ( ramp == ThornsProcRampDirection.None )
			return;

		GetRiseDelta( ramp, out var riseDx, out var riseDy );
		TryAdd( upperCells, x, y );
		TryAdd( upperCells, x + riseDx, y + riseDy );
	}

	/// <summary>True when this upper-floor cell is a ramp shaft or ascent headroom opening.</summary>
	public static bool IsRampHeadroomCell(
		ThornsProcBuildingLayout layout,
		int upperStory,
		int x,
		int y )
	{
		if ( upperStory < 1 || !layout.IsOpeningCell( upperStory, x, y ) )
			return false;

		var rampStory = upperStory - 1;
		foreach ( var ramp in layout.GetRampsOnStory( rampStory ) )
		{
			GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
			if ( x == ramp.X && y == ramp.Y )
				return true;
			if ( x == ramp.X + riseDx && y == ramp.Y + riseDy )
				return true;
		}

		return false;
	}

	static void ClearOpeningNeighborhood(
		bool[] opening,
		int w,
		int d,
		int story,
		int shaftX,
		int shaftY,
		int keepAX,
		int keepAY,
		int keepBX,
		int keepBY )
	{
		for ( var ox = -1; ox <= 1; ox++ )
		for ( var oy = -1; oy <= 1; oy++ )
		{
			if ( ox == 0 && oy == 0 )
				continue;

			var cx = shaftX + ox;
			var cy = shaftY + oy;
			if ( cx == keepAX && cy == keepAY )
				continue;
			if ( cx == keepBX && cy == keepBY )
				continue;

			SetOpening( opening, w, d, story, cx, cy, false );
		}
	}

	static int GetStories( bool[] opening, int w, int d )
	{
		var stride = w * d;
		return stride > 0 ? opening.Length / stride : 0;
	}

	static void SetOpening( bool[] opening, int w, int d, int story, int x, int y, bool value )
	{
		if ( x < 0 || x >= w || y < 0 || y >= d )
			return;

		opening[story * w * d + y * w + x] = value;
	}

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

	static void TryAdd( List<(int x, int y)> list, int x, int y )
	{
		for ( var i = 0; i < list.Count; i++ )
		{
			if ( list[i].x == x && list[i].y == y )
				return;
		}

		list.Add( (x, y) );
	}
}
