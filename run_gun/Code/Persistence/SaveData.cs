namespace RunGun;

public sealed class SaveData
{
	public int Version { get; set; } = 4;

	public double Cash { get; set; }
	public double LifetimeEarned { get; set; }

	public Dictionary<string, int> Upgrades { get; set; } = new();

	public float BestDistance { get; set; }
	public double BestScore { get; set; }
	public int TotalRuns { get; set; }
	public int PrestigeLevel { get; set; }

	/// <summary>True after the player finishes (or skips) the forced first riot run.</summary>
	public bool HasCompletedTutorialRun { get; set; }

	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();

	/// <summary>First tutorial run — player took a green squad gate.</summary>
	public bool TutorialGreenGatePassed { get; set; }

	/// <summary>First tutorial run — player survived a red trap gate.</summary>
	public bool TutorialRedSurvived { get; set; }

	/// <summary>Soft-revives used across the lifetime soft-revive window.</summary>
	public int SoftRevivesUsed { get; set; }

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
