namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed partial class ThornsInventoryScreen
{
	const float CarriedWeightCapKg = 100f;

	static readonly (string Key, string Label, string IconKey)[] InventoryFilters =
	{
		("all", "All", "filter_all"),
		("resource", "Building", "filter_building"),
		("weapon", "Weapons", "filter_weapons"),
		("tool", "Tools", "filter_tools"),
		("armor", "Apparel", "filter_apparel"),
		("consumable", "Consumables", "filter_consumables"),
	};

	Panel _contentRow;
	Panel _gridsSection;
	Panel _conceptGridArea;
	Panel _conceptHotbarDock;
	Panel _conceptGridHost;
	Panel _conceptGrid;
	Panel _conceptHotbar;
	int _appliedConceptSlotPx = -1;

	Panel _armorColumn;
	Panel _craftFooterBtn;

	Panel _inspectCompactLayout;
	Panel _inspectWeaponLayout;
	Panel _inspectWeaponLeftCol;
	Panel _inspectWeaponPreviewWrap;
	Panel _inspectWeaponIdentityCol;
	Panel _inspectWeaponLeftMeta;
	ThornsScenePreviewPanel _inspectWeaponPreview;
	Label _inspectWeaponDesc;
	Label _inspectWeaponTitle;
	Panel _inspectWeaponActions;
	Panel _inspectWeaponCenterCol;
	Panel _inspectWeaponStats;
	Panel _inspectWeaponMeta;
	Label _inspectWeaponTier;
	Panel _inspectWeaponRightCol;
	Panel _inspectWeaponAttachRow;
	readonly List<ThornsAttachmentInspectSlot> _weaponAttachmentSlots = new();

	void BuildArmorColumn()
	{
		_armorColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-left inventory-armor-column inventory-armor-column-concept", flexWeight: 30f );
		_armorColumn.Style.FlexDirection = FlexDirection.Column;
		_armorColumn.Style.MinHeight = Length.Pixels( 0 );

		ThornsTheme.CreateSectionHeader( _armorColumn, "ARMOR", "inventory-armor-header" );

		_armorSlotsCol = ThornsUiFactory.AddPanel( _armorColumn, "inventory-armor-slots-col inventory-armor-slots-concept" );
		_armorSlotsCol.Style.FlexDirection = FlexDirection.Column;
		_armorSlotsCol.Style.FlexShrink = 1;
		_armorSlotsCol.Style.FlexGrow = 1;
		_armorSlotsCol.Style.JustifyContent = Justify.SpaceEvenly;
		_armorSlotsCol.Style.AlignItems = Align.Stretch;
		_armorSlotsCol.Style.Width = Length.Percent( 100 );
		_armorSlotsCol.Style.MinHeight = Length.Pixels( 0 );
		_armorSlotsCol.Style.PaddingTop = Length.Pixels( 4 );
		_armorSlotsCol.Style.PaddingBottom = Length.Pixels( 4 );

		_headSlot = CreateLabeledArmorSlot( _armorSlotsCol, ThornsContainerKind.Head, "HEAD" );
		_chestSlot = CreateLabeledArmorSlot( _armorSlotsCol, ThornsContainerKind.Chest, "CHEST" );
		_legsSlot = CreateLabeledArmorSlot( _armorSlotsCol, ThornsContainerKind.Legs, "LEGS" );

		_statsPanel = ThornsUiFactory.AddPanel( _armorColumn, "player-stats-panel inventory-resist-panel inventory-resist-panel-concept" );
		_statsPanel.Style.FlexDirection = FlexDirection.Column;
		_statsPanel.Style.FlexShrink = 0;
		_statsPanel.Style.Padding = Length.Pixels( 12 );
		_statsPanel.Style.MarginBottom = Length.Pixels( 8 );

		RebuildArmorAttributes();
	}

	void BuildInventoryCenterColumn()
	{
		var center = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-center inventory-center inventory-center-concept", flexWeight: 40f );
		center.Style.FlexDirection = FlexDirection.Column;
		center.Style.MinHeight = Length.Pixels( 0 );
		center.Style.Overflow = OverflowMode.Hidden;

		ThornsTheme.CreateSectionHeader( center, "INVENTORY", "inventory-center-header" );

		_inventoryFilterRow = BuildFilterRow( center, "inventory-filter-row inventory-concept-hidden", InventoryFilters, key =>
		{
			_inventoryFilterKey = key;
			UpdateInventoryFilterButtons();
			ApplyInventoryFilter();
		}, () => _inventoryFilterKey );

		_inspectPanel = ThornsUiFactory.AddPanel( center, "inventory-inspect inventory-inspect-compact inventory-inspect-concept" );
		_inspectPanel.Style.Display = DisplayMode.Flex;
		_inspectPanel.Style.FlexDirection = FlexDirection.Column;
		_inspectPanel.Style.FlexGrow = 0;
		_inspectPanel.Style.FlexShrink = 0;
		_inspectPanel.Style.Width = Length.Percent( 100 );
		_inspectPanel.Style.Height = Length.Pixels( ThornsUiMetrics.MenuWeaponInspectPanelHeight );
		_inspectPanel.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuWeaponInspectPanelHeight );
		_inspectPanel.Style.MarginBottom = Length.Pixels( 10 );
		_inspectPanel.Style.Padding = Length.Pixels( 10 );
		_inspectPanel.Style.AlignItems = Align.Stretch;
		_inspectPanel.Style.Overflow = OverflowMode.Hidden;
		_inspectPanel.Style.Position = PositionMode.Relative;
		ThornsTheme.ApplyConceptSection( _inspectPanel );

		BuildInspectCompactLayout();
		BuildInspectWeaponLayout();

		var gridsSection = ThornsUiFactory.AddPanel( center, "inventory-grids-section inventory-grids-section-concept" );
		_gridsSection = gridsSection;
		gridsSection.Style.FlexDirection = FlexDirection.Column;
		gridsSection.Style.FlexGrow = 1;
		gridsSection.Style.FlexShrink = 1;
		gridsSection.Style.MinHeight = Length.Pixels( 0 );
		gridsSection.Style.Overflow = OverflowMode.Hidden;
		gridsSection.Style.BackgroundColor = Color.Transparent;
		gridsSection.Style.JustifyContent = Justify.FlexStart;
		gridsSection.Style.AlignItems = Align.Center;

		var gridArea = ThornsUiFactory.AddPanel( gridsSection, "inventory-grid-area inventory-grid-area-concept" );
		_conceptGridArea = gridArea;
		gridArea.Style.FlexDirection = FlexDirection.Column;
		gridArea.Style.FlexGrow = 1;
		gridArea.Style.FlexShrink = 1;
		gridArea.Style.MinHeight = Length.Pixels( 0 );
		gridArea.Style.JustifyContent = Justify.FlexStart;
		gridArea.Style.AlignItems = Align.Center;
		gridArea.Style.Width = Length.Percent( 100 );
		gridArea.Style.Overflow = OverflowMode.Hidden;

		var gridHost = ThornsUiFactory.AddPanel( gridArea, "inventory-grid-host" );
		_conceptGridHost = gridHost;
		gridHost.Style.FlexDirection = FlexDirection.Column;
		gridHost.Style.FlexGrow = 0;
		gridHost.Style.FlexShrink = 0;
		gridHost.Style.MinHeight = Length.Pixels( 0 );
		gridHost.Style.JustifyContent = Justify.FlexStart;
		gridHost.Style.AlignItems = Align.Center;
		gridHost.Style.Width = Length.Percent( 100 );

		var grid = ThornsUiFactory.AddPanel( gridHost, "inventory-grid-6x5 inventory-grid-concept" );
		_conceptGrid = grid;
		grid.Style.FlexDirection = FlexDirection.Column;
		grid.Style.AlignItems = Align.Center;
		grid.Style.AlignSelf = Align.Center;
		grid.Style.FlexShrink = 0;
		grid.Style.Width = Length.Percent( 100 );

		for ( var row = 0; row < ThornsUiMetrics.MenuInventoryRows; row++ )
		{
			var rowPanel = ThornsUiFactory.AddPanel( grid, "inventory-grid-row" );
			rowPanel.Style.FlexDirection = FlexDirection.Row;
			rowPanel.Style.FlexWrap = Wrap.NoWrap;
			rowPanel.Style.JustifyContent = Justify.Center;
			rowPanel.Style.AlignItems = Align.Stretch;
			rowPanel.Style.FlexShrink = 0;
			rowPanel.Style.Width = Length.Percent( 100 );
			rowPanel.Style.MarginBottom = Length.Pixels( ThornsUiMetrics.MenuInventoryGridGap );

			for ( var col = 0; col < ThornsUiMetrics.MenuInventoryColumns; col++ )
			{
				var slot = CreateSlot( rowPanel, ThornsContainerKind.Inventory, row * ThornsUiMetrics.MenuInventoryColumns + col );
				slot.Style.FlexShrink = 0;
				if ( col > 0 )
					slot.Style.MarginLeft = Length.Pixels( ThornsUiMetrics.MenuInventoryGridGap );
				_inventorySlots.Add( slot );
			}
		}

		var hotbarDock = ThornsUiFactory.AddPanel( gridsSection, "inventory-hotbar-dock inventory-hotbar-dock-concept" );
		_conceptHotbarDock = hotbarDock;
		hotbarDock.Style.FlexDirection = FlexDirection.Column;
		hotbarDock.Style.FlexShrink = 0;
		hotbarDock.Style.FlexGrow = 0;
		hotbarDock.Style.AlignItems = Align.Center;
		hotbarDock.Style.Width = Length.Percent( 100 );
		hotbarDock.Style.PaddingBottom = Length.Pixels( 4 );

		ThornsTheme.CreateSectionHeader( hotbarDock, "HOTBAR", "inventory-hotbar-header inventory-hotbar-header-concept" );

		var hotbarSection = ThornsUiFactory.AddPanel( hotbarDock, "inventory-hotbar-section" );
		hotbarSection.Style.FlexDirection = FlexDirection.Column;
		hotbarSection.Style.FlexShrink = 0;
		hotbarSection.Style.AlignItems = Align.Center;
		hotbarSection.Style.Width = Length.Percent( 100 );
		hotbarSection.Style.MarginTop = Length.Pixels( 0 );

		var hotbar = ThornsUiFactory.AddPanel( hotbarSection, "thorns-hotbar inventory-hotbar inventory-hotbar-concept" );
		_conceptHotbar = hotbar;
		hotbar.Style.FlexDirection = FlexDirection.Row;
		hotbar.Style.FlexWrap = Wrap.NoWrap;
		hotbar.Style.FlexShrink = 0;
		hotbar.Style.Width = Length.Percent( 100 );
		hotbar.Style.JustifyContent = Justify.Center;

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
		{
			var slotCell = CreateSlot( hotbar, ThornsContainerKind.Hotbar, i, isHotbar: true );
			slotCell.Style.FlexShrink = 0;
			if ( i > 0 )
				slotCell.Style.MarginLeft = Length.Pixels( ThornsUiMetrics.MenuInventoryGridGap );
			_hotbarSlots.Add( slotCell );
		}
	}

	void BuildInspectCompactLayout()
	{
		_inspectCompactLayout = ThornsUiFactory.AddPanel( _inspectPanel, "inspect-compact-layout" );
		_inspectCompactLayout.Style.Display = DisplayMode.Flex;
		_inspectCompactLayout.Style.FlexDirection = FlexDirection.Row;
		_inspectCompactLayout.Style.Position = PositionMode.Absolute;
		_inspectCompactLayout.Style.Left = Length.Pixels( 0 );
		_inspectCompactLayout.Style.Top = Length.Pixels( 0 );
		_inspectCompactLayout.Style.Right = Length.Pixels( 0 );
		_inspectCompactLayout.Style.Bottom = Length.Pixels( 0 );
		_inspectCompactLayout.Style.Width = Length.Percent( 100 );
		_inspectCompactLayout.Style.Height = Length.Percent( 100 );
		_inspectCompactLayout.Style.AlignItems = Align.Stretch;
		_inspectCompactLayout.Style.Overflow = OverflowMode.Hidden;

		_inspectIconWrap = ThornsUiFactory.AddPanel( _inspectCompactLayout, "inspect-icon-wrap" );
		_inspectIconWrap.Style.FlexShrink = 0;
		_inspectIconWrap.Style.Width = Length.Pixels( 88 );
		_inspectIconWrap.Style.MinWidth = Length.Pixels( 88 );

		_inspectIcon = ThornsUiFactory.AddPanel( _inspectIconWrap, "inspect-icon slot-icon" );
		_inspectIcon.Style.Width = Length.Pixels( 72 );
		_inspectIcon.Style.Height = Length.Pixels( 72 );
		_inspectIcon.Style.FlexShrink = 0;

		var inspectBody = ThornsUiFactory.AddPanel( _inspectCompactLayout, "inspect-body" );
		inspectBody.Style.FlexDirection = FlexDirection.Column;
		inspectBody.Style.FlexGrow = 1;
		inspectBody.Style.FlexShrink = 1;
		inspectBody.Style.MinWidth = Length.Pixels( 0 );
		inspectBody.Style.Overflow = OverflowMode.Hidden;
		inspectBody.Style.MarginLeft = Length.Pixels( 12 );

		var inspectHeader = ThornsUiFactory.AddPanel( inspectBody, "inspect-header" );
		inspectHeader.Style.FlexDirection = FlexDirection.Row;
		inspectHeader.Style.AlignItems = Align.Center;
		inspectHeader.Style.FlexShrink = 0;
		inspectHeader.Style.MarginBottom = Length.Pixels( 6 );

		_inspectTitle = ThornsUiFactory.AddLabel( inspectHeader, "SELECT AN ITEM", "thorns-header inspect-title" );
		_inspectTitle.Style.FlexShrink = 0;
		_inspectTitle.Style.FlexGrow = 1;

		_inspectTier = ThornsUiFactory.AddPassiveLabel( inspectHeader, "", "inspect-tier-pill" );
		_inspectTier.Style.Display = DisplayMode.None;
		_inspectTier.Style.FlexShrink = 0;
		_inspectTier.Style.MarginLeft = Length.Pixels( 10 );

		_inspectStats = ThornsUiFactory.AddPanel( inspectBody, "inspect-stats" );
		_inspectStats.Style.FlexDirection = FlexDirection.Column;
		_inspectStats.Style.FlexGrow = 1;
		_inspectStats.Style.FlexShrink = 1;
		_inspectStats.Style.MinHeight = Length.Pixels( 0 );
		_inspectStats.Style.Overflow = OverflowMode.Scroll;

		_inspectMeta = ThornsUiFactory.AddPanel( inspectBody, "inspect-meta" );
		_inspectMeta.Style.FlexShrink = 0;
		_inspectMeta.Style.MarginTop = Length.Pixels( 6 );

		_inspectActions = ThornsUiFactory.AddPanel( inspectBody, "inspect-actions" );
		_inspectActions.Style.FlexDirection = FlexDirection.Row;
		_inspectActions.Style.MarginTop = Length.Pixels( 8 );
		_inspectActions.Style.FlexShrink = 0;
	}

	void BuildInspectWeaponLayout()
	{
		const int attachmentSlotPx = 42;

		_inspectWeaponLayout = ThornsUiFactory.AddPanel( _inspectPanel, "inspect-weapon-layout inventory-inspect-weapon" );
		_inspectWeaponLayout.Style.Display = DisplayMode.None;
		_inspectWeaponLayout.Style.FlexDirection = FlexDirection.Row;
		_inspectWeaponLayout.Style.Position = PositionMode.Absolute;
		_inspectWeaponLayout.Style.Left = Length.Pixels( 0 );
		_inspectWeaponLayout.Style.Top = Length.Pixels( 0 );
		_inspectWeaponLayout.Style.Right = Length.Pixels( 0 );
		_inspectWeaponLayout.Style.Bottom = Length.Pixels( 0 );
		_inspectWeaponLayout.Style.Width = Length.Percent( 100 );
		_inspectWeaponLayout.Style.Height = Length.Percent( 100 );
		_inspectWeaponLayout.Style.AlignItems = Align.Stretch;
		_inspectWeaponLayout.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponLayout.Style.MinHeight = Length.Pixels( 0 );

		_inspectWeaponLeftCol = ThornsUiFactory.AddPanel( _inspectWeaponLayout, "inspect-weapon-left" );
		_inspectWeaponLeftCol.Style.FlexDirection = FlexDirection.Column;
		_inspectWeaponLeftCol.Style.FlexShrink = 0;
		_inspectWeaponLeftCol.Style.FlexGrow = 0;
		_inspectWeaponLeftCol.Style.Width = Length.Percent( 30 );
		_inspectWeaponLeftCol.Style.MinWidth = Length.Pixels( 160 );
		_inspectWeaponLeftCol.Style.MaxWidth = Length.Percent( 34 );
		_inspectWeaponLeftCol.Style.AlignItems = Align.Stretch;
		_inspectWeaponLeftCol.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponLeftCol.Style.PaddingRight = Length.Pixels( 10 );

		var leftTopRow = ThornsUiFactory.AddPanel( _inspectWeaponLeftCol, "inspect-weapon-left-top" );
		leftTopRow.Style.FlexDirection = FlexDirection.Row;
		leftTopRow.Style.FlexShrink = 0;
		leftTopRow.Style.AlignItems = Align.Stretch;
		leftTopRow.Style.Overflow = OverflowMode.Hidden;
		leftTopRow.Style.MinHeight = Length.Pixels( 0 );
		leftTopRow.Style.MaxHeight = Length.Pixels( 76 );
		leftTopRow.Style.Height = Length.Pixels( 76 );

		_inspectWeaponPreviewWrap = ThornsUiFactory.AddPanel( leftTopRow, "inspect-weapon-preview-wrap" );
		_inspectWeaponPreviewWrap.Style.FlexShrink = 0;
		_inspectWeaponPreviewWrap.Style.Width = Length.Pixels( 56 );
		_inspectWeaponPreviewWrap.Style.MinWidth = Length.Pixels( 56 );
		_inspectWeaponPreviewWrap.Style.Height = Length.Pixels( 76 );
		_inspectWeaponPreviewWrap.Style.Overflow = OverflowMode.Hidden;

		_inspectWeaponPreview = new ThornsScenePreviewPanel( _inspectWeaponPreviewWrap );
		_inspectWeaponPreview.ConfigureWeaponInspectPreview();

		_inspectWeaponIdentityCol = ThornsUiFactory.AddPanel( leftTopRow, "inspect-weapon-identity" );
		_inspectWeaponIdentityCol.Style.FlexDirection = FlexDirection.Column;
		_inspectWeaponIdentityCol.Style.FlexGrow = 1;
		_inspectWeaponIdentityCol.Style.FlexShrink = 1;
		_inspectWeaponIdentityCol.Style.MinWidth = Length.Pixels( 0 );
		_inspectWeaponIdentityCol.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponIdentityCol.Style.MarginLeft = Length.Pixels( 8 );

		_inspectWeaponTitle = ThornsUiFactory.AddLabel( _inspectWeaponIdentityCol, "", "inspect-weapon-title thorns-header" );
		_inspectWeaponTitle.Style.FlexShrink = 0;
		_inspectWeaponTitle.Style.FontSize = Length.Pixels( 15 );
		_inspectWeaponTitle.Style.LetterSpacing = Length.Pixels( 1 );
		_inspectWeaponTitle.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponTitle.Style.TextOverflow = TextOverflow.Ellipsis;
		_inspectWeaponTitle.Style.WhiteSpace = WhiteSpace.NoWrap;

		_inspectWeaponDesc = ThornsUiFactory.AddPassiveLabel( _inspectWeaponIdentityCol, "", "inspect-weapon-desc thorns-muted" );
		_inspectWeaponDesc.Style.FlexShrink = 1;
		_inspectWeaponDesc.Style.FlexGrow = 1;
		_inspectWeaponDesc.Style.WhiteSpace = WhiteSpace.Normal;
		_inspectWeaponDesc.Style.FontSize = Length.Pixels( 10 );
		_inspectWeaponDesc.Style.LineHeight = Length.Pixels( 13 );
		_inspectWeaponDesc.Style.MaxHeight = Length.Pixels( 52 );
		_inspectWeaponDesc.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponDesc.Style.MarginTop = Length.Pixels( 4 );

		_inspectWeaponLeftMeta = ThornsUiFactory.AddPanel( _inspectWeaponLeftCol, "inspect-meta inspect-weapon-left-meta" );
		_inspectWeaponLeftMeta.Style.FlexShrink = 0;
		_inspectWeaponLeftMeta.Style.MarginTop = Length.Pixels( 6 );
		_inspectWeaponLeftMeta.Style.Overflow = OverflowMode.Hidden;

		_inspectWeaponActions = ThornsUiFactory.AddPanel( _inspectWeaponLeftCol, "inspect-weapon-actions" );
		_inspectWeaponActions.Style.FlexShrink = 0;
		_inspectWeaponActions.Style.MarginTop = Length.Pixels( 6 );
		_inspectWeaponActions.Style.Width = Length.Percent( 100 );
		_inspectWeaponActions.Style.Overflow = OverflowMode.Hidden;

		_inspectWeaponCenterCol = ThornsUiFactory.AddPanel( _inspectWeaponLayout, "inspect-weapon-center" );
		_inspectWeaponCenterCol.Style.FlexDirection = FlexDirection.Column;
		_inspectWeaponCenterCol.Style.FlexGrow = 1;
		_inspectWeaponCenterCol.Style.FlexShrink = 1;
		_inspectWeaponCenterCol.Style.MinWidth = Length.Pixels( 0 );
		_inspectWeaponCenterCol.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponCenterCol.Style.PaddingLeft = Length.Pixels( 12 );
		_inspectWeaponCenterCol.Style.PaddingRight = Length.Pixels( 12 );

		var weaponCenterHeader = ThornsUiFactory.AddPanel( _inspectWeaponCenterCol, "inspect-weapon-center-header" );
		weaponCenterHeader.Style.FlexDirection = FlexDirection.Row;
		weaponCenterHeader.Style.AlignItems = Align.Center;
		weaponCenterHeader.Style.FlexShrink = 0;
		weaponCenterHeader.Style.MarginBottom = Length.Pixels( 4 );

		var statsTitle = ThornsUiFactory.AddLabel( weaponCenterHeader, "STATS", "inspect-weapon-stats-title thorns-muted" );
		statsTitle.Style.FlexShrink = 0;

		_inspectWeaponStats = ThornsUiFactory.AddPanel( _inspectWeaponCenterCol, "inspect-stats inspect-weapon-stats" );
		_inspectWeaponStats.Style.FlexDirection = FlexDirection.Column;
		_inspectWeaponStats.Style.FlexGrow = 1;
		_inspectWeaponStats.Style.FlexShrink = 1;
		_inspectWeaponStats.Style.MinHeight = Length.Pixels( 0 );
		_inspectWeaponStats.Style.Overflow = OverflowMode.Hidden;

		_inspectWeaponMeta = ThornsUiFactory.AddPanel( _inspectWeaponCenterCol, "inspect-meta inspect-weapon-meta" );
		_inspectWeaponMeta.Style.Display = DisplayMode.None;

		_inspectWeaponRightCol = ThornsUiFactory.AddPanel( _inspectWeaponLayout, "inspect-weapon-right" );
		_inspectWeaponRightCol.Style.FlexDirection = FlexDirection.Column;
		_inspectWeaponRightCol.Style.FlexShrink = 0;
		_inspectWeaponRightCol.Style.FlexGrow = 0;
		_inspectWeaponRightCol.Style.Width = Length.Pixels( 168 );
		_inspectWeaponRightCol.Style.MinWidth = Length.Pixels( 168 );
		_inspectWeaponRightCol.Style.MaxWidth = Length.Pixels( 168 );
		_inspectWeaponRightCol.Style.AlignItems = Align.Stretch;
		_inspectWeaponRightCol.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponRightCol.Style.PaddingLeft = Length.Pixels( 10 );
		_inspectWeaponRightCol.Style.PaddingRight = Length.Pixels( 4 );

		_inspectWeaponTier = ThornsUiFactory.AddPassiveLabel( _inspectWeaponRightCol, "", "inspect-tier-pill inspect-weapon-tier-pill" );
		_inspectWeaponTier.Style.Display = DisplayMode.None;
		_inspectWeaponTier.Style.FlexShrink = 0;
		_inspectWeaponTier.Style.AlignSelf = Align.FlexEnd;
		_inspectWeaponTier.Style.MarginBottom = Length.Pixels( 8 );
		_inspectWeaponTier.Style.MaxWidth = Length.Percent( 100 );
		_inspectWeaponTier.Style.Overflow = OverflowMode.Hidden;
		_inspectWeaponTier.Style.TextOverflow = TextOverflow.Ellipsis;
		_inspectWeaponTier.Style.WhiteSpace = WhiteSpace.NoWrap;

		ThornsUiFactory.AddLabel( _inspectWeaponRightCol, "ATTACHMENTS", "inspect-weapon-attach-title thorns-muted" )
			.Style.MarginBottom = Length.Pixels( 8 );

		_inspectWeaponAttachRow = ThornsUiFactory.AddPanel( _inspectWeaponRightCol, "inspect-weapon-attach-row" );
		_inspectWeaponAttachRow.Style.FlexDirection = FlexDirection.Row;
		_inspectWeaponAttachRow.Style.FlexWrap = Wrap.NoWrap;
		_inspectWeaponAttachRow.Style.JustifyContent = Justify.FlexStart;
		_inspectWeaponAttachRow.Style.AlignItems = Align.Center;
		_inspectWeaponAttachRow.Style.FlexShrink = 0;
		_inspectWeaponAttachRow.Style.Overflow = OverflowMode.Hidden;

		for ( var i = 0; i < Terraingen.Combat.Attachments.ThornsAttachmentCatalog.MaxSlotsPerWeapon; i++ )
		{
			var slot = new ThornsAttachmentInspectSlot( _inspectWeaponAttachRow, i, Rebuild );
			slot.Style.Width = Length.Pixels( attachmentSlotPx );
			slot.Style.Height = Length.Pixels( attachmentSlotPx );
			slot.Style.MinWidth = Length.Pixels( attachmentSlotPx );
			slot.Style.MinHeight = Length.Pixels( attachmentSlotPx );
			slot.Style.MarginBottom = Length.Pixels( 0 );
			if ( i > 0 )
				slot.Style.MarginLeft = Length.Pixels( 5 );
			_weaponAttachmentSlots.Add( slot );
		}

		var attachHint = ThornsUiFactory.AddLabel(
			_inspectWeaponRightCol,
			"Drag an attachment to one of these slots or directly onto the gun to attach it.",
			"inspect-weapon-attach-hint thorns-muted" );
		attachHint.Style.MarginTop = Length.Pixels( 8 );
		attachHint.Style.FontSize = Length.Pixels( 10 );
		attachHint.Style.LineHeight = Length.Pixels( 14 );
		attachHint.Style.WhiteSpace = WhiteSpace.Normal;
	}

	void BuildCraftColumn()
	{
		_craftPanel = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right inventory-craft-column inventory-craft-column-concept", flexWeight: 30f );
		_craftPanel.Style.FlexDirection = FlexDirection.Column;
		_craftPanel.Style.MinHeight = Length.Pixels( 0 );
		_craftPanel.Style.Overflow = OverflowMode.Hidden;

		var craftHeader = ThornsTheme.CreateSectionHeader( _craftPanel, "CRAFTING", "craft-header-concept" );
		_craftHeaderLabel = FindSectionHeaderLabel( craftHeader );

		_categoryRow = ThornsUiFactory.AddPanel( _craftPanel, "craft-categories inventory-filter-row craft-categories-concept" );
		_categoryRow.Style.FlexDirection = FlexDirection.Row;
		_categoryRow.Style.FlexWrap = Wrap.NoWrap;
		_categoryRow.Style.FlexShrink = 0;
		_categoryRow.Style.Width = Length.Percent( 100 );
		_categoryRow.Style.Overflow = OverflowMode.Scroll;
		_categoryRow.Style.MarginBottom = Length.Pixels( 10 );
		_categoryRow.Style.JustifyContent = Justify.Center;

		var searchRow = ThornsUiFactory.AddPanel( _craftPanel, "craft-search-row" );
		searchRow.Style.FlexDirection = FlexDirection.Row;
		searchRow.Style.FlexShrink = 0;
		searchRow.Style.MarginBottom = Length.Pixels( 6 );
		_craftSearchEntry = searchRow.AddChild( new TextEntry() );
		_craftSearchEntry.Placeholder = "Search recipes…";
		_craftSearchEntry.AddEventListener( "onsubmit", () =>
		{
			_craftSearchQuery = _craftSearchEntry.Text ?? "";
			_recipeListReady = true;
			RebuildRecipeList();
		} );
		_craftSearchEntry.AddEventListener( "onchange", () =>
		{
			_craftSearchQuery = _craftSearchEntry.Text ?? "";
			_recipeListReady = true;
			RebuildRecipeList();
		} );
		_craftSearchEntry.Style.FlexGrow = 1;

		var craftBody = ThornsUiFactory.AddPanel( _craftPanel, "craft-body craft-body-concept" );
		_craftBody = craftBody;
		craftBody.Style.FlexDirection = FlexDirection.Column;
		craftBody.Style.FlexGrow = 1;
		craftBody.Style.FlexShrink = 1;
		craftBody.Style.MinHeight = Length.Pixels( 0 );
		craftBody.Style.Overflow = OverflowMode.Hidden;
		craftBody.Style.BackgroundColor = Color.Transparent;

		_recipeDetail = ThornsUiFactory.AddPanel( craftBody, "recipe-detail recipe-detail-hidden" );
		_recipeDetail.Style.Display = DisplayMode.None;
		_recipeTitle = ThornsUiFactory.AddLabel( _recipeDetail, "", "recipe-detail-title" );
		_recipeDetailIcon = ThornsUiFactory.AddPanel( _recipeDetail, "recipe-detail-icon slot-icon" );
		_recipeDetailDesc = ThornsUiFactory.AddLabel( _recipeDetail, "", "recipe-detail-desc" );
		_recipeDetailMeta = ThornsUiFactory.AddLabel( _recipeDetail, "", "recipe-detail-meta" );
		_recipeIngredients = ThornsUiFactory.AddPanel( _recipeDetail, "recipe-ingredients" );

		_recipeList = ThornsUiFactory.AddPanel( craftBody, "recipe-list recipe-card-list recipe-list-concept" );
		_recipeList.Style.FlexDirection = FlexDirection.Column;
		_recipeList.Style.FlexGrow = 1;
		_recipeList.Style.FlexShrink = 1;
		_recipeList.Style.MinHeight = Length.Pixels( 0 );
		_recipeList.Style.Width = Length.Percent( 100 );
		_recipeList.Style.Overflow = OverflowMode.Scroll;
		_recipeList.Style.BackgroundColor = Color.Transparent;

		var craftFooter = ThornsUiFactory.AddPanel( _craftPanel, "inventory-craft-footer-stack" );
		craftFooter.Style.FlexDirection = FlexDirection.Column;
		craftFooter.Style.FlexShrink = 0;
		craftFooter.Style.Width = Length.Percent( 100 );
		craftFooter.Style.MarginTop = Length.Pixels( 10 );

		_craftFooterBtn = ThornsUiFactory.AddClickable( craftFooter, "inventory-craft-footer-btn thorns-btn-primary", "CRAFT", OnConceptCraftFooterClicked );
		_craftFooterBtn.Style.FlexShrink = 0;
		_craftFooterBtn.Style.MinHeight = Length.Pixels( 42 );
		_craftFooterBtn.Style.Width = Length.Percent( 100 );
		_craftFooterBtn.Style.JustifyContent = Justify.Center;
		_craftFooterBtn.Style.AlignItems = Align.Center;

		_craftQueue = ThornsUiFactory.AddPanel( craftFooter, "craft-queue inventory-craft-queue-concept" );
		_craftQueue.Style.FlexDirection = FlexDirection.Column;
		_craftQueue.Style.FlexShrink = 0;
		_craftQueue.Style.Width = Length.Percent( 100 );
		_craftQueue.Style.MarginTop = Length.Pixels( 8 );
		_craftQueue.Style.MinHeight = Length.Pixels( 72 );
		_craftQueue.Style.MaxHeight = Length.Pixels( 190 );
		_craftQueue.Style.Overflow = OverflowMode.Hidden;
	}

	void OnConceptCraftFooterClicked()
	{
		var recipeId = ThornsUiClientState.Snapshot?.Inventory?.SelectedRecipeId;
		if ( !string.IsNullOrWhiteSpace( recipeId ) )
			RequestCraftRecipe( recipeId );
	}

	static Label FindSectionHeaderLabel( Panel header )
	{
		if ( header is null || !header.IsValid )
			return null;

		foreach ( var child in header.Children )
		{
			if ( child is Label label && label.HasClass( "thorns-section-header-label" ) )
				return label;

			var nested = FindSectionHeaderLabel( child );
			if ( nested is not null )
				return nested;
		}

		return null;
	}

	void EnsureConceptLayoutSizing()
	{
		if ( _appliedConceptSlotPx > 0 )
			return;

		if ( _contentRow is null || !_contentRow.IsValid || _contentRow.Box.RectInner.Width < 64f )
			return;

		ApplyFixedConceptLayout();
	}

	void ApplyFixedConceptLayout()
	{
		var slotPx = ThornsUiMetrics.MenuItemSlot;
		var armorPx = ThornsUiMetrics.MenuArmorSlot;
		var gridW = ThornsUiMetrics.MenuInventoryGridWidth;
		var hotbarW = ThornsUiMetrics.MenuHotbarGridWidth;

		_appliedConceptSlotPx = slotPx;

		ApplyArmorSlotLayout( _headSlot, armorPx );
		ApplyArmorSlotLayout( _chestSlot, armorPx );
		ApplyArmorSlotLayout( _legsSlot, armorPx );

		if ( _conceptGrid is { IsValid: true } )
		{
			_conceptGrid.Style.Width = Length.Pixels( gridW );
			_conceptGrid.Style.MinWidth = Length.Pixels( gridW );
			_conceptGrid.Style.MaxWidth = Length.Pixels( gridW );
			_conceptGrid.Style.AlignItems = Align.Center;
			_conceptGrid.Style.AlignSelf = Align.Center;
		}

		if ( _conceptGridHost is { IsValid: true } )
		{
			_conceptGridHost.Style.AlignItems = Align.Center;
			_conceptGridHost.Style.JustifyContent = Justify.FlexStart;
		}

		if ( _conceptHotbar is { IsValid: true } )
		{
			_conceptHotbar.Style.Width = Length.Pixels( hotbarW );
			_conceptHotbar.Style.MinWidth = Length.Pixels( hotbarW );
			_conceptHotbar.Style.MaxWidth = Length.Pixels( hotbarW );
			_conceptHotbar.Style.AlignSelf = Align.Center;
		}

		foreach ( var slot in _inventorySlots )
			ApplyMenuSlotSize( slot, slotPx );

		foreach ( var slot in _hotbarSlots )
			ApplyMenuSlotSize( slot, slotPx );
	}

	static void ApplyArmorSlotLayout( ThornsItemSlot slot, int px )
	{
		if ( slot is null || !slot.IsValid )
			return;

		slot.Style.Width = Length.Pixels( px );
		slot.Style.Height = Length.Pixels( px );
		slot.Style.MinWidth = Length.Pixels( px );
		slot.Style.MinHeight = Length.Pixels( px );
		slot.Style.MaxWidth = Length.Pixels( px );
		slot.Style.MaxHeight = Length.Pixels( px );
		slot.Style.MarginLeft = Length.Pixels( -px / 2 );
		slot.Style.MarginTop = Length.Pixels( -px / 2 );

		var row = slot.Parent;
		if ( row is null || !row.IsValid )
			return;

		foreach ( var child in row.Children )
		{
			if ( child == slot || !child.HasClass( "inventory-armor-slot-label" ) )
				continue;

			child.Style.MarginLeft = Length.Pixels( px / 2 + 14 );
			child.Style.MarginTop = Length.Pixels( -7 );
			break;
		}
	}

	static void ApplyMenuSlotSize( ThornsItemSlot slot, int px )
	{
		if ( slot is null || !slot.IsValid )
			return;

		slot.Style.Width = Length.Pixels( px );
		slot.Style.Height = Length.Pixels( px );
		slot.Style.MinWidth = Length.Pixels( px );
		slot.Style.MinHeight = Length.Pixels( px );
		slot.Style.MaxWidth = Length.Pixels( px );
		slot.Style.MaxHeight = Length.Pixels( px );
		slot.Style.FlexGrow = 0;
		slot.Style.FlexShrink = 0;
	}

	Panel BuildFilterRow( Panel parent, string rowClass, (string Key, string Label, string IconKey)[] filters,
		Action<string> onSelect, Func<string> getActiveKey )
	{
		var row = ThornsUiFactory.AddPanel( parent, rowClass );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.FlexWrap = Wrap.NoWrap;
		row.Style.FlexShrink = 0;
		row.Style.Width = Length.Percent( 100 );
		row.Style.Overflow = OverflowMode.Hidden;
		row.Style.MarginBottom = Length.Pixels( 8 );
		row.Style.JustifyContent = Justify.Center;

		foreach ( var filter in filters )
		{
			var captured = filter.Key;
			var btn = ThornsUiFactory.AddClickable( row, "inventory-filter-btn", () => onSelect( captured ) );
			btn.SetClass( "active", string.Equals( getActiveKey(), captured, StringComparison.OrdinalIgnoreCase ) );
			btn.Tooltip = filter.Label;

			var icon = ThornsUiFactory.AddPanel( btn, "inventory-filter-icon slot-icon" );
			icon.Style.PointerEvents = PointerEvents.None;
			ThornsIconCache.ApplyToPanel( icon, ResolveFilterIconPath( filter.IconKey, filter.Key ) );
		}

		return row;
	}

	static string ResolveFilterIconPath( string iconKey, string fallbackCategory ) =>
		iconKey switch
		{
			"filter_all" => ThornsIconRegistry.InventoryUi( "inventory" ),
			"filter_building" => ThornsIconRegistry.InventoryUi( "craft_build" ),
			"filter_weapons" => ThornsIconRegistry.InventoryUi( "craft_ammo" ),
			"filter_tools" => ThornsIconRegistry.InventoryUi( "craft_tools" ),
			"filter_apparel" => ThornsIconRegistry.InventoryUi( "craft_armor" ),
			"filter_consumables" => ThornsIconRegistry.InventoryUi( "craft_medical" ),
			"filter_food" => ThornsIconRegistry.InventoryUi( "craft_medical" ),
			_ => ThornsIconRegistry.InventoryUi( $"craft_{fallbackCategory}" ),
		};

	ThornsItemSlot CreateLabeledArmorSlot( Panel parent, ThornsContainerKind container, string label )
	{
		var armorPx = ThornsUiMetrics.MenuArmorSlot;

		var wrap = ThornsUiFactory.AddPanel( parent, "inventory-armor-slot-wrap inventory-armor-slot-row" );
		wrap.Style.Position = PositionMode.Relative;
		wrap.Style.FlexDirection = FlexDirection.Row;
		wrap.Style.AlignItems = Align.Center;
		wrap.Style.FlexShrink = 1;
		wrap.Style.FlexGrow = 1;
		wrap.Style.Width = Length.Percent( 100 );
		wrap.Style.MinHeight = Length.Pixels( armorPx );

		var slot = CreateVerticalArmorSlot( wrap, container );
		slot.Style.Position = PositionMode.Absolute;
		slot.Style.Left = Length.Percent( 50 );
		slot.Style.Top = Length.Percent( 50 );
		slot.Style.FlexShrink = 0;

		var labelPanel = ThornsUiFactory.AddLabel( wrap, label, "inventory-armor-slot-label" );
		labelPanel.Style.Position = PositionMode.Absolute;
		labelPanel.Style.Left = Length.Percent( 50 );
		labelPanel.Style.Top = Length.Percent( 50 );
		labelPanel.Style.FlexShrink = 0;

		ApplyArmorSlotLayout( slot, armorPx );
		return slot;
	}

	ThornsItemSlot CreateVerticalArmorSlot( Panel parent, ThornsContainerKind container )
	{
		var slot = CreateArmorSlot( parent, container );
		slot.AddClass( "inventory-armor-slot-vertical inventory-armor-slot-concept" );
		slot.Tooltip = container.ToString();

		return slot;
	}

	void RebuildLevelXp()
	{
		// Level/XP moved to skills screen in concept layout — armor column stays equipment-only.
	}

	void RebuildArmorAttributes()
	{
		if ( _statsPanel is null || !_statsPanel.IsValid )
			return;

		_statsPanel.DeleteChildren( true );

		var armorVal = 0f;
		if ( ThornsUiClientState.HasSnapshot )
		{
			foreach ( var kind in new[] { ThornsContainerKind.Head, ThornsContainerKind.Chest, ThornsContainerKind.Legs } )
			{
				var slot = ThornsUiClientState.Snapshot.Inventory.Slots.FirstOrDefault( s => s.Container == kind );
				if ( slot is null || string.IsNullOrEmpty( slot.ItemId ) )
					continue;

				if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) )
					continue;

				var stack = new ThornsItemStack
				{
					ItemId = slot.ItemId,
					ItemTier = slot.ItemTier > 0 ? slot.ItemTier : slot.WeaponTier,
					StatRoll = slot.StatRoll
				};
				armorVal += ThornsItemTier.ResolveArmorProtection( stack, def );
			}
		}

		AddResistRow( _statsPanel, "Armor", "shield", $"{Math.Clamp( armorVal * 100f, 0f, 99f ):0}" );
		AddResistRow( _statsPanel, "Cold Resist", "cold", "—" );
		AddResistRow( _statsPanel, "Heat Resist", "heat", "—" );
	}

	static void AddResistRow( Panel parent, string label, string iconKey, string value )
	{
		var row = ThornsUiFactory.AddPanel( parent, "inventory-resist-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.JustifyContent = Justify.SpaceBetween;
		row.Style.MarginBottom = Length.Pixels( 4 );

		var left = ThornsUiFactory.AddPanel( row, "inventory-resist-left" );
		left.Style.FlexDirection = FlexDirection.Row;
		left.Style.AlignItems = Align.Center;

		var icon = ThornsUiFactory.AddPanel( left, "inventory-resist-icon slot-icon" );
		icon.Style.Width = Length.Pixels( 16 );
		icon.Style.Height = Length.Pixels( 16 );
		icon.Style.MarginRight = Length.Pixels( 6 );
		ThornsIconCache.ApplyToPanel( icon, ThornsIconRegistry.InventoryUi( iconKey ) );

		ThornsUiFactory.AddLabel( left, label, "inventory-resist-label thorns-muted" );
		ThornsUiFactory.AddLabel( row, value, "inventory-resist-value" );
	}

	void RebuildSkillsPanelsIfChanged()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var skills = ThornsUiClientState.Snapshot.Skills;
		var key = $"{skills.PlayerLevel}|{skills.TotalXp}|{skills.Ranks?.Count ?? 0}";
		if ( string.Equals( key, _lastSkillsUiKey, StringComparison.Ordinal ) )
			return;

		_lastSkillsUiKey = key;
		RebuildArmorAttributes();
	}

	void UpdateInventoryFilterButtons()
	{
		if ( _inventoryFilterRow is null || !_inventoryFilterRow.IsValid )
			return;

		var i = 0;
		foreach ( var child in _inventoryFilterRow.Children )
		{
			if ( child is not Panel btn || i >= InventoryFilters.Length )
				continue;

			btn.SetClass( "active", string.Equals( InventoryFilters[i].Key, _inventoryFilterKey, StringComparison.OrdinalIgnoreCase ) );
			i++;
		}
	}

	void ApplyInventoryFilter()
	{
		foreach ( var slot in _inventorySlots )
		{
			var stack = slot.PeekStack();
			var visible = MatchesInventoryFilter( stack.ItemId );
			slot.SetClass( "inventory-filter-hidden", !visible );
			slot.Style.PointerEvents = visible ? PointerEvents.All : PointerEvents.None;
		}
	}

	bool MatchesInventoryFilter( string itemId )
	{
		if ( string.Equals( _inventoryFilterKey, "all", StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( string.IsNullOrWhiteSpace( itemId ) )
			return true;

		var def = ThornsDefinitionRegistry.GetItem( itemId );
		if ( def is null )
			return true;

		if ( string.Equals( _inventoryFilterKey, "resource", StringComparison.OrdinalIgnoreCase ) )
			return def.Category is ThornsItemCategory.Resource or ThornsItemCategory.Ammo;

		if ( Enum.TryParse<ThornsItemCategory>( _inventoryFilterKey, true, out var category ) )
			return def.Category == category;

		return true;
	}

	void RequestCraftRecipe( string recipeId )
	{
		if ( string.IsNullOrWhiteSpace( recipeId ) )
			return;

		var recipe = ThornsDefinitionRegistry.GetRecipe( recipeId );
		if ( recipe is null || !ThornsMenuSnapshotHelpers.CanCraftRecipe( recipe ) )
			return;

		ThornsPlayerGameplay.Local?.SetCraftUiState( _craftExpanded, ThornsUiClientState.Snapshot.Inventory.ActiveCraftCategoryId, recipeId );
		ThornsPlayerGameplay.Local?.RequestCraft( new ThornsCraftRequest { RecipeId = recipeId, Quantity = 1 } );
	}
}
