namespace Terraingen.UI.Screens.Guild;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.NpcGuild;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;
using Terraingen.UI.Presenters;
using Terraingen.Victory;

/// <summary>Renders guild command center sections from host-built snapshots (no game logic).</summary>
public static class ThornsGuildPanelBuilders
{
	public static void BuildIdentityPanel(
		Panel parent,
		ThornsGuildSnapshotDto snap,
		string accountKey,
		ref TextEntry renameEntry,
		ref TextEntry noticeEntry,
		Action onRenameCommitted,
		Action onNoticeCommitted,
		Action toggleManagement )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "GUILD OVERVIEW" );

		var bannerRow = ThornsUiFactory.AddPanel( parent, "guild-overview-banner-row" );
		bannerRow.Style.FlexDirection = FlexDirection.Row;
		bannerRow.Style.AlignItems = Align.FlexStart;
		bannerRow.Style.MarginBottom = Length.Pixels( 10 );

		var bannerWrap = ThornsUiFactory.AddPanel( bannerRow, "guild-overview-banner-wrap" );
		var emblem = ThornsUiFactory.AddPanel( bannerWrap, "guild-overview-banner slot-icon" );
		ThornsIconCache.ApplyToPanel( emblem, string.IsNullOrWhiteSpace( snap.BannerIconPath )
			? ThornsGuildUiCatalog.GuildEmblemPath
			: snap.BannerIconPath );

		var identity = ThornsUiFactory.AddPanel( bannerRow, "guild-overview-identity" );
		identity.Style.FlexDirection = FlexDirection.Column;
		identity.Style.FlexGrow = 1;
		identity.Style.MinWidth = Length.Pixels( 0 );
		identity.Style.MarginLeft = Length.Pixels( 10 );

		var self = snap.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		var canManage = self is not null && string.Equals( self.Rank, "Leader", StringComparison.OrdinalIgnoreCase );

		if ( canManage )
		{
			var renameRow = ThornsUiFactory.AddPanel( identity, "guild-rename-row" );
			renameEntry = renameRow.AddChild( new TextEntry() );
			renameEntry.AddClass( "guild-cc-name-entry" );
			renameEntry.Text = snap.GuildName;
			renameEntry.AddEventListener( "onblur", onRenameCommitted );
			ThornsUiFactory.AddClickable( renameRow, "guild-rename-btn", "✎", onRenameCommitted );
		}
		else
		{
			ThornsUiFactory.AddLabel( identity, snap.GuildName.ToUpper(), "guild-cc-name" );
		}

		var motto = string.IsNullOrWhiteSpace( snap.Motto )
			? ThornsGuildUiPresenter.DefaultMotto( snap.IsNpcGuild )
			: snap.Motto;
		ThornsUiFactory.AddPassiveLabel( identity, motto, "guild-cc-motto" );

		var levelRow = ThornsUiFactory.AddPanel( parent, "guild-overview-level-row" );
		levelRow.Style.FlexDirection = FlexDirection.Row;
		levelRow.Style.AlignItems = Align.Center;
		levelRow.Style.JustifyContent = Justify.SpaceBetween;
		levelRow.Style.MarginBottom = Length.Pixels( 4 );
		ThornsUiFactory.AddPassiveLabel( levelRow, "GUILD LEVEL", "guild-overview-stat-label" );
		ThornsUiFactory.AddPassiveLabel( levelRow, $"{Math.Max( 1, snap.GuildLevel )}", "guild-overview-level-value" );

		var xpTrack = ThornsUiFactory.AddPanel( parent, "guild-overview-xp-track" );
		xpTrack.Style.Width = Length.Percent( 100 );
		xpTrack.Style.Height = Length.Pixels( 8 );
		xpTrack.Style.MarginBottom = Length.Pixels( 4 );
		var xpFill = ThornsUiFactory.AddPanel( xpTrack, "guild-overview-xp-fill" );
		var xpFrac = snap.GuildXpToNext > 0f ? Math.Clamp( snap.GuildXp / snap.GuildXpToNext, 0f, 1f ) : 0f;
		xpFill.Style.Width = Length.Fraction( xpFrac );

		ThornsUiFactory.AddPassiveLabel( parent,
			ThornsGuildUiPresenter.FormatGuildXp( snap.GuildXp, snap.GuildXpToNext ),
			"guild-overview-xp-label" );

		var overview = snap.Overview ?? new ThornsGuildOverviewDto();
		AddOverviewStat( parent, "MEMBERS", ThornsGuildUiPresenter.FormatMemberCount( snap.Members.Count ) );
		AddOverviewStat( parent, "GUILD POINTS", ThornsVictoryUiPresenter.FormatScore( overview.VictoryScore ) );
		AddOverviewStat( parent, "RIVAL FACTION", ThornsGuildUiPresenter.FormatAllianceOrRival( snap ) );

		var online = snap.Members.Where( m => m.IsOnline ).OrderByDescending( m =>
			string.Equals( m.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( m => string.Equals( m.Rank, "Officer", StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( m => m.DisplayName )
			.ToList();

		ThornsTheme.CreateSectionHeader( parent, $"ONLINE MEMBERS ({online.Count})" );
		var onlineList = ThornsUiFactory.AddPanel( parent, "guild-online-list" );
		onlineList.Style.FlexDirection = FlexDirection.Column;
		onlineList.Style.FlexGrow = 1;
		onlineList.Style.MinHeight = Length.Pixels( 0 );
		onlineList.Style.Overflow = OverflowMode.Scroll;

		if ( online.Count == 0 )
		{
			ThornsTheme.CreateMuted( onlineList, "No members online right now." );
		}
		else
		{
			foreach ( var member in online.Take( 12 ) )
				AddOnlineMemberRow( onlineList, member );
		}

		ThornsUiFactory.AddClickable( parent, "guild-management-btn guild-view-members-btn", "VIEW ALL MEMBERS", toggleManagement );

		_ = noticeEntry;
		_ = onNoticeCommitted;
	}

	public static void BuildVictoryPathsPanel( Panel parent, ThornsGuildCommandSnapshotDto command, ThornsMenuHost host )
	{
		parent.DeleteChildren( true );

		var headerRow = ThornsUiFactory.AddPanel( parent, "guild-victory-header" );
		headerRow.Style.FlexDirection = FlexDirection.Column;
		headerRow.Style.FlexShrink = 0;
		headerRow.Style.MarginBottom = Length.Pixels( 10 );
		ThornsTheme.CreateSectionHeader( headerRow, "VICTORY PATHS" );
		ThornsUiFactory.AddPassiveLabel( headerRow,
			"Four paths. One world to claim. Your guild's progress is shared across all members.",
			"guild-cc-section-sub" );

		var cardsRow = ThornsUiFactory.AddPanel( parent, "guild-victory-cards-row guild-victory-cards-mockup" );
		cardsRow.Style.Width = Length.Percent( 100 );
		cardsRow.Style.FlexGrow = 1;
		cardsRow.Style.MinHeight = Length.Pixels( 0 );
		var paths = command?.Victory?.Paths;
		if ( paths is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "Victory data syncing…" );
			return;
		}

		foreach ( var path in paths )
		{
			var pathId = path.PathId;
			var selected = string.Equals( command.SelectedVictoryPathId, pathId, StringComparison.OrdinalIgnoreCase );
			var accent = ThornsVictoryUiPresenter.PathAccentColor( pathId );

			var card = ThornsUiFactory.AddClickable( cardsRow, "guild-victory-card guild-victory-card-mockup concept-section",
				() => host.SetVictoryPath( pathId ) );
			card.AddClass( ThornsVictoryUiPresenter.PathCssClass( pathId ) );
			card.SetClass( "selected", selected );

			var hero = ThornsUiFactory.AddPanel( card, "guild-victory-card-hero" );
			if ( !ThornsIconCache.ApplyToPanel( hero, path.IconPath ) )
				hero.Style.BackgroundColor = accent.WithAlpha( 0.35f );

			var body = ThornsUiFactory.AddPanel( card, "guild-victory-card-body" );
			ThornsUiFactory.AddPassiveLabel( body, path.DisplayName.ToUpper(), "guild-victory-card-title" );
			ThornsUiFactory.AddPassiveLabel( body, path.Summary, "guild-victory-card-summary" );

			var pctRow = ThornsUiFactory.AddPanel( body, "guild-victory-card-pct-ring-row" );
			pctRow.Style.FlexDirection = FlexDirection.Row;
			pctRow.Style.AlignItems = Align.Center;
			var ring = ThornsUiFactory.AddPanel( pctRow, "guild-victory-card-pct-ring" );
			ThornsUiFactory.AddPassiveLabel( ring, ThornsVictoryUiPresenter.FormatPercent( path.PercentComplete ), "guild-victory-card-pct" );
			ThornsUiFactory.AddPassiveLabel( pctRow,
				ThornsGuildUiPresenter.FormatPathProgressDetail( path ).ToUpperInvariant(),
				"guild-victory-card-progress-detail" );

			var milestoneBox = ThornsUiFactory.AddPanel( body, "guild-victory-card-milestone-box" );
			milestoneBox.Style.FlexDirection = FlexDirection.Column;
			ThornsUiFactory.AddPassiveLabel( milestoneBox, "NEXT MILESTONE", "guild-victory-card-milestone-label" );
			var milestoneText = string.IsNullOrWhiteSpace( path.NextMilestoneTitle )
				? "All milestones reached."
				: path.NextMilestoneTitle;
			ThornsUiFactory.AddPassiveLabel( milestoneBox, milestoneText, "guild-victory-card-milestone" );
		}
	}

	public static void BuildActivityPanel( Panel parent, ThornsGuildCommandSnapshotDto command )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "RECENT ACTIVITY" );

		var feed = command?.WorldActivity;
		if ( feed is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "No recorded activity yet." );
			return;
		}

		var scroll = ThornsUiFactory.AddPanel( parent, "guild-activity-feed-mockup" );
		scroll.Style.FlexDirection = FlexDirection.Column;
		foreach ( var act in feed.Take( 6 ) )
			AddActivityRow( scroll, act );
	}

	public static void BuildNoticesPanel(
		Panel parent,
		ThornsGuildSnapshotDto snap,
		string accountKey,
		ref TextEntry noticeEntry,
		Action onNoticeCommitted,
		Action focusNoticeEntry )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "GUILD NOTICES" );

		var self = snap.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		var canManage = self is not null && string.Equals( self.Rank, "Leader", StringComparison.OrdinalIgnoreCase );

		var noticeScroll = ThornsUiFactory.AddPanel( parent, "guild-notices-scroll" );
		noticeScroll.Style.FlexDirection = FlexDirection.Column;
		noticeScroll.Style.FlexGrow = 1;
		noticeScroll.Style.MinHeight = Length.Pixels( 0 );
		noticeScroll.Style.Overflow = OverflowMode.Scroll;

		var noticeMessage = snap.Notice?.Message ?? snap.Announcement ?? "";
		if ( string.IsNullOrWhiteSpace( noticeMessage ) )
		{
			if ( canManage )
			{
				var editCard = ThornsUiFactory.AddPanel( noticeScroll, "guild-notice-card guild-notice-card-edit" );
				editCard.Style.FlexDirection = FlexDirection.Column;
				ThornsUiFactory.AddPassiveLabel( editCard, "GUILD NOTICE", "guild-notice-card-title" );
				noticeEntry = editCard.AddChild( new TextEntry() );
				noticeEntry.AddClass( "guild-notice-entry" );
				noticeEntry.Placeholder = "Post a guild notice…";
				noticeEntry.AddEventListener( "onblur", onNoticeCommitted );
			}
			else
			{
				ThornsTheme.CreateMuted( noticeScroll, "No guild notice posted yet." );
			}
		}
		else if ( canManage )
		{
			var editCard = ThornsUiFactory.AddPanel( noticeScroll, "guild-notice-card guild-notice-card-edit" );
			editCard.Style.FlexDirection = FlexDirection.Column;
			ThornsUiFactory.AddPassiveLabel( editCard, "GUILD NOTICE", "guild-notice-card-title" );
			noticeEntry = editCard.AddChild( new TextEntry() );
			noticeEntry.AddClass( "guild-notice-entry" );
			noticeEntry.Text = noticeMessage;
			noticeEntry.Placeholder = "Post a guild notice…";
			noticeEntry.AddEventListener( "onblur", onNoticeCommitted );
			var authorLine = ThornsGuildUiPresenter.FormatNoticeAuthorLine( snap.Notice );
			if ( !string.IsNullOrWhiteSpace( authorLine ) )
				ThornsUiFactory.AddPassiveLabel( editCard, authorLine, "guild-notice-author" );
		}
		else
		{
			AddNoticeCard( noticeScroll, "GUILD NOTICE", noticeMessage, snap.Notice );
		}

		foreach ( var act in snap.Activity?.Take( 4 ) ?? Enumerable.Empty<ThornsGuildActivityDto>() )
		{
			var title = string.IsNullOrWhiteSpace( act.Kind ) ? "Activity" : act.Kind.Replace( '_', ' ' );
			AddNoticeCard( noticeScroll, title.ToUpperInvariant(), act.Message, null, act.TimestampUtc );
		}

		if ( canManage )
		{
			ThornsUiFactory.AddClickable( parent, "guild-management-btn guild-edit-notice-btn", "EDIT NOTICE",
				focusNoticeEntry );
		}
	}

	public static void BuildComparisonPanel( Panel parent, ThornsGuildCommandSnapshotDto command, string ownGuildId )
	{
		parent.DeleteChildren( true );

		var headerRow = ThornsUiFactory.AddPanel( parent, "guild-cc-section-header-row" );
		var titleCol = ThornsUiFactory.AddPanel( headerRow, "guild-cc-section-titles" );
		ThornsTheme.CreateSectionHeader( titleCol, "GUILD PROGRESS COMPARISON" );
		ThornsUiFactory.AddPassiveLabel( titleCol, "Live standings from host-tracked guild victory progress.", "guild-cc-section-sub" );

		var rows = command?.ComparisonRows;
		if ( rows is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "No guilds registered on this server yet." );
			return;
		}

		var table = ThornsUiFactory.AddPanel( parent, "guild-comparison-table" );
		var header = ThornsUiFactory.AddPanel( table, "guild-comparison-header-row" );
		AddComparisonHeader( header, "GUILD", "guild-col-name" );
		foreach ( var def in ThornsVictoryPathCatalog.All )
			AddComparisonHeader( header, def.DisplayName.ToUpper(), ThornsVictoryUiPresenter.PathCssClass( def.PathId ) );

		foreach ( var row in rows )
		{
			var isSelf = string.Equals( row.GuildId, ownGuildId, StringComparison.OrdinalIgnoreCase );
			var dataRow = ThornsUiFactory.AddPanel( table, "guild-comparison-data-row" );
			dataRow.SetClass( "self", isSelf );
			dataRow.SetClass( "npc", row.IsNpcGuild );

			var nameCell = ThornsUiFactory.AddPanel( dataRow, "guild-comparison-cell guild-col-name" );
			if ( row.IsNpcGuild && row.IsEliminated )
				dataRow.SetClass( "eliminated", true );

			var nameRow = ThornsUiFactory.AddPanel( nameCell, "guild-comparison-name-row" );
			if ( row.IsNpcGuild && row.IsEliminated )
			{
				var defeated = ThornsUiFactory.AddPanel( nameRow, "guild-comparison-defeated-icon slot-icon" );
				ThornsIconCache.ApplyToPanel( defeated, ThornsIconRegistry.Hud( "alert" ) );
			}
			ThornsUiFactory.AddPassiveLabel( nameRow, row.GuildName, "guild-comparison-guild-name" );
			if ( isSelf )
				ThornsUiFactory.AddPassiveLabel( nameRow, "(You)", "guild-comparison-you-tag" );
			if ( row.IsNpcGuild )
				ThornsUiFactory.AddPassiveLabel( nameRow, "(NPC)", "guild-comparison-npc-tag" );

			foreach ( var def in ThornsVictoryPathCatalog.All )
			{
				var pct = row.PathRows?.FirstOrDefault( p => string.Equals( p.PathId, def.PathId, StringComparison.OrdinalIgnoreCase ) )
					?.PercentComplete ?? 0f;
				var cell = ThornsUiFactory.AddPanel( dataRow, $"guild-comparison-cell guild-col-path {ThornsVictoryUiPresenter.PathCssClass( def.PathId )}" );
				ThornsUiFactory.AddPassiveLabel( cell, ThornsVictoryUiPresenter.FormatPercent( pct ), "guild-comparison-pct" );
				var miniWrap = ThornsUiFactory.AddPanel( cell, "guild-comparison-mini-bar-wrap" );
				new ThornsProgressBar( miniWrap, ThornsVictoryUiPresenter.PathAccentColor( def.PathId ) ).SetFraction( pct / 100f );
			}

		}
	}

	public static void BuildStrategicColumn( Panel parent, ThornsGuildSnapshotDto guild, ThornsGuildCommandSnapshotDto command )
	{
		parent.DeleteChildren( true );

		var rivalsSection = ThornsUiFactory.AddPanel( parent, "guild-strategic-section" );
		BuildRivalFactionsPanel( rivalsSection, guild.RivalNpcGuilds );

		var leadersSection = ThornsUiFactory.AddPanel( parent, "guild-strategic-section" );
		ThornsTheme.CreateSectionHeader( leadersSection, "PATH LEADERS" );
		var pathLeaders = command?.PathLeaders;
		if ( pathLeaders is null or { Count: 0 } || pathLeaders.All( l => l.GuildProgress <= 0 ) )
		{
			ThornsTheme.CreateMuted( leadersSection, "No guild has scored on a victory path yet." );
		}
		else
		{
			foreach ( var leader in pathLeaders )
			{
				if ( leader.GuildProgress <= 0 )
					continue;

				var row = ThornsUiFactory.AddPanel( leadersSection, "guild-world-leader-row" );
				row.AddClass( ThornsVictoryUiPresenter.PathCssClass( leader.PathId ) );
				ThornsUiFactory.AddPassiveLabel( row, leader.PathDisplayName.ToUpper(), "guild-world-leader-path" );
				ThornsUiFactory.AddPassiveLabel( row, leader.GuildLeaderName, "guild-world-leader-name" );
				ThornsUiFactory.AddPassiveLabel( row,
					ThornsVictoryUiPresenter.FormatPercent( leader.GuildPercentComplete ),
					"guild-world-leader-world-pct" );
			}
		}

		var rankingsSection = ThornsUiFactory.AddPanel( parent, "guild-strategic-section" );
		ThornsTheme.CreateSectionHeader( rankingsSection, "ALL GUILDS OVERVIEW" );
		var rankings = command?.GlobalRankings;
		if ( rankings is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( rankingsSection, "No guilds registered yet." );
		}
		else
		{
			foreach ( var entry in rankings )
			{
				var isSelf = string.Equals( entry.ScopeKey, guild.GuildId, StringComparison.OrdinalIgnoreCase );
				var row = ThornsUiFactory.AddPanel( rankingsSection, "guild-rank-row" );
				row.SetClass( "self", isSelf );
				row.SetClass( "npc", entry.IsNpcGuild );
				var rankText = entry.Progress > 0 ? ThornsVictoryUiPresenter.FormatRank( entry.Rank ) : "—";
				ThornsUiFactory.AddPassiveLabel( row, rankText, "guild-rank-num" );
				var nameCol = ThornsUiFactory.AddPanel( row, "guild-rank-name-col" );
				ThornsUiFactory.AddPassiveLabel( nameCol, entry.DisplayName, "guild-rank-name" );
				if ( isSelf )
					ThornsUiFactory.AddPassiveLabel( nameCol, "(You)", "guild-comparison-you-tag" );
				if ( entry.IsNpcGuild )
					ThornsUiFactory.AddPassiveLabel( nameCol, "(NPC)", "guild-comparison-npc-tag" );
			}
		}

		var activitySection = ThornsUiFactory.AddPanel( parent, "guild-activity-section" );
		ThornsTheme.CreateSectionHeader( activitySection, "RECENT GUILD ACTIVITY" );
		var feed = command?.WorldActivity;
		if ( feed is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( activitySection, "No recorded activity yet." );
			return;
		}

		var scroll = ThornsUiFactory.AddPanel( activitySection, "guild-activity-feed" );
		foreach ( var act in feed.Take( 16 ) )
			AddActivityRow( scroll, act );
	}

	static void BuildRivalFactionsPanel( Panel parent, IReadOnlyList<ThornsNpcGuildRivalDto> rivals )
	{
		ThornsTheme.CreateSectionHeader( parent, "RIVAL FACTIONS" );
		var active = rivals?.Where( r => r is not null && r.HasRival ).ToList();
		if ( active is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "No rival guilds are active on this world." );
			return;
		}

		foreach ( var rival in active )
			BuildRivalFactionBox( parent, rival );
	}

	static void BuildRivalFactionBox( Panel parent, ThornsNpcGuildRivalDto rival )
	{
		var box = ThornsUiFactory.AddPanel( parent, "guild-rival-box" );
		ThornsUiFactory.AddPassiveLabel( box, rival.GuildName, "guild-rival-name" );

		if ( rival.IsEliminated )
			ThornsUiFactory.AddPassiveLabel( box, "Eliminated", "guild-rival-eliminated" );
		else
		{
			var target = Math.Max( 1, rival.OutpostTarget );
			ThornsUiFactory.AddPassiveLabel( box, $"{rival.OutpostCount}/{target}", "guild-rival-outposts" );
		}
	}

	public static void BuildIdentityPanelConcept(
		Panel parent,
		ThornsGuildSnapshotDto snap,
		string accountKey,
		ref TextEntry renameEntry,
		ref TextEntry noticeEntry,
		Action onRenameCommitted,
		Action onNoticeCommitted,
		Action toggleManagement,
		ThornsPlayerGameplay player = null )
	{
		parent.DeleteChildren( true );

		var bannerWrap = ThornsUiFactory.AddPanel( parent, "guild-concept-banner-wrap" );
		var emblem = ThornsUiFactory.AddPanel( bannerWrap, "guild-concept-banner slot-icon" );
		ThornsIconCache.ApplyToPanel( emblem, string.IsNullOrWhiteSpace( snap.BannerIconPath )
			? ThornsGuildUiCatalog.GuildEmblemPath
			: snap.BannerIconPath );

		var self = snap.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		var canManage = self is not null && string.Equals( self.Rank, "Leader", StringComparison.OrdinalIgnoreCase );

		if ( canManage )
		{
			var renameRow = ThornsUiFactory.AddPanel( parent, "guild-concept-name-row" );
			renameEntry = renameRow.AddChild( new TextEntry() );
			renameEntry.AddClass( "guild-concept-name-entry" );
			renameEntry.Text = snap.GuildName.ToUpperInvariant();
			renameEntry.AddEventListener( "onblur", onRenameCommitted );
		}
		else
		{
			ThornsUiFactory.AddLabel( parent, snap.GuildName.ToUpperInvariant(), "guild-concept-name" );
		}

		var motto = string.IsNullOrWhiteSpace( snap.Motto )
			? ThornsGuildUiPresenter.DefaultMotto( snap.IsNpcGuild )
			: snap.Motto;
		ThornsUiFactory.AddPassiveLabel( parent, motto, "guild-concept-motto" );

		var levelSection = ThornsUiFactory.AddPanel( parent, "guild-concept-level-section" );
		var levelHeader = ThornsUiFactory.AddPanel( levelSection, "guild-concept-level-header" );
		var levelIcon = ThornsUiFactory.AddPanel( levelHeader, "guild-concept-level-icon slot-icon" );
		ThornsIconCache.ApplyToPanel( levelIcon, ThornsIconRegistry.Guild( "rank" ) );
		ThornsUiFactory.AddPassiveLabel( levelHeader, "GUILD LEVEL", "guild-concept-level-label" );
		ThornsUiFactory.AddPassiveLabel( levelHeader, $"{Math.Max( 1, snap.GuildLevel )}", "guild-concept-level-badge" );

		var xpRow = ThornsUiFactory.AddPanel( levelSection, "guild-concept-xp-row" );
		var xpTrack = ThornsUiFactory.AddPanel( xpRow, "guild-concept-xp-track" );
		var xpFill = ThornsUiFactory.AddPanel( xpTrack, "guild-concept-xp-fill" );
		var xpFrac = snap.GuildXpToNext > 0f ? Math.Clamp( snap.GuildXp / snap.GuildXpToNext, 0f, 1f ) : 0f;
		xpFill.Style.Width = Length.Fraction( xpFrac );
		ThornsUiFactory.AddPassiveLabel( xpRow,
			ThornsGuildUiPresenter.FormatGuildXp( snap.GuildXp, snap.GuildXpToNext ),
			"guild-concept-xp-label" );

		AddConceptStatRow( parent, "member", "Members", ThornsGuildUiPresenter.FormatMemberCount( snap.Members.Count, 50 ) );
		AddConceptStatRow( parent, "dominion", "Playstyle", "PvP / PvE" );
		AddConceptStatRow( parent, "notification", "Language", "English" );
		AddConceptStatRow( parent, "home", "Created", "—" );

		var description = ThornsUiFactory.AddPanel( parent, "guild-concept-description concept-section" );
		description.Style.FlexDirection = FlexDirection.Column;
		var noticeMessage = snap.Notice?.Message ?? snap.Announcement ?? "";
		if ( canManage )
		{
			noticeEntry = description.AddChild( new TextEntry() );
			noticeEntry.AddClass( "guild-concept-description-entry" );
			noticeEntry.Text = noticeMessage;
			noticeEntry.Placeholder = "Guild description…";
			noticeEntry.AddEventListener( "onblur", onNoticeCommitted );
		}
		else if ( string.IsNullOrWhiteSpace( noticeMessage ) )
		{
			ThornsUiFactory.AddPassiveLabel( description,
				"A band of survivors united under one banner, carving out a place in the wasteland.",
				"guild-concept-description-text" );
		}
		else
		{
			ThornsUiFactory.AddPassiveLabel( description, noticeMessage, "guild-concept-description-text" );
		}

		ThornsUiFactory.AddClickable( parent, "guild-concept-btn guild-concept-btn-primary", "MANAGE GUILD", toggleManagement );
		if ( player is not null )
			ThornsUiFactory.AddClickable( parent, "guild-concept-btn guild-concept-btn-leave", "LEAVE GUILD", () => player.RequestGuildLeave() );
	}

	public static void BuildVictoryPathsPanelConcept(
		Panel parent,
		ThornsGuildCommandSnapshotDto command,
		ThornsMenuHost host )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "VICTORY PATHS" );

		var list = ThornsUiFactory.AddPanel( parent, "guild-victory-path-list" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexGrow = 1;
		list.Style.MinHeight = Length.Pixels( 0 );
		list.Style.Overflow = OverflowMode.Scroll;

		var paths = command?.Victory?.Paths;
		if ( paths is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( list, "Victory data syncing…" );
			return;
		}

		foreach ( var path in paths )
		{
			var pathId = path.PathId;
			var selected = string.Equals( command.SelectedVictoryPathId, pathId, StringComparison.OrdinalIgnoreCase );
			var accent = ThornsVictoryUiPresenter.PathAccentColor( pathId );

			var row = ThornsUiFactory.AddClickable( list, "guild-victory-path-row",
				() => host.SetVictoryPath( pathId ) );
			row.AddClass( ThornsVictoryUiPresenter.PathCssClass( pathId ) );
			row.SetClass( "selected", selected );

			var iconWrap = ThornsUiFactory.AddPanel( row, "guild-victory-path-icon-wrap" );
			var icon = ThornsUiFactory.AddPanel( iconWrap, "guild-victory-path-icon slot-icon" );
			if ( !ThornsIconCache.ApplyToPanel( icon, path.IconPath ) )
				iconWrap.Style.BackgroundColor = accent.WithAlpha( 0.35f );

			var body = ThornsUiFactory.AddPanel( row, "guild-victory-path-body" );
			ThornsUiFactory.AddPassiveLabel( body, path.DisplayName.ToUpperInvariant(), "guild-victory-path-title" );
			ThornsUiFactory.AddPassiveLabel( body, path.Summary, "guild-victory-path-summary" );

			var track = ThornsUiFactory.AddPanel( body, "guild-victory-path-track" );
			var fill = ThornsUiFactory.AddPanel( track, "guild-victory-path-fill" );
			fill.Style.Width = Length.Fraction( Math.Clamp( path.PercentComplete / 100f, 0f, 1f ) );
			fill.Style.BackgroundColor = accent;

			var stats = ThornsUiFactory.AddPanel( row, "guild-victory-path-stats" );
			ThornsUiFactory.AddPassiveLabel( stats, ThornsVictoryUiPresenter.FormatPercent( path.PercentComplete ), "guild-victory-path-pct" );
			ThornsUiFactory.AddPassiveLabel( stats, FormatMilestoneFraction( path ), "guild-victory-path-fraction" );
		}
	}

	public static void BuildActivityPanelConcept( Panel parent, ThornsGuildCommandSnapshotDto command )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "GUILD ACTIVITY" );

		var feed = command?.WorldActivity;
		if ( feed is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "No recorded activity yet." );
			return;
		}

		var scroll = ThornsUiFactory.AddPanel( parent, "guild-activity-list-concept" );
		scroll.Style.FlexDirection = FlexDirection.Column;
		foreach ( var act in feed.Take( 3 ) )
			AddActivityRowConcept( scroll, act );
	}

	public static void BuildMembersPanelConcept(
		Panel parent,
		ThornsGuildSnapshotDto snap,
		string accountKey,
		ThornsPlayerGameplay player,
		Action toggleManagement )
	{
		parent.DeleteChildren( true );

		var headerRow = ThornsUiFactory.AddPanel( parent, "guild-members-header-row" );
		ThornsTheme.CreateSectionHeader( headerRow, "GUILD MEMBERS" );
		ThornsUiFactory.AddPassiveLabel( headerRow,
			ThornsGuildUiPresenter.FormatMemberCount( snap.Members.Count, 50 ),
			"guild-members-count" );

		var list = ThornsUiFactory.AddPanel( parent, "guild-members-list-concept" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexGrow = 1;
		list.Style.MinHeight = Length.Pixels( 0 );
		list.Style.Overflow = OverflowMode.Scroll;

		var members = snap.Members
			.OrderByDescending( m => m.IsOnline )
			.ThenByDescending( m => RankSortKey( m.Rank ) )
			.ThenBy( m => m.DisplayName, StringComparer.OrdinalIgnoreCase )
			.ToList();

		if ( members.Count == 0 )
		{
			ThornsTheme.CreateMuted( list, "No members yet." );
		}
		else
		{
			foreach ( var member in members.Take( 8 ) )
				AddMemberRowConcept( list, member );
		}

		var self = snap.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		var canInvite = self is not null
		                && string.Equals( self.Rank, "Leader", StringComparison.OrdinalIgnoreCase )
		                && player is not null;

		if ( canInvite )
			ThornsUiFactory.AddClickable( parent, "guild-concept-btn guild-concept-btn-add-member", "+ ADD MEMBER", toggleManagement );

		_ = accountKey;
	}

	public static void BuildLeaderboardPanelConcept(
		Panel parent,
		ThornsGuildCommandSnapshotDto command,
		string ownGuildId,
		int activeTab,
		Action<int> onTabChanged )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateSectionHeader( parent, "GUILD LEADERBOARD" );

		var tabs = ThornsUiFactory.AddPanel( parent, "guild-leaderboard-tabs" );
		tabs.Style.FlexDirection = FlexDirection.Row;
		tabs.Style.Width = Length.Percent( 100 );
		tabs.Style.FlexShrink = 0;

		var pathTabs = ThornsVictoryPathCatalog.All.ToList();
		for ( var i = 0; i < pathTabs.Count; i++ )
			AddLeaderboardTab( tabs, pathTabs[i].DisplayName, i, activeTab, onTabChanged );

		var activePathId = LeaderboardPathIdForTab( activeTab );
		var activePathName = ThornsVictoryPathCatalog.TryGet( activePathId, out var activeDef )
			? activeDef.DisplayName
			: "Progress";

		var table = ThornsUiFactory.AddPanel( parent, "guild-leaderboard-table" );
		table.Style.FlexDirection = FlexDirection.Column;
		table.Style.Width = Length.Percent( 100 );
		table.Style.MinWidth = Length.Pixels( 0 );
		table.Style.FlexShrink = 0;

		var header = ThornsUiFactory.AddPanel( table, "guild-leaderboard-header-row" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.Width = Length.Percent( 100 );
		AddLeaderboardHeaderCell( header, "Rank", "guild-leaderboard-rank" );
		AddLeaderboardHeaderCell( header, "Guild", "guild-leaderboard-name-col" );
		AddLeaderboardHeaderCell( header, $"{activePathName} %", "guild-leaderboard-score-col" );

		var rows = BuildLeaderboardRows( command, activeTab );
		if ( rows.Count == 0 )
		{
			ThornsTheme.CreateMuted( table, "No standings yet." );
		}
		else
		{
			foreach ( var row in rows.Take( 6 ) )
			{
				var isSelf = string.Equals( row.GuildId, ownGuildId, StringComparison.OrdinalIgnoreCase );
				var dataRow = ThornsUiFactory.AddPanel( table, "guild-leaderboard-data-row" );
				dataRow.Style.FlexDirection = FlexDirection.Row;
				dataRow.Style.Width = Length.Percent( 100 );
				dataRow.SetClass( "self", isSelf );

				var rankCell = ThornsUiFactory.AddPanel( dataRow, "guild-leaderboard-cell guild-leaderboard-rank" );
				rankCell.Style.FlexDirection = FlexDirection.Row;
				rankCell.Style.AlignItems = Align.Center;
				ThornsUiFactory.AddPassiveLabel( rankCell, row.Rank > 0 ? $"{row.Rank}" : "—", "guild-leaderboard-rank-text" );

				var nameCell = ThornsUiFactory.AddPanel( dataRow, "guild-leaderboard-cell guild-leaderboard-name-col" );
				nameCell.Style.FlexDirection = FlexDirection.Row;
				nameCell.Style.AlignItems = Align.Center;
				nameCell.Style.MinWidth = Length.Pixels( 0 );
				nameCell.Style.FlexGrow = 1;
				var banner = ThornsUiFactory.AddPanel( nameCell, "guild-leaderboard-banner slot-icon" );
				banner.Style.FlexShrink = 0;
				ThornsIconCache.ApplyToPanel( banner, ThornsGuildUiCatalog.GuildEmblemPath );
				ThornsUiFactory.AddPassiveLabel( nameCell, row.GuildName, "guild-leaderboard-guild-name" );

				var scoreCell = ThornsUiFactory.AddPanel( dataRow, "guild-leaderboard-cell guild-leaderboard-score-col" );
				scoreCell.Style.FlexDirection = FlexDirection.Row;
				scoreCell.Style.AlignItems = Align.Center;
				scoreCell.Style.JustifyContent = Justify.FlexEnd;
				ThornsUiFactory.AddPassiveLabel( scoreCell, row.ScoreText, "guild-leaderboard-score" );
			}
		}
	}

	public static void BuildManagementPanel( Panel parent, ThornsGuildSnapshotDto snap, string accountKey, ThornsPlayerGameplay player )
	{
		parent.DeleteChildren( true );
		ThornsTheme.CreateHeader( parent, "GUILD MEMBERS" );

		foreach ( var member in snap.Members.OrderByDescending( m => m.IsOnline ).ThenBy( m => m.DisplayName ) )
		{
			var card = ThornsUiFactory.AddPanel( parent, "guild-member-card" );
			card.SetClass( "online", member.IsOnline );
			var avatar = ThornsUiFactory.AddPanel( card, "guild-member-avatar slot-icon" );
			ThornsIconCache.ApplyToPanel( avatar, ThornsGuildUiCatalog.MemberAvatarPath );
			var info = ThornsUiFactory.AddPanel( card, "guild-member-info" );
			ThornsUiFactory.AddPassiveLabel( info, member.DisplayName, "guild-member-name" );
			ThornsUiFactory.AddPassiveLabel( info, member.Rank, "guild-member-rank" );
			var status = ThornsUiFactory.AddPanel( card, "guild-member-status-pill" );
			status.SetClass( "online", member.IsOnline );
			ThornsUiFactory.AddPassiveLabel( status, member.IsOnline ? "ONLINE" : "OFFLINE", "guild-member-status" );
		}

		var self = snap.Members.FirstOrDefault( m => m.AccountKey == accountKey );
		if ( self is null || !string.Equals( self.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) || player is null )
			return;

		ThornsTheme.CreateHeader( parent, "INVITE PLAYERS" );
		var invitable = ThornsGuildWorldService.Instance?.ListInvitableOnlinePlayers( snap.GuildId );
		if ( invitable is null or { Count: 0 } )
		{
			ThornsTheme.CreateMuted( parent, "No one else is online to invite." );
			return;
		}

		foreach ( var (targetAccountKey, displayName) in invitable )
		{
			var account = targetAccountKey;
			var card = ThornsUiFactory.AddClickable( parent, "guild-invite-card", () => player.RequestGuildInvite( account ) );
			ThornsUiFactory.AddPassiveLabel( card, displayName, "guild-member-name" );
			ThornsUiFactory.AddPassiveLabel( card, "INVITE", "guild-invite-action" );
		}
	}

	static void AddOverviewStat( Panel parent, string label, string value )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-overview-stat-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.JustifyContent = Justify.SpaceBetween;
		row.Style.MarginBottom = Length.Pixels( 4 );
		ThornsUiFactory.AddPassiveLabel( row, label, "guild-overview-stat-label" );
		ThornsUiFactory.AddPassiveLabel( row, value, "guild-overview-stat-value" );
	}

	static void AddConceptStatRow( Panel parent, string iconKey, string label, string value )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-concept-stat-row" );
		var icon = ThornsUiFactory.AddPanel( row, "guild-concept-stat-icon slot-icon" );
		ThornsIconCache.ApplyToPanel( icon, ThornsIconRegistry.Guild( iconKey ) );
		var text = ThornsUiFactory.AddPanel( row, "guild-concept-stat-text" );
		ThornsUiFactory.AddPassiveLabel( text, label, "guild-concept-stat-label" );
		ThornsUiFactory.AddPassiveLabel( text, value, "guild-concept-stat-value" );
	}

	static void AddMemberRowConcept( Panel parent, ThornsGuildMemberDto member )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-member-row-concept" );
		row.SetClass( "online", member.IsOnline );

		var avatar = ThornsUiFactory.AddPanel( row, "guild-member-row-avatar slot-icon" );
		ThornsIconCache.ApplyToPanel( avatar, ThornsGuildUiCatalog.MemberAvatarPath );

		var rankGlyph = ThornsUiFactory.AddPanel( row, "guild-member-row-rank-glyph" );
		ThornsUiFactory.AddPassiveLabel( rankGlyph, RankGlyph( member.Rank ), "guild-member-rank-symbol" );

		var info = ThornsUiFactory.AddPanel( row, "guild-member-row-info" );
		ThornsUiFactory.AddPassiveLabel( info, member.DisplayName, "guild-member-row-name" );
		ThornsUiFactory.AddPassiveLabel( info, member.Rank, "guild-member-row-rank" );

		var statusCol = ThornsUiFactory.AddPanel( row, "guild-member-row-status-col" );
		if ( member.IsOnline )
		{
			var dot = ThornsUiFactory.AddPanel( statusCol, "guild-online-dot online" );
			_ = dot;
			ThornsUiFactory.AddPassiveLabel( statusCol, "Online", "guild-member-row-status" );
		}
		else
		{
			ThornsUiFactory.AddPassiveLabel( statusCol, "Offline", "guild-member-row-status offline" );
		}
	}

	static void AddActivityRowConcept( Panel parent, ThornsGuildActivityDto act )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-activity-row-concept" );
		var iconWrap = ThornsUiFactory.AddPanel( row, "guild-activity-icon-wrap" );
		var icon = ThornsUiFactory.AddPanel( iconWrap, "guild-activity-icon slot-icon" );
		if ( !ThornsIconCache.ApplyToPanel( icon, ThornsGuildUiCatalog.ActivityIconPath( act.Kind ) ) )
			ThornsIconCache.ApplyToPanel( icon, ThornsGuildUiCatalog.ActivityIconPathDefault );

		ThornsUiFactory.AddPassiveLabel( row, act.Message, "guild-activity-msg" );
		if ( !string.IsNullOrWhiteSpace( act.TimestampUtc ) )
			ThornsUiFactory.AddPassiveLabel( row, ThornsGuildUiPresenter.FormatRelativeTime( act.TimestampUtc ), "guild-activity-time" );
	}

	static void AddLeaderboardTab( Panel parent, string label, int tabIndex, int activeTab, Action<int> onTabChanged )
	{
		var tab = ThornsUiFactory.AddClickable( parent, "guild-leaderboard-tab", () => onTabChanged( tabIndex ) );
		tab.SetClass( "active", tabIndex == activeTab );
		ThornsUiFactory.AddPassiveLabel( tab, label, "guild-leaderboard-tab-label" );
	}

	static void AddLeaderboardHeaderCell( Panel row, string label, string columnClass )
	{
		var cell = ThornsUiFactory.AddPanel( row, $"guild-leaderboard-header-cell guild-leaderboard-cell {columnClass}" );
		cell.Style.FlexDirection = FlexDirection.Row;
		cell.Style.AlignItems = Align.Center;
		if ( columnClass.Contains( "score-col" ) )
			cell.Style.JustifyContent = Justify.FlexEnd;
		ThornsUiFactory.AddPassiveLabel( cell, label, "guild-leaderboard-header-label" );
	}

	static string LeaderboardPathIdForTab( int activeTab ) => activeTab switch
	{
		1 => ThornsVictoryPathIds.Ascension,
		2 => ThornsVictoryPathIds.Purification,
		3 => ThornsVictoryPathIds.Apex,
		_ => ThornsVictoryPathIds.Dominion
	};

	sealed class LeaderboardDisplayRow
	{
		public string GuildId { get; init; } = "";
		public string GuildName { get; init; } = "";
		public int Rank { get; init; }
		public string ScoreText { get; init; } = "—";
	}

	static List<LeaderboardDisplayRow> BuildLeaderboardRows( ThornsGuildCommandSnapshotDto command, int activeTab )
	{
		var pathId = LeaderboardPathIdForTab( activeTab );
		return command?.ComparisonRows?
			.OrderByDescending( r => ThornsVictoryUiPresenter.PathPercentForGuild( r, pathId ) )
			.ThenBy( r => r.GuildName )
			.Select( ( r, i ) => new LeaderboardDisplayRow
			{
				GuildId = r.GuildId,
				GuildName = r.GuildName,
				Rank = i + 1,
				ScoreText = ThornsVictoryUiPresenter.FormatPercent(
					ThornsVictoryUiPresenter.PathPercentForGuild( r, pathId ) )
			} )
			.ToList() ?? new List<LeaderboardDisplayRow>();
	}

	static string FormatMilestoneFraction( ThornsGuildVictoryPathEntryDto path )
	{
		if ( path is null )
			return "—";

		if ( !ThornsVictoryPathCatalog.TryGet( path.PathId, out var def ) || def.Milestones is null or { Count: 0 } )
			return ThornsGuildUiPresenter.FormatPathProgressDetail( path );

		var total = def.Milestones.Count;
		var reached = def.Milestones.Count( m => path.GuildProgress >= m.Threshold );
		return $"{reached}/{total}";
	}

	static int RankSortKey( string rank )
	{
		if ( string.Equals( rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			return 3;
		if ( string.Equals( rank, "Officer", StringComparison.OrdinalIgnoreCase ) )
			return 2;
		if ( string.Equals( rank, "Veteran", StringComparison.OrdinalIgnoreCase ) )
			return 1;
		return 0;
	}

	static string RankGlyph( string rank )
	{
		if ( string.Equals( rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			return "♛";
		if ( string.Equals( rank, "Officer", StringComparison.OrdinalIgnoreCase ) )
			return "⚔";
		if ( string.Equals( rank, "Veteran", StringComparison.OrdinalIgnoreCase ) )
			return "⬡";
		return "●";
	}

	static void AddOnlineMemberRow( Panel parent, ThornsGuildMemberDto member )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-online-member-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.JustifyContent = Justify.SpaceBetween;
		row.Style.MarginBottom = Length.Pixels( 4 );

		var nameCol = ThornsUiFactory.AddPanel( row, "guild-online-member-name-col" );
		nameCol.Style.FlexDirection = FlexDirection.Row;
		nameCol.Style.AlignItems = Align.Center;
		var dot = ThornsUiFactory.AddPanel( nameCol, "guild-online-dot" );
		dot.SetClass( "online", member.IsOnline );
		if ( string.Equals( member.Rank, "Leader", StringComparison.OrdinalIgnoreCase ) )
			ThornsUiFactory.AddPassiveLabel( nameCol, "♛", "guild-online-leader-crown" );
		ThornsUiFactory.AddPassiveLabel( nameCol, member.DisplayName, "guild-online-member-name" );

		ThornsUiFactory.AddPassiveLabel( row, member.Rank.ToUpperInvariant(), "guild-online-member-rank" );
	}

	static void AddNoticeCard( Panel parent, string title, string body, ThornsGuildNoticeDto notice, string timestampUtc = null )
	{
		var card = ThornsUiFactory.AddPanel( parent, "guild-notice-card" );
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MarginBottom = Length.Pixels( 8 );
		ThornsMenuChrome.ApplyMenuSubFrame( card );

		var titleRow = ThornsUiFactory.AddPanel( card, "guild-notice-card-title-row" );
		titleRow.Style.FlexDirection = FlexDirection.Row;
		titleRow.Style.AlignItems = Align.Center;
		var icon = ThornsUiFactory.AddPanel( titleRow, "guild-notice-card-icon slot-icon" );
		ThornsIconCache.ApplyToPanel( icon, ThornsGuildUiCatalog.ActivityIconPathDefault );
		ThornsUiFactory.AddPassiveLabel( titleRow, title, "guild-notice-card-title" );

		ThornsUiFactory.AddPassiveLabel( card, body, "guild-notice-card-body" );

		var footer = notice is not null
			? ThornsGuildUiPresenter.FormatNoticeAuthorLine( notice )
			: string.IsNullOrWhiteSpace( timestampUtc )
				? ""
				: $"Posted · {ThornsGuildUiPresenter.FormatRelativeTime( timestampUtc )}";
		if ( !string.IsNullOrWhiteSpace( footer ) )
			ThornsUiFactory.AddPassiveLabel( card, footer, "guild-notice-author" );
	}

	static void AddOverviewRow( Panel parent, string iconKey, string label, string value )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-overview-stat" );
		var icon = ThornsUiFactory.AddPanel( row, "guild-overview-glyph slot-icon" );
		ThornsIconCache.ApplyToPanel( icon, ThornsIconRegistry.Guild( iconKey ) );
		var text = ThornsUiFactory.AddPanel( row, "guild-overview-text" );
		ThornsUiFactory.AddPassiveLabel( text, label, "guild-sidebar-stat-label" );
		ThornsUiFactory.AddPassiveLabel( text, value, "guild-sidebar-stat-value" );
	}

	static void AddComparisonHeader( Panel row, string label, string pathClass )
	{
		var cellClass = label switch
		{
			"GUILD" => "guild-comparison-header-cell guild-col-name",
			_ => $"guild-comparison-header-cell guild-col-path {pathClass}"
		};
		var cell = ThornsUiFactory.AddPanel( row, cellClass );
		ThornsUiFactory.AddPassiveLabel( cell, label, "guild-comparison-header-label" );
	}

	static void AddActivityRow( Panel parent, ThornsGuildActivityDto act )
	{
		var row = ThornsUiFactory.AddPanel( parent, "guild-activity-card" );
		var iconWrap = ThornsUiFactory.AddPanel( row, "guild-activity-icon-wrap" );
		var icon = ThornsUiFactory.AddPanel( iconWrap, "guild-activity-icon slot-icon" );
		if ( !ThornsIconCache.ApplyToPanel( icon, ThornsGuildUiCatalog.ActivityIconPath( act.Kind ) ) )
			ThornsIconCache.ApplyToPanel( icon, ThornsGuildUiCatalog.ActivityIconPathDefault );

		var text = ThornsUiFactory.AddPanel( row, "guild-activity-text" );
		var msgRow = ThornsUiFactory.AddPanel( text, "guild-activity-msg-row" );
		ThornsUiFactory.AddPassiveLabel( msgRow, act.Message, "guild-activity-msg" );
		if ( !string.IsNullOrWhiteSpace( act.TimestampUtc ) )
			ThornsUiFactory.AddPassiveLabel( msgRow, ThornsGuildUiPresenter.FormatRelativeTime( act.TimestampUtc ), "guild-activity-time" );
	}
}
