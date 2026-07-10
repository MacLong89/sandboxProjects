namespace Sandbox;

/// <summary>
/// Host-only world persistence facade — orchestrates domain snapshot services and disk I/O.
/// Each <see cref="Connection"/> owns one pawn; snapshots keyed by
/// <see cref="ThornsPersistenceIdentity.GetStableAccountKey"/>.
/// </summary>
[Title( "Thorns — World persistence (host)" )]
[Category( "Thorns/Multiplayer" )]
[Icon( "save" )]
public sealed class ThornsWorldPersistence : Component
{
	public const string DefaultRelativePath = "Thorns/mp_persistent_world.json";

	static string _pendingRelativeSavePath;

	public static ThornsWorldPersistence Instance { get; private set; }

	public static void ClearPendingRelativeSavePath() => _pendingRelativeSavePath = null;

	public static void HostNotifyWorldStructuresDirty() =>
		ThornsStructureSnapshotService.HostNotifyWorldStructuresDirty();

	public static void HostNotifyWorldStructureSpawned() =>
		ThornsStructureSnapshotService.HostNotifyWorldStructureSpawned();

	public static void HostNotifyStructureDestroyedByDemolish() =>
		ThornsStructureSnapshotService.HostNotifyStructureDestroyedByDemolish();

	public static bool TryGetRuntimePlayerSnapshot( string accountKey, out ThornsPersistentPlayerDto dto ) =>
		ThornsPlayerSnapshotService.TryGetRuntimePlayerSnapshot( accountKey, out dto );

	public void HostWriteBedSpawnForAccount( string accountKey, ThornsPersistentPlayerDto bedFieldsSource ) =>
		ThornsPlayerSnapshotService.HostWriteBedSpawnForAccount( _live, accountKey, bedFieldsSource );

	public static void HostTryRememberPlayerBeforeTeardown(
		ThornsPlayer session,
		ThornsInventorySlotNet[] inventorySlotsOverride = null ) =>
		ThornsPlayerSnapshotService.HostTryRememberPlayerBeforeTeardown( session, inventorySlotsOverride );

	public static void HostTryRememberPlayerBeforeTeardownFromRoot(
		string accountKey,
		Guid ownerConnectionId,
		GameObject pawnRoot,
		ThornsInventorySlotNet[] inventorySlotsOverride = null ) =>
		ThornsPlayerSnapshotService.HostTryRememberPlayerBeforeTeardownFromRoot(
			accountKey,
			ownerConnectionId,
			pawnRoot,
			inventorySlotsOverride );

	public static void HostRefreshTamedWildlifeRuntimeCache() =>
		ThornsWildlifeSnapshotService.HostRefreshTamedWildlifeRuntimeCache();

	public static void HostRefreshTamedWildlifeRuntimeCacheThrottled( float minIntervalSeconds = 6f ) =>
		ThornsWildlifeSnapshotService.HostRefreshTamedWildlifeRuntimeCacheThrottled( minIntervalSeconds );

	public static void SetPendingRelativeSavePath( string relativePath )
	{
		if ( string.IsNullOrWhiteSpace( relativePath ) )
			return;
		_pendingRelativeSavePath = relativePath.Trim().Replace( '\\', '/' );
	}

	public static bool TryPeekWorldGenerationSeed( string relativePath, out int seed ) =>
		ThornsPersistenceSerializer.TryPeekWorldGenerationSeed( relativePath, out seed );

	[Property] public string RelativeSavePath { get; set; } = DefaultRelativePath;

	[Property] public float AutoSaveSeconds { get; set; } = 45f;

	[Property] public float WorldRestoreDelaySeconds { get; set; } = 0.35f;

	ThornsPersistentWorldDto _live;

	bool _worldApplied;

	bool _persistenceHostInitialized;

	Guid _spawnRestoreChannelId = Guid.Empty;

	ThornsPersistentPlayerDto _spawnRestoreProfile;

	bool _spawnRestoreActive;

	string _spawnRestoreMatchedAccountKey = "";

	double _nextAutoSaveTime;

	bool _saveQueued;

