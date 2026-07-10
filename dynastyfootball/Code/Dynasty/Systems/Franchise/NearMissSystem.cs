using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Near-miss inbox alerts — tease milestones the player is one step away from.
/// </summary>
public sealed class NearMissSystem : ILeagueSystem
{
	public string SystemId => "near_miss";

	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state )
	{
		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		if ( state.Phase is not (LeaguePhase.RegularSeason or LeaguePhase.Playoffs) )
			return;

		var progress = FtueHelper.EnsureProgress( state );

		if ( team.Record.Wins == 2 && team.Record.Losses == 0 && state.CurrentWeek == 3 )
			TryAlert( state, progress, human, "near_undefeated_3",
				"One win from 3-0",
				"An undefeated start is within reach. Win this week and the league takes notice." );

		if ( team.Record.Wins == 5 && state.CurrentWeek >= 10 )
			TryAlert( state, progress, human, "near_six_wins",
				"One win from six",
				"Six wins puts you in the playoff picture. Don't let up now." );

		var security = progress.OwnerJobSecurity;
		if ( security is > 20 and <= 30 )
			TryAlert( state, progress, human, "near_hot_seat",
				"One win stabilizes your job",
				"Owner patience is thin. A victory this week buys breathing room." );
	}

	public void OnHumanGameResult( LeagueState state, ScheduledGame game, bool won )
	{
		if ( won )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty || !state.Teams.TryGetValue( human, out var team ) )
			return;

		if ( state.Phase == LeaguePhase.Preseason )
			return;

		var progress = FtueHelper.EnsureProgress( state );
		var gamesPlayed = team.Record.Wins + team.Record.Losses + team.Record.Ties;

		if ( gamesPlayed == 0 && !won )
			TryAlert( state, progress, human, "bounce_back_week1",
				"Bounce-back opportunity",
				"Week 1 didn't go your way. Respond this week and the season is still yours." );
	}

	void TryAlert( LeagueState state, Domain.Franchise.FranchiseProgressState progress, Core.Identifiers.TeamId human, string key, string subject, string body )
	{
		progress.NearMissAlertsSent ??= new HashSet<string>();
		if ( !progress.NearMissAlertsSent.Add( key ) )
			return;

		_inbox?.Add( state, InboxCategory.League, InboxPriority.Normal,
			subject, body, false, human, navigateTab: "schedule" );
		state.BumpRevision( "near_miss" );
	}
}
