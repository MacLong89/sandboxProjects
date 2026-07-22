namespace Terraingen.Multiplayer;

using Terraingen.World;

/// <summary>Persists death/loot crates in the world save (v13+).</summary>
public static class ThornsDeathCratePersistence
{
	public static void Capture( ThornsPersistentWorldDto world )
	{
		if ( world is null )
			return;

		world.DeathCrates ??= new List<ThornsPersistentDeathCrateDto>();
		world.DeathCrates.Clear();

		var service = ThornsDeathCrateWorldService.Instance;
		if ( service is null || !service.IsValid() )
			return;

		world.DeathCrates.AddRange( service.HostExportSnapshots() );
	}

	public static void RestoreHost()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var world = ThornsWorldPersistence.Instance?.Live;
		var service = ThornsDeathCrateWorldService.Instance;
		if ( service is null || !service.IsValid() || world?.DeathCrates is null or { Count: 0 } )
			return;

		service.HostImportSnapshots( world.DeathCrates );
	}
}
