namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

/// <summary>Campfire smelting overlay — inventory + draggable wood / ore / ingot slots.</summary>
public sealed class ThornsCampfireHud
{
	readonly Panel _backdrop;
	readonly Panel _root;
	readonly ThornsStationOverlayInventoryPane _inventoryPane;
	readonly ThornsItemSlot _woodSlot;
	readonly ThornsItemSlot _oreSlot;
	readonly ThornsItemSlot _outputSlot;
	readonly Label _woodMeta;
	readonly Label _oreMeta;
	readonly Label _outputMeta;
	readonly Panel _progressTrack;
	readonly Panel _progressFill;
	readonly Label _progress;
	readonly Panel _actions;

	double _nextActionRebuildRealtime;

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	Action<UiRevisionChannel, int> _onRevision;

	public ThornsCampfireHud( Panel parent )
	{
		(_backdrop, _root) = ThornsMenuChrome.CreateOverlayShell(
			parent,
			"campfire-hud world-container-hud",
			ThornsUiMetrics.WorldContainerMaxWidthPx );
		_backdrop.AddClass( "campfire-backdrop world-container-backdrop" );
		_backdrop.Style.Display = DisplayMode.None;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.InventoryBuild );

		_root.Style.MinHeight = Length.Pixels( ThornsUiMetrics.CampfireOverlayMinHeightPx );
		_root.Style.MaxHeight = Length.Percent( 88 );
		_root.Style.Overflow = OverflowMode.Visible;

		ThornsTheme.CreateStationOverlayHeader(
			_root,
			out _,
			"CAMPFIRE FORGE",
			() => ThornsPlayerGameplay.Local?.RequestCloseCampfire(),
			"campfire-title world-container-title" );

		var hint = ThornsUiFactory.AddPassiveLabel(
			_root,
			"Drag wood and ore into the fire · shift-click to transfer · ESC to close",
			"campfire-hint thorns-muted world-container-hint thorns-station-hint" );
		hint.Style.MarginBottom = Length.Pixels( 12 );

