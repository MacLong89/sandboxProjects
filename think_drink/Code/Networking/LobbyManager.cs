namespace ThinkDrink;

/// <summary>Pre-match lobby — ready state, settings voting, auto-start countdown.</summary>
public sealed class LobbyManager : Component
{
	public static LobbyManager Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public float CountdownRemaining { get; set; }
	[Sync( SyncFlags.FromHost )] public int VotedWinThreshold { get; set; } = GameConstants.DefaultWinThreshold;
	[Sync( SyncFlags.FromHost )] public bool CountdownActive { get; set; }

	private readonly Dictionary<string, int> _votes = new();
	private int _lastCountdownSecond = -1;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void OnPlayerJoined( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var player = ThinkDrinkPlayer.FindByConnection( connection );
		player?.RefreshLifetimeStats();

		var profile = PersistenceManager.Instance?.GetOrCreateProfile( connection );
		if ( profile is not null )
		{
			if ( profile.LastVotedThreshold >= GameConstants.MinWinThreshold )
				RegisterVote( profile.SteamId, profile.LastVotedThreshold );

			if ( GameModeRegistry.IsSelectable( profile.LastGameModeId ) )
				GameNightManager.Instance?.SelectMode( profile.LastGameModeId );
		}

		BotManager.Instance?.OnHumanJoined();
		TryStartCountdown();
	}

	public void OnPlayerLeft( Connection connection )
	{
		if ( !Networking.IsHost ) return;

		var key = PersistenceManager.GetSteamKey( connection );
		_votes.Remove( key );

		BotManager.Instance?.OnHumanLeft();

		if ( CountdownActive && !AllReady() )
			ResetCountdown();
	}

	public void OnReadyChanged()
	{
		if ( !Networking.IsHost ) return;
		BotManager.Instance?.OnReadyChanged();
		TryStartCountdown();
	}

	public void RegisterVote( string steamKey, int threshold )
	{
		if ( !Networking.IsHost ) return;
		if ( steamKey == GameConstants.BotSteamKey ) return;

		threshold = Math.Clamp( threshold, GameConstants.MinWinThreshold, GameConstants.MaxWinThreshold );
		_votes[steamKey] = threshold;
		RecalculateVote();

		var profile = PersistenceManager.Instance?.GetProfile( steamKey );
		if ( profile is not null )
		{
			profile.LastVotedThreshold = threshold;
			PersistenceManager.Instance.SaveProfile( profile );
		}

		GameEvents.RaisePlayerListChanged();
	}

	public void RegisterGameMode( string steamKey, GameModeId mode )
	{
		if ( !Networking.IsHost ) return;
		if ( steamKey == GameConstants.BotSteamKey ) return;

		var profile = PersistenceManager.Instance?.GetProfile( steamKey );
		if ( profile is null ) return;

		profile.LastGameModeId = mode;
		PersistenceManager.Instance.SaveProfile( profile );
	}

	private void RecalculateVote()
	{
		if ( _votes.Count == 0 )
		{
			VotedWinThreshold = GameConstants.DefaultWinThreshold;
			return;
		}

		var sum = 0;
		foreach ( var v in _votes.Values )
			sum += v;
		VotedWinThreshold = Math.Clamp( sum / _votes.Count, GameConstants.MinWinThreshold, GameConstants.MaxWinThreshold );
	}

	public int GetMinPlayersRequired() => GameConstants.MinPlayers;

	public int GetReadyParticipantCount() => ThinkDrinkPlayer.CountReadyParticipants();

	public int GetParticipantCount() => ThinkDrinkPlayer.CountParticipants();

	public bool AllReady()
	{
		if ( ThinkDrinkPlayer.CountParticipants() < GameConstants.MinPlayers ) return false;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( !p.IsParticipant ) continue;
			if ( p.IsBot ) continue;
			if ( !p.IsReady ) return false;
		}

		return ThinkDrinkPlayer.AnyHumanReady();
	}

	public void TryStartCountdown()
	{
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;
		if ( !AllReady() ) return;

		if ( IsBotPracticeLobby() )
		{
			LaunchMatch();
			return;
		}

		StartCountdown( GameConstants.LobbyCountdownSeconds );
	}

	public void StartRematchCountdown()
	{
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;

		for ( var i = 0; i < ThinkDrinkPlayer.All.Count; i++ )
		{
			var p = ThinkDrinkPlayer.All[i];
			if ( p.IsBot ) continue;
			if ( !p.IsConnected ) continue;
			p.IsReady = true;
		}

		BotManager.Instance?.OnReadyChanged();
		if ( ThinkDrinkPlayer.CountParticipants() < GameConstants.MinPlayers )
			BotManager.Instance?.EnsureBot( autoReady: true );

		if ( IsBotPracticeLobby() )
		{
			LaunchMatch();
			return;
		}

		StartCountdown( GameConstants.RematchCountdownSeconds );
	}

	public void ResetCountdown()
	{
		CountdownActive = false;
		CountdownRemaining = 0;
		_lastCountdownSecond = -1;
	}

	private void StartCountdown( float seconds )
	{
		CountdownActive = true;
		CountdownRemaining = seconds;
		_lastCountdownSecond = (int)MathF.Ceiling( seconds );
		GameEvents.RaiseAudio( AudioEventId.Countdown );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Networking.IsHost ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.Lobby ) return;
		if ( !CountdownActive ) return;

		CountdownRemaining -= Time.Delta;

		var second = (int)MathF.Ceiling( CountdownRemaining );
		if ( second != _lastCountdownSecond && second > 0 && second <= 5 )
		{
			_lastCountdownSecond = second;
			GameEvents.RaiseAudio( AudioEventId.Countdown );
		}

		if ( CountdownRemaining > 0 ) return;

		LaunchMatch();
	}

	static bool IsBotPracticeLobby() =>
		BotManager.Instance?.BotActive == true && ThinkDrinkPlayer.CountHumans() == 1;

	void LaunchMatch()
	{
		CountdownActive = false;
		CountdownRemaining = 0;
		_lastCountdownSecond = -1;
		MatchManager.Instance?.StartMatch( VotedWinThreshold, GameNightManager.Instance?.SelectedGameMode ?? GameModeId.TriviaShowdown );
	}
}
