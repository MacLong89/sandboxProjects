namespace Fauna2;

/// <summary>
/// Scene bootstrapper. Shows the main menu first; spawns the networked ZooCore
/// only after the host picks continue or new game.
/// </summary>
public sealed class GameManager : Component, Component.INetworkListener
{
	public static GameManager Instance { get; private set; }

	[Property] public bool AutoCreateLobby { get; set; } = true;
	[Property] public int MaxPlayers { get; set; } = 16;

	[Sync( SyncFlags.FromHost )] public bool GameStarted { get; set; }

	public int ActiveSaveSlot { get; private set; } = 1;

	protected override void OnAwake()
	{
		Instance = this;
		GameSettings.Load();
		PixelArt.ResetRuntimeCaches();
		WorldSprites.ResetDiagnostics();
		Log.Info( "[Fauna2 Boot] GameManager awake — fauna2 code assembly loaded." );
		DefinitionCatalog.EnsureInitialized();
		Fauna2Debug.Info( "Boot", "GameManager OnAwake" );
		Fauna2Debug.LogDefinitions();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart()
	{
		Fauna2Debug.Info( "Boot", $"GameManager OnStart editor={Scene.IsEditor} autoLobby={AutoCreateLobby}" );

		if ( Scene.IsEditor ) return;

		if ( AutoCreateLobby && !Networking.IsActive )
		{
			Fauna2Debug.Info( "Net", "Creating lobby..." );
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig
			{
				MaxPlayers = MaxPlayers,
				Name = "Fauna2 Sanctuary"
			} );
			Fauna2Debug.Info( "Net", $"Lobby created active={Networking.IsActive}" );
		}
		else
		{
			Fauna2Debug.Info( "Net", $"Skipped lobby create active={Networking.IsActive}" );
		}
	}

	public void BeginFromSave( int slotId )
	{
		if ( !SaveHost.CanStartSession || GameStarted ) return;

		UI.UiState.BeginSessionLoadingFresh();
		ActiveSaveSlot = slotId;
		SpawnZooCore();

		var path = slotId == 0 ? GameConstants.LegacySaveFile : SaveSystem.GetSlotPath( slotId );
		var hadFile = FileSystem.Data.FileExists( path );

		if ( !SaveSystem.Instance.TryLoadSlot( slotId ) )
		{
			if ( hadFile )
				Log.Error( $"Fauna: save exists at '{path}' but could not be loaded." );
			else
				Log.Warning( $"Fauna: slot {slotId} is empty — using defaults." );

			SaveSystem.Instance.ActiveSlotId = slotId;
			ZooState.Instance.SetNewGameDefaults();
			PlotSystem.Instance.SetNewGameDefaults();
			if ( TerrainObstacleSystem.Instance.IsValid() )
			{
				TerrainObstacleSystem.Instance.TotalCleared = 0;
				TerrainObstacleSystem.Instance.GenerateWorld( ZooState.Instance.StarterBiome, PlotSystem.Instance, slotId );
			}
			if ( WildernessSpawner.Instance.IsValid() )
				WildernessSpawner.Instance.GenerateWorld( ZooState.Instance.StarterBiome );
		}

		FinishSessionStart();
	}

	public void BeginNewGame( ZooStarterProfile profile, int slotId )
	{
		if ( !SaveHost.CanStartSession || GameStarted || profile is null ) return;

		UI.UiState.BeginSessionLoadingFresh();
		UI.UiState.RequestOnboardingForNewZoo();
		ActiveSaveSlot = slotId;
		SpawnZooCore();
		SaveSystem.Instance.StartNewGame( profile, slotId );
		FinishSessionStart();
		// Catch-up / bootstrap must not leave a brand-new zoo at Level 2+.
		SaveSystem.Instance?.ForceNewGameLevel();
	}

	private void FinishSessionStart()
	{
		Fauna2Debug.Info( "Boot", $"FinishSessionStart slot={ActiveSaveSlot}" );
		Fauna2RenderDiagnostics.ResetSession();
		UI.UiState.CloseModals();
		GameStarted = true;
		PlotSystem.Instance?.EnsureStarterPlot();
		GameSettings.Apply();
		SocialSystem.ClaimPendingVisitorCredits();
		SocialSystem.Instance?.OnVisitorJoined( Connection.Local );

		if ( SaveSystem.Instance.IsValid() )
			SaveSystem.Instance.ActiveSlotId = ActiveSaveSlot;

		EnsureLocalPlayerReady();
		ObjectiveSystem.Instance?.CatchUpGoalsAfterLoad();
		WorldEnvironment.Instance?.BootstrapSessionWorld();
		ClientWorldSync.Instance?.PushFullWorldToClients();
	}

