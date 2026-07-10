namespace Terraingen.Multiplayer;



using Terraingen.Buildings;



/// <summary>Captures and restores player-placed structures from world save.</summary>

public static class ThornsStructurePersistence

{

	public static bool PersistedStructuresRestoreAttempted { get; private set; }



	public static void ResetRestoreGate() => PersistedStructuresRestoreAttempted = false;



	/// <summary>

	/// Snapshots live player structures into the world DTO.

	/// When <paramref name="forceReplace"/> is false and capture is empty (scene teardown / registry gap),

	/// existing saved structures are kept so quit/reload does not wipe them.

	/// </summary>

	public static int Capture( ThornsPersistentWorldDto world, Scene scene, bool forceReplace )

	{

		world.Structures ??= new List<ThornsPersistentStructureDto>();

		world.StructureStorages ??= new List<ThornsPersistentStructureStorageEntryDto>();



		RefreshRegistryFromScene( scene );



		var captured = new List<ThornsPersistentStructureDto>();

		var storages = new List<ThornsPersistentStructureStorageEntryDto>();



		foreach ( var placed in EnumeratePlacedStructures( scene ) )

		{

			if ( placed is null || !placed.IsValid() || string.IsNullOrWhiteSpace( placed.StructureId ) )

				continue;



			var t = placed.GameObject.WorldTransform;

			var angles = t.Rotation.Angles();

			var doorOpen = false;
			if ( string.Equals( placed.StructureId, "wood_doorframe", StringComparison.OrdinalIgnoreCase )
			     && ThornsPlayerDoor.ActiveByFrameKey.TryGetValue( placed.InstanceKey, out var door )
			     && door.IsValid() )
				doorOpen = door.DoorOpenSync;

			captured.Add( new ThornsPersistentStructureDto

			{

				InstanceKey = placed.InstanceKey,

				StructureId = placed.StructureId,

				OwnerAccountKey = placed.OwnerAccountKey ?? "",

				MaterialTier = placed.MaterialTier,
				CurrentHealth = placed.CurrentHealth,

				Px = t.Position.x,

				Py = t.Position.y,

				Pz = t.Position.z,

				RPitch = angles.pitch,

				RYaw = angles.yaw,

				RRoll = angles.roll,

				DoorOpen = doorOpen

			} );



			if ( !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )

				continue;



			var storage = ThornsPlacedStructureStorage.EnsureOn( placed );

			if ( !storage.IsValid() )

				continue;



			var storageDto = storage.CaptureDto();

			storages.Add( new ThornsPersistentStructureStorageEntryDto

			{

				InstanceKey = placed.InstanceKey,

				Slots = storageDto.Slots ?? new List<ThornsPersistentItemStackDto>()

			} );

		}



		if ( captured.Count > 0 )

		{

			world.Structures = captured;

			world.StructureStorages = FilterStoragesForStructures( storages, captured );

			return captured.Count;

		}



		if ( storages.Count > 0 )

			world.StructureStorages = MergeStoragesByInstanceKey( world.StructureStorages, storages );



		if ( !forceReplace )

			return world.Structures.Count;



		if ( !PersistedStructuresRestoreAttempted && world.Structures.Count > 0 )

			return world.Structures.Count;



		world.Structures = captured;

		world.StructureStorages = storages;

		return world.Structures.Count;

	}



	public static void RestoreHost( Scene scene, ThornsPersistentWorldDto world )

	{

		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() || world?.Structures is null )

			return;



		PersistedStructuresRestoreAttempted = true;



		var restored = 0;

		var restoredPortables = 0;



		foreach ( var saved in world.Structures )

		{

			if ( saved is null || string.IsNullOrWhiteSpace( saved.StructureId ) )

				continue;



			if ( ThornsPlacedBuildStructure.TryFindByInstanceKey( saved.InstanceKey, out var existing ) && existing.IsValid() )

				continue;



			var rot = new Angles( saved.RPitch, saved.RYaw, saved.RRoll ).ToRotation();

			var pos = new Vector3( saved.Px, saved.Py, saved.Pz );

			var placed = ThornsPlacedBuildStructure.SpawnHost(

				scene,

				null,

				saved.StructureId,

				pos,

				rot,

				saved.InstanceKey );

			if ( !placed.IsValid() )

				continue;



			placed.OwnerAccountKey = saved.OwnerAccountKey ?? "";

			placed.MaterialTier = saved.MaterialTier;
			placed.HostEnsureHealthInitialized( saved.CurrentHealth > 0f ? saved.CurrentHealth : null );

			placed.ApplyCurrentVisual();

			placed.HostEnsureStorageComponent();
			placed.HostEnsureDoorComponent( saved.DoorOpen );

			restored++;



			if ( ThornsPlacedStructureStorage.IsPlayerPortableStructure( saved.StructureId ) )

				restoredPortables++;

		}



