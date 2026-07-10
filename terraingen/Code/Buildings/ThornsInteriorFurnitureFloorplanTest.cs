namespace Terraingen.Buildings;

using Terraingen;
using Terraingen.AI;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.World;

/// <summary>
/// Dev gallery: one 3×3 proc building per <see cref="ThornsProcBuildingType"/> with scripted interior furniture.
/// Open <see cref="ScenePath"/> and press Play.
/// </summary>
[Title( "Thorns Interior Furniture Floorplan Test" )]
[Category( "Terrain/Buildings" )]
[Icon( "home" )]
public sealed class ThornsInteriorFurnitureFloorplanTest : Component
{
	public const string ScenePath = "scenes/thorns_interior_furniture_floorplan_test.scene";

	[Property, Group( "Floor" )] public string FloorModelPath { get; set; } = "models/dev/box.vmdl";

	[Property, Group( "Floor" )] public Vector3 FloorScale { get; set; } = new( 3200f, 2800f, 4f );

	[Property, Group( "Gallery" )] public Vector3 GalleryOrigin { get; set; } = new( 0f, 0f, 64f );

	[Property, Group( "Gallery" )] public float BuildingSpacing { get; set; } = 380f;

	[Property, Group( "Gallery" )] public int GridColumns { get; set; } = 4;

	[Property, Group( "Gallery" )] public int LayoutVariantIndex { get; set; } = ThornsProcBuildingSpawnDefaults.LayoutVariantIndex;

	[Property, Group( "Gallery" )] public int GalleryStories { get; set; } = ThornsProcBuildingSpawnDefaults.Stories;

	[Property, Group( "Gallery" )] public float FoundationDepthInches { get; set; } = ThornsProcBuildingSpawnDefaults.MinFoundationDepthInches;

	[Property, Group( "Gallery" )] public string DevBoxModel { get; set; } = "models/dev/box.vmdl";

	[Property, Group( "Gallery" )] public Vector3 PlayerSpawnOffset { get; set; } = new( -200f, 0f, 80f );

	[Property, Group( "Player" )] public string PlayerPrefabPath { get; set; } = "templates/gameobject/player controller.prefab";

	[Property, Group( "Player" )] public float WalkSpeedMultiplier { get; set; } = 2.5f;

	[Property] public bool RebuildOnStart { get; set; } = true;

	[Property] public bool LogLayoutsOnRebuild { get; set; } = true;

	// Logs [Thorns FurnitureMat] / [Thorns ModelLoad] for mount, vmat, png, overrides.
	[Property] public bool LogFurnitureMaterialDebug { get; set; } = true;

	[Property] public bool SpawnPlayerOnStart { get; set; } = true;

	[Property, Group( "Bandit Combat Test" )]
	public bool EnableBanditCombatTest { get; set; }

	[Property, Group( "Bandit Combat Test" )]
	public float BanditSpawnDelaySeconds { get; set; } = 3f;

	[Property, Group( "Bandit Combat Test" )]
	public int BanditCount { get; set; } = 3;

	[Property, Group( "Bandit Combat Test" )]
	public float BanditSpawnDistance { get; set; } = 320f;

	[Property, Group( "Bandit Combat Test" )]
	public float BanditGroupRadius { get; set; } = 140f;

	// When true, only WorkbenchBuildingType is spawned (faster review).
	[Property] public bool SingleBuildingWorkbench { get; set; }

	[Property] public ThornsProcBuildingType WorkbenchBuildingType { get; set; } = ThornsProcBuildingType.Store;

	GameObject _floor;
	GameObject _galleryRoot;
	ThornsProcBuildingShellSpawner _spawner;
	ThornsBuildingLootWorldService _lootService;
	GameObject _player;
	bool _banditsSpawned;
	TimeUntil _banditSpawnDelay;

	protected override void OnAwake()
	{
		ThornsCollisionDebug.EnsureOn( GameObject );

		if ( EnableBanditCombatTest )
		{
			ThornsBanditCombatTestScene.SetActive( true );
			ThornsBanditCombatTestScene.EnsureSceneInfrastructure( GameObject );
		}

		ThornsWorldLootContainerService.EnsureForScene( Scene );
		_lootService = Components.Get<ThornsBuildingLootWorldService>()
		               ?? Components.Create<ThornsBuildingLootWorldService>();
		_lootService.Clear();
	}

	protected override void OnDestroy()
	{
		if ( EnableBanditCombatTest && ThornsBanditCombatTestScene.IsActive )
			ThornsBanditCombatTestScene.SetActive( false );
	}

