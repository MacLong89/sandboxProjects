namespace ThinkDrink.Data;

public sealed class AchievementCatalogLoader
{
	private AchievementCatalog _catalog;

	public AchievementCatalog Catalog => _catalog ??= Load();

	public AchievementCatalog Load()
	{
		try
		{
			const string path = "data/achievements.json";
			if ( FileSystem.Mounted.FileExists( path ) )
			{
				var json = FileSystem.Mounted.ReadAllText( path );
				var catalog = Json.Deserialize<AchievementCatalog>( json );
				if ( catalog?.Achievements?.Count > 0 )
					return catalog;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: achievement catalog load failed — {e.Message}" );
		}

		return CreateDefault();
	}

	private static AchievementCatalog CreateDefault() => new()
	{
		Achievements = new List<AchievementDefinition>
		{
			new() { Id = "first_win", Title = "First Win", Description = "Win your first match.", Icon = "emoji_events", Trigger = AchievementTrigger.FirstWin, Threshold = 1, XpReward = 100 },
			new() { Id = "trivia_master", Title = "Trivia Master", Description = "Win 25 matches.", Icon = "school", Trigger = AchievementTrigger.TotalWins, Threshold = 25, XpReward = 500 },
			new() { Id = "century", Title = "100 Answers", Description = "Answer 100 questions correctly.", Icon = "check_circle", Trigger = AchievementTrigger.TotalCorrect, Threshold = 100, XpReward = 200 },
			new() { Id = "millennium", Title = "1000 Answers", Description = "Answer 1000 questions correctly.", Icon = "stars", Trigger = AchievementTrigger.TotalCorrect, Threshold = 1000, XpReward = 2000 },
			new() { Id = "streak_10", Title = "10 Win Streak", Description = "Reach a 10 win streak.", Icon = "local_fire_department", Trigger = AchievementTrigger.WinStreak, Threshold = 10, XpReward = 750 },
			new() { Id = "perfect_match", Title = "Perfect Match", Description = "Win a match without a wrong answer.", Icon = "verified", Trigger = AchievementTrigger.PerfectMatch, Threshold = 1, XpReward = 300 },
			new() { Id = "comeback_king", Title = "Comeback King", Description = "Win after trailing late.", Icon = "trending_up", Trigger = AchievementTrigger.ComebackWin, Threshold = 1, XpReward = 250 },
			new() { Id = "speed_demon", Title = "Speed Demon", Description = "Answer correctly in under 3 seconds.", Icon = "bolt", Trigger = AchievementTrigger.FastestAnswer, Threshold = 3, XpReward = 200 },
			new() { Id = "regular", Title = "Regular", Description = "Play 50 matches.", Icon = "groups", Trigger = AchievementTrigger.TotalGames, Threshold = 50, XpReward = 400 },
		}
	};
}
