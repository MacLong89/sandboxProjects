namespace ThinkDrink.Services;

public sealed class AchievementService : IAchievementEvaluator
{
	public IReadOnlyList<AchievementDefinition> Evaluate(
		PlayerProfile profile,
		MatchResult match,
		AchievementCatalog catalog )
	{
		if ( profile is null || catalog?.Achievements is null )
			return Array.Empty<AchievementDefinition>();

		var unlocked = new HashSet<string>( profile.UnlockedAchievements );
		var newly = new List<AchievementDefinition>();

		foreach ( var def in catalog.Achievements )
		{
			if ( unlocked.Contains( def.Id ) ) continue;
			if ( !MeetsThreshold( profile, match, def ) ) continue;

			newly.Add( def );
			unlocked.Add( def.Id );
		}

		return newly;
	}

	private static bool MeetsThreshold( PlayerProfile profile, MatchResult match, AchievementDefinition def )
	{
		var playerResult = match?.Players?.FirstOrDefault( p => p.SteamId == profile.SteamId );

		return def.Trigger switch
		{
			AchievementTrigger.FirstWin => profile.Wins >= 1,
			AchievementTrigger.TotalWins => profile.Wins >= def.Threshold,
			AchievementTrigger.TotalCorrect => profile.CorrectAnswers >= def.Threshold,
			AchievementTrigger.WinStreak => profile.BestWinStreak >= def.Threshold,
			AchievementTrigger.TotalGames => profile.GamesPlayed >= def.Threshold,
			AchievementTrigger.PerfectMatch => playerResult is not null &&
				playerResult.Incorrect == 0 && playerResult.Correct >= 3,
			AchievementTrigger.ComebackWin => playerResult is not null && playerResult.IsWinner &&
				WasComeback( match, profile.SteamId ),
			AchievementTrigger.FastestAnswer => profile.FastestAnswerSeconds <= def.Threshold &&
				profile.FastestAnswerSeconds < float.MaxValue,
			AchievementTrigger.BuzzWins => profile.BuzzWins >= def.Threshold,
			AchievementTrigger.MatchCorrect => playerResult is not null &&
				playerResult.Correct >= def.Threshold,
			_ => false
		};
	}

	public static int GetProgressValue( PlayerProfile profile, AchievementDefinition def )
	{
		if ( profile is null || def is null ) return 0;

		return def.Trigger switch
		{
			AchievementTrigger.FirstWin => profile.Wins,
			AchievementTrigger.TotalWins => profile.Wins,
			AchievementTrigger.TotalCorrect => profile.CorrectAnswers,
			AchievementTrigger.WinStreak => profile.BestWinStreak,
			AchievementTrigger.TotalGames => profile.GamesPlayed,
			AchievementTrigger.FastestAnswer => profile.FastestAnswerSeconds < float.MaxValue
				? (int)MathF.Max( 0, profile.FastestAnswerSeconds ) : 999,
			AchievementTrigger.BuzzWins => profile.BuzzWins,
			_ => 0
		};
	}

	public static int GetProgressTarget( AchievementDefinition def ) => def.Trigger switch
	{
		AchievementTrigger.FirstWin => 1,
		AchievementTrigger.FastestAnswer => def.Threshold,
		_ => def.Threshold
	};

	private static bool WasComeback( MatchResult match, string steamId )
	{
		if ( match?.Players is null || match.Players.Count < 2 ) return false;

		var winner = match.Players.FirstOrDefault( p => p.SteamId == steamId );
		if ( winner is null || !winner.IsWinner ) return false;

		var maxOther = match.Players.Where( p => p.SteamId != steamId ).Max( p => p.Score );
		return winner.Score > maxOther && maxOther >= winner.Score - 2;
	}
}
