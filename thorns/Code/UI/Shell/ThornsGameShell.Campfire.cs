#nullable disable

using Sandbox.UI;

namespace Sandbox;

public sealed partial class ThornsGameShell
{
	public void ApplyCampfireSnapshot(
		Guid structureInstanceId,
		ThornsInventorySlotNet[] slots,
		string processingInputLabel,
		string processingOutputLabel,
		float progress01,
		float remainingSeconds,
		bool presentOverlay )
	{
		CloseStorageChestUi();
		CloseWorkbenchUi();
		_campfireStructureId = structureInstanceId;
		var need = ThornsCampfire.SlotCount;
		if ( slots is null || slots.Length != need )
		{
			_campfireMirror = new ThornsInventorySlotNet[need];
			var n = Math.Min( need, slots?.Length ?? 0 );
			for ( var i = 0; i < n; i++ )
				_campfireMirror[i] = slots![i];
		}
		else
		{
			_campfireMirror = slots;
		}

		_campfireInputLabel = processingInputLabel ?? "";
		_campfireOutputLabel = processingOutputLabel ?? "";
		_campfireProgress01 = progress01;
		_campfireRemainingSec = remainingSeconds;

		EnsureCampfireUiBuilt();
		if ( presentOverlay )
		{
			CampfireUiOpen = true;
			if ( _campfireLayer.IsValid )
				SetCampfireLayerVisible( true );
		}

		if ( CampfireUiOpen )
		{
			RefreshCampfireGridFromMirror();
			RefreshCampfireOverlayPlayerSlotsFromMirror();
			RefreshCampfireProgressUi();
		}
	}

	public void CloseCampfireUi()
	{
		if ( !CampfireUiOpen && !_campfireUiBuilt )
			return;

		var sid = _campfireStructureId;
		var inv = Components.Get<ThornsInventory>();
		if ( inv.IsValid() && sid != Guid.Empty )
		{
			if ( !Networking.IsActive )
			{
				if ( ThornsCampfire.TryGetForStructure( sid, out var cf ) && cf.IsValid() )
					cf.HostClearLastInteractInventoryIf( inv );
			}
			else
				inv.RequestNotifyCampfireUiClosed( sid.ToString( "D" ) );
		}

		CampfireUiOpen = false;
		_campfireStructureId = Guid.Empty;
		_campfireMirror = null;
		CampfireClearDrag();

		if ( _campfireLayer.IsValid )
			SetCampfireLayerVisible( false );
	}

	void TickCampfireProximity()
	{
		if ( !CampfireUiOpen || _campfireStructureId == Guid.Empty )
			return;

		if ( !ThornsCampfire.TryGetForStructure( _campfireStructureId, out var cf ) || !cf.IsValid() )
		{
			CloseCampfireUi();
			return;
		}

		if ( !ThornsCampfire.HostValidatePlayerUseRange(
			     GameObject,
			     cf,
			     ThornsCampfire.InteractionRange * 1.2f ) )
			CloseCampfireUi();
	}

