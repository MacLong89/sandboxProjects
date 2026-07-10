namespace Terraingen.Buildings;

/// <summary>Per-storey ramp anchors for compact 3×3 proc towns (ported from thorns ASCII).</summary>
public static class ThornsProcBuildingRampPlanner
{
	public const int GridCells = 3;

	public static List<ThornsProcRampSpec>[] BuildCompact3x3SwitchbackRampsByStory( int stories )
	{
		stories = Math.Clamp( stories, 1, ThornsProcBuildingSpawnDefaults.MaxStories );
		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var i = 0; i < stories; i++ )
			byStory[i] = new List<ThornsProcRampSpec>( 1 );

		if ( stories <= 1 )
			return byStory;

		for ( var rampStory = 0; rampStory < stories - 1; rampStory++ )
		{
			var (x, y) = ResolveSwitchbackAnchor( rampStory );
			byStory[rampStory].Add( new ThornsProcRampSpec
			{
				Story = rampStory,
				X = x,
				Y = y,
				Direction = ThornsProcRampDirection.West
			} );
		}

		return byStory;
	}

	/// <summary>Map canonical 3×3 ramp anchors onto wider/deeper interior grids.</summary>
	public static void MapRampsToFootprint(
		List<ThornsProcRampSpec>[] rampsByStory,
		int widthCells,
		int depthCells )
	{
		if ( rampsByStory is null )
			return;

		for ( var story = 0; story < rampsByStory.Length; story++ )
		{
			var ramps = rampsByStory[story];
			if ( ramps is null )
				continue;

			for ( var i = 0; i < ramps.Count; i++ )
			{
				var ramp = ramps[i];
				var (mappedX, mappedY) = ThornsInteriorFurnitureCanonicalSlots.MapCanonicalCellToFootprint(
					ramp.X,
					ramp.Y,
					widthCells,
					depthCells );
				ramps[i] = ramp with { X = mappedX, Y = mappedY };
			}
		}
	}

	internal static (int X, int Y) ResolveSwitchbackAnchor( int rampStory )
	{
		if ( rampStory <= 0 )
			return (ThornsInteriorFurnitureCanonicalSlots.RampGridX, ThornsInteriorFurnitureCanonicalSlots.RampGridY);

		return rampStory % 2 == 1
			? (2, 2)
			: (ThornsInteriorFurnitureCanonicalSlots.RampGridX, ThornsInteriorFurnitureCanonicalSlots.RampGridY);
	}
}
