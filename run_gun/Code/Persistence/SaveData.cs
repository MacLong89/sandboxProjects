namespace RunGun;

public sealed class SaveData
{
	public int Version { get; set; } = 2;

	public double Cash { get; set; }
	public double LifetimeEarned { get; set; }

	public Dictionary<string, int> Upgrades { get; set; } = new();

	public float BestDistance { get; set; }
	public double BestScore { get; set; }
	public int TotalRuns { get; set; }
	public int PrestigeLevel { get; set; }

	public string SelectedCharacter { get; set; } = "runner";
	public HashSet<string> UnlockedCharacters { get; set; } = new() { "runner" };
	public HashSet<string> CompletedAchievements { get; set; } = new();

	public int DailyChallengeDate { get; set; }
	public DailyModifier DailyChallengeModifier { get; set; }
	public bool DailyChallengeCompleted { get; set; }

	public long LastPlayedUnix { get; set; }

	public int GetUpgrade( string id ) => Upgrades.TryGetValue( id, out var lvl ) ? lvl : 0;
	public void SetUpgrade( string id, int level ) => Upgrades[id] = level;
}
