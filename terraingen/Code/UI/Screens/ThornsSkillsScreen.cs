namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed partial class ThornsSkillsScreen : ThornsScreenBase
{
	Panel _categoryColumn;
	Panel _treeColumn;
	Panel _detail;
	Label _pointsFooterValue;

	public ThornsSkillsScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }

	protected override void Build() => BuildSkillsLayout();

	public override void Rebuild()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		RebuildCategories();
		RebuildTree();
		RebuildDetail();
	}

	void RebuildCategories()
	{
		if ( _categoryNavWrap is null || !_categoryNavWrap.IsValid )
			return;

		_categoryNavWrap.DeleteChildren( true );
		var snap = ThornsUiClientState.Snapshot.Skills;

		foreach ( ThornsSkillCategory cat in Enum.GetValues<ThornsSkillCategory>() )
		{
			var captured = cat;
			var active = snap.ActiveCategory == captured;
			var btn = ThornsUiFactory.AddClickable( _categoryNavWrap, "skill-cat-btn skills-category-btn",
				() => Host.SetSkillCategory( captured ) );
			btn.SetClass( "active", active );
			btn.Style.FlexDirection = FlexDirection.Row;
			btn.Style.AlignItems = Align.Center;
			btn.Style.Padding = Length.Pixels( 10 );
			btn.Style.MarginBottom = Length.Pixels( 6 );
			btn.Style.Width = Length.Percent( 100 );

			var icon = ThornsUiFactory.AddPanel( btn, "skill-cat-icon slot-icon" );
			icon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuSkillCategoryIcon );
			icon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuSkillCategoryIcon );
			icon.Style.FlexShrink = 0;
			icon.Style.MarginRight = Length.Pixels( 10 );
			ThornsIconCache.ApplyToPanel( icon, ThornsSkillUiCatalog.CategoryIconPath( captured ) );

			var textCol = ThornsUiFactory.AddPanel( btn, "skill-cat-text" );
			textCol.Style.FlexDirection = FlexDirection.Column;
			textCol.Style.FlexGrow = 1;
			textCol.Style.MinWidth = Length.Pixels( 0 );
			ThornsUiFactory.AddPassiveLabel( textCol, ThornsSkillUiCatalog.CategoryTitle( captured ), "skill-cat-label" );

			var metaCol = ThornsUiFactory.AddPanel( btn, "skill-cat-meta-col" );
			metaCol.Style.FlexDirection = FlexDirection.Row;
			metaCol.Style.AlignItems = Align.Center;
			metaCol.Style.FlexShrink = 0;
			ThornsUiFactory.AddPassiveLabel( metaCol, ThornsSkillUiCatalog.CategoryProgressText( snap, captured ), "skill-cat-progress" );
			ThornsUiFactory.AddPassiveLabel( metaCol, "›", "skill-cat-chevron" );
		}

		if ( _pointsFooterPanel is not null && _pointsFooterPanel.IsValid )
		{
			_pointsFooterPanel.DeleteChildren( true );
			ThornsUiFactory.AddPassiveLabel( _pointsFooterPanel, "SKILL POINTS AVAILABLE", "skill-points-label" );
			_pointsFooterValue = ThornsUiFactory.AddLabel( _pointsFooterPanel, $"{snap.AvailablePoints}", "skill-points-value" );
		}
	}

	void RebuildTree()
	{
		if ( _treeScroll is null || !_treeScroll.IsValid )
			return;

		_treeScroll.DeleteChildren( true );
		var snap = ThornsUiClientState.Snapshot.Skills;
		var cat = snap.ActiveCategory;

		var header = ThornsUiFactory.AddPanel( _treeScroll, "skills-tree-header skills-tree-header-concept" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.Center;
		header.Style.MarginBottom = Length.Pixels( 14 );
		header.Style.Width = Length.Percent( 100 );

		var headerIcon = ThornsUiFactory.AddPanel( header, "skills-tree-cat-icon slot-icon" );
		headerIcon.Style.Width = Length.Pixels( 36 );
		headerIcon.Style.Height = Length.Pixels( 36 );
		headerIcon.Style.FlexShrink = 0;
		headerIcon.Style.MarginRight = Length.Pixels( 10 );
		ThornsIconCache.ApplyToPanel( headerIcon, ThornsSkillUiCatalog.CategoryIconPath( cat ) );

		var headerText = ThornsUiFactory.AddPanel( header, "skills-tree-header-text" );
		headerText.Style.FlexDirection = FlexDirection.Column;
		headerText.Style.FlexGrow = 1;
		headerText.Style.MinWidth = Length.Pixels( 0 );
		ThornsUiFactory.AddLabel( headerText, ThornsSkillUiCatalog.CategoryTitle( cat ), "skills-tree-cat-title thorns-header" );
		var categoryDesc = ThornsTheme.CreateMuted( headerText, ThornsSkillUiCatalog.CategoryDescription( cat ) );
		categoryDesc.AddClass( "skills-tree-desc" );

		ThornsUiFactory.AddPassiveLabel( header, ThornsSkillUiCatalog.CategoryProgressText( snap, cat ), "skills-tree-cat-progress" );

		var skills = ThornsDefinitionRegistry.AllSkills.Values
			.Where( s => s.Category == cat )
			.OrderBy( s => s.Tier )
			.ThenBy( s => s.DisplayName )
			.ToList();

		if ( skills.Count == 0 )
		{
			ThornsTheme.CreateMuted( _treeScroll, "No skills in this category." );
			return;
		}

		var maxTier = skills.Max( s => s.Tier );
		for ( var tier = 1; tier <= maxTier; tier++ )
		{
			var tierSkills = skills.Where( s => s.Tier == tier ).ToList();
			if ( tierSkills.Count == 0 )
				continue;

			var tierRow = ThornsUiFactory.AddPanel( _treeScroll, "skill-tier-row skill-tier-row-concept" );
			tierRow.Style.FlexDirection = FlexDirection.Row;
			tierRow.Style.AlignItems = Align.Center;
			tierRow.Style.MarginBottom = Length.Pixels( 16 );
			tierRow.Style.Width = Length.Percent( 100 );

			var tierLabel = ThornsUiFactory.AddPanel( tierRow, "skill-tier-label-col" );
			tierLabel.Style.FlexShrink = 0;
			tierLabel.Style.Width = Length.Pixels( 56 );
			tierLabel.Style.MarginRight = Length.Pixels( 8 );
			ThornsUiFactory.AddPassiveLabel( tierLabel, TierRomanLabel( tier ), "skill-tier-label" );

			var nodes = ThornsUiFactory.AddPanel( tierRow, "skill-tier-nodes skill-tier-nodes-square" );
			nodes.Style.Display = DisplayMode.Flex;
			nodes.Style.FlexDirection = FlexDirection.Row;
			nodes.Style.FlexWrap = Wrap.Wrap;
			nodes.Style.JustifyContent = Justify.SpaceEvenly;
			nodes.Style.AlignItems = Align.Center;
			nodes.Style.FlexGrow = 1;
			nodes.Style.MinWidth = Length.Pixels( 0 );

			foreach ( var skill in tierSkills )
				AddSkillNode( nodes, skill, snap );

			if ( tier < maxTier )
			{
				var connector = ThornsUiFactory.AddPanel( _treeScroll, "skill-tier-connector" );
				connector.Style.Width = Length.Percent( 100 );
				connector.Style.Height = Length.Pixels( 12 );
				connector.Style.MarginLeft = Length.Pixels( 64 );
				connector.Style.MarginBottom = Length.Pixels( 4 );
			}
		}
	}

	void AddSkillNode( Panel parent, ThornsSkillDefinition skill, ThornsSkillsSnapshotDto snap )
	{
		var rank = snap.Ranks.FirstOrDefault( r => r.SkillId == skill.Id )?.Rank ?? 0;
		var locked = ThornsSkillUiCatalog.IsLocked( skill, snap );
		var maxed = rank >= skill.MaxRank;
		var selected = snap.SelectedSkillId == skill.Id;
		var cost = ThornsUpgradeBalance.NextPurchaseCost( skill, rank );
		var canBuy = !locked && !maxed && snap.AvailablePoints >= cost;

		var wrap = ThornsUiFactory.AddClickable( parent, "skill-node-wrap skill-node-wrap-square",
			() => Host.SetSelectedSkill( skill.Id ) );
		wrap.SetClass( "selected", selected );
		wrap.SetClass( "maxed", maxed );
		wrap.SetClass( "locked", locked );
		wrap.SetClass( "available", canBuy && rank == 0 );
		wrap.SetClass( "progress", rank > 0 && !maxed );

		var tile = ThornsUiFactory.AddPanel( wrap, "skill-node-square" );
		if ( locked )
		{
			var lockedIcon = ThornsUiFactory.AddPanel( tile, "skill-node-lock slot-icon" );
			ThornsIconCache.ApplyToPanel( lockedIcon, ThornsIconRegistry.InventoryUi( "lock" ) );
		}
		else
		{
			var icon = ThornsUiFactory.AddPanel( tile, "skill-node-icon slot-icon" );
			icon.Style.PointerEvents = PointerEvents.None;
			ThornsIconCache.ApplyToPanel( icon, skill.IconPath );
		}

		ThornsUiFactory.AddPassiveLabel( wrap, $"{rank}/{skill.MaxRank}", "skill-node-rank" );
	}

	void RebuildDetail()
	{
		if ( _detailBody is null || !_detailBody.IsValid )
			return;

		_detailBody.DeleteChildren( true );
		if ( _detailFooter is not null && _detailFooter.IsValid )
			_detailFooter.DeleteChildren( true );

		var snap = ThornsUiClientState.Snapshot.Skills;
		var selected = ResolveSelectedSkill( snap );
		if ( selected is null )
		{
			ThornsTheme.CreateMuted( _detailBody, "Select a skill node to inspect." );
			return;
		}

		var rank = snap.Ranks.FirstOrDefault( r => r.SkillId == selected.Id )?.Rank ?? 0;
		var cost = ThornsUpgradeBalance.NextPurchaseCost( selected, rank );
		var locked = ThornsSkillUiCatalog.IsLocked( selected, snap );
		var maxed = rank >= selected.MaxRank;

		var hero = ThornsUiFactory.AddPanel( _detailBody, "skill-detail-hero skill-detail-hero-concept" );
		hero.Style.FlexDirection = FlexDirection.Row;
		hero.Style.AlignItems = Align.Center;
		hero.Style.MarginBottom = Length.Pixels( 12 );

		var iconFrame = ThornsUiFactory.AddPanel( hero, "skill-detail-icon-frame concept-section" );
		iconFrame.Style.FlexShrink = 0;
		iconFrame.Style.FlexDirection = FlexDirection.Row;
		iconFrame.Style.Width = Length.Pixels( 88 );
		iconFrame.Style.Height = Length.Pixels( 88 );
		iconFrame.Style.MarginRight = Length.Pixels( 12 );
		iconFrame.Style.JustifyContent = Justify.Center;
		iconFrame.Style.AlignItems = Align.Center;
		var bigIcon = ThornsUiFactory.AddPanel( iconFrame, "skill-node-icon slot-icon" );
		bigIcon.Style.Width = Length.Pixels( 64 );
		bigIcon.Style.Height = Length.Pixels( 64 );
		ThornsIconCache.ApplyToPanel( bigIcon, selected.IconPath );

		var titleCol = ThornsUiFactory.AddPanel( hero, "skill-detail-title-col" );
		titleCol.Style.FlexDirection = FlexDirection.Column;
		titleCol.Style.FlexGrow = 1;
		titleCol.Style.MinWidth = Length.Pixels( 0 );
		ThornsUiFactory.AddLabel( titleCol, selected.DisplayName.ToUpperInvariant(), "skill-detail-title" );
		ThornsUiFactory.AddPassiveLabel( titleCol, $"Level {rank}/{selected.MaxRank}", "skill-detail-rank" );
		if ( !string.IsNullOrWhiteSpace( selected.Description ) )
			ThornsUiFactory.AddPassiveLabel( titleCol, selected.Description, "skill-detail-flavor" );

		var currentBlock = ThornsUiFactory.AddPanel( _detailBody, "skill-detail-current concept-section" );
		currentBlock.Style.FlexDirection = FlexDirection.Column;
		currentBlock.Style.Padding = Length.Pixels( 10 );
		currentBlock.Style.MarginBottom = Length.Pixels( 8 );
		ThornsTheme.CreateSectionHeader( currentBlock, "CURRENT LEVEL", "skill-detail-block-header-wrap" );

		if ( rank <= 0 )
			AddSkillBullet( currentBlock, "No bonus yet." );
		else
			foreach ( var line in ThornsSkillUiCatalog.CurrentBonusBullets( selected, rank ) )
				AddSkillBullet( currentBlock, line );

		if ( maxed )
		{
			var maxBlock = ThornsUiFactory.AddPanel( _detailBody, "skill-detail-max concept-section" );
			maxBlock.Style.FlexDirection = FlexDirection.Column;
			maxBlock.Style.Padding = Length.Pixels( 10 );
			maxBlock.Style.MarginBottom = Length.Pixels( 8 );
			ThornsTheme.CreateSectionHeader( maxBlock, "MAX LEVEL", "skill-detail-block-header-wrap" );

			if ( rank > 0 )
			{
				foreach ( var line in ThornsSkillUiCatalog.CurrentBonusBullets( selected, rank ) )
					AddSkillBullet( maxBlock, line );
			}
			else
			{
				AddSkillBullet( maxBlock, "Unlock this skill to gain its bonuses." );
			}
		}
		else
		{
			var nextBlock = ThornsUiFactory.AddPanel( _detailBody, "skill-detail-next concept-section" );
			nextBlock.Style.FlexDirection = FlexDirection.Column;
			nextBlock.Style.Padding = Length.Pixels( 10 );
			nextBlock.Style.MarginBottom = Length.Pixels( 8 );
			ThornsTheme.CreateSectionHeader( nextBlock, "NEXT LEVEL", "skill-detail-block-header-wrap" );
			AddSkillBullet( nextBlock, ThornsSkillUiCatalog.NextBonus( selected, rank ) );
			AddSkillBullet( nextBlock, ThornsSkillUiCatalog.RequirementText( selected, snap, cost ) );
		}

		if ( _detailFooter is null || !_detailFooter.IsValid )
			return;

		var canUpgrade = !maxed && !locked && snap.AvailablePoints >= cost;
		var upgradeLabel = maxed ? "MAX LEVEL REACHED" : locked ? "LOCKED" : $"UPGRADE ({cost} PT.)";

		if ( !canUpgrade && !maxed && !locked )
			ThornsUiFactory.AddPassiveLabel( _detailFooter, "UNMET REQUIREMENTS", "skill-detail-unmet" );

		var btn = ThornsUiFactory.AddClickable( _detailFooter, "thorns-btn-primary skill-upgrade-btn", upgradeLabel,
			() =>
			{
				if ( canUpgrade )
					ThornsPlayerGameplay.Local?.RequestSkillUnlock( selected.Id );
			} );
		btn.SetClass( "disabled", !canUpgrade );
		btn.Style.Width = Length.Percent( 100 );
		btn.Style.MinHeight = Length.Pixels( 40 );
		btn.Style.JustifyContent = Justify.Center;
		btn.Style.AlignItems = Align.Center;
	}

	protected override void OnRevision( UiRevisionChannel channel, int revision )
	{
		_ = revision;
		if ( channel != UiRevisionChannel.Skills )
			return;

		Rebuild();
		if ( _pointsFooterValue is not null && _pointsFooterValue.IsValid && ThornsUiClientState.HasSnapshot )
			_pointsFooterValue.Text = $"{ThornsUiClientState.Snapshot.Skills.AvailablePoints}";
	}
}
