using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Data;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using DepthChartData = Dynasty.Data.DepthChart;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Formation;
using Dynasty.Systems.Season;

namespace Dynasty.Systems.Franchise;

public sealed class InboxSystem : ILeagueSystem
{
	public string SystemId => "inbox";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context )
	{
		_context = context;
		context.Events.Subscribe<PlayerInjuredEvent>( OnPlayerInjured );
		context.Events.Subscribe<PhaseChangedEvent>( OnPhaseChanged );
		context.Events.Subscribe<GameSimulatedEvent>( OnGameSimulated );
	}

	public void OnLeagueCreated( LeagueState state )
	{
		Add( state, InboxCategory.League, InboxPriority.High, "Welcome to Sunday Dynasty",
			"You're the GM. Follow your Inbox and To-Do list — each item takes you where you need to go.",
			false, navigateTab: "home" );

		if ( state.Phase == LeaguePhase.Draft )
		{
			Add( state, InboxCategory.League, InboxPriority.High, "Step 1 — Complete the draft",
				"New to dynasty mode? Use Sim Rest of Draft on the Draft tab to jump into the season quickly. Want the full experience? Draft manually when you're on the clock.",
				false, navigateTab: "draft" );

			Add( state, InboxCategory.League, InboxPriority.Normal, "Step 2 — Set your lineup",
				"After the draft, open Team and review starters on the Formation View.",
				true, navigateTab: "team" );

			Add( state, InboxCategory.League, InboxPriority.Normal, "Step 3 — Play your first game",
				"Use Schedule to simulate your matchup, watch the replay, then Continue to advance the week.",
				false, navigateTab: "schedule" );
		}
		else
		{
			Add( state, InboxCategory.League, InboxPriority.Normal, "Step 1 — Set your lineup",
				"Open Team and assign starters on the Formation View. Starters drive every game simulation.",
				true, navigateTab: "team" );

			Add( state, InboxCategory.League, InboxPriority.Normal, "Step 2 — Play your games",
				"Use Schedule to simulate your matchup, then Continue to advance the league week.",
				false, navigateTab: "schedule" );
		}
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		if ( humanTeam.IsEmpty )
			return;

		switch ( phase )
		{
			case LeaguePhase.Preseason:
				Add( state, InboxCategory.League, InboxPriority.High,
					$"Season {state.CurrentSeason} — Training camp opens",
					"Roster carries over, but the slate is fresh. Set your depth chart and trim to the roster limit.",
					true, humanTeam, navigateTab: "team" );
				break;

			case LeaguePhase.RegularSeason:
				Add( state, InboxCategory.League, InboxPriority.High,
					$"Season {state.CurrentSeason} regular season begins",
					"Your record resets to 0-0. Chase the playoffs from Week 1.",
					false, humanTeam, navigateTab: "schedule" );
				break;

			case LeaguePhase.Playoffs:
				Add( state, InboxCategory.League, InboxPriority.High,
					"Playoffs — win or go home",
					"Every game matters now. Simulate your playoff matchup from Schedule.",
					true, humanTeam, navigateTab: "schedule" );
				break;

			case LeaguePhase.Offseason when state.OffseasonSubPhase == OffseasonSubPhase.Retirements:
				Add( state, InboxCategory.League, InboxPriority.Normal,
					$"Season {state.CurrentSeason - 1} is in the books",
					"Review Legacy for your all-time record, then advance through offseason milestones.",
					false, humanTeam, navigateTab: "legacy" );
				break;

			case LeaguePhase.FreeAgency:
				Add( state, InboxCategory.Contract, InboxPriority.High,
					"Free agency is open",
					"Sign free agents before rivals beat you to your targets.",
					true, humanTeam, navigateTab: "freeagency" );
				break;

			case LeaguePhase.Draft:
				Add( state, InboxCategory.Draft, InboxPriority.High,
					$"Season {state.CurrentSeason} draft is here",
					"Scout prospects and build through the draft board.",
					true, humanTeam, navigateTab: "draft" );
				break;
		}

		if ( phase == LeaguePhase.Offseason )
			GenerateOffseasonSubphaseMessage( state, humanTeam );
	}

	public void OnOffseasonSubphaseEntered( LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		if ( humanTeam.IsEmpty )
			return;

		GenerateOffseasonSubphaseMessage( state, humanTeam );
	}

	public void OnWeekAdvanced( LeagueState state ) => GenerateWeeklyMessages( state );

	public void OnSeasonEnded( LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		if ( humanTeam.IsEmpty || !state.Teams.TryGetValue( humanTeam, out var team ) )
			return;

		var record = TeamRecordArchive.FormatRecord( team.Record.Wins, team.Record.Losses, team.Record.Ties );
		Add( state, InboxCategory.League, InboxPriority.High,
			$"Season {state.CurrentSeason} final: {record}",
			"Your season record is archived to Legacy. A new offseason begins next.",
			false, humanTeam, navigateTab: "legacy" );
	}

	void OnPhaseChanged( PhaseChangedEvent e ) { }
	void OnPlayerInjured( PlayerInjuredEvent e ) { }
	void OnGameSimulated( GameSimulatedEvent e ) { }

	void GenerateOffseasonSubphaseMessage( LeagueState state, TeamId humanTeam )
	{
		switch ( state.OffseasonSubPhase )
		{
			case OffseasonSubPhase.CoachingChanges:
				Add( state, InboxCategory.Coaching, InboxPriority.Normal,
					"Coaching carousel underway",
					"League head coaches are evaluated. Check News for league-wide moves.",
					false, humanTeam, navigateTab: "news" );
				break;

			case OffseasonSubPhase.Scouting:
				Add( state, InboxCategory.League, InboxPriority.Normal,
					"Scouting season",
					"Review draft prospects before the rookie draft.",
					false, humanTeam, navigateTab: "draft" );
				break;

			case OffseasonSubPhase.FacilityUpgrades:
				Add( state, InboxCategory.General, InboxPriority.Normal,
					"Invest in your franchise",
					"Upgrade stadium, training, and medical facilities before camp.",
					true, humanTeam, navigateTab: "facilities" );
				break;
		}
	}

	void GenerateWeeklyMessages( LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );

		if ( state.Phase == LeaguePhase.Preseason )
		{
			Add( state, InboxCategory.Roster, InboxPriority.Normal, "Roster cuts loom",
				"Trim your roster before the regular season opener.", false, navigateTab: "team" );

			if ( !humanTeam.IsEmpty && state.Teams.TryGetValue( humanTeam, out var preTeam )
				&& preTeam.RosterPlayerIds.Count > state.Settings.RosterSize )
			{
				Add( state, InboxCategory.Roster, InboxPriority.High,
					"Cut your roster to the limit",
					$"You have {preTeam.RosterPlayerIds.Count} players — cut to {state.Settings.RosterSize} before kickoff.",
					true, humanTeam, navigateTab: "team" );
			}
		}

		if ( state.Phase == LeaguePhase.FreeAgency && state.FreeAgency.IsOpen )
		{
			Add( state, InboxCategory.Contract, InboxPriority.High, "Free agency is open",
				"Submit offers to available players before rivals sign your targets.", true,
				humanTeam, navigateTab: "freeagency" );
		}

		if ( state.Phase == LeaguePhase.Draft && state.Draft.IsActive )
			AddDraftInboxIfNeeded( state );

		if ( !humanTeam.IsEmpty && state.Teams.TryGetValue( humanTeam, out var team ) )
		{
			if ( TeamNeedsDepthChart( team ) )
			{
				Add( state, InboxCategory.Roster, InboxPriority.High,
					"Depth chart incomplete",
					"Assign starters at every position before you play.",
					true, humanTeam, navigateTab: "team" );
			}

			if ( HumanHasUnplayedGame( state, humanTeam ) )
			{
				Add( state, InboxCategory.League, InboxPriority.High,
					$"Week {state.CurrentWeek} game ready",
					"Your matchup is ready to simulate.",
					true, humanTeam, navigateTab: "schedule" );
			}

			if ( state.Phase == LeaguePhase.RegularSeason && state.CurrentWeek == 10 )
			{
				Add( state, InboxCategory.Trade, InboxPriority.High,
					"Trade deadline week",
					"Last chance to swing a deal this season.",
					true, humanTeam, navigateTab: "trades" );
			}

			CheckExpiringContracts( state, team, humanTeam );
		}

		CheckMoraleIssues( state );
		CheckInjuryReports( state );
	}

	void CheckExpiringContracts( LeagueState state, Domain.Teams.TeamState team, TeamId humanTeam )
	{
		if ( state.Phase is not (LeaguePhase.Preseason or LeaguePhase.RegularSeason or LeaguePhase.Offseason) )
			return;

		var expiring = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired && p.Contract.YearsRemaining <= 1 )
			.OrderByDescending( p => p.Ratings.Overall )
			.Take( 2 )
			.ToList();

		foreach ( var player in expiring )
		{
			Add( state, InboxCategory.Contract, InboxPriority.Normal,
				$"Contract expiring: {player.Identity.FullName}",
				$"{player.Identity.FullName} has {player.Contract.YearsRemaining} year(s) left — extend or plan ahead.",
				false, humanTeam, player.Id, navigateTab: "team" );
		}
	}

	void CheckMoraleIssues( LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		if ( humanTeam.IsEmpty )
			return;

		foreach ( var player in state.Players.Values.Where( p => p.TeamId.Value == humanTeam.Value && !p.IsRetired ) )
		{
			if ( player.Morale.Morale > 35 && !player.Morale.TradeRequested )
				continue;

			var subject = player.Morale.TradeRequested
				? $"{player.Identity.FullName} requests a trade"
				: $"{player.Identity.FullName} is unhappy";

			Add( state, InboxCategory.Roster, InboxPriority.High, subject,
				"Low morale hurts development and chemistry. Talk to the player or consider a trade.",
				true, humanTeam, player.Id, navigateTab: "team" );
		}
	}

	void CheckInjuryReports( LeagueState state )
	{
		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		foreach ( var player in state.Players.Values.Where( p => p.Injury.Severity != InjurySeverity.None ) )
		{
			if ( !humanTeam.IsEmpty && player.TeamId.Value != humanTeam.Value )
				continue;

			Add( state, InboxCategory.Injury, InboxPriority.Normal,
				$"Injury report: {player.Identity.FullName}",
				$"{player.Injury.Description} — estimated {player.Injury.WeeksRemaining} week(s) out.",
				false, player.TeamId, player.Id, navigateTab: "team" );
		}
	}

	public void AddDraftInboxIfNeeded( LeagueState state )
	{
		if ( !state.Draft.IsActive )
			return;

		var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
		if ( current == null || current.IsComplete )
			return;

		if ( !GmAssignmentHelper.IsHumanTeam( state, current.TeamId ) )
			return;

		Add( state, InboxCategory.Draft, InboxPriority.Urgent, "You are on the clock",
			$"Round {current.Round}, pick {current.OverallPick}. Select a prospect in the Draft room.",
			true, current.TeamId, navigateTab: "draft" );
	}

	public void Add(
		LeagueState state,
		InboxCategory category,
		InboxPriority priority,
		string subject,
		string body,
		bool requiresAction,
		TeamId teamId = default,
		PlayerId playerId = default,
		string navigateTab = "" )
	{
		if ( state.Inbox.Any( m => !m.IsResolved && m.Subject == subject && m.Season == state.CurrentSeason && m.Week == state.CurrentWeek ) )
			return;

		state.Inbox.Insert( 0, new InboxMessage
		{
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			Category = category,
			Priority = priority,
			Subject = subject,
			Body = body,
			RequiresAction = requiresAction,
			TeamId = teamId,
			PlayerId = playerId,
			NavigateTab = navigateTab,
			CreatedUtc = _context.Clock.UtcNow
		} );

		if ( state.Inbox.Count > 100 )
			state.Inbox.RemoveRange( 100, state.Inbox.Count - 100 );

		state.BumpRevision( "inbox" );
	}

	public static void MarkRead( LeagueState state, Guid messageId )
	{
		var msg = state.Inbox.FirstOrDefault( m => m.Id == messageId );
		if ( msg == null || msg.IsRead )
			return;

		msg.IsRead = true;
		state.BumpRevision( "inbox_read" );
	}

	public static bool TryResolve( LeagueState state, Guid messageId, out string error )
	{
		error = "";
		var msg = state.Inbox.FirstOrDefault( m => m.Id == messageId );
		if ( msg == null )
		{
			error = "Message not found.";
			return false;
		}

		if ( !msg.RequiresAction )
		{
			Resolve( state, messageId );
			return true;
		}

		if ( msg.Category == InboxCategory.Draft && state.Phase == LeaguePhase.Draft && state.Draft.IsActive )
		{
			var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
			if ( current != null && !current.IsComplete && GmAssignmentHelper.IsHumanTeam( state, current.TeamId ) )
			{
				error = "Make your draft pick before resolving this message.";
				return false;
			}
		}

		if ( msg.Category == InboxCategory.Contract && state.Phase == LeaguePhase.FreeAgency && state.FreeAgency.IsOpen )
		{
			error = "Submit a free agent offer or wait until free agency closes.";
			return false;
		}

		if ( msg.Category == InboxCategory.Roster && !msg.PlayerId.IsEmpty
			&& state.Players.TryGetValue( msg.PlayerId, out var player )
			&& ( player.Morale.TradeRequested || player.Morale.Morale <= 35 ) )
		{
			error = "Address the player's morale or trade request first.";
			return false;
		}

		if ( msg.Subject == "Set this week's game plan" && !msg.TeamId.IsEmpty
			&& state.Teams.TryGetValue( msg.TeamId, out var planTeam )
			&& planTeam.WeeklyGamePlan == WeeklyGamePlan.None )
		{
			error = "Set your weekly game plan on the Team tab first.";
			return false;
		}

		if ( msg.Subject == "You've been fired" )
		{
			error = "Your GM career with this franchise is over. Return to the main menu and start a new dynasty.";
			return false;
		}

		if ( msg.Subject == "Depth chart incomplete" && !msg.TeamId.IsEmpty
			&& state.Teams.TryGetValue( msg.TeamId, out var team )
			&& TeamNeedsDepthChart( team ) )
		{
			error = "Fill every starter slot on your depth chart first.";
			return false;
		}

		error = "Complete the required action first — use Take Action instead of Mark Resolved.";
		return false;
	}

	public static void Resolve( LeagueState state, Guid messageId )
	{
		var msg = state.Inbox.FirstOrDefault( m => m.Id == messageId );
		if ( msg == null )
			return;

		msg.IsResolved = true;
		msg.IsRead = true;
		state.BumpRevision( "inbox_resolve" );
	}

	public static void PurgeStaleMessages( LeagueState state )
	{
		if ( state == null )
			return;

		var changed = false;

		foreach ( var msg in state.Inbox.Where( m => !m.IsResolved ).ToList() )
		{
			if ( msg.RequiresAction )
			{
				if ( ActionStillRequired( state, msg ) )
					continue;

				msg.IsResolved = true;
				msg.IsRead = true;
				changed = true;
				continue;
			}

			if ( !msg.IsRead )
				continue;

			if ( msg.Season < state.CurrentSeason
				|| ( msg.Season == state.CurrentSeason && msg.Week < state.CurrentWeek ) )
			{
				msg.IsResolved = true;
				changed = true;
			}
		}

		if ( changed )
			state.BumpRevision( "inbox_purge" );
	}

	static bool ActionStillRequired( LeagueState state, InboxMessage msg )
	{
		if ( msg.Category == InboxCategory.Draft )
		{
			if ( state.Phase != LeaguePhase.Draft || !state.Draft.IsActive )
				return false;

			var current = state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );
			return current != null && !current.IsComplete && GmAssignmentHelper.IsHumanTeam( state, current.TeamId );
		}

		if ( msg.Category == InboxCategory.Contract )
			return state.Phase == LeaguePhase.FreeAgency && state.FreeAgency.IsOpen;

		if ( msg.Subject == "Depth chart incomplete" && !msg.TeamId.IsEmpty
			&& state.Teams.TryGetValue( msg.TeamId, out var team ) )
			return TeamNeedsDepthChart( team );

		if ( !msg.PlayerId.IsEmpty && state.Players.TryGetValue( msg.PlayerId, out var player ) )
			return player.Morale.TradeRequested || player.Morale.Morale <= 35;

		if ( msg.Subject.Contains( "game ready" ) && !msg.TeamId.IsEmpty )
			return HumanHasUnplayedGame( state, msg.TeamId );

		return true;
	}

	static bool TeamNeedsDepthChart( Domain.Teams.TeamState team )
	{
		var offense = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defense = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		var special = FormationLayoutRegistry.GetSpecialTeams();

		foreach ( var slot in offense.Slots.Concat( defense.Slots ).Concat( special.Slots ) )
		{
			if ( slot.IsOptional )
				continue;

			if ( DepthChartData.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty )
				return true;
		}

		return false;
	}

	static bool HumanHasUnplayedGame( LeagueState state, TeamId humanTeamId )
		=> state.Schedule.Games.Any( g =>
			g.Season == state.CurrentSeason
			&& g.Week == state.CurrentWeek
			&& !g.IsComplete
			&& ( g.HomeTeamId.Value == humanTeamId.Value || g.AwayTeamId.Value == humanTeamId.Value ) );
}
