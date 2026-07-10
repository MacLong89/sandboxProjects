#nullable disable

namespace Sandbox;

/// <summary>
/// Placed <c>storage_chest</c> structure — host-only slot grid replicated to the owner via <see cref="ThornsInventory.ClientReceiveStorageChestSnapshot"/>.
/// </summary>
[Title( "Thorns — Storage chest" )]
[Category( "Thorns/Building" )]
[Icon( "inventory" )]
public sealed class ThornsStorageChest : Component
{
	public const int SlotCount = 24;
	public const float InteractionRange = ThornsBuildingVisuals.PlaceableInteractionUseRange;

	public static readonly Dictionary<Guid, ThornsStorageChest> ActiveByStructureId = new();

	readonly ThornsInventorySlot[] _slots = new ThornsInventorySlot[SlotCount];

	ThornsPlacedStructure _placed;

	public Guid StructureInstanceId => _placed.IsValid() ? _placed.InstanceId : Guid.Empty;

	protected override void OnAwake()
	{
		_placed = Components.Get<ThornsPlacedStructure>();
		if ( _placed.IsValid() && _placed.StructureDefId == "storage_chest" )
			ActiveByStructureId[_placed.InstanceId] = this;
	}

	protected override void OnStart()
	{
		// Network replicas may miss host MaterialOverride — match <see cref="ThornsLootCrate"/> dev box + playerchest.
		if ( _placed.IsValid() && _placed.StructureDefId == "storage_chest" )
			ThornsBuildingVisuals.TryApplyStorageChestFromStructureRoot( GameObject );
	}

	protected override void OnDestroy()
	{
		var id = StructureInstanceId;
		if ( id != Guid.Empty )
			ActiveByStructureId.Remove( id );
	}

	public static bool TryGetForStructure( Guid instanceId, out ThornsStorageChest chest ) =>
		ActiveByStructureId.TryGetValue( instanceId, out chest ) && chest.IsValid();

	internal void HostPushSnapshotToOwner( ThornsInventory ownerInv )
	{
		if ( !Networking.IsHost || ownerInv is null || !ownerInv.IsValid() )
			return;

		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		ownerInv.ClientReceiveStorageChestSnapshot( StructureInstanceId.ToString( "D" ), payload );
	}

	/// <summary>Shift-click style transfer: merge into a compatible stack if possible, otherwise first empty slot on the other side.</summary>
	internal bool HostTryQuickTransfer( bool fromChest, int fromIdx, ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( fromChest )
		{
			if ( !IsChestIndex( fromIdx ) )
				return false;

			ref var src = ref _slots[fromIdx];
			if ( src.IsEmpty )
				return false;

			var toIdx = HostFindQuickDepositPlayerSlot( src, playerInv );
			if ( toIdx < 0 )
				return false;

			return HostApplyTransfer( true, fromIdx, false, toIdx, playerInv );
		}

		if ( !playerInv.HostIsValidInventorySlot( fromIdx ) )
			return false;

		ref var psrc = ref playerInv.HostGetSlotRef( fromIdx );
		if ( psrc.IsEmpty )
			return false;

		var chestTo = HostFindQuickDepositChestSlot( psrc );
		if ( chestTo < 0 )
			return false;

		return HostApplyTransfer( false, fromIdx, true, chestTo, playerInv );
	}

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

	int HostFindQuickDepositChestSlot( ThornsInventorySlot src )
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

	internal bool HostApplyTransfer(
		bool fromChest,
		int fromIdx,
		bool toChest,
		int toIdx,
		ThornsInventory playerInv )
	{
		if ( !Networking.IsHost || playerInv is null || !playerInv.IsValid() )
			return false;

		if ( fromChest == toChest )
		{
			if ( !fromChest )
				return false;
			if ( !IsChestIndex( fromIdx ) || !IsChestIndex( toIdx ) || fromIdx == toIdx )
				return false;

			ref var a = ref _slots[fromIdx];
			ref var b = ref _slots[toIdx];
			SwapSlots( ref a, ref b );
			ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
			HostPushSnapshotToOwner( playerInv );
			return true;
		}

		if ( fromChest )
		{
			if ( !IsChestIndex( fromIdx ) || !playerInv.HostIsValidInventorySlot( toIdx ) )
				return false;

			ref var c = ref _slots[fromIdx];
			ref var p = ref playerInv.HostGetSlotRef( toIdx );
			if ( !TryMoveOrMergeOnto( ref c, ref p ) )
				SwapSlots( ref c, ref p );
		}
		else
		{
			if ( !playerInv.HostIsValidInventorySlot( fromIdx ) || !IsChestIndex( toIdx ) )
				return false;

			ref var p = ref playerInv.HostGetSlotRef( fromIdx );
			ref var c = ref _slots[toIdx];
			if ( !TryMoveOrMergeOnto( ref p, ref c ) )
				SwapSlots( ref p, ref c );
		}

		playerInv.HostPushInventorySnapshotToOwner();
		ThornsWorldPersistence.HostNotifyWorldStructuresDirty();
		return true;
	}

	static bool IsChestIndex( int i ) => i >= 0 && i < SlotCount;

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

	public static bool HostValidatePlayerUseRange( GameObject pawnRoot, ThornsStorageChest chest, float range )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || chest is null || !chest.IsValid() )
			return false;

		var d = (pawnRoot.WorldPosition - chest.GameObject.WorldPosition).Length;
		return d <= range;
	}

	/// <summary>Host: distance plus view toward the chest (matches client E-press selection).</summary>
	public static bool HostValidatePlayerUseAllowed( GameObject pawnRoot, ThornsStorageChest chest, float range )
	{
		if ( !HostValidatePlayerUseRange( pawnRoot, chest, range ) )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( pawnRoot, chest.GameObject, range );
	}

	internal ThornsInventorySlotNet[] HostSnapshotSlotsForPersistence()
	{
		var payload = new ThornsInventorySlotNet[SlotCount];
		for ( var i = 0; i < SlotCount; i++ )
			payload[i] = ThornsInventory.ToNet( _slots[i] );

		return payload;
	}

	internal void HostRestoreSlotsFromPersistence( ThornsInventorySlotNet[] src )
	{
		if ( !Networking.IsHost || src is null )
			return;

		for ( var i = 0; i < SlotCount; i++ )
			_slots[i] = i < src.Length ? ThornsInventory.SlotFromNet( src[i] ) : ThornsInventorySlot.Empty;
	}
}