		var body = ThornsUiFactory.AddPanel( _root, "campfire-body world-container-body thorns-station-body" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 0;
		body.Style.MinHeight = Length.Pixels( 380 );
		body.Style.Width = Length.Percent( 100 );

		_inventoryPane = new ThornsStationOverlayInventoryPane( body, Refresh );

		ThornsTheme.CreateWoodColumnDivider( body );

		var stationCol = ThornsUiFactory.AddPanel( body, "campfire-station-column world-container-column" );
		stationCol.Style.FlexDirection = FlexDirection.Column;
		stationCol.Style.FlexGrow = 1;
		stationCol.Style.MinWidth = Length.Pixels( 0 );
		stationCol.Style.AlignItems = Align.Stretch;

		var slotsRow = ThornsUiFactory.AddPanel( stationCol, "campfire-slots-row" );
		slotsRow.Style.FlexDirection = FlexDirection.Row;
		slotsRow.Style.FlexGrow = 1;
		slotsRow.Style.MinHeight = Length.Pixels( 220 );
		slotsRow.Style.Width = Length.Percent( 100 );
		slotsRow.Style.AlignItems = Align.Stretch;

		BuildStationColumn( slotsRow, "campfire-wood-column", "WOOD", ThornsContainerKind.CampfireStation, ThornsCampfireStationSlots.Wood, out _woodSlot, out _woodMeta );
		ThornsTheme.CreateWoodColumnDivider( slotsRow );
		BuildStationColumn( slotsRow, "campfire-ore-column", "ORE", ThornsContainerKind.CampfireStation, ThornsCampfireStationSlots.Ore, out _oreSlot, out _oreMeta );
		ThornsTheme.CreateWoodColumnDivider( slotsRow );
		BuildStationColumn( slotsRow, "campfire-output-column", "INGOT", ThornsContainerKind.CampfireStation, ThornsCampfireStationSlots.Output, out _outputSlot, out _outputMeta );

		var forgeRow = ThornsUiFactory.AddPanel( stationCol, "campfire-forge-row thorns-station-action-panel" );
		forgeRow.Style.FlexDirection = FlexDirection.Column;
		forgeRow.Style.FlexShrink = 0;
		forgeRow.Style.Width = Length.Percent( 100 );
		forgeRow.Style.MarginTop = Length.Pixels( 16 );
		forgeRow.Style.PaddingLeft = Length.Pixels( 10 );
		forgeRow.Style.PaddingRight = Length.Pixels( 10 );

		ThornsTheme.CreateSectionHeader( forgeRow, "SMELTING" );

		_progressTrack = ThornsUiFactory.AddPanel( forgeRow, "campfire-progress-track thorns-station-progress-track" );
		_progressTrack.Style.Height = Length.Pixels( 18 );
		_progressTrack.Style.MarginTop = Length.Pixels( 10 );
		_progressTrack.Style.MarginBottom = Length.Pixels( 8 );
		_progressTrack.Style.Overflow = OverflowMode.Hidden;
		_progressTrack.Style.Width = Length.Percent( 100 );
		_progressTrack.Style.Position = PositionMode.Relative;
		_progressTrack.Style.FlexShrink = 0;
		_progressTrack.Style.FlexGrow = 0;
		_progressFill = ThornsUiFactory.AddPanel( _progressTrack, "campfire-progress-fill thorns-station-progress-fill" );
		_progressFill.Style.Position = PositionMode.Absolute;
		_progressFill.Style.Left = Length.Pixels( 0 );
		_progressFill.Style.Top = Length.Pixels( 0 );
		_progressFill.Style.Height = Length.Percent( 100 );
		_progressFill.Style.Width = Length.Percent( 0 );

		_progress = ThornsUiFactory.AddPassiveLabel( forgeRow, "", "campfire-progress thorns-muted thorns-station-meta" );
		_progress.Style.WhiteSpace = WhiteSpace.Normal;
		_progress.Style.MarginBottom = Length.Pixels( 8 );

		_actions = ThornsUiFactory.AddPanel( forgeRow, "campfire-actions" );
		_actions.Style.FlexDirection = FlexDirection.Row;
		_actions.Style.FlexWrap = Wrap.Wrap;
		_actions.Style.MarginTop = Length.Pixels( 4 );

		_backdrop.AddEventListener( "onmouseup", OnMouseUp );
		_root.AddEventListener( "onmouseup", OnMouseUp );

		_onRevision = OnRevision;
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	static void BuildStationColumn(
		Panel parent,
		string cssClass,
		string title,
		ThornsContainerKind container,
		int slotIndex,
		out ThornsItemSlot slot,
		out Label metaLabel )
	{
		var column = ThornsUiFactory.AddPanel( parent, $"campfire-material-column world-container-column {cssClass}" );
		column.Style.FlexDirection = FlexDirection.Column;
		column.Style.FlexGrow = 1;
		column.Style.FlexShrink = 1;
		column.Style.FlexBasis = Length.Pixels( 0 );
		column.Style.MinWidth = Length.Pixels( 0 );
		column.Style.AlignItems = Align.Center;
		column.Style.PaddingLeft = Length.Pixels( 10 );
		column.Style.PaddingRight = Length.Pixels( 10 );

		ThornsTheme.CreateSectionHeader( column, title );

		var slotWrap = ThornsUiFactory.AddPanel( column, "campfire-material-slot station-slot-wrap" );
		slotWrap.Style.MarginTop = Length.Pixels( 12 );
		slotWrap.Style.MarginBottom = Length.Pixels( 10 );
		slotWrap.Style.JustifyContent = Justify.Center;
		slotWrap.Style.AlignItems = Align.Center;

		slot = new ThornsItemSlot( slotWrap, container, slotIndex, () => { }, worldContainerLayout: true );

		metaLabel = ThornsUiFactory.AddPassiveLabel( column, "", "campfire-slot-meta thorns-muted thorns-station-meta" );
		metaLabel.Style.WhiteSpace = WhiteSpace.Normal;
		metaLabel.Style.TextAlign = TextAlign.Center;
		metaLabel.Style.Width = Length.Percent( 100 );
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel is UiRevisionChannel.Campfire or UiRevisionChannel.Inventory )
			Refresh();
	}

