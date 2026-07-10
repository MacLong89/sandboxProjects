namespace Fauna2;

/// <summary>Always-on placement logging to trace build crashes.</summary>
public static class BuildDiagnostics
{
	public static void Write( string phase, PlaceableDefinition def, Vector3 pos, float yaw, string detail = null )
	{
		if ( def is null )
		{
			Log.Info( $"[Fauna2 Build] {phase} (no definition) pos={pos}" );
			return;
		}

		var id = Defs.IdOf( def );
		var footprint = BuildValidation.RotatedFootprint( def.EffectiveFootprint, yaw );
		var overlap = !def.IsPathTile && !def.IsHabitat
			&& PlaceableRegistry.FootprintOverlapsAny( pos, def.EffectiveFootprint, yaw );

		Log.Info(
			$"[Fauna2 Build] {phase} id={id} pos={pos} yaw={yaw:0} footprint={footprint.x:0}x{footprint.y:0} " +
			$"registry={PlaceableRegistry.Count} overlap={overlap} " +
			$"restaurants={PlaceableRegistry.RestaurantCount} restrooms={PlaceableRegistry.RestroomCount} " +
			$"paths={PlaceableRegistry.PathList.Count}" +
			(string.IsNullOrEmpty( detail ) ? "" : $" {detail}") );
	}
}
