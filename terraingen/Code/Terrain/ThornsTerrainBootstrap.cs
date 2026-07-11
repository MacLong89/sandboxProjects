namespace Terraingen.TerrainGen;

using Sandbox;
using Terraingen;
using Terraingen.Boulders;
using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Multiplayer;
using Terraingen.Animals;
using Terraingen.AI;
using Terraingen.Buildings;
using Terraingen.World;
using Terraingen.World.Environment;
using Terraingen.Rendering;
using Terraingen.Combat;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;
using Terraingen.NpcGuild;

/// <summary>
/// Server-authoritative terrain bootstrap with async sculpt, disk cache, and MP height sync.
/// </summary>
[Title( "Thorns Terrain Bootstrap" )]
[Category( "Terrain" )]
[Icon( "landscape" )]
public sealed class ThornsTerrainBootstrap : Component
{
	[Property] public ThornsTerrainConfig Config { get; set; } = new();

	[Property] public ThornsTerrainExplorer Explorer { get; set; }

	[Property] public ThornsFoliageFoundation Foliage { get; set; }

	[Property] public ThornsMineralFoundation Minerals { get; set; }

	[Property] public ThornsClutterFoundation Clutter { get; set; }

	[Property] public ThornsBoulderFoundation Boulders { get; set; }

	[Property] public ClientGrassRenderer ClientGrass { get; set; }

	Terrain _terrain;
	GameObject _waterSheet;
	GameObject _worldBoundary;
	ThornsTerrainAsyncGenerator _asyncGenerator;
	ThornsWorldHeightCacheRpc _cacheRpc;
	HeightmapField _lastField;
	bool _worldReady;
	bool _waitingForLobbyWorld;
	HeightmapField _deferredCosmeticsField;
	bool _cosmeticsDeferred;
	bool _localPlayerCosmeticsNotified;
	bool _pendingLocalPlayerNotify;
	int _cosmeticsPipelineStep;
	Vector3 _cosmeticsPlayerPos;

	public bool IsWorldApplied => _worldReady;

	public static ThornsTerrainBootstrap Instance { get; private set; }

	public Terrain WorldTerrain => _terrain;

	public GameObject WaterSheetObject => _waterSheet;

	/// <summary>Normalized heightfield used for the live terrain (map UI, etc.).</summary>
	public HeightmapField GetHeightFieldForMap() => _worldReady ? _lastField : null;
	TimeUntil _clientCacheRequestDelay;

	protected override void OnAwake()
	{
		if ( Instance is null || !Instance.IsValid() )
			Instance = this;

		ClearBrokenComponentRefs();

		if ( !Scene.IsEditor && Game.IsPlaying && !ThornsLightingTestSceneBootstrap.IsActive )
		{
			ThornsGameplayUiHost.EnsureOnHost( GameObject );
			ThornsPublishedAssetValidation.LogBootValidation( "terrain bootstrap" );
		}
	}

	protected override void OnStart()
	{
		if ( !Scene.IsEditor && Game.IsPlaying && !ThornsLightingTestSceneBootstrap.IsActive )
		{
			ThornsCombatFeedbackHost.EnsureOn( GameObject );
			ThornsCollisionDebug.EnsureOn( GameObject );
			ThornsBoulderCollisionTuning.EnsureOn( GameObject );
			ThornsLocalCameraGuard.EnsureOn( GameObject );
			_ = Components.Get<ThornsWorldAmbience>() ?? Components.Create<ThornsWorldAmbience>();
			_ = Components.Get<ThornsAtmosphericMusic>() ?? Components.Create<ThornsAtmosphericMusic>();
			_ = Components.Get<ThornsGameplayEnterAudioFade>() ?? Components.Create<ThornsGameplayEnterAudioFade>();
		}

		if ( Scene.IsEditor || !Config.GenerateOnStart )
			return;

		if ( !ThornsLightingTestSceneBootstrap.IsActive )
			ThornsEnvironmentDirector.EnsureGameplayEnvironment( Scene );

		var netManager = Scene.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault();
		_cacheRpc = netManager?.EnsureHeightCacheRpc( this );
		_ = netManager?.EnsureBuildingSyncRpc( this );

		ThornsWorldSession.TryReadFromLobby();
		ThornsWorldSession.ApplyConfig( Config );

		// Joining clients load the gameplay scene before Connect. Do not apply a local
		// height cache / sculpt with the default seed — wait for the host lobby seed.
		if ( ShouldWaitForHostWorld() )
		{
			ThornsWorldBootGate.BeginLocalBoot();
			_waitingForLobbyWorld = true;
			_clientCacheRequestDelay = 0.35f;
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.LoadingWorld );
			ThornsLoadingScreenUtil.Show( "Waiting for host world..." );
			return;
		}

