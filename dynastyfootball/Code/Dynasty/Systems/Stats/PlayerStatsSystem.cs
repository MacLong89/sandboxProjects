using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Stats;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Schedule;

namespace Dynasty.Systems.Stats;

public sealed class PlayerStatsSystem : ILeagueSystem
{
	public string SystemId => "player_stats";

	public void Register( LeagueSystemContext context ) { }

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state ) { }

	public void OnWeekAdvanced( LeagueState state ) { }

	public void OnSeasonEnded( LeagueState state )
	{
		ArchiveSeasonStats( state );
		state.BumpRevision( "player_stats_season" );
	}

	public static void ApplyBoxScores( LeagueState state, ScheduledGame game )
	{
		if ( game.Result?.PlayerBoxScores == null || game.Result.PlayerBoxScores.Count == 0 )
			return;

		foreach ( var (playerId, boxStats) in game.Result.PlayerBoxScores )
		{
			if ( !state.Players.TryGetValue( playerId, out var player ) || player.IsRetired )
				continue;

			StatAggregator.Merge( player.Career.SeasonStats, boxStats );
			AppendGameLog( state, player, game, boxStats );
		}
	}

	static void AppendGameLog( LeagueState state, PlayerState player, ScheduledGame game, Dictionary<string, int> boxStats )
	{
		var isHome = player.TeamId.Value == game.HomeTeamId.Value;
		var opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
		var opponent = state.Teams.TryGetValue( opponentId, out var opp ) ? opp.Identity.Abbreviation : "???";
		var teamScore = isHome ? game.Result.HomeScore : game.Result.AwayScore;
		var oppScore = isHome ? game.Result.AwayScore : game.Result.HomeScore;
		var result = teamScore > oppScore ? "W" : teamScore < oppScore ? "L" : "T";

		player.Career.GameLogs.Insert( 0, new PlayerGameStatEntry
		{
			GameId = game.Id,
			Season = game.Season,
			Week = game.Week,
			OpponentAbbreviation = opponent,
			Result = result,
			Stats = new Dictionary<string, int>( boxStats )
		} );

		const int maxLogs = 24;
		if ( player.Career.GameLogs.Count > maxLogs )
			player.Career.GameLogs.RemoveRange( maxLogs, player.Career.GameLogs.Count - maxLogs );
	}

	static void ArchiveSeasonStats( LeagueState state )
	{
		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired ) )
		{
			if ( player.Career.SeasonStats.Count == 0 )
				continue;

			player.Career.SeasonHistory.Add( new PlayerSeasonStatEntry
			{
				Season = state.CurrentSeason,
				Stats = new Dictionary<string, int>( player.Career.SeasonStats )
			} );

			StatAggregator.Merge( player.Career.CareerStats, player.Career.SeasonStats );
			player.Career.SeasonStats.Clear();
		}
	}
}
