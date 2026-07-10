namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

/// <summary>Workbench overlay — inventory + draggable item / parts / restored slots.</summary>
public sealed class ThornsWorkbenchHud
{
	readonly Panel _backdrop;
	readonly Panel _root;
	readonly ThornsStationOverlayInventoryPane _inventoryPane;
	readonly ThornsItemSlot _itemSlot;
	readonly ThornsItemSlot _part0Slot;
	readonly ThornsItemSlot _part1Slot;
	readonly ThornsItemSlot _part2Slot;
	readonly ThornsItemSlot _outputSlot;
	readonly Label _itemMeta;
	readonly Label _partsMeta;
	readonly Label _outputMeta;
	readonly Panel _progressTrack;
	readonly Panel _progressFill;
	readonly Label _progress;
	readonly Panel _actions;

	double _nextActionRebuildRealtime;

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	Action<UiRevisionChannel, int> _onRevision;

	public ThornsWorkbenchHud( Panel parent )
	{
		(_backdrop, _root) = ThornsMenuChrome.CreateOverlayShell(
			parent,
			"workbench-hud world-container-hud",
			ThornsUiMetrics.WorldContainerMaxWidthPx );
		_backdrop.AddClass( "workbench-backdrop world-container-backdrop" );
		_backdrop.Style.Display = DisplayMode.None;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.InventoryBuild );

		_root.Style.MinHeight = Length.Pixels( ThornsUiMetrics.CampfireOverlayMinHeightPx );
		_root.Style.MaxHeight = Length.Percent( 88 );
		_root.Style.Overflow = OverflowMode.Visible;

		ThornsTheme.CreateStationOverlayHeader(
			_root,
			out _,
			"WORKBENCH",
			() => ThornsPlayerGameplay.Local?.RequestCloseWorkbench(),
			"workbench-title world-container-title" );

		var hint = ThornsUiFactory.AddPassiveLabel(
			_root,
			"Drag gear and repair parts into the bench · shift-click to transfer · ESC to close",
			"workbench-hint thorns-muted world-container-hint thorns-station-hint" );
		hint.Style.MarginBottom = Length.Pixels( 12 );

