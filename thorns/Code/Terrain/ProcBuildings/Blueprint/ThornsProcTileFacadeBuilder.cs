namespace Sandbox;

/// <summary>Maps blueprint edge modifiers to <see cref="ThornsProcBuildingFacadePlan"/> spawn data.</summary>
public static class ThornsProcTileFacadeBuilder
{
	// Matches ThornsTerrainSystem perimeter sides: 0=south, 1=east, 2=north, 3=west.
	public static ThornsProcBuildingFacadePlan Build(
		ThornsProcBuildingLayout layout,
		ThornsProcTileBlueprint blueprint,
		Random rnd )
	{
		var facade = new ThornsProcBuildingFacadePlan
		{
			WindowChance = blueprint.WindowChance,
			PreferFrontWindows = blueprint.PreferFrontWindows
		};

		if ( blueprint.Layers is null )
			return facade;

		for ( var s = 0; s < layout.Stories && s < blueprint.Layers.Count; s++ )
		{
			var layer = blueprint.Layers[s];
			for ( var x = 0; x < layer.Width; x++ )
			for ( var y = 0; y < layer.Depth; y++ )
			{
				ref var c = ref layer.Cell( x, y );
				if ( !layout.IsCellOccupied( s, x, y ) && !c.Opening )
					continue;

				if ( c.DoorSouth )
					facade.AddDoor( s, 0, x, y );
				if ( c.DoorNorth )
					facade.AddDoor( s, 2, x, y );
				if ( c.DoorEast )
					facade.AddDoor( s, 1, x, y );
				if ( c.DoorWest )
					facade.AddDoor( s, 3, x, y );

				if ( c.WindowSouth )
					facade.AddForceWindow( s, 0, x, y );
				if ( c.WindowNorth )
					facade.AddForceWindow( s, 2, x, y );
				if ( c.WindowEast )
					facade.AddForceWindow( s, 1, x, y );
				if ( c.WindowWest )
					facade.AddForceWindow( s, 3, x, y );
			}
		}

		_ = rnd;
		return facade;
	}
}
