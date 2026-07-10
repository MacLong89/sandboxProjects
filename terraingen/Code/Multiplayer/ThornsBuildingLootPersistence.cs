namespace Terraingen.Multiplayer;

using Terraingen.Buildings;
using Terraingen.World;

/// <summary>Persists furniture loot container state in the world save.</summary>
public static class ThornsBuildingLootPersistence
{
	public static void Capture( ThornsPersistentWorldDto world )
	{
		if ( world is null )
			return;

		world.FurnitureContainers ??= new List<ThornsPersistentFurnitureContainerDto>();
		world.FurnitureContainers.Clear();

		var service = ThornsWorldLootContainerService.Instance;
		if ( service is null || !service.IsValid() )
			return;

		world.FurnitureContainers.AddRange( service.HostExportFurnitureSnapshots() );
	}

	public static void RestoreHost()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var world = ThornsWorldPersistence.Instance?.Live;
		var containerService = ThornsWorldLootContainerService.Instance;
		var buildingService = ThornsBuildingLootWorldService.Instance;
		if ( containerService is null || !containerService.IsValid() || buildingService is null || !buildingService.IsValid() )
			return;

		if ( world?.FurnitureContainers is { Count: > 0 } )
		{
			foreach ( var saved in world.FurnitureContainers )
				containerService.HostApplyFurnitureSnapshot( saved );

			return;
		}

		if ( world?.LootedFurnitureIds is null or { Count: 0 } )
		{
			buildingService.HostSyncFurnitureContainers();
			return;
		}

		var now = Time.Now;
		foreach ( var furnitureId in world.LootedFurnitureIds )
		{
			if ( furnitureId <= 0 )
				continue;

			if ( !buildingService.TryGetFurnitureLootTable( furnitureId, out var lootTable ) )
				lootTable = "home_clutter";

			containerService.HostApplyFurnitureSnapshot( new ThornsPersistentFurnitureContainerDto
			{
				FurnitureId = furnitureId,
				LootTable = lootTable,
				LootSeed = HashCode.Combine( furnitureId, lootTable, 0xF00D ),
				HasRolledLoot = true,
				EmptySinceUtc = now,
				Slots = new List<ThornsPersistentItemStackDto>()
			} );
		}
	}
}
