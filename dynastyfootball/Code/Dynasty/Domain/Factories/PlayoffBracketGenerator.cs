using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Core.Interfaces;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Domain.Teams;

namespace Dynasty.Domain.Factories;

/// <summary>
/// Seeds top teams and builds a single-elimination bracket (8 teams, 3 rounds over 3 active weeks).
/// </summary>
public static class PlayoffBracketGenerator
{
	const int PlayoffTeamCount = 8;

	public static void EnsurePlayoffBracket( LeagueState league, ILeagueRandom random )
	{
		if ( league == null || league.Phase != LeaguePhase.Playoffs )
			return;

		var existing = league.Schedule.Games
			.Any( g => g.Season == league.CurrentSeason && g.IsPlayoffGame );

		if ( existing )
			return;

		SeedTeams( league );
		BuildBracket( league, random );
	}

	static void SeedTeams( LeagueState league )
	{
		var ranked = league.Teams.Values
			.OrderByDescending( t => t.Record.Wins )
			.ThenByDescending( t => t.Record.Ties )
			.ThenBy( t => t.Record.Losses )
			.ThenByDescending( t => t.Record.PointsFor - t.Record.PointsAgainst )
			.Take( PlayoffTeamCount )
			.ToList();

		for ( var i = 0; i < ranked.Count; i++ )
		{
			ranked[i].Record.ConferenceRank = i + 1;
			ranked[i].Record.PlayoffStatus = PlayoffRound.WildCard;
		}

		foreach ( var team in league.Teams.Values.Where( t => t.Record.ConferenceRank == 0 ) )
			team.Record.PlayoffStatus = PlayoffRound.None;
	}

	static void BuildBracket( LeagueState league, ILeagueRandom random )
	{
		league.Schedule.PlayoffBracket.Clear();

		var seeds = league.Teams.Values
			.Where( t => t.Record.ConferenceRank > 0 && t.Record.ConferenceRank <= PlayoffTeamCount )
			.OrderBy( t => t.Record.ConferenceRank )
			.ToList();

		if ( seeds.Count < PlayoffTeamCount )
			return;

		// Week 1 — quarterfinals (4 games)
		var qfWinners = new PlayoffBracketSlot[4];
		for ( var i = 0; i < 4; i++ )
		{
			var high = seeds[i];
			var low = seeds[7 - i];
			qfWinners[i] = AddGame( league, 1, PlayoffRound.WildCard, high.Id, low.Id );
		}

		// Week 2 — semifinals (TBD until QF complete)
		var sfWinners = new PlayoffBracketSlot[2];
		sfWinners[0] = AddGame( league, 2, PlayoffRound.Divisional, TeamId.Empty, TeamId.Empty );
		sfWinners[1] = AddGame( league, 2, PlayoffRound.Divisional, TeamId.Empty, TeamId.Empty );
		sfWinners[0].SourceGameIds = new List<GameId> { qfWinners[0].GameId, qfWinners[1].GameId };
		sfWinners[1].SourceGameIds = new List<GameId> { qfWinners[2].GameId, qfWinners[3].GameId };

		// Week 3 — championship
		var championship = AddGame( league, 3, PlayoffRound.SuperBowl, TeamId.Empty, TeamId.Empty );
		championship.SourceGameIds = new List<GameId> { sfWinners[0].GameId, sfWinners[1].GameId };

		league.BumpRevision( "playoff_bracket_created" );
	}

	static PlayoffBracketSlot AddGame(
		LeagueState league,
		int week,
		PlayoffRound round,
		TeamId home,
		TeamId away )
	{
		var game = new ScheduledGame
		{
			Id = GameId.New(),
			Season = league.CurrentSeason,
			Week = week,
			HomeTeamId = home,
			AwayTeamId = away,
			IsPlayoffGame = true,
			PlayoffRound = round
		};

		league.Schedule.Games.Add( game );

		var slot = new PlayoffBracketSlot
		{
			GameId = game.Id,
			Round = round,
			HomeTeamId = home,
			AwayTeamId = away
		};

		league.Schedule.PlayoffBracket.Add( slot );
		return slot;
	}

