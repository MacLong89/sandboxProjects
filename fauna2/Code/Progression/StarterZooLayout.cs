namespace Fauna2;

/// <summary>
/// A compact, no-cost opening layout so a new player reaches the animal fantasy
/// immediately instead of spending the first several minutes on infrastructure.
/// The starter profile's setup fee already pays for these pieces.
/// </summary>
public static class StarterZooLayout
{
	/// <summary>
	/// Validated south-edge starter worksite (plot covers ±3072).
	/// Entrance at y=-2880, path strip north of it, habitat centered on the path axis.
	/// </summary>
	private static readonly Vector3 EntranceAnchor = new( 0f, -2880f, 0f );
	private static readonly Vector3 HabitatAnchor = new( 0f, -2048f, 0f );
	private static readonly float[] PathYs = [-2656f, -2592f, -2528f, -2464f, -2400f];

	/// <summary>Obstacle generation leaves this lower-center worksite open.</summary>
	public static bool IsReservedWorksite( Vector3 position )
	{
		var half = GameConstants.PlotSize * 0.5f;
		return position.x >= -512f
			&& position.x <= 512f
			&& position.y >= -half
			&& position.y <= -1408f;
	}

	public static bool Build( Biome biome )
	{
		if ( !Networking.IsHost || !BuildSystem.Instance.IsValid() || !PlotSystem.Instance.IsValid() )
			return false;

		var entranceDef = Defs.Placeable( "entrance" );
		var pathDef = Defs.Placeable( "path_straight" );
		var habitatDef = StarterGoalGuide.RecommendedHabitat( biome );
		if ( entranceDef is null || pathDef is null || habitatDef is null )
		{
			Log.Warning( "[Fauna2 Starter] Could not create starter layout — required definitions are missing." );
			return false;
		}

		var entrancePosition = BuildSnap.ResolvePlacement( EntranceAnchor, entranceDef, 0f, PlotSystem.Instance );
		if ( !BuildValidation.IsNearOwnedPlotEdge( entrancePosition ) )
		{
			Log.Warning( $"[Fauna2 Starter] Entrance anchor invalid at {entrancePosition}." );
			return false;
		}

		var entrance = BuildSystem.Instance.SpawnPlaceable( entranceDef, entrancePosition, 0f );
		if ( entrance is null )
			return false;

		// 1×1 paths snap to tile centers (x=32), north of the south-edge entrance.
		var pathX = GameConstants.TileSize * 0.5f;
		foreach ( var pathY in PathYs )
		{
			var pathPosition = new Vector3( pathX, pathY, 0f );
			BuildSystem.Instance.SpawnPlaceable( pathDef, pathPosition, 0f );
		}

		var habitatPosition = BuildSnap.ResolvePlacement( HabitatAnchor, habitatDef, 0f, PlotSystem.Instance );
		var habitat = BuildSystem.Instance.SpawnHabitat( habitatDef, habitatPosition );
		if ( habitat is null )
			return false;

		PathNetwork.Invalidate();
		Log.Info( $"[Fauna2 Starter] Ready-to-open layout built for {biome} entrance={entrancePosition} habitat={habitatPosition}." );
		return true;
	}
}
