namespace ThinkDrink.Services;

public static class XpService
{
	public static int ApplyXp( PlayerProfile profile, int amount )
	{
		if ( profile is null || amount <= 0 ) return profile?.Level ?? 1;

		var oldLevel = profile.Level;
		profile.Xp += amount;
		profile.Level = GameConstants.LevelFromXp( profile.Xp );

		if ( profile.Level > oldLevel )
		{
			GameEvents.RaiseLevelUp( profile.SteamId, profile.Level );
			ThinkDrinkPlayer.FindBySteamKey( profile.SteamId )?.NotifyLevelUp( profile.Level, ProgressionTitles.GetTitle( profile.Level ) );
		}

		return profile.Level;
	}

	public static int ComputeMatchXp( MatchPlayerResult result, bool won, List<string> xpLines )
	{
		var xp = GameConstants.ParticipationXpBonus;
		xpLines.Add( $"Played match · +{GameConstants.ParticipationXpBonus}" );

		var correctXp = result.Correct * GameConstants.BaseXpPerCorrect;
		if ( correctXp > 0 )
		{
			xp += correctXp;
			xpLines.Add( $"{result.Correct} correct · +{correctXp}" );
		}

		var buzzXp = result.BuzzWins * GameConstants.BuzzFirstXpBonus;
		if ( buzzXp > 0 )
		{
			xp += buzzXp;
			xpLines.Add( $"{result.BuzzWins} buzz wins · +{buzzXp}" );
		}

		if ( won )
		{
			xp += GameConstants.WinXpBonus;
			xpLines.Add( $"Match win · +{GameConstants.WinXpBonus}" );
		}

		if ( result.IsMvp )
		{
			xp += 50;
			xpLines.Add( "MVP · +50" );
		}

		return xp;
	}
}