	public static void AdvanceBracket( LeagueState league, ScheduledGame completedGame )
	{
		if ( !completedGame.IsPlayoffGame || completedGame.Result == null )
			return;

		var slot = league.Schedule.PlayoffBracket.FirstOrDefault( s => s.GameId.Value == completedGame.Id.Value );
		if ( slot == null )
			return;

		var hs = completedGame.Result.HomeScore;
		var aws = completedGame.Result.AwayScore;
		var winner = ResolveWinner( league, completedGame, hs, aws );
		slot.WinnerTeamId = winner;

		if ( league.Teams.TryGetValue( winner, out var winnerTeam ) )
		{
			winnerTeam.Record.PlayoffStatus = completedGame.PlayoffRound switch
			{
				PlayoffRound.WildCard => PlayoffRound.Divisional,
				PlayoffRound.Divisional => PlayoffRound.Conference,
				PlayoffRound.SuperBowl => PlayoffRound.SuperBowl,
				_ => winnerTeam.Record.PlayoffStatus
			};
		}

		if ( completedGame.PlayoffRound != PlayoffRound.SuperBowl )
			TryPopulateNextRound( league, slot );

		league.BumpRevision( "playoff_bracket_advanced" );
	}

	public static bool IsChampionshipGame( ScheduledGame game )
		=> game.IsPlayoffGame && game.PlayoffRound == PlayoffRound.SuperBowl;

	public static TeamId GetChampionshipLoser( ScheduledGame game )
	{
		if ( game.Result == null )
			return TeamId.Empty;

		var winner = game.Result.HomeScore > game.Result.AwayScore
			? game.HomeTeamId
			: game.Result.AwayScore > game.Result.HomeScore
				? game.AwayTeamId
				: TeamId.Empty;

		if ( winner.IsEmpty )
			return TeamId.Empty;

		return winner.Value == game.HomeTeamId.Value ? game.AwayTeamId : game.HomeTeamId;
	}

	static TeamId ResolveWinner( LeagueState league, ScheduledGame game, int homeScore, int awayScore )
	{
		if ( homeScore > awayScore )
			return game.HomeTeamId;

		if ( awayScore > homeScore )
			return game.AwayTeamId;

		if ( !league.Teams.TryGetValue( game.HomeTeamId, out var home )
			|| !league.Teams.TryGetValue( game.AwayTeamId, out var away ) )
			return game.HomeTeamId;

		var homeDiff = home.Record.PointsFor - home.Record.PointsAgainst;
		var awayDiff = away.Record.PointsFor - away.Record.PointsAgainst;

		if ( homeDiff != awayDiff )
			return homeDiff > awayDiff ? game.HomeTeamId : game.AwayTeamId;

		return home.Record.Wins >= away.Record.Wins ? game.HomeTeamId : game.AwayTeamId;
	}

	static void TryPopulateNextRound( LeagueState league, PlayoffBracketSlot completedSlot )
	{
		foreach ( var next in league.Schedule.PlayoffBracket.Where( s => s.SourceGameIds?.Count > 0 ) )
		{
			if ( !next.SourceGameIds.Contains( completedSlot.GameId ) )
				continue;

			if ( !AllSourceWinnersReady( league, next ) )
				continue;

			var winners = next.SourceGameIds
				.Select( id => league.Schedule.PlayoffBracket.FirstOrDefault( s => s.GameId.Value == id.Value ) )
				.Where( s => s != null && !s.WinnerTeamId.IsEmpty )
				.Select( s => s.WinnerTeamId )
				.ToList();

			if ( winners.Count != next.SourceGameIds.Count )
				continue;

			if ( !next.HomeTeamId.IsEmpty && !next.AwayTeamId.IsEmpty )
				continue;

			next.HomeTeamId = winners[0];
			next.AwayTeamId = winners[1];

			var game = league.Schedule.Games.FirstOrDefault( g => g.Id.Value == next.GameId.Value );
			if ( game == null )
				continue;

			game.HomeTeamId = next.HomeTeamId;
			game.AwayTeamId = next.AwayTeamId;
		}

		league.BumpRevision( "playoff_bracket_advanced" );
	}

	static bool AllSourceWinnersReady( LeagueState league, PlayoffBracketSlot slot )
		=> slot.SourceGameIds.All( id =>
		{
			var source = league.Schedule.PlayoffBracket.FirstOrDefault( s => s.GameId.Value == id.Value );
			return source != null && !source.WinnerTeamId.IsEmpty;
		} );
}
