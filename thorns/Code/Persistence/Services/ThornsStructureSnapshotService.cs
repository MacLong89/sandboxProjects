namespace Sandbox;

/// <summary>
/// Structure snapshot domain — placed buildings, containers, workbenches, campfires, doors, shadow quit-save state.
/// </summary>
public static class ThornsStructureSnapshotService
{
	static double _structureDirtySaveDueRealtime;
	static List<ThornsPersistentStructureDto> _structureShadowCopy;
	static bool _structureShadowAuthoritativelyEmpty;
	static bool _pendingDemolishAuthoritativeEmptyCheck;

	public static bool PendingDemolishAuthoritativeEmptyCheck
	{
		get => _pendingDemolishAuthoritativeEmptyCheck;
		set => _pendingDemolishAuthoritativeEmptyCheck = value;
	}

	public static double StructureDirtySaveDueRealtime
	{
		get => _structureDirtySaveDueRealtime;
		set => _structureDirtySaveDueRealtime = value;
	}

	public static int ShadowCopyCount => _structureShadowCopy?.Count ?? 0;

	public static bool ShadowAuthoritativelyEmpty => _structureShadowAuthoritativelyEmpty;

	public static void HostNotifyWorldStructuresDirty()
	{
		if ( !Networking.IsHost )
			return;

		_structureDirtySaveDueRealtime = Time.Now + 0.45;
	}

	public static void HostNotifyWorldStructureSpawned()
	{
		if ( !Networking.IsHost )
			return;

		_structureShadowAuthoritativelyEmpty = false;
	}

	public static void HostNotifyStructureDestroyedByDemolish()
	{
		if ( !Networking.IsHost )
			return;

		if ( ThornsPlacedStructure.ActiveByInstanceId.Count == 0 )
			HostMarkWorldStructuresAuthoritativelyEmpty();
		else
			_pendingDemolishAuthoritativeEmptyCheck = true;
	}

	public static void HostTickPendingDemolishEmptyCheck()
	{
		_pendingDemolishAuthoritativeEmptyCheck = false;
		if ( ThornsPlacedStructure.ActiveByInstanceId.Count == 0 )
			HostMarkWorldStructuresAuthoritativelyEmpty();
	}

	static void HostMarkWorldStructuresAuthoritativelyEmpty()
	{
		_structureShadowAuthoritativelyEmpty = true;
		_structureShadowCopy = null;
	}

	public static void HostRefreshStructureShadowFromLoadedDisk( ThornsPersistentWorldDto live )
	{
		_structureShadowAuthoritativelyEmpty = false;
		if ( live?.Structures is { Count: > 0 } )
			_structureShadowCopy = CloneStructureList( live.Structures );
		else
			_structureShadowCopy = null;
	}

	public static List<ThornsPersistentStructureDto> HostResolveStructuresForSnapshot()
	{
		var liveStructures = HostCaptureStructures();
		if ( liveStructures.Count > 0 )
		{
			_structureShadowCopy = CloneStructureList( liveStructures );
			_structureShadowAuthoritativelyEmpty = false;
			return liveStructures;
		}

		if ( !_structureShadowAuthoritativelyEmpty && _structureShadowCopy is { Count: > 0 } )
		{
			Log.Warning(
				"[Thorns] Persistence: structure registry empty while capturing snapshot — preserving last known world geometry (buildings are world state; scene teardown can clear the registry before save)." );
			return CloneStructureList( _structureShadowCopy );
		}

		return liveStructures;
	}