	void EnsureCampfireUiBuilt()
	{
		if ( _campfireUiBuilt || Panel is null || !Panel.IsValid )
			return;

		_campfireUiBuilt = true;

		_campfireLayer = ThornsUiPanelAdd.AddChildPanel( Panel, "thorns-campfire-layer" );
		_campfireLayer.Style.Position = PositionMode.Absolute;
		_campfireLayer.Style.Left = 0;
		_campfireLayer.Style.Top = 0;
		_campfireLayer.Style.Width = Length.Fraction( 1f );
		_campfireLayer.Style.Height = Length.Fraction( 1f );
		_campfireLayer.Style.BackgroundColor = new Color( 0.02f, 0.02f, 0.03f, 0.72f );
		_campfireLayer.Style.JustifyContent = Justify.Center;
		_campfireLayer.Style.AlignItems = Align.Center;
		_campfireLayer.Style.ZIndex = 200;
		_campfireLayer.Style.PointerEvents = PointerEvents.All;
		_campfireLayer.AddEventListener( "onmouseup", CampfireOnLayerMouseUp );

		_campfireCard = ThornsUiPanelAdd.AddChildPanel( _campfireLayer, "thorns-campfire-card" );
		_campfireCard.Style.FlexDirection = FlexDirection.Column;
		_campfireCard.Style.AlignItems = Align.Stretch;
		_campfireCard.Style.Padding = 18;
		_campfireCard.Style.BackgroundColor = new Color( 0.09f, 0.06f, 0.05f, 0.96f );
		_campfireCard.Style.BorderWidth = 2;
		_campfireCard.Style.BorderColor = new Color( 0.72f, 0.42f, 0.22f, 0.95f );
		_campfireCard.Style.MinWidth = Length.Pixels( 720 );
		_campfireCard.Style.MaxWidth = Length.Pixels( 900 );

		var headerRow = ThornsUiPanelAdd.AddChildPanel( _campfireCard, "thorns-campfire-header" );
		headerRow.Style.FlexDirection = FlexDirection.Row;
		headerRow.Style.AlignItems = Align.Center;
		headerRow.Style.JustifyContent = Justify.SpaceBetween;
		headerRow.Style.MarginBottom = 8;

		var title = headerRow.AddChild( new Label( "CAMPFIRE", "thorns-campfire-title" ) );
		title.Style.FontSize = 18;
		title.Style.FontWeight = 900;
		title.Style.FontColor = new Color( 0.95f, 0.72f, 0.48f, 1f );

		var closeBtn = ThornsUiPanelAdd.AddChildPanel( headerRow, "thorns-campfire-close" );
		closeBtn.Style.PointerEvents = PointerEvents.All;
		closeBtn.AddEventListener( "onmousedown", CampfireCloseButtonMouseDown );
		closeBtn.AddChild( new Label( "×", "thorns-campfire-close-glyph" ) );

		var hint = _campfireCard.AddChild( new Label(
			"Fuel: wood only · Raw: ore, meat, dirty water · Outputs appear on the right. Shift-click quick-moves stacks.",
			"thorns-campfire-hint" ) );
		hint.Style.FontSize = 12;
		hint.Style.FontColor = new Color( 0.7f, 0.65f, 0.58f, 1f );
		hint.Style.MarginBottom = 10;

		_campfireProgressLabel = _campfireCard.AddChild(
			new Label( "", "thorns-campfire-progress-text" ) );
		_campfireProgressLabel.Style.FontSize = 13;
		_campfireProgressLabel.Style.FontWeight = 700;
		_campfireProgressLabel.Style.FontColor = new Color( 0.88f, 0.78f, 0.62f, 1f );
		_campfireProgressLabel.Style.MarginBottom = 6;
		_campfireProgressLabel.Style.Width = Length.Fraction( 1f );
		_campfireProgressLabel.Style.WhiteSpace = WhiteSpace.Normal;

		var track = ThornsUiPanelAdd.AddChildPanel( _campfireCard, "thorns-campfire-progress-track" );
		track.Style.Width = Length.Fraction( 1f );
		track.Style.Height = Length.Pixels( 14 );
		track.Style.MinHeight = Length.Pixels( 14 );
		track.Style.MaxHeight = Length.Pixels( 14 );
		track.Style.FlexShrink = 0;
		track.Style.Overflow = OverflowMode.Hidden;
		track.Style.BackgroundColor = new Color( 0.12f, 0.1f, 0.09f, 1f );
		track.Style.BorderWidth = 1;
		track.Style.BorderColor = new Color( 0.35f, 0.28f, 0.22f, 1f );
		track.Style.MarginBottom = 14;
		_campfireProgressTrack = track;

		_campfireProgressFill = ThornsUiPanelAdd.AddChildPanel( track, "thorns-campfire-progress-fill" );
		_campfireProgressFill.Style.PointerEvents = PointerEvents.None;
		_campfireProgressFill.Style.Position = PositionMode.Absolute;
		_campfireProgressFill.Style.Left = 0;
		_campfireProgressFill.Style.Top = 0;
		_campfireProgressFill.Style.Bottom = 0;
		_campfireProgressFill.Style.Width = Length.Fraction( 0f );
		_campfireProgressFill.Style.BackgroundColor = new Color( 0.85f, 0.45f, 0.18f, 0.95f );

		var columns = ThornsUiPanelAdd.AddChildPanel( _campfireCard, "thorns-campfire-columns" );
		columns.Style.FlexDirection = FlexDirection.Row;
		columns.Style.JustifyContent = Justify.Center;
		columns.Style.MarginBottom = 14;

		static Panel MakeColumn( Panel parent, string titleText )
		{
			var col = ThornsUiPanelAdd.AddChildPanel( parent, "thorns-campfire-col" );
			col.Style.FlexDirection = FlexDirection.Column;
			col.Style.MarginRight = 16;
			var t = col.AddChild( new Label( titleText, "thorns-campfire-col-title" ) );
			t.Style.FontWeight = 800;
			t.Style.FontSize = 11;
			t.Style.FontColor = new Color( 0.82f, 0.65f, 0.45f, 1f );
			t.Style.MarginBottom = 6;
			return col;
		}

		var fuelCol = MakeColumn( columns, "FUEL (wood)" );
		var rawCol = MakeColumn( columns, "RAW" );
		var outCol = MakeColumn( columns, "OUTPUT" );

		_campfireSlots = new ThornsUiGridSlot[ThornsCampfire.SlotCount];

		for ( var i = 0; i < ThornsCampfire.FuelSlotCount; i++ )
		{
			var idx = ThornsCampfire.FuelSlotStart + i;
			AddCampfireSlot( fuelCol, idx );
		}

		for ( var r = 0; r < 4; r++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel( rawCol, $"campfire-raw-row-{r}" );
			rowP.Style.FlexDirection = FlexDirection.Row;
			rowP.Style.MarginBottom = 4;
			for ( var c = 0; c < 2; c++ )
			{
				var idx = ThornsCampfire.RawSlotStart + r * 2 + c;
				AddCampfireSlotToRow( rowP, idx );
			}
		}

		for ( var r = 0; r < 4; r++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel( outCol, $"campfire-out-row-{r}" );
			rowP.Style.FlexDirection = FlexDirection.Row;
			rowP.Style.MarginBottom = 4;
			for ( var c = 0; c < 2; c++ )
			{
				var idx = ThornsCampfire.OutputSlotStart + r * 2 + c;
				AddCampfireSlotToRow( rowP, idx );
			}
		}

		void AddCampfireSlot( Panel col, int idx )
		{
			var slot = col.AddChild( new ThornsUiGridSlot( idx ) );
			slot.Style.Width = Length.Pixels( 76 );
			slot.Style.Height = Length.Pixels( 44 );
			slot.Style.MarginBottom = 4;
			slot.OnInventoryPointerDown = CampfireOnCampfireSlotMouseDown;
			slot.OnInventoryPointerUp = CampfireOnCampfireSlotMouseUp;
			slot.OnHoverEnter = CampfireOnCampfireHoverEnter;
			slot.OnHoverLeave = CampfireOnCampfireHoverLeave;
			_campfireSlots[idx] = slot;
		}

		void AddCampfireSlotToRow( Panel rowP, int idx )
		{
			var slot = rowP.AddChild( new ThornsUiGridSlot( idx ) );
			slot.Style.Width = Length.Pixels( 76 );
			slot.Style.Height = Length.Pixels( 44 );
			slot.Style.MarginRight = 4;
			slot.OnInventoryPointerDown = CampfireOnCampfireSlotMouseDown;
			slot.OnInventoryPointerUp = CampfireOnCampfireSlotMouseUp;
			slot.OnHoverEnter = CampfireOnCampfireHoverEnter;
			slot.OnHoverLeave = CampfireOnCampfireHoverLeave;
			_campfireSlots[idx] = slot;
		}

		var invTitle = _campfireCard.AddChild( new Label( "Your inventory", "thorns-campfire-section" ) );
		invTitle.Style.FontWeight = 800;
		invTitle.Style.MarginBottom = 6;

		var invHost = ThornsUiPanelAdd.AddChildPanel( _campfireCard, "thorns-campfire-player-slots-host" );
		invHost.Style.FlexDirection = FlexDirection.Column;

		_campfireOverlayPlayerSlots = new ThornsUiGridSlot[ThornsInventory.TotalSlots];
		const int cols = 6;
		var rows = (ThornsInventory.TotalSlots + cols - 1) / cols;
		for ( var row = 0; row < rows; row++ )
		{
			var rowP = ThornsUiPanelAdd.AddChildPanel( invHost, $"thorns-campfire-player-row-{row}" );
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
				slot.OnInventoryPointerDown = CampfireOnPlayerSlotMouseDown;
				slot.OnInventoryPointerUp = CampfireOnPlayerSlotMouseUp;
				slot.OnHoverEnter = CampfireOnPlayerHoverEnter;
				slot.OnHoverLeave = CampfireOnPlayerHoverLeave;
				_campfireOverlayPlayerSlots[idx] = slot;
			}
		}

