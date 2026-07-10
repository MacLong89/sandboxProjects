namespace Terraingen.Progression;

/// <summary>Player retention contracts — daily (tomorrow hook) and weekly (longer arc).</summary>
public sealed class ThornsSurvivorContractsSnapshotDto
{
	public ThornsDailyContractDto Daily { get; set; } = new();
	public ThornsWeeklyContractDto Weekly { get; set; } = new();
}

public sealed class ThornsDailyContractDto
{
	public string DayId { get; set; } = "";
	public string GoalId { get; set; } = "";
	public bool Completed { get; set; }
}

public sealed class ThornsWeeklyContractDto
{
	public string WeekId { get; set; } = "";
	public List<string> GoalIds { get; set; } = new();
	public bool Completed { get; set; }
}