	public static void HostApplyStructuresFromSave( Scene scene, IEnumerable<ThornsPersistentStructureDto> structures )
	{
		if ( structures is null )
			return;

		foreach ( var s in structures )
		{
			if ( s is null || string.IsNullOrWhiteSpace( s.StructureDefId ) || s.InstanceId == Guid.Empty )
				continue;

			var pos = new Vector3( s.Px, s.Py, s.Pz );
			var rot = Rotation.From( s.RPitch, s.RYaw, s.RRoll );

			var ps = ThornsPlacedStructure.SpawnHostFromSave(
				scene,
				s.InstanceId,
				s.OwnerAccountKey ?? "",
				Guid.Empty,
				s.StructureDefId,
				pos,
				rot,
				s.CurrentHealth,
				s.UpgradeTier,
				out var fail );

			if ( !ps.IsValid() )
			{
				Log.Warning( $"[Thorns] Persistence: structure restore failed '{fail}' def={s.StructureDefId}" );
				continue;
			}

			if ( string.Equals( s.StructureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase )
			     && s.ChestSlots is { Length: > 0 }
			     && ThornsStorageChest.TryGetForStructure( ps.InstanceId, out var chest )
			     && chest.IsValid() )
			{
				chest.HostRestoreSlotsFromPersistence( ChestPersistRowsToNetForStorageChest( s.ChestSlots ) );
			}

			if ( ThornsFurnitureLootPolicy.IsPlayerStorableFurniture( s.StructureDefId )
			     && s.ChestSlots is { Length: > 0 }
			     && ThornsFurnitureContainer.TryGetForStructure( ps.InstanceId, out var furniture )
			     && furniture.IsValid() )
			{
				furniture.HostRestoreSlotsFromPersistence( ChestPersistRowsToNetForStorageChest( s.ChestSlots ) );
			}

			if ( string.Equals( s.StructureDefId, "campfire", StringComparison.OrdinalIgnoreCase )
			     && s.CampfireSlots is { Length: > 0 }
			     && ThornsCampfire.TryGetForStructure( ps.InstanceId, out var cf )
			     && cf.IsValid() )
			{
				cf.HostRestoreSlotsFromPersistence( ChestPersistRowsToNetForCampfire( s.CampfireSlots ) );
			}

			if ( string.Equals( s.StructureDefId, "workbench", StringComparison.OrdinalIgnoreCase )
			     && s.WorkbenchSlots is { Length: > 0 }
			     && ThornsWorkbench.TryGetForStructure( ps.InstanceId, out var wb )
			     && wb.IsValid() )
			{
				wb.HostRestoreSlotsFromPersistence( ChestPersistRowsToNetForWorkbench( s.WorkbenchSlots ) );
			}

			if ( string.Equals( s.StructureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase )
			     && ThornsPlayerDoor.ActiveByFrameId.TryGetValue( ps.InstanceId, out var door )
			     && door.IsValid() )
				door.HostSetOpenImmediate( s.DoorOpen );
		}
	}