	static void OnMouseUp( PanelEvent e )
	{
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

	public void Refresh()
	{
		if ( !_backdrop.IsValid() )
			return;

		var campfire = ThornsUiClientState.Snapshot.Campfire;
		var open = campfire?.IsOpen == true;
		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		if ( !open )
			return;

		_inventoryPane.RefreshSlots();
		_woodSlot.Refresh();
		_oreSlot.Refresh();
		_outputSlot.Refresh();

		var woodCount = StationCount( campfire, ThornsCampfireStationSlots.Wood );
		var oreCount = StationCount( campfire, ThornsCampfireStationSlots.Ore );
		var ingotCount = StationCount( campfire, ThornsCampfireStationSlots.Output );
		var maxBatches = ThornsCampfireSmelt.MaxAffordableBatches( oreCount, woodCount );

		_woodMeta.Text = $"{campfire.WoodPerBatch} per batch\nIn fire: {woodCount}";
		_oreMeta.Text = $"{campfire.OrePerIngot} per ingot\nIn fire: {oreCount}";
		_outputMeta.Text = campfire.SmeltBatchesRemaining > 0
			? $"Processing {campfire.SmeltBatchesRemaining} batch(es)\nReady: {ingotCount}"
			: $"Drag ingots out\nReady: {ingotCount}";

		if ( campfire.SmeltBatchesRemaining > 0 )
		{
			var batchProgress = 1f - Math.Clamp(
				campfire.SmeltSecondsRemaining / Math.Max( 0.001f, campfire.SmeltSecondsPerBatch ),
				0f,
				1f );
			_progressFill.Style.Width = Length.Percent( Math.Clamp( batchProgress, 0.02f, 1f ) * 100f );
			_progress.Text =
				$"Smelting batch {campfire.SmeltBatchesRemaining} in queue · {FormatTime( campfire.SmeltSecondsRemaining )} left on current ingot";
		}
		else
		{
			_progressFill.Style.Width = Length.Percent( 0 );
			_progress.Text = maxBatches > 0
				? $"Ready — {campfire.WoodPerBatch} wood + {campfire.OrePerIngot} ore → {campfire.IngotPerBatch} ingot ({campfire.SmeltSecondsPerBatch:0.#}s each)"
				: BuildBlockedMessage( woodCount, oreCount, campfire );
		}

		if ( Time.Now < _nextActionRebuildRealtime && _actions.Children.Count() > 0 )
			return;

		_nextActionRebuildRealtime = Time.Now + 0.35;
		RebuildActions( maxBatches );
	}

	static int StationCount( ThornsCampfireSnapshotDto campfire, int index ) =>
		campfire?.StationSlots?.FirstOrDefault( s => s.Index == index )?.Count ?? 0;

	static string BuildBlockedMessage( int woodCount, int oreCount, ThornsCampfireSnapshotDto campfire )
	{
		if ( woodCount < campfire.WoodPerBatch && oreCount < campfire.OrePerIngot )
			return $"Drag {campfire.WoodPerBatch} wood and {campfire.OrePerIngot} ore into the fire.";

		if ( woodCount < campfire.WoodPerBatch )
			return $"Need at least {campfire.WoodPerBatch} wood in the fire slot.";

		return $"Need at least {campfire.OrePerIngot} ore in the ore slot.";
	}

	void RebuildActions( int maxBatches )
	{
		_actions.DeleteChildren( true );
		if ( maxBatches <= 0 )
			return;

		AddSmeltButton( "Smelt ×1", 1 );
		if ( maxBatches >= 5 )
			AddSmeltButton( "Smelt ×5", 5 );
		if ( maxBatches >= 10 )
			AddSmeltButton( "Smelt ×10", 10 );
		if ( maxBatches > 1 )
			AddSmeltButton( $"Smelt all ({maxBatches})", maxBatches );
	}

	void AddSmeltButton( string label, int batches )
	{
		var btn = ThornsUiFactory.AddClickable( _actions, "thorns-btn-primary campfire-smelt-btn", label, () =>
			ThornsPlayerGameplay.Local?.RequestStartCampfireSmelt( batches ) );
		btn.Style.MarginRight = Length.Pixels( 10 );
		btn.Style.MarginBottom = Length.Pixels( 10 );
		btn.Style.MinWidth = Length.Pixels( 120 );
		btn.Style.PaddingTop = Length.Pixels( 10 );
		btn.Style.PaddingBottom = Length.Pixels( 10 );
		btn.Style.PaddingLeft = Length.Pixels( 16 );
		btn.Style.PaddingRight = Length.Pixels( 16 );
	}

	static string FormatTime( float seconds )
	{
		var total = Math.Max( 0, (int)MathF.Ceiling( seconds ) );
		var mm = total / 60;
		var ss = total % 60;
		return $"{mm:00}:{ss:00}";
	}
}
