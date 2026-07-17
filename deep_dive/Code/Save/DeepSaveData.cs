namespace DeepDive;

public sealed class DeepDiveSaveData
{
	public const int CurrentVersion = 4;

	public int Version { get; set; } = CurrentVersion;
	public float DeepestEverMeters { get; set; }
	public float Money { get; set; }
	public float Shells { get; set; }
	public float LifetimeMoneyEarned { get; set; }
	public int SuccessfulDives { get; set; }
	public int FailedDives { get; set; }
	public Dictionary<string, int> UpgradeLevels { get; set; } = new();
	public List<string> DiscoveredCollectibles { get; set; } = new();
	public List<string> DiscoveredZones { get; set; } = new();
	public List<string> DiscoveredCreatures { get; set; } = new();
	public List<string> DiscoveredStories { get; set; } = new();
	public List<string> DiscoveredCheckpoints { get; set; } = new();
	public Dictionary<string, int> LoadoutReserves { get; set; } = new();
	public List<string> UnlockedTools { get; set; } = new();
	public List<DiveHistoryEntry> DiveHistory { get; set; } = new();
	public int DayNumber { get; set; } = 1;
	public float TimeOfDayHours { get; set; } = 8.5f;
}