	protected override void OnFixedUpdate()
	{
		if ( !EnableBanditCombatTest || !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		if ( _banditsSpawned || !_banditSpawnDelay )
			return;

		if ( !_player.IsValid() )
			return;

		_banditsSpawned = true;
		ThornsBanditCombatTestScene.HostSpawnEncounterNearPlayer(
			Scene,
			_player,
			BanditCount,
			BanditSpawnDistance,
			BanditGroupRadius );
	}

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		if ( EnableBanditCombatTest )
		{
			SingleBuildingWorkbench = true;
			RebuildOnStart = true;
			Log.Info( "[Thorns FloorplanTest] Bandit combat test — flat floor, one building, M4 loadout, nearby bandits." );
		}

		SetFurnitureMaterialDebug( LogFurnitureMaterialDebug && !EnableBanditCombatTest );

		EnsureFloor();
		if ( RebuildOnStart )
			RebuildGallery();

		if ( SpawnPlayerOnStart )
			SpawnExplorerPlayer();

		if ( EnableBanditCombatTest && _player.IsValid() )
			_banditSpawnDelay = BanditSpawnDelaySeconds;
	}

	[Button( "Rebuild Gallery" )]
	public void RebuildGallery()
	{
		if ( !Game.IsPlaying || !GameObject.IsValid() )
			return;

		EnsureFloor();
		DestroyGallery();

		SetFurnitureMaterialDebug( LogFurnitureMaterialDebug );

		_spawner = new ThornsProcBuildingShellSpawner(
			Scene,
			DevBoxModel,
			debugLogging: LogFurnitureMaterialDebug,
			maxFurnitureDebugLogs: 32 );
		_spawner.ResetCounters();
		_lootService?.Clear();

		_galleryRoot = new GameObject( true, "FloorplanTest_Gallery" );
		_galleryRoot.Parent = GameObject;
		_galleryRoot.WorldPosition = GalleryOrigin;

		var placementRng = new Random( 42069 );
		var types = SingleBuildingWorkbench
			? new[] { WorkbenchBuildingType }
			: Enum.GetValues<ThornsProcBuildingType>();
		var totalProps = 0;
		var built = 0;

		if ( LogLayoutsOnRebuild )
			Log.Info( "[Thorns FloorplanTest] Scripted corner furniture per building type (3×3, up to 4 storeys)." );

		for ( var i = 0; i < types.Length; i++ )
		{
			var type = types[i];
			if ( !ThornsInteriorFurnitureAsciiLayouts.SupportsBuildingType( type ) )
				continue;

			try
			{
				var col = built % Math.Max( 1, GridColumns );
				var row = built / Math.Max( 1, GridColumns );
				var offset = new Vector3( col * BuildingSpacing, -row * BuildingSpacing, 0f );
				var worldPos = GalleryOrigin + offset;

				var result = _spawner.Spawn(
					new ThornsProcBuildingShellSpawner.Request(
						worldPos,
						Rotation.Identity,
						_galleryRoot,
						type,
						LayoutVariantIndex,
						GalleryStories,
						built,
						FoundationDepthInches,
						$"FloorplanTest_{type}",
						RegisterFootprint: false ),
					_lootService,
					placementRng );

				totalProps += result.PropsSpawned;
				built++;

				if ( LogLayoutsOnRebuild )
				{
					Log.Info(
						$"[Thorns FloorplanTest] === {type} v{LayoutVariantIndex} @ {worldPos} props={result.PropsSpawned} ===" );
					Log.Info( ThornsInteriorFurnitureAsciiLayouts.FormatVariant( type, LayoutVariantIndex ) );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( $"[Thorns FloorplanTest] Failed {type} v{LayoutVariantIndex}: {ex.Message}" );
			}
		}

		_lootService?.HostSyncFurnitureContainers();

		Log.Info(
			$"[Thorns FloorplanTest] Gallery: {built} buildings, {totalProps} props, "
			+ $"variant={LayoutVariantIndex}, stories={GalleryStories}, spacing={BuildingSpacing}, cols={GridColumns}, "
			+ $"workbench={SingleBuildingWorkbench}, furnitureFallbackBoxes={_spawner.FurnitureModelFallbacks}" );

		if ( LogFurnitureMaterialDebug )
		{
			Log.Info(
				"[Thorns FloorplanTest] Material debug: filter console for [Thorns FurnitureMat], [Thorns ModelLoad], PlaceableMat. "
				+ "Check pngFile/vmatFile=false and modelError=true first." );
		}
	}

	static void SetFurnitureMaterialDebug( bool enabled )
	{
		ThornsFurnitureMaterialDebug.Enabled = enabled;
		ThornsFurnitureMaterialDebug.MaxLogs = 64;
		ThornsFurnitureMaterialDebug.Reset();
		ThornsModelResourceLoad.VerboseLoadLogging = enabled;
		ThornsModelResourceLoad.ResetVerboseLoadLogCount();

		if ( enabled )
		{
			Log.Info(
				"[Thorns FloorplanTest] Furniture material debug ON — [Thorns FurnitureMat], [Thorns ModelLoad], PlaceableMat." );
		}
	}

	void DestroyGallery()
	{
		if ( _galleryRoot.IsValid() )
			_galleryRoot.Destroy();
		_galleryRoot = null;
	}

	void EnsureFloor()
	{
		if ( _floor.IsValid() )
			return;

		_spawner ??= new ThornsProcBuildingShellSpawner( Scene, DevBoxModel );
		_floor = _spawner.SpawnGalleryFloor( GameObject, GalleryOrigin, FloorScale, FloorModelPath );
	}

	void SpawnExplorerPlayer()
	{
		if ( _player.IsValid() )
			return;

		var prefab = ResolvePlayerPrefab();
		if ( !prefab.IsValid() )
		{
			Log.Error( "[Thorns FloorplanTest] Could not find player prefab for gallery walkthrough." );
			return;
		}

		var spawnPos = GalleryOrigin + PlayerSpawnOffset;
		_player = prefab.Clone( new Transform( spawnPos, Rotation.Identity ), name: "Floorplan Explorer" );
		if ( EnableBanditCombatTest )
			ConfigureCombatTestPlayer( _player );
		else
			ConfigurePlayer( _player );

		ThornsPlayerPresentationBootstrap.EnsureFirstPersonPresentation( _player );
		ThornsSceneObserver.FocusLocalPlayer( Scene, _player );
		ThornsIconCache.WarmGameplayIcons();

		if ( EnableBanditCombatTest )
		{
			ThornsBanditCombatTestScene.SyncLocalPlayerUi( _player );
			Log.Info( $"[Thorns FloorplanTest] Combat test player at {spawnPos} — bandits in {BanditSpawnDelaySeconds:0.#}s." );
		}
		else
		{
			Log.Info( $"[Thorns FloorplanTest] Spawned explorer at {spawnPos}" );
		}
	}

	GameObject ResolvePlayerPrefab()
	{
		var prefab = GameObject.GetPrefab( PlayerPrefabPath );
		return prefab.IsValid() ? prefab : null;
	}

	void ConfigurePlayer( GameObject player )
	{
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>()
		                 ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();
		controller.UseInputControls = true;
		controller.ThirdPerson = false;
		controller.UseCameraControls = true;
		controller.UseLookControls = true;

		if ( WalkSpeedMultiplier > 0f && Math.Abs( WalkSpeedMultiplier - 1f ) > 0.001f )
		{
			controller.WalkSpeed *= WalkSpeedMultiplier;
			controller.RunSpeed *= WalkSpeedMultiplier;
		}

		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerHealth>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerHealth>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerDamageReceiver>()
		    ?? player.Components.Create<Terraingen.Combat.ThornsPlayerDamageReceiver>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerRadioShopUse>()
		    ?? player.Components.Create<Terraingen.Combat.ThornsPlayerRadioShopUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerContainerUse>()
		    ?? player.Components.Create<Terraingen.Combat.ThornsPlayerContainerUse>();
		_ = player.Components.Get<ThornsPlayerGameplay>() ?? player.Components.Create<ThornsPlayerGameplay>();
	}

	void ConfigureCombatTestPlayer( GameObject player )
	{
		var controller = player.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>()
		                 ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();
		controller.UseInputControls = true;
		controller.ThirdPerson = false;
		controller.UseCameraControls = true;
		controller.UseLookControls = true;

		if ( WalkSpeedMultiplier > 0f && Math.Abs( WalkSpeedMultiplier - 1f ) > 0.001f )
		{
			controller.WalkSpeed *= WalkSpeedMultiplier;
			controller.RunSpeed *= WalkSpeedMultiplier;
		}

		ThornsTerrainExplorer.EnsureStandardGameplayComponents( player );
	}
}
