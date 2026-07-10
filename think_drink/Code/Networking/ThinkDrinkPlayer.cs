namespace ThinkDrink;

/// <summary>
/// Per-player networked state. Owned by the player's connection.
/// Clients send input via Rpc.Host; host validates everything.
/// </summary>
public sealed class ThinkDrinkPlayer : Component
{
	public static readonly List<ThinkDrinkPlayer> All = new();

	[Sync( SyncFlags.FromHost )] public string PlayerName { get; set; } = "Player";
	[Sync( SyncFlags.FromHost )] public string SteamKey { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public bool IsReady { get; set; }
	[Sync( SyncFlags.FromHost )] public int MatchScore { get; set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeWins { get; set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeCorrect { get; set; }
	[Sync( SyncFlags.FromHost )] public int LifetimeLevel { get; set; }
	[Sync( SyncFlags.FromHost )] public int LeaderboardRank { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsBuzzWinner { get; set; }
	[Sync( SyncFlags.FromHost )] public bool HasAnsweredThisRound { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsConnected { get; set; } = true;

	[Sync( SyncFlags.FromHost )] public bool IsSpectator { get; set; }
	[Sync( SyncFlags.FromHost )] public bool IsBot { get; set; }
	[Sync( SyncFlags.FromHost )] public string CreativeOwnVoteLetter { get; set; } = "";

	/// <summary>Human connection, or null for bots.</summary>
	public Connection Connection => IsBot ? null : Network.Owner;

	/// <summary>In-match participant (human or bot).</summary>
	public bool IsParticipant => (IsConnected && !IsSpectator) || IsBot;

	public void SetupAsBot( bool ready )
	{
		if ( !Networking.IsHost ) return;

		IsBot = true;
		IsConnected = true;
		IsSpectator = false;
		PlayerName = GameConstants.BotDisplayName;
		SteamKey = GameConstants.BotSteamKey;
		IsReady = ready;
		LifetimeLevel = 3;
		LifetimeWins = 12;
		LifetimeCorrect = 48;
		LeaderboardRank = 0;
	}

	protected override void OnStart()
	{
		if ( !All.Contains( this ) )
			All.Add( this );

		if ( Networking.IsHost && !IsBot )
		{
			PlayerName = Connection?.DisplayName ?? "Player";
			SteamKey = PersistenceManager.GetSteamKey( Connection );
			IsConnected = true;

			if ( MatchManager.Instance?.Phase is not MatchPhase.Lobby and not MatchPhase.PostMatch )
				IsSpectator = true;

			RefreshLifetimeStats();
		}
	}

	protected override void OnDestroy()
	{
		All.Remove( this );
	}

	public void RefreshLifetimeStats()
	{
		if ( !Networking.IsHost ) return;
		if ( IsBot ) return;

		var profile = PersistenceManager.Instance?.GetOrCreateProfile( Connection );
		if ( profile is null ) return;

		LifetimeWins = profile.Wins;
		LifetimeCorrect = profile.CorrectAnswers;
		LifetimeLevel = profile.Level;
		LeaderboardRank = LeaderboardManager.Instance?.GetRank( profile.SteamId ) ?? 0;
	}

	public static ThinkDrinkPlayer FindBySteamKey( string steamKey )
	{
		for ( var i = 0; i < All.Count; i++ )
		{
			if ( All[i].SteamKey == steamKey )
				return All[i];
		}
		return null;
	}

	public static ThinkDrinkPlayer FindByConnection( Connection connection )
	{
		if ( connection is null ) return null;
		for ( var i = 0; i < All.Count; i++ )
		{
			if ( All[i].IsBot ) continue;
			if ( All[i].Connection == connection )
				return All[i];
		}
		return null;
	}

	public static int CountParticipants()
	{
		var n = 0;
		for ( var i = 0; i < All.Count; i++ )
			if ( All[i].IsParticipant ) n++;
		return n;
	}

	public static int CountReadyParticipants()
	{
		var n = 0;
		for ( var i = 0; i < All.Count; i++ )
		{
			var p = All[i];
			if ( !p.IsParticipant ) continue;
			if ( p.IsBot || p.IsReady ) n++;
		}
		return n;
	}

	public static int CountHumans()
	{
		var n = 0;
		for ( var i = 0; i < All.Count; i++ )
		{
			var p = All[i];
			if ( p.IsBot ) continue;
			if ( p.IsConnected ) n++;
		}
		return n;
	}

	public static bool AnyHumanReady()
	{
		for ( var i = 0; i < All.Count; i++ )
		{
			var p = All[i];
			if ( p.IsBot ) continue;
			if ( p.IsConnected && p.IsReady ) return true;
		}
		return false;
	}

	public static ThinkDrinkPlayer GetBot() =>
		All.FirstOrDefault( p => p.IsBot && p.IsValid() );

	public static ThinkDrinkPlayer Local
	{
		get
		{
			var localConnection = Sandbox.Connection.Local;
			if ( localConnection is not null )
			{
				var byConnection = FindByConnection( localConnection );
				if ( byConnection.IsValid() )
					return byConnection;
			}

			for ( var i = 0; i < All.Count; i++ )
			{
				var p = All[i];
				if ( p.IsValid() && !p.IsBot && !p.IsProxy )
					return p;
			}

			return null;
		}
	}

	[Rpc.Host]
	public void RequestReady( bool ready )
	{
		if ( !Networking.IsHost ) return;
		IsReady = ready;
		Log.Info( $"[ThinkDrink][Ready] {PlayerName} ready={IsReady}" );
		LobbyManager.Instance?.OnReadyChanged();
		GameEvents.RaisePlayerListChanged();
	}

	[Rpc.Host]
	public void RequestBuzz()
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TryRegisterBuzz( this );
	}

	[Rpc.Host]
	public void SubmitAnswer( string answer )
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TrySubmitAnswer( this, answer, isSteal: false );
	}

	[Rpc.Host]
	public void SubmitStealAnswer( string answer )
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TrySubmitAnswer( this, answer, isSteal: true );
	}

	[Rpc.Host]
	public void SubmitPrediction( string answer )
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TrySubmitPrediction( this, answer );
	}

	[Rpc.Host]
	public void SubmitCreativeQuip( string answer )
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TrySubmitCreativeQuip( this, answer );
	}

