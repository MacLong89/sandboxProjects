namespace Terraingen.Player;

using Terraingen.GameData;

/// <summary>Shift-click / quick deposit between containers — merge first, then first empty slot.</summary>
public static class ThornsInventoryQuickTransfer
{
	public enum PlayerStorageSearchMode
	{
		HotbarThenInventory,
		HotbarOnly,
		InventoryOnly
	}

	public delegate ThornsItemStack SlotReader( int index );
	public delegate void SlotWriter( int index, ThornsItemStack stack );

	readonly record struct ContainerSearch( ThornsContainerKind Kind, int SlotCount );

	static readonly ContainerSearch[] HotbarThenInventorySearchOrder =
	[
		new( ThornsContainerKind.Hotbar, ThornsInventoryContainer.HotbarSlotCount ),
		new( ThornsContainerKind.Inventory, ThornsInventoryContainer.InventorySlotCount )
	];

	static readonly ContainerSearch[] HotbarOnlySearchOrder =
	[
		new( ThornsContainerKind.Hotbar, ThornsInventoryContainer.HotbarSlotCount )
	];

	static readonly ContainerSearch[] InventoryOnlySearchOrder =
	[
		new( ThornsContainerKind.Inventory, ThornsInventoryContainer.InventorySlotCount )
	];

	static ReadOnlySpan<ContainerSearch> GetSearchOrder( PlayerStorageSearchMode mode ) => mode switch
	{
		PlayerStorageSearchMode.HotbarOnly => HotbarOnlySearchOrder,
		PlayerStorageSearchMode.InventoryOnly => InventoryOnlySearchOrder,
		_ => HotbarThenInventorySearchOrder
	};

