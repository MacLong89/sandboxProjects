namespace Terraingen.Multiplayer;

using Terraingen;
using Terraingen.Victory;

/// <summary>World save import/export for host victory state.</summary>
public static class ThornsVictoryPersistence
{
	public static void Capture( ThornsPersistentWorldDto world )
	{
		if ( world is null || ThornsVictoryManager.Instance is null || !ThornsVictoryManager.Instance.IsValid )
			return;

		world.VictoryState = ThornsVictoryManager.Instance.ExportPersistent();
	}

	public static void RestoreHost()
	{
		var persistence = ThornsWorldPersistence.Instance;
		if ( persistence is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		persistence.HostEnsureInitialized();
		var manager = ThornsVictoryManager.EnsureInstance();
		if ( manager is null || !manager.IsValid )
			return;

		manager.ImportPersistent( persistence.Live?.VictoryState );
	}
}