	public static void HostRemapStructureOwnersForAccountKey( string accountKey, Guid newConnectionId )
	{
		if ( string.IsNullOrEmpty( accountKey ) || newConnectionId == Guid.Empty )
			return;

		var idStr = newConnectionId.ToString( "D" );

		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( ps is null || !ps.IsValid() )
				continue;

			if ( ps.OwnerAccountKeySync == accountKey )
			{
				ps.OwnerConnectionIdSync = idStr;
				if ( string.Equals( ps.StructureDefId, "base_core", StringComparison.OrdinalIgnoreCase ) )
					ThornsBuildingAuthority.HostRegisterPlacedBaseCore( newConnectionId, ps.GameObject.WorldPosition );
			}
		}
	}

	static List<ThornsPersistentStructureDto> HostCaptureStructures()
	{
		var list = new List<ThornsPersistentStructureDto>();
		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( ps is null || !ps.IsValid() )
				continue;

			var t = ps.GameObject.WorldTransform;
			var ang = t.Rotation.Angles();
			var row = new ThornsPersistentStructureDto
			{
				InstanceId = ps.InstanceId,
				OwnerAccountKey = ps.OwnerAccountKeySync ?? "",
				StructureDefId = ps.StructureDefId ?? "",
				Px = t.Position.x,
				Py = t.Position.y,
				Pz = t.Position.z,
				RPitch = ang.pitch,
				RYaw = ang.yaw,
				RRoll = ang.roll,
				CurrentHealth = ps.CurrentHealth,
				UpgradeTier = ps.MaterialTier
			};

			if ( string.Equals( ps.StructureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase )
			     && ThornsStorageChest.TryGetForStructure( ps.InstanceId, out var chest )
			     && chest.IsValid() )
			{
				row.ChestSlots = HostChestSlotsNetToPersistRows( chest.HostSnapshotSlotsForPersistence() );
			}

			if ( ThornsFurnitureLootPolicy.IsPlayerStorableFurniture( ps.StructureDefId )
			     && ThornsFurnitureContainer.TryGetForStructure( ps.InstanceId, out var furniture )
			     && furniture.IsValid() )
			{
				row.ChestSlots = HostChestSlotsNetToPersistRows( furniture.HostSnapshotSlotsForPersistence() );
			}

			if ( string.Equals( ps.StructureDefId, "campfire", StringComparison.OrdinalIgnoreCase )
			     && ThornsCampfire.TryGetForStructure( ps.InstanceId, out var campfire )
			     && campfire.IsValid() )
			{
				row.CampfireSlots = HostChestSlotsNetToPersistRows( campfire.HostSnapshotSlotsForPersistence() );
			}

			if ( string.Equals( ps.StructureDefId, "workbench", StringComparison.OrdinalIgnoreCase )
			     && ThornsWorkbench.TryGetForStructure( ps.InstanceId, out var workbench )
			     && workbench.IsValid() )
			{
				row.WorkbenchSlots = HostChestSlotsNetToPersistRows( workbench.HostSnapshotSlotsForPersistence() );
			}

			if ( string.Equals( ps.StructureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase )
			     && ThornsPlayerDoor.ActiveByFrameId.TryGetValue( ps.InstanceId, out var door )
			     && door.IsValid() )
				row.DoorOpen = door.DoorOpenSync;

			list.Add( row );
		}

		return list;
	}

	static ThornsPersistInventorySlotDto[] CloneChestPersistRows( ThornsPersistInventorySlotDto[] src )
	{
		if ( src is null || src.Length == 0 )
			return null;

		var dst = new ThornsPersistInventorySlotDto[src.Length];
		for ( var i = 0; i < src.Length; i++ )
		{
			var r = src[i];
			if ( r is null )
			{
				dst[i] = null;
				continue;
			}

			dst[i] = new ThornsPersistInventorySlotDto
			{
				ItemId = r.ItemId,
				Quantity = r.Quantity,
				HasDurability = r.HasDurability,
				Durability = r.Durability,
				WeaponInstanceId = r.WeaponInstanceId,
				WeaponLoadedAmmo = r.WeaponLoadedAmmo,
				WeaponRollPayload = r.WeaponRollPayload,
				ArmorRollPayload = r.ArmorRollPayload
			};
		}

		return dst;
	}

	static ThornsPersistInventorySlotDto[] HostChestSlotsNetToPersistRows( ThornsInventorySlotNet[] net )
	{
		if ( net is null || net.Length == 0 )
			return null;

		var dst = new ThornsPersistInventorySlotDto[net.Length];
		for ( var i = 0; i < net.Length; i++ )
			dst[i] = ThornsPersistInventorySlotDto.FromSlotNet( net[i] );

		return dst;
	}

	static ThornsInventorySlotNet[] ChestPersistRowsToNetForStorageChest( ThornsPersistInventorySlotDto[] rows )
	{
		var n = ThornsStorageChest.SlotCount;
		var dst = new ThornsInventorySlotNet[n];
		if ( rows is null )
			return dst;

		for ( var i = 0; i < n; i++ )
			dst[i] = i < rows.Length && rows[i] is not null
				? ThornsPersistInventorySlotDto.ToSlotNet( rows[i] )
				: default;

		return dst;
	}

	static ThornsInventorySlotNet[] ChestPersistRowsToNetForCampfire( ThornsPersistInventorySlotDto[] rows )
	{
		var n = ThornsCampfire.SlotCount;
		var dst = new ThornsInventorySlotNet[n];
		if ( rows is null )
			return dst;

		for ( var i = 0; i < n; i++ )
			dst[i] = i < rows.Length && rows[i] is not null
				? ThornsPersistInventorySlotDto.ToSlotNet( rows[i] )
				: default;

		return dst;
	}

	static ThornsInventorySlotNet[] ChestPersistRowsToNetForWorkbench( ThornsPersistInventorySlotDto[] rows )
	{
		var n = ThornsWorkbench.SlotCount;
		var dst = new ThornsInventorySlotNet[n];
		if ( rows is null )
			return dst;

		for ( var i = 0; i < n; i++ )
			dst[i] = i < rows.Length && rows[i] is not null
				? ThornsPersistInventorySlotDto.ToSlotNet( rows[i] )
				: default;

		return dst;
	}

	static List<ThornsPersistentStructureDto> CloneStructureList( List<ThornsPersistentStructureDto> src )
	{
		var dst = new List<ThornsPersistentStructureDto>( src?.Count ?? 0 );
		if ( src is null )
			return dst;

		foreach ( var row in src )
		{
			if ( row is null )
				continue;

			dst.Add( new ThornsPersistentStructureDto
			{
				InstanceId = row.InstanceId,
				OwnerAccountKey = row.OwnerAccountKey ?? "",
				StructureDefId = row.StructureDefId ?? "",
				Px = row.Px,
				Py = row.Py,
				Pz = row.Pz,
				RPitch = row.RPitch,
				RYaw = row.RYaw,
				RRoll = row.RRoll,
				CurrentHealth = row.CurrentHealth,
				UpgradeTier = row.UpgradeTier,
				ChestSlots = CloneChestPersistRows( row.ChestSlots ),
				CampfireSlots = CloneChestPersistRows( row.CampfireSlots ),
				WorkbenchSlots = CloneChestPersistRows( row.WorkbenchSlots ),
				DoorOpen = row.DoorOpen
			} );
		}

		return dst;
	}
}
