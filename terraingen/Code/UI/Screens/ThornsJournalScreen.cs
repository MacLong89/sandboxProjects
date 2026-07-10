namespace Terraingen.UI.Screens;



using Sandbox.UI;

using Terraingen.GameData;

using Terraingen.Multiplayer;

using Terraingen.Player;

using Terraingen.Progression;

using Terraingen.UI;

using Terraingen.UI.Components;
using Terraingen.UI.Core;
using Terraingen.UI.Presenters;



public sealed partial class ThornsJournalScreen : ThornsScreenBase

{

	static readonly Color GoalCompleteColor = new( 0.47f, 0.75f, 0.33f );

	static readonly Color GoalActiveColor = new( 0.9f, 0.55f, 0.15f );

	static readonly Color GoalLockedColor = new( 0.45f, 0.48f, 0.52f );



	Panel _sectionColumn;

	Panel _navWrap;

	Panel _list;

	Panel _detail;



	public ThornsJournalScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }



	protected override void Build()
	{
		BuildJournalLayout();
	}



	protected override void OnRevision( UiRevisionChannel channel, int revision )

	{

		_ = revision;

		if ( channel == UiRevisionChannel.Journal )
			Rebuild();

	}



	public override void OnShown()

	{

		ThornsDefinitionRegistry.EnsureInitialized();

		if ( ThornsMultiplayer.IsHostOrOffline )
			ThornsPlayerGameplay.Local?.HostRefreshJourneyJournal();
		else if ( ThornsUiClientState.HasSnapshot )
			ThornsJourneyProgression.HostMigrateJournalSnapshot( ThornsUiClientState.Snapshot.Journal );

		base.OnShown();

	}



	public override void Rebuild()

	{

		if ( !ThornsUiClientState.HasSnapshot )

		{

			ShowEmptyState( "Syncing journal…" );

			return;

		}



		RebuildNavFilters();

		RebuildList();

		RebuildCenterStory();

	}



	void RebuildList()
	{
		if ( _list is null || !_list.IsValid )
			return;

		_list.DeleteChildren( true );

		var journal = ThornsUiClientState.Snapshot.Journal;

		if ( journal.ActiveSection == ThornsJournalSection.VictoryPaths )
			Host.SetJournalSection( ThornsJournalSection.Goals );

		if ( _conceptCategory == JournalConceptCategory.Notes )
		{
			RebuildAchievementsList( journal );
			return;
		}

		switch ( journal.ActiveSection )
		{
			case ThornsJournalSection.Goals:
				if ( _conceptCategory is JournalConceptCategory.Quests or JournalConceptCategory.Story )
					RebuildGoalsList( journal );
				break;
			case ThornsJournalSection.Discoveries:
				RebuildDiscoveriesList( journal );
				break;
			case ThornsJournalSection.Events:
				RebuildEventsList( journal );
				break;
			case ThornsJournalSection.Achievements:
				RebuildAchievementsList( journal );
				break;
		}
	}

	void RebuildGoalsList( ThornsJournalSnapshotDto journal )
	{
		var entries = FilterGoalEntries( journal ).ToList();

		if ( entries.Count == 0 )
		{
			ThornsTheme.CreateMuted( _list,
				"More journal pages will appear as you survive, explore, and uncover this world." );
			return;
		}

		foreach ( var goal in entries )
			AddCompactQuestCard( goal, journal );
	}

	IEnumerable<ThornsJournalGoalProgressDto> FilterGoalEntries( ThornsJournalSnapshotDto journal )
	{
		var goals = VisibleGoals( journal ).AsEnumerable();

		goals = _conceptCategory switch
		{
			JournalConceptCategory.Story => goals.Where( g => IsMainQuest( g.GoalId ) ),
			JournalConceptCategory.Quests => goals,
			_ => Enumerable.Empty<ThornsJournalGoalProgressDto>()
		};

		if ( _conceptCategory is JournalConceptCategory.Quests or JournalConceptCategory.Story )
			goals = goals.Where( g => g.State != ThornsGoalState.Completed );

		return goals.OrderBy( g => ThornsDefinitionRegistry.GetGoal( g.GoalId )?.SortOrder ?? 999 );
	}

	static IEnumerable<ThornsJournalGoalProgressDto> VisibleGoals( ThornsJournalSnapshotDto journal )
	{
		foreach ( var goal in journal.Goals )
		{
			var def = ThornsDefinitionRegistry.GetGoal( goal.GoalId );
			if ( def is null )
				continue;
			if ( ThornsJourneyProgression.IsVisibleInJournal( def, goal ) )
				yield return goal;
		}
	}



	void RebuildDiscoveriesList( ThornsJournalSnapshotDto journal )

	{

		var entries = FilterDiscoveriesForCategory( journal, _conceptCategory )
			.OrderBy( d => d.Category )
			.ThenBy( d => d.Discovered )
			.ThenBy( d => d.Title )
			.ToList();

		if ( entries.Count == 0 )

		{

			ThornsTheme.CreateMuted( _list, "No discoveries registered yet." );

			return;

		}



		foreach ( var entry in entries )
		{
			var captured = entry;

			var card = ThornsUiFactory.AddClickable( _list, "journal-discovery-card concept-section",
				() => Host.SetSelectedDiscovery( captured.Id ) );
			card.SetClass( "selected", captured.Id == journal.SelectedDiscoveryId );
			card.SetClass( "discovered", captured.Discovered );
			ApplyJournalCardShell( card, minHeightPx: 56 );

			var status = ThornsUiFactory.AddPanel( card, "journal-status-icon" );
			status.SetClass( "done", captured.Discovered );
			ApplyJournalStatusIcon( status );
			ThornsUiFactory.AddPassiveLabel( status, captured.Discovered ? "✓" : "", "journal-status-glyph" );

			var icon = ThornsUiFactory.AddPanel( card, "journal-discovery-icon slot-icon" );
			icon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuJournalListIcon );
			icon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuJournalListIcon );
			icon.Style.FlexShrink = 0;
			icon.Style.PointerEvents = PointerEvents.None;
			icon.Style.Opacity = captured.Discovered ? 1f : 0.35f;
			if ( captured.Discovered && !string.IsNullOrWhiteSpace( captured.IconPath ) )
				ThornsIconCache.ApplyToPanel( icon, captured.IconPath );

			var body = ThornsUiFactory.AddPanel( card, "journal-card-body" );
			ApplyJournalCardBody( body );

			ThornsUiFactory.AddPassiveLabel( body,
				captured.Discovered ? captured.Title : "???",
				"journal-card-title" );
			ThornsUiFactory.AddPassiveLabel( body, captured.Category, "journal-card-desc" );
		}
	}



	void RebuildEventsList( ThornsJournalSnapshotDto journal )

	{

		if ( journal.CompletedEventIds.Count == 0 )

		{

			ThornsTheme.CreateMuted( _list, "No world events completed yet." );

			return;

		}



		foreach ( var eventId in journal.CompletedEventIds )

		{

			var row = ThornsUiFactory.AddPanel( _list, "journal-simple-card completed" );

			ApplyJournalCardShell( row, minHeightPx: 44 );

			var status = ThornsUiFactory.AddPanel( row, "journal-status-icon done" );

			ApplyJournalStatusIcon( status );

			ThornsUiFactory.AddPassiveLabel( status, "✓", "journal-status-glyph" );

			ThornsUiFactory.AddPassiveLabel( row, ThornsJournalEventCatalog.DisplayName( eventId ), "journal-card-title" );

		}

	}



	void RebuildAchievementsList( ThornsJournalSnapshotDto journal )

	{

		if ( journal.UnlockedAchievementIds.Count == 0 )

		{

			ThornsTheme.CreateMuted( _list, "No achievements unlocked yet." );

			return;

		}



		foreach ( var id in journal.UnlockedAchievementIds )

		{

			var row = ThornsUiFactory.AddPanel( _list, "journal-simple-card completed" );

			ApplyJournalCardShell( row, minHeightPx: 44 );

			var status = ThornsUiFactory.AddPanel( row, "journal-status-icon done" );

			ApplyJournalStatusIcon( status );

			ThornsUiFactory.AddPassiveLabel( status, "✓", "journal-status-glyph" );

			ThornsUiFactory.AddPassiveLabel( row, ThornsJournalProgress.AchievementDisplayName( id ), "journal-card-title" );

		}

	}



	static void ApplyJournalCardShell( Panel card, int minHeightPx )
	{
		card.Style.Display = DisplayMode.Flex;
		card.Style.FlexDirection = FlexDirection.Row;
		card.Style.AlignItems = Align.Center;
		card.Style.Width = Length.Percent( 100 );
		card.Style.MinHeight = Length.Pixels( minHeightPx );
		card.Style.FlexShrink = 0;
	}

	static void ApplyJournalCardBody( Panel body )
	{
		body.Style.Display = DisplayMode.Flex;
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinWidth = Length.Pixels( 0 );
		body.Style.Overflow = OverflowMode.Hidden;
	}

	static void ApplyJournalStatusIcon( Panel status )
	{
		status.Style.Display = DisplayMode.Flex;
		status.Style.FlexDirection = FlexDirection.Row;
		status.Style.AlignItems = Align.Center;
		status.Style.JustifyContent = Justify.Center;
		status.Style.FlexShrink = 0;
	}

	static void ApplyJournalProgressRow( Panel row )
	{
		row.Style.Display = DisplayMode.Flex;
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.Width = Length.Percent( 100 );
	}

	static void TryApplyHeroImage( Panel hero, string imagePath )
	{
		if ( hero is null || !hero.IsValid || string.IsNullOrWhiteSpace( imagePath ) )
			return;

		try
		{
			var tex = Texture.Load( imagePath );
			if ( tex is not null && tex.IsValid )
				hero.Style.BackgroundImage = tex;
		}
		catch
		{
			// optional goal art
		}
	}

	void ShowEmptyState( string message )
	{
		_navWrap?.DeleteChildren( true );
		_list?.DeleteChildren( true );
		_activeQuestPanel?.DeleteChildren( true );
		_centerStory?.DeleteChildren( true );
		_centerHero?.DeleteChildren( true );
		if ( _centerHero is not null && _centerHero.IsValid )
			_centerHero.Style.BackgroundImage = null;
		_rightObjectives?.DeleteChildren( true );
		_rightRewards?.DeleteChildren( true );
		_detailActions?.DeleteChildren( true );
		ThornsTheme.CreateMuted( _list, message );
	}

}
