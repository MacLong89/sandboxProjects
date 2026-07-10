namespace ThinkDrink;

/// <summary>Facade for local player profile access — UI and client convenience.</summary>
public static class PlayerProfileManager
{
	public static PlayerProfile GetLocalProfile()
	{
		var key = PersistenceManager.GetSteamKey( Connection.Local );
		return PersistenceManager.Instance?.GetProfile( key );
	}

	public static PlayerProfile GetProfile( string steamKey ) =>
		PersistenceManager.Instance?.GetProfile( steamKey );

	public static ChallengeProgress GetDailyChallenge( PlayerProfile profile )
	{
		if ( profile?.DailyChallenges is null || profile.DailyChallenges.Count == 0 )
			return null;
		return profile.DailyChallenges.Values.FirstOrDefault();
	}

	public static ChallengeProgress GetWeeklyChallenge( PlayerProfile profile )
	{
		if ( profile?.WeeklyChallenges is null || profile.WeeklyChallenges.Count == 0 )
			return null;
		return profile.WeeklyChallenges.Values.FirstOrDefault();
	}
}
