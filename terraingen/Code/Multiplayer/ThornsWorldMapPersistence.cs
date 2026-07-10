namespace Terraingen.Multiplayer;

using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Persists per-account map waypoints in the world save.</summary>
public static class ThornsWorldMapPersistence
{
	public static void Capture( ThornsPersistentWorldDto world )
	{
		if ( world is null )
			return;

		world.PlayerMapsByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerMapDto>();
		ThornsMapWorldService.Instance?.ExportHostWaypointsTo( world.PlayerMapsByAccountKey );
	}

	public static void RestoreHost()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var world = ThornsWorldPersistence.Instance?.Live;
		if ( world?.PlayerMapsByAccountKey is null )
			return;

		ThornsMapWorldService.Instance?.ImportHostWaypointsFrom( world.PlayerMapsByAccountKey );
	}
}
