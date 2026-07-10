namespace Terraingen.World;

using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Host-authoritative loot containers for furniture, airdrops, death crates, and placed chests.</summary>
[Title( "Thorns World Loot Containers" )]
[Category( "Thorns/World" )]
public sealed class ThornsWorldLootContainerService : Component
{
	public const int DefaultLootSlotCount = 12;
	public const int DeathCrateSlotCount = 24;
	public const float RefillEmptySeconds = 300f;

	public static ThornsWorldLootContainerService Instance { get; private set; }

	readonly Dictionary<string, ContainerRecord> _containers = new( 128 );

	protected override void OnStart() => Instance = this;

	protected override void OnDestroy()
	{
		_containers.Clear();
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		HostTickRefills();
	}

	public static void EnsureForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( scene.GetAllComponents<ThornsWorldLootContainerService>().FirstOrDefault() is not null )
			return;

		var go = scene.CreateObject();
		go.Name = "ThornsWorldLootContainers";
		go.Components.Create<ThornsWorldLootContainerService>();
	}

	public static string FurnitureKey( int furnitureId ) => $"furn:{furnitureId}";
	public static string AirdropKey( int airdropId ) => $"air:{airdropId}";
	public static string DeathCrateKey( int crateId ) => $"death:{crateId}";
	public static string StructureKey( string instanceKey ) => $"struct:{instanceKey}";

	public void HostRegisterFurniture( int furnitureId, string lootTable )
	{
		var key = FurnitureKey( furnitureId );
		if ( _containers.ContainsKey( key ) )
			return;

		_containers[key] = new ContainerRecord
		{
			Key = key,
			Title = "Furniture",
			SlotCount = DefaultLootSlotCount,
			Slots = new ThornsItemStack[DefaultLootSlotCount],
			LootTable = lootTable ?? "home_clutter",
			LootSeed = HashCode.Combine( furnitureId, lootTable, 0xF00D ),
			CanRefill = true
		};
	}

	public void HostRegisterAirdrop( int airdropId, IEnumerable<ThornsBuildingLoot> loot, int lootSeed )
	{
		var key = AirdropKey( airdropId );
		var record = new ContainerRecord
		{
			Key = key,
			Title = "Supply Drop",
			SlotCount = DefaultLootSlotCount,
			Slots = new ThornsItemStack[DefaultLootSlotCount],
			LootTable = "airdrop",
			LootSeed = lootSeed,
			CanRefill = false
		};
		HostPlaceLootIntoRecord( record, loot );
		_containers[key] = record;
	}

	public void HostRegisterDeathCrate( int crateId, IReadOnlyList<ThornsItemStack> items, string title = "Death Crate" )
	{
		var key = DeathCrateKey( crateId );
		var record = new ContainerRecord
		{
			Key = key,
			Title = string.IsNullOrWhiteSpace( title ) ? "Death Crate" : title,
			SlotCount = DeathCrateSlotCount,
			Slots = new ThornsItemStack[DeathCrateSlotCount],
			CanRefill = false
		};
		HostPlaceStacksIntoRecord( record, items );
		_containers[key] = record;
	}

	public void HostRegisterStructureStorage( string instanceKey, ThornsPlacedStructureStorage storage )
	{
		if ( string.IsNullOrWhiteSpace( instanceKey ) || storage is null || !storage.IsValid() )
			return;

		var key = StructureKey( instanceKey );
		var title = ResolveStructureContainerTitle( storage );

		if ( _containers.TryGetValue( key, out var existing ) )
		{
			existing.Storage = storage;
			existing.Title = title;
			return;
		}

		_containers[key] = new ContainerRecord
		{
			Key = key,
			Title = title,
			SlotCount = ThornsPlacedStructureStorage.SlotCount,
			Slots = new ThornsItemStack[ThornsPlacedStructureStorage.SlotCount],
			CanRefill = false,
			Storage = storage
		};
	}

	static string ResolveStructureContainerTitle( ThornsPlacedStructureStorage storage )
	{
		var structure = storage?.Components.Get<ThornsPlacedBuildStructure>();
		if ( structure is null || !structure.IsValid() )
			return "Storage Chest";

		if ( ThornsPlayerBuildingDefinitions.TryGet( structure.StructureId, out var def )
		     && !string.IsNullOrWhiteSpace( def.DisplayName ) )
			return def.DisplayName;

		return "Storage Chest";
	}

	public void HostUnregister( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return;

		_containers.Remove( key );
	}

	public bool TryGet( string key, out ContainerRecord record )
	{
		record = null;
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		return _containers.TryGetValue( key, out record ) && record is not null;
	}

	/// <summary>Register a container key on demand when interaction succeeds but bootstrap registration was missed.</summary>
	public void HostEnsureRegistered( string key )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( key ) || TryGet( key, out _ ) )
			return;

		if ( key.StartsWith( "furn:", StringComparison.Ordinal )
		     && int.TryParse( key.AsSpan( 5 ), out var furnitureId )
		     && ThornsBuildingLootWorldService.Instance?.TryGetFurnitureLootTable( furnitureId, out var lootTable ) == true )
		{
			HostRegisterFurniture( furnitureId, lootTable );
			return;
		}

		if ( !key.StartsWith( "struct:", StringComparison.Ordinal ) )
			return;

		var instanceKey = key["struct:".Length..];
		if ( string.IsNullOrWhiteSpace( instanceKey )
		     || !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed )
		     || !placed.IsValid()
		     || !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )
			return;

		var storage = ThornsPlacedStructureStorage.EnsureOn( placed );
		if ( storage.IsValid() )
			placed.HostSyncWorldContainer( storage );
	}

	/// <summary>Ensure placed storage chests are registered after world load / hot reload.</summary>
	public void HostResyncStructureStorages( Scene scene )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() )
			return;

		foreach ( var placed in scene.GetAllComponents<ThornsPlacedBuildStructure>() )
		{
			if ( placed is null || !placed.IsValid() || !ThornsPlacedStructureStorage.IsStorageStructure( placed.StructureId ) )
				continue;

			var storage = ThornsPlacedStructureStorage.EnsureOn( placed );
			if ( storage.IsValid() )
				placed.HostSyncWorldContainer( storage );
		}
	}

	public void HostEnsureLootReady( string key )
	{
		if ( !TryGet( key, out var record ) )
			return;

		if ( record.Storage is not null )
			return;

		if ( !record.IsEmpty() )
		{
			record.EmptySince = -1;
			return;
		}

		if ( record.HasRolledLoot )
			return;

		HostRollLootTableIntoRecord( record );
		record.HasRolledLoot = true;
		record.EmptySince = -1;
	}

	public ThornsItemStack HostGetSlot( string key, int index )
	{
		if ( !TryGet( key, out var record ) )
			return ThornsItemStack.EmptyStack;

		if ( record.Storage is not null )
			return record.Storage.GetSlot( index );

		if ( index < 0 || index >= record.Slots.Length )
			return ThornsItemStack.EmptyStack;

		return record.Slots[index];
	}

	public void HostSetSlot( string key, int index, ThornsItemStack stack )
	{
		if ( !TryGet( key, out var record ) )
			return;

		if ( record.Storage is not null )
		{
			record.Storage.SetSlot( index, stack );
			HostUpdateEmptyState( record );
			return;
		}

		if ( index < 0 || index >= record.Slots.Length )
			return;

		record.Slots[index] = stack;
		HostEnforceLootableFurnitureGunCap( record );
		HostUpdateEmptyState( record );
	}

	public bool HostIsEmpty( string key ) =>
		TryGet( key, out var record ) && record.IsEmpty();

	public ThornsExternalContainerSnapshotDto HostBuildSnapshot( string key )
	{
		if ( !TryGet( key, out var record ) )
			return new ThornsExternalContainerSnapshotDto();

		HostEnsureLootReady( key );

		var dto = new ThornsExternalContainerSnapshotDto
		{
			IsOpen = true,
			ContainerKey = key,
			Title = record.Title,
			SlotCount = record.SlotCount
		};

		for ( var i = 0; i < record.SlotCount; i++ )
		{
			var stack = HostGetSlot( key, i );
			if ( stack.IsEmpty )
				continue;

			dto.Slots.Add( BuildSlotDto( i, stack ) );
		}

		if ( record.CanRefill && record.IsEmpty() && record.EmptySince >= 0 )
		{
			var elapsed = (float)(Time.Now - record.EmptySince);
			dto.RefillSecondsRemaining = Math.Max( 0f, RefillEmptySeconds - elapsed );
		}

		return dto;
	}

	public void HostApplyFurnitureSnapshot( ThornsPersistentFurnitureContainerDto saved )
	{
		if ( saved is null || saved.FurnitureId <= 0 )
			return;

		var key = FurnitureKey( saved.FurnitureId );
		if ( !_containers.TryGetValue( key, out var record ) )
		{
			record = new ContainerRecord
			{
				Key = key,
				Title = "Furniture",
				SlotCount = DefaultLootSlotCount,
				Slots = new ThornsItemStack[DefaultLootSlotCount],
				LootTable = saved.LootTable ?? "home_clutter",
				LootSeed = saved.LootSeed != 0 ? saved.LootSeed : HashCode.Combine( saved.FurnitureId, 0xF00D ),
				CanRefill = true
			};
			_containers[key] = record;
		}

		record.HasRolledLoot = saved.HasRolledLoot;
		record.EmptySince = saved.EmptySinceUtc > 0 ? saved.EmptySinceUtc : -1;
		HostClearRecordSlots( record );

		if ( saved.Slots is null )
			return;

		foreach ( var entry in saved.Slots )
		{
			if ( entry is null || entry.SlotIndex < 0 || entry.SlotIndex >= record.SlotCount )
				continue;

			if ( string.IsNullOrWhiteSpace( entry.ItemId ) || entry.Count <= 0 )
				continue;

			var stack = new ThornsItemStack
			{
				ItemId = entry.ItemId.Trim(),
				Count = entry.Count,
				ItemTier = entry.ItemTier,
				StatRoll = entry.StatRoll,
				HasDurability = entry.HasDurability,
				Durability = entry.Durability
			};

			if ( entry.ItemTier <= 0 )
				ThornsInventoryWeaponState.PrepareWorldLootStack( ref stack, premiumTable: record.LootTable?.StartsWith( "military", StringComparison.OrdinalIgnoreCase ) == true );
			else if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			{
				var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId );
				ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, combatId );
				ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref stack, def );
			}

			record.Slots[entry.SlotIndex] = stack;
		}

		HostEnforceLootableFurnitureGunCap( record );
	}

	public IEnumerable<ThornsPersistentFurnitureContainerDto> HostExportFurnitureSnapshots()
	{
		foreach ( var record in _containers.Values )
		{
			if ( !record.Key.StartsWith( "furn:", StringComparison.Ordinal ) )
				continue;

			if ( !int.TryParse( record.Key.AsSpan( 5 ), out var furnitureId ) )
				continue;

			yield return HostCaptureFurnitureDto( furnitureId, record );
		}
	}

	public void HostSyncStructureFromStorage( ThornsPlacedStructureStorage storage )
	{
		if ( storage is null || !storage.IsValid() )
			return;

		var structure = storage.Components.Get<ThornsPlacedBuildStructure>();
		if ( !structure.IsValid() || string.IsNullOrWhiteSpace( structure.InstanceKey ) )
			return;

		HostRegisterStructureStorage( structure.InstanceKey, storage );
	}

	void HostTickRefills()
	{
		var now = Time.Now;
		foreach ( var record in _containers.Values )
		{
			if ( !record.CanRefill || record.Storage is not null )
				continue;

			if ( !record.IsEmpty() )
			{
				record.EmptySince = -1;
				continue;
			}

			if ( record.EmptySince < 0 )
				record.EmptySince = now;

			if ( now - record.EmptySince < RefillEmptySeconds )
				continue;

			HostRollLootTableIntoRecord( record );
			record.HasRolledLoot = true;
			record.EmptySince = -1;
			record.LootSeed = HashCode.Combine( record.LootSeed, (int)now );
		}
	}

	void HostUpdateEmptyState( ContainerRecord record )
	{
		if ( record.Storage is not null || !record.CanRefill )
		{
			if ( record.IsEmpty() )
			{
				if ( record.Key.StartsWith( "death:", StringComparison.Ordinal ) )
					HostDespawnDeathCrate( record.Key );
				else if ( record.Key.StartsWith( "air:", StringComparison.Ordinal ) )
					HostDespawnAirdrop( record.Key );
			}

			return;
		}

		record.EmptySince = record.IsEmpty() ? Time.Now : -1;
	}

	void HostDespawnAirdrop( string key )
	{
		if ( !key.StartsWith( "air:", StringComparison.Ordinal ) )
			return;

		if ( !int.TryParse( key.AsSpan( 4 ), out var airdropId ) )
			return;

		ThornsAirdropWorldService.Instance?.HostDespawnWhenEmpty( airdropId );
	}

	void HostDespawnDeathCrate( string key )
	{
		if ( !key.StartsWith( "death:", StringComparison.Ordinal ) )
			return;

		if ( !int.TryParse( key.AsSpan( 6 ), out var crateId ) )
			return;

		ThornsDeathCrateWorldService.Instance?.HostDespawnWhenEmpty( crateId );
	}

	void HostRollLootTableIntoRecord( ContainerRecord record )
	{
		HostClearRecordSlots( record );
		if ( string.Equals( record.LootTable, "airdrop", StringComparison.OrdinalIgnoreCase ) )
		{
			var loot = ThornsAirdropLootTables.Roll( new Random( record.LootSeed ) );
			HostPlaceLootIntoRecord( record, loot );
			return;
		}

		var rng = new Random( record.LootSeed );
		var excludePlayerBuildingItems = record.Key.StartsWith( "furn:", StringComparison.Ordinal );
		HostPlaceLootIntoRecord( record, ThornsBuildingLootTables.Roll( record.LootTable, rng, excludePlayerBuildingItems ) );
	}

	static void HostPlaceLootIntoRecord( ContainerRecord record, IEnumerable<ThornsBuildingLoot> loot )
	{
		if ( loot is null )
			return;

		foreach ( var entry in loot )
		{
			if ( string.IsNullOrWhiteSpace( entry.ItemId ) || entry.Count <= 0 )
				continue;

			HostTryAddStackToRecord( record, new ThornsItemStack { ItemId = entry.ItemId.Trim(), Count = entry.Count } );
		}

		HostNormalizeRecordLootWeapons( record );
	}

	static void HostPlaceStacksIntoRecord( ContainerRecord record, IReadOnlyList<ThornsItemStack> stacks )
	{
		if ( stacks is null )
			return;

		foreach ( var stack in stacks )
		{
			if ( stack.IsEmpty )
				continue;

			HostTryAddStackToRecord( record, stack );
		}

		HostNormalizeRecordLootWeapons( record );
	}

	static bool IsLootableFurnitureContainer( ContainerRecord record ) =>
		record.Key.StartsWith( "furn:", StringComparison.Ordinal ) && record.Storage is null;

	static bool StackIsGun( in ThornsItemStack stack )
	{
		if ( stack.IsEmpty )
			return false;

		return ThornsItemRegistry.TryGet( stack.ItemId, out var def )
		       && def.Category == ThornsItemCategory.Weapon;
	}

	static bool RecordContainsGun( ContainerRecord record )
	{
		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			if ( StackIsGun( record.Slots[i] ) )
				return true;
		}

		return false;
	}

	static void HostEnforceLootableFurnitureGunCap( ContainerRecord record )
	{
		if ( !IsLootableFurnitureContainer( record ) )
			return;

		var keptGun = false;
		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			if ( !StackIsGun( record.Slots[i] ) )
				continue;

			if ( !keptGun )
			{
				keptGun = true;
				continue;
			}

			record.Slots[i] = ThornsItemStack.EmptyStack;
		}
	}

	static void HostTryAddStackToRecord( ContainerRecord record, ThornsItemStack incoming )
	{
		incoming = ThornsItemIdAliases.CanonicalizeStack( incoming );
		if ( incoming.IsEmpty || !ThornsItemRegistry.TryGet( incoming.ItemId, out var def ) )
			return;

		if ( IsLootableFurnitureContainer( record ) && StackIsGun( incoming ) && RecordContainsGun( record ) )
			return;

		ThornsInventoryWeaponState.PrepareWorldLootStack( ref incoming );

		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			var slot = record.Slots[i];
			if ( slot.IsEmpty || !string.Equals( slot.ItemId, incoming.ItemId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var space = def.MaxStack - slot.Count;
			if ( space <= 0 )
				continue;

			var move = Math.Min( space, incoming.Count );
			record.Slots[i] = MergeStacks( slot, move );
			incoming.Count -= move;
			if ( incoming.Count <= 0 )
				return;
		}

		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			if ( !record.Slots[i].IsEmpty )
				continue;

			record.Slots[i] = incoming;
			return;
		}
	}

	static ThornsItemStack MergeStacks( ThornsItemStack slot, int addedCount ) =>
		ThornsInventoryWeaponState.CopyStackWithCount( slot, slot.Count + addedCount );

	static void HostNormalizeRecordLootWeapons( ContainerRecord record )
	{
		var premium = record.LootTable?.StartsWith( "military", StringComparison.OrdinalIgnoreCase ) == true;
		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			var stack = record.Slots[i];
			if ( stack.IsEmpty )
				continue;

			ThornsInventoryWeaponState.PrepareWorldLootStack( ref stack, premiumTable: premium );
			record.Slots[i] = stack;
		}

		HostEnforceLootableFurnitureGunCap( record );
	}

	static void HostClearRecordSlots( ContainerRecord record )
	{
		for ( var i = 0; i < record.Slots.Length; i++ )
			record.Slots[i] = ThornsItemStack.EmptyStack;
	}

	static ThornsInventorySlotDto BuildSlotDto( int index, ThornsItemStack stack )
	{
		var dto = new ThornsInventorySlotDto
		{
			Index = index,
			Container = ThornsContainerKind.WorldLoot,
			ItemId = stack.ItemId,
			Count = stack.Count,
			HasDurability = stack.HasDurability,
			Durability = stack.Durability,
			WeaponLoadedAmmo = stack.WeaponLoadedAmmo
		};

		dto.WeaponAttachmentIds = Terraingen.Combat.Attachments.ThornsWeaponAttachmentState.ToDtoList( stack );

		if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
		{
			var tier = ThornsInventoryWeaponState.ResolveDisplayTier( stack, def );
			dto.ItemTier = tier;
			dto.WeaponTier = tier;
			dto.StatRoll = stack.StatRoll;
			var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId );
			dto.WeaponBroken = stack.IsWeaponBroken( combatId );
			if ( def.Category == ThornsItemCategory.Weapon )
			{
				var wdef = ThornsWeaponDefinitions.Get( combatId );
				dto.WeaponClipSize = Terraingen.Combat.Attachments.ThornsWeaponEffectiveStats.Resolve( wdef, combatId, stack ).ClipSize;
			}
		}

		return dto;
	}

	static ThornsPersistentFurnitureContainerDto HostCaptureFurnitureDto( int furnitureId, ContainerRecord record )
	{
		var dto = new ThornsPersistentFurnitureContainerDto
		{
			FurnitureId = furnitureId,
			LootTable = record.LootTable,
			LootSeed = record.LootSeed,
			HasRolledLoot = record.HasRolledLoot,
			EmptySinceUtc = record.EmptySince,
			Slots = new List<ThornsPersistentItemStackDto>()
		};

		for ( var i = 0; i < record.Slots.Length; i++ )
		{
			var stack = record.Slots[i];
			if ( stack.IsEmpty )
				continue;

			dto.Slots.Add( new ThornsPersistentItemStackDto
			{
				SlotIndex = i,
				ItemId = stack.ItemId,
				Count = stack.Count,
				ItemTier = stack.ItemTier,
				StatRoll = stack.StatRoll,
				HasDurability = stack.HasDurability,
				Durability = stack.Durability
			} );
		}

		return dto;
	}

	public sealed class ContainerRecord
	{
		public string Key = "";
		public string Title = "Container";
		public int SlotCount = DefaultLootSlotCount;
		public ThornsItemStack[] Slots = new ThornsItemStack[DefaultLootSlotCount];
		public string LootTable = "home_clutter";
		public int LootSeed;
		public bool HasRolledLoot;
		public double EmptySince = -1;
		public bool CanRefill;
		public ThornsPlacedStructureStorage Storage;

		public bool IsEmpty()
		{
			if ( Storage is not null )
			{
				for ( var i = 0; i < ThornsPlacedStructureStorage.SlotCount; i++ )
				{
					if ( !Storage.GetSlot( i ).IsEmpty )
						return false;
				}

				return true;
			}

			for ( var i = 0; i < Slots.Length; i++ )
			{
				if ( !Slots[i].IsEmpty )
					return false;
			}

			return true;
		}
	}
}