		SetCampfireLayerVisible( false );
	}

	void RefreshCampfireProgressUi()
	{
		if ( _campfireProgressLabel is null || !_campfireProgressLabel.IsValid )
			return;

		if ( string.IsNullOrWhiteSpace( _campfireInputLabel ) || string.IsNullOrWhiteSpace( _campfireOutputLabel ) )
		{
			_campfireProgressLabel.Text = "Idle — add wood to fuel and processable items to raw.";
			if ( _campfireProgressFill.IsValid )
				_campfireProgressFill.Style.Width = Length.Fraction( 0f );
			return;
		}

		_campfireProgressLabel.Text =
			$"Processing: {_campfireInputLabel} → {_campfireOutputLabel}   ({_campfireRemainingSec:F1}s left)";
		if ( _campfireProgressFill.IsValid )
		{
			// One source of truth with the label: bar = time remaining for this cycle (not host elapsed progress01).
			var dur = ThornsCampfire.ProcessSecondsPerItem;
			var frac = dur > 0.001f
				? Math.Clamp( _campfireRemainingSec / dur, 0f, 1f )
				: 0f;
			_campfireProgressFill.Style.Width = Length.Fraction( frac );
		}
	}

	static void ApplyCampfireLayerVisibility( Panel layer, bool visible )
	{
		if ( layer is null || !layer.IsValid )
			return;

		layer.Style.Opacity = visible ? 1 : 0;
		layer.Style.PointerEvents = visible ? PointerEvents.All : PointerEvents.None;
	}

	void SetCampfireLayerVisible( bool visible ) =>
		ApplyCampfireLayerVisibility( _campfireLayer, visible );

	void CampfireCloseButtonMouseDown( PanelEvent e ) => CloseCampfireUi();

	void RefreshCampfirePanelSlots()
	{
		if ( !CampfireUiOpen )
			return;

		RefreshCampfireGridFromMirror();
		RefreshCampfireOverlayPlayerSlotsFromMirror();
		RefreshCampfireProgressUi();
		CampfireRefreshDragDecorations();
	}

	bool TryGetCampfireMirrorSlot( int index, out ThornsInventorySlotNet net )
	{
		net = default;
		if ( _campfireMirror is null || index < 0 || index >= ThornsCampfire.SlotCount )
			return false;

		net = _campfireMirror[index];
		return true;
	}

	void RefreshCampfireGridFromMirror()
	{
		if ( _campfireSlots is null )
			return;

		for ( var i = 0; i < _campfireSlots.Length; i++ )
		{
			var cell = _campfireSlots[i];
			if ( cell is null || !cell.IsValid )
				continue;

			if ( !TryGetCampfireMirrorSlot( i, out var net )
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

	void RefreshCampfireOverlayPlayerSlotsFromMirror()
	{
		if ( _campfireOverlayPlayerSlots is null )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		for ( var i = 0; i < _campfireOverlayPlayerSlots.Length; i++ )
		{
			var cell = _campfireOverlayPlayerSlots[i];
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

	void CampfireOnCampfireSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !CampfireUiOpen || btn != MouseButtons.Left )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !TryGetCampfireMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			CampfireQuickTransferShiftClick( fromCampfire: true, fromIdx: slotIndex );
			return;
		}

		if ( _campfireDragFromCampfire.HasValue )
			return;

		if ( !TryGetCampfireMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		CampfireBeginDrag( fromCampfire: true, slotIndex );
	}

	void CampfireOnPlayerSlotMouseDown( int slotIndex, MouseButtons btn )
	{
		if ( !CampfireUiOpen || btn != MouseButtons.Left )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		if ( Input.Keyboard.Down( "Shift" ) )
		{
			if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net ) || net.Quantity <= 0 )
				return;

			CampfireQuickTransferShiftClick( fromCampfire: false, fromIdx: slotIndex );
			return;
		}

		if ( _campfireDragFromCampfire.HasValue )
			return;

		if ( !inv.TryGetClientMirrorSlot( slotIndex, out var net2 ) || net2.Quantity <= 0 )
			return;

		CampfireBeginDrag( fromCampfire: false, slotIndex );
	}

	void CampfireBeginDrag( bool fromCampfire, int slotIndex )
	{
		_campfireDragFromCampfire = fromCampfire;
		_campfireDragSlot = slotIndex;
		_campfireHoverCampfireSlot = fromCampfire ? slotIndex : (int?)null;
		_campfireHoverPlayerSlot = fromCampfire ? (int?)null : slotIndex;
		_shellDnDSyntheticCursorReady = false;
		CampfireRebuildDragGhost();
		CampfireUpdateDropTargetUnderCursor();
		CampfireRefreshDragDecorations();
	}

	void CampfireOnCampfireSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !CampfireUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_campfireDragFromCampfire.HasValue )
			return;

		CampfireFinalizeDragDropFromCurrentTarget();
	}

	void CampfireOnPlayerSlotMouseUp( int slotIndex, MouseButtons btn )
	{
		if ( !CampfireUiOpen || btn != MouseButtons.Left )
			return;

		if ( !_campfireDragFromCampfire.HasValue )
			return;

		CampfireFinalizeDragDropFromCurrentTarget();
	}

	void CampfireOnLayerMouseUp( PanelEvent e )
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		if ( e.Target != _campfireLayer )
			return;

		CampfireFinalizeDragDropFromCurrentTarget();
	}

	void CampfireQuickTransferShiftClick( bool fromCampfire, int fromIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _campfireStructureId == Guid.Empty )
			return;

		CampfireClearDrag();

		inv.RequestCampfireQuickTransfer(
			_campfireStructureId.ToString( "D" ),
			fromCampfire,
			fromIdx );
	}

	void CampfireFinalizeDragDropAt( int? campfireDropSlot, int? playerDropSlot )
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _campfireStructureId == Guid.Empty )
		{
			CampfireClearDrag();
			return;
		}

		var fromCampfire = _campfireDragFromCampfire.Value;
		var fromIdx = _campfireDragSlot;

		CampfireClearDrag();

		if ( fromCampfire )
		{
			if ( playerDropSlot.HasValue )
				CampfireRequestTransfer( true, fromIdx, false, playerDropSlot.Value );
			else if ( campfireDropSlot.HasValue && campfireDropSlot.Value != fromIdx )
				CampfireRequestTransfer( true, fromIdx, true, campfireDropSlot.Value );
		}
		else
		{
			if ( campfireDropSlot.HasValue )
				CampfireRequestTransfer( false, fromIdx, true, campfireDropSlot.Value );
			else if ( playerDropSlot.HasValue && playerDropSlot.Value != fromIdx )
				CampfireRequestTransfer( false, fromIdx, false, playerDropSlot.Value );
		}
	}

	void CampfireFinalizeDragDropFromCurrentTarget()
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		CampfireUpdateDropTargetUnderCursor();
		CampfireFinalizeDragDropAt( _campfireHoverCampfireSlot, _campfireHoverPlayerSlot );
	}

	void CampfireRequestTransfer( bool fromCampfire, int fromIdx, bool toCampfire, int toIdx )
	{
		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() || _campfireStructureId == Guid.Empty )
			return;

		ThornsInventoryClientTransfer.SubmitCampfireStructuredTransfer(
			inv,
			_campfireStructureId.ToString( "D" ),
			fromCampfire,
			fromIdx,
			toCampfire,
			toIdx );
	}

	void CampfireOnCampfireHoverEnter( int idx )
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		_campfireHoverCampfireSlot = idx;
		_campfireHoverPlayerSlot = null;
		CampfireRefreshDragDecorations();
	}

	void CampfireOnCampfireHoverLeave( int idx )
	{
		if ( _campfireHoverCampfireSlot == idx )
			_campfireHoverCampfireSlot = null;

		CampfireRefreshDragDecorations();
	}

	void CampfireOnPlayerHoverEnter( int idx )
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		_campfireHoverPlayerSlot = idx;
		_campfireHoverCampfireSlot = null;
		CampfireRefreshDragDecorations();
	}

	void CampfireOnPlayerHoverLeave( int idx )
	{
		if ( _campfireHoverPlayerSlot == idx )
			_campfireHoverPlayerSlot = null;

		CampfireRefreshDragDecorations();
	}

	void CampfireRefreshDragDecorations()
	{
		if ( _campfireSlots is null )
			return;

		foreach ( var s in _campfireSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _campfireDragFromCampfire.HasValue && _campfireHoverCampfireSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _campfireDragFromCampfire == true && _campfireDragSlot == s.SlotIndex );
		}

		if ( _campfireOverlayPlayerSlots is null )
			return;

		foreach ( var s in _campfireOverlayPlayerSlots )
		{
			if ( s is null || !s.IsValid )
				continue;

			var h = _campfireDragFromCampfire.HasValue && _campfireHoverPlayerSlot == s.SlotIndex;
			s.SetHighlighted( h );
			s.SetDragSource( _campfireDragFromCampfire == false && _campfireDragSlot == s.SlotIndex );
		}
	}

	void CampfireTickDragReleaseFallback()
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		if ( !(Input.Released( "Attack1" ) || Input.Released( "attack1" )) )
			return;

		CampfireFinalizeDragDropFromCurrentTarget();
	}

	void CampfireUpdateDropTargetUnderCursor()
	{
		if ( !_campfireDragFromCampfire.HasValue )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		ShellAdvanceSyntheticCursorForDnD();
		var screenPos = ShellCurrentMouseScreenPosition();

		int? cf = null;
		int? player = null;

		if ( _campfireSlots is not null )
		{
			foreach ( var s in _campfireSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				cf = s.SlotIndex;
				break;
			}
		}

		if ( !cf.HasValue && _campfireOverlayPlayerSlots is not null )
		{
			foreach ( var s in _campfireOverlayPlayerSlots )
			{
				if ( s is null || !s.IsValid )
					continue;

				if ( !ShellPanelContainsScreenPoint( s, screenPos ) )
					continue;

				player = s.SlotIndex;
				break;
			}
		}

		if ( _campfireHoverCampfireSlot == cf && _campfireHoverPlayerSlot == player )
			return;

		_campfireHoverCampfireSlot = cf;
		_campfireHoverPlayerSlot = player;

		CampfireRefreshDragDecorations();
	}

	void CampfireClearDrag()
	{
		_campfireDragFromCampfire = null;
		_campfireDragSlot = -1;
		_campfireHoverCampfireSlot = null;
		_campfireHoverPlayerSlot = null;
		CampfireDestroyDragGhost();
		CampfireRefreshDragDecorations();
	}

	void CampfireDestroyDragGhost()
	{
		if ( _campfireDragGhost is not null && _campfireDragGhost.IsValid )
			_campfireDragGhost.Delete();

		_campfireDragGhost = null;
	}

	void CampfireRebuildDragGhost()
	{
		CampfireDestroyDragGhost();
		if ( !_campfireDragFromCampfire.HasValue || Panel is null || !Panel.IsValid )
			return;

		ThornsInventorySlotNet net = default;
		if ( _campfireDragFromCampfire.Value )
		{
			if ( !TryGetCampfireMirrorSlot( _campfireDragSlot, out net ) || net.Quantity <= 0 )
				return;
		}
		else
		{
			var inv = Components.Get<ThornsInventory>();
			if ( !inv.IsValid()
			     || !inv.TryGetClientMirrorSlot( _campfireDragSlot, out net )
			     || net.Quantity <= 0 )
				return;
		}

		_campfireDragGhost = ThornsUiPanelAdd.AddChildPanel( ShellCampfireDragGhostHostParent(), "thorns-shell-campfire-drag-ghost-host" );
		ThornsConfigureInventoryDragGhostHost( _campfireDragGhost, zIndex: 520 );
		_campfireDragGhost.Style.MarginLeft = 0;
		_campfireDragGhost.Style.MarginTop = 0;
		_campfireDragGhost.AddClass( "thorns-shell-inv-drag-ghost" );

		var fromPlayerInv = !_campfireDragFromCampfire.Value;
		var toolbar =
			fromPlayerInv && _campfireDragSlot >= 0 && _campfireDragSlot < ThornsInventory.HotbarSlotCount;
		var preview = _campfireDragGhost.AddChild(
			new ThornsUiGridSlot( _campfireDragSlot, toolbar: toolbar ) );
		ThornsConfigureDragGhostPreviewSlot( preview );

		if ( fromPlayerInv && toolbar )
			preview.SetToolbarFromMirror( net, _campfireDragSlot + 1 );
		else
		{
			Color? rowTint = null;
			if ( ThornsUiWeaponInspectFormatting.TryGetWeaponInventoryTitleTint( net, out var wt ) )
				rowTint = wt;
			else if ( ThornsUiArmorInspectFormatting.TryGetArmorInventoryTitleTint( net, out var at ) )
				rowTint = at;
			preview.SetMirrorSlotVisual( net, rowTint );
		}

		UpdateCampfireDragGhostPosition();
	}

	void UpdateCampfireDragGhostPosition()
	{
		if ( _campfireDragGhost is null || !_campfireDragGhost.IsValid || !_campfireDragFromCampfire.HasValue )
			return;

		PositionInventoryDragGhostUnderCursor( _campfireDragGhost );
	}
}
