namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed partial class ThornsSkillsScreen
{
	Panel _contentRow;
	Panel _categoryNavWrap;
	Panel _categorySpacer;
	Panel _pointsFooterPanel;
	Panel _treeScroll;
	Panel _detailColumn;
	Panel _detailScroll;
	Panel _detailBody;
	Panel _detailFooter;

	void BuildSkillsLayout()
	{
		AddClass( "skills-screen skills-screen-concept" );
		Style.Position = PositionMode.Relative;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "skills-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		_categoryColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-left skills-cat-column skills-cat-column-concept", flexWeight: 26f );
		_categoryColumn.Style.FlexDirection = FlexDirection.Column;
		_categoryColumn.Style.MinHeight = Length.Pixels( 0 );
		_categoryColumn.Style.Overflow = OverflowMode.Hidden;
		ThornsTheme.CreateSectionHeader( _categoryColumn, "SKILL CATEGORIES", "skills-categories-header" );

		_categoryNavWrap = ThornsUiFactory.AddPanel( _categoryColumn, "skills-category-nav" );
		_categoryNavWrap.Style.FlexDirection = FlexDirection.Column;
		_categoryNavWrap.Style.FlexShrink = 0;

		_categorySpacer = ThornsUiFactory.AddPanel( _categoryColumn, "skills-category-spacer" );
		_categorySpacer.Style.FlexGrow = 1;
		_categorySpacer.Style.MinHeight = Length.Pixels( 8 );

		_pointsFooterPanel = ThornsUiFactory.AddPanel( _categoryColumn, "skill-points-box concept-section" );
		_pointsFooterPanel.Style.FlexDirection = FlexDirection.Column;
		_pointsFooterPanel.Style.AlignItems = Align.Center;
		_pointsFooterPanel.Style.JustifyContent = Justify.Center;
		_pointsFooterPanel.Style.FlexShrink = 0;
		_pointsFooterPanel.Style.Padding = Length.Pixels( 14 );
		_pointsFooterPanel.Style.Width = Length.Percent( 100 );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_treeColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-center skills-tree-column skills-tree-column-concept", flexWeight: 44f );
		_treeColumn.Style.FlexDirection = FlexDirection.Column;
		_treeColumn.Style.MinHeight = Length.Pixels( 0 );
		_treeColumn.Style.Overflow = OverflowMode.Hidden;
		_treeColumn.Style.Padding = Length.Pixels( 0 );

		_treeScroll = ThornsUiFactory.AddPanel( _treeColumn, "skills-tree-scroll" );
		_treeScroll.Style.Display = DisplayMode.Flex;
		_treeScroll.Style.FlexDirection = FlexDirection.Column;
		_treeScroll.Style.FlexGrow = 1;
		_treeScroll.Style.MinHeight = Length.Pixels( 0 );
		_treeScroll.Style.Overflow = OverflowMode.Scroll;
		_treeScroll.Style.Width = Length.Percent( 100 );
		_treeScroll.Style.Padding = Length.Pixels( 10 );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_detailColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right skills-detail-column skills-detail-column-concept", flexWeight: 30f );
		_detailColumn.Style.FlexDirection = FlexDirection.Column;
		_detailColumn.Style.MinHeight = Length.Pixels( 0 );
		_detailColumn.Style.Overflow = OverflowMode.Hidden;
		_detailColumn.Style.Padding = Length.Pixels( 0 );
		_detail = _detailColumn;

		var detailHeaderWrap = ThornsUiFactory.AddPanel( _detailColumn, "skills-detail-header-wrap" );
		detailHeaderWrap.Style.FlexShrink = 0;
		detailHeaderWrap.Style.Padding = Length.Pixels( 10 );
		detailHeaderWrap.Style.PaddingBottom = Length.Pixels( 6 );
		ThornsTheme.CreateSectionHeader( detailHeaderWrap, "SKILL DETAILS", "skills-detail-header" );

		_detailScroll = ThornsUiFactory.AddPanel( _detailColumn, "skill-detail-scroll" );
		_detailScroll.Style.FlexDirection = FlexDirection.Column;
		_detailScroll.Style.FlexGrow = 1;
		_detailScroll.Style.MinHeight = Length.Pixels( 0 );
		_detailScroll.Style.Overflow = OverflowMode.Scroll;
		_detailScroll.Style.PaddingLeft = Length.Pixels( 10 );
		_detailScroll.Style.PaddingRight = Length.Pixels( 10 );
		_detailBody = _detailScroll;

		_detailFooter = ThornsUiFactory.AddPanel( _detailColumn, "skill-detail-footer-wrap" );
		_detailFooter.Style.FlexShrink = 0;
		_detailFooter.Style.Padding = Length.Pixels( 10 );
		_detailFooter.Style.PaddingTop = Length.Pixels( 0 );
	}

	static ThornsSkillDefinition ResolveSelectedSkill( ThornsSkillsSnapshotDto snap )
	{
		var selected = ThornsDefinitionRegistry.GetSkill( snap.SelectedSkillId );
		if ( selected is not null && selected.Category == snap.ActiveCategory )
			return selected;

		return ThornsDefinitionRegistry.AllSkills.Values
			.Where( s => s.Category == snap.ActiveCategory )
			.OrderBy( s => s.Tier )
			.ThenBy( s => s.DisplayName )
			.FirstOrDefault();
	}

	void AddSkillBullet( Panel parent, string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		ThornsUiFactory.AddPassiveLabel( parent, text, "skill-detail-bullet-text" );
	}

	static string TierRomanLabel( int tier ) => ThornsSkillUiCatalog.TierLabel( tier );

}
