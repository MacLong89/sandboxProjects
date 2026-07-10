using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Formation;
using Dynasty.Systems.Franchise;

namespace Dynasty.UI.ViewModels;

public enum TodoActionType
{
	Navigate,
	AdvanceDay,
	AdvanceWeek,
	AdvanceNextEvent
}

public sealed class ActionTodoViewModel
{
	public IReadOnlyList<ActionTodoItem> Items { get; init; } = Array.Empty<ActionTodoItem>();
	public int UrgentCount { get; init; }

	public static ActionTodoViewModel From( LeagueState state, TeamId humanTeamId )
	{
		if ( state == null )
			return new ActionTodoViewModel();

		var items = new List<ActionTodoItem>();
		var seen = new HashSet<string>();

		void Add( ActionTodoItem item )
		{
			if ( !seen.Add( item.Id ) )
				return;

			items.Add( item );
		}

		foreach ( var msg in state.Inbox
			.Where( m => !m.IsResolved && m.RequiresAction && InboxActionIsLive( state, m ) )
			.OrderByDescending( m => m.Priority )
			.ThenByDescending( m => m.CreatedUtc )
			.ToList() )
		{
			var tab = !string.IsNullOrEmpty( msg.NavigateTab )
				? msg.NavigateTab
				: InboxNavigateTab( msg.Category );

			Add( new ActionTodoItem
			{
				Id = $"inbox-{msg.Id}",
				Title = msg.Subject,
				Detail = Truncate( msg.Body, 72 ),
				Priority = ToPriority( msg.Priority ),
				IsBlocking = true,
				Action = TodoActionType.Navigate,
				NavigateTab = tab,
				InboxMessageId = msg.Id,
				PlayerId = msg.PlayerId
			} );
		}

		var hasDraftInbox = state.Inbox.Any( m =>
			!m.IsResolved && m.RequiresAction && m.Category == InboxCategory.Draft && InboxActionIsLive( state, m ) );
		if ( !hasDraftInbox && FranchiseWorkflowHelper.IsHumanOnDraftClock( state ) )
		{
			Add( new ActionTodoItem
			{
				Id = "draft-pick",
				Title = "You're on the clock",
				Detail = $"Round {state.Draft.CurrentRound} — select a prospect.",
				Priority = TodoPriority.Urgent,
				IsBlocking = true,
				Action = TodoActionType.Navigate,
				NavigateTab = "draft"
			} );
		}

		var nextEvent = state.EventQueue
			.Where( e => !e.IsComplete && EventIsCurrent( state, e ) )
			.OrderBy( e => e.SortOrder )
			.FirstOrDefault();

		if ( nextEvent != null )
		{
			Add( new ActionTodoItem
			{
				Id = $"event-{nextEvent.Id}",
				Title = nextEvent.Title,
				Detail = nextEvent.Description,
				Priority = nextEvent.IsBlocking ? TodoPriority.High : TodoPriority.Normal,
				IsBlocking = nextEvent.IsBlocking,
				Action = TodoActionType.AdvanceNextEvent,
				NavigateTab = nextEvent.Type == FranchiseEventType.GameDay ? "schedule" : null
			} );
		}

		if ( state.Inbox.Any( m => !m.IsResolved && !m.RequiresAction && !m.IsRead ) )
		{
			Add( new ActionTodoItem
			{
				Id = "inbox-review",
				Title = "Unread inbox messages",
				Detail = "Catch up on league news and staff notes.",
				Priority = TodoPriority.Normal,
				Action = TodoActionType.Navigate,
				NavigateTab = "inbox"
			} );
		}

		AddPhaseGuidance( state, humanTeamId, Add );

		if ( !HasBlockingItems( state ) )
		{
			Add( new ActionTodoItem
			{
				Id = "advance-week",
				Title = "Advance the week",
				Detail = PhaseAdvanceHint( state ),
				Priority = TodoPriority.Low,
				Action = TodoActionType.AdvanceWeek
			} );
		}

		var sorted = items
			.OrderByDescending( i => (int)i.Priority )
			.ThenByDescending( i => i.IsBlocking )
			.Take( 8 )
			.ToList();

		return new ActionTodoViewModel
		{
			Items = sorted,
			UrgentCount = sorted.Count( i => i.Priority == TodoPriority.Urgent )
		};
	}

