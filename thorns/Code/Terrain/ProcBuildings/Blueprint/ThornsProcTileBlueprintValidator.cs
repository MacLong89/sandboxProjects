namespace Sandbox;

/// <summary>Blueprint-specific validation (dual ramp headroom openings).</summary>
public static class ThornsProcTileBlueprintValidator
{
	public static bool ValidateRampOpenings(
		ThornsProcBuildingLayout layout,
		ThornsProcTileBlueprint blueprint,
		out string error )
	{
		error = null;
		if ( blueprint?.Layers is null || layout.Stories <= 1 )
			return true;

		for ( var s = 0; s < layout.Stories - 1; s++ )
		{
			var layer = blueprint.Layers[s];
			for ( var x = 0; x < layer.Width; x++ )
			for ( var y = 0; y < layer.Depth; y++ )
			{
				var ramp = layer.Cell( x, y ).Ramp;
				if ( ramp == ThornsProcRampDirection.None )
					continue;

				if ( !ThornsProcTileRampHeadroom.HasRequiredOpenings( layout, s, x, y, ramp ) )
				{
					ThornsProcTileRampHeadroom.GetRiseDelta( ramp, out var dx, out var dy );
					error =
						$"Ramp at ({x},{y}) story {s} needs openings on story {s + 1} at ({x},{y}) and ({x + dx},{y + dy}).";
					return false;
				}
			}
		}

		return true;
	}

	public static List<(int s, int x, int y)> FindRampOpeningMismatches(
		ThornsProcBuildingLayout layout,
		ThornsProcTileBlueprint blueprint )
	{
		var list = new List<(int s, int x, int y)>( 8 );
		if ( blueprint?.Layers is null || layout.Stories <= 1 )
			return list;

		for ( var s = 0; s < layout.Stories - 1; s++ )
		{
			var layer = blueprint.Layers[s];
			for ( var x = 0; x < layer.Width; x++ )
			for ( var y = 0; y < layer.Depth; y++ )
			{
				var ramp = layer.Cell( x, y ).Ramp;
				if ( ramp == ThornsProcRampDirection.None )
					continue;

				if ( !ThornsProcTileRampHeadroom.HasRequiredOpenings( layout, s, x, y, ramp ) )
					list.Add( (s, x, y) );
			}
		}

		return list;
	}
}
