namespace Dynasty.Domain.Franchise;

/// <summary>
/// One active owner mandate per season — tracked progress with inbox payoff on completion.
/// </summary>
public sealed class SeasonObjectiveState
{
	public string ObjectiveId { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public int Target { get; set; }
	public int Progress { get; set; }
	public bool Completed { get; set; }
	public bool Failed { get; set; }
	public int AssignedSeason { get; set; }
}
