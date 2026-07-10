namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed partial class ThornsGuildScreen
{
	enum GuildLeaderboardTab
	{
		Dominion,
		Ascension,
		Purification,
		Apex
	}

	Panel _contentRow;
	Panel _centerVictory;
	Panel _centerActivity;
	Panel _rightMembers;
	Panel _rightLeaderboard;
	GuildLeaderboardTab _leaderboardTab = GuildLeaderboardTab.Dominion;

	void BuildGuildLayout()
	{
		AddClass( "guild-screen guild-screen-concept" );
		Style.Position = PositionMode.Relative;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "guild-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		_left = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-left guild-overview-column guild-overview-column-concept", flexWeight: 28f );
		_left.Style.FlexDirection = FlexDirection.Column;
		_left.Style.MinHeight = Length.Pixels( 0 );
		_left.Style.Overflow = OverflowMode.Hidden;

		ThornsTheme.CreateSectionHeader( _left, "GUILD", "guild-column-header" );

		_identityPanel = ThornsUiFactory.AddPanel( _left, "guild-identity-panel guild-identity-panel-concept" );
		_identityPanel.Style.FlexDirection = FlexDirection.Column;
		_identityPanel.Style.FlexGrow = 1;
		_identityPanel.Style.MinHeight = Length.Pixels( 0 );
		_identityPanel.Style.Overflow = OverflowMode.Scroll;

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		var center = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-center guild-center-column guild-center-column-concept", flexWeight: 34f );
		center.Style.FlexDirection = FlexDirection.Column;
		center.Style.MinHeight = Length.Pixels( 0 );
		center.Style.Overflow = OverflowMode.Hidden;

		_centerVictory = ThornsUiFactory.AddPanel( center, "guild-victory-section guild-victory-section-concept" );
		_centerVictory.Style.FlexDirection = FlexDirection.Column;
		_centerVictory.Style.FlexGrow = 1;
		_centerVictory.Style.MinHeight = Length.Pixels( 0 );
		_centerVictory.Style.Overflow = OverflowMode.Hidden;

		_centerActivity = ThornsUiFactory.AddPanel( center, "guild-activity-section guild-activity-section-concept concept-section" );
		_centerActivity.Style.FlexDirection = FlexDirection.Column;
		_centerActivity.Style.FlexShrink = 0;
		_centerActivity.Style.Padding = Length.Pixels( 10 );
		_centerActivity.Style.MarginTop = Length.Pixels( 8 );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		var right = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right guild-right-column guild-right-column-concept", flexWeight: 42f );
		right.Style.FlexDirection = FlexDirection.Column;
		right.Style.MinHeight = Length.Pixels( 0 );
		right.Style.Overflow = OverflowMode.Hidden;

		_rightMembers = ThornsUiFactory.AddPanel( right, "guild-members-panel guild-members-panel-concept" );
		_rightMembers.Style.FlexDirection = FlexDirection.Column;
		_rightMembers.Style.FlexGrow = 1;
		_rightMembers.Style.MinHeight = Length.Pixels( 0 );
		_rightMembers.Style.Overflow = OverflowMode.Hidden;

		_rightLeaderboard = ThornsUiFactory.AddPanel( right, "guild-leaderboard-panel guild-leaderboard-panel-concept concept-section" );
		_rightLeaderboard.Style.FlexDirection = FlexDirection.Column;
		_rightLeaderboard.Style.FlexShrink = 0;
		_rightLeaderboard.Style.Padding = Length.Pixels( 10 );
		_rightLeaderboard.Style.MarginTop = Length.Pixels( 8 );

		_centerManagement = ThornsUiFactory.AddPanel( center, "guild-members-drawer guild-members-drawer-concept" );
		_centerManagement.Style.Display = DisplayMode.None;
		_centerManagement.Style.FlexDirection = FlexDirection.Column;
		_centerManagement.Style.FlexShrink = 0;
		_centerManagement.Style.MaxHeight = Length.Pixels( 240 );
		_centerManagement.Style.Overflow = OverflowMode.Scroll;
		_centerManagement.Style.MarginTop = Length.Pixels( 8 );

	}

	void SetLeaderboardTab( GuildLeaderboardTab tab )
	{
		_leaderboardTab = tab;
		_lastGuildRevision = -1;
		Rebuild();
	}

}
