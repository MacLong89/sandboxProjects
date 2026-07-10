namespace ThinkDrink.Services;

public sealed class LeaderboardService
{
	public LeaderboardSnapshot Build( IEnumerable<PlayerProfile> profiles )
	{
		var list = profiles?.Where( p => p is not null ).ToList() ?? new List<PlayerProfile>();
		var snap = new LeaderboardSnapshot();

		snap.Wins = list
			.OrderByDescending( p => p.Wins )
			.ThenByDescending( p => p.CorrectAnswers )
			.Take( GameConstants.LeaderboardTopCount )
			.Select( p => ToEntry( p, p.Wins ) )
			.ToList();

		snap.CorrectAnswers = list
			.OrderByDescending( p => p.CorrectAnswers )
			.ThenByDescending( p => p.Wins )
			.Take( GameConstants.LeaderboardTopCount )
			.Select( p => ToEntry( p, p.CorrectAnswers ) )
			.ToList();

		snap.Accuracy = list
			.Where( p => p.GamesPlayed >= GameConstants.LeaderboardAccuracyMinGames )
			.OrderByDescending( p => p.Accuracy )
			.ThenByDescending( p => p.CorrectAnswers )
			.Take( GameConstants.LeaderboardTopCount )
			.Select( p => ToEntry( p, (int)(p.Accuracy * 100f), p.Accuracy ) )
			.ToList();

		snap.WinStreak = list
			.OrderByDescending( p => p.BestWinStreak )
			.ThenByDescending( p => p.Wins )
			.Take( GameConstants.LeaderboardTopCount )
			.Select( p => ToEntry( p, p.BestWinStreak ) )
			.ToList();

		var month = GameConstants.CurrentMonthlyPeriod();
		snap.MonthlyWins = list
			.Where( p => p.MonthlyPeriod == month && p.MonthlyWins > 0 )
			.OrderByDescending( p => p.MonthlyWins )
			.ThenByDescending( p => p.Wins )
			.Take( GameConstants.LeaderboardTopCount )
			.Select( p => ToEntry( p, p.MonthlyWins ) )
			.ToList();

		return snap;
	}

	private static LeaderboardEntry ToEntry( PlayerProfile p, int value, float accuracy = 0f ) => new()
	{
		SteamId = p.SteamId,
		DisplayName = p.DisplayName,
		Value = value,
		Accuracy = accuracy > 0 ? accuracy : p.Accuracy,
		GamesPlayed = p.GamesPlayed
	};
}
