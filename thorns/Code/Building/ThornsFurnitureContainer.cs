#nullable disable

using System;

namespace Sandbox;

/// <summary>
/// Interior furniture storage — proc world-gen loot (rerolls 5 min after emptied) or player-placed persistent chest.
/// Uses the same drag-drop UI as <see cref="ThornsStorageChest"/> via <see cref="ThornsInventory"/>.
/// </summary>
[Title( "Thorns — Furniture container" )]
[Category( "Thorns/Building" )]
[Icon( "inventory_2" )]
public sealed class ThornsFurnitureContainer : Component
{
	public const float InteractionRange = ThornsBuildingVisuals.PlaceableInteractionUseRange;

	public static readonly Dictionary<Guid, ThornsFurnitureContainer> ActiveByContainerId = new();

	[Sync( SyncFlags.FromHost )] public string ContainerIdSync { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public string StructureDefIdSync { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public bool IsProcLootSync { get; set; }

	public Guid ContainerId => SyncGuidParse( ContainerIdSync );

	public int SlotCount => IsProcLootSync ? ThornsLootCrate.LootGridSlots : ThornsStorageChest.SlotCount;

	readonly ThornsInventorySlot[] _slots = new ThornsInventorySlot[ThornsStorageChest.SlotCount];

	ThornsPlacedStructure _placed;
	bool _procLootRegenScheduled;
	double _procLootRegenAt;
	double _nextRegisterRetryTime;
	Guid _registeredContainerId;

	protected override void OnAwake()
	{
		_placed = Components.Get<ThornsPlacedStructure>();
	}

	protected override void OnStart() => RegisterActiveIfNeeded();

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( _registeredContainerId != Guid.Empty )
			return;

		if ( Time.Now < _nextRegisterRetryTime )
			return;

		_nextRegisterRetryTime = Time.Now + 0.5f;
		RegisterActiveIfNeeded();
	}

	protected override void OnDestroy()
	{
		if ( _registeredContainerId != Guid.Empty )
			ActiveByContainerId.Remove( _registeredContainerId );
	}

	void RegisterActiveIfNeeded()
	{
		var id = ContainerId;
		if ( id == Guid.Empty || id == _registeredContainerId )
			return;

		if ( _registeredContainerId != Guid.Empty )
			ActiveByContainerId.Remove( _registeredContainerId );

		_registeredContainerId = id;
		ActiveByContainerId[id] = this;
	}

	public static bool TryGet( Guid containerId, out ThornsFurnitureContainer container ) =>
		ActiveByContainerId.TryGetValue( containerId, out container ) && container.IsValid();

	public static bool TryGetForStructure( Guid structureInstanceId, out ThornsFurnitureContainer container ) =>
		TryGet( structureInstanceId, out container );

	/// <summary>Host: proc interior furniture — loot grid rerolls after empty.</summary>
	public void HostInitializeProcLoot( string structureDefId, ThornsProcBuildingType buildingType )
	{
		if ( !Networking.IsHost )
			return;

		IsProcLootSync = true;
		StructureDefIdSync = structureDefId ?? "";
		ContainerIdSync = Guid.NewGuid().ToString( "D" );
		_procLootRegenScheduled = false;
		HostRerollProcLoot( buildingType );
	}

	/// <summary>Host: player-placed cabinet / fridge — empty persistent grid.</summary>
	public void HostInitializePlayerStorage( ThornsPlacedStructure placed )
	{
		if ( !Networking.IsHost || placed is null || !placed.IsValid() )
			return;

		_placed = placed;
		IsProcLootSync = false;
		StructureDefIdSync = placed.StructureDefId ?? "";
		ContainerIdSync = placed.InstanceId.ToString( "D" );
		ClearSlots();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !IsProcLootSync )
			return;

		if ( IsProcLootEmpty() )
		{
			if ( !_procLootRegenScheduled )
			{
				_procLootRegenScheduled = true;
				_procLootRegenAt = Time.Now + ThornsFurnitureLootPolicy.ProcLootRerollSeconds;
			}
			else if ( Time.Now >= _procLootRegenAt )
			{
				_procLootRegenScheduled = false;
				var buildingType = ResolveProcBuildingType();
				HostRerollProcLoot( buildingType );
			}
		}
		else
			_procLootRegenScheduled = false;
	}

