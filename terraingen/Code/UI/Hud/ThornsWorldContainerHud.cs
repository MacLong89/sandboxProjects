namespace Terraingen.UI.Hud;



using Sandbox.UI;

using Terraingen.GameData;

using Terraingen.Player;

using Terraingen.UI;

using Terraingen.UI.Components;

using Terraingen.Core;

using Terraingen.UI.Core;

using Terraingen.World;



/// <summary>Dual-pane loot UI — player inventory + open world container with drag-drop.</summary>

public sealed class ThornsWorldContainerHud

{

	readonly Panel _backdrop;

	readonly Panel _root;

	readonly Panel _playerGrid;

	readonly Panel _playerHotbar;

	readonly Panel _containerGrid;

	readonly Label _title;

	readonly Label _hint;

	readonly List<ThornsItemSlot> _playerSlots = new();

	readonly List<ThornsItemSlot> _playerHotbarSlots = new();

	readonly List<ThornsItemSlot> _containerSlots = new();



	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;



	public Panel Backdrop => _backdrop;



	Action<UiRevisionChannel, int> _onRevision;

	double _nextSlotPoll;

	string _lastContainerSignature = "";



	public ThornsWorldContainerHud( Panel parent )

	{

		(_backdrop, _root) = ThornsMenuChrome.CreateOverlayShell( parent, "world-container-hud", ThornsUiMetrics.WorldContainerMaxWidthPx );

		_backdrop.AddClass( "world-container-backdrop" );

		_backdrop.Style.Display = DisplayMode.None;

		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.InventoryBuild );



		ThornsTheme.CreateStationOverlayHeader(

			_root,

			out _title,

			"CONTAINER",

			() => ThornsPlayerGameplay.Local?.RequestCloseWorldContainer(),

			"world-container-title" );



		_hint = ThornsUiFactory.AddPassiveLabel(

			_root,

			"Shift-click transfer · ESC close",

			"thorns-muted world-container-hint thorns-station-hint" );

		_hint.Style.MarginBottom = Length.Pixels( 12 );



		var body = ThornsUiFactory.AddPanel( _root, "world-container-body thorns-station-body" );

		body.Style.FlexDirection = FlexDirection.Row;

		body.Style.FlexGrow = 1;

		body.Style.MinHeight = Length.Pixels( 0 );



		var left = ThornsTheme.CreateStationColumn( body, "world-container-column" );

		left.Style.AlignItems = Align.Center;

		ThornsTheme.CreateSectionHeader( left, "YOUR INVENTORY" );

		_playerGrid = ThornsUiFactory.AddPanel( left, "thorns-item-grid inventory-grid-5" );

		_playerGrid.Style.JustifyContent = Justify.Center;

		_playerGrid.Style.AlignSelf = Align.Center;



		ThornsTheme.CreateSectionHeader( left, "QUICKBAR" );

		_playerHotbar = ThornsUiFactory.AddPanel( left, "thorns-hotbar inventory-hotbar world-container-hotbar" );

		_playerHotbar.Style.FlexDirection = FlexDirection.Row;

		_playerHotbar.Style.FlexWrap = Wrap.NoWrap;

		_playerHotbar.Style.FlexShrink = 0;

		_playerHotbar.Style.AlignSelf = Align.Stretch;

		_playerHotbar.Style.JustifyContent = Justify.Center;

		_playerHotbar.Style.MarginTop = Length.Pixels( 8 );



		ThornsTheme.CreateWoodColumnDivider( body );



		var right = ThornsTheme.CreateStationColumn( body, "world-container-column" );

		right.Style.AlignItems = Align.Center;

		ThornsTheme.CreateSectionHeader( right, "STORAGE" );

		_containerGrid = ThornsUiFactory.AddPanel( right, "thorns-item-grid inventory-grid-4" );

		_containerGrid.Style.JustifyContent = Justify.Center;

		_containerGrid.Style.AlignSelf = Align.Center;



		_backdrop.AddEventListener( "onmouseup", OnBackdropMouseUp );

