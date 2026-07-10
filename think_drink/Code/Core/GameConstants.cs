namespace ThinkDrink;

/// <summary>Central tuning knobs for Think &amp; Drink.</summary>
public static class GameConstants
{
	public const string SaveDirectory = "think_drink";
	public const string ProfilesFile = "think_drink/profiles.json";
	public const string LeaderboardFile = "think_drink/leaderboards.json";
	public const string SettingsFile = "think_drink/settings.json";
	public const int SaveVersion = 1;

	public const int MinPlayers = 2;
	public const int MaxPlayers = 8;

	public const string BotSteamKey = "bot:quizmaster";
	public const string BotDisplayName = "Practice Bot";
	public const float BotAutoJoinDelaySeconds = 10f;
	public const float RematchCountdownSeconds = 3f;
	public const int DailyFirstMatchXpBonus = 100;
	public const int ParticipationXpBonus = 20;
	public const int WeeklyFeaturedCategoryXpBonus = 15;
	public const int DefaultWinThreshold = 10;
	public const int MinWinThreshold = 5;
	public const int MaxWinThreshold = 20;

	public const float BuzzWindowSeconds = 10f;
	public const float AnswerWindowSeconds = 20f;
	public const float LobbyCountdownSeconds = 10f;
	public const float FirstRoundCategoryRevealSeconds = 4f;
	public const float FirstRoundQuestionRevealSeconds = 3f;
	public const float CategoryRevealSeconds = 2.5f;
	public const float QuestionRevealSeconds = 2f;
	public const float RoundRevealSeconds = 2f;
	public const float ScoreboardRevealSeconds = 2f;
	public const float PostMatchSeconds = 8f;
	public const float PostMatchSkipDelaySeconds = 3f;
	public const float NearMissBuzzSeconds = 0.35f;
	public const float StealWindowSeconds = 8f;

	public const float RandomEventChance = 0.12f;
	public const int RecentQuestionMemory = 40;
	public const int LeaderboardAccuracyMinGames = 10;
	public const int LeaderboardTopCount = 25;

	public const int BaseXpPerCorrect = 25;
	public const int PredictionXpBonus = 10;
	public const int WinXpBonus = 150;
	public const int BuzzFirstXpBonus = 5;

	public static readonly string[] Categories =
	{
		"Animals", "Geography", "Movies", "Science", "Gaming", "Sports", "History", "Food", "Math"
	};

	public static int XpForLevel( int level ) => 100 + (level - 1) * 75;

	public static int LevelFromXp( int xp )
	{
		var level = 1;
		while ( xp >= XpForLevel( level ) && level < 999 )
		{
			xp -= XpForLevel( level );
			level++;
		}
		return level;
	}

	public static string GetFeaturedCategory()
	{
		var index = (DateTime.UtcNow.DayOfYear / 7) % Categories.Length;
		return Categories[index];
	}

	public static string CurrentMonthlyPeriod() => DateTime.UtcNow.ToString( "yyyy-MM" );
}