	ThornsProcBuildingType ResolveProcBuildingType()
	{
		var buildingType = ThornsProcBuildingType.House;
		var root = ThornsInteriorFurnitureProp.FindProcBuildingRoot( GameObject );
		if ( root.IsValid() )
		{
			var layout = ThornsProcBuildingLayoutHost.TryGet( root );
			if ( layout?.Identity is not null )
				buildingType = layout.Identity.Type;
		}

		return buildingType;
	}

	bool IsProcLootEmpty()
	{
		for ( var i = 0; i < SlotCount; i++ )
		{
			if ( !_slots[i].IsEmpty )
				return false;
		}

		return true;
	}

	void HostRerollProcLoot( ThornsProcBuildingType buildingType )
	{
		var rng = Random.Shared;
		var kind = ThornsFurnitureLootPolicy.PickProcLootKind( StructureDefIdSync, buildingType, rng );
		var grid = ThornsLootGenerator.GenerateLootGrid( kind, rng );
		ClearSlots();
		var n = Math.Min( grid.Length, ThornsLootCrate.LootGridSlots );
		for ( var i = 0; i < n; i++ )
			_slots[i] = grid[i];
	}

	void ClearSlots()
	{
		for ( var i = 0; i < _slots.Length; i++ )
			_slots[i] = ThornsInventorySlot.Empty;
	}