	static bool TryFindQuickDepositTarget(
		ThornsItemStack src,
		Func<ThornsContainerKind, int, ThornsItemStack> readSlot,
		ReadOnlySpan<ContainerSearch> searchOrder,
		out ThornsContainerKind targetKind,
		out int targetIndex,
		ThornsContainerKind excludeKind = default,
		int excludeIndex = -1,
		bool excludeSlot = false )
	{
		targetKind = default;
		targetIndex = -1;

		src = ThornsItemIdAliases.CanonicalizeStack( src );
		if ( src.IsEmpty )
			return false;

		var def = ThornsDefinitionRegistry.GetItem( src.ItemId );
		if ( def is null )
			return false;

		bool IsExcluded( ThornsContainerKind kind, int index ) =>
			excludeSlot && kind == excludeKind && index == excludeIndex;

		foreach ( var container in searchOrder )
		{
			for ( var i = 0; i < container.SlotCount; i++ )
			{
				if ( IsExcluded( container.Kind, i ) )
					continue;

				var slot = ThornsItemIdAliases.CanonicalizeStack( readSlot( container.Kind, i ) );
				if ( slot.IsEmpty )
					continue;

				if ( !string.Equals( slot.ItemId, src.ItemId, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( def.MaxStack - slot.Count > 0 )
				{
					targetKind = container.Kind;
					targetIndex = i;
					return true;
				}
			}
		}

		foreach ( var container in searchOrder )
		{
			for ( var i = 0; i < container.SlotCount; i++ )
			{
				if ( IsExcluded( container.Kind, i ) )
					continue;

				if ( readSlot( container.Kind, i ).IsEmpty )
				{
					targetKind = container.Kind;
					targetIndex = i;
					return true;
				}
			}
		}

		return false;
	}

	public static bool TryFindPlayerStorageDepositTarget(
		ThornsItemStack src,
		Func<ThornsContainerKind, int, ThornsItemStack> readSlot,
		out ThornsContainerKind targetKind,
		out int targetIndex,
		ThornsContainerKind excludeKind = default,
		int excludeIndex = -1,
		bool excludeSlot = false,
		PlayerStorageSearchMode searchMode = PlayerStorageSearchMode.HotbarThenInventory ) =>
		TryFindQuickDepositTarget(
			src,
			readSlot,
			GetSearchOrder( searchMode ),
			out targetKind,
			out targetIndex,
			excludeKind,
			excludeIndex,
			excludeSlot );

	public static void TryQuickTransferToPlayerStorage(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		Func<ThornsContainerKind, int, ThornsItemStack> readPlayerSlot,
		Action<ThornsContainerKind, int, ThornsItemStack> writePlayerSlot,
		PlayerStorageSearchMode searchMode = PlayerStorageSearchMode.HotbarThenInventory,
		ThornsContainerKind excludeKind = default,
		int excludeIndex = -1,
		bool excludeSlot = false ) =>
		TryQuickTransferStack(
			readFrom,
			writeFrom,
			fromIndex,
			readPlayerSlot,
			writePlayerSlot,
			GetSearchOrder( searchMode ),
			excludeKind,
			excludeIndex,
			excludeSlot );

	public static void TryQuickTransferStack(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		Func<ThornsContainerKind, int, ThornsItemStack> readTarget,
		Action<ThornsContainerKind, int, ThornsItemStack> writeTarget,
		PlayerStorageSearchMode searchMode ) =>
		TryQuickTransferStack(
			readFrom,
			writeFrom,
			fromIndex,
			readTarget,
			writeTarget,
			GetSearchOrder( searchMode ) );

	static void TryQuickTransferStack(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		Func<ThornsContainerKind, int, ThornsItemStack> readTarget,
		Action<ThornsContainerKind, int, ThornsItemStack> writeTarget,
		ReadOnlySpan<ContainerSearch> searchOrder,
		ThornsContainerKind excludeKind = default,
		int excludeIndex = -1,
		bool excludeSlot = false )
	{
		while ( true )
		{
			var stack = ThornsItemIdAliases.CanonicalizeStack( readFrom( fromIndex ) );
			if ( stack.IsEmpty )
				return;

			if ( !TryFindQuickDepositTarget(
				    stack,
				    readTarget,
				    searchOrder,
				    out var targetKind,
				    out var targetIndex,
				    excludeKind,
				    excludeIndex,
				    excludeSlot ) )
				return;

			if ( !TryMergeOrMoveToEmpty(
				    readFrom,
				    writeFrom,
				    fromIndex,
				    i => readTarget( targetKind, i ),
				    ( i, s ) => writeTarget( targetKind, i, s ),
				    targetIndex ) )
				return;
		}
	}

	public static void TryQuickTransferStack(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		SlotReader readTo,
		SlotWriter writeTo,
		int slotCount,
		Func<int, ThornsItemStack, bool> canAcceptSlot = null )
	{
		while ( true )
		{
			var stack = ThornsItemIdAliases.CanonicalizeStack( readFrom( fromIndex ) );
			if ( stack.IsEmpty )
				return;

			var targetIndex = FindQuickDepositIndex( stack, readTo, slotCount, canAcceptSlot );
			if ( targetIndex < 0 )
				return;

			if ( !TryMergeOrMoveToEmpty( readFrom, writeFrom, fromIndex, readTo, writeTo, targetIndex ) )
				return;
		}
	}

	public static bool TryMergeOrMoveToEmpty(
		SlotReader readFrom,
		SlotWriter writeFrom,
		int fromIndex,
		SlotReader readTo,
		SlotWriter writeTo,
		int toIndex,
		ThornsEquipSlot requiredEquipSlot = ThornsEquipSlot.None )
	{
		var from = ThornsItemIdAliases.CanonicalizeStack( readFrom( fromIndex ) );
		if ( from.IsEmpty )
			return false;

		var to = ThornsItemIdAliases.CanonicalizeStack( readTo( toIndex ) );
		var def = ThornsDefinitionRegistry.GetItem( from.ItemId );
		if ( def is null )
			return false;

		if ( requiredEquipSlot != ThornsEquipSlot.None && def.EquipSlot != requiredEquipSlot )
			return false;

		if ( !to.IsEmpty )
		{
			if ( !string.Equals( to.ItemId, from.ItemId, StringComparison.OrdinalIgnoreCase ) )
				return false;

			var space = def.MaxStack - to.Count;
			if ( space <= 0 )
				return false;

			var move = Math.Min( space, from.Count );
			writeTo( toIndex, CopyStackWithCount( to, to.Count + move ) );
			var left = from.Count - move;
			writeFrom( fromIndex, left > 0 ? CopyStackWithCount( from, left ) : ThornsItemStack.EmptyStack );
			return true;
		}

		writeTo( toIndex, from );
		writeFrom( fromIndex, ThornsItemStack.EmptyStack );
		return true;
	}

	public static int FindQuickDepositIndex( ThornsItemStack src, SlotReader readSlot, int slotCount )
		=> FindQuickDepositIndex( src, readSlot, slotCount, null );

	public static int FindQuickDepositIndex(
		ThornsItemStack src,
		SlotReader readSlot,
		int slotCount,
		Func<int, ThornsItemStack, bool> canAcceptSlot )
	{
		src = ThornsItemIdAliases.CanonicalizeStack( src );
		if ( src.IsEmpty )
			return -1;

		var def = ThornsDefinitionRegistry.GetItem( src.ItemId );

		for ( var i = 0; i < slotCount; i++ )
		{
			if ( canAcceptSlot is not null && !canAcceptSlot( i, src ) )
				continue;

			var slot = ThornsItemIdAliases.CanonicalizeStack( readSlot( i ) );
			if ( slot.IsEmpty || def is null )
				continue;

			if ( !string.Equals( slot.ItemId, src.ItemId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( def.MaxStack - slot.Count > 0 )
				return i;
		}

		for ( var i = 0; i < slotCount; i++ )
		{
			if ( canAcceptSlot is not null && !canAcceptSlot( i, src ) )
				continue;

			if ( readSlot( i ).IsEmpty )
				return i;
		}

		return -1;
	}

	static ThornsItemStack CopyStackWithCount( ThornsItemStack stack, int count ) =>
		ThornsInventoryWeaponState.CopyStackWithCount( stack, count );
}
