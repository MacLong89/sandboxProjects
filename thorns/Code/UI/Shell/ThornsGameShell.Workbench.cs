#nullable disable

using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	public void ApplyWorkbenchSnapshot(
		Guid structureInstanceId,
		ThornsInventorySlotNet[] slots,
		string processingLabel,
		float progress01,
		float remainingSeconds )
	{
		CloseStorageChestUi();
		CloseCampfireUi();
		_workbenchStructureId = structureInstanceId;
		var need = ThornsWorkbench.SlotCount;
		if ( slots is null || slots.Length != need )
		{
			_workbenchMirror = new ThornsInventorySlotNet[need];
			var n = Math.Min( need, slots?.Length ?? 0 );
			for ( var i = 0; i < n; i++ )
				_workbenchMirror[i] = slots![i];
		}
		else
		{
			_workbenchMirror = slots;
		}

		_workbenchProcessingLabel = processingLabel ?? "";
		_workbenchProgress01 = progress01;
		_workbenchRemainingSec = remainingSeconds;

		EnsureWorkbenchUiBuilt();
		WorkbenchUiOpen = true;
		if ( _workbenchLayer.IsValid )
			SetWorkbenchLayerVisible( true );

		RefreshWorkbenchGridFromMirror();
		RefreshWorkbenchOverlayPlayerSlotsFromMirror();
		RefreshWorkbenchProgressUi();
	}

	public void CloseWorkbenchUi()
	{
		if ( !WorkbenchUiOpen && !_workbenchUiBuilt )
			return;

		WorkbenchUiOpen = false;
		_workbenchStructureId = Guid.Empty;
		_workbenchMirror = null;
		WorkbenchClearDrag();

		if ( _workbenchLayer.IsValid )
			SetWorkbenchLayerVisible( false );
	}

	void TickWorkbenchProximity()
	{
		if ( !WorkbenchUiOpen || _workbenchStructureId == Guid.Empty )
			return;

		if ( !ThornsWorkbench.TryGetForStructure( _workbenchStructureId, out var wb ) || !wb.IsValid() )
		{
			CloseWorkbenchUi();
			return;
		}

		if ( !ThornsWorkbench.HostValidatePlayerUseRange(
			     GameObject,
			     wb,
			     ThornsWorkbench.InteractionRange * 1.2f ) )
			CloseWorkbenchUi();
	}

	void EnsureWorkbenchUiBuilt()
	{
		if ( _workbenchUiBuilt || Panel is null || !Panel.IsValid )
			return;

		_workbenchUiBuilt = true;

		_workbenchLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-workbench-layer" );
		_workbenchLayer.Style.Position = PositionMode.Absolute;
		_workbenchLayer.Style.Left = 0;
		_workbenchLayer.Style.Top = 0;
		_workbenchLayer.Style.Width = Length.Fraction( 1f );
		_workbenchLayer.Style.Height = Length.Fraction( 1f );
		_workbenchLayer.Style.BackgroundColor = new Color( 0.02f, 0.03f, 0.04f, 0.72f );
		_workbenchLayer.Style.JustifyContent = Justify.Center;
		_workbenchLayer.Style.AlignItems = Align.Center;
		_workbenchLayer.Style.ZIndex = 200;
		_workbenchLayer.Style.PointerEvents = PointerEvents.All;
		_workbenchLayer.AddEventListener( "onmouseup", WorkbenchOnLayerMouseUp );

		_workbenchCard = ThornsUiPanelAdd.AddChildPanel( _workbenchLayer, "thorns-workbench-card" );
		_workbenchCard.Style.FlexDirection = FlexDirection.Column;
		_workbenchCard.Style.AlignItems = Align.Stretch;
		_workbenchCard.Style.Padding = 18;
		_workbenchCard.Style.BackgroundColor = new Color( 0.07f, 0.08f, 0.1f, 0.96f );
		_workbenchCard.Style.BorderWidth = 2;
		_workbenchCard.Style.BorderColor = new Color( 0.35f, 0.55f, 0.72f, 0.95f );
		_workbenchCard.Style.MinWidth = Length.Pixels( 720 );
		_workbenchCard.Style.MaxWidth = Length.Pixels( 920 );

		var headerRow = ThornsUiPanelAdd.AddChildPanel( _workbenchCard, "thorns-workbench-header" );
		headerRow.Style.FlexDirection = FlexDirection.Row;
		headerRow.Style.AlignItems = Align.Center;
		headerRow.Style.JustifyContent = Justify.SpaceBetween;
		headerRow.Style.MarginBottom = 8;

		var title = headerRow.AddChild( new Label( "WORKBENCH", "thorns-workbench-title" ) );
		title.Style.FontSize = 18;
		title.Style.FontWeight = 900;
		title.Style.FontColor = new Color( 0.72f, 0.86f, 0.98f, 1f );

		var closeBtn = ThornsUiPanelAdd.AddChildPanel( headerRow, "thorns-workbench-close" );
		closeBtn.Style.PointerEvents = PointerEvents.All;
		closeBtn.AddEventListener( "onmousedown", WorkbenchCloseButtonMouseDown );
		closeBtn.AddChild( new Label( "×", "thorns-workbench-close-glyph" ) );

		var hint = _workbenchCard.AddChild( new Label(
			"Item to repair (low durability) · Materials: metal, cloth, leather scrap · Repaired item appears on the right. Shift-click quick-moves.",
			"thorns-workbench-hint" ) );
		hint.Style.FontSize = 12;
		hint.Style.FontColor = new Color( 0.68f, 0.72f, 0.78f, 1f );
		hint.Style.MarginBottom = 10;

		_workbenchProgressLabel = _workbenchCard.AddChild(
			new Label( "", "thorns-workbench-progress-text" ) );
		_workbenchProgressLabel.Style.FontSize = 13;
		_workbenchProgressLabel.Style.FontWeight = 700;
		_workbenchProgressLabel.Style.FontColor = new Color( 0.82f, 0.88f, 0.95f, 1f );
		_workbenchProgressLabel.Style.MarginBottom = 6;
		_workbenchProgressLabel.Style.Width = Length.Fraction( 1f );
		_workbenchProgressLabel.Style.WhiteSpace = WhiteSpace.Normal;

		var track = ThornsUiPanelAdd.AddChildPanel( _workbenchCard, "thorns-workbench-progress-track" );
		track.Style.Width = Length.Fraction( 1f );
		track.Style.Height = Length.Pixels( 14 );
		track.Style.MinHeight = Length.Pixels( 14 );
		track.Style.MaxHeight = Length.Pixels( 14 );
		track.Style.FlexShrink = 0;
		track.Style.Overflow = OverflowMode.Hidden;
		track.Style.BackgroundColor = new Color( 0.1f, 0.11f, 0.13f, 1f );
		track.Style.BorderWidth = 1;
		track.Style.BorderColor = new Color( 0.32f, 0.4f, 0.5f, 1f );
		track.Style.MarginBottom = 14;
		_workbenchProgressTrack = track;

		_workbenchProgressFill = ThornsUiPanelAdd.AddChildPanel( track, "thorns-workbench-progress-fill" );
		_workbenchProgressFill.Style.PointerEvents = PointerEvents.None;
		_workbenchProgressFill.Style.Position = PositionMode.Absolute;
		_workbenchProgressFill.Style.Left = 0;
		_workbenchProgressFill.Style.Top = 0;
		_workbenchProgressFill.Style.Bottom = 0;
		_workbenchProgressFill.Style.Width = Length.Fraction( 0f );
		_workbenchProgressFill.Style.BackgroundColor = new Color( 0.28f, 0.62f, 0.92f, 0.95f );

		var columns = ThornsUiPanelAdd.AddChildPanel( _workbenchCard, "thorns-workbench-columns" );
		columns.Style.FlexDirection = FlexDirection.Row;
		columns.Style.JustifyContent = Justify.Center;
		columns.Style.MarginBottom = 14;

		static Panel MakeColumn( Panel parent, string titleText )
		{
			var col = ThornsUiPanelAdd.AddChildPanel( parent, "thorns-workbench-col" );
			col.Style.FlexDirection = FlexDirection.Column;
			col.Style.MarginRight = 16;
			var t = col.AddChild( new Label( titleText, "thorns-workbench-col-title" ) );
			t.Style.FontWeight = 800;
			t.Style.FontSize = 11;
			t.Style.FontColor = new Color( 0.72f, 0.82f, 0.95f, 1f );
			t.Style.MarginBottom = 6;
			return col;
		}

		var itemCol = MakeColumn( columns, "ITEM (repair)" );
		var matCol = MakeColumn( columns, "MATERIALS" );
		var outCol = MakeColumn( columns, "OUTPUT" );

		_workbenchSlots = new ThornsUiGridSlot[ThornsWorkbench.SlotCount];

		{
			var slot = itemCol.AddChild( new ThornsUiGridSlot( ThornsWorkbench.InputSlot ) );
			slot.Style.Width = Length.Pixels( 88 );
			slot.Style.Height = Length.Pixels( 52 );
			slot.Style.MarginBottom = 4;
			slot.OnInventoryPointerDown = WorkbenchOnWorkbenchSlotMouseDown;
			slot.OnInventoryPointerUp = WorkbenchOnWorkbenchSlotMouseUp;
			slot.OnHoverEnter = WorkbenchOnWorkbenchHoverEnter;
			slot.OnHoverLeave = WorkbenchOnWorkbenchHoverLeave;
			_workbenchSlots[ThornsWorkbench.InputSlot] = slot;
		}

		for ( var r = 0; r < 4; r++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel( matCol, $"workbench-mat-row-{r}" );
			rowP.Style.FlexDirection = FlexDirection.Row;
			rowP.Style.MarginBottom = 4;
			for ( var c = 0; c < 2; c++ )
			{
				var idx = ThornsWorkbench.MaterialSlotStart + r * 2 + c;
				var slot = rowP.AddChild( new ThornsUiGridSlot( idx ) );
				slot.Style.Width = Length.Pixels( 76 );
				slot.Style.Height = Length.Pixels( 44 );
				slot.Style.MarginRight = 4;
				slot.OnInventoryPointerDown = WorkbenchOnWorkbenchSlotMouseDown;
				slot.OnInventoryPointerUp = WorkbenchOnWorkbenchSlotMouseUp;
				slot.OnHoverEnter = WorkbenchOnWorkbenchHoverEnter;
				slot.OnHoverLeave = WorkbenchOnWorkbenchHoverLeave;
				_workbenchSlots[idx] = slot;
			}
		}

		{
			var slot = outCol.AddChild( new ThornsUiGridSlot( ThornsWorkbench.OutputSlot ) );
			slot.Style.Width = Length.Pixels( 88 );
			slot.Style.Height = Length.Pixels( 52 );
			slot.Style.MarginBottom = 4;
			slot.OnInventoryPointerDown = WorkbenchOnWorkbenchSlotMouseDown;
			slot.OnInventoryPointerUp = WorkbenchOnWorkbenchSlotMouseUp;
			slot.OnHoverEnter = WorkbenchOnWorkbenchHoverEnter;
			slot.OnHoverLeave = WorkbenchOnWorkbenchHoverLeave;
			_workbenchSlots[ThornsWorkbench.OutputSlot] = slot;
		}

		var invTitle = _workbenchCard.AddChild( new Label( "Your inventory", "thorns-workbench-section" ) );
		invTitle.Style.FontWeight = 800;
		invTitle.Style.MarginBottom = 6;

		var invHost = ThornsUiPanelAdd.AddChildPanel( _workbenchCard, "thorns-workbench-player-slots-host" );
		invHost.Style.FlexDirection = FlexDirection.Column;

		_workbenchOverlayPlayerSlots = new ThornsUiGridSlot[ThornsInventory.TotalSlots];
		const int cols = 6;
		var rows = (ThornsInventory.TotalSlots + cols - 1) / cols;
		for ( var row = 0; row < rows; row++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel( invHost, $"thorns-workbench-player-row-{row}" );
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
				slot.OnInventoryPointerDown = WorkbenchOnPlayerSlotMouseDown;
				slot.OnInventoryPointerUp = WorkbenchOnPlayerSlotMouseUp;
				slot.OnHoverEnter = WorkbenchOnPlayerHoverEnter;
				slot.OnHoverLeave = WorkbenchOnPlayerHoverLeave;
				_workbenchOverlayPlayerSlots[idx] = slot;
			}
		}

		SetWorkbenchLayerVisible( false );
	}

	void RefreshWorkbenchProgressUi()
	{
		if ( _workbenchProgressLabel is null || !_workbenchProgressLabel.IsValid )
			return;

		if ( string.IsNullOrWhiteSpace( _workbenchProcessingLabel ) )
		{
			_workbenchProgressLabel.Text = "Idle — place a damaged tool, weapon, or armor piece and repair materials.";
			if ( _workbenchProgressFill.IsValid )
				_workbenchProgressFill.Style.Width = Length.Fraction( 0f );
			return;
		}

		_workbenchProgressLabel.Text =
			$"Repairing: {_workbenchProcessingLabel}   ({_workbenchRemainingSec:F1}s left)";
		if ( _workbenchProgressFill.IsValid )
			_workbenchProgressFill.Style.Width = Length.Fraction( Math.Clamp( _workbenchProgress01, 0f, 1f ) );
	}

	static void ApplyWorkbenchLayerVisibility( Panel layer, bool visible )
	{
		if ( layer is null || !layer.IsValid )
			return;

		layer.Style.Opacity = visible ? 1 : 0;
		layer.Style.PointerEvents = visible ? PointerEvents.All : PointerEvents.None;
	}

	void SetWorkbenchLayerVisible( bool visible ) =>
		ApplyWorkbenchLayerVisibility( _workbenchLayer, visible );

	void WorkbenchCloseButtonMouseDown( PanelEvent e ) => CloseWorkbenchUi();

	void RefreshWorkbenchPanelSlots()
	{
		if ( !WorkbenchUiOpen )
			return;

		RefreshWorkbenchGridFromMirror();
		RefreshWorkbenchOverlayPlayerSlotsFromMirror();
		RefreshWorkbenchProgressUi();
		WorkbenchRefreshDragDecorations();
	}

	bool TryGetWorkbenchMirrorSlot( int index, out ThornsInventorySlotNet net )
	{
		net = default;
		if ( _workbenchMirror is null || index < 0 || index >= ThornsWorkbench.SlotCount )
			return false;

		net = _workbenchMirror[index];
		return true;
	}

	void RefreshWorkbenchGridFromMirror()
	{
		if ( _workbenchSlots is null )
			return;

		for ( var i = 0; i < _workbenchSlots.Length; i++ )
		{
			var cell = _workbenchSlots[i];
			if ( cell is null || !cell.IsValid )
				continue;

			if ( !TryGetWorkbenchMirrorSlot( i, out var net )
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

	void RefreshWorkbenchOverlayPlayerSlotsFromMirror()
	{
		if ( _workbenchOverlayPlayerSlots is null )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		for ( var i = 0; i < _workbenchOverlayPlayerSlots.Length; i++ )
		{
			var cell = _workbenchOverlayPlayerSlots[i];
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

	void WorkbenchOnWorkbenchSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !WorkbenchUiOpen || btn != MouseButtons.Left )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !TryGetWorkbenchMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			WorkbenchQuickTransferShiftClick( fromWorkbench: true, fromIdx: slotIndex );
			return;
		}

		if ( _workbenchDragFromWorkbench.HasValue )
			return;

		if ( !TryGetWorkbenchMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		WorkbenchBeginDrag( fromWorkbench: true, slotIndex );
	}

	void WorkbenchOnPlayerSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !WorkbenchUiOpen || btn != MouseButtons.Left )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			WorkbenchQuickTransferShiftClick( fromWorkbench: false, fromIdx: slotIndex );
			return;
		}

		if ( _workbenchDragFromWorkbench.HasValue )
			return;

		if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		WorkbenchBeginDrag( fromWorkbench: false, slotIndex );
	}

	void WorkbenchBeginDrag( bool fromWorkbench, int slotIndex )
	{
		_workbenchDragFromWorkbench = fromWorkbench;
		_workbenchDragSlot = slotIndex;
		_workbenchHoverWorkbenchSlot = fromWorkbench ? slotIndex : (int?)null;
		_workbenchHoverPlayerSlot = fromWorkbench ? (int?)null : slotIndex;
		_shellDnDSyntheticCursorReady = false;
		WorkbenchRebuildDragGhost();
		WorkbenchUpdateDropTargetUnderCursor();
		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchOnWorkbenchSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !WorkbenchUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		WorkbenchFinalizeDragDropFromCurrentTarget();
	}

	void WorkbenchOnPlayerSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !WorkbenchUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		WorkbenchFinalizeDragDropFromCurrentTarget();
	}

	void WorkbenchOnLayerMouseUp( PanelEvent e )
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		if ( e.Target != _workbenchLayer )
			return;

		WorkbenchFinalizeDragDropFromCurrentTarget();
	}

	void WorkbenchQuickTransferShiftClick( bool fromWorkbench, int fromIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _workbenchStructureId == Guid.Empty )
			return;

		WorkbenchClearDrag();

		inv.RequestWorkbenchQuickTransfer(
			_workbenchStructureId.ToString( "D" ),
			fromWorkbench,
			fromIdx );
	}

	void WorkbenchFinalizeDragDropAt( int? workbenchDropSlot, int? playerDropSlot )
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _workbenchStructureId == Guid.Empty )
		{
			WorkbenchClearDrag();
			return;
		}

		var fromWorkbench = _workbenchDragFromWorkbench.Value;
		var fromIdx = _workbenchDragSlot;

		WorkbenchClearDrag();

		if ( fromWorkbench )
		{
			if ( playerDropSlot.HasValue )
				WorkbenchRequestTransfer( true, fromIdx, false, playerDropSlot.Value );
			else if ( workbenchDropSlot.HasValue && workbenchDropSlot.Value != fromIdx )
				WorkbenchRequestTransfer( true, fromIdx, true, workbenchDropSlot.Value );
		}
		else
		{
			if ( workbenchDropSlot.HasValue )
				WorkbenchRequestTransfer( false, fromIdx, true, workbenchDropSlot.Value );
			else if ( playerDropSlot.HasValue && playerDropSlot.Value != fromIdx )
				WorkbenchRequestTransfer( false, fromIdx, false, playerDropSlot.Value );
		}
	}

	void WorkbenchFinalizeDragDropFromCurrentTarget()
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		WorkbenchUpdateDropTargetUnderCursor();
		WorkbenchFinalizeDragDropAt( _workbenchHoverWorkbenchSlot, _workbenchHoverPlayerSlot );
	}

	void WorkbenchRequestTransfer( bool fromWorkbench, int fromIdx, bool toWorkbench, int toIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _workbenchStructureId == Guid.Empty )
			return;

		ThornsInventoryClientTransfer.SubmitWorkbenchStructuredTransfer(
			inv,
			_workbenchStructureId.ToString( "D" ),
			fromWorkbench,
			fromIdx,
			toWorkbench,
			toIdx );
	}

	void WorkbenchOnWorkbenchHoverEnter( int idx )
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		_workbenchHoverWorkbenchSlot = idx;
		_workbenchHoverPlayerSlot = null;
		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchOnWorkbenchHoverLeave( int idx )
	{
		if ( _workbenchHoverWorkbenchSlot == idx )
			_workbenchHoverWorkbenchSlot = null;

		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchOnPlayerHoverEnter( int idx )
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		_workbenchHoverPlayerSlot = idx;
		_workbenchHoverWorkbenchSlot = null;
		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchOnPlayerHoverLeave( int idx )
	{
		if ( _workbenchHoverPlayerSlot == idx )
			_workbenchHoverPlayerSlot = null;

		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchRefreshDragDecorations()
	{
		if ( _workbenchSlots is null )
			return;

		foreach ( var s in _workbenchSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _workbenchDragFromWorkbench.HasValue && _workbenchHoverWorkbenchSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _workbenchDragFromWorkbench == true && _workbenchDragSlot == s.SlotIndex );
		}

		if ( _workbenchOverlayPlayerSlots is null )
			return;

		foreach ( var s in _workbenchOverlayPlayerSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _workbenchDragFromWorkbench.HasValue && _workbenchHoverPlayerSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _workbenchDragFromWorkbench == false && _workbenchDragSlot == s.SlotIndex );
		}
	}

	void WorkbenchTickDragReleaseFallback()
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		if ( !(Input.Released( "Attack1" ) || Input.Released( "attack1" )) )
			return;

		WorkbenchFinalizeDragDropFromCurrentTarget();
	}

	void WorkbenchUpdateDropTargetUnderCursor()
	{
		if ( !_workbenchDragFromWorkbench.HasValue )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		ShellAdvanceSyntheticCursorForDnD();
		var screenPos = ShellCurrentMouseScreenPosition();

		int? wb = null;
		int? player = null;

		if ( _workbenchSlots is not null )
		{
			foreach ( var s in _workbenchSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				wb = s.SlotIndex;
				break;
			}
		}

		if ( !wb.HasValue && _workbenchOverlayPlayerSlots is not null )
		{
			foreach ( var s in _workbenchOverlayPlayerSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				player = s.SlotIndex;
				break;
			}
		}

		if ( _workbenchHoverWorkbenchSlot == wb && _workbenchHoverPlayerSlot == player )
			return;

		_workbenchHoverWorkbenchSlot = wb;
		_workbenchHoverPlayerSlot = player;

		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchClearDrag()
	{
		_workbenchDragFromWorkbench = null;
		_workbenchDragSlot = -1;
		_workbenchHoverWorkbenchSlot = null;
		_workbenchHoverPlayerSlot = null;
		WorkbenchDestroyDragGhost();
		WorkbenchRefreshDragDecorations();
	}

	void WorkbenchDestroyDragGhost()
	{
		if ( _workbenchDragGhost is not null && _workbenchDragGhost.IsValid )
			_workbenchDragGhost.Delete();

		_workbenchDragGhost = null;
	}

	void WorkbenchRebuildDragGhost()
	{
		WorkbenchDestroyDragGhost();
		if ( !_workbenchDragFromWorkbench.HasValue || Panel is null || !Panel.IsValid )
			return;

		ThornsInventorySlotNet net = default;
		if ( _workbenchDragFromWorkbench.Value )
		{
			if ( !TryGetWorkbenchMirrorSlot( _workbenchDragSlot, out net ) || net.Quantity <= 0 )
				return;
		}
		else
		{
			var inv = Components.Get<ThornsInventory>();
			if ( !inv.IsValid()
			     || !inv.TryGetClientMirrorSlot( _workbenchDragSlot, out net )
			     || net.Quantity <= 0 )
				return;
		}

		_workbenchDragGhost = ThornsUiPanelAdd.AddChildPanel( ShellWorkbenchDragGhostHostParent(), "thorns-shell-workbench-drag-ghost-host" );
		ThornsConfigureInventoryDragGhostHost( _workbenchDragGhost, zIndex: 520 );
		_workbenchDragGhost.Style.MarginLeft = 0;
		_workbenchDragGhost.Style.MarginTop = 0;
		_workbenchDragGhost.AddClass( "thorns-shell-inv-drag-ghost" );

		var fromPlayerInv = !_workbenchDragFromWorkbench.Value;
		var toolbar =
			fromPlayerInv && _workbenchDragSlot >= 0 && _workbenchDragSlot < ThornsInventory.HotbarSlotCount;
		var preview = _workbenchDragGhost.AddChild(
			new ThornsUiGridSlot( _workbenchDragSlot, toolbar: toolbar ) );
		ThornsConfigureDragGhostPreviewSlot( preview );

		if ( fromPlayerInv && toolbar )
			preview.SetToolbarFromMirror( net, _workbenchDragSlot + 1 );
		else
		{
			Color? rowTint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				rowTint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				rowTint = at;
			preview.SetMirrorSlotVisual( net, rowTint );
		}

		UpdateWorkbenchDragGhostPosition();
	}

	void UpdateWorkbenchDragGhostPosition()
	{
		if ( _workbenchDragGhost is null || !_workbenchDragGhost.IsValid || !_workbenchDragFromWorkbench.HasValue )
			return;

		PositionInventoryDragGhostUnderCursor( _workbenchDragGhost );
	}
}
