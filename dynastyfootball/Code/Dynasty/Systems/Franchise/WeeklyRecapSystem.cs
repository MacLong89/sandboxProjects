using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Stats;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;

namespace Dynasty.Systems.Franchise;

/// <summary>
/// Delivers a short post-game recap inbox — makes each week feel like an event.
/// </summary>
public sealed class WeeklyRecapSystem : ILeagueSystem
{
	public string SystemId => "weekly_recap";

	private InboxSystem _inbox;

	public void Register( LeagueSystemContext context ) { }

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void OnLeagueCreated( LeagueState state ) { }
	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }
	public void OnWeekAdvanced( LeagueState state ) { }
	public void OnSeasonEnded( LeagueState state ) { }

	public void OnHumanGameCompleted( LeagueState state, ScheduledGame game )
	{
		if ( game.Result == null )
			return;

		var human = GmAssignmentHelper.GetHumanTeamId( state );
		if ( human.IsEmpty )
			return;

		var isHome = game.HomeTeamId.Value == human.Value;
		if ( !isHome && game.AwayTeamId.Value != human.Value )
			return;

		var includePreseason = state.Phase == LeaguePhase.Preseason && FtueHelper.IsFtueActive( state );
		if ( state.Phase == LeaguePhase.Preseason && !includePreseason )
			return;

		var teamScore = isHome ? game.Result.HomeScore : game.Result.AwayScore;
		var oppScore = isHome ? game.Result.AwayScore : game.Result.HomeScore;
		var opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
		state.Teams.TryGetValue( opponentId, out var opponent );

		var won = teamScore > oppScore;
		var lost = teamScore < oppScore;
		var resultWord = won ? "WIN" : lost ? "LOSS" : "TIE";

		var starLine = FindStarPerformers( state, game, human );
		var record = state.Teams[human].Record;
		var recordLine = $"{record.Wins}-{record.Losses}";

		var priority = won ? InboxPriority.Normal : lost ? InboxPriority.High : InboxPriority.Normal;
		var subject = $"{resultWord} — Week {game.Week}: {teamScore}-{oppScore} vs {opponent?.Identity.Abbreviation ?? "???"}";

		_inbox?.Add( state, InboxCategory.League, priority,
			subject,
			$"Record now {recordLine}. {starLine}",
			false, human, navigateTab: "schedule" );
	}

	static string FindStarPerformers( LeagueState state, ScheduledGame game, Core.Identifiers.TeamId humanTeamId )
	{
		if ( game.Result?.PlayerBoxScores == null || game.Result.PlayerBoxScores.Count == 0 )
			return "Review the box score on Schedule.";

		var best = game.Result.PlayerBoxScores
			.Where( kv => state.Players.TryGetValue( kv.Key, out var p ) && p.TeamId.Value == humanTeamId.Value )
			.Select( kv =>
			{
				var stats = kv.Value;
				var score = stats.GetValueOrDefault( PlayerStatKeys.PassTd ) * 4
					+ stats.GetValueOrDefault( PlayerStatKeys.RushTd ) * 4
					+ stats.GetValueOrDefault( PlayerStatKeys.RecTd ) * 4
					+ stats.GetValueOrDefault( PlayerStatKeys.PassYds ) / 25
					+ stats.GetValueOrDefault( PlayerStatKeys.RushYds ) / 10
					+ stats.GetValueOrDefault( PlayerStatKeys.RecYds ) / 10
					+ stats.GetValueOrDefault( PlayerStatKeys.Sacks ) * 3
					+ stats.GetValueOrDefault( PlayerStatKeys.Tackles ) / 2;
				return (PlayerId: kv.Key, Score: score, Stats: stats);
			} )
			.OrderByDescending( x => x.Score )
			.FirstOrDefault();

		if ( best.PlayerId.IsEmpty || !state.Players.TryGetValue( best.PlayerId, out var player ) )
			return "Team effort across the board.";

		if ( best.Stats.GetValueOrDefault( PlayerStatKeys.PassYds ) >= 200 )
			return $"Star: {player.Identity.FullName} — {best.Stats[PlayerStatKeys.PassYds]} pass yards.";

		if ( best.Stats.GetValueOrDefault( PlayerStatKeys.RushYds ) >= 80 )
			return $"Star: {player.Identity.FullName} — {best.Stats[PlayerStatKeys.RushYds]} rush yards.";

		if ( best.Stats.GetValueOrDefault( PlayerStatKeys.RecYds ) >= 80 )
			return $"Star: {player.Identity.FullName} — {best.Stats[PlayerStatKeys.RecYds]} rec yards.";

		if ( best.Stats.GetValueOrDefault( PlayerStatKeys.Sacks ) >= 1 )
			return $"Star: {player.Identity.FullName} — {best.Stats[PlayerStatKeys.Sacks]} sack(s).";

		return $"Standout: {player.Identity.FullName}.";
	}
}