	/// <summary>Called once world visuals and the minimum loading time are ready.</summary>
	public void OnSessionLoadingComplete()
	{
		if ( !SaveHost.CanStartSession )
		{
			UI.UiState.ReleaseStartupNotifications();
			return;
		}

		UI.UiState.BeginOnboardingTips();
	}

	private void EnsureLocalPlayerReady()
	{
		foreach ( var player in Scene.GetAllComponents<PlayerState>() )
		{
			if ( !player.IsValid() || player.IsProxy || !player.IsZooOwner ) continue;
			player.Components.Get<ZooPlayerController>()?.EnsureMovableAfterLoad();
			return;
		}
	}

	private void SpawnZooCore()
	{
		if ( ZooState.Instance.IsValid() ) return;

		var go = new GameObject( true, "ZooCore" );
		go.Tags.Add( "zoocore" );

		go.AddComponent<ZooState>();
		go.AddComponent<PlotSystem>();
		go.AddComponent<EconomySystem>();
		go.AddComponent<GuestSystem>();
		go.AddComponent<AnimalSystem>();
		go.AddComponent<BreedingSystem>();
		go.AddComponent<BuildSystem>();
		go.AddComponent<CollectionSystem>();
		go.AddComponent<ObjectiveSystem>();
		go.AddComponent<ZooMilestones>();
		go.AddComponent<AchievementSystem>();
		go.AddComponent<WeatherSeasonSystem>();
		go.AddComponent<SanctuaryEventSystem>();
		go.AddComponent<DailySanctuarySystem>();
		go.AddComponent<SanctuaryMomentumSystem>();
		go.AddComponent<StaffSystem>();
		go.AddComponent<ResearchSystem>();
		go.AddComponent<FranchiseSystem>();
		go.AddComponent<SocialSystem>();
		go.AddComponent<SaveSystem>();
		go.AddComponent<DailyBonusSystem>();
		go.AddComponent<TerrainObstacleSystem>();
		go.AddComponent<WildernessSpawner>();
		go.AddComponent<CatchSystem>();
		go.AddComponent<WildAttackSystem>();
		go.AddComponent<ClientWorldSync>();
		go.AddComponent<ZooSoundNetwork>();

		Fauna2Debug.Info( "Boot", "SpawnZooCore — networking zoo systems" );

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );
	}

	void INetworkListener.OnActive( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		var go = new GameObject( true, $"Player - {channel.DisplayName}" );
		go.Tags.Add( "player" );
		var playerState = go.AddComponent<PlayerState>();
		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn( channel );
		go.Network.SetOrphanedMode( NetworkOrphaned.Destroy );

		// AUDIT FIX B1: Host stamps IsZooOwner after NetworkSpawn so FromHost Sync
		// replicates to everyone. Lobby host connection == zoo operator.
		// Previously each client wrote IsZooOwner = SaveHost.CanStartSession locally,
		// which a modded client could forge. Revert: remove StampZooOwnership and
		// restore local write in PlayerState.TryBindLocal (NOT recommended).
		var isLobbyHost = Connection.Host is not null
			? channel == Connection.Host
			: channel == Connection.Local;
		playerState.StampZooOwnership( isLobbyHost );

		// Inventory lives on the zoo owner only (was gated on IsZooOwner in OnStart,
		// which may race with FromHost stamp on late replication — ensure here).
		if ( isLobbyHost && playerState.Components.Get<PlayerInventory>() is null )
			playerState.GameObject.GetOrAddComponent<PlayerInventory>();

		if ( GameStarted )
		{
			SocialSystem.Instance?.OnVisitorJoined( channel );
			ClientWorldSync.Instance?.PushFullWorldToClients();
		}
	}

	void INetworkListener.OnDisconnected( Connection channel )
	{
		CatchSystem.Instance?.CancelIfOwnerDisconnected( channel );
		WildAttackSystem.Instance?.CancelIfOwnerDisconnected( channel );
		SocialSystem.Instance?.OnVisitorLeft( channel );
	}
}
