namespace ThinkDrink;

/// <summary>Host-side match and lifetime stat tracking.</summary>
public sealed class StatsManager : Component
{
	public static StatsManager Instance { get; private set; }

	private readonly Dictionary<string, int> _correct = new();
	private readonly Dictionary<string, int> _incorrect = new();
	private readonly Dictionary<string, int> _buzzWins = new();
	private readonly Dictionary<string, float> _fastest = new();
	private readonly Dictionary<string, int> _scores = new();

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void BeginMatch()
	{
		if ( !Networking.IsHost ) return;
		_correct.Clear();
		_incorrect.Clear();
		_buzzWins.Clear();
		_fastest.Clear();
		_scores.Clear();
	}

	public void RecordCorrect( string steamKey, string category, Difficulty difficulty, float responseTime )
	{
		if ( !Networking.IsHost ) return;

		Inc( _correct, steamKey );
		if ( responseTime < GetFastest( steamKey ) )
			_fastest[steamKey] = responseTime;

		if ( steamKey == GameConstants.BotSteamKey ) return;

		var profile = PersistenceManager.Instance?.GetOrCreateProfile( FindConnection( steamKey ) );
		if ( profile is null ) return;

		profile.CorrectAnswers++;
		if ( responseTime < profile.FastestAnswerSeconds )
			profile.FastestAnswerSeconds = responseTime;

		if ( !profile.CategoryCorrect.ContainsKey( category ) )
			profile.CategoryCorrect[category] = 0;
		profile.CategoryCorrect[category]++;

		PersistenceManager.Instance.SaveProfile( profile );
		ThinkDrinkPlayer.FindBySteamKey( steamKey )?.RefreshLifetimeStats();
	}

	public void RecordIncorrect( string steamKey )
	{
		if ( !Networking.IsHost ) return;
		Inc( _incorrect, steamKey );
		if ( steamKey == GameConstants.BotSteamKey ) return;

		var profile = PersistenceManager.Instance?.GetProfile( steamKey );
		if ( profile is null ) return;
		profile.IncorrectAnswers++;
		PersistenceManager.Instance.SaveProfile( profile );
	}

	public void RecordBuzzWin( string steamKey )
	{
		if ( !Networking.IsHost ) return;
		Inc( _buzzWins, steamKey );
	}

	public MatchResult FinalizeMatch( string winnerKey, int rounds )
	{
		var result = new MatchResult
		{
			WinnerSteamId = winnerKey ?? "",
			RoundsPlayed = rounds,
			Players = new List<MatchPlayerResult>()
		};

		ThinkDrinkPlayer mvp = null;
		var mvpScore = -1;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( !p.IsParticipant && p.MatchScore == 0 && !_correct.ContainsKey( p.SteamKey ) )
				continue;

			var won = p.SteamKey == winnerKey;
			var playerResult = new MatchPlayerResult
			{
				SteamId = p.SteamKey,
				DisplayName = p.PlayerName,
				Score = p.MatchScore,
				Correct = _correct.TryGetValue( p.SteamKey, out var c ) ? c : 0,
				Incorrect = _incorrect.TryGetValue( p.SteamKey, out var ic ) ? ic : 0,
				BuzzWins = _buzzWins.TryGetValue( p.SteamKey, out var b ) ? b : 0,
				FastestAnswer = _fastest.TryGetValue( p.SteamKey, out var f ) ? f : float.MaxValue,
				IsWinner = won
			};

			if ( p.IsBot )
			{
				result.Players.Add( playerResult );
				if ( p.MatchScore > mvpScore )
				{
					mvpScore = p.MatchScore;
					mvp = p;
				}
				continue;
			}

			var profile = PersistenceManager.Instance?.GetProfile( p.SteamKey );
			if ( profile is null && p.Connection is not null )
				profile = PersistenceManager.Instance?.GetOrCreateProfile( p.Connection );
			if ( profile is null ) continue;

			profile.GamesPlayed++;

			if ( won )
			{
				profile.Wins++;
				profile.CurrentWinStreak++;
				if ( profile.CurrentWinStreak > profile.BestWinStreak )
					profile.BestWinStreak = profile.CurrentWinStreak;

				var month = GameConstants.CurrentMonthlyPeriod();
				if ( profile.MonthlyPeriod != month )
				{
					profile.MonthlyPeriod = month;
					profile.MonthlyWins = 0;
				}

				profile.MonthlyWins++;
			}
			else if ( p.IsConnected )
			{
				profile.Losses++;
				profile.CurrentWinStreak = 0;
			}

			playerResult.XpLines = new List<string>();
			var xpLines = playerResult.XpLines;
			playerResult.XpEarned = XpService.ComputeMatchXp( playerResult, won, xpLines );

			var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
			if ( profile.DailyBonusClaimedDate != today )
			{
				profile.DailyBonusClaimedDate = today;
				playerResult.XpEarned += GameConstants.DailyFirstMatchXpBonus;
				xpLines.Insert( 0, $"Daily bonus · +{GameConstants.DailyFirstMatchXpBonus}" );
			}

			UpdatePlayStreak( profile );

			XpService.ApplyXp( profile, playerResult.XpEarned );
			PersistenceManager.Instance.SaveProfile( profile );
			p.RefreshLifetimeStats();

			if ( p.MatchScore > mvpScore )
			{
				mvpScore = p.MatchScore;
				mvp = p;
			}

			result.Players.Add( playerResult );
		}

		if ( mvp is not null )
		{
			var mvpResult = result.Players.FirstOrDefault( x => x.SteamId == mvp.SteamKey );
			if ( mvpResult is not null ) mvpResult.IsMvp = true;
		}

		result.WinnerName = result.Players.FirstOrDefault( p => p.IsWinner )?.DisplayName ?? "";
		result.Players = result.Players.OrderByDescending( p => p.Score ).ToList();
		return result;
	}

	private static void Inc( Dictionary<string, int> dict, string key )
	{
		if ( !dict.ContainsKey( key ) ) dict[key] = 0;
		dict[key]++;
	}

	private static float GetFastest( string key ) =>
		Instance?._fastest.TryGetValue( key, out var f ) == true ? f : float.MaxValue;

	private static Connection FindConnection( string steamKey )
	{
		var player = ThinkDrinkPlayer.FindBySteamKey( steamKey );
		return player?.Connection;
	}

	private static void UpdatePlayStreak( PlayerProfile profile )
	{
		var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		if ( profile.LastPlayDateUtc == today )
			return;

		if ( DateTime.TryParse( profile.LastPlayDateUtc, out var last ) &&
		     (DateTime.UtcNow.Date - last.Date).TotalDays <= 1.5 )
			profile.ConsecutivePlayDays++;
		else
			profile.ConsecutivePlayDays = 1;

		profile.LastPlayDateUtc = today;
	}
}
