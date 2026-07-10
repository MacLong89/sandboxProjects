namespace Sandbox;

/// <summary>
/// Single choke point for <b>owner-client</b> inventory drag/drop intents (TAB shell, storage overlay, legacy HUD).
/// Mirrors are non-authoritative: only decide swap vs merge vs equip path; host still validates via existing RPCs.
/// </summary>
public static class ThornsInventoryClientTransfer
{
	static void LogGrid( string op, int from, int to, string detail = "" )
	{
		Log.Info(
			string.IsNullOrEmpty( detail )
				? $"[Thorns][InvTransfer] {op} inventory slot {from} -> {to}"
				: $"[Thorns][InvTransfer] {op} inventory slot {from} -> {to} ({detail})" );
	}

	/// <summary>Hotbar, backpack rows, menu toolbar — same flat indices as <see cref="ThornsInventory"/>.</summary>
	public static void SubmitMoveOrSwapInventorySlots( ThornsInventory inv, int fromSlot, int toSlot )
	{
		if ( !inv.IsValid() )
			return;

		if ( fromSlot == toSlot )
			return;

		var hasFrom = inv.TryGetClientMirrorSlot( fromSlot, out var fromNet );
		var hasTo = inv.TryGetClientMirrorSlot( toSlot, out var toNet );
		var qty = hasFrom && fromNet.Quantity > 0 ? fromNet.Quantity : 1;

		if ( hasFrom && hasTo && fromNet.Quantity > 0 && toNet.Quantity > 0 )
		{
			LogGrid( "swap", fromSlot, toSlot );
			inv.RequestSwapSlots( fromSlot, toSlot );
		}
		else
		{
			LogGrid( "move_full_stack", fromSlot, toSlot, $"qty={qty}" );
			inv.RequestMoveItem( fromSlot, toSlot, qty );
		}
	}

	/// <summary>Dragging an armor equip cell onto an inventory/hotbar cell.</summary>
	public static void SubmitUnequipArmorToInventorySlot( ThornsArmorEquipment armor, int armorSlotIndex,
		int inventorySlotIndex )
	{
		if ( !armor.IsValid() )
			return;

		Log.Info(
			$"[Thorns][InvTransfer] unequip armor slot {armorSlotIndex} -> inventory slot {inventorySlotIndex}" );
		armor.RequestUnequipArmorToInventorySlot( armorSlotIndex, inventorySlotIndex );
	}

	/// <summary>Dragging inventory item onto an armor equip cell (standard drop).</summary>
	public static void SubmitEquipArmorFromInventorySlot( ThornsArmorEquipment armor, int inventorySlotIndex )
	{
		if ( !armor.IsValid() )
			return;

		Log.Info( $"[Thorns][InvTransfer] equip armor from inventory slot {inventorySlotIndex}" );
		armor.RequestEquipArmorFromInventory( inventorySlotIndex );
	}

	/// <summary>Shift-modified drop onto a grid cell — armor equip shortcut.</summary>
	public static void SubmitShiftEquipArmorFromInventorySlot( ThornsArmorEquipment armor, int inventorySlotIndex )
	{
		Log.Info( $"[Thorns][InvTransfer] shift-equip armor from inventory slot {inventorySlotIndex}" );
		SubmitEquipArmorFromInventorySlot( armor, inventorySlotIndex );
	}

	/// <summary>Chest overlay: structured transfer (move/merge vs swap-within-chest handled on host).</summary>
	public static void SubmitStorageChestStructuredTransfer(
		ThornsInventory inv,
		string chestStructureIdGuidD,
		bool fromChest,
		int fromIdx,
		bool toChest,
		int toIdx )
	{
		if ( !inv.IsValid() )
			return;

		Log.Info(
			$"[Thorns][InvTransfer] chest transfer fromChest={fromChest} fromIdx={fromIdx} toChest={toChest} toIdx={toIdx}" );

		inv.RequestStorageChestTransfer( chestStructureIdGuidD, fromChest, fromIdx, toChest, toIdx );
	}

	public static void SubmitCampfireStructuredTransfer(
		ThornsInventory inv,
		string campfireStructureIdGuidD,
		bool fromCampfire,
		int fromIdx,
		bool toCampfire,
		int toIdx )
	{
		if ( !inv.IsValid() )
			return;

		Log.Info(
			$"[Thorns][InvTransfer] campfire transfer fromCampfire={fromCampfire} fromIdx={fromIdx} toCampfire={toCampfire} toIdx={toIdx}" );

		inv.RequestCampfireTransfer( campfireStructureIdGuidD, fromCampfire, fromIdx, toCampfire, toIdx );
	}

	public static void SubmitWorkbenchStructuredTransfer(
		ThornsInventory inv,
		string workbenchStructureIdGuidD,
		bool fromBench,
		int fromIdx,
		bool toBench,
		int toIdx )
	{
		if ( !inv.IsValid() )
			return;

		Log.Info(
			$"[Thorns][InvTransfer] workbench transfer fromBench={fromBench} fromIdx={fromIdx} toBench={toBench} toIdx={toIdx}" );

		inv.RequestWorkbenchTransfer( workbenchStructureIdGuidD, fromBench, fromIdx, toBench, toIdx );
	}
}
