using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Simulation;

namespace Dynasty.Domain.Schedule;

public sealed class ScheduleState
{
	public List<ScheduledGame> Games { get; set; } = new();
	public List<PlayoffBracketSlot> PlayoffBracket { get; set; } = new();
}

public sealed class ScheduledGame
{
	public GameId Id { get; set; }
	public int Season { get; set; }
	public int Week { get; set; }
	public TeamId HomeTeamId { get; set; }
	public TeamId AwayTeamId { get; set; }
	public bool IsPlayoffGame { get; set; }
	public PlayoffRound PlayoffRound { get; set; } = PlayoffRound.None;
	public bool IsComplete { get; set; }
	public GameResult Result { get; set; }
}

public sealed class GameResult
{
	public int HomeScore { get; set; }
	public int AwayScore { get; set; }
	public List<SimEventRecord> SimulationEvents { get; set; } = new();
	public Dictionary<PlayerId, Dictionary<string, int>> PlayerBoxScores { get; set; } = new();
	public DateTime SimulatedUtc { get; set; }
}

public sealed class PlayoffBracketSlot
{
	public GameId GameId { get; set; }
	public PlayoffRound Round { get; set; }
	public TeamId HomeTeamId { get; set; }
	public TeamId AwayTeamId { get; set; }
	public TeamId WinnerTeamId { get; set; }
	public List<GameId> SourceGameIds { get; set; } = new();
}