		if ( CanUseLocalHeightCache() && ThornsTerrainHeightCache.TryLoad( Config, out var cached ) )
		{
			ThornsWorldBootGate.BeginLocalBoot();
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.GeneratingTerrain );
			ApplyCachedField( cached );
			ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncCharacter );
			return;
		}

		if ( ShouldRunSculpt() )
			BeginAsyncGenerate();
		else
		{
			ThornsWorldBootGate.BeginLocalBoot();
			_waitingForLobbyWorld = true;
			_clientCacheRequestDelay = 0.35f;
		}
	}

	static bool CanUseLocalHeightCache()
	{
		if ( ThornsMultiplayer.IsRemoteJoinClient )
			return false;

		return true;
	}

	bool ShouldWaitForHostWorld()
	{
		if ( ThornsMultiplayer.IsRemoteJoinClient && !_worldReady )
			return true;

		if ( ThornsWorldSession.WorldReady )
			return false;

		if ( ThornsSessionBootstrap.IsJoiningRemoteLobby )
			return true;

		return Networking.IsActive && !Networking.IsHost;
	}

	protected override void OnUpdate()
	{
		try
		{
			if ( _cosmeticsPipelineStep > 0 )
				TickLocalCosmeticsPipeline();

			if ( _asyncGenerator is not null )
				TickAsyncGenerate();

			if ( !_waitingForLobbyWorld )
				return;

			ThornsWorldSession.TryReadFromLobby();
			ThornsWorldSession.ApplyConfig( Config );

			if ( _worldReady && Config.WorldSeed != ThornsWorldSession.WorldSeed )
			{
				ThornsJoinFlowDebug.JoinWarn(
					$"Lobby seed changed ({Config.WorldSeed} → {ThornsWorldSession.WorldSeed}) — re-applying terrain." );
				_worldReady = false;
			}

			if ( !ThornsWorldSession.WorldReady
			     && ThornsSessionBootstrap.IsJoiningRemoteLobby
			     && !(Networking.IsActive && !Networking.IsHost) )
			{
				// Still offline waiting for Connect — keep holding.
				return;
			}

			if ( !ThornsWorldSession.WorldReady && Networking.IsActive && !Networking.IsHost )
				return;

			if ( CanUseLocalHeightCache() && ThornsTerrainHeightCache.TryLoad( Config, out var cached ) )
			{
				_waitingForLobbyWorld = false;
				ApplyCachedField( cached );
				return;
			}

			if ( ShouldRunSculpt() )
			{
				_waitingForLobbyWorld = false;
				BeginAsyncGenerate();
				return;
			}

			if ( _clientCacheRequestDelay )
				return;

			_clientCacheRequestDelay = 2f;
			_cacheRpc?.ClientRequestIfNeeded();
		}
		catch ( Exception ex )
		{
			Log.Error( ex, "[Thorns Terrain] Bootstrap Update failed." );
		}
	}

	bool ShouldRunSculpt()
	{
		if ( ThornsMultiplayer.IsRemoteJoinClient )
			return false;

		if ( Config.ClientsGenerateDeterministic && Networking.IsActive && !Networking.IsHost )
			return true;

		return ThornsMultiplayer.ShouldHostSculptTerrain( Config.HostAuthoritative );
	}

	void BeginAsyncGenerate()
	{
		ThornsWorldBootGate.BeginLocalBoot();
		_asyncGenerator = new ThornsTerrainAsyncGenerator();
		_asyncGenerator.Begin( Config );
		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.GeneratingTerrain );
		ThornsLoadingScreenUtil.Show( ThornsMenuJoinFlow.StageLabel( ThornsMenuJoinStage.GeneratingTerrain ) );
	}

	void TickAsyncGenerate()
	{
		_asyncGenerator.Tick();
		if ( !_asyncGenerator.IsComplete )
			return;

		ThornsMenuJoinFlow.SetStage( ThornsMenuJoinStage.SyncCharacter );
		var generator = _asyncGenerator;
		_asyncGenerator = null;

		if ( !generator.Succeeded )
		{
			Log.Error( $"[Thorns Terrain] Async generation failed: {generator.Error}" );
			return;
		}

		FinishGeneratedField( generator.Result );
	}

	void FinishGeneratedField( HeightmapField field )
	{
		_lastField = field;
		ThornsTerrainHeightCache.Save( Config, field );
		_cacheRpc?.HostSetSourceField( field );

		ApplyCachedField( field );
	}

	public void ApplyCachedField( HeightmapField field )
	{
		if ( field is null )
			return;

		if ( _worldReady && _lastField is not null && Config is not null
		     && Config.WorldSeed == ThornsWorldSession.WorldSeed )
			return;

		_lastField = field;
		ApplyToTerrain( field );
		_worldReady = true;
		_waitingForLobbyWorld = false;
		ThornsNaturalWaterDrink.InvalidateBootstrapCache();

		if ( Networking.IsHost || !Networking.IsActive )
			ThornsWorldSession.PublishFromHost( Config );
	}

	void ApplyToTerrain( HeightmapField field )
	{
		ThornsWorldPersistence.EnsureHostReady( GameObject );

		_terrain ??= GameObject.Components.Get<Terrain>( FindMode.EverythingInSelfAndChildren );
		if ( !_terrain.IsValid() )
		{
			_terrain = GameObject.Components.Create<Terrain>();
			_terrain.Storage = new TerrainStorage();
		}

		var storage = _terrain.Storage ?? new TerrainStorage();
		var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( Config.TerrainResolution );

		storage.SetResolution( resolution );
		var worldResolution = Math.Max( resolution, ThornsTerrainGenerator.RoundDownToPowerOfTwo( Config.TerrainWorldResolution ) );
		storage.TerrainSize = worldResolution * Config.WorldScaleInches * Config.HorizontalScale;
		storage.TerrainHeight = Config.MaxTerrainHeightInches;

		TerrainMaterialLibrary.PopulateMaterials( storage, Config );

		storage.HeightMap = ThornsTerrainGenerator.ToTerrainHeightmap( field );

		TerrainMaterialPainter.InitializeDefaultControlMap( storage );
		if ( storage.Materials.Count > 0 )
			TerrainMaterialPainter.PaintControlMap( storage, field, Config );

		_terrain.Storage = storage;
		_terrain.Create();
		_terrain.UpdateMaterialsBuffer();
		_terrain.SyncGPUTexture();
		ThornsTerrainCache.Register( _terrain, Config );

		var terrainWorldSize = storage.TerrainSize;
		TerrainPlacement.ApplyOriginOffset( GameObject, terrainWorldSize, Config.CenterAtWorldOrigin );

		if ( Config.FrameCameraOnGenerate && !Networking.IsActive )
			TerrainPlacement.FramePreviewCamera( Scene, terrainWorldSize, storage.TerrainHeight );

		_waterSheet = ThornsWaterSheet.Sync( Scene, GameObject, _waterSheet, Config, terrainWorldSize, storage.TerrainHeight, _terrain, field );
		_worldBoundary = ThornsWorldBoundary.Sync( Scene, GameObject, _worldBoundary, _terrain );

		var explorer = Explorer ?? GameObject.Components.Get<ThornsTerrainExplorer>();
		if ( !Networking.IsActive || ThornsMinimalTestSceneBootstrap.IsActive )
			explorer?.SpawnAtTerrainCenter( _terrain, storage.TerrainHeight );

		ThornsWorldLootContainerService.EnsureForScene( Scene );
		Terraingen.Audio.ThornsAudioWorldService.EnsureForScene( Scene );
		ThornsGameplaySfx.WarmHarvestToolSounds();
		ThornsCombatTracerWorldService.EnsureForScene( Scene );
		ThornsCombatHitFxWorldService.EnsureForScene( Scene );
		QueueOrPopulateCosmetics( field );
		ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();

		if ( !Networking.IsActive )
			QueueLocalPlayerCosmetics();

		TryFlushPendingLocalPlayerCosmetics();
		Log.Info( "[Thorns Terrain] World apply complete." );
		ThornsWorldBootGate.NotifyWorldApplied( Scene );

		foreach ( var net in Scene.GetAllComponents<ThornsNetworkGameManager>() )
			net.FlushPendingPlayerSpawns();
		NotifyAnimalSystems( _terrain );
		NotifyAirdropSystems( _terrain );
		NotifyBloomSeedSystems( _terrain );
		NotifyDeathCrateSystems( _terrain );
		ThornsWorldPersistence.Instance?.HostRestoreStructuresOnce( Scene );
		ThornsAnimalManager.Instance?.RequestStructureAwareNavRebake( _terrain );
		ThornsWorldPersistence.Instance?.HostRestoreBuildingLootOnce();
		ThornsWorldLootContainerService.Instance?.HostResyncStructureStorages( Scene );
		ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();
		NotifyMapSystems();
		ThornsEnvironmentDirector.EnsureGameplayEnvironment( Scene );
		ThornsWorldPersistence.Instance?.HostApplyFreshWorldSunriseIfNeeded( Scene );
	}

	void NotifyAirdropSystems( Terrain terrain )
	{
		if ( ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		var service = Scene.GetAllComponents<ThornsAirdropWorldService>().FirstOrDefault()
		              ?? GameObject.Components.Create<ThornsAirdropWorldService>();
		service?.OnWorldReady( terrain, Config );
	}

	void NotifyBloomSeedSystems( Terrain terrain )
	{
		if ( ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		ThornsBloomSeedWorldService.EnsureOnHost( GameObject )?.OnWorldReady( terrain, Config );
	}

	void NotifyDeathCrateSystems( Terrain terrain )
	{
		var service = Scene.GetAllComponents<ThornsDeathCrateWorldService>().FirstOrDefault()
		              ?? GameObject.Components.Create<ThornsDeathCrateWorldService>();
		service?.OnWorldReady( terrain );
	}

	void NotifyMapSystems()
	{
		foreach ( var map in Scene.GetAllComponents<ThornsMapWorldService>() )
			map.NotifyTerrainReady();
	}

	void NotifyAnimalSystems( Terrain terrain )
	{
		if ( ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		var manager = Scene.GetAllComponents<ThornsAnimalManager>().FirstOrDefault()
		              ?? GameObject.Components.Get<ThornsAnimalManager>()
		              ?? GameObject.Components.Create<ThornsAnimalManager>();
		manager?.OnWorldReady( terrain, Config );

		var bandits = Scene.GetAllComponents<ThornsBanditManager>().FirstOrDefault()
		              ?? GameObject.Components.Get<ThornsBanditManager>()
		              ?? GameObject.Components.Create<ThornsBanditManager>();
		bandits?.OnWorldReady( terrain, Config );

		NotifyNpcGuildSystems( terrain );
	}

	void NotifyNpcGuildSystems( Terrain terrain )
	{
		if ( ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		_ = ThornsGuildWorldService.EnsureInstance();

		ThornsNpcGuildWorldService.EnsureInstance()?.OnWorldReady( terrain, Config );
	}

	void QueueOrPopulateCosmetics( HeightmapField field )
	{
		if ( field is null )
			return;

		if ( ShouldDeferCosmeticsUntilPlayer() )
		{
			_deferredCosmeticsField = field;
			_cosmeticsDeferred = true;
			Log.Info( "[Thorns Terrain] Deferred foliage/minerals/grass until local player spawn." );
			return;
		}

		PopulateWorldCosmetics( field );
	}

	bool ShouldDeferCosmeticsUntilPlayer()
	{
		if ( !Config.DeferCosmeticsUntilLocalPlayer )
			return false;

		return Networking.IsActive && !Networking.IsHost;
	}

	void PopulateWorldCosmetics( HeightmapField field )
	{
		try
		{
			PopulateWorldCosmeticsCore( field );
		}
		catch ( Exception ex )
		{
			Log.Error( ex, "[Thorns Terrain] World cosmetics populate failed." );
		}
	}

	void PopulateWorldCosmeticsCore( HeightmapField field )
	{
		var populateStructures = ThornsMultiplayer.ShouldPopulateWorldStructures( Config.HostAuthoritative );
		var populateVisuals = ThornsMultiplayer.ShouldPopulateVisualCosmetics( Config.HostAuthoritative, Config.ClientsGenerateDeterministic );

		if ( !populateStructures && !populateVisuals )
			return;

		var foliage = Foliage ?? GameObject.Components.Get<ThornsFoliageFoundation>();
		var clutter = Clutter ?? GameObject.Components.Get<ThornsClutterFoundation>();
		var boulders = Boulders ?? GameObject.Components.Get<ThornsBoulderFoundation>();
		var foliageConfig = foliage?.Config ?? new ThornsFoliageConfig();

		if ( populateStructures && !ThornsMinimalTestSceneBootstrap.IsActive )
		{
			ThornsWorldScatterFootprintRegistry.Clear();
			ThornsWorldVisualLodService.EnsureForScene( Scene );
			var buildings = GameObject.Components.Get<ThornsWorldBuildingGenerator>()
			                ?? GameObject.Components.Create<ThornsWorldBuildingGenerator>();
			Log.Info( $"[Thorns Terrain] Populating world buildings on '{GameObject.Name}' terrain={_terrain.IsValid()} seed={Config.WorldSeed}." );
			try
			{
				buildings.Generate( _terrain, field, Config );
				ThornsWorldBuildingSyncRpc.NotifyHostGenerated( buildings );
			}
			catch ( Exception ex )
			{
				Log.Error( ex, "[Thorns Terrain] World building generation failed — continuing foliage/minerals/grass." );
			}
		}

		if ( !populateVisuals )
		{
			RepairRuntimeWorldShadows( "cosmetics-populate" );
			return;
		}

		var sharedSampler = new ThornsFoliageBiomeSampler( field, _terrain, Config, foliageConfig );

		if ( populateStructures )
		{
			boulders ??= GameObject.Components.Create<ThornsBoulderFoundation>();
			boulders.BeginPopulate( _terrain, field, Config, sharedSampler );
		}

		foliage?.BeginPopulate( _terrain, field, Config, sharedSampler );

		ResolveMinerals().BeginPopulate( _terrain, field, Config, foliageConfig, sharedSampler );

		if ( clutter is not null )
			clutter.Config ??= new ThornsClutterConfig();

		var grassConfig = clutter?.Config ?? new ThornsClutterConfig { WorldSeed = Config.WorldSeed };
		grassConfig.WorldSeed = Config.WorldSeed;

		var clientGrass = ClientGrass ?? GameObject.Components.Get<ClientGrassRenderer>()
		                  ?? GameObject.Components.Create<ClientGrassRenderer>();
		if ( grassConfig.Enabled && grassConfig.UseGpuGrassBlades )
			clientGrass.BeginStreaming( _terrain, field, Config, grassConfig, sharedSampler );
		else
			clientGrass.Clear();

		if ( clutter is not null && grassConfig.Enabled
		     && (grassConfig.PlaceRocks || grassConfig.PlaceGrass || grassConfig.PlaceCloudDetail) )
			clutter.BeginStreaming( _terrain, field, Config, sharedSampler );
		else
			clutter?.RequestStop();

		RepairRuntimeWorldShadows( "cosmetics-populate" );
	}

	public void Generate()
	{
		try
		{
			ThornsWorldBootGate.BeginLocalBoot();
			ThornsLoadingScreenUtil.Show( "Sculpting Thorns Terrain…" );
			var field = ThornsTerrainGenerator.GenerateHeightField( Config );
			FinishGeneratedField( field );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Thorns Terrain] Generation failed: {e.Message}" );
		}
	}

	/// <summary>Host listen-server entry (spawn) and offline mode. Joining clients use <see cref="QueueLocalPlayerCosmetics"/> from their pawn.</summary>
	public void NotifyLocalPlayerSpawned() => QueueLocalPlayerCosmetics();

	/// <summary>
	/// Populate deferred client cosmetics once the local pawn exists. Safe to call multiple times;
	/// no-ops after success unless terrain was not ready yet.
	/// </summary>
	public bool RequestLocalPlayerCosmetics() => QueueLocalPlayerCosmetics();

	/// <summary>Queue one cosmetics pipeline step per frame (avoids re-entrant camera/grass work during spawn).</summary>
	public bool QueueLocalPlayerCosmetics()
	{
		if ( _localPlayerCosmeticsNotified && !_cosmeticsDeferred )
			return true;

		if ( !_worldReady )
		{
			_pendingLocalPlayerNotify = true;
			Log.Info( "[Thorns Terrain] Local player ready; deferring cosmetics until terrain apply completes." );
			return false;
		}

		if ( _cosmeticsPipelineStep > 0 )
			return true;

		_cosmeticsPipelineStep = 1;
		Log.Info( "[Thorns Terrain] Local cosmetics: queued (frame pipeline)." );
		return true;
	}

	void TryFlushPendingLocalPlayerCosmetics()
	{
		if ( !_pendingLocalPlayerNotify && !( _cosmeticsDeferred && LocalPlayerExists() ) )
			return;

		if ( !LocalPlayerExists() )
			return;

		QueueLocalPlayerCosmetics();
	}

	bool LocalPlayerExists() => ThornsSceneObserver.FindLocalPlayerObject( Scene ).IsValid();

	void TickLocalCosmeticsPipeline()
	{
		try
		{
			switch ( _cosmeticsPipelineStep )
			{
				case 1:
					Log.Info( "[Thorns Terrain] Local cosmetics: step 1/5 resolve player." );
					var player = ThornsSceneObserver.FindLocalPlayerObject( Scene );
					_cosmeticsPlayerPos = player.IsValid() ? player.WorldPosition : Vector3.Zero;
					Log.Info( $"[Thorns Terrain] Local cosmetics: player={( player.IsValid() ? player.Name : "none" )} pos={_cosmeticsPlayerPos}." );
					_cosmeticsPipelineStep = 2;
					return;
				case 2:
					if ( _cosmeticsDeferred && _deferredCosmeticsField is not null )
					{
						Log.Info( "[Thorns Terrain] Local cosmetics: step 2/5 deferred world populate." );
						PopulateWorldCosmetics( _deferredCosmeticsField );
						_cosmeticsDeferred = false;
						_deferredCosmeticsField = null;
					}
					else
					{
						Log.Info( "[Thorns Terrain] Local cosmetics: step 2/5 skip deferred populate." );
					}

					_cosmeticsPipelineStep = 3;
					return;
				case 3:
					Log.Info( "[Thorns Terrain] Local cosmetics: step 3/5 grass observer refresh." );
					var grass = ClientGrass ?? GameObject.Components.Get<ClientGrassRenderer>();
					var clutterConfig = Clutter?.Config ?? new ThornsClutterConfig { WorldSeed = Config.WorldSeed };
					if ( clutterConfig.UseGpuGrassBlades )
						grass?.ForceObserverRefresh();
					else
						grass?.Clear();
					_cosmeticsPipelineStep = 4;
					return;
				case 4:
					Log.Info( "[Thorns Terrain] Local cosmetics: step 4/5 shadows + foliage/minerals." );
					RepairRuntimeWorldShadows( "local-player-spawn" );

					var foliage = Foliage ?? GameObject.Components.Get<ThornsFoliageFoundation>();
					if ( _cosmeticsPlayerPos != Vector3.Zero )
						foliage?.OnLocalPlayerReady( _cosmeticsPlayerPos );

					if ( _cosmeticsPlayerPos != Vector3.Zero )
						ResolveMinerals().OnLocalPlayerReady( _cosmeticsPlayerPos );

					_cosmeticsPipelineStep = 5;
					return;
				case 5:
					Log.Info( "[Thorns Terrain] Local cosmetics: step 5/5 mineral pocket scatter." );
					ResolveMinerals().TryScatterPlayerPocket();
					_localPlayerCosmeticsNotified = true;
					_pendingLocalPlayerNotify = false;
					_cosmeticsPipelineStep = 0;
					Log.Info( "[Thorns Terrain] Local cosmetics: complete." );
					return;
			}
		}
		catch ( Exception ex )
		{
			Log.Error( ex, $"[Thorns Terrain] Local cosmetics failed at step {_cosmeticsPipelineStep}." );
			_cosmeticsPipelineStep = 0;
		}
	}

	void RepairRuntimeWorldShadows( string reason )
	{
		var stats = ThornsWorldShadowUtil.RepairSceneWorldShadows( Scene );
		if ( stats.Enabled > 0 )
		{
			Log.Info(
				$"[Thorns Shadows] Repaired {stats.Enabled} renderer(s) after {reason} " +
				$"(scanned={stats.Scanned}, alreadyOn={stats.AlreadyOn}, skipped={stats.Skipped})." );
		}
	}

	ThornsMineralFoundation ResolveMinerals()
	{
		if ( Minerals is not null && Minerals.IsValid() )
			return Minerals;

		Minerals = GameObject.Components.Get<ThornsMineralFoundation>();
		if ( Minerals is not null && Minerals.IsValid() )
			return Minerals;

		Minerals = GameObject.Components.Create<ThornsMineralFoundation>();
		return Minerals;
	}

	/// <summary>Guaranteed punchable stone/ore near spawn — run as soon as the local pawn exists.</summary>
	public void EnsureMineralPocketNearPlayer( Vector3 worldPosition )
	{
		if ( worldPosition == Vector3.Zero )
			return;

		var minerals = ResolveMinerals();
		if ( !minerals.IsValid() )
			return;

		minerals.ScatterPlayerPocket( worldPosition );
		minerals.OnLocalPlayerReady( worldPosition );
	}

	void ClearBrokenComponentRefs()
	{
		if ( Explorer is not null && !Explorer.IsValid() )
			Explorer = null;
		if ( Foliage is not null && !Foliage.IsValid() )
			Foliage = null;
		if ( Minerals is not null && !Minerals.IsValid() )
			Minerals = null;
		if ( Clutter is not null && !Clutter.IsValid() )
			Clutter = null;
		if ( Boulders is not null && !Boulders.IsValid() )
			Boulders = null;
		if ( ClientGrass is not null && !ClientGrass.IsValid() )
			ClientGrass = null;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}
}
