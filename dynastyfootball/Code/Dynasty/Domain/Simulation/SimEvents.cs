using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Simulation;

/// <summary>
/// Immutable simulation event stream. Consumed by visualization replay — never drives outcomes.
/// </summary>
public sealed class SimEventRecord
{
	public int Index { get; set; }
	public SimEventType Type { get; set; }
	public int Quarter { get; set; }
	public int ClockSeconds { get; set; }
	public int HomeScore { get; set; }
	public int AwayScore { get; set; }
	public int YardLine { get; set; }
	public int Down { get; set; }
	public int YardsToGo { get; set; }
	public TeamId PossessionTeamId { get; set; }
	public string Description { get; set; } = "";
	public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class GameSimulationInput
{
	public GameId GameId { get; set; }
	public TeamId HomeTeamId { get; set; }
	public TeamId AwayTeamId { get; set; }
	public TeamSimulationProfile Home { get; set; }
	public TeamSimulationProfile Away { get; set; }
	public bool IsPlayoff { get; set; }
}

public sealed class TeamSimulationProfile
{
	public TeamId TeamId { get; set; }
	public int OffenseRating { get; set; }
	public int DefenseRating { get; set; }
	public int SpecialTeamsRating { get; set; }
	public int CoachingRating { get; set; }
	public int ChemistryRating { get; set; }
	public float InjuryPenalty { get; set; }
}

public sealed class GameSimulationOutput
{
	public int HomeScore { get; set; }
	public int AwayScore { get; set; }
	public List<SimEventRecord> Events { get; set; } = new();
	public Dictionary<PlayerId, Dictionary<string, int>> PlayerBoxScores { get; set; } = new();
}