	[Rpc.Host]
	public void SubmitCreativeVote( string letter )
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.TrySubmitCreativeVote( this, letter );
	}

	[Rpc.Host]
	public void VoteWinThreshold( int threshold )
	{
		if ( !Networking.IsHost ) return;
		LobbyManager.Instance?.RegisterVote( SteamKey, threshold );
	}

	[Rpc.Host]
	public void RequestGameMode( GameModeId mode )
	{
		if ( !Networking.IsHost ) return;
		if ( IsBot ) return;
		GameNightManager.Instance?.SelectMode( mode );
		LobbyManager.Instance?.RegisterGameMode( SteamKey, mode );
	}

	[Rpc.Host]
	public void RequestRematch()
	{
		if ( !Networking.IsHost || IsBot ) return;
		MatchManager.Instance?.RequestRematch();
	}

	[Rpc.Host]
	public void RequestSkipPostMatch()
	{
		if ( !Networking.IsHost ) return;
		MatchManager.Instance?.SkipPostMatch();
	}

	[Rpc.Host]
	public void RequestSkipPhase()
	{
		if ( !Networking.IsHost || IsBot ) return;
		MatchManager.Instance?.TrySkipPhase( this );
	}

	[Rpc.Host]
	public void CompleteOnboarding()
	{
		if ( !Networking.IsHost || IsBot ) return;
		var profile = PersistenceManager.Instance?.GetOrCreateProfile( Connection );
		if ( profile is null ) return;
		profile.HasSeenOnboarding = true;
		PersistenceManager.Instance.SaveProfile( profile );
	}

	[Rpc.Owner]
	public void NotifyBuzzWinner()
	{
		if ( IsBot ) return;
		ThinkDrink.UI.UIManager.ShowFlash( "YOU BUZZED IN!", true );
	}

	[Rpc.Owner]
	public void NotifyAnswerResult( bool correct, bool isSteal )
	{
		if ( IsBot ) return;

		if ( correct && isSteal )
			ThinkDrink.UI.UIManager.ShowFlash( "STOLEN!", true );
		else
			ThinkDrink.UI.UIManager.ShowFlash( correct ? "CORRECT!" : "WRONG!", correct );

		if ( correct )
		{
			if ( MatchManager.Instance?.LastAnswerResponseTime <= 3f )
				ThinkDrink.UI.UIManager.ShowToast( "Speed bonus — under 3 seconds!", true );
		}

		if ( !Networking.IsHost )
		{
			if ( correct )
				ThinkDrink.UI.UiFeedback.Success();
			else
				ThinkDrink.UI.UiFeedback.Error();
		}
	}

	[Rpc.Owner]
	public void NotifyPredictionResult( bool correct )
	{
		if ( IsBot ) return;
		if ( correct )
			ThinkDrink.UI.UIManager.ShowToast( $"+{GameConstants.PredictionXpBonus} XP — nice prediction!", true );
		else
			ThinkDrink.UI.UIManager.ShowToast( "Prediction locked in — wait for the reveal.", false );
	}

	[Rpc.Owner]
	public void NotifyBuzzTooSlow()
	{
		if ( IsBot ) return;
		ThinkDrink.UI.UIManager.ShowFlash( "TOO SLOW!", false );
	}

	[Rpc.Owner]
	public void NotifyBuzzNearMiss()
	{
		if ( IsBot ) return;
		ThinkDrink.UI.UIManager.ShowFlash( "SO CLOSE!", false );
		ThinkDrink.UI.UIManager.ShowToast( "You were milliseconds behind — try again!", false );
	}

	[Rpc.Owner]
	public void NotifyToast( string message, bool positive )
	{
		if ( IsBot ) return;
		ThinkDrink.UI.UIManager.ShowToast( message, positive );
	}

	[Rpc.Owner]
	public void NotifyLevelUp( int level, string title )
	{
		if ( IsBot ) return;
		ThinkDrink.UI.UIManager.ShowLevelUp( level, title );
	}

	[Rpc.Host]
	public void RequestPracticeBot()
	{
		if ( !Networking.IsHost || IsBot ) return;
		BotManager.Instance?.ForcePracticeBot();
	}

	public void ResetRoundState()
	{
		if ( !Networking.IsHost ) return;
		IsBuzzWinner = false;
		HasAnsweredThisRound = false;
		CreativeOwnVoteLetter = "";
	}

	public void ResetMatchScore()
	{
		if ( !Networking.IsHost ) return;
		MatchScore = 0;
	}
}
