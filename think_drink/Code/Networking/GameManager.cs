namespace ThinkDrink;

/// <summary>
/// Scene bootstrapper — creates lobby, spawns GameCore and per-player objects.
/// Implements host-authoritative multiplayer entry point.
/// </summary>
public sealed class GameManager : Component, Component.INetworkListener
{
	public static GameManager Instance { get; private set; }

	[Property] public bool AutoCreateLobby { get; set; } = true;

	/// <summary>Host migration prep: GameCore uses NetworkOrphaned.Host.</summary>
	[Property] public bool HostAuthoritative { get; set; } = true;

	protected override void OnAwake()
	{
		Instance = this;
		GameSettings.Load();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor ) return;

		if ( AutoCreateLobby && !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = GameConstants.MaxPlayers,
				Name = "Think & Drink",
				Privacy = Sandbox.Network.LobbyPrivacy.Public
			} );
		}

		if ( Networking.IsHost && !GameCore.Instance.IsValid() )
			SpawnGameCore();

		if ( Networking.IsHost )
			_ = EnsureHostPlayerAsync();
	}

	private async Task EnsureHostPlayerAsync()
	{
		await Task.DelayRealtimeSeconds( 0.25f );
		if ( Connection.Local is null ) return;
		if ( ThinkDrinkPlayer.FindByConnection( Connection.Local ) is not null ) return;
		( (INetworkListener)this ).OnActive( Connection.Local );
	}

	private static void SpawnGameCore()
	{
		var go = new GameObject( true, "GameCore" );
		go.Tags.Add( "gamecore" );

		go.AddComponent<GameCore>();
		go.AddComponent<PersistenceManager>();
		go.AddComponent<QuestionManager>();
		go.AddComponent<GameNightManager>();
		go.AddComponent<MatchManager>();
		go.AddComponent<LobbyManager>();
		go.AddComponent<StatsManager>();
		go.AddComponent<LeaderboardManager>();
		go.AddComponent<ChallengeManager>();
		go.AddComponent<AchievementManager>();
		go.AddComponent<AudioManager>();
		go.AddComponent<MusicManager>();
		go.AddComponent<RandomEventManager>();
		go.AddComponent<BotManager>();

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );
	}

	void INetworkListener.OnActive( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		if ( !GameCore.Instance.IsValid() )
			SpawnGameCore();

		var go = new GameObject( true, $"Player - {channel.DisplayName}" );
		go.Tags.Add( "player" );
		go.AddComponent<ThinkDrinkPlayer>();
		go.AddComponent<PlayerPawn>();

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn( channel );
		go.Network.SetOrphanedMode( NetworkOrphaned.Destroy );

		LobbyManager.Instance?.OnPlayerJoined( channel );
		if ( ThinkDrinkPlayer.CountHumans() == 1 )
			BotManager.Instance?.OnReturnedToLobby();
		GameEvents.RaisePlayerListChanged();
	}

	void INetworkListener.OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		MatchManager.Instance?.OnPlayerDisconnected( channel );
		LobbyManager.Instance?.OnPlayerLeft( channel );
		GameEvents.RaisePlayerListChanged();
	}
}

/// <summary>Marker/accessor for the networked core object.</summary>
public sealed class GameCore : Component
{
	public static GameCore Instance { get; private set; }

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}
}