		_root.AddEventListener( "onmouseup", OnContainerMouseUp );



		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )

			_playerSlots.Add( new ThornsItemSlot( _playerGrid, ThornsContainerKind.Inventory, i, Refresh, onSelected: null, worldContainerLayout: true ) );



		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )

			_playerHotbarSlots.Add( new ThornsItemSlot( _playerHotbar, ThornsContainerKind.Hotbar, i, Refresh, onSelected: null, isHotbar: true, worldContainerLayout: true ) );



		_onRevision = OnRevision;

		UiRevisionBus.MenuRevisionChanged += _onRevision;

		Refresh();

	}



	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;



	void OnRevision( UiRevisionChannel channel, int _ )

	{

		if ( channel is UiRevisionChannel.Inventory or UiRevisionChannel.WorldContainer )

		{

			_nextSlotPoll = 0;

			Refresh();

		}

	}



	public void Refresh()

	{

		if ( !_backdrop.IsValid() )

			return;



		var external = ThornsUiClientState.Snapshot.ExternalContainer;

		var open = external?.IsOpen == true && !string.IsNullOrWhiteSpace( external.ContainerKey );

		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;



		if ( !open )

		{

			_lastContainerSignature = "";

			return;

		}



		_title.Text = string.IsNullOrWhiteSpace( external.Title ) ? "CONTAINER" : external.Title.ToUpper();

		if ( external.Slots is null or { Count: 0 } && external.RefillSecondsRemaining > 0.5f )

		{

			var mins = (int)(external.RefillSecondsRemaining / 60f);

			var secs = (int)(external.RefillSecondsRemaining % 60f);

			_hint.Text = $"Empty — refills in {mins}:{secs:00}";

		}

		else

			_hint.Text = "Shift-click transfer · ESC close";



		var slotCount = external.SlotCount > 0 ? external.SlotCount : ThornsWorldLootContainerService.DefaultLootSlotCount;

		var signature = $"{external.ContainerKey}|{slotCount}";

		var structureChanged = !string.Equals( signature, _lastContainerSignature, StringComparison.Ordinal );

		if ( structureChanged )

		{

			_lastContainerSignature = signature;

			ApplyContainerGridColumns( slotCount );

			RebuildContainerSlots( slotCount );

			MaybeShowShiftClickHint();

		}



		if ( !structureChanged && Time.Now < _nextSlotPoll )

			return;



		_nextSlotPoll = Time.Now + ThornsHudTickRates.WorldContainerSlotPollSeconds;



		foreach ( var slot in _playerSlots )

			slot.Refresh();



		foreach ( var slot in _playerHotbarSlots )

			slot.Refresh();



		foreach ( var slot in _containerSlots )

			slot.Refresh();

	}



	static void OnBackdropMouseUp( PanelEvent e )

	{

		if ( e.Target is Panel target && target.HasClass( "world-container-backdrop" ) )

			ThornsPlayerGameplay.Local?.RequestCloseWorldContainer();

	}



	static void OnContainerMouseUp( PanelEvent e )

	{

		_ = e;

		if ( !ThornsDragState.IsDragging )

			return;



		ThornsAttachmentInspectSlot.RefreshDropTarget();
		if ( ThornsAttachmentInspectSlot.TryCompleteHoveredDrop() )
			return;

		if ( ThornsDragState.PointerMoved && ThornsItemSlot.TryCompleteHoveredDrop() )

			return;



		ThornsDragState.Clear();

		ThornsItemSlot.ClearDropTarget();

	}



	void MaybeShowShiftClickHint()

	{

		if ( ThornsLocalSettings.Current.ContainerShiftHintSeen )

			return;



		ThornsLocalSettings.Current.ContainerShiftHintSeen = true;

		ThornsLocalSettings.Save();

		ThornsNotificationBus.Push( "Tip: Shift-click items to transfer them between your inventory, hotbar, and this container.", "info", 6f );

	}



	void ApplyContainerGridColumns( int slotCount )

	{

		var columns = slotCount > 12 ? 6 : 4;

		_containerGrid.SetClass( "inventory-grid-4", columns == 4 );

		_containerGrid.SetClass( "inventory-grid-6", columns == 6 );

	}



	void RebuildContainerSlots( int slotCount )

	{

		if ( _containerSlots.Count == slotCount )

			return;



		_containerGrid.DeleteChildren( true );

		_containerSlots.Clear();



		for ( var i = 0; i < slotCount; i++ )

			_containerSlots.Add( new ThornsItemSlot( _containerGrid, ThornsContainerKind.WorldLoot, i, Refresh, onSelected: null, worldContainerLayout: true ) );

	}

}