	static bool HasBlockingItems( LeagueState state )
		=> FranchiseWorkflowSystem.HasBlockingItems( state );

	static bool EventIsCurrent( LeagueState state, FranchiseQueuedEvent evt )
		=> evt.Season == state.CurrentSeason && evt.Week == state.CurrentWeek;

	static string InboxNavigateTab( InboxCategory category ) => category switch
	{
		InboxCategory.Draft => "draft",
		InboxCategory.Contract => "freeagency",
		InboxCategory.Trade => "trades",
		InboxCategory.Coaching => "news",
		InboxCategory.General => "facilities",
		InboxCategory.Roster or InboxCategory.Injury => "team",
		_ => "home"
	};

	static bool InboxActionIsLive( LeagueState state, InboxMessage msg )
	{
		if ( msg.Category == InboxCategory.Draft )
			return FranchiseWorkflowHelper.IsHumanOnDraftClock( state );

		if ( msg.Category == InboxCategory.Contract )
			return state.Phase == LeaguePhase.FreeAgency && state.FreeAgency.IsOpen;

		if ( msg.Subject == "Depth chart incomplete" && !msg.TeamId.IsEmpty
			&& state.Teams.TryGetValue( msg.TeamId, out var team ) )
			return TeamNeedsDepthChart( state, team );

		if ( msg.Subject.Contains( "game ready" ) && !msg.TeamId.IsEmpty )
			return HumanHasUnplayedGame( state, msg.TeamId );

		if ( !msg.PlayerId.IsEmpty && state.Players.TryGetValue( msg.PlayerId, out var player ) )
			return player.Morale.TradeRequested || player.Morale.Morale <= 35;

		return true;
	}

	static void AddPhaseGuidance( LeagueState state, TeamId humanTeamId, Action<ActionTodoItem> add )
	{
		if ( humanTeamId.IsEmpty || !state.Teams.TryGetValue( humanTeamId, out var team ) )
			return;

		switch ( state.Phase )
		{
			case LeaguePhase.Preseason:
				if ( team.RosterPlayerIds.Count > state.Settings.RosterSize )
				{
					add( new ActionTodoItem
					{
						Id = "preseason-roster",
						Title = "Trim your roster",
						Detail = $"Cut to {state.Settings.RosterSize} players before the regular season.",
						Priority = TodoPriority.High,
						Action = TodoActionType.Navigate,
						NavigateTab = "team"
					} );
				}

				if ( TeamNeedsDepthChart( state, team ) )
				{
					add( new ActionTodoItem
					{
						Id = "preseason-depth",
						Title = "Set your depth chart",
						Detail = "Assign starters before preseason games.",
						Priority = TodoPriority.Normal,
						Action = TodoActionType.Navigate,
						NavigateTab = "team"
					} );
				}

				break;

			case LeaguePhase.RegularSeason:
				if ( state.CurrentWeek == 10 && HasPendingTradeDeadline( state ) )
				{
					add( new ActionTodoItem
					{
						Id = "trade-deadline",
						Title = "Trade deadline week",
						Detail = "Last chance to swing a deal this season.",
						Priority = TodoPriority.High,
						Action = TodoActionType.Navigate,
						NavigateTab = "trades"
					} );
				}

				if ( HumanHasUnplayedGame( state, humanTeamId ) )
				{
					add( new ActionTodoItem
					{
						Id = "regular-schedule",
						Title = "Play this week's game",
						Detail = $"Week {state.CurrentWeek} matchup is ready to simulate.",
						Priority = TodoPriority.Normal,
						Action = TodoActionType.Navigate,
						NavigateTab = "schedule"
					} );
				}

				break;

			case LeaguePhase.Playoffs:
				if ( HumanHasUnplayedGame( state, humanTeamId ) )
				{
					add( new ActionTodoItem
					{
						Id = "playoffs",
						Title = "Playoff game ready",
						Detail = "Simulate your playoff matchup.",
						Priority = TodoPriority.High,
						Action = TodoActionType.Navigate,
						NavigateTab = "schedule"
					} );
				}

				break;

			case LeaguePhase.Offseason:
				if ( state.OffseasonSubPhase == OffseasonSubPhase.FacilityUpgrades )
				{
					add( new ActionTodoItem
					{
						Id = "offseason-facilities",
						Title = "Upgrade facilities",
						Detail = "Invest in stadium and training upgrades.",
						Priority = TodoPriority.Normal,
						Action = TodoActionType.Navigate,
						NavigateTab = "facilities"
					} );
				}

				break;

			case LeaguePhase.FreeAgency:
				if ( state.FreeAgency.IsOpen )
				{
					add( new ActionTodoItem
					{
						Id = "free-agency",
						Title = "Sign free agents",
						Detail = "Build your roster before training camp.",
						Priority = TodoPriority.High,
						Action = TodoActionType.Navigate,
						NavigateTab = "freeagency"
					} );
				}

				break;

			case LeaguePhase.Draft when !state.Draft.IsActive:
				add( new ActionTodoItem
				{
					Id = "draft-prep",
					Title = "Prepare for the draft",
					Detail = "Review prospects and team needs.",
					Priority = TodoPriority.Normal,
					Action = TodoActionType.Navigate,
					NavigateTab = "draft"
				} );
				break;
		}

		if ( team.RosterPlayerIds.Count < 45 && state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Preseason )
		{
			add( new ActionTodoItem
			{
				Id = "thin-roster",
				Title = "Roster is thin",
				Detail = $"Only {team.RosterPlayerIds.Count} players — add depth.",
				Priority = TodoPriority.High,
				Action = TodoActionType.Navigate,
				NavigateTab = "team"
			} );
		}
	}

