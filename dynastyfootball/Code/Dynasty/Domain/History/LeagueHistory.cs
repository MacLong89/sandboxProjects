using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.History;

public sealed class LeagueHistory
{
	public List<ChampionshipRecord> Championships { get; set; } = new();
	public List<AwardRecord> Awards { get; set; } = new();
	public List<StatRecord> Records { get; set; } = new();
	public List<HallOfFameEntry> HallOfFame { get; set; } = new();
	public List<RetiredPlayerRecord> RetiredPlayers { get; set; } = new();
	public List<TeamSeasonRecordEntry> SeasonRecords { get; set; } = new();
}

public sealed class TeamSeasonRecordEntry
{
	public TeamId TeamId { get; set; }
	public int Season { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Ties { get; set; }
	public int PointsFor { get; set; }
	public int PointsAgainst { get; set; }
}

public sealed class ChampionshipRecord
{
	public int Season { get; set; }
	public TeamId ChampionId { get; set; }
	public TeamId RunnerUpId { get; set; }
	public int ChampionScore { get; set; }
	public int RunnerUpScore { get; set; }
}

public sealed class AwardRecord
{
	public int Season { get; set; }
	public string AwardName { get; set; } = "";
	public PlayerId PlayerId { get; set; }
	public TeamId TeamId { get; set; }
}

public sealed class StatRecord
{
	public string RecordName { get; set; } = "";
	public string StatKey { get; set; } = "";
	public int Value { get; set; }
	public PlayerId PlayerId { get; set; }
	public int Season { get; set; }
}

public sealed class HallOfFameEntry
{
	public PlayerId PlayerId { get; set; }
	public int InductionSeason { get; set; }
	public string Citation { get; set; } = "";
}

public sealed class RetiredPlayerRecord
{
	public PlayerId PlayerId { get; set; }
	public int RetirementSeason { get; set; }
	public TeamId FinalTeamId { get; set; }
}