		RefreshRegistryFromScene( scene );



		if ( world.StructureStorages is not null )

		{

			foreach ( var entry in world.StructureStorages )

			{

				if ( entry is null || string.IsNullOrWhiteSpace( entry.InstanceKey ) )

					continue;



				if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( entry.InstanceKey, out var placed ) || !placed.IsValid() )

					continue;



				var storage = ThornsPlacedStructureStorage.EnsureOn( placed );

				if ( !storage.IsValid() )

					continue;



				storage.ApplyDto( new ThornsPersistentStructureStorageDto

				{

					Slots = entry.Slots ?? new List<ThornsPersistentItemStackDto>()

				} );

				placed.HostSyncWorldContainer( storage );

			}

		}



		foreach ( var placed in EnumeratePlacedStructures( scene ) )

		{

			if ( placed is null || !placed.IsValid() || !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )

				continue;



			var storage = ThornsPlacedStructureStorage.EnsureOn( placed );

			if ( !storage.IsValid() || string.IsNullOrWhiteSpace( placed.InstanceKey ) )

				continue;



			placed.HostSyncWorldContainer( storage );

		}



		if ( world.Structures.Count > 0 )

		{

			Log.Info(

				$"[Thorns Terrain] Restored {world.Structures.Count} player structure(s) from world save " +

				$"(spawned={restored}, portables={restoredPortables}, storages={world.StructureStorages?.Count ?? 0})." );

		}

	}



	static List<ThornsPersistentStructureStorageEntryDto> FilterStoragesForStructures(

		List<ThornsPersistentStructureStorageEntryDto> storages,

		List<ThornsPersistentStructureDto> structures )

	{

		if ( storages is null or { Count: 0 } || structures is null or { Count: 0 } )

			return storages ?? new List<ThornsPersistentStructureStorageEntryDto>();



		var keys = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var structure in structures )

		{

			if ( !string.IsNullOrWhiteSpace( structure?.InstanceKey ) )

				keys.Add( structure.InstanceKey );

		}



		var filtered = new List<ThornsPersistentStructureStorageEntryDto>( storages.Count );

		foreach ( var entry in storages )

		{

			if ( entry is null || string.IsNullOrWhiteSpace( entry.InstanceKey ) )

				continue;



			if ( keys.Contains( entry.InstanceKey ) )

				filtered.Add( entry );

		}



		return filtered;

	}



	static List<ThornsPersistentStructureStorageEntryDto> MergeStoragesByInstanceKey(

		List<ThornsPersistentStructureStorageEntryDto> existing,

		List<ThornsPersistentStructureStorageEntryDto> live )

	{

		var merged = new Dictionary<string, ThornsPersistentStructureStorageEntryDto>( StringComparer.OrdinalIgnoreCase );



		if ( existing is not null )

		{

			foreach ( var entry in existing )

			{

				if ( entry is null || string.IsNullOrWhiteSpace( entry.InstanceKey ) )

					continue;



				merged[entry.InstanceKey] = entry;

			}

		}



		foreach ( var entry in live )

		{

			if ( entry is null || string.IsNullOrWhiteSpace( entry.InstanceKey ) )

				continue;



			merged[entry.InstanceKey] = entry;

		}



		return merged.Values.ToList();

	}



	static IEnumerable<ThornsPlacedBuildStructure> EnumeratePlacedStructures( Scene scene )

	{

		if ( scene is not null && scene.IsValid() )

		{

			foreach ( var placed in scene.GetAllComponents<ThornsPlacedBuildStructure>() )

			{

				if ( placed.IsValid() )

					yield return placed;

			}



			yield break;

		}



		foreach ( var placed in ThornsPlacedBuildStructure.Registry )

		{

			if ( placed.IsValid() )

				yield return placed;

		}

	}



	static void RefreshRegistryFromScene( Scene scene )

	{

		foreach ( var placed in ThornsPlacedBuildStructure.Registry.ToArray() )

		{

			if ( placed is null || !placed.IsValid() )

				ThornsPlacedBuildStructure.Registry.Remove( placed );

		}



		if ( scene is null || !scene.IsValid() )

			return;



		foreach ( var placed in scene.GetAllComponents<ThornsPlacedBuildStructure>() )

		{

			if ( placed.IsValid() )

				ThornsPlacedBuildStructure.Registry.Add( placed );

		}

	}

}

