namespace Terraingen.Multiplayer;

using Sandbox.Network;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.Victory;
using Terraingen.NpcGuild;
using Terraingen.World.Environment;

/// <summary>
/// Host-only, server-specific player persistence. Saves player transforms by stable account key inside the selected world file.
/// </summary>
[Title( "Thorns World Persistence" )]
[Category( "Thorns/Multiplayer" )]
[Icon( "save" )]
public sealed class ThornsWorldPersistence : Component
{
	public const string DefaultRelativePath = "Terraingen/mp_persistent_world.json";

	static string _pendingRelativeSavePath;

	public static ThornsWorldPersistence Instance { get; private set; }

	[Property] public string RelativeSavePath { get; set; } = DefaultRelativePath;

	[Property, Range( 5f, 120f )] public float AutoSaveSeconds { get; set; } = 25f;

	ThornsPersistentWorldDto _live = new();
	double _nextAutoSaveTime;
	bool _initialized;
	bool _structuresRestored;
	bool _buildingLootRestored;
	bool _worldResourcesRestored;
	bool _deathCratesRestored;
	bool _saveWorkerBusy;
	bool _saveQueued;
	bool _queuedPreserveTamesWhenSceneEmpty;
	bool _pendingSaveWrite;
	ThornsPersistentWorldDto _pendingSaveDto;
	string _pendingSavePath;
	bool _freshWorldPendingSunrise;
	bool _significantSavePending;
	double _significantSaveDueTime;
	bool _loadFailed;
	string _loadFailedError = "";
	string _quarantinedSavePath = "";

	const float SignificantSaveDebounceSeconds = 3f;

	/// <summary>True when the host save could not be loaded; autosaves stay suppressed until recovery.</summary>
	public bool LoadFailed => _loadFailed;
	public string LoadFailedError => _loadFailedError;
	public string QuarantinedSavePath => _quarantinedSavePath;

	public static bool SavesSuppressed { get; private set; }

	public static void RequestSave()
	{
		if ( SavesSuppressed )
			return;

		Instance?.TryHostSaveNow();
	}

	/// <summary>Debounced save for high-frequency state (health drain, vitals, passive changes).</summary>
	public static void RequestSignificantSave()
	{
		if ( SavesSuppressed )
			return;

		var inst = Instance;
		if ( inst is null )
			return;

		inst._significantSavePending = true;
		if ( inst._significantSaveDueTime <= 0 )
			inst._significantSaveDueTime = Time.Now + SignificantSaveDebounceSeconds;
	}

	/// <summary>Flush pending debounced work and save now (death, respawn, quit).</summary>
	public static void RequestImmediateSave( bool forceSync = false, bool preserveTamesWhenSceneEmpty = false )
	{
		if ( SavesSuppressed )
			return;

		var inst = Instance;
		if ( inst is null )
			return;

		inst._significantSavePending = false;
		inst._significantSaveDueTime = 0;
		inst.TryHostSaveNow( preserveTamesWhenSceneEmpty, forceSync );
	}

	/// <summary>Best-effort flush before process exit or scene teardown.</summary>
	public static void FlushBeforeExit()
	{
		if ( SavesSuppressed || !ThornsMultiplayer.IsHostOrOffline )
			return;

		RequestImmediateSave( forceSync: true, preserveTamesWhenSceneEmpty: true );
	}

	public static IDisposable BeginSaveSuppression()
	{
		SavesSuppressed = true;
		return new SaveSuppressionScope();
	}

	sealed class SaveSuppressionScope : IDisposable
	{
		public void Dispose() => SavesSuppressed = false;
	}

	/// <summary>Ensure the host save file is loaded before world restore/spawn.</summary>
	public static void EnsureHostReady( GameObject hostObject = null )
	{
		if ( !ThornsMultiplayer.ShouldRunHostPersistence || !Game.IsPlaying )
			return;

		if ( Instance is null || !Instance.IsValid() )
		{
			hostObject ??= Game.ActiveScene?.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject
			               ?? Game.ActiveScene?.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.GameObject;
			if ( hostObject is null || !hostObject.IsValid() )
				return;

			_ = hostObject.Components.Get<ThornsWorldPersistence>() ?? hostObject.Components.Create<ThornsWorldPersistence>();
		}

		Instance?.HostEnsureInitialized();
	}

	public ThornsPersistentWorldDto Live => _live;

	public static void SetPendingRelativeSavePath( string relativePath )
	{
		if ( string.IsNullOrWhiteSpace( relativePath ) )
			return;

		_pendingRelativeSavePath = relativePath.Trim().Replace( '\\', '/' );
	}

