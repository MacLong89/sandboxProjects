using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiInventoryTabBody : Panel
{
	public readonly ThornsUiGridSlot[] BackpackSlots;
	public readonly ThornsUiArmorEquipSlot[] ArmorSlots;
	public readonly ThornsUiGridSlot[] MenuToolbarSlots;
	/// <summary>Scroll host for backpack rows — drag locks overflow so LMB drag does not scroll the grid.</summary>
	public readonly Panel BackpackGridWrap;
	/// <summary>Single column wrapping grid + armor + menu toolbar — one pointer subtree for more predictable hit-testing.</summary>
	public readonly Panel InventoryDnDStack;
	public readonly Panel ContextHost;
	public readonly Panel ContextDetailsBody;
	public readonly Label CraftingTierLabel;
	public readonly Panel CraftingFilterHost;
	public readonly Panel CraftingScrollHost;

	public ThornsUiInventoryTabBody()
	{
		AddClass( "thorns-tab-inventory" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );

		var shell = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-tab-inventory-shell" );
		shell.Style.FlexDirection = FlexDirection.Row;
		shell.Style.FlexGrow = 1;
		shell.Style.FlexShrink = 1;
		shell.Style.MinHeight = 0;
		shell.Style.Width = Length.Fraction( 1f );

		var left = ThornsUiPanelAdd.AddChildPanel(shell,  "thorns-tab-inventory-left" );
		left.Style.PointerEvents = PointerEvents.All;

		var head = left.AddChild( new Label( "INVENTORY", "thorns-tab-section-title" ) );
		head.Style.PointerEvents = PointerEvents.None;

		var dndStack = ThornsUiPanelAdd.AddChildPanel(left,  "thorns-inv-dnd-stack" );
		InventoryDnDStack = dndStack;
		dndStack.Style.FlexDirection = FlexDirection.Column;
		dndStack.Style.FlexGrow = 1;
		dndStack.Style.FlexShrink = 1;
		dndStack.Style.MinHeight = 0;
		dndStack.Style.Width = Length.Fraction( 1f );
		dndStack.Style.PointerEvents = PointerEvents.All;

		var gridWrap = ThornsUiPanelAdd.AddChildPanel(dndStack,  "thorns-inv-grid-wrap" );
		BackpackGridWrap = gridWrap;
		// Wheel still scrolls; LMB+drag must not pan the whole grid during item DnD.
		gridWrap.CanDragScroll = false;
		dndStack.CanDragScroll = false;
		const int cols = 6;
		var packRows = (ThornsInventory.BackpackSlotCount + cols - 1) / cols;
		BackpackSlots = new ThornsUiGridSlot[ThornsInventory.BackpackSlotCount];
		for ( var r = 0; r < packRows; r++ )
		{
			var row = ThornsUiPanelAdd.AddChildPanel(gridWrap,  "thorns-inv-grid-row" );
			for ( var c = 0; c < cols; c++ )
			{
				var flat = r * cols + c;
				if ( flat >= ThornsInventory.BackpackSlotCount )
					break;

				var invSlotIndex = ThornsInventory.HotbarSlotCount + flat;
				var slot = row.AddChild( new ThornsUiGridSlot( invSlotIndex ) );
				BackpackSlots[flat] = slot;
			}
		}

		var armorBlock = ThornsUiPanelAdd.AddChildPanel(dndStack,  "thorns-tab-inventory-armor-block" );
		armorBlock.AddChild( new Label( "ARMOR", "thorns-tab-section-title" ) ).Style.PointerEvents =
			PointerEvents.None;
		var armorRow = ThornsUiPanelAdd.AddChildPanel(armorBlock,  "thorns-armor-equip-row" );
		var armorTitles = new[] { "Head", "Chest", "Legs" };
		ArmorSlots = new ThornsUiArmorEquipSlot[3];
		for ( var ai = 0; ai < 3; ai++ )
			ArmorSlots[ai] = armorRow.AddChild( new ThornsUiArmorEquipSlot( ai, armorTitles[ai] ) );

		var toolbarBlock = ThornsUiPanelAdd.AddChildPanel(dndStack,  "thorns-tab-inventory-toolbar-block" );
		toolbarBlock.AddChild( new Label( "TOOLBAR", "thorns-tab-section-title" ) ).Style.PointerEvents =
			PointerEvents.None;
		var toolbarRow = ThornsUiPanelAdd.AddChildPanel(toolbarBlock,  "thorns-inv-menu-toolbar-row" );
		MenuToolbarSlots = new ThornsUiGridSlot[ThornsInventory.HotbarSlotCount];
		for ( var ti = 0; ti < ThornsInventory.HotbarSlotCount; ti++ )
			MenuToolbarSlots[ti] = toolbarRow.AddChild( new ThornsUiGridSlot( ti, toolbar: true ) );

		ContextHost = ThornsUiPanelAdd.AddChildPanel(shell,  "thorns-tab-inventory-context" );
		var ctxTitle = ContextHost.AddChild( new Label( "INSPECT", "thorns-tab-section-title" ) );
		ctxTitle.Style.PointerEvents = PointerEvents.None;
		ContextDetailsBody = ThornsUiPanelAdd.AddChildPanel(ContextHost,  "thorns-tab-context-body" );
		ContextDetailsBody.CanDragScroll = false;
		ContextDetailsBody.AddChild( new Label(
			"No item selected.\n\nSuggested: craft bandages when you have cloth.",
			"thorns-tab-context-placeholder" ) );

		var craftingCol = ThornsUiPanelAdd.AddChildPanel(shell,  "thorns-tab-inventory-crafting" );
		var craftHead = craftingCol.AddChild( new Label( "CRAFTING", "thorns-tab-section-title" ) );
		craftHead.Style.PointerEvents = PointerEvents.None;
		CraftingTierLabel = craftingCol.AddChild( new Label(
			"",
			"thorns-inv-craft-tier" ) );
		CraftingTierLabel.Style.PointerEvents = PointerEvents.None;

		CraftingFilterHost = ThornsUiPanelAdd.AddChildPanel(craftingCol,  "thorns-inv-craft-filters" );
		CraftingFilterHost.Style.FlexDirection = FlexDirection.Row;
		CraftingFilterHost.Style.FlexShrink = 0;
		CraftingFilterHost.Style.PointerEvents = PointerEvents.All;

		CraftingScrollHost = ThornsUiPanelAdd.AddChildPanel(craftingCol,  "thorns-inv-craft-scroll" );
		CraftingScrollHost.Style.FlexDirection = FlexDirection.Column;
		CraftingScrollHost.Style.FlexGrow = 1;
		CraftingScrollHost.Style.MinHeight = 0;
		CraftingScrollHost.Style.Overflow = OverflowMode.Scroll;
		CraftingScrollHost.Style.PointerEvents = PointerEvents.All;
		CraftingScrollHost.CanDragScroll = false;
	}
}

