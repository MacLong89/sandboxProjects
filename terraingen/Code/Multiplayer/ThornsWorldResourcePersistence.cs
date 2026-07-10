namespace Terraingen.Multiplayer;

using Terraingen.Foliage;
using Terraingen.Minerals;

/// <summary>Captures and restores depleted trees and mineral nodes.</summary>
public static class ThornsWorldResourcePersistence
{
	public static void Capture( ThornsPersistentWorldDto world )
	{
		if ( world is null )
			return;

		world.DepletedTreeIds ??= new List<int>();
		world.DepletedMineralNodeIds ??= new List<int>();
		world.DepletedTreeIds.Clear();
		world.DepletedMineralNodeIds.Clear();

		var trees = ThornsTreeWorldService.Instance;
		if ( trees is not null && trees.IsValid() )
			world.DepletedTreeIds.AddRange( trees.HostExportDepletedIds() );

		var minerals = ThornsMineralWorldService.Instance;
		if ( minerals is not null && minerals.IsValid() )
			world.DepletedMineralNodeIds.AddRange( minerals.HostExportDepletedIds() );
	}

	public static void RestoreHost()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var world = ThornsWorldPersistence.Instance?.Live;
		if ( world is null )
			return;

		var trees = ThornsTreeWorldService.Instance;
		if ( trees is not null && trees.IsValid() && world.DepletedTreeIds?.Count > 0 )
			trees.HostApplyDepletedIds( world.DepletedTreeIds );

		var minerals = ThornsMineralWorldService.Instance;
		if ( minerals is not null && minerals.IsValid() && world.DepletedMineralNodeIds?.Count > 0 )
			minerals.HostApplyDepletedIds( world.DepletedMineralNodeIds );
	}
}
