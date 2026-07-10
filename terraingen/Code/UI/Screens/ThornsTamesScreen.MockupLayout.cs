namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed partial class ThornsTamesScreen
{
	static readonly ThornsTameCommand[] ConceptActionCommands =
	{
		ThornsTameCommand.Follow,
		ThornsTameCommand.Stay,
		ThornsTameCommand.Passive,
		ThornsTameCommand.Attack
	};

	Panel _contentRow;
	Panel _listColumn;
	Panel _listScroll;
	Panel _listFooter;
	Panel _detailColumn;
	Panel _detailBody;
	Panel _detailProfile;
	Panel _detailPortrait;
	Panel _detailSpeciesIcon;
	Panel _detailAvailableXpBox;
	Label _detailAvailableXpValue;
	Panel _detailHealthTrack;
	Panel _detailHealthFill;
	Label _detailHealthLabel;
	Panel _detailExpTrack;
	Panel _detailExpFill;
	Label _detailExpLabel;
	Label _detailExpLevelLabel;
	Panel _detailBottomRow;
	Panel _detailAttributesFrame;
	Panel _detailActionsFrame;

	void BuildTamesConceptLayout()
	{
		AddClass( "tames-screen tames-screen-concept" );
		Style.Position = PositionMode.Relative;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "tames-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		_listColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-left tames-list-column tames-list-column-concept", flexWeight: 42f );
		_listColumn.Style.FlexDirection = FlexDirection.Column;
		_listColumn.Style.MinHeight = Length.Pixels( 0 );
		_listColumn.Style.Overflow = OverflowMode.Hidden;

		var listHeader = ThornsUiFactory.AddPanel( _listColumn, "tame-list-header tames-concept-list-header" );
		listHeader.Style.FlexDirection = FlexDirection.Row;
		listHeader.Style.AlignItems = Align.Center;
		listHeader.Style.FlexShrink = 0;
		listHeader.Style.MarginBottom = Length.Pixels( 8 );
		ThornsTheme.CreateSectionHeader( listHeader, "MY TAMES", "tames-list-section-header" );
		_capacityLabel = ThornsUiFactory.AddLabel( listHeader, "0 / 0", "tame-capacity tames-capacity-badge" );

		_listScroll = ThornsUiFactory.AddPanel( _listColumn, "tames-list-scroll" );
		_listScroll.Style.Display = DisplayMode.Flex;
		_listScroll.Style.FlexDirection = FlexDirection.Column;
		_listScroll.Style.FlexGrow = 1;
		_listScroll.Style.MinHeight = Length.Pixels( 0 );
		_listScroll.Style.Overflow = OverflowMode.Scroll;
		_listScroll.Style.Width = Length.Percent( 100 );
		_list = _listScroll;

		_listFooter = ThornsUiFactory.AddPanel( _listColumn, "tames-list-footer" );
		_listFooter.Style.FlexShrink = 0;
		_listFooter.Style.MarginTop = Length.Pixels( 8 );
		_listFooter.Style.Width = Length.Percent( 100 );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_detailColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right tames-detail-column tames-detail-column-concept", flexWeight: 58f );
		_detailColumn.Style.FlexDirection = FlexDirection.Column;
		_detailColumn.Style.MinHeight = Length.Pixels( 0 );
		_detailColumn.Style.Overflow = OverflowMode.Hidden;
		_detailColumn.Style.Padding = Length.Pixels( 0 );
		_right = _detailColumn;

		var detailHeader = ThornsUiFactory.AddPanel( _detailColumn, "tames-detail-header-row" );
		detailHeader.Style.FlexDirection = FlexDirection.Row;
		detailHeader.Style.AlignItems = Align.Center;
		detailHeader.Style.FlexShrink = 0;
		detailHeader.Style.Padding = Length.Pixels( 10 );
		detailHeader.Style.PaddingBottom = Length.Pixels( 6 );
		detailHeader.Style.Width = Length.Percent( 100 );
		detailHeader.Style.MinWidth = Length.Pixels( 0 );
		ThornsTheme.CreateSectionHeader( detailHeader, "TAME DETAILS", "tames-detail-section-header" );

		_detailBody = ThornsUiFactory.AddPanel( _detailColumn, "tames-detail-body" );
		_detailBody.Style.FlexDirection = FlexDirection.Column;
		_detailBody.Style.FlexGrow = 1;
		_detailBody.Style.MinHeight = Length.Pixels( 0 );
		_detailBody.Style.Overflow = OverflowMode.Scroll;
		_detailBody.Style.PaddingLeft = Length.Pixels( 10 );
		_detailBody.Style.PaddingRight = Length.Pixels( 10 );
		_detailBody.Style.PaddingBottom = Length.Pixels( 10 );

		_detailProfile = ThornsUiFactory.AddPanel( _detailBody, "tames-detail-profile concept-section" );
		_detailProfile.Style.FlexDirection = FlexDirection.Row;
		_detailProfile.Style.AlignItems = Align.FlexStart;
		_detailProfile.Style.FlexShrink = 0;
		_detailProfile.Style.Padding = Length.Pixels( 12 );
		_detailProfile.Style.MarginBottom = Length.Pixels( 10 );
		_detailProfile.Style.Position = PositionMode.Relative;

		_detailPortrait = ThornsUiFactory.AddPanel( _detailProfile, "tames-detail-portrait tame-portrait slot-icon" );
		_detailPortrait.Style.Width = Length.Pixels( 120 );
		_detailPortrait.Style.Height = Length.Pixels( 120 );
		_detailPortrait.Style.FlexShrink = 0;
		_detailPortrait.Style.MarginRight = Length.Pixels( 14 );

		var profileMain = ThornsUiFactory.AddPanel( _detailProfile, "tames-detail-profile-main" );
		profileMain.Style.FlexDirection = FlexDirection.Column;
		profileMain.Style.FlexGrow = 1;
		profileMain.Style.MinWidth = Length.Pixels( 0 );
		profileMain.Style.PaddingRight = Length.Pixels( 122 );

		var identityRow = ThornsUiFactory.AddPanel( profileMain, "tames-detail-identity-row" );
		identityRow.Style.FlexDirection = FlexDirection.Row;
		identityRow.Style.AlignItems = Align.Center;
		identityRow.Style.MarginBottom = Length.Pixels( 6 );

		var speciesIcon = ThornsUiFactory.AddPanel( identityRow, "tame-species-icon slot-icon tames-detail-species-icon" );
		speciesIcon.Style.Width = Length.Pixels( 22 );
		speciesIcon.Style.Height = Length.Pixels( 22 );
		speciesIcon.Style.FlexShrink = 0;
		speciesIcon.Style.MarginRight = Length.Pixels( 6 );
		_detailSpeciesIcon = speciesIcon;

		_heroName = ThornsUiFactory.AddLabel( identityRow, "", "tames-detail-name thorns-header" );

		_heroTier = ThornsUiFactory.AddLabel( profileMain, "", "tames-detail-tier tame-card-tier" );
		_heroTier.Style.MarginBottom = Length.Pixels( 2 );

		_heroLevel = ThornsUiFactory.AddLabel( profileMain, "", "tames-detail-level-label" );
		_heroLevel.Style.MarginBottom = Length.Pixels( 8 );

		var renameLabel = ThornsUiFactory.AddLabel( profileMain, "Name", "tames-detail-rename-label" );
		renameLabel.Style.MarginBottom = Length.Pixels( 4 );

		var renameRow = ThornsUiFactory.AddPanel( profileMain, "tame-rename-row tames-detail-rename-row" );
		renameRow.Style.FlexDirection = FlexDirection.Row;
		renameRow.Style.AlignItems = Align.Center;
		renameRow.Style.MaxWidth = Length.Pixels( 260 );
		_renameEntry = renameRow.AddChild( new TextEntry() );
		_renameEntry.AddClass( "tame-rename-entry tames-detail-rename-entry" );
		_renameEntry.Style.FlexGrow = 1;
		_renameEntry.Style.BackgroundColor = Color.Transparent;
		_renameEntry.Style.BorderColor = new Color( 80f / 255f, 62f / 255f, 42f / 255f, 0.45f );
		_renameEntry.Style.BorderWidth = Length.Pixels( 1 );
		_renameEntry.Style.FontColor = new Color( 28f / 255f, 19f / 255f, 10f / 255f );
		_renameEntry.AddEventListener( "onblur", OnRenameCommitted );

		_detailAvailableXpBox = ThornsUiFactory.AddPanel( _detailProfile, "tames-available-xp-box concept-section" );
		_detailAvailableXpBox.Style.FlexDirection = FlexDirection.Column;
		_detailAvailableXpBox.Style.AlignItems = Align.Center;
		_detailAvailableXpBox.Style.JustifyContent = Justify.Center;
		_detailAvailableXpBox.Style.FlexShrink = 0;
		_detailAvailableXpBox.Style.Width = Length.Pixels( 118 );
		_detailAvailableXpBox.Style.MinWidth = Length.Pixels( 118 );
		_detailAvailableXpBox.Style.MinHeight = Length.Pixels( 76 );
		_detailAvailableXpBox.Style.Padding = Length.Pixels( 8 );
		_detailAvailableXpBox.Style.Position = PositionMode.Absolute;
		_detailAvailableXpBox.Style.Top = Length.Pixels( 10 );
		_detailAvailableXpBox.Style.Right = Length.Pixels( 10 );

		var xpIcon = ThornsUiFactory.AddPanel( _detailAvailableXpBox, "tames-available-xp-icon slot-icon" );
		xpIcon.Style.Width = Length.Pixels( 20 );
		xpIcon.Style.Height = Length.Pixels( 20 );
		xpIcon.Style.MarginBottom = Length.Pixels( 4 );
		ThornsIconCache.ApplyToPanel( xpIcon, ThornsTameCatalog.StatIconPath( "attack" ) );

		_detailAvailableXpValue = ThornsUiFactory.AddLabel( _detailAvailableXpBox, "0", "tames-available-xp-value" );
		ThornsUiFactory.AddPassiveLabel( _detailAvailableXpBox, "AVAILABLE XP", "tames-available-xp-label" );

		var statusFrame = ThornsUiFactory.AddPanel( _detailBody, "tames-detail-status concept-section" );
		statusFrame.Style.FlexDirection = FlexDirection.Column;
		statusFrame.Style.FlexShrink = 0;
		statusFrame.Style.Padding = Length.Pixels( 12 );
		statusFrame.Style.MarginBottom = Length.Pixels( 10 );

		var healthHeader = ThornsUiFactory.AddPanel( statusFrame, "tames-status-row-header" );
		healthHeader.Style.FlexDirection = FlexDirection.Row;
		healthHeader.Style.AlignItems = Align.Center;
		healthHeader.Style.JustifyContent = Justify.SpaceBetween;
		healthHeader.Style.MarginBottom = Length.Pixels( 4 );
		ThornsUiFactory.AddPassiveLabel( healthHeader, "♥ HEALTH", "tames-status-label" );
		_detailHealthLabel = ThornsUiFactory.AddLabel( healthHeader, "0 / 0", "tames-status-value" );

		_detailHealthTrack = ThornsUiFactory.AddPanel( statusFrame, "tames-status-track tames-health-track" );
		_detailHealthFill = ThornsUiFactory.AddPanel( _detailHealthTrack, "tames-status-fill tames-health-fill" );

		var expHeader = ThornsUiFactory.AddPanel( statusFrame, "tames-status-row-header" );
		expHeader.Style.FlexDirection = FlexDirection.Row;
		expHeader.Style.AlignItems = Align.Center;
		expHeader.Style.JustifyContent = Justify.SpaceBetween;
		expHeader.Style.MarginTop = Length.Pixels( 10 );
		expHeader.Style.MarginBottom = Length.Pixels( 4 );
		ThornsUiFactory.AddPassiveLabel( expHeader, "★ EXPERIENCE", "tames-status-label" );
		_detailExpLevelLabel = ThornsUiFactory.AddLabel( expHeader, "Level 1", "tames-status-level" );

		_detailExpLabel = ThornsUiFactory.AddLabel( statusFrame, "0 / 0 XP", "tames-status-xp-text" );
		_detailExpLabel.Style.MarginBottom = Length.Pixels( 4 );

		_detailExpTrack = ThornsUiFactory.AddPanel( statusFrame, "tames-status-track tames-exp-track" );
		_detailExpFill = ThornsUiFactory.AddPanel( _detailExpTrack, "tames-status-fill tames-exp-fill" );

		_detailBottomRow = ThornsUiFactory.AddPanel( _detailBody, "tames-detail-bottom-row" );
		_detailBottomRow.Style.FlexDirection = FlexDirection.Row;
		_detailBottomRow.Style.FlexShrink = 0;
		_detailBottomRow.Style.MinHeight = Length.Pixels( 0 );
		_detailBottomRow.Style.Width = Length.Percent( 100 );
		_detailBottomRow.Style.MinWidth = Length.Pixels( 0 );

		_detailAttributesFrame = ThornsUiFactory.AddPanel( _detailBottomRow, "tames-attributes-frame concept-section" );
		_detailAttributesFrame.Style.FlexDirection = FlexDirection.Column;
		_detailAttributesFrame.Style.FlexGrow = 1;
		_detailAttributesFrame.Style.FlexBasis = Length.Percent( 50 );
		_detailAttributesFrame.Style.Padding = Length.Pixels( 10 );
		_detailAttributesFrame.Style.MarginRight = Length.Pixels( 5 );
		ThornsTheme.CreateSectionHeader( _detailAttributesFrame, "ATTRIBUTES", "tames-attributes-header" );
		_attributesPanel = ThornsUiFactory.AddPanel( _detailAttributesFrame, "tame-stats-panel tame-attributes-panel" );
		_attributesPanel.Style.FlexDirection = FlexDirection.Column;
		_traitsPanel = ThornsUiFactory.AddPanel( _detailAttributesFrame, "tame-traits-panel" );
		_traitsPanel.Style.FlexDirection = FlexDirection.Column;
		_traitsPanel.Style.MarginTop = Length.Pixels( 6 );

		_detailActionsFrame = ThornsUiFactory.AddPanel( _detailBottomRow, "tames-actions-frame concept-section" );
		_detailActionsFrame.Style.FlexDirection = FlexDirection.Column;
		_detailActionsFrame.Style.FlexGrow = 1;
		_detailActionsFrame.Style.FlexBasis = Length.Percent( 50 );
		_detailActionsFrame.Style.MinWidth = Length.Pixels( 0 );
		_detailActionsFrame.Style.Width = Length.Percent( 50 );
		_detailActionsFrame.Style.Padding = Length.Pixels( 10 );
		_detailActionsFrame.Style.MarginLeft = Length.Pixels( 5 );
		ThornsTheme.CreateSectionHeader( _detailActionsFrame, "ACTIONS", "tames-actions-header" );
		_commands = ThornsUiFactory.AddPanel( _detailActionsFrame, "tame-commands-grid" );
		_commands.Style.FlexDirection = FlexDirection.Row;
		_commands.Style.FlexWrap = Wrap.Wrap;
		_commands.Style.Width = Length.Percent( 100 );
		_commands.Style.MinWidth = Length.Pixels( 0 );
		_commands.Style.JustifyContent = Justify.FlexStart;

		_feedNoticeAnchor = ThornsUiFactory.AddPanel( this, "tame-feed-notice-anchor" );
		_feedNoticeAnchor.Style.Position = PositionMode.Absolute;
		_feedNoticeAnchor.Style.Right = Length.Pixels( 20 );
		_feedNoticeAnchor.Style.Bottom = Length.Pixels( 16 );
		_feedNoticeAnchor.Style.FlexDirection = FlexDirection.Row;
		_feedNoticeAnchor.Style.JustifyContent = Justify.FlexEnd;
		_feedNoticeAnchor.Style.AlignItems = Align.Center;
		_feedNoticeAnchor.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyLocalOrder( _feedNoticeAnchor, 10 );
		_feedNoticeAnchor.Style.Display = DisplayMode.None;

		_feedNotice = ThornsUiFactory.AddLabel( _feedNoticeAnchor, "", "tame-feed-notice" );
		_feedNotice.Style.PointerEvents = PointerEvents.None;

		BuildBreedSubmenuShell();
	}

	static string ConceptCommandLabel( ThornsTameCommand cmd ) => cmd switch
	{
		ThornsTameCommand.Attack => "AGGRESSIVE",
		_ => cmd.ToString().ToUpperInvariant()
	};

	void AddConceptActionButton( Panel parent, string label, string iconPath, bool selected, Action onClick )
	{
		var btn = ThornsUiFactory.AddClickable( parent, "tame-cmd-btn tame-action-grid-btn", onClick );
		btn.SetClass( "selected", selected );
		btn.Style.FlexDirection = FlexDirection.Column;
		btn.Style.AlignItems = Align.Center;
		btn.Style.JustifyContent = Justify.Center;
		ApplyActionButtonGridStyles( btn );

		var icon = ThornsUiFactory.AddPanel( btn, "tame-cmd-icon slot-icon" );
		icon.Style.Width = Length.Pixels( 28 );
		icon.Style.Height = Length.Pixels( 28 );
		icon.Style.MinWidth = Length.Pixels( 28 );
		icon.Style.MinHeight = Length.Pixels( 28 );
		icon.Style.FlexShrink = 0;
		icon.Style.PointerEvents = PointerEvents.None;
		ThornsIconCache.ApplyToPanel( icon, iconPath );

		var cmdLabel = ThornsUiFactory.AddPassiveLabel( btn, label, "tame-cmd-label" );
		cmdLabel.Style.TextAlign = TextAlign.Center;
		cmdLabel.Style.Width = Length.Percent( 100 );
	}

	static void ApplyActionButtonGridStyles( Panel btn )
	{
		const float widthPercent = 48f;
		btn.Style.FlexGrow = 0;
		btn.Style.FlexShrink = 0;
		btn.Style.FlexBasis = Length.Percent( widthPercent );
		btn.Style.Width = Length.Percent( widthPercent );
		btn.Style.MinWidth = Length.Percent( widthPercent );
		btn.Style.MaxWidth = Length.Percent( widthPercent );
		btn.Style.MinHeight = Length.Pixels( 64 );
		btn.Style.MarginBottom = Length.Pixels( 8 );
	}

}
