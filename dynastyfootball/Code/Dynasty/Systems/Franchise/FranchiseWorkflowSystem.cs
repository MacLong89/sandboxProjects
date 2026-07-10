using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.Calendar;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.League;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Roster;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Drives calendar sync, event queue, and "what to do next" guidance.
/// </summary>
public sealed class FranchiseWorkflowSystem : ILeagueSystem
{
	public string SystemId => "franchise_workflow";

	private LeagueSystemContext _context;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void OnLeagueCreated( LeagueState state )
	{
		LeagueCalendar.Sync( state );
		RebuildWeeklyQueue( state );
		UpdateSuggestedAction( state );
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		LeagueCalendar.Sync( state );
		RebuildWeeklyQueue( state );
		UpdateSuggestedAction( state );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		LeagueCalendar.Sync( state );
		RebuildWeeklyQueue( state );
		UpdateSuggestedAction( state );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	public static bool HasBlockingItems( LeagueState state )
	{
		if ( state == null )
			return false;

		if ( FranchiseRetentionSystem.IsHumanFired( state ) )
			return true;

		if ( state.Phase == LeaguePhase.Preseason )
		{
			var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
			if ( !humanTeam.IsEmpty && RosterLimits.IsOverLimit( state, humanTeam ) )
				return true;
		}

		return state.EventQueue.Any( e => !e.IsComplete && e.IsBlocking )
			|| state.Inbox.Any( m => !m.IsResolved && m.RequiresAction )
			|| FranchiseWorkflowHelper.IsHumanOnDraftClock( state );
	}

	public static string GetBlockingReason( LeagueState state )
	{
		if ( state == null )
			return "No league loaded.";

		if ( FranchiseRetentionSystem.IsHumanFired( state ) )
			return "You've been fired. Start a new franchise from the main menu.";

		var humanTeam = GmAssignmentHelper.GetHumanTeamId( state );
		if ( state.Phase == LeaguePhase.Preseason && !humanTeam.IsEmpty && RosterLimits.IsOverLimit( state, humanTeam ) )
		{
			var count = state.Teams[humanTeam].RosterPlayerIds.Count;
			return $"Roster over limit ({count}/{state.Settings.RosterSize}) — release players in the Team tab.";
		}

		var inbox = state.Inbox
			.Where( m => !m.IsResolved && m.RequiresAction )
			.OrderByDescending( m => m.Priority )
			.FirstOrDefault();

		if ( inbox != null )
			return $"Resolve inbox: \"{inbox.Subject}\" — use Take Action, not Mark Resolved.";

		var evt = state.EventQueue
			.Where( e => !e.IsComplete && e.IsBlocking )
			.OrderBy( e => e.SortOrder )
			.FirstOrDefault();

		if ( evt != null )
			return $"Complete blocking event: \"{evt.Title}\".";

		if ( FranchiseWorkflowHelper.IsHumanOnDraftClock( state ) )
			return "You're on the clock — make your draft pick in the Draft tab.";

		return "Resolve required inbox items before advancing.";
	}

	public void RebuildWeeklyQueue( LeagueState state )
	{
		PurgeStaleState( state );

		var order = state.EventQueue.Count;

		if ( state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Playoffs or LeaguePhase.Preseason )
			Enqueue( state, FranchiseEventType.GameDay, "Game day", "Simulate this week's matchups.", false, order++ );

		if ( state.Phase == LeaguePhase.RegularSeason && state.CurrentWeek == 10 )
			Enqueue( state, FranchiseEventType.TradeDeadline, "Trade deadline", "Final call on deadline deals.", true, order++ );
	}

	public void PurgeStaleState( LeagueState state )
	{
		PurgeStaleEvents( state );
		InboxSystem.PurgeStaleMessages( state );
		state.EventQueue.RemoveAll( e => e.IsComplete );
	}

	static void PurgeStaleEvents( LeagueState state )
	{
		foreach ( var evt in state.EventQueue.Where( e => !e.IsComplete ).ToList() )
		{
			if ( evt.Season < state.CurrentSeason || evt.Week < state.CurrentWeek )
				evt.IsComplete = true;

			if ( evt.Type == FranchiseEventType.TradeDeadline
				&& ( state.Phase != LeaguePhase.RegularSeason || state.CurrentWeek > 10 ) )
				evt.IsComplete = true;

			if ( evt.Type == FranchiseEventType.GameDay
				&& state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs or LeaguePhase.Preseason) )
				evt.IsComplete = true;
		}
	}

	void Enqueue( LeagueState state, FranchiseEventType type, string title, string description, bool blocking, int order )
	{
		if ( state.EventQueue.Any( e => e.Type == type && !e.IsComplete && e.Week == state.CurrentWeek ) )
			return;

		state.EventQueue.Add( new FranchiseQueuedEvent
		{
			Type = type,
			Season = state.CurrentSeason,
			Week = state.CurrentWeek,
			Title = title,
			Description = description,
			IsBlocking = blocking,
			RequiresAction = blocking,
			SortOrder = order
		} );
	}

	public FranchiseQueuedEvent GetNextEvent( LeagueState state )
		=> state.EventQueue.Where( e => !e.IsComplete ).OrderBy( e => e.SortOrder ).FirstOrDefault();

	public void CompleteNextEvent( LeagueState state )
	{
		var next = GetNextEvent( state );
		if ( next != null )
			next.IsComplete = true;
	}

	public void UpdateSuggestedAction( LeagueState state )
	{
		var urgent = state.Inbox.FirstOrDefault( m => !m.IsResolved && m.RequiresAction );
		if ( urgent != null )
		{
			state.NextSuggestedAction = urgent.Subject;
			return;
		}

		var next = GetNextEvent( state );
		if ( next != null )
		{
			state.NextSuggestedAction = next.Title;
			return;
		}

		state.NextSuggestedAction = state.Phase switch
		{
			LeaguePhase.Preseason => "Set your depth chart and trim the roster.",
			LeaguePhase.RegularSeason => "Advance week to play games and chase the playoffs.",
			LeaguePhase.Playoffs => "Win the championship.",
			LeaguePhase.Offseason => $"Offseason: {state.OffseasonSubPhase}",
			LeaguePhase.FreeAgency => "Sign free agents and manage the cap.",
			LeaguePhase.Draft => "Make your draft picks.",
			_ => "Advance time to continue your dynasty."
		};
	}
}