	static bool TeamNeedsDepthChart( LeagueState state, TeamState team )
	{
		var offense = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defense = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );
		var special = FormationLayoutRegistry.GetSpecialTeams();

		foreach ( var slot in offense.Slots.Concat( defense.Slots ).Concat( special.Slots ) )
		{
			if ( slot.IsOptional )
				continue;

			if ( DepthChart.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty )
				return true;
		}

		return false;
	}

	static bool HasPendingTradeDeadline( LeagueState state )
		=> state.EventQueue.Any( e =>
			e.Type == FranchiseEventType.TradeDeadline
			&& !e.IsComplete
			&& e.Season == state.CurrentSeason
			&& e.Week == state.CurrentWeek );

	static bool HumanHasUnplayedGame( LeagueState state, TeamId humanTeamId )
		=> state.Schedule.Games.Any( g =>
			g.Season == state.CurrentSeason
			&& g.Week == state.CurrentWeek
			&& !g.IsComplete
			&& ( g.HomeTeamId.Value == humanTeamId.Value || g.AwayTeamId.Value == humanTeamId.Value ) );

	static string PhaseAdvanceHint( LeagueState state ) => state.Phase switch
	{
		LeaguePhase.Preseason => "Run practice reports and preseason games.",
		LeaguePhase.RegularSeason => "Simulate games and update standings.",
		LeaguePhase.Playoffs => "Advance through the playoff bracket.",
		LeaguePhase.Offseason => "Progress retirements, coaching, and the draft.",
		LeaguePhase.FreeAgency => "Let free agency play out across the league.",
		LeaguePhase.Draft => "Complete the rookie draft.",
		_ => "Keep your dynasty moving forward."
	};

	static TodoPriority ToPriority( InboxPriority priority ) => priority switch
	{
		InboxPriority.Urgent => TodoPriority.Urgent,
		InboxPriority.High => TodoPriority.High,
		InboxPriority.Low => TodoPriority.Low,
		_ => TodoPriority.Normal
	};

	static string Truncate( string text, int max )
	{
		if ( string.IsNullOrEmpty( text ) || text.Length <= max )
			return text ?? "";

		return text[..(max - 1)] + "…";
	}
}

public enum TodoPriority
{
	Low,
	Normal,
	High,
	Urgent
}

public sealed class ActionTodoItem
{
	public string Id { get; init; } = "";
	public string Title { get; init; } = "";
	public string Detail { get; init; } = "";
	public TodoPriority Priority { get; init; }
	public bool IsBlocking { get; init; }
	public TodoActionType Action { get; init; }
	public string NavigateTab { get; init; }
	public Guid? InboxMessageId { get; init; }
	public PlayerId PlayerId { get; init; }
}
