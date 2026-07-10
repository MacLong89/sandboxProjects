namespace ThinkDrink.Domain;

public sealed class PlayerProfile
{
	public string SteamId { get; set; } = "";
	public string DisplayName { get; set; } = "Player";

	public int Level { get; set; } = 1;
	public int Xp { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int CorrectAnswers { get; set; }
	public int IncorrectAnswers { get; set; }
	public int GamesPlayed { get; set; }
	public int CurrentWinStreak { get; set; }
	public int BestWinStreak { get; set; }
	public int BuzzWins { get; set; }
	public float FastestAnswerSeconds { get; set; } = float.MaxValue;

	public List<string> UnlockedAchievements { get; set; } = new();
	public Dictionary<string, int> CategoryCorrect { get; set; } = new();
	public Dictionary<string, ChallengeProgress> DailyChallenges { get; set; } = new();
	public Dictionary<string, ChallengeProgress> WeeklyChallenges { get; set; } = new();
	public List<string> RecentQuestionIds { get; set; } = new();
	public bool HasSeenOnboarding { get; set; }
	public int LastVotedThreshold { get; set; } = 10;
	public GameModeId LastGameModeId { get; set; } = GameModeId.TriviaShowdown;
	public string DailyBonusClaimedDate { get; set; } = "";
	public string LastPlayDateUtc { get; set; } = "";
	public int ConsecutivePlayDays { get; set; }
	public int MonthlyWins { get; set; }
	public string MonthlyPeriod { get; set; } = "";

	public float WinRate => GamesPlayed > 0 ? Wins / (float)GamesPlayed : 0f;
	public float Accuracy
	{
		get
		{
			var total = CorrectAnswers + IncorrectAnswers;
			return total > 0 ? CorrectAnswers / (float)total : 0f;
		}
	}
}

public sealed class ChallengeProgress
{
	public string ChallengeId { get; set; } = "";
	public int Current { get; set; }
	public int Target { get; set; }
	public bool Completed { get; set; }
	public bool Claimed { get; set; }
	public int XpReward { get; set; }
	public string Description { get; set; } = "";
}

public sealed class MatchSettings
{
	public int WinThreshold { get; set; } = 10;
	public bool StealEnabled { get; set; } = true;
	public bool PredictionsEnabled { get; set; } = true;
	public int MinPlayers { get; set; } = 2;
}

public sealed class MatchPlayerResult
{
	public string SteamId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public int Score { get; set; }
	public int Correct { get; set; }
	public int Incorrect { get; set; }
	public int BuzzWins { get; set; }
	public float FastestAnswer { get; set; } = float.MaxValue;
	public int XpEarned { get; set; }
	public List<string> XpLines { get; set; } = new();
	public bool IsWinner { get; set; }
	public bool IsMvp { get; set; }
}

public sealed class MatchResult
{
	public string WinnerSteamId { get; set; } = "";
	public string WinnerName { get; set; } = "";
	public List<MatchPlayerResult> Players { get; set; } = new();
	public int RoundsPlayed { get; set; }
	public List<string> NewAchievements { get; set; } = new();
	public List<string> CompletedChallenges { get; set; } = new();
}

public sealed class LeaderboardEntry
{
	public string SteamId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public int Value { get; set; }
	public float Accuracy { get; set; }
	public int GamesPlayed { get; set; }
}

public sealed class LeaderboardSnapshot
{
	public List<LeaderboardEntry> Wins { get; set; } = new();
	public List<LeaderboardEntry> CorrectAnswers { get; set; } = new();
	public List<LeaderboardEntry> Accuracy { get; set; } = new();
	public List<LeaderboardEntry> WinStreak { get; set; } = new();
	public List<LeaderboardEntry> MonthlyWins { get; set; } = new();
}

public sealed class AchievementDefinition
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public string Icon { get; set; } = "emoji_events";
	public AchievementTrigger Trigger { get; set; }
	public int Threshold { get; set; } = 1;
	public int XpReward { get; set; } = 50;
}

public sealed class AchievementCatalog
{
	public List<AchievementDefinition> Achievements { get; set; } = new();
}

public sealed class PlayerSettings
{
	public int SettingsVersion { get; set; }
	public float MasterVolume { get; set; } = 0.5f;
	public float SfxVolume { get; set; } = 0.5f;
	public float MusicVolume { get; set; } = 0.3f;
	public bool ShowHints { get; set; } = true;
	public bool AutoReady { get; set; }
}

public sealed class ProfilesSaveFile
{
	public int Version { get; set; } = 1;
	public Dictionary<string, PlayerProfile> Profiles { get; set; } = new();
}

public sealed class LeaderboardSaveFile
{
	public int Version { get; set; } = 1;
	public LeaderboardSnapshot Snapshot { get; set; } = new();
}
