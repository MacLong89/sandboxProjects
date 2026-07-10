namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed partial class ThornsJournalScreen
{
	enum JournalConceptCategory
	{
		Quests,
		Story,
		Notes,
		Lore,
		Recipes,
		Bestiary
	}

	Panel _contentRow;
	Panel _listColumn;
	Panel _listHeader;
	Panel _centerHero;
	Panel _centerStory;
	Panel _rightObjectives;
	Panel _rightRewards;
	Panel _detailActions;
	Panel _activeQuestPanel;
	JournalConceptCategory _conceptCategory = JournalConceptCategory.Quests;

	void BuildJournalLayout()
	{
		AddClass( "journal-screen journal-screen-concept" );
		Style.Position = PositionMode.Relative;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = Length.Pixels( 0 );
		Style.BackgroundColor = Color.Transparent;

		_contentRow = ThornsUiFactory.AddPanel( this, "journal-content-row" );
		_contentRow.Style.FlexDirection = FlexDirection.Row;
		_contentRow.Style.FlexGrow = 1;
		_contentRow.Style.FlexShrink = 1;
		_contentRow.Style.MinHeight = Length.Pixels( 0 );
		_contentRow.Style.Width = Length.Percent( 100 );

		_sectionColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-left journal-nav-column journal-nav-column-concept", flexWeight: 28f );
		_sectionColumn.Style.FlexDirection = FlexDirection.Column;
		_sectionColumn.Style.MinHeight = Length.Pixels( 0 );
		ThornsTheme.CreateSectionHeader( _sectionColumn, "CATEGORIES", "journal-categories-header" );

		_navWrap = ThornsUiFactory.AddPanel( _sectionColumn, "journal-nav-wrap journal-category-list" );
		_navWrap.Style.FlexDirection = FlexDirection.Column;
		_navWrap.Style.FlexShrink = 0;

		var navSpacer = ThornsUiFactory.AddPanel( _sectionColumn, "journal-nav-spacer" );
		navSpacer.Style.FlexGrow = 1;
		navSpacer.Style.MinHeight = Length.Pixels( 8 );

		_activeQuestPanel = ThornsUiFactory.AddPanel( _sectionColumn, "journal-active-quest-panel concept-section" );
		_activeQuestPanel.Style.FlexDirection = FlexDirection.Column;
		_activeQuestPanel.Style.FlexShrink = 0;
		_activeQuestPanel.Style.Width = Length.Percent( 100 );
		_activeQuestPanel.Style.Padding = Length.Pixels( 10 );
		ThornsTheme.CreateSectionHeader( _activeQuestPanel, "ACTIVE QUEST", "journal-active-quest-header" );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_listColumn = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-center journal-list-column journal-list-column-concept", flexWeight: 36f );
		_listColumn.Style.FlexDirection = FlexDirection.Column;
		_listColumn.Style.MinHeight = Length.Pixels( 0 );
		_listColumn.Style.Overflow = OverflowMode.Hidden;

		_listHeader = ThornsTheme.CreateSectionHeader( _listColumn, "QUESTS", "journal-list-header" );

		_list = ThornsUiFactory.AddPanel( _listColumn, "journal-quest-list journal-list-scroll" );
		_list.Style.Display = DisplayMode.Flex;
		_list.Style.FlexDirection = FlexDirection.Column;
		_list.Style.FlexGrow = 1;
		_list.Style.MinHeight = Length.Pixels( 0 );
		_list.Style.Overflow = OverflowMode.Scroll;
		_list.Style.Width = Length.Percent( 100 );

		ThornsTheme.CreateWoodColumnDivider( _contentRow );

		_detail = ThornsTheme.CreateMenuSectionWindow( _contentRow,
			"thorns-col-right journal-detail-column journal-detail-column-concept", flexWeight: 36f );
		_detail.Style.FlexDirection = FlexDirection.Column;
		_detail.Style.MinHeight = Length.Pixels( 0 );
		_detail.Style.Overflow = OverflowMode.Hidden;
		_detail.Style.Padding = Length.Pixels( 0 );

		_centerStory = ThornsUiFactory.AddPanel( _detail, "journal-detail-story" );
		_centerStory.Style.FlexDirection = FlexDirection.Column;
		_centerStory.Style.FlexShrink = 0;
		_centerStory.Style.Padding = Length.Pixels( 10 );
		_centerStory.Style.PaddingBottom = Length.Pixels( 6 );

		var objectivesFrame = ThornsUiFactory.AddPanel( _detail, "journal-objectives-frame concept-section" );
		objectivesFrame.Style.FlexDirection = FlexDirection.Column;
		objectivesFrame.Style.FlexShrink = 0;
		objectivesFrame.Style.Padding = Length.Pixels( 10 );
		objectivesFrame.Style.MarginLeft = Length.Pixels( 10 );
		objectivesFrame.Style.MarginRight = Length.Pixels( 10 );
		objectivesFrame.Style.MarginBottom = Length.Pixels( 8 );
		ThornsTheme.CreateSectionHeader( objectivesFrame, "OBJECTIVES", "journal-objectives-header" );
		_rightObjectives = ThornsUiFactory.AddPanel( objectivesFrame, "journal-objectives-list" );
		_rightObjectives.Style.FlexDirection = FlexDirection.Column;

		var rewardsFrame = ThornsUiFactory.AddPanel( _detail, "journal-rewards-frame concept-section" );
		rewardsFrame.Style.FlexDirection = FlexDirection.Column;
		rewardsFrame.Style.FlexShrink = 0;
		rewardsFrame.Style.Padding = Length.Pixels( 10 );
		rewardsFrame.Style.MarginLeft = Length.Pixels( 10 );
		rewardsFrame.Style.MarginRight = Length.Pixels( 10 );
		rewardsFrame.Style.MarginBottom = Length.Pixels( 8 );
		ThornsTheme.CreateSectionHeader( rewardsFrame, "REWARDS", "journal-rewards-header" );
		_rightRewards = ThornsUiFactory.AddPanel( rewardsFrame, "journal-rewards-row journal-right-rewards" );
		_rightRewards.Style.FlexDirection = FlexDirection.Row;
		_rightRewards.Style.FlexWrap = Wrap.Wrap;

		_detailActions = ThornsUiFactory.AddPanel( _detail, "journal-detail-actions" );
		_detailActions.Style.FlexDirection = FlexDirection.Row;
		_detailActions.Style.FlexShrink = 0;
		_detailActions.Style.Padding = Length.Pixels( 10 );
		_detailActions.Style.PaddingTop = Length.Pixels( 0 );

		_centerHero = ThornsUiFactory.AddPanel( _detail, "journal-detail-hero journal-center-hero" );
		_centerHero.Style.FlexGrow = 1;
		_centerHero.Style.FlexShrink = 1;
		_centerHero.Style.MinHeight = Length.Pixels( 140 );
		_centerHero.Style.MarginLeft = Length.Pixels( 10 );
		_centerHero.Style.MarginRight = Length.Pixels( 10 );
		_centerHero.Style.MarginBottom = Length.Pixels( 10 );
	}

	void RebuildNavFilters()
	{
		if ( _navWrap is null || !_navWrap.IsValid )
			return;

		_navWrap.DeleteChildren( true );
		RebuildListHeaderTitle();

		var journal = ThornsUiClientState.Snapshot.Journal;
		var visibleGoals = VisibleGoals( journal ).ToList();
		var discoveries = journal.Discoveries;

		AddConceptCategory( JournalConceptCategory.Quests, "Quests", ThornsIconRegistry.JournalSection( ThornsJournalSection.Goals ),
			visibleGoals.Count );
		AddConceptCategory( JournalConceptCategory.Story, "Story", ThornsIconRegistry.JournalSection( ThornsJournalSection.Events ),
			visibleGoals.Count( g => IsMainQuest( g.GoalId ) ) );
		AddConceptCategory( JournalConceptCategory.Notes, "Notes", ThornsIconRegistry.JournalSection( ThornsJournalSection.Achievements ),
			journal.UnlockedAchievementIds.Count );
		AddConceptCategory( JournalConceptCategory.Lore, "Lore", ThornsIconRegistry.JournalSection( ThornsJournalSection.Discoveries ),
			discoveries.Count( d => d.Category != "Creature" ) );
		AddConceptCategory( JournalConceptCategory.Recipes, "Recipes", ThornsIconRegistry.InventoryUi( "craft_build" ),
			discoveries.Count( IsRecipeDiscovery ) );
		AddConceptCategory( JournalConceptCategory.Bestiary, "Bestiary", ThornsIconRegistry.InventoryUi( "craft_medical" ),
			discoveries.Count( d => d.Category == "Creature" ) );

		RebuildActiveQuestPanel( journal );
	}

	void RebuildListHeaderTitle()
	{
		if ( _listHeader is null || !_listHeader.IsValid )
			return;

		foreach ( var child in _listHeader.Children )
		{
			if ( child is Label label && label.HasClass( "thorns-section-header-label" ) )
			{
				label.Text = ConceptCategoryListTitle( _conceptCategory );
				return;
			}
		}
	}

	static string ConceptCategoryListTitle( JournalConceptCategory category ) => category switch
	{
		JournalConceptCategory.Quests => "QUESTS",
		JournalConceptCategory.Story => "QUESTS",
		JournalConceptCategory.Notes => "NOTES",
		JournalConceptCategory.Lore => "LORE",
		JournalConceptCategory.Recipes => "RECIPES",
		JournalConceptCategory.Bestiary => "BESTIARY",
		_ => "JOURNAL"
	};

	void AddConceptCategory( JournalConceptCategory category, string label, string iconPath, int count )
	{
		var active = _conceptCategory == category;
		var row = ThornsUiFactory.AddClickable( _navWrap, "journal-category-btn journal-nav-filter", () =>
		{
			_conceptCategory = category;
			SyncJournalSectionForCategory( category );
			Rebuild();
		} );
		row.SetClass( "active", active );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.Width = Length.Percent( 100 );
		row.Style.Padding = Length.Pixels( 8 );
		row.Style.MarginBottom = Length.Pixels( 4 );

		var icon = ThornsUiFactory.AddPanel( row, "journal-category-icon slot-icon" );
		icon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuJournalSectionIcon );
		icon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuJournalSectionIcon );
		icon.Style.MinWidth = Length.Pixels( ThornsUiMetrics.MenuJournalSectionIcon );
		icon.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuJournalSectionIcon );
		icon.Style.FlexShrink = 0;
		icon.Style.MarginRight = Length.Pixels( 10 );
		icon.Style.PointerEvents = PointerEvents.None;
		ThornsIconCache.ApplyToPanel( icon, iconPath );

		var labelWrap = ThornsUiFactory.AddPanel( row, "journal-category-label-wrap" );
		labelWrap.Style.FlexDirection = FlexDirection.Row;
		labelWrap.Style.AlignItems = Align.Center;
		labelWrap.Style.JustifyContent = Justify.SpaceBetween;
		labelWrap.Style.FlexGrow = 1;
		labelWrap.Style.MinWidth = Length.Pixels( 0 );

		ThornsUiFactory.AddPassiveLabel( labelWrap, label.ToUpperInvariant(), "journal-nav-filter-label journal-category-label" );
		ThornsUiFactory.AddPassiveLabel( labelWrap, count.ToString(), "journal-nav-filter-count journal-category-count" );
	}

	void SyncJournalSectionForCategory( JournalConceptCategory category )
	{
		switch ( category )
		{
			case JournalConceptCategory.Notes:
				Host.SetJournalSection( ThornsJournalSection.Achievements );
				break;
			case JournalConceptCategory.Story:
				Host.SetJournalSection( ThornsJournalSection.Goals );
				break;
			case JournalConceptCategory.Lore:
			case JournalConceptCategory.Recipes:
			case JournalConceptCategory.Bestiary:
				Host.SetJournalSection( ThornsJournalSection.Discoveries );
				break;
			default:
				Host.SetJournalSection( ThornsJournalSection.Goals );
				break;
		}
	}

	static bool IsRecipeDiscovery( ThornsDiscoveryEntryDto entry ) =>
		entry.Category is "Resource" or "Tool" or "Consumable" or "Item";

	static IEnumerable<ThornsDiscoveryEntryDto> FilterDiscoveriesForCategory(
		ThornsJournalSnapshotDto journal,
		JournalConceptCategory category )
	{
		var discoveries = journal.Discoveries.AsEnumerable();
		return category switch
		{
			JournalConceptCategory.Lore => discoveries.Where( d => d.Category != "Creature" ),
			JournalConceptCategory.Recipes => discoveries.Where( IsRecipeDiscovery ),
			JournalConceptCategory.Bestiary => discoveries.Where( d => d.Category == "Creature" ),
			_ => discoveries
		};
	}

	static ThornsJournalGoalDefinition ResolveSelectedGoal( ThornsJournalSnapshotDto journal )
	{
		var selected = ThornsDefinitionRegistry.GetGoal( journal.SelectedGoalId );
		if ( selected is not null )
			return selected;

		var first = journal.Goals.FirstOrDefault( g =>
		{
			var def = ThornsDefinitionRegistry.GetGoal( g.GoalId );
			return def is not null && ThornsJourneyProgression.IsVisibleInJournal( def, g );
		} );

		return first is null ? null : ThornsDefinitionRegistry.GetGoal( first.GoalId );
	}

	static bool IsMainQuest( string goalId )
	{
		var def = ThornsDefinitionRegistry.GetGoal( goalId );
		return def is not null && (def.AutoPinUntilComplete || def.JourneyCategory == ThornsJourneyCategory.Survival);
	}

	static int DisplayQuestLevel( ThornsJournalGoalDefinition def ) =>
		Math.Max( 1, (def.SortOrder + 4) / 5 );

	static string DisplayQuestType( ThornsJournalGoalDefinition def ) =>
		def.AutoPinUntilComplete ? "MAIN QUEST" : $"{def.JourneyCategory.ToString().ToUpper()} QUEST";

	static string TruncateText( string text, int maxLen )
	{
		if ( string.IsNullOrWhiteSpace( text ) || text.Length <= maxLen )
			return text ?? "";
		return text[..( maxLen - 1 )].TrimEnd() + "…";
	}

	void RebuildActiveQuestPanel( ThornsJournalSnapshotDto journal )
	{
		if ( _activeQuestPanel is null || !_activeQuestPanel.IsValid )
			return;

		foreach ( var child in _activeQuestPanel.Children.ToArray() )
		{
			if ( child.HasClass( "journal-active-quest-header" ) || child.HasClass( "thorns-section-header" ) )
				continue;
			child.Delete( true );
		}

		var activeGoalId = !string.IsNullOrWhiteSpace( journal.HudPinnedGoalId )
			? journal.HudPinnedGoalId
			: journal.Goals.FirstOrDefault( g => g.State == ThornsGoalState.Active )?.GoalId;

		if ( string.IsNullOrWhiteSpace( activeGoalId ) )
		{
			ThornsTheme.CreateMuted( _activeQuestPanel, "No active quest tracked." );
			return;
		}

		var def = ThornsDefinitionRegistry.GetGoal( activeGoalId );
		var prog = journal.Goals.FirstOrDefault( g => g.GoalId == activeGoalId );
		if ( def is null || prog is null )
		{
			ThornsTheme.CreateMuted( _activeQuestPanel, "No active quest tracked." );
			return;
		}

		ThornsUiFactory.AddPassiveLabel( _activeQuestPanel, def.Title.ToUpperInvariant(), "journal-active-quest-title" );

		var task = def.Tasks.FirstOrDefault();
		var progress = task is null ? null : prog.Tasks.FirstOrDefault( t => t.TaskId == task.Id );
		var label = task is null || string.IsNullOrWhiteSpace( task.Label ) ? def.RequirementText : task.Label;
		var done = prog.State == ThornsGoalState.Completed;

		var row = ThornsUiFactory.AddPanel( _activeQuestPanel, "journal-objective-row journal-active-quest-objective" );
		row.SetClass( "done", done );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginTop = Length.Pixels( 6 );

		var box = ThornsUiFactory.AddPanel( row, "journal-objective-check" );
		box.SetClass( "checked", done );
		ThornsUiFactory.AddPassiveLabel( box, done ? "✓" : "", "journal-objective-glyph" );
		ThornsUiFactory.AddPassiveLabel( row, label, "journal-objective-label" );

		var rewardsRow = ThornsUiFactory.AddPanel( _activeQuestPanel, "journal-active-quest-rewards" );
		rewardsRow.Style.FlexDirection = FlexDirection.Row;
		rewardsRow.Style.FlexWrap = Wrap.Wrap;
		rewardsRow.Style.MarginTop = Length.Pixels( 8 );
		RebuildRewardRow( rewardsRow, def, compact: true );
	}

	void RebuildCenterStory()
	{
		_centerStory?.DeleteChildren( true );
		_centerHero?.DeleteChildren( true );
		_centerHero.Style.BackgroundImage = null;
		_rightObjectives?.DeleteChildren( true );
		_rightRewards?.DeleteChildren( true );
		_detailActions?.DeleteChildren( true );

		var journal = ThornsUiClientState.Snapshot.Journal;

		if ( journal.ActiveSection == ThornsJournalSection.Discoveries
		     && _conceptCategory is JournalConceptCategory.Lore
		        or JournalConceptCategory.Recipes
		        or JournalConceptCategory.Bestiary )
		{
			RebuildDiscoveryCenter( journal );
			return;
		}

		if ( journal.ActiveSection == ThornsJournalSection.Events
		     || journal.ActiveSection == ThornsJournalSection.Achievements
		     || _conceptCategory == JournalConceptCategory.Notes )
		{
			ThornsTheme.CreateMuted( _centerStory, "Select an entry from the list." );
			return;
		}

		var selected = ResolveSelectedGoal( journal );
		if ( selected is null )
		{
			ThornsTheme.CreateMuted( _centerStory, "Select a quest to read its journal entry." );
			ThornsTheme.CreateMuted( _rightObjectives, "No objectives selected." );
			return;
		}

		var prog = journal.Goals.FirstOrDefault( g => g.GoalId == selected.Id );
		var completed = prog?.State == ThornsGoalState.Completed;

		ThornsTheme.CreateSectionHeader( _centerStory, selected.Title.ToUpperInvariant(), "journal-detail-title-header" );

		var metaRow = ThornsUiFactory.AddPanel( _centerStory, "journal-center-meta-row" );
		metaRow.Style.FlexDirection = FlexDirection.Row;
		metaRow.Style.AlignItems = Align.Center;
		metaRow.Style.MarginTop = Length.Pixels( 2 );
		metaRow.Style.MarginBottom = Length.Pixels( 6 );
		ThornsUiFactory.AddPassiveLabel( metaRow, DisplayQuestType( selected ), "journal-center-type" );
		if ( prog?.State == ThornsGoalState.Active )
			ThornsUiFactory.AddPassiveLabel( metaRow, "ACTIVE", "journal-detail-active-badge" );

		var journalText = ThornsJourneyProgression.DisplayJournalText( selected );
		if ( !string.IsNullOrWhiteSpace( journalText ) )
			ThornsUiFactory.AddPassiveLabel( _centerStory, journalText, "journal-center-desc" );

		TryApplyHeroImage( _centerHero, selected.ImagePath );
		RebuildObjectiveRows( selected, prog );
		RebuildRewardRow( _rightRewards, selected, compact: false );
		RebuildDetailActions( selected, prog, completed, journal );
	}

	void RebuildObjectiveRows( ThornsJournalGoalDefinition selected, ThornsJournalGoalProgressDto prog )
	{
		if ( _rightObjectives is null || !_rightObjectives.IsValid )
			return;

		var tasks = selected.Tasks.Count > 0
			? selected.Tasks
			: [new ThornsJournalTaskDefinition { Id = "default", Label = selected.RequirementText, TargetCount = selected.TargetCount }];

		foreach ( var taskDef in tasks )
		{
			var progress = prog?.Tasks.FirstOrDefault( t => t.TaskId == taskDef.Id );
			var current = progress?.Current ?? 0;
			var target = progress?.Target ?? taskDef.TargetCount;
			if ( target <= 0 )
				target = Math.Max( 1, selected.TargetCount );
			var done = prog?.State == ThornsGoalState.Completed || current >= target;

			var row = ThornsUiFactory.AddPanel( _rightObjectives, "journal-objective-row" );
			row.SetClass( "done", done );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.MarginBottom = Length.Pixels( 6 );

			var box = ThornsUiFactory.AddPanel( row, "journal-objective-check" );
			box.SetClass( "checked", done );
			ThornsUiFactory.AddPassiveLabel( box, done ? "✓" : "", "journal-objective-glyph" );

			var label = string.IsNullOrWhiteSpace( taskDef.Label ) ? selected.RequirementText : taskDef.Label;
			if ( target > 1 )
				label = $"{label} ({Math.Min( current, target )}/{target})";

			ThornsUiFactory.AddPassiveLabel( row, label, "journal-objective-label" );
		}
	}

	void RebuildRewardRow( Panel parent, ThornsJournalGoalDefinition selected, bool compact )
	{
		if ( parent is null || !parent.IsValid )
			return;

		parent.DeleteChildren( true );

		var rewards = selected.Rewards.Count > 0
			? selected.Rewards
			: [new ThornsJournalRewardDto
			{
				Label = $"{selected.XpReward} XP",
				Kind = "xp",
				IconPath = ThornsIconRegistry.Hud( "xp" )
			}];

		var row = ThornsUiFactory.AddPanel( parent, compact ? "journal-center-rewards-row" : "journal-rewards-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.FlexWrap = Wrap.Wrap;
		row.Style.MarginTop = Length.Pixels( compact ? 0 : 0 );

		foreach ( var reward in rewards )
		{
			var card = ThornsUiFactory.AddPanel( row, compact ? "journal-center-reward-chip" : "journal-reward-card" );
			card.Style.FlexDirection = FlexDirection.Column;
			card.Style.AlignItems = Align.Center;
			card.Style.Padding = Length.Pixels( compact ? 4 : 8 );
			card.Style.MarginRight = Length.Pixels( 8 );
			card.Style.MarginBottom = Length.Pixels( compact ? 4 : 8 );

			var icon = ThornsUiFactory.AddPanel( card, "journal-reward-icon slot-icon" );
			icon.Style.Width = Length.Pixels( compact ? 28 : ThornsUiMetrics.MenuJournalRewardIcon );
			icon.Style.Height = Length.Pixels( compact ? 28 : ThornsUiMetrics.MenuJournalRewardIcon );
			if ( !string.IsNullOrWhiteSpace( reward.IconPath ) )
				ThornsIconCache.ApplyToPanel( icon, reward.IconPath );

			ThornsUiFactory.AddPassiveLabel( card, reward.Label, "journal-reward-label" );
		}
	}

	void RebuildDetailActions( ThornsJournalGoalDefinition selected, ThornsJournalGoalProgressDto prog,
		bool completed, ThornsJournalSnapshotDto journal )
	{
		if ( _detailActions is null || !_detailActions.IsValid )
			return;

		var isPinned = !string.IsNullOrEmpty( journal.HudPinnedGoalId )
		               && string.Equals( journal.HudPinnedGoalId, selected.Id, StringComparison.OrdinalIgnoreCase );
		var canPin = prog?.State == ThornsGoalState.Active;

		if ( !canPin && !isPinned )
			return;

		var pinBtn = ThornsUiFactory.AddClickable( _detailActions, "journal-pin-btn inventory-craft-footer-btn thorns-btn-primary",
			isPinned ? "PINNED TO SURVIVAL LOG" : "PIN TO SURVIVAL LOG",
			() => Host.PinGoalToHud( selected.Id ) );
		pinBtn.SetClass( "pinned", isPinned );
	}

	void RebuildDiscoveryCenter( ThornsJournalSnapshotDto journal )
	{
		var filtered = FilterDiscoveriesForCategory( journal, _conceptCategory ).ToList();
		var entry = filtered.FirstOrDefault( d => d.Id == journal.SelectedDiscoveryId )
		           ?? filtered.FirstOrDefault();
		if ( entry is null )
		{
			ThornsTheme.CreateMuted( _centerStory, "Select a discovery." );
			return;
		}

		ThornsTheme.CreateSectionHeader( _centerStory,
			entry.Discovered ? entry.Title.ToUpperInvariant() : "UNKNOWN",
			"journal-detail-title-header" );
		ThornsUiFactory.AddPassiveLabel( _centerStory, entry.Category, "journal-center-type" );

		var statusText = entry.Discovered ? "Discovered — logged in your journal." : "Not yet encountered.";
		ThornsUiFactory.AddPassiveLabel( _centerStory, statusText, "journal-center-desc" );

		if ( !entry.Discovered )
		{
			ThornsTheme.CreateMuted( _rightObjectives,
				entry.Category == "Creature"
					? "Get close to this creature in the world to reveal it."
					: "Loot or gather this item once to check it off." );
		}

		if ( entry.Discovered && !string.IsNullOrWhiteSpace( entry.IconPath ) )
		{
			var heroIcon = ThornsUiFactory.AddPanel( _centerHero, "journal-discovery-hero-icon slot-icon" );
			heroIcon.Style.Width = Length.Percent( 100 );
			heroIcon.Style.Height = Length.Percent( 100 );
			ThornsIconCache.ApplyToPanel( heroIcon, entry.IconPath );
		}
	}

	void AddCompactQuestCard( ThornsJournalGoalProgressDto goal, ThornsJournalSnapshotDto journal )
	{
		var def = ThornsDefinitionRegistry.GetGoal( goal.GoalId );
		if ( def is null )
			return;

		var completed = goal.State == ThornsGoalState.Completed;
		var locked = goal.State == ThornsGoalState.Locked;
		var active = goal.State == ThornsGoalState.Active;
		var selected = goal.GoalId == journal.SelectedGoalId;

		var card = ThornsUiFactory.AddClickable( _list, "journal-quest-card concept-section",
			() => Host.SetSelectedGoal( goal.GoalId ) );
		card.SetClass( "selected", selected );
		card.SetClass( "completed", completed );
		card.SetClass( "locked", locked );
		card.SetClass( "active", active );
		card.Style.FlexDirection = FlexDirection.Row;
		card.Style.AlignItems = Align.Stretch;
		card.Style.Width = Length.Percent( 100 );
		card.Style.Padding = Length.Pixels( 8 );
		card.Style.MarginBottom = Length.Pixels( 6 );
		card.Style.Overflow = OverflowMode.Hidden;

		var thumb = ThornsUiFactory.AddPanel( card, "journal-quest-card-thumb slot-icon" );
		thumb.Style.Width = Length.Pixels( 64 );
		thumb.Style.Height = Length.Pixels( 64 );
		thumb.Style.MinWidth = Length.Pixels( 64 );
		thumb.Style.MinHeight = Length.Pixels( 64 );
		thumb.Style.FlexShrink = 0;
		thumb.Style.MarginRight = Length.Pixels( 10 );
		thumb.Style.BackgroundColor = new Color( 0.15f, 0.15f, 0.15f, 0.9f );
		if ( !string.IsNullOrWhiteSpace( def.ImagePath ) )
			TryApplyHeroImage( thumb, def.ImagePath );
		else
			ThornsIconCache.ApplyToPanel( thumb, ThornsIconRegistry.JournalSection( ThornsJournalSection.Goals ) );

		var body = ThornsUiFactory.AddPanel( card, "journal-quest-card-body" );
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinWidth = Length.Pixels( 0 );
		body.Style.JustifyContent = Justify.Center;

		var titleRow = ThornsUiFactory.AddPanel( body, "journal-quest-card-title-row" );
		titleRow.Style.FlexDirection = FlexDirection.Row;
		titleRow.Style.AlignItems = Align.Center;
		titleRow.Style.JustifyContent = Justify.SpaceBetween;
		titleRow.Style.Width = Length.Percent( 100 );

		ThornsUiFactory.AddPassiveLabel( titleRow, def.Title.ToUpperInvariant(), "journal-quest-card-title" );
		if ( active )
			ThornsUiFactory.AddPassiveLabel( titleRow, "ACTIVE", "journal-quest-active-badge" );

		var excerpt = TruncateText( ThornsJourneyProgression.DisplayJournalText( def ), 72 );
		if ( !string.IsNullOrWhiteSpace( excerpt ) )
			ThornsUiFactory.AddPassiveLabel( body, excerpt, "journal-quest-card-desc" );

		if ( !string.IsNullOrWhiteSpace( def.RequirementText ) )
			ThornsUiFactory.AddPassiveLabel( body, def.RequirementText, "journal-quest-card-objective" );
	}
}
