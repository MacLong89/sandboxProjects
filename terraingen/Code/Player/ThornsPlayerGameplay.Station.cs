namespace Terraingen.Player;

using Terraingen.GameData;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.World;

/// <summary>Drag-drop between player inventory and campfire/workbench station slots.</summary>
public sealed partial class ThornsPlayerGameplay
{
	readonly ThornsItemStack[] _campfireStation = new ThornsItemStack[ThornsCampfireStationSlots.SlotCount];
	readonly ThornsItemStack[] _workbenchStation = new ThornsItemStack[ThornsWorkbenchStationSlots.SlotCount];

	bool HostMoveUsesStation( ThornsMoveItemRequest req )
	{
		if ( req.FromContainer is ThornsContainerKind.CampfireStation or ThornsContainerKind.WorkbenchStation
		     || req.ToContainer is ThornsContainerKind.CampfireStation or ThornsContainerKind.WorkbenchStation )
			return true;

		if ( !HasOpenCampfire && !HasOpenWorkbench )
			return false;

		return req.Mode is ThornsMoveItemMode.QuickTransfer or ThornsMoveItemMode.DoubleClickTransfer
		       || req.ShiftHeld;
	}

	public void HostMoveItemWithStation( ThornsMoveItemRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
			return;

		MarkInventorySyncDirty();

		if ( req.Mode == ThornsMoveItemMode.QuickTransfer || req.ShiftHeld )
		{
			HostQuickTransferStation( req.FromContainer, req.FromIndex );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			PushCampfireToOwner();
			PushWorkbenchToOwner();
			HostPersistPlayerState();
			return;
		}

		if ( req.Mode == ThornsMoveItemMode.DoubleClickTransfer )
		{
			HostQuickTransferStation( req.FromContainer, req.FromIndex );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			PushCampfireToOwner();
			PushWorkbenchToOwner();
			HostPersistPlayerState();
			return;
		}

		var fromIsCampfire = req.FromContainer == ThornsContainerKind.CampfireStation;
		var toIsCampfire = req.ToContainer == ThornsContainerKind.CampfireStation;
		var fromIsWorkbench = req.FromContainer == ThornsContainerKind.WorkbenchStation;
		var toIsWorkbench = req.ToContainer == ThornsContainerKind.WorkbenchStation;

		if ( fromIsCampfire || toIsCampfire )
		{
			if ( !HasOpenCampfire || _campfire.SmeltBatchesRemaining > 0 )
				return;

			HostMoveWithStationArray( req, _campfireStation, ThornsContainerKind.CampfireStation, ThornsCampfireStationSlots.SlotCount );
		}
		else if ( fromIsWorkbench || toIsWorkbench )
		{
			if ( !HasOpenWorkbench || _workbench.RepairInProgress )
				return;

			HostMoveWithStationArray( req, _workbenchStation, ThornsContainerKind.WorkbenchStation, ThornsWorkbenchStationSlots.SlotCount );
		}
		else
		{
			HostQuickTransferStation( req.FromContainer, req.FromIndex );
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		PushCampfireToOwner();
		PushWorkbenchToOwner();
		HostPersistPlayerState();
	}

	void HostMoveWithStationArray(
		ThornsMoveItemRequest req,
		ThornsItemStack[] station,
		ThornsContainerKind stationKind,
		int slotCount )
	{
		var fromIsStation = req.FromContainer == stationKind;
		var toIsStation = req.ToContainer == stationKind;
		var fromIndex = fromIsStation ? req.FromIndex : NormalizeIndex( req.FromContainer, req.FromIndex );
		var toIndex = toIsStation ? req.ToIndex : NormalizeIndex( req.ToContainer, req.ToIndex );

		if ( fromIsStation && ( fromIndex < 0 || fromIndex >= slotCount ) )
			return;

		if ( toIsStation && ( toIndex < 0 || toIndex >= slotCount ) )
			return;

		if ( toIsStation && ThornsStationSlotRules.IsOutputSlot( stationKind, toIndex ) )
		{
			var incoming = fromIsStation ? station[fromIndex] : _inventory.GetSlot( req.FromContainer, fromIndex );
			if ( !incoming.IsEmpty )
				return;
		}

		if ( toIsStation )
		{
			var incoming = fromIsStation ? station[fromIndex] : _inventory.GetSlot( req.FromContainer, fromIndex );
			if ( !incoming.IsEmpty && !ThornsStationSlotRules.CanAccept( stationKind, toIndex, incoming.ItemId ) )
				return;
		}

		if ( req.Mode == ThornsMoveItemMode.SplitHalf )
		{
			var from = fromIsStation ? station[fromIndex] : _inventory.GetSlot( req.FromContainer, fromIndex );
			if ( from.IsEmpty )
				return;

			var half = from.Count / 2;
			if ( half <= 0 )
				return;

			var to = toIsStation ? station[toIndex] : _inventory.GetSlot( req.ToContainer, toIndex );
			if ( !to.IsEmpty )
				return;

			var piece = ThornsInventoryWeaponState.CopyStackWithCount( from, half );
			if ( toIsStation && !ThornsStationSlotRules.CanAccept( stationKind, toIndex, piece.ItemId ) )
				return;

			if ( toIsStation )
				station[toIndex] = piece;
			else
				_inventory.SetSlot( req.ToContainer, toIndex, piece );

			if ( fromIsStation )
				station[fromIndex] = ThornsInventoryWeaponState.CopyStackWithCount( from, from.Count - half );
			else
				_inventory.SetSlot( req.FromContainer, fromIndex, ThornsInventoryWeaponState.CopyStackWithCount( from, from.Count - half ) );

			return;
		}

		ThornsContainerItemMoves.TrySwapOrMerge(
			i => fromIsStation ? station[i] : _inventory.GetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, i ) ),
			( i, s ) =>
			{
				if ( fromIsStation )
					station[i] = s;
				else
					_inventory.SetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, i ), s );
			},
			fromIndex,
			i => toIsStation ? station[i] : _inventory.GetSlot( req.ToContainer, NormalizeIndex( req.ToContainer, i ) ),
			( i, s ) =>
			{
				if ( toIsStation )
				{
					if ( !s.IsEmpty && !ThornsStationSlotRules.CanAccept( stationKind, i, s.ItemId ) )
						return;

					station[i] = s;
				}
				else
				{
					_inventory.SetSlot( req.ToContainer, NormalizeIndex( req.ToContainer, i ), s );
				}
			},
			toIndex,
			toIsStation ? ThornsEquipSlot.None : MapEquipSlot( req.ToContainer ) );
	}

	void HostQuickTransferStation( ThornsContainerKind from, int fromIndex )
	{
		if ( from == ThornsContainerKind.CampfireStation )
		{
			if ( !HasOpenCampfire )
				return;

			ThornsInventoryQuickTransfer.TryQuickTransferToPlayerStorage(
				i => _campfireStation[i],
				( i, s ) => _campfireStation[i] = s,
				fromIndex,
				( container, slotIndex ) => _inventory.GetSlot( container, slotIndex ),
				( container, slotIndex, stack ) => _inventory.SetSlot( container, slotIndex, stack ) );
			return;
		}

		if ( from == ThornsContainerKind.WorkbenchStation )
		{
			if ( !HasOpenWorkbench )
				return;

			ThornsInventoryQuickTransfer.TryQuickTransferToPlayerStorage(
				i => _workbenchStation[i],
				( i, s ) => _workbenchStation[i] = s,
				fromIndex,
				( container, slotIndex ) => _inventory.GetSlot( container, slotIndex ),
				( container, slotIndex, stack ) => _inventory.SetSlot( container, slotIndex, stack ) );
			return;
		}

		if ( HasOpenCampfire && _campfire.SmeltBatchesRemaining <= 0 )
		{
			var idx = NormalizeIndex( from, fromIndex );
			var stack = _inventory.GetSlot( from, idx );
			if ( stack.IsEmpty )
				return;

			ThornsInventoryQuickTransfer.TryQuickTransferStack(
				i => _inventory.GetSlot( from, i ),
				( i, s ) => _inventory.SetSlot( from, i, s ),
				idx,
				i => _campfireStation[i],
				( i, s ) => _campfireStation[i] = s,
				ThornsCampfireStationSlots.SlotCount,
				( toIdx, incoming ) => ThornsStationSlotRules.CanAccept( ThornsContainerKind.CampfireStation, toIdx, incoming.ItemId ) );
			return;
		}

		if ( HasOpenWorkbench && !_workbench.RepairInProgress )
		{
			var idx = NormalizeIndex( from, fromIndex );
			var stack = _inventory.GetSlot( from, idx );
			if ( stack.IsEmpty )
				return;

			ThornsInventoryQuickTransfer.TryQuickTransferStack(
				i => _inventory.GetSlot( from, i ),
				( i, s ) => _inventory.SetSlot( from, i, s ),
				idx,
				i => _workbenchStation[i],
				( i, s ) => _workbenchStation[i] = s,
				ThornsWorkbenchStationSlots.SlotCount,
				( toIdx, incoming ) => !ThornsStationSlotRules.IsOutputSlot( ThornsContainerKind.WorkbenchStation, toIdx )
				                        && ThornsStationSlotRules.CanAccept( ThornsContainerKind.WorkbenchStation, toIdx, incoming.ItemId ) );
		}
	}

	void HostReturnCampfireStationToInventory()
	{
		HostReturnStationArrayToInventory( _campfireStation );
	}

	void HostReturnWorkbenchStationToInventory()
	{
		HostReturnStationArrayToInventory( _workbenchStation );
	}

	void HostReturnStationArrayToInventory( ThornsItemStack[] station )
	{
		for ( var i = 0; i < station.Length; i++ )
		{
			ref var stack = ref station[i];
			if ( stack.IsEmpty )
				continue;

			HostTryDepositStackAnywhere( stack );
			stack = ThornsItemStack.EmptyStack;
		}
	}

	bool HostTryDepositStackAnywhere( ThornsItemStack stack )
	{
		if ( stack.IsEmpty )
			return true;

		if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
		{
			ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId ) );
			ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref stack, def );
		}

		foreach ( var kind in new[] { ThornsContainerKind.Inventory, ThornsContainerKind.Hotbar } )
		{
			var max = kind == ThornsContainerKind.Inventory
				? ThornsInventoryContainer.InventorySlotCount
				: ThornsInventoryContainer.HotbarSlotCount;

			for ( var i = 0; i < max; i++ )
			{
				var existing = _inventory.GetSlot( kind, i );
				if ( !existing.IsEmpty )
					continue;

				_inventory.SetSlot( kind, i, stack );
				return true;
			}
		}

		return HostTryGrantItemStack( stack );
	}

	void HostDepositStackToStationSlot( ThornsItemStack[] station, int index, ThornsItemStack stack )
	{
		if ( stack.IsEmpty || index < 0 || index >= station.Length )
			return;

		ref var slot = ref station[index];
		if ( slot.IsEmpty )
		{
			slot = stack;
			return;
		}

		if ( string.Equals( slot.ItemId, stack.ItemId, StringComparison.OrdinalIgnoreCase )
		     && ThornsItemRegistry.TryGet( stack.ItemId, out var def )
		     && ThornsItemTier.StacksMatchForMerge( slot, stack, def ) )
		{
			slot.Count += stack.Count;
			return;
		}

		HostTryDepositStackAnywhere( stack );
	}
}