		var body = ThornsUiFactory.AddPanel( _root, "workbench-body world-container-body thorns-station-body" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 0;
		body.Style.MinHeight = Length.Pixels( 380 );
		body.Style.Width = Length.Percent( 100 );

		_inventoryPane = new ThornsStationOverlayInventoryPane( body, Refresh );

		ThornsTheme.CreateWoodColumnDivider( body );

		var stationCol = ThornsUiFactory.AddPanel( body, "workbench-station-column world-container-column" );
		stationCol.Style.FlexDirection = FlexDirection.Column;
		stationCol.Style.FlexGrow = 1;
		stationCol.Style.MinWidth = Length.Pixels( 0 );
		stationCol.Style.AlignItems = Align.Stretch;

		var slotsRow = ThornsUiFactory.AddPanel( stationCol, "workbench-slots-row" );
		slotsRow.Style.FlexDirection = FlexDirection.Row;
		slotsRow.Style.FlexGrow = 1;
		slotsRow.Style.MinHeight = Length.Pixels( 220 );
		slotsRow.Style.Width = Length.Percent( 100 );
		slotsRow.Style.AlignItems = Align.Stretch;

		BuildStationColumn( slotsRow, "workbench-item-column", "ITEM", ThornsWorkbenchStationSlots.Item, out _itemSlot, out _itemMeta );
		ThornsTheme.CreateWoodColumnDivider( slotsRow );
		BuildPartsColumn( slotsRow, out _part0Slot, out _part1Slot, out _part2Slot, out _partsMeta );
		ThornsTheme.CreateWoodColumnDivider( slotsRow );
		BuildStationColumn( slotsRow, "workbench-output-column", "RESTORED", ThornsWorkbenchStationSlots.Output, out _outputSlot, out _outputMeta );

		var repairRow = ThornsUiFactory.AddPanel( stationCol, "workbench-repair-row thorns-station-action-panel" );
		repairRow.Style.FlexDirection = FlexDirection.Column;
		repairRow.Style.FlexShrink = 0;
		repairRow.Style.Width = Length.Percent( 100 );
		repairRow.Style.MarginTop = Length.Pixels( 16 );
		repairRow.Style.PaddingLeft = Length.Pixels( 10 );
		repairRow.Style.PaddingRight = Length.Pixels( 10 );

		ThornsTheme.CreateSectionHeader( repairRow, "REPAIRING" );

		_progressTrack = ThornsUiFactory.AddPanel( repairRow, "workbench-progress-track thorns-station-progress-track" );
		_progressTrack.Style.Height = Length.Pixels( 18 );
		_progressTrack.Style.MarginTop = Length.Pixels( 10 );
		_progressTrack.Style.MarginBottom = Length.Pixels( 8 );
		_progressTrack.Style.Overflow = OverflowMode.Hidden;
		_progressTrack.Style.Width = Length.Percent( 100 );
		_progressTrack.Style.Position = PositionMode.Relative;
		_progressTrack.Style.FlexShrink = 0;
		_progressTrack.Style.FlexGrow = 0;
		_progressFill = ThornsUiFactory.AddPanel( _progressTrack, "workbench-progress-fill thorns-station-progress-fill" );
		_progressFill.Style.Position = PositionMode.Absolute;
		_progressFill.Style.Left = Length.Pixels( 0 );
		_progressFill.Style.Top = Length.Pixels( 0 );
		_progressFill.Style.Height = Length.Percent( 100 );
		_progressFill.Style.Width = Length.Percent( 0 );

		_progress = ThornsUiFactory.AddPassiveLabel( repairRow, "", "workbench-progress thorns-muted thorns-station-meta" );
		_progress.Style.WhiteSpace = WhiteSpace.Normal;
		_progress.Style.MarginBottom = Length.Pixels( 8 );

		_actions = ThornsUiFactory.AddPanel( repairRow, "workbench-actions" );
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
		int slotIndex,
		out ThornsItemSlot slot,
		out Label metaLabel )
	{
		var column = ThornsUiFactory.AddPanel( parent, $"workbench-material-column world-container-column {cssClass}" );
		column.Style.FlexDirection = FlexDirection.Column;
		column.Style.FlexGrow = 1;
		column.Style.FlexShrink = 1;
		column.Style.FlexBasis = Length.Pixels( 0 );
		column.Style.MinWidth = Length.Pixels( 0 );
		column.Style.AlignItems = Align.Center;
		column.Style.PaddingLeft = Length.Pixels( 10 );
		column.Style.PaddingRight = Length.Pixels( 10 );

		ThornsTheme.CreateSectionHeader( column, title );

		var slotWrap = ThornsUiFactory.AddPanel( column, "workbench-material-slot station-slot-wrap" );
		slotWrap.Style.MarginTop = Length.Pixels( 12 );
		slotWrap.Style.MarginBottom = Length.Pixels( 10 );
		slotWrap.Style.JustifyContent = Justify.Center;
		slotWrap.Style.AlignItems = Align.Center;

		slot = new ThornsItemSlot( slotWrap, ThornsContainerKind.WorkbenchStation, slotIndex, () => { }, worldContainerLayout: true );

		metaLabel = ThornsUiFactory.AddPassiveLabel( column, "", "workbench-slot-meta thorns-muted thorns-station-meta" );
		metaLabel.Style.WhiteSpace = WhiteSpace.Normal;
		metaLabel.Style.TextAlign = TextAlign.Center;
		metaLabel.Style.Width = Length.Percent( 100 );
	}

	static void BuildPartsColumn(
		Panel parent,
		out ThornsItemSlot part0,
		out ThornsItemSlot part1,
		out ThornsItemSlot part2,
		out Label metaLabel )
	{
		var column = ThornsUiFactory.AddPanel( parent, "workbench-material-column workbench-parts-column world-container-column" );
		column.Style.FlexDirection = FlexDirection.Column;
		column.Style.FlexGrow = 1;
		column.Style.FlexShrink = 1;
		column.Style.FlexBasis = Length.Pixels( 0 );
		column.Style.MinWidth = Length.Pixels( 0 );
		column.Style.AlignItems = Align.Center;
		column.Style.PaddingLeft = Length.Pixels( 10 );
		column.Style.PaddingRight = Length.Pixels( 10 );

		ThornsTheme.CreateSectionHeader( column, "PARTS" );

		var stack = ThornsUiFactory.AddPanel( column, "workbench-parts-stack station-slot-wrap" );
		stack.Style.FlexDirection = FlexDirection.Column;
		stack.Style.AlignItems = Align.Center;
		stack.Style.MarginTop = Length.Pixels( 8 );
		stack.Style.MarginBottom = Length.Pixels( 8 );

		part0 = new ThornsItemSlot( stack, ThornsContainerKind.WorkbenchStation, ThornsWorkbenchStationSlots.Part0, () => { }, worldContainerLayout: true );
		part0.Style.MarginBottom = Length.Pixels( 6 );
		part1 = new ThornsItemSlot( stack, ThornsContainerKind.WorkbenchStation, ThornsWorkbenchStationSlots.Part1, () => { }, worldContainerLayout: true );
		part1.Style.MarginBottom = Length.Pixels( 6 );
		part2 = new ThornsItemSlot( stack, ThornsContainerKind.WorkbenchStation, ThornsWorkbenchStationSlots.Part2, () => { }, worldContainerLayout: true );

		metaLabel = ThornsUiFactory.AddPassiveLabel( column, "", "workbench-slot-meta thorns-muted thorns-station-meta" );
		metaLabel.Style.WhiteSpace = WhiteSpace.Normal;
		metaLabel.Style.TextAlign = TextAlign.Center;
		metaLabel.Style.Width = Length.Percent( 100 );
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel is UiRevisionChannel.Workbench or UiRevisionChannel.Inventory )
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

