namespace Sandbox;

/// <summary>Interior walls from room-id boundaries between adjacent floor tiles.</summary>
public static class ThornsProcTileAutoWalls
{
	public static ThornsProcBuildingWallPlan Build(
		ThornsProcBuildingLayout layout,
		ThornsProcTileBlueprint blueprint )
	{
		var plan = ThornsProcBuildingWallPlan.Empty( layout.Stories, layout.WidthCells, layout.DepthCells );
		if ( blueprint?.Layers is null )
			return plan;

		for ( var s = 0; s < layout.Stories && s < blueprint.Layers.Count; s++ )
		{
			var layer = blueprint.Layers[s];
			for ( var x = 0; x < layout.WidthCells - 1 && x < layer.Width - 1; x++ )
			for ( var y = 0; y < layout.DepthCells && y < layer.Depth; y++ )
			{
				if ( !IsFloor( layout, s, x, y, layer ) || !IsFloor( layout, s, x + 1, y, layer ) )
					continue;

				if ( layer.Cell( x, y ).RoomId != layer.Cell( x + 1, y ).RoomId )
					plan.SetInteriorWallEast( s, x, y, true );
			}

			for ( var x = 0; x < layout.WidthCells && x < layer.Width; x++ )
			for ( var y = 0; y < layout.DepthCells - 1 && y < layer.Depth - 1; y++ )
			{
				if ( !IsFloor( layout, s, x, y, layer ) || !IsFloor( layout, s, x, y + 1, layer ) )
					continue;

				if ( layer.Cell( x, y ).RoomId != layer.Cell( x, y + 1 ).RoomId )
					plan.SetInteriorWallNorth( s, x, y, true );
			}
		}

		return plan;
	}

	static bool IsFloor( ThornsProcBuildingLayout layout, int s, int x, int y, ThornsProcTileLayer layer )
	{
		if ( x < 0 || x >= layer.Width || y < 0 || y >= layer.Depth )
			return false;

		if ( !layout.HasWalkableFloorAt( s, x, y ) )
			return false;

		return layer.Cell( x, y ).HasFloorLike;
	}
}