	public static void ClearPendingRelativeSavePath() => _pendingRelativeSavePath = null;

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( ThornsMultiplayer.IsHostOrOffline && Game.IsPlaying )
			FlushBeforeExit();

		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		HostEnsureInitialized();
	}

	protected override void OnDestroy()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			TryHostSaveNow( preserveTamesWhenSceneEmpty: true, forceSync: true );
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		ProcessPendingSave();
		ProcessSignificantSaveQueue();

		HostEnsureInitialized();
		if ( Time.Now < _nextAutoSaveTime )
			return;

		_nextAutoSaveTime = Time.Now + Math.Max( 5f, AutoSaveSeconds );
		TryHostSaveNow();
	}

	public void SyncGuildsToWorldService()
	{
		_live ??= new ThornsPersistentWorldDto();
		_live.Guilds ??= new List<ThornsPersistentGuildDto>();
		_live.AccountGuildIds ??= new Dictionary<string, string>();
		ThornsGuildWorldService.EnsureInstance()?.ImportFromSave( _live );
	}

	/// <summary>Keep in-memory <see cref="_live"/> guild rows aligned with the host guild service.</summary>
	public void HostSyncGuildStateToLive()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_live ??= new ThornsPersistentWorldDto();
		ThornsGuildWorldService.EnsureInstance()?.ExportToSave( _live );
	}

	/// <summary>Keep in-memory <see cref="_live"/> NPC rival rows aligned with the world service.</summary>
	public void HostSyncNpcGuildStateToLive( ThornsNpcGuildWorldService service = null )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		service ??= ThornsNpcGuildWorldService.Instance;
		if ( service is null || !service.IsValid() )
			return;

		_live ??= new ThornsPersistentWorldDto();
		service.ExportToSave( _live );
	}

	public void HostEnsureInitialized()
	{
		if ( !Game.IsPlaying || !ThornsMultiplayer.ShouldRunHostPersistence )
			return;

		if ( !string.IsNullOrEmpty( _pendingRelativeSavePath ) )
		{
			var pending = _pendingRelativeSavePath;
			_pendingRelativeSavePath = null;
			if ( !string.Equals( RelativeSavePath, pending, StringComparison.OrdinalIgnoreCase ) )
			{
				RelativeSavePath = pending;
				_initialized = false;
			}
		}

		if ( _initialized )
			return;

		_initialized = true;
		_structuresRestored = false;
		_buildingLootRestored = false;
		_worldResourcesRestored = false;
		ThornsAnimalManager.ResetPersistedTameRestoreGate();
		ThornsStructurePersistence.ResetRestoreGate();
		TryHostLoadDiskIntoLive();
		_nextAutoSaveTime = Time.Now + Math.Max( 5f, AutoSaveSeconds );
	}

	public bool TryGetPlayer( Connection connection, out ThornsPersistentPlayerDto dto )
	{
		dto = null;
		if ( connection is null )
			return false;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( connection );
		return !string.IsNullOrEmpty( key )
			&& _live?.PlayersByAccountKey is not null
			&& _live.PlayersByAccountKey.TryGetValue( key, out dto )
			&& dto is not null;
	}

	public bool TryGetPlayerByAccountKey( string accountKey, out ThornsPersistentPlayerDto dto )
	{
		dto = null;
		return !string.IsNullOrWhiteSpace( accountKey )
		       && _live?.PlayersByAccountKey is not null
		       && _live.PlayersByAccountKey.TryGetValue( accountKey, out dto )
		       && dto is not null;
	}

	public void HostSetBedSpawn( string accountKey, Vector3 worldPosition, float yawDegrees )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( accountKey ) )
			return;

		_live ??= new ThornsPersistentWorldDto();
		_live.PlayersByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerDto>();
		if ( !_live.PlayersByAccountKey.TryGetValue( accountKey, out var dto ) || dto is null )
		{
			dto = new ThornsPersistentPlayerDto();
			_live.PlayersByAccountKey[accountKey] = dto;
		}

		dto.HasBedSpawn = true;
		dto.BedSpawnX = worldPosition.x;
		dto.BedSpawnY = worldPosition.y;
		dto.BedSpawnZ = worldPosition.z;
		dto.BedSpawnYaw = yawDegrees;
		RequestSave();
	}

	public void HostOnPlayerDisconnected( Connection channel )
	{
		if ( !Networking.IsHost )
			return;

		HostCleanupSessionsForConnection( channel );
		HostCaptureConnection( channel );
		HostCaptureProgressForConnection( channel );
		TryHostSaveNow( preserveTamesWhenSceneEmpty: false, forceSync: true );
	}

	void HostCleanupSessionsForConnection( Connection channel )
	{
		if ( channel is null || Scene is null || !Scene.IsValid() )
			return;

		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || session.OwnerConnection?.Id != channel.Id )
				continue;

			var gameplay = session.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			gameplay?.HostCleanupOpenSessions();

			session.Components.Get<ThornsPlayerBuildingController>( FindMode.EnabledInSelf )?.ForceCloseBuildMode();
			return;
		}
	}

	public void TryHostSaveNow( bool preserveTamesWhenSceneEmpty = false, bool forceSync = false )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || SavesSuppressed || _loadFailed )
			return;

		if ( !forceSync && _saveWorkerBusy )
		{
			_saveQueued = true;
			_queuedPreserveTamesWhenSceneEmpty |= preserveTamesWhenSceneEmpty;
			return;
		}

		try
		{
			var dto = CloneSnapshotForSave( CaptureSnapshot( preserveTamesWhenSceneEmpty ) );
			if ( forceSync || !Game.IsPlaying )
			{
				WriteSnapshotSync( dto );
				return;
			}

			_pendingSaveDto = dto;
			_pendingSavePath = RelativeSavePath;
			_saveWorkerBusy = true;
			_pendingSaveWrite = true;
		}
		catch ( Exception e )
		{
			_saveWorkerBusy = false;
			Log.Warning( e, $"[Thorns Terrain] Persistence save failed for '{RelativeSavePath}'." );
		}
	}

	void ProcessSignificantSaveQueue()
	{
		if ( !_significantSavePending || Time.Now < _significantSaveDueTime )
			return;

		_significantSavePending = false;
		_significantSaveDueTime = 0;
		TryHostSaveNow();
	}

	void ProcessPendingSave()
	{
		if ( !_pendingSaveWrite )
			return;

		_pendingSaveWrite = false;
		var dto = _pendingSaveDto;
		var path = _pendingSavePath ?? RelativeSavePath;
		_pendingSaveDto = null;
		_pendingSavePath = null;

		try
		{
			if ( dto is null )
				return;

			if ( !ThornsSaveFormat.TrySave( path, dto, out var error ) )
				Log.Warning( $"[Thorns Terrain] Persistence save failed for '{path}': {error}" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns Terrain] Persistence save failed for '{path}'." );
		}
		finally
		{
			_saveWorkerBusy = false;

			if ( _saveQueued )
			{
				var preserveTames = _queuedPreserveTamesWhenSceneEmpty;
				_saveQueued = false;
				_queuedPreserveTamesWhenSceneEmpty = false;
				TryHostSaveNow( preserveTamesWhenSceneEmpty: preserveTames );
			}
		}
	}

	void WriteSnapshotSync( ThornsPersistentWorldDto dto )
	{
		if ( !ThornsSaveFormat.TrySave( RelativeSavePath, dto, out var error ) )
			Log.Warning( $"[Thorns Terrain] Persistence save failed for '{RelativeSavePath}': {error}" );
		else
			Log.Info( $"[Thorns Terrain] Persistence wrote '{RelativeSavePath}' players={dto.PlayersByAccountKey.Count} progress={dto.PlayerProgressByAccountKey.Count} structures={dto.Structures?.Count ?? 0} furnitureContainers={dto.FurnitureContainers?.Count ?? 0} tames={dto.Tames?.Count ?? 0} guilds={dto.Guilds?.Count ?? 0}." );
	}

	void TryHostLoadDiskIntoLive()
	{
		using var _ = BeginSaveSuppression();

		var saveExisted = FileSystem.Data.FileExists( RelativeSavePath );

		if ( ThornsSaveFormat.TryLoad( RelativeSavePath, out var loaded, out var error ) )
		{
			_live = loaded;
			_loadFailed = false;
			_loadFailedError = "";
			_quarantinedSavePath = "";
			_freshWorldPendingSunrise = !saveExisted;
			SyncGuildsToWorldService();
			ThornsVictoryPersistence.RestoreHost();
			ThornsWorldMapPersistence.RestoreHost();
			Log.Info( $"[Thorns Terrain] Persistence loaded '{RelativeSavePath}' v{_live.Version} players={_live.PlayersByAccountKey.Count} progress={_live.PlayerProgressByAccountKey.Count} structures={_live.Structures?.Count ?? 0} resources={_live.DepletedTreeIds?.Count ?? 0}/{_live.DepletedMineralNodeIds?.Count ?? 0} storages={_live.StructureStorages?.Count ?? 0}." );
			return;
		}

		// Quarantine the unreadable primary so autosave cannot silently overwrite recoverable data.
		_loadFailed = true;
		_loadFailedError = string.IsNullOrWhiteSpace( error ) ? "deserialize failed" : error;
		_quarantinedSavePath = TryQuarantineUnreadableSave( RelativeSavePath );
		_live = ThornsSaveFormat.CreateEmpty();
		_freshWorldPendingSunrise = true;
		SyncGuildsToWorldService();
		Log.Error(
			$"[Thorns Terrain] Persistence load FAILED for '{RelativeSavePath}' ({_loadFailedError}). " +
			$"Autosave suppressed. Quarantined copy: '{( string.IsNullOrEmpty( _quarantinedSavePath ) ? "(none)" : _quarantinedSavePath )}'. " +
			"Host must choose recovery (new world) via AcknowledgeFailedLoadAndStartFresh before saves resume." );
	}

	string TryQuarantineUnreadableSave( string relativePath )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( relativePath ) )
				return "";

			var stamp = DateTime.UtcNow.ToString( "yyyyMMdd_HHmmss" );
			var quarantine = $"{relativePath}.corrupt.{stamp}";
			// World saves are JSON — copy as text to avoid Span/byte[] API mismatch.
			var text = FileSystem.Data.ReadAllText( relativePath );
			if ( string.IsNullOrEmpty( text ) )
				return "";

			FileSystem.Data.WriteAllText( quarantine, text );
			return quarantine;
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns Terrain] Failed to quarantine unreadable save '{relativePath}'." );
			return "";
		}
	}

	/// <summary>Host accepts starting a fresh world after a failed load; re-enables autosave.</summary>
	public void AcknowledgeFailedLoadAndStartFresh()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		_loadFailed = false;
		_loadFailedError = "";
		_live ??= ThornsSaveFormat.CreateEmpty();
		Log.Warning( "[Thorns Terrain] Host acknowledged failed load — starting fresh world and re-enabling saves." );
		TryHostSaveNow( forceSync: true );
	}

	public void HostApplyFreshWorldSunriseIfNeeded( Scene scene )
	{
		if ( !_freshWorldPendingSunrise || !ThornsMultiplayer.IsHostOrOffline || ThornsLightingTestSceneBootstrap.IsActive )
			return;

		if ( scene is null || !scene.IsValid() )
			return;

		ThornsEnvironmentDirector.EnsureInScene( scene );
		if ( !ThornsTimeOfDaySystem.TryGet( scene, out var time ) || !time.IsValid() )
			return;

		time.SetTimeHours( ThornsTimeOfDaySystem.FreshWorldSunriseHour, pauseAfterSet: false );
		_freshWorldPendingSunrise = false;
		Log.Info( $"[Thorns Environment] Fresh world started at sunrise ({ThornsTimeOfDaySystem.FreshWorldSunriseHour:F1}h)." );
	}

	public void HostRestoreStructuresOnce( Scene scene )
	{
		if ( _structuresRestored || !ThornsMultiplayer.IsHostOrOffline || scene is null )
			return;

		_structuresRestored = true;
		ThornsStructurePersistence.RestoreHost( scene, _live );
		ThornsWorldMapPersistence.RestoreHost();
		ThornsVictoryPersistence.RestoreHost();
	}

	public void HostRestoreDeathCratesOnce()
	{
		if ( _deathCratesRestored || !ThornsMultiplayer.IsHostOrOffline )
			return;

		_deathCratesRestored = true;
		ThornsDeathCratePersistence.RestoreHost();
	}

	public void HostRestoreBuildingLootOnce()
	{
		if ( _buildingLootRestored || !ThornsMultiplayer.IsHostOrOffline )
			return;

		_buildingLootRestored = true;
		ThornsBuildingLootPersistence.RestoreHost();
	}

	public void HostRestoreWorldResourcesOnce()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _worldResourcesRestored )
			return;

		_worldResourcesRestored = true;
		ThornsWorldResourcePersistence.RestoreHost();
	}

	static ThornsPersistentWorldDto CloneSnapshotForSave( ThornsPersistentWorldDto source )
	{
		if ( source is null )
			return ThornsSaveFormat.CreateEmpty();

		return Json.Deserialize<ThornsPersistentWorldDto>( Json.Serialize( source ) )
		       ?? ThornsSaveFormat.CreateEmpty();
	}

	ThornsPersistentWorldDto CaptureSnapshot( bool preserveTamesWhenSceneEmpty )
	{
		_live ??= new ThornsPersistentWorldDto();
		_live.PlayersByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerDto>();

		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() )
				continue;

			var key = session.HostPersistenceAccountKey;
			if ( string.IsNullOrEmpty( key ) && session.OwnerConnection is not null )
				key = ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );

			if ( string.IsNullOrEmpty( key ) )
				continue;

			if ( session.GameObject is not { IsValid: true } )
				continue;

			_live.PlayersByAccountKey.TryGetValue( key, out var existing );
			var captured = CapturePlayerDto( session.GameObject, session.OwnerConnection );
			CopyRespawnAnchor( existing, captured );
			_live.PlayersByAccountKey[key] = captured;
		}

		ThornsWorldTamePersistence.CaptureTames( _live, forceReplace: !preserveTamesWhenSceneEmpty );
		ThornsGuildWorldService.Instance?.ExportToSave( _live );
		ThornsNpcGuildWorldService.Instance?.ExportToSave( _live );
		ThornsVictoryPersistence.Capture( _live );
		ThornsStructurePersistence.Capture( _live, Scene, forceReplace: !preserveTamesWhenSceneEmpty );
		ThornsBuildingLootPersistence.Capture( _live );
		ThornsWorldResourcePersistence.Capture( _live );
		ThornsWorldMapPersistence.Capture( _live );
		ThornsDeathCratePersistence.Capture( _live );

		foreach ( var gameplay in Scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( gameplay.IsValid() && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
				ThornsSaveFormat.CapturePlayerProgress( _live, gameplay );
		}

		return _live;
	}

	public IReadOnlyList<ThornsPersistentTameDto> GetSavedTames()
	{
		_live ??= new ThornsPersistentWorldDto();
		_live.Tames ??= new List<ThornsPersistentTameDto>();
		return _live.Tames;
	}

	void HostCaptureConnection( Connection channel )
	{
		if ( channel is null )
			return;

		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || session.OwnerConnection?.Id != channel.Id )
				continue;

			var key = session.HostPersistenceAccountKey;
			if ( string.IsNullOrEmpty( key ) )
				key = ThornsPersistenceIdentity.GetStableAccountKey( channel );

			if ( !string.IsNullOrEmpty( key ) && session.GameObject is { IsValid: true } )
			{
				_live.PlayersByAccountKey.TryGetValue( key, out var existing );
				var captured = CapturePlayerDto( session.GameObject, channel );
				CopyRespawnAnchor( existing, captured );
				_live.PlayersByAccountKey[key] = captured;
			}

			return;
		}
	}

	void HostCaptureProgressForConnection( Connection channel )
	{
		if ( channel is null )
			return;

		foreach ( var session in Scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || session.OwnerConnection?.Id != channel.Id )
				continue;

			var gameplay = session.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			if ( gameplay.IsValid() && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
				ThornsSaveFormat.CapturePlayerProgress( _live, gameplay );

			return;
		}
	}

	static ThornsPersistentPlayerDto CapturePlayerDto( GameObject player, Connection connection )
	{
		if ( player is null || !player.IsValid() )
		{
			return new ThornsPersistentPlayerDto
			{
				DisplayName = connection?.DisplayName ?? "",
				LastSeenUtcIso = DateTime.UtcNow.ToString( "o" ),
			};
		}

		var t = player.WorldTransform;
		var angles = t.Rotation.Angles();
		return new ThornsPersistentPlayerDto
		{
			DisplayName = connection?.DisplayName ?? "",
			LastSeenUtcIso = DateTime.UtcNow.ToString( "o" ),
			Px = t.Position.x,
			Py = t.Position.y,
			Pz = t.Position.z,
			RPitch = angles.pitch,
			RYaw = angles.yaw,
			RRoll = angles.roll
		};
	}

	static void CopyRespawnAnchor( ThornsPersistentPlayerDto from, ThornsPersistentPlayerDto to )
	{
		if ( from is null || to is null || !from.HasBedSpawn )
			return;

		to.HasBedSpawn = true;
		to.BedSpawnX = from.BedSpawnX;
		to.BedSpawnY = from.BedSpawnY;
		to.BedSpawnZ = from.BedSpawnZ;
		to.BedSpawnYaw = from.BedSpawnYaw;
	}
}
