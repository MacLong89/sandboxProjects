namespace ThinkDrink;

public sealed class ChallengeManager : Component
{
	public static ChallengeManager Instance { get; private set; }

	private readonly ChallengeService _service = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void OnBuzzWin( string steamKey )
	{
		if ( !Networking.IsHost || steamKey == GameConstants.BotSteamKey ) return;
		var profile = PersistenceManager.Instance?.GetProfile( steamKey );
		if ( profile is null ) return;
		_service.OnBuzzWin( profile );
	}

	public void OnCorrectAnswer( string steamKey, TriviaQuestion question, bool buzzedFirst )
	{
		if ( !Networking.IsHost || steamKey == GameConstants.BotSteamKey ) return;
		var profile = PersistenceManager.Instance?.GetProfile( steamKey );
		if ( profile is null ) return;

		_service.EnsureChallenges( profile, DateTime.UtcNow );
		_service.OnCorrectAnswer( profile, question, buzzedFirst );
		PersistenceManager.Instance.SaveProfile( profile );
	}

	public void ProcessMatchEnd( MatchResult result )
	{
		if ( !Networking.IsHost || result is null ) return;

		foreach ( var pr in result.Players )
		{
			var profile = PersistenceManager.Instance?.GetProfile( pr.SteamId );
			if ( profile is null ) continue;

			_service.EnsureChallenges( profile, DateTime.UtcNow );
			_service.OnMatchEnd( profile, result );
			var completed = _service.GetCompletedUnclaimed( profile );
			foreach ( var id in completed )
			{
				var desc = profile.DailyChallenges.Values.FirstOrDefault( c => c.ChallengeId == id )?.Description
					?? profile.WeeklyChallenges.Values.FirstOrDefault( c => c.ChallengeId == id )?.Description
					?? id;
				result.CompletedChallenges.Add( desc );
			}
			_service.ClaimRewards( profile );
			PersistenceManager.Instance.SaveProfile( profile );
		}
	}
}