		var workbench = ThornsUiClientState.Snapshot.Workbench;
		var open = workbench?.IsOpen == true;
		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		if ( !open )
			return;

		_inventoryPane.RefreshSlots();
		_itemSlot.Refresh();
		_part0Slot.Refresh();
		_part1Slot.Refresh();
		_part2Slot.Refresh();
		_outputSlot.Refresh();

		var itemDto = StationSlot( workbench, ThornsWorkbenchStationSlots.Item );
		var repairing = workbench.RepairInProgress;

		if ( itemDto is not null && ThornsItemRegistry.TryGet( itemDto.ItemId, out var def ) )
		{
			var stack = ToStack( itemDto );
			var max = ThornsItemTier.ResolveMaxDurability( stack, def );
			var costs = ThornsItemTier.GetRepairCost( stack, def );
			var canRepair = ThornsItemTier.CanRepair( stack, def );
			var hasParts = HasMaterialsInSnapshot( workbench, costs );

			_itemMeta.Text = BuildItemMeta( stack, def, max );
			_partsMeta.Text = BuildPartsMeta( workbench, costs, repairing );
			_outputMeta.Text = canRepair
				? $"Fully restored\n{max:F0} / {max:F0} HP"
				: "Already at full durability";

			if ( repairing )
			{
				var jobProgress = 1f - Math.Clamp(
					workbench.RepairSecondsRemaining / Math.Max( 0.001f, workbench.RepairSecondsPerJob ),
					0f,
					1f );
				_progressFill.Style.Width = Length.Percent( Math.Clamp( jobProgress, 0.02f, 1f ) * 100f );
				_progress.Text = $"Repairing {def.DisplayName} · {FormatTime( workbench.RepairSecondsRemaining )} remaining";
			}
			else
			{
				_progressFill.Style.Width = Length.Percent( 0 );
				_progress.Text = canRepair
					? hasParts
						? $"Ready — repair takes {workbench.RepairSecondsPerJob:0.#}s"
						: "Drag repair materials into the parts slots."
					: "This item does not need repair.";
			}

			if ( Time.Now >= _nextActionRebuildRealtime || _actions.Children.Count() == 0 )
			{
				_nextActionRebuildRealtime = Time.Now + 0.35;
				RebuildActions( workbench, stack, def, canRepair, hasParts, repairing );
			}
		}
		else
		{
			var outputDto = StationSlot( workbench, ThornsWorkbenchStationSlots.Output );
			_itemMeta.Text = "Drag damaged gear here";
			_partsMeta.Text = "Metal, cloth, leather…";
			_outputMeta.Text = outputDto is not null
				? "Drag restored item to inventory"
				: "Repaired item appears here";
			_progressFill.Style.Width = Length.Percent( 0 );
			_progress.Text = repairing
				? $"Repairing · {FormatTime( workbench.RepairSecondsRemaining )} remaining"
				: "Place an item and parts to begin.";
			_actions.DeleteChildren( true );
		}
	}

	void RebuildActions(
		ThornsWorkbenchSnapshotDto workbench,
		ThornsItemStack stack,
		ThornsItemDefinition def,
		bool canRepair,
		bool hasParts,
		bool repairing )
	{
		_actions.DeleteChildren( true );
		if ( repairing )
			return;

		if ( canRepair && hasParts )
		{
			var repairBtn = ThornsUiFactory.AddClickable( _actions, "thorns-btn-primary workbench-repair-btn", "REPAIR", () =>
				ThornsPlayerGameplay.Local?.RequestStartWorkbenchRepair() );
			repairBtn.Style.MarginRight = Length.Pixels( 10 );
			repairBtn.Style.MarginBottom = Length.Pixels( 10 );
			repairBtn.Style.MinWidth = Length.Pixels( 120 );
		}

		if ( ThornsItemTier.CanUpgrade( stack, def ) )
		{
			var tier = ThornsItemTier.ResolveTier( stack, def );
			if ( HasIngredients( ThornsItemTier.GetUpgradeCost( tier ) ) )
			{
				var nextName = ThornsWeaponTierVisuals.TierName( ThornsItemTier.NextTier( stack, def ) ).ToUpperInvariant();
				var upgradeBtn = ThornsUiFactory.AddClickable( _actions, "thorns-btn-secondary workbench-upgrade-btn",
					$"UPGRADE → {nextName}", () =>
						ThornsPlayerGameplay.Local?.RequestUpgradeItem( new ThornsUpgradeItemRequest
						{
							Container = ThornsContainerKind.WorkbenchStation,
							Index = ThornsWorkbenchStationSlots.Item
						} ) );
				upgradeBtn.Style.MarginRight = Length.Pixels( 10 );
				upgradeBtn.Style.MarginBottom = Length.Pixels( 10 );
			}
		}
	}

	static ThornsInventorySlotDto StationSlot( ThornsWorkbenchSnapshotDto workbench, int index ) =>
		workbench?.StationSlots?.FirstOrDefault( s => s.Index == index );

	static string BuildItemMeta( ThornsItemStack stack, ThornsItemDefinition def, float max )
	{
		var tierName = ThornsWeaponTierVisuals.TierName( ThornsItemTier.ResolveTier( stack, def ) );
		var lines = new List<string> { def.DisplayName, tierName };
		if ( stack.HasDurability && max > 0.001f )
			lines.Add( $"{stack.Durability:F0} / {max:F0} HP" );
		return string.Join( "\n", lines );
	}

	static string BuildPartsMeta( ThornsWorkbenchSnapshotDto workbench, IReadOnlyList<ThornsRecipeIngredient> costs, bool repairing )
	{
		if ( costs.Count == 0 )
			return repairing ? "Parts consumed" : "No parts required";

		var lines = costs.Select( c =>
		{
			var inBench = CountInStation( workbench, c.ItemId );
			return repairing
				? $"Used {c.Count} {ItemName( c.ItemId )}"
				: $"{c.Count} {ItemName( c.ItemId )}\nIn bench: {inBench}";
		} );
		return string.Join( "\n", lines );
	}

	static bool HasMaterialsInSnapshot( ThornsWorkbenchSnapshotDto workbench, IReadOnlyList<ThornsRecipeIngredient> costs )
	{
		foreach ( var cost in costs )
		{
			if ( CountInStation( workbench, cost.ItemId ) < cost.Count )
				return false;
		}

		return true;
	}

	static int CountInStation( ThornsWorkbenchSnapshotDto workbench, string itemId )
	{
		var total = 0;
		foreach ( var slot in workbench?.StationSlots ?? [] )
		{
			if ( slot.Index is >= ThornsWorkbenchStationSlots.FirstPart
			     and < ThornsWorkbenchStationSlots.FirstPart + ThornsWorkbenchStationSlots.PartCount
			     && string.Equals( slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				total += slot.Count;
		}

		return total;
	}

	static ThornsItemStack ToStack( ThornsInventorySlotDto slot ) => new()
	{
		ItemId = slot.ItemId,
		Count = slot.Count,
		HasDurability = slot.HasDurability,
		Durability = slot.Durability,
		ItemTier = slot.ItemTier > 0 ? slot.ItemTier : slot.WeaponTier,
		StatRoll = slot.StatRoll,
		WeaponLoadedAmmo = slot.WeaponLoadedAmmo
	};

	static bool HasIngredients( IEnumerable<ThornsRecipeIngredient> costs )
	{
		foreach ( var cost in costs )
		{
			if ( CountInInventory( cost.ItemId ) < cost.Count )
				return false;
		}

		return true;
	}

	static string ItemName( string itemId ) =>
		ThornsItemRegistry.TryGet( itemId, out var def ) ? def.DisplayName : itemId;

	static int CountInInventory( string itemId )
	{
		var total = 0;
		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory?.Slots ?? [] )
		{
			if ( string.Equals( slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				total += slot.Count;
		}

		return total;
	}

	static string FormatTime( float seconds )
	{
		var total = Math.Max( 0, (int)MathF.Ceiling( seconds ) );
		var mm = total / 60;
		var ss = total % 60;
		return $"{mm:00}:{ss:00}";
	}
}