	internal void HostPushSnapshotToOwner( ThornsInventory ownerInv )
	{
		if ( !Networking.IsHost || ownerInv is null || !ownerInv.IsValid() )
			return;

		var payload = new ThornsInventorySlotNet[ThornsStorageChest.SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		ownerInv.ClientReceiveStorageChestSnapshot( ContainerId.ToString( "D" ), payload );
	}

	internal bool HostTryQuickTransfer( bool fromContainer, int fromIdx, ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( fromContainer )
		{
			if ( !IsContainerIndex( fromIdx ) )
				return false;

			ref var src = ref _slots[fromIdx];
			if ( src.IsEmpty )
				return false;

			var toIdx = HostFindQuickDepositPlayerSlot( src, playerInv );
			if ( toIdx < 0 )
				return false;

			return HostApplyTransfer( true, fromIdx, false, toIdx, playerInv );
		}

		if ( IsProcLootSync )
			return false;

		if ( !playerInv.HostIsValidInventorySlot( fromIdx ) )
			return false;

		ref var psrc = ref playerInv.HostGetSlotRef( fromIdx );
		if ( psrc.IsEmpty )
			return false;

		var chestTo = HostFindQuickDepositContainerSlot( psrc );
		if ( chestTo < 0 )
			return false;

		return HostApplyTransfer( false, fromIdx, true, chestTo, playerInv );
	}

	internal bool HostApplyTransfer(
		bool fromContainer,
		int fromIdx,
		bool toContainer,
		int toIdx,
		ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( IsProcLootSync && toContainer )
			return false;

		if ( fromContainer == toContainer )
		{
			if ( !fromContainer || IsProcLootSync )
				return false;

			if ( !IsContainerIndex( fromIdx ) || !IsContainerIndex( toIdx ) || fromIdx == toIdx )
				return false;

			ref var a = ref _slots[fromIdx];
			ref var b = ref _slots[toIdx];
			SwapSlots( ref a, ref b );
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
			HostPushSnapshotToOwner( playerInv );
			return true;
		}

		if ( fromContainer )
		{
			if ( !IsContainerIndex( fromIdx ) || !playerInv.HostIsValidInventorySlot( toIdx ) )
				return false;

			ref var c = ref _slots[fromIdx];
			ref var p = ref playerInv.HostGetSlotRef( toIdx );
			if ( !TryMoveOrMergeOnto( ref c, ref p ) )
				SwapSlots( ref c, ref p );
		}
		else
		{
			if ( !playerInv.HostIsValidInventorySlot( fromIdx ) || !IsContainerIndex( toIdx ) )
				return false;

			ref var p = ref playerInv.HostGetSlotRef( fromIdx );
			ref var c = ref _slots[toIdx];
			if ( !TryMoveOrMergeOnto( ref p, ref c ) )
				SwapSlots( ref p, ref c );
		}

		playerInv.HostPushInventorySnapshotToOwner();
		if ( !IsProcLootSync )
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();

		HostPushSnapshotToOwner( playerInv );
		return true;
	}

	bool IsContainerIndex( int i ) => i >= 0 && i < SlotCount;

	static int HostFindQuickDepositPlayerSlot( ThornsInventorySlot src, ThornsInventory inv )
	{
		if ( src.IsEmpty || !ThornsItemRegistry.TryGet( src.ItemId, out var def ) )
			return -1;

		for ( var i = 0; i < ThornsInventory.TotalSlots; i++ )
		{
			ref var p = ref inv.HostGetSlotRef( i );
			if ( p.IsEmpty )
				continue;

			if ( src.ItemId != p.ItemId || !src.EqualsStackIdentity( p ) )
				continue;

			var space = def.MaxStack - p.Quantity;
			if ( space > 0 )
				return i;
		}

		for ( var i = 0; i < ThornsInventory.TotalSlots; i++ )
		{
			ref var p = ref inv.HostGetSlotRef( i );
			if ( p.IsEmpty )
				return i;
		}

		return -1;
	}

	int HostFindQuickDepositContainerSlot( ThornsInventorySlot src )
	{
		if ( src.IsEmpty || !ThornsItemRegistry.TryGet( src.ItemId, out var def ) )
			return -1;

		for ( var i = 0; i < SlotCount; i++ )
		{
			ref var c = ref _slots[i];
			if ( c.IsEmpty )
				continue;

			if ( src.ItemId != c.ItemId || !src.EqualsStackIdentity( c ) )
				continue;

			var space = def.MaxStack - c.Quantity;
			if ( space > 0 )
				return i;
		}

		for ( var i = 0; i < SlotCount; i++ )
		{
			ref var c = ref _slots[i];
			if ( c.IsEmpty )
				return i;
		}

		return -1;
	}

	static void SwapSlots( ref ThornsInventorySlot a, ref ThornsInventorySlot b ) =>
		(a, b) = (b, a);

	static bool TryMoveOrMergeOnto( ref ThornsInventorySlot from, ref ThornsInventorySlot onto )
	{
		if ( from.IsEmpty )
			return false;

		if ( onto.IsEmpty )
		{
			onto = from;
			from = ThornsInventorySlot.Empty;
			return true;
		}

		if ( !ThornsItemRegistry.TryGet( from.ItemId, out var def ) )
			return false;

		if ( from.ItemId != onto.ItemId || !from.EqualsStackIdentity( onto ) )
			return false;

		var space = def.MaxStack - onto.Quantity;
		if ( space <= 0 )
			return false;

		var put = Math.Min( from.Quantity, space );
		onto.Quantity += put;
		from.Quantity -= put;
		if ( from.Quantity <= 0 )
			from = ThornsInventorySlot.Empty;

		return true;
	}

	public static bool HostValidatePlayerUseAllowed( GameObject pawnRoot, ThornsFurnitureContainer container, float range )
	{
		if ( !HostValidatePlayerUseRange( pawnRoot, container, range ) )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, container.GameObject, range );
	}

	public static bool HostValidatePlayerUseRange( GameObject pawnRoot, ThornsFurnitureContainer container, float range )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || container is null || !container.IsValid() )
			return false;

		var d = (pawnRoot.WorldPosition - container.GameObject.WorldPosition).Length;
		return d <= range;
	}

	internal ThornsInventorySlotNet[] HostSnapshotSlotsForPersistence()
	{
		if ( IsProcLootSync )
			return Array.Empty<ThornsInventorySlotNet>();

		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		return payload;
	}

	internal void HostRestoreSlotsFromPersistence( ThornsInventorySlotNet[] src )
	{
		if ( !Networking.IsHost || IsProcLootSync || src is null )
			return;

		for ( var i = 0; i < SlotCount; i++ )
			_slots[i] = i < src.Length ? ThornsInventory.SlotFromNet( src[i] ) : ThornsInventorySlot.Empty;
	}

	static Guid SyncGuidParse( string s ) =>
		Guid.TryParse( s, out var g ) ? g : Guid.Empty;
}
