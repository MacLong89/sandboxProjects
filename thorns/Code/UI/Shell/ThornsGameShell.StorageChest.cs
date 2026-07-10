#nullable disable

using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	public void ApplyStorageChestSnapshot( Guid structureInstanceId, ThornsInventorySlotNet[] slots )
	{
		CloseCampfireUi();
		CloseWorkbenchUi();
		Components.Get<ThornsPawnMovement>()?.StopLocalMovement();
		_storageChestStructureId = structureInstanceId;
		var need = slots is { Length: > 0 }
			? slots.Length
			: ThornsStorageChest.SlotCount;
		if ( slots is null || slots.Length != need )
		{
			_storageChestMirror = new ThornsInventorySlotNet[need];
			var n = Math.Min( need, slots?.Length ?? 0 );
			for ( var i = 0; i < n; i++ )
				_storageChestMirror[i] = slots![i];
		}
		else
		{
			_storageChestMirror = slots;
		}

		EnsureStorageChestUiBuilt();
		StorageChestUiOpen = true;
		if ( _storageChestLayer.IsValid )
			SetStorageChestLayerVisible( true );

		RefreshStorageChestGridFromMirror();
		RefreshStorageOverlayPlayerSlotsFromMirror();
	}

	public void CloseStorageChestUi()
	{
		if ( !StorageChestUiOpen && !_storageChestUiBuilt )
			return;

		StorageChestUiOpen = false;
		_storageChestStructureId = Guid.Empty;
		_storageChestMirror = null;
		StorageClearDrag();

		if ( _storageChestLayer.IsValid )
			SetStorageChestLayerVisible( false );
	}

	void TickStorageChestProximity()
	{
		if ( !StorageChestUiOpen || _storageChestStructureId == Guid.Empty )
			return;

		if ( ThornsStorageChest.TryGetForStructure( _storageChestStructureId, out var ch ) && ch.IsValid() )
		{
			if ( !ThornsStorageChest.HostValidatePlayerUseRange(
				     GameObject,
				     ch,
				     ThornsStorageChest.InteractionRange * 1.2f ) )
				CloseStorageChestUi();
			return;
		}

		if ( !ThornsFurnitureContainer.TryGet( _storageChestStructureId, out var fc ) || !fc.IsValid() )
		{
			CloseStorageChestUi();
			return;
		}

		if ( !ThornsFurnitureContainer.HostValidatePlayerUseRange(
			     GameObject,
			     fc,
			     ThornsFurnitureContainer.InteractionRange * 1.2f ) )
			CloseStorageChestUi();
	}

	void EnsureStorageChestUiBuilt()
	{
		if ( _storageChestUiBuilt || Panel is null || !Panel.IsValid )
			return;

		_storageChestUiBuilt = true;

		_storageChestLayer = ThornsUiPanelAdd.AddChildPanel(Panel,  "thorns-storage-chest-layer" );
		_storageChestLayer.Style.Position = PositionMode.Absolute;
		_storageChestLayer.Style.Left = 0;
		_storageChestLayer.Style.Top = 0;
		_storageChestLayer.Style.Width = Length.Fraction( 1f );
		_storageChestLayer.Style.Height = Length.Fraction( 1f );
		_storageChestLayer.Style.BackgroundColor = new Color( 0.02f, 0.02f, 0.03f, 0.72f );
		_storageChestLayer.Style.JustifyContent = Justify.Center;
		_storageChestLayer.Style.AlignItems = Align.Center;
		_storageChestLayer.Style.ZIndex = 200;
		_storageChestLayer.Style.PointerEvents = PointerEvents.All;
		_storageChestLayer.AddEventListener( "onmouseup", StorageOnChestLayerMouseUp );

		_storageChestCard = ThornsUiPanelAdd.AddChildPanel(_storageChestLayer,  "thorns-storage-chest-card" );
		_storageChestCard.Style.FlexDirection = FlexDirection.Column;
		_storageChestCard.Style.Padding = 18;
		_storageChestCard.Style.BackgroundColor = new Color( 0.07f, 0.07f, 0.09f, 0.96f );
		_storageChestCard.Style.BorderWidth = 2;
		_storageChestCard.Style.BorderColor = new Color( 0.55f, 0.48f, 0.35f, 0.95f );
		_storageChestCard.Style.MinWidth = Length.Pixels( 520 );

		var headerRow = ThornsUiPanelAdd.AddChildPanel( _storageChestCard, "thorns-storage-chest-header" );
		headerRow.Style.FlexDirection = FlexDirection.Row;
		headerRow.Style.AlignItems = Align.Center;
		headerRow.Style.JustifyContent = Justify.SpaceBetween;
		headerRow.Style.MarginBottom = 8;

		var title = headerRow.AddChild( new Label(
			"STORAGE CHEST",
			"thorns-storage-chest-title" ) );
		title.Style.FontSize = 18;
		title.Style.FontWeight = 900;
		title.Style.FontColor = new Color( 0.92f, 0.88f, 0.76f, 1f );

		var closeBtn = ThornsUiPanelAdd.AddChildPanel( headerRow, "thorns-storage-chest-close" );
		closeBtn.Style.PointerEvents = PointerEvents.All;
		closeBtn.AddEventListener( "onmousedown", StorageCloseButtonMouseDown );
		closeBtn.AddChild( new Label( "×", "thorns-storage-chest-close-glyph" ) );

		var hint = _storageChestCard.AddChild( new Label(
			"Drag items to move (release to drop). Shift-click to send stacks to the other side (merges when possible). ESC or Tab to close.",
			"thorns-storage-chest-hint" ) );
		hint.Style.FontSize = 12;
		hint.Style.FontColor = new Color( 0.7f, 0.68f, 0.62f, 1f );
		hint.Style.MarginBottom = 14;

		var chestLabel = _storageChestCard.AddChild( new Label( "Chest", "thorns-storage-chest-section" ) );
		chestLabel.Style.FontWeight = 800;
		chestLabel.Style.MarginBottom = 6;

		var chestHost = ThornsUiPanelAdd.AddChildPanel(_storageChestCard,  "thorns-storage-chest-slots-host" );
		chestHost.Style.FlexDirection = FlexDirection.Column;
		chestHost.Style.MarginBottom = 18;

		_storageChestSlots = new ThornsUiGridSlot[ThornsStorageChest.SlotCount];
		for ( var row = 0; row < 4; row++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel(chestHost,  $"thorns-storage-chest-row-{row}" );
			rowP.Style.FlexDirection = FlexDirection.Row;
			rowP.Style.MarginBottom = 4;

			for ( var col = 0; col < 6; col++ )
			{
				var idx = row * 6 + col;
				var slot = rowP.AddChild( new ThornsUiGridSlot( idx ) );
				slot.Style.Width = Length.Pixels( 76 );
				slot.Style.Height = Length.Pixels( 44 );
				slot.Style.MarginRight = 4;
				var i = idx;
				slot.OnInventoryPointerDown = ( si, btn ) => StorageOnChestSlotMouseDown( si, btn );
				slot.OnInventoryPointerUp = ( si, btn ) => StorageOnChestSlotMouseUp( si, btn );
				slot.OnHoverEnter = StorageOnChestHoverEnter;
				slot.OnHoverLeave = StorageOnChestHoverLeave;
				_storageChestSlots[idx] = slot;
			}
		}

		var invTitle = _storageChestCard.AddChild( new Label( "Your inventory", "thorns-storage-chest-section" ) );
		invTitle.Style.FontWeight = 800;
		invTitle.Style.MarginBottom = 6;

		var invHost = ThornsUiPanelAdd.AddChildPanel(_storageChestCard,  "thorns-storage-player-slots-host" );
		invHost.Style.FlexDirection = FlexDirection.Column;

		_storageOverlayPlayerSlots = new ThornsUiGridSlot[ThornsInventory.TotalSlots];
		const int cols = 6;
		var rows = (ThornsInventory.TotalSlots + cols - 1) / cols;
		for ( var row = 0; row < rows; row++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel(invHost,  $"thorns-storage-player-row-{row}" );
			rowP.Style.FlexDirection = FlexDirection.Row;
			rowP.Style.MarginBottom = 4;

			for ( var col = 0; col < cols; col++ )
			{
				var idx = row * cols + col;
				if ( idx >= ThornsInventory.TotalSlots )
					break;

				var slot = rowP.AddChild( new ThornsUiGridSlot( idx ) );
				slot.Style.Width = Length.Pixels( 76 );
				slot.Style.Height = Length.Pixels( 44 );
				slot.Style.MarginRight = 4;
				slot.OnInventoryPointerDown = StorageOnPlayerSlotMouseDown;
				slot.OnInventoryPointerUp = StorageOnPlayerSlotMouseUp;
				slot.OnHoverEnter = StorageOnPlayerHoverEnter;
				slot.OnHoverLeave = StorageOnPlayerHoverLeave;
				_storageOverlayPlayerSlots[idx] = slot;
			}
		}

		SetStorageChestLayerVisible( false );
	}

	static void ApplyStorageChestLayerVisibility( Panel layer, bool visible )
	{
		if ( layer is null || !layer.IsValid )
			return;

		layer.Style.Opacity = visible ? 1 : 0;
		layer.Style.PointerEvents = visible ? PointerEvents.All : PointerEvents.None;
	}

	void SetStorageChestLayerVisible( bool visible )
	{
		ApplyStorageChestLayerVisibility( _storageChestLayer, visible );
	}

	void StorageCloseButtonMouseDown( PanelEvent e )
	{
		CloseStorageChestUi();
	}

	void RefreshStorageChestPanelSlots()
	{
		if ( !StorageChestUiOpen )
			return;

		RefreshStorageChestGridFromMirror();
		RefreshStorageOverlayPlayerSlotsFromMirror();
		StorageRefreshDragDecorations();
	}

	bool TryGetChestMirrorSlot( int index, out ThornsInventorySlotNet net )
	{
		net = default;
		if ( _storageChestMirror is null || index < 0 || index >= ThornsStorageChest.SlotCount )
			return false;

		net = _storageChestMirror[index];
		return true;
	}

	void RefreshStorageChestGridFromMirror()
	{
		if ( _storageChestSlots is null )
			return;

		for ( var i = 0; i < _storageChestSlots.Length; i++ )
		{
			var cell = _storageChestSlots[i];
			if ( cell is null || !cell.IsValid )
				continue;

			if ( !TryGetChestMirrorSlot( i, out var net )
			     || string.IsNullOrWhiteSpace( net.ItemId )
			     || net.Quantity <= 0 )
			{
				cell.SetContent( "", "" );
				continue;
			}

			Color? tint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				tint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				tint = at;

			cell.SetMirrorSlotVisual( net, tint );
		}
	}

	void RefreshStorageOverlayPlayerSlotsFromMirror()
	{
		if ( _storageOverlayPlayerSlots is null )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		for ( var i = 0; i < _storageOverlayPlayerSlots.Length; i++ )
		{
			var cell = _storageOverlayPlayerSlots[i];
			if ( cell is null || !cell.IsValid )
				continue;

			inv.TryGetClientMirrorSlot( i, out var net );
			if ( string.IsNullOrWhiteSpace( net.ItemId ) || net.Quantity <= 0 )
			{
				cell.SetContent( "", "" );
				continue;
			}

			Color? tint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				tint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				tint = at;

			cell.SetMirrorSlotVisual( net, tint );
		}
	}

	void StorageOnChestSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !StorageChestUiOpen || btn != MouseButtons.Left )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !TryGetChestMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			StorageQuickTransferShiftClick( fromChest: true, fromIdx: slotIndex );
			return;
		}

		if ( _storageDragFromChest.HasValue )
			return;

		if ( !TryGetChestMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		StorageBeginDrag( fromChest: true, slotIndex );
	}

	void StorageOnPlayerSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !StorageChestUiOpen || btn != MouseButtons.Left )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			StorageQuickTransferShiftClick( fromChest: false, fromIdx: slotIndex );
			return;
		}

		if ( _storageDragFromChest.HasValue )
			return;

		if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		StorageBeginDrag( fromChest: false, slotIndex );
	}

	void StorageBeginDrag( bool fromChest, int slotIndex )
	{
		_storageDragFromChest = fromChest;
		_storageDragSlot = slotIndex;
		_storageHoverChestSlot = fromChest ? slotIndex : (int?)null;
		_storageHoverPlayerSlot = fromChest ? (int?)null : slotIndex;
		_shellDnDSyntheticCursorReady = false;
		StorageRebuildDragGhost();
		StorageUpdateDropTargetUnderCursor();
		StorageRefreshDragDecorations();
	}

	void StorageOnChestSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !StorageChestUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_storageDragFromChest.HasValue )
			return;

		StorageFinalizeDragDropFromCurrentTarget();
	}

	void StorageOnPlayerSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !StorageChestUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_storageDragFromChest.HasValue )
			return;

		StorageFinalizeDragDropFromCurrentTarget();
	}

	void StorageOnChestLayerMouseUp( PanelEvent e )
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		if ( e.Target != _storageChestLayer )
			return;

		StorageFinalizeDragDropFromCurrentTarget();
	}

	void StorageQuickTransferShiftClick( bool fromChest, int fromIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _storageChestStructureId == Guid.Empty )
			return;

		StorageClearDrag();

		inv.RequestStorageChestQuickTransfer(
			_storageChestStructureId.ToString( "D" ),
			fromChest,
			fromIdx );
	}

	void StorageFinalizeDragDropAt( int? chestDropSlot, int? playerDropSlot )
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _storageChestStructureId == Guid.Empty )
		{
			StorageClearDrag();
			return;
		}

		var fromChest = _storageDragFromChest.Value;
		var fromIdx = _storageDragSlot;

		StorageClearDrag();

		if ( fromChest )
		{
			if ( playerDropSlot.HasValue )
				StorageRequestTransfer( true, fromIdx, false, playerDropSlot.Value );
			else if ( chestDropSlot.HasValue && chestDropSlot.Value != fromIdx )
				StorageRequestTransfer( true, fromIdx, true, chestDropSlot.Value );
		}
		else
		{
			if ( chestDropSlot.HasValue )
				StorageRequestTransfer( false, fromIdx, true, chestDropSlot.Value );
			else if ( playerDropSlot.HasValue && playerDropSlot.Value != fromIdx )
				StorageRequestTransfer( false, fromIdx, false, playerDropSlot.Value );
		}
	}

	void StorageFinalizeDragDropFromCurrentTarget()
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		StorageUpdateDropTargetUnderCursor();
		StorageFinalizeDragDropAt( _storageHoverChestSlot, _storageHoverPlayerSlot );
	}

	void StorageRequestTransfer( bool fromChest, int fromIdx, bool toChest, int toIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _storageChestStructureId == Guid.Empty )
			return;

		ThornsInventoryClientTransfer.SubmitStorageChestStructuredTransfer(
			inv,
			_storageChestStructureId.ToString( "D" ),
			fromChest,
			fromIdx,
			toChest,
			toIdx );
	}

	void StorageOnChestHoverEnter( int idx )
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		_storageHoverChestSlot = idx;
		_storageHoverPlayerSlot = null;
		StorageRefreshDragDecorations();
	}

	void StorageOnChestHoverLeave( int idx )
	{
		if ( _storageHoverChestSlot == idx )
			_storageHoverChestSlot = null;

		StorageRefreshDragDecorations();
	}

	void StorageOnPlayerHoverEnter( int idx )
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		_storageHoverPlayerSlot = idx;
		_storageHoverChestSlot = null;
		StorageRefreshDragDecorations();
	}

	void StorageOnPlayerHoverLeave( int idx )
	{
		if ( _storageHoverPlayerSlot == idx )
			_storageHoverPlayerSlot = null;

		StorageRefreshDragDecorations();
	}

	void StorageRefreshDragDecorations()
	{
		if ( _storageChestSlots is null )
			return;

		foreach ( var s in _storageChestSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _storageDragFromChest.HasValue && _storageHoverChestSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _storageDragFromChest == true && _storageDragSlot == s.SlotIndex );
		}

		if ( _storageOverlayPlayerSlots is null )
			return;

		foreach ( var s in _storageOverlayPlayerSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _storageDragFromChest.HasValue && _storageHoverPlayerSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _storageDragFromChest == false && _storageDragSlot == s.SlotIndex );
		}
	}

	void StorageTickDragReleaseFallback()
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		if ( !(Input.Released( "Attack1" ) || Input.Released( "attack1" )) )
			return;

		Log.Info( "[Thorns][Storage DnD] global mouse release" );
		StorageFinalizeDragDropFromCurrentTarget();
	}

	void StorageUpdateDropTargetUnderCursor()
	{
		if ( !_storageDragFromChest.HasValue )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		ShellAdvanceSyntheticCursorForDnD();
		var screenPos = ShellCurrentMouseScreenPosition();

		int? chest = null;
		int? player = null;

		if ( _storageChestSlots is not null )
		{
			foreach ( var s in _storageChestSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				chest = s.SlotIndex;
				break;
			}
		}

		if ( !chest.HasValue && _storageOverlayPlayerSlots is not null )
		{
			foreach ( var s in _storageOverlayPlayerSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				player = s.SlotIndex;
				break;
			}
		}

		if ( _storageHoverChestSlot == chest && _storageHoverPlayerSlot == player )
			return;

		_storageHoverChestSlot = chest;
		_storageHoverPlayerSlot = player;

		Log.Info(
			$"[Thorns][Storage DnD] target chest={(chest.HasValue ? chest.Value.ToString() : "null")} player={(player.HasValue ? player.Value.ToString() : "null")}" );
		StorageRefreshDragDecorations();
	}

	void StorageClearDrag()
	{
		_storageDragFromChest = null;
		_storageDragSlot = -1;
		_storageHoverChestSlot = null;
		_storageHoverPlayerSlot = null;
		StorageDestroyDragGhost();
		StorageRefreshDragDecorations();
	}

	void StorageDestroyDragGhost()
	{
		if ( _storageDragGhost is not null && _storageDragGhost.IsValid )
			_storageDragGhost.Delete();

		_storageDragGhost = null;
	}

	void StorageRebuildDragGhost()
	{
		StorageDestroyDragGhost();
		if ( !_storageDragFromChest.HasValue || Panel is null || !Panel.IsValid )
			return;

		ThornsInventorySlotNet net = default;
		if ( _storageDragFromChest.Value )
		{
			if ( !TryGetChestMirrorSlot( _storageDragSlot, out net ) || net.Quantity <= 0 )
				return;
		}
		else
		{
			var inv = Components.Get<ThornsInventory>();
			if ( !inv.IsValid()
			     || !inv.TryGetClientMirrorSlot( _storageDragSlot, out net )
			     || net.Quantity <= 0 )
				return;
		}

		_storageDragGhost = ThornsUiPanelAdd.AddChildPanel( ShellStorageChestDragGhostHostParent(), "thorns-shell-inv-drag-ghost-host" );
		ThornsConfigureInventoryDragGhostHost( _storageDragGhost, zIndex: 520 );
		_storageDragGhost.Style.MarginLeft = 0;
		_storageDragGhost.Style.MarginTop = 0;
		_storageDragGhost.AddClass( "thorns-shell-inv-drag-ghost" );

		var fromPlayerInv = !_storageDragFromChest.Value;
		var toolbar =
			fromPlayerInv && _storageDragSlot >= 0 && _storageDragSlot < ThornsInventory.HotbarSlotCount;
		var preview = _storageDragGhost.AddChild(
			new ThornsUiGridSlot( _storageDragSlot, toolbar: toolbar ) );
		ThornsConfigureDragGhostPreviewSlot( preview );

		if ( fromPlayerInv && toolbar )
			preview.SetToolbarFromMirror( net, _storageDragSlot + 1 );
		else
		{
			Color? rowTint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				rowTint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				rowTint = at;
			preview.SetMirrorSlotVisual( net, rowTint );
		}

		UpdateStorageChestDragGhostPosition();
	}

	void UpdateStorageChestDragGhostPosition()
	{
		if ( _storageDragGhost is null || !_storageDragGhost.IsValid || !_storageDragFromChest.HasValue )
			return;

		PositionInventoryDragGhostUnderCursor( _storageDragGhost );
	}
}
