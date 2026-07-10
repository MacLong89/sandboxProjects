namespace Terraingen.Player;

using Sandbox;
using Sandbox.Network;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.Rendering;
using Terraingen.Victory;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

/// <summary>Core player gameplay host: lifecycle, snapshots, tames, menu, and shared RPC validation.</summary>
[Title( "Thorns Player Gameplay" )]
[Category( "Thorns/Player" )]
public sealed partial class ThornsPlayerGameplay : Component, Component.INetworkSpawn
{
	public const int XpPerLevel = ThornsXpBalance.XpPerLevel;
	public const int XpWildlifeKill = 25;
	public const int XpBanditKill = 35;
	public const int XpTameCreature = 75;

	public static ThornsPlayerGameplay Local { get; private set; }

	[Sync( SyncFlags.FromHost )] public string AccountKey { get; private set; } = "";

	readonly ThornsInventoryContainer _inventory = new();
	readonly ThornsCraftQueueHost _craftQueue = new();

	bool _craftPanelExpanded = true;
	string _craftCategory = ThornsCraftCatalog.AllCraftCategoryId;
	string _selectedRecipeId = "recipe_stone_pickaxe";
	int _activeHotbarIndex;
	ThornsCraftStationKind _nearestStation = ThornsCraftStationKind.Hand;

	int _totalXp;
	int _playerLevel = 1;

	ThornsJournalSnapshotDto _journal = new();
	ThornsSkillsSnapshotDto _skills = new();
	ThornsTamesSnapshotDto _tames = new();
	ThornsGuildSnapshotDto _guild = new();
	ThornsVitalsSnapshotDto _vitals = new();
	ThornsResearchSnapshotDto _research = new();

	bool _hostProgressInitialized;

	readonly HashSet<string> _hostConsumedMilestoneEvents = new( StringComparer.OrdinalIgnoreCase );

	static readonly HashSet<string> ClientRequestableOneShotMilestoneEvents = new( StringComparer.OrdinalIgnoreCase )
	{
	};

	float _healthRevealTimer;
	float _foodRevealTimer;
	float _waterRevealTimer;
	float _tempRevealTimer;
	float _starveDamageTimer;
	TimeSince _researchPushDebounce;
	float _clientMapRefreshTimer;
	bool _localCosmeticsNotifyAttempted;
	bool _localPresentationBootstrapped;
	bool _ownerRpcGraceActive;
	bool _lastReportedSprintHeld;
	float _sprintHoldReportTimer;
	TimeSince _cameraReclaimCooldown;

	Connection _owner;
	readonly ThornsPlayerVitalsNetwork _vitalsNetwork = new();

	protected override void OnStart()
	{
		ThornsDefinitionRegistry.EnsureInitialized();

		if ( ThornsMultiplayer.IsHostOrOffline )
			HostInitialize();

		ThornsWorldShadowUtil.EnableWorldShadowsOnHierarchy( GameObject );

		TryBootstrapLocalPresentation();

		if ( IsLocalPlayer() && ThornsTerrainBootstrap.Instance?.IsWorldApplied == true )
			RefreshMapSnapshot();
	}

	protected override void OnDestroy()
	{
		if ( IsLocalPlayer() )
			TryShowSessionRecap();

		if ( Local == this )
			Local = null;
	}

	void TryShowSessionRecap()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var snapshot = ThornsUiClientState.Snapshot;
		var journal = snapshot.Journal;
		var goalId = ThornsJourneyProgression.HostResolveHudPinnedGoalId( journal );
		var nextGoal = ThornsDefinitionRegistry.GetGoal( goalId )?.Title ?? "Survive and build shelter";

		var lines = new List<string> { "Progress saved." };

		if ( snapshot.Map?.Markers?.Any( m => m.Kind == ThornsMapMarkerKind.LastDeath ) == true )
			lines.Add( "Your death crate is marked on the map." );

		var killer = HostGetSessionLastKillerDisplayName();
		if ( !string.IsNullOrWhiteSpace( killer ) )
			lines.Add( $"Last killed by {killer} — settle the score next time." );

