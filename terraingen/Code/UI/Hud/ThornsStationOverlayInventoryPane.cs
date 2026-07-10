namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

/// <summary>Shared inventory + hotbar pane for station overlays (campfire, workbench).</summary>
public sealed class ThornsStationOverlayInventoryPane
{
	public Panel Root { get; }
	readonly List<ThornsItemSlot> _inventorySlots = new();
	readonly List<ThornsItemSlot> _hotbarSlots = new();

	public ThornsStationOverlayInventoryPane( Panel parent, Action refresh )
	{
		Root = ThornsTheme.CreateStationColumn( parent, "station-overlay-inventory-column world-container-column" );
		Root.Style.AlignItems = Align.Center;

		ThornsTheme.CreateSectionHeader( Root, "YOUR INVENTORY" );
		var grid = ThornsUiFactory.AddPanel( Root, "thorns-item-grid inventory-grid-5" );
		grid.Style.JustifyContent = Justify.Center;
		grid.Style.AlignSelf = Align.Center;

		ThornsTheme.CreateSectionHeader( Root, "QUICKBAR" );
		var hotbar = ThornsUiFactory.AddPanel( Root, "thorns-hotbar inventory-hotbar world-container-hotbar" );
		hotbar.Style.FlexDirection = FlexDirection.Row;
		hotbar.Style.FlexWrap = Wrap.NoWrap;
		hotbar.Style.FlexShrink = 0;
		hotbar.Style.AlignSelf = Align.Stretch;
		hotbar.Style.JustifyContent = Justify.Center;
		hotbar.Style.MarginTop = Length.Pixels( 8 );

		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
			_inventorySlots.Add( new ThornsItemSlot( grid, ThornsContainerKind.Inventory, i, refresh, onSelected: null, worldContainerLayout: true ) );

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
			_hotbarSlots.Add( new ThornsItemSlot( hotbar, ThornsContainerKind.Hotbar, i, refresh, onSelected: null, isHotbar: true, worldContainerLayout: true ) );
	}

	public void RefreshSlots()
	{
		foreach ( var slot in _inventorySlots )
			slot.Refresh();

		foreach ( var slot in _hotbarSlots )
			slot.Refresh();
	}
}
