using Dynasty.Core.Identifiers;
using Dynasty.Domain.League;
using Dynasty.Domain.Schedule;
using Dynasty.Systems.Simulation;
using Dynasty.Domain.Simulation;

namespace Dynasty.UI.ViewModels;

public sealed class GameViewerViewModel
{
	public GameId GameId { get; init; }
	public int Season { get; init; }
	public int Week { get; init; }
	public string Away { get; init; } = "";
	public string Home { get; init; } = "";
	public string AwayFull { get; init; } = "";
	public string HomeFull { get; init; } = "";
	public bool IsComplete { get; init; }
	public int AwayScore { get; init; }
	public int HomeScore { get; init; }
	public bool IsUserGame { get; init; }
	public bool AwayWon { get; init; }
	public bool HomeWon { get; init; }
	public bool IsTie { get; init; }
	public string WinnerLabel { get; init; } = "";
	public int ScoringPlayCount { get; init; }
	public int TotalPlays { get; init; }
	public IReadOnlyList<PlayLogRow> PlayLog { get; init; } = Array.Empty<PlayLogRow>();
	public IReadOnlyList<HighlightClip> Highlights { get; init; } = Array.Empty<HighlightClip>();
	public bool AutoPlayReplay { get; init; }

	public static GameViewerViewModel From( LeagueState state, GameId gameId, TeamId userTeamId, bool autoPlayReplay = false )
	{
		if ( state == null )
			return null;

		var game = state.Schedule.Games.FirstOrDefault( g => g.Id.Value == gameId.Value );
		if ( game == null )
			return null;

		var home = state.Teams[game.HomeTeamId];
		var away = state.Teams[game.AwayTeamId];
		var isUser = !userTeamId.IsEmpty
			&& ( game.HomeTeamId.Value == userTeamId.Value || game.AwayTeamId.Value == userTeamId.Value );

		IReadOnlyList<PlayLogRow> playLog = Array.Empty<PlayLogRow>();
		if ( game.IsComplete && game.Result?.SimulationEvents != null )
			playLog = game.Result.SimulationEvents.Select( PlayLogRow.From ).ToList();

		var awayScore = game.IsComplete ? game.Result.AwayScore : 0;
		var homeScore = game.IsComplete ? game.Result.HomeScore : 0;
		var awayWon = game.IsComplete && awayScore > homeScore;
		var homeWon = game.IsComplete && homeScore > awayScore;
		var isTie = game.IsComplete && awayScore == homeScore;
		var winnerLabel = isTie ? "Final — Tie" : awayWon ? $"{away.Identity.Abbreviation} wins" : homeWon ? $"{home.Identity.Abbreviation} wins" : "";

		return new GameViewerViewModel
		{
			GameId = game.Id,
			Season = game.Season,
			Week = game.Week,
			Away = away.Identity.Abbreviation,
			Home = home.Identity.Abbreviation,
			AwayFull = $"{away.Identity.City} {away.Identity.Name}",
			HomeFull = $"{home.Identity.City} {home.Identity.Name}",
			IsComplete = game.IsComplete,
			AwayScore = awayScore,
			HomeScore = homeScore,
			IsUserGame = isUser,
			AwayWon = awayWon,
			HomeWon = homeWon,
			IsTie = isTie,
			WinnerLabel = winnerLabel,
			ScoringPlayCount = playLog.Count( p => p.IsScoring ),
			TotalPlays = playLog.Count,
			PlayLog = playLog,
			Highlights = game.IsComplete && game.Result?.SimulationEvents != null
				? HighlightReelBuilder.Build( game.Result.SimulationEvents )
				: Array.Empty<HighlightClip>(),
			AutoPlayReplay = autoPlayReplay
		};
	}
}

public sealed class PlayLogRow
{
	public string Quarter { get; init; } = "";
	public string Clock { get; init; } = "";
	public string Description { get; init; } = "";
	public string Score { get; init; } = "";
	public bool IsScoring { get; init; }

	public static PlayLogRow From( SimEventRecord e )
	{
		var mins = e.ClockSeconds / 60;
		var secs = e.ClockSeconds % 60;
		var isScoring = e.Type is Core.Enums.SimEventType.Score or Core.Enums.SimEventType.FieldGoalAttempt;

		return new PlayLogRow
		{
			Quarter = $"Q{e.Quarter}",
			Clock = $"{mins}:{secs:D2}",
			Description = e.Description,
			Score = $"{e.AwayScore} - {e.HomeScore}",
			IsScoring = isScoring
		};
	}
}