		lines.Add( $"Next goal when you return:\n{nextGoal}" );
		ThornsMenuHost.Instance?.ShowSessionRecap( string.Join( "\n\n", lines ) );
	}

	public void OnNetworkSpawn( Connection owner )
	{
		_owner = owner ?? Connection.Find( GameObject.Network.OwnerId );
		AccountKey = ThornsPersistenceIdentity.GetStableAccountKey( _owner );

		if ( ThornsMultiplayer.IsHostOrOffline )
			HostInitialize();

		ThornsWorldShadowUtil.EnableWorldShadowsOnHierarchy( GameObject );

		TryBootstrapLocalPresentation();

		if ( ThornsMultiplayer.IsHostOrOffline )
			PushSnapshotToOwnerClient();
	}

	/// <summary>
	/// Joining clients can miss network ownership during the first <see cref="OnNetworkSpawn"/> frame.
	/// Retry until the local spawn coordinator focuses the pawn camera and binds HUD input.
	/// </summary>
	void TryBootstrapLocalPresentation()
	{
		if ( !IsLocalPlayer() )
			return;

		Local = this;
		ThornsPawnInputIsolation.ApplyForLocalPawn( Scene, GameObject );
		ThornsWorldBootGate.EnsureDriver();

		if ( Networking.IsActive && !Networking.IsHost )
			ThornsWorldBootGate.BeginLocalBoot();

		if ( Networking.IsActive && !Networking.IsHost )
		{
			if ( !ThornsLocalHostSpawnCoordinator.IsHandling( GameObject ) )
			{
				ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncCharacter );
				ThornsLocalHostSpawnCoordinator.Queue( Scene, GameObject );
			}

			_localPresentationBootstrapped = ThornsLocalHostSpawnCoordinator.IsHandling( GameObject );
			return;
		}

		if ( !_localPresentationBootstrapped )
			_ = DeferredLocalSpawnFollowUpAsync();

		_localPresentationBootstrapped = true;
	}

	void MaybeReclaimLocalCamera()
	{
		if ( !IsLocalPlayer() || !Networking.IsActive || Networking.IsHost || _cameraReclaimCooldown < 0.75f )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		var scene = Scene;
		if ( scene is null || !scene.IsValid )
			return;

		var cam = scene.Camera;
		var previewName = "Terrain Preview Camera";
		var needsReclaim = !cam.IsValid()
		                   || !cam.Enabled
		                   || ( cam.IsValid() && cam.GameObject.Name.Equals( previewName, StringComparison.OrdinalIgnoreCase ) );

		if ( !needsReclaim && cam.IsValid() )
		{
			var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
			if ( rig.IsValid() && cam.GameObject != rig )
				needsReclaim = true;
		}

		if ( !needsReclaim )
			return;

		_cameraReclaimCooldown = 0;
		Log.Info( "[Thorns Player] Reclaiming local pawn camera after join." );
		ThornsSceneObserver.FocusLocalPlayer( scene, GameObject );
		ThornsGameplayUiHost.RefreshScreenPanelCamera( scene );
	}

	/// <summary>Joining clients: deferred terrain cosmetics populate when the local pawn spawns.</summary>
	void TryNotifyLocalPlayerCosmetics()
	{
		if ( _localCosmeticsNotifyAttempted || !IsLocalPlayer() )
			return;

		// Listen-server host applies cosmetics via ThornsNetworkGameManager frame pipeline.
		if ( Networking.IsActive && Networking.IsHost )
			return;

		var bootstrap = ThornsTerrainBootstrap.Instance;
		if ( !bootstrap.IsValid() )
			return;

		if ( !bootstrap.QueueLocalPlayerCosmetics() )
			return;

		_localCosmeticsNotifyAttempted = true;
		Log.Info( "[Thorns Player] Local player cosmetics ready after network spawn." );
	}

	void TryRefreshMapWhenWorldReady()
	{
		if ( ThornsTerrainBootstrap.Instance?.IsWorldApplied == true )
			RefreshMapSnapshot();
	}

	async System.Threading.Tasks.Task DeferredLocalSpawnFollowUpAsync()
	{
		await System.Threading.Tasks.Task.Yield();
		await System.Threading.Tasks.Task.Yield();

		if ( !GameObject.IsValid() || !Game.IsPlaying || !IsLocalPlayer() )
			return;

		TryNotifyLocalPlayerCosmetics();
		TryRefreshMapWhenWorldReady();

		if ( Networking.IsActive && !Networking.IsHost )
			Log.Info( "[Thorns Player] Joining client deferred follow-up complete." );
	}

	void PushSnapshotToOwnerClient()
	{
		if ( !Networking.IsActive )
		{
			var bundle = HostBuildSnapshotBundle();
			ThornsUiClientState.ApplySnapshot( bundle );
			ScheduleMenuProfileCacheSave( bundle );
			return;
		}

		_ = DeferredPushSnapshotToOwnerAsync();
	}

	static void ScheduleMenuProfileCacheSave( ThornsPlayerSnapshotBundle bundle )
	{
		if ( bundle is null )
			return;

		_ = DeferredSaveMenuProfileCacheAsync( bundle );
	}

	static async System.Threading.Tasks.Task DeferredSaveMenuProfileCacheAsync( ThornsPlayerSnapshotBundle bundle )
	{
		await System.Threading.Tasks.Task.Yield();
		ThornsMenuProfile.SaveFromSnapshot( bundle );
	}

	async System.Threading.Tasks.Task DeferredPushSnapshotToOwnerAsync()
	{
		var yields = !Networking.IsActive || IsLocalPlayer() ? 2 : 16;
		for ( var i = 0; i < yields; i++ )
			await System.Threading.Tasks.Task.Yield();

		if ( !GameObject.IsValid() || !IsValid || !Game.IsPlaying )
			return;

		_ownerRpcGraceActive = false;

		try
		{
			RpcReceiveSnapshotJson( Json.Serialize( HostBuildSnapshotBundle() ) );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Deferred snapshot push failed." );
		}
	}

	public bool IsLocalPlayer() => ThornsLocalPlayer.IsLocalConnectionOwner( this );

	bool CanPushOwnerRpcs() => !Networking.IsActive || IsLocalPlayer() || !_ownerRpcGraceActive;

	/// <summary>Offline terrain explorer and combat sandboxes — stable key for tames/saves.</summary>
	public void HostEnsurePersistenceAccountKey()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !GameObject.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( AccountKey ) )
			return;

		var session = Components.Get<ThornsPlayerSession>() ?? Components.Create<ThornsPlayerSession>();
		session.HostEnsurePersistenceKey( _owner ?? Connection.Local );
		AccountKey = ThornsPersistenceIdentity.GetStableAccountKey( GameObject );

		if ( string.IsNullOrWhiteSpace( AccountKey ) && session.IsValid() )
			AccountKey = session.HostPersistenceAccountKey;
	}

	/// <summary>Combat sandboxes spawn the explorer before a persistence key exists — run after <see cref="HostEnsurePersistenceAccountKey"/>.</summary>
	public void HostEnsureProgressInitialized()
	{
		HostEnsurePersistenceAccountKey();
		HostInitialize();
	}

	/// <summary>Re-apply the mob test gun kit even when progress was already initialized from persistence.</summary>
	public void HostApplyMobTestCombatKit()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !ThornsBanditCombatTestScene.IsActive )
			return;

		ThornsStarterLoadout.ApplyBanditTest( _inventory );
		ThornsStarterLoadout.ApplyFullVitals( _vitals );
		HostNormalizeWeaponRows();
		HostActivateHotbarSlot( 0 );
		MarkInventorySyncDirty();
		PushInventoryToOwner( force: true );
		Components.Get<ThornsPlayerWeaponCombat>()?.HostRefreshHudFromActiveWeapon();
		Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	/// <summary>Re-apply the bow test kit even when progress was already initialized from persistence.</summary>
	public void HostApplyBowTestKit()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !ThornsBowTestScene.IsActive )
			return;

		ThornsStarterLoadout.ApplyBowTest( _inventory );
		ThornsStarterLoadout.ApplyFullVitals( _vitals );
		HostNormalizeWeaponRows();
		HostActivateHotbarSlot( 0 );
		MarkInventorySyncDirty();
		PushInventoryToOwner( force: true );
		Components.Get<ThornsPlayerWeaponCombat>()?.HostRefreshHudFromActiveWeapon();
		Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	void HostInitialize()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostEnsurePersistenceAccountKey();

		if ( string.IsNullOrWhiteSpace( AccountKey ) )
			return;

		var deferOwnerSync = Networking.IsActive && !IsLocalPlayer();
		_ownerRpcGraceActive = deferOwnerSync;

		if ( _hostProgressInitialized )
		{
			if ( ThornsJourneyProgression.NeedsJournalMigration( _journal ) )
				HostRefreshJourneyJournal();
			return;
		}

		_hostProgressInitialized = true;

		var persistence = ThornsWorldPersistence.Instance;
		var restoredProgress = !ThornsBanditCombatTestScene.IsActive
		                       && !ThornsBowTestScene.IsActive
		                       && persistence?.Live is not null
		                       && ThornsPlayerProgressPersistence.TryRestoreFromWorld( persistence.Live, this );

		if ( ThornsBanditCombatTestScene.IsActive )
		{
			ThornsStarterLoadout.ApplyBanditTest( _inventory );
			ThornsStarterLoadout.ApplyFullVitals( _vitals );
		}
		else if ( ThornsBowTestScene.IsActive )
		{
			ThornsStarterLoadout.ApplyBowTest( _inventory );
			ThornsStarterLoadout.ApplyFullVitals( _vitals );
		}
		else if ( !ThornsBanditCombatTestScene.IsActive && !ThornsBowTestScene.IsActive && _inventory.IsEmpty() )
		{
			_inventory.SeedStarterItems();
			if ( !restoredProgress )
				ThornsStarterLoadout.ApplyNewPlayerVitals( _vitals );
		}

		HostNormalizeWeaponRows();

		_ = ScheduleDeferredIconWarm();
		if ( restoredProgress )
		{
			if ( _journal.Goals is null || _journal.Goals.Count == 0 )
				HostRebuildJournal();
			else
				ThornsJourneyProgression.HostMigrateJournalSnapshot( _journal );

			if ( _skills.Ranks is null || _skills.Ranks.Count == 0 )
				HostRebuildSkills();
			else
				HostRecalculateUpgradePoints();
		}
		else
		{
			HostRebuildJournal();
			HostRebuildSkills();
		}

		HostRebuildTames();
		if ( !ThornsBanditCombatTestScene.IsActive && !ThornsBowTestScene.IsActive )
			ThornsTameSummonUtil.HostSummonOwnedTamesNearPlayer( Scene, AccountKey );
		ThornsWorldPersistence.Instance?.SyncGuildsToWorldService();
		ThornsGuildWorldService.Instance?.HostEnsurePersonalGuild( this );
		_guild = ThornsGuildWorldService.Instance?.GetGuildSnapshotFor( AccountKey ) ?? new ThornsGuildSnapshotDto();
		HostRefreshVictorySnapshot();
		ThornsGuildWorldService.Instance?.EnrichGuildSnapshot( _guild, _victory );

		if ( ThornsMultiplayer.IsHostOrOffline && !deferOwnerSync )
			HostPushVictorySnapshot();

		if ( !restoredProgress && !ThornsBanditCombatTestScene.IsActive && !ThornsBowTestScene.IsActive )
			ThornsFirstSessionRetention.HostOnNewPlayerReady( this );

		HostApplySurvivalCaps();
		HostRefreshVitals( forceShowHealth: true, skipOwnerPush: deferOwnerSync );

		if ( ThornsMultiplayer.IsHostOrOffline && !deferOwnerSync
		     && ThornsJourneyProgression.NeedsJournalMigration( _journal ) )
			HostRefreshJourneyJournal();
	}

	protected override void OnUpdate()
	{
		if ( IsLocalPlayer() )
		{
			TryBootstrapLocalPresentation();

			if ( ThornsLocalHostSpawnCoordinator.IsDeferredPending )
				ThornsLocalHostSpawnCoordinator.TickDeferred();

			TickLocalPeerPresentation();
			MaybeReclaimLocalCamera();
		}

		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( HostIsDead() )
			return;

		var completed = _craftQueue.Tick( Time.Delta, out var changed );
		if ( completed is not null )
			HostGrantRecipeOutput( completed );

		if ( changed || _craftQueue.Entries.Count > 0 )
		{
			MarkInventorySyncDirty();
			PushInventoryToOwner();
		}

		HostTickResearch( Time.Delta );
		HostTickCampfireSmelt( Time.Delta );
		HostTickWorkbenchRepair( Time.Delta );
		TickSurvival( Time.Delta );
		TickVitalsRevealTimers();
		HostRefreshVitals( forceShowHealth: false );
		ThornsJourneyProgression.HostTickWorldUnlocks( this );
		ThornsJourneyWaypointSync.HostTick( this );
		ThornsFirstSessionRetention.HostTick( Scene );
	}

	/// <summary>Local peer (host or client): sprint intent replication, deferred cosmetics, throttled map refresh.</summary>
	void TickLocalPeerPresentation()
	{
		ReportSprintIntentToHost();

		if ( !_localCosmeticsNotifyAttempted )
			TryNotifyLocalPlayerCosmetics();

		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsTabMenuOpen )
			return;

		_clientMapRefreshTimer -= Time.Delta;
		if ( _clientMapRefreshTimer > 0f )
			return;

		_clientMapRefreshTimer = Core.ThornsHudTickRates.MapSnapshotSeconds;

		var bootstrap = ThornsTerrainBootstrap.Instance;
		if ( bootstrap is null || !bootstrap.IsValid() || !bootstrap.IsWorldApplied )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RefreshClientWorldMapSnapshot();
		else
			RefreshMapSnapshot();
	}

	void ReportSprintIntentToHost()
	{
		if ( !Networking.IsActive || Networking.IsHost || !IsLocalPlayer() )
			return;

		var sprint = Input.Down( "Run" ) && CanSprint;
		var changed = sprint != _lastReportedSprintHeld;
		_sprintHoldReportTimer -= Time.Delta;
		if ( !changed && ( !sprint || _sprintHoldReportTimer > 0f ) )
			return;

		_lastReportedSprintHeld = sprint;
		if ( sprint )
			_sprintHoldReportTimer = 0.2f;

		RpcReportSprintHeld( sprint );
	}

	void HostRebuildTames()
	{
		var previousSelected = _tames?.SelectedEntityId ?? Guid.Empty;
		_tames = ThornsTameSnapshotBuilder.BuildForAccount( Scene, AccountKey );

		if ( previousSelected != Guid.Empty && _tames.Tames.Any( t => t.EntityId == previousSelected ) )
			_tames.SelectedEntityId = previousSelected;
	}

	public void HostRebuildTamesFromWorld()
	{
		HostRebuildTames();
		PushTamesToOwner();
	}

	public ThornsPlayerSnapshotBundle HostBuildSnapshotBundle()
	{
		var map = ThornsMapWorldService.Instance?.BuildSnapshotFor( this, AccountKey ) ?? new ThornsMapSnapshotDto();
		HostRefreshVictorySnapshot();
		return new ThornsPlayerSnapshotBundle
		{
			Inventory = _inventory.ToSnapshot( _craftPanelExpanded, _craftCategory, _selectedRecipeId, _activeHotbarIndex ),
			Craft = _craftQueue.ToSnapshot( _nearestStation ),
			Journal = _journal,
			Skills = _skills,
			Tames = _tames,
			Guild = _guild,
			Map = map,
			Vitals = _vitals,
			Research = HostBuildResearchSnapshot(),
			Campfire = HostBuildCampfireSnapshot(),
			Workbench = HostBuildWorkbenchSnapshot(),
			Victory = _victory,
			Contracts = _survivorContracts
		};
	}

	[Rpc.Owner]
	void RpcReceiveSnapshotJson( string json )
	{
		try
		{
			if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsPlayerSnapshotBundle bundle ) )
				return;

			ThornsUiClientState.ApplySnapshot( bundle );
			ScheduleMenuProfileCacheSave( bundle );

			if ( IsLocalPlayer() )
			{
				Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
				ThornsMenuHost.Instance?.RefreshHud();
			}
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Snapshot deserialize failed." );
		}
	}

	bool ValidateCaller() => ThornsNetAuthority.ValidateOwnerCaller( this );

	[Rpc.Owner]
	void RpcPlayOwnerSfx( string resourcePath )
	{
		ThornsGameplaySfx.PlayAtPawnEar( GameObject, resourcePath );
	}

	void PlayOwnerSfx( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		if ( !Networking.IsActive || IsLocalPlayer() )
			ThornsGameplaySfx.PlayAtPawnEar( GameObject, resourcePath );
		else
			RpcPlayOwnerSfx( resourcePath );
	}

	public void RequestTameCommand( ThornsTameCommandRequest req )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcTameCommand( req );
		else
			HostTameCommand( req );
	}

	[Rpc.Host]
	void RpcTameCommand( ThornsTameCommandRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostTameCommand( req );
	}

	public void HostTameCommand( ThornsTameCommandRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		ThornsTameCommandHost.Apply( Scene, AccountKey, req );
		HostRebuildTames();
		PushTamesToOwner();
		ThornsWorldPersistence.RequestSave();
	}

	public void RequestTameRename( ThornsTameRenameRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcTameRename( req );
		else
			HostTameRename( req );
	}

	[Rpc.Host]
	void RpcTameRename( ThornsTameRenameRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostTameRename( req );
	}

	public void HostTameRename( ThornsTameRenameRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		ThornsTameRenameHost.Apply( Scene, AccountKey, req );
		HostRebuildTames();
		PushTamesToOwner();
		ThornsWorldPersistence.Instance?.TryHostSaveNow();
	}

	public void RequestTameFeed( ThornsTameFeedRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcTameFeed( req );
		else
			HostTameFeed( req );
	}

	[Rpc.Host]
	void RpcTameFeed( ThornsTameFeedRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostTameFeed( req );
	}

	public void HostTameFeed( ThornsTameFeedRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		var ok = ThornsTameFeedHost.Apply( Scene, AccountKey, this, req, out var message );
		if ( !string.IsNullOrWhiteSpace( message ) )
			PushTameFeedNotice( message, ok ? "success" : "warning" );

		if ( !ok )
			return;

		HostRebuildTames();
		PushTamesToOwner();
		ThornsWorldPersistence.Instance?.TryHostSaveNow();
	}

	public void RequestTameStatUpgrade( ThornsTameStatUpgradeRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcTameStatUpgrade( req );
		else
			HostTameStatUpgrade( req );
	}

	[Rpc.Host]
	void RpcTameStatUpgrade( ThornsTameStatUpgradeRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostTameStatUpgrade( req );
	}

	public void HostTameStatUpgrade( ThornsTameStatUpgradeRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		var ok = ThornsTameStatUpgradeHost.Apply( Scene, AccountKey, req, out var message );
		if ( !string.IsNullOrWhiteSpace( message ) )
			PushTameFeedNotice( message, ok ? "success" : "warning" );

		if ( !ok )
			return;

		HostRebuildTames();
		PushTamesToOwner();
		ThornsWorldPersistence.Instance?.TryHostSaveNow();
	}

	void PushOwnerNotification( string message, string kind )
	{
		if ( IsLocalPlayer() )
			ThornsNotificationBus.Push( message, kind );
		else if ( Networking.IsActive )
			RpcOwnerNotification( message, kind );
	}

	void PushTameFeedNotice( string message, string kind )
	{
		if ( IsLocalPlayer() )
			ThornsTameFeedNoticeBus.Push( message, kind );
		else if ( Networking.IsActive )
			RpcTameFeedNotice( message, kind );
	}

	[Rpc.Owner]
	void RpcOwnerNotification( string message, string kind )
	{
		ThornsNotificationBus.Push( message, kind );
	}

	[Rpc.Owner]
	void RpcTameFeedNotice( string message, string kind )
	{
		ThornsTameFeedNoticeBus.Push( message, kind );
	}

	void PushTamesToOwner()
	{
		if ( !CanPushOwnerRpcs() )
			return;

		if ( !Networking.IsActive )
		{
			ThornsUiClientState.ApplyPartialTames( _tames );
			return;
		}

		RpcSyncTamesJson( Json.Serialize( _tames ) );
	}

	[Rpc.Owner]
	void RpcSyncTamesJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsTamesSnapshotDto tames ) )
			return;

		ThornsUiClientState.ApplyPartialTames( tames );
	}

	/// <summary>Pull latest authoritative snapshot when opening the Tab menu.</summary>
	public void RefreshMenuSnapshot()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
		{
			RpcRequestMenuSnapshot();
			return;
		}

		_ = DeferredRefreshMenuSnapshotAsync();
	}

	async System.Threading.Tasks.Task DeferredRefreshMenuSnapshotAsync()
	{
		await System.Threading.Tasks.Task.Yield();

		if ( !GameObject.IsValid() || !IsValid || !Game.IsPlaying || !IsLocalPlayer() )
			return;

		RefreshGuildFromWorld();
		PushSnapshotToOwnerClient();
	}

	/// <summary>Host-only: ensure a personal guild exists before opening the Guild tab (avoids re-entrant UI rebuild).</summary>
	public void EnsurePersonalGuildForMenu()
	{
		if ( !IsLocalPlayer() || !ThornsMultiplayer.IsHostOrOffline )
			return;

		ThornsGuildWorldService.Instance?.HostEnsurePersonalGuild( this );
		RefreshGuildFromWorld();
	}

	[Rpc.Host]
	void RpcRequestMenuSnapshot()
	{
		if ( !ValidateCaller() )
			return;

		PushSnapshotToOwnerClient();
	}

	public void RefreshGuildFromWorld( bool pushVictory = true )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_guild = ThornsGuildWorldService.Instance?.GetGuildSnapshotFor( AccountKey ) ?? new ThornsGuildSnapshotDto();
		HostRefreshVictorySnapshot();
		ThornsGuildWorldService.Instance?.EnrichGuildSnapshot( _guild, _victory );

		if ( pushVictory )
			PushVictoryToOwner();

		if ( !CanPushOwnerRpcs() )
			return;

		if ( !Networking.IsActive )
		{
			ThornsUiClientState.ApplyPartialGuild( _guild );
			return;
		}

		RpcSyncGuildJson( Json.Serialize( _guild ) );
	}

	[Rpc.Owner]
	void RpcSyncGuildJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsGuildSnapshotDto guild ) )
			return;

		ThornsUiClientState.ApplyPartialGuild( guild );
	}

	void HostRebuildJournalFromState()
	{
		if ( _journal.Goals is null || _journal.Goals.Count == 0 )
			HostRebuildJournal();
		else
			ThornsJourneyProgression.HostNormalizeGoalStates( _journal );
	}

	/// <summary>Host-only: close loot/radio/research sessions (death, disconnect).</summary>
	public void HostCleanupOpenSessions()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		HostCloseWorldContainer();
		HostCloseRadioShop();
		HostCloseResearchStation();
		HostCloseCampfire();
		HostCloseWorkbench();
	}

	bool HostIsDead()
	{
		var health = Components.Get<ThornsPlayerHealth>();
		return health.IsValid() && ( !health.IsAlive || health.IsDeadState );
	}

	static async System.Threading.Tasks.Task ScheduleDeferredIconWarm()
	{
		try
		{
			await System.Threading.Tasks.Task.Yield();
			await System.Threading.Tasks.Task.Yield();
			ThornsIconCache.WarmGameplayIcons();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Deferred icon warm failed." );
		}
	}
}