	ThornsDeferredHostSpawnQueue _saveQueue;

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		ThornsPlayerSnapshotService.HostClearRuntimeCache();
		ThornsWildlifeSnapshotService.HostClearRuntimeCache();
		HostTryInitializePersistenceIfNeeded();
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
			TryHostSaveNow( immediate: true );
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || !Game.IsPlaying || !_saveQueued )
			return;

		_saveQueued = false;
		EnsureSaveQueue().EnqueueOrRunNow( ExecuteHostSaveNow );
	}

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost && !_persistenceHostInitialized )
			HostTryInitializePersistenceIfNeeded();

		if ( Networking.IsHost && ThornsStructureSnapshotService.PendingDemolishAuthoritativeEmptyCheck )
			ThornsStructureSnapshotService.HostTickPendingDemolishEmptyCheck();

		if ( Networking.IsHost && Game.IsPlaying && ThornsStructureSnapshotService.StructureDirtySaveDueRealtime > 0
		     && Time.Now >= ThornsStructureSnapshotService.StructureDirtySaveDueRealtime )
		{
			ThornsStructureSnapshotService.StructureDirtySaveDueRealtime = 0;
			TryHostSaveNow();
		}

		if ( !Networking.IsHost || !Game.IsPlaying )
			return;

		if ( Time.Now < _nextAutoSaveTime )
			return;

		_nextAutoSaveTime = Time.Now + Math.Max( 15f, AutoSaveSeconds );
		TryHostSaveNow();
	}

	void HostTryInitializePersistenceIfNeeded()
	{
		if ( _persistenceHostInitialized || !Game.IsPlaying || !Networking.IsHost )
			return;

		if ( !string.IsNullOrEmpty( _pendingRelativeSavePath ) )
		{
			RelativeSavePath = _pendingRelativeSavePath;
			_pendingRelativeSavePath = null;
		}

		_persistenceHostInitialized = true;
		TryHostLoadDiskIntoLive();
		_nextAutoSaveTime = Time.Now + Math.Max( 5f, AutoSaveSeconds );
		_ = ApplyWorldSnapshotWhenReadyAsync();
	}

	public void HostEnsureInitializedBeforePlayerSpawn() =>
		HostTryInitializePersistenceIfNeeded();

	async System.Threading.Tasks.Task ApplyWorldSnapshotWhenReadyAsync()
	{
		await Task.DelayRealtimeSeconds( Math.Max( 0.05f, WorldRestoreDelaySeconds ) );
		if ( !this.IsValid() || !Networking.IsHost || !Game.IsPlaying )
			return;

		TryHostApplyWorldStructuresAndWildlife();
	}

	void TryHostLoadDiskIntoLive()
	{
		_live = new ThornsPersistentWorldDto();
		try
		{
			if ( !FileSystem.Data.FileExists( RelativeSavePath ) )
			{
				Log.Info( $"[Thorns] Persistence: no save at '{RelativeSavePath}' (fresh world)." );
				return;
			}

			var dto = ThornsPersistenceSerializer.ReadWorld( RelativeSavePath, out var readMs, out _ );
			if ( dto.Version < 1 )
			{
				Log.Warning( $"[Thorns] Persistence: unreadable or unknown version at '{RelativeSavePath}'." );
				return;
			}

			_live = dto;
			Log.Info(
				$"[Thorns] Persistence: loaded save v{dto.Version} structures={dto.Structures.Count} wildlife={dto.Wildlife.Count} players={dto.PlayersByAccountKey.Count} utc={dto.SavedUtcIso} readMs={readMs:F1}" );
			ThornsPlayerSnapshotService.LogInventoryDiagnosticsFromWorldDto( dto, "disk read (before hydrate)" );
			ThornsPersistenceSerializer.TryHydrateInventorySlotsBlobFromRawDiskJson( _live, RelativeSavePath );
			ThornsPlayerSnapshotService.LogInventoryDiagnosticsFromWorldDto( _live, "disk read (after hydrate)" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns] Persistence: failed reading '{RelativeSavePath}' — starting fresh runtime snapshot." );
			_live = new ThornsPersistentWorldDto();
		}
		finally
		{
			ThornsStructureSnapshotService.HostRefreshStructureShadowFromLoadedDisk( _live );
		}
	}

	void TryHostApplyWorldStructuresAndWildlife()
	{
		if ( _worldApplied || _live is null || !Networking.IsHost )
			return;

		ThornsStructureSnapshotService.HostApplyStructuresFromSave( Scene, _live.Structures ?? [] );
		ThornsWildlifeSnapshotService.HostApplyWildlifeFromSave( Scene, _live.Wildlife ?? [], GameObject );

		_worldApplied = true;
		Log.Info( "[Thorns] Persistence: world snapshot spawned on host." );
	}

	public bool HostBeginSpawnRestore( Connection channel )
	{
		if ( !ThornsPlayerSnapshotService.HostTryResolveSpawnProfile( _live, channel, out var profile, out var matchedKey ) )
			return false;

		_spawnRestoreChannelId = channel.Id;
		_spawnRestoreProfile = profile;
		_spawnRestoreMatchedAccountKey = matchedKey;
		_spawnRestoreActive = true;
		Log.Info( $"[Thorns] Persistence: spawn restore queued for '{channel.DisplayName}' matchedKey={matchedKey}" );
		return true;
	}

	public void HostApplySpawnRestoreProfile( Connection channel, GameObject playerRoot )
	{
		if ( !_spawnRestoreActive || channel is null || _spawnRestoreProfile is null || playerRoot is null
		     || !playerRoot.IsValid() )
			return;

		try
		{
			ThornsPlayerSnapshotService.HostApplySpawnRestoreProfile(
				channel,
				playerRoot,
				_spawnRestoreProfile,
				_spawnRestoreMatchedAccountKey,
				_live );
		}
		finally
		{
			_spawnRestoreActive = false;
			_spawnRestoreChannelId = Guid.Empty;
			_spawnRestoreProfile = null;
			_spawnRestoreMatchedAccountKey = "";
		}
	}

	public bool HostSpawnRestoreSkipsDefaultInventory( Connection owner ) =>
		owner != null && _spawnRestoreActive && owner.Id == _spawnRestoreChannelId;

	public bool HostSpawnRestoreSkipsWalletStartingGold( Connection owner ) =>
		owner != null && _spawnRestoreActive && owner.Id == _spawnRestoreChannelId;

	public bool HostSuppressVitalsBootstrapForPawn( GameObject pawnRoot )
	{
		if ( !_spawnRestoreActive || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		return pawnRoot.Network.OwnerId == _spawnRestoreChannelId;
	}

	public static void HostRemapStructureOwnersForConnection( Connection channel )
	{
		if ( !Networking.IsHost || channel is null )
			return;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( channel );
		ThornsStructureSnapshotService.HostRemapStructureOwnersForAccountKey( key, channel.Id );
		ThornsWildlifeSnapshotService.HostRemapWildlifeOwnersForAccountKey( key, channel.Id );
	}

	public void TryHostSaveNow( bool immediate = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( immediate )
		{
			_saveQueued = false;
			ExecuteHostSaveNow();
			return;
		}

		_saveQueued = true;
	}

	ThornsDeferredHostSpawnQueue EnsureSaveQueue()
	{
		if ( _saveQueue is null || !_saveQueue.IsValid() )
		{
			_saveQueue = Components.Get<ThornsDeferredHostSpawnQueue>();
			if ( !_saveQueue.IsValid() )
				_saveQueue = Components.Create<ThornsDeferredHostSpawnQueue>();
			_saveQueue.WorkBudgetPerFrame = 1;
		}

		return _saveQueue;
	}

	void ExecuteHostSaveNow()
	{
		if ( !Networking.IsHost )
			return;

		try
		{
			var dto = CaptureSnapshot();
			ThornsReplicationDiagnostics.LogPersistenceWriteFootprint( dto, RelativeSavePath );
			if ( !ThornsPersistenceSerializer.WriteWorld( RelativeSavePath, dto ) )
				return;

			var full = FileSystem.Data.GetFullPath( RelativeSavePath );
			Log.Info(
				$"[Thorns] Persistence: wrote '{full}' structures={dto.Structures.Count} players={dto.PlayersByAccountKey.Count}" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Persistence: save failed (snapshot)." );
		}
	}

	public void HostTryFlushDisconnectedPlayer( Connection channel ) =>
		ThornsPlayerSnapshotService.HostTryFlushDisconnectedPlayer( channel, Scene );

	public void HostOnPlayerDisconnected( Connection channel )
	{
		if ( !Networking.IsHost )
			return;

		HostTryFlushDisconnectedPlayer( channel );
		ThornsWildlifeMountHost.HostDismountConnectionFromAnyWildlife( GameObject.Scene, channel.Id );
		HostRefreshTamedWildlifeRuntimeCache();
		TryHostSaveNow( immediate: true );
	}

	ThornsPersistentWorldDto CaptureSnapshot()
	{
		var players = ThornsPlayerSnapshotService.HostCapturePlayersForSnapshot( _live, Scene );
		var wildlife = ThornsWildlifeSnapshotService.HostResolveWildlifeForSnapshot();
		var structures = ThornsStructureSnapshotService.HostResolveStructuresForSnapshot();
		var snapshotDto = ThornsWorldMetaSnapshotService.AssembleSnapshot( Scene, _live, players, structures, wildlife );
		_live = snapshotDto;
		return snapshotDto;
	}
}
