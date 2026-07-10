namespace Terraingen.Buildings;

using System.Threading.Tasks;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.World;

/// <summary>
/// Flat mob sandbox — bandits and wolves on one arena. Open <see cref="ScenePath"/> and press Play.
/// </summary>
[Title( "Thorns Mob Test Scene" )]
[Category( "Terrain/AI" )]
[Icon( "groups" )]
public sealed class ThornsBanditTestSceneBootstrap : Component
{
	public const string ScenePath = "scenes/thorns_bandit_test.scene";

	public static bool IsActive { get; private set; }

	[Property] public int WorldSeed { get; set; } = 42069;

	[Property, Group( "Flat terrain" )]
	public int TerrainResolution { get; set; } = 256;

	[Property, Group( "Flat terrain" )]
	public int TerrainWorldResolution { get; set; } = 768;

	[Property, Group( "Flat terrain" )]
	public float FlatHeightNormalized { get; set; } = 0.07f;

	[Property, Group( "Bandits" )]
	public int GroupCount { get; set; } = 2;

	[Property, Group( "Bandits" )]
	public int BanditsPerGroup { get; set; } = 5;

	[Property, Group( "Bandits" )]
	public float GroupDistanceFromCenter { get; set; } = 2400f;

	[Property, Group( "Bandits" )]
	public bool UseWandererArchetype { get; set; }

	[Property, Group( "Animals" )]
	public bool SpawnAnimals { get; set; } = true;

	[Property, Group( "Animals" )]
	public int AnimalGroupCount { get; set; } = 4;

	[Property, Group( "Animals" )]
	public float AnimalGroupDistanceFromCenter { get; set; } = 1500f;

	[Property, Group( "Animals" )]
	public int DeerPerGroup { get; set; }

	[Property, Group( "Animals" )]
	public int WolvesPerGroup { get; set; } = 6;

	[Property, Group( "Animals" )]
	public bool SpawnPanther { get; set; }

	[Property, Group( "Animals" )]
	public bool SpawnMoose { get; set; }

	[Property, Group( "Bow test" )]
	public bool BowTestMode { get; set; }

	[Property, Group( "Bow test" )]
	public bool SpawnTargetBandits { get; set; } = true;

	[Property, Group( "Bow test" )]
	public int TargetBanditCount { get; set; } = 4;

	[Property, Group( "Bow test" )]
	public float TargetDistanceFromSpawn { get; set; } = 1800f;

	Terrain _terrain;
	GameObject _worldBoundary;
	ThornsTerrainConfig _terrainConfig;

	protected override void OnAwake()
	{
		IsActive = true;
		if ( BowTestMode )
		{
			ThornsBowTestScene.SetActive( true );
			ThornsBowTestScene.EnsureSceneInfrastructure( GameObject );
		}
		else
		{
			ThornsBanditCombatTestScene.SetActive( true );
			ThornsBanditCombatTestScene.EnsureSceneInfrastructure( GameObject );
		}

		StripWorld();
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !Game.IsPlaying || !ThornsMultiplayer.IsHostOrOffline )
			return;

		BuildFlatWorld();
		if ( BowTestMode )
		{
			_ = ThornsBowTestScene.SetupArenaAsync(
				Scene,
				_terrain,
				SpawnTargetBandits,
				TargetBanditCount,
				TargetDistanceFromSpawn );
			return;
		}

		_ = SetupMobArenaAsync();
	}

	async Task SetupMobArenaAsync()
	{
		try
		{
			var bandits = SpawnBanditGroups();
			var animals = 0;
			var navReady = false;

			if ( SpawnAnimals )
			{
				ThornsMobTestScene.EnsureFlatNavFloor( Scene, GameObject, _terrain );
				var manager = ThornsMobTestScene.EnsureAnimalManager( GameObject );
				manager?.OnWorldReady( _terrain, _terrainConfig );
				navReady = await ThornsMobTestScene.WaitForNavMeshReadyAsync( Scene );
				animals = ThornsMobTestScene.HostSpawnArenaAnimals(
					Scene,
					_terrain,
					AnimalGroupCount,
					AnimalGroupDistanceFromCenter,
					DeerPerGroup,
					WolvesPerGroup,
					SpawnPanther,
					SpawnMoose );
			}

			var player = ThornsSceneObserver.FindLocalPlayerObject( Scene );
			if ( player.IsValid() )
				ThornsBanditCombatTestScene.SyncLocalPlayerUi( player );

			Log.Info(
				$"[Thorns Mob Test] Arena ready — {bandits} bandit(s) ({GroupCount}×{BanditsPerGroup}, " +
				$"{(UseWandererArchetype ? "wanderer" : "defender")}), {animals} wolf(s), " +
				$"nav={(navReady ? "ready" : "fallback")}. Tame food in inventory. Console: animal_debug 1, bandit_debug 1." );
		}
		catch ( Exception ex )
		{
			Log.Error( ex, "[Thorns Mob Test] Arena setup failed." );
		}
	}

	protected override void OnDestroy()
	{
		if ( !IsActive )
			return;

		IsActive = false;
		if ( ThornsBowTestScene.IsActive )
			ThornsBowTestScene.SetActive( false );
		if ( ThornsBanditCombatTestScene.IsActive )
			ThornsBanditCombatTestScene.SetActive( false );
	}

	void BuildFlatWorld()
	{
		_terrainConfig = CreateFlatTerrainConfig();
		var field = CreateFlatHeightField( _terrainConfig, FlatHeightNormalized );
		_terrain = ApplyFlatTerrain( GameObject, _terrainConfig, field );
		_worldBoundary = ThornsWorldBoundary.Sync( Scene, GameObject, _worldBoundary, _terrain );

		var explorer = Components.Get<ThornsTerrainExplorer>();
		if ( explorer is not null && explorer.IsValid() )
		{
			explorer.SpawnOnTerrainReady = true;
			explorer.UseSceneCameraForPlayer = true;
			explorer.FirstPerson = true;
			explorer.WalkSpeedMultiplier = BowTestMode ? 1.5f : 2.5f;
			explorer.SpawnAtTerrainCenter( _terrain, _terrain.TerrainHeight );
		}

		TerrainPlacement.FramePreviewCamera( Scene, _terrain.TerrainSize, _terrain.TerrainHeight );
	}

	int SpawnBanditGroups()
	{
		if ( !_terrain.IsValid() )
			return 0;

		var groups = Math.Max( 1, GroupCount );
		var perGroup = Math.Max( 1, BanditsPerGroup );
		var distance = Math.Max( 400f, GroupDistanceFromCenter );
		var totalSpawned = 0;

		for ( var g = 0; g < groups; g++ )
		{
			var angleDeg = groups == 1 ? 0f : (360f / groups) * g;
			var rad = angleDeg * MathF.PI / 180f;
			var anchor = SampleFlatSurface(
				_terrain,
				MathF.Cos( rad ) * distance,
				MathF.Sin( rad ) * distance );
			var groupId = Game.Random.Int( 1, 2_000_000_000 );
			totalSpawned += ThornsBanditCombatTestScene.HostSpawnBanditGroup(
				Scene,
				anchor,
				perGroup,
				groupId,
				UseWandererArchetype );
		}

		return totalSpawned;
	}

	static Vector3 SampleFlatSurface( Terrain terrain, float x, float y )
	{
		var rayStart = new Vector3( x, y, terrain.TerrainHeight * 2f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, terrain.TerrainHeight * 4f, out var localHit ) )
			return terrain.GameObject.WorldTransform.PointToWorld( localHit );

		return new Vector3( x, y, terrain.TerrainHeight * 0.35f );
	}

	ThornsTerrainConfig CreateFlatTerrainConfig()
	{
		return new ThornsTerrainConfig
		{
			WorldSeed = WorldSeed,
			TerrainResolution = TerrainResolution,
			TerrainWorldResolution = TerrainWorldResolution,
			WorldScaleInches = 39f,
			MaxTerrainHeightInches = 12000f,
			SeaLevelNormalized = 0.06f,
			HorizontalScale = 1f,
			CenterAtWorldOrigin = true,
			CreateWaterSheet = false,
			FrameCameraOnGenerate = false,
			GenerateOnStart = false
		};
	}

	static HeightmapField CreateFlatHeightField( ThornsTerrainConfig config, float normalizedHeight )
	{
		var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainResolution );
		var height = Math.Clamp( normalizedHeight, 0f, 1f );
		var field = new HeightmapField( resolution, resolution );
		for ( var i = 0; i < field.Heights.Length; i++ )
			field.Heights[i] = height;
		return field;
	}

	static Terrain ApplyFlatTerrain( GameObject host, ThornsTerrainConfig config, HeightmapField field )
	{
		ThornsWorldPersistence.EnsureHostReady( host );

		var terrain = host.Components.Get<Terrain>( FindMode.EverythingInSelfAndChildren );
		if ( !terrain.IsValid() )
		{
			terrain = host.Components.Create<Terrain>();
			terrain.Storage = new TerrainStorage();
		}

		var storage = terrain.Storage ?? new TerrainStorage();
		var resolution = ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainResolution );
		storage.SetResolution( resolution );
		var worldResolution = Math.Max(
			resolution,
			ThornsTerrainGenerator.RoundDownToPowerOfTwo( config.TerrainWorldResolution ) );
		storage.TerrainSize = worldResolution * config.WorldScaleInches * config.HorizontalScale;
		storage.TerrainHeight = config.MaxTerrainHeightInches;

		TerrainMaterialLibrary.PopulateMaterials( storage, config );
		storage.HeightMap = ThornsTerrainGenerator.ToTerrainHeightmap( field );
		TerrainMaterialPainter.InitializeDefaultControlMap( storage );
		if ( storage.Materials.Count > 0 )
			TerrainMaterialPainter.PaintControlMap( storage, field, config );

		terrain.Storage = storage;
		terrain.Create();
		terrain.UpdateMaterialsBuffer();
		terrain.SyncGPUTexture();
		ThornsTerrainCache.Register( terrain, config );

		TerrainPlacement.ApplyOriginOffset( host, storage.TerrainSize, config.CenterAtWorldOrigin );
		return terrain;
	}

	void StripWorld()
	{
		var net = Components.Get<ThornsNetworkGameManager>();
		if ( net is not null && net.IsValid() )
			net.CreateLobbyOnLoad = false;

		var menu = Components.Get<ThornsServerMenuLauncher>();
		if ( menu is not null && menu.IsValid() )
			menu.CreateUiOnStart = false;

		var foliage = Components.Get<ThornsFoliageFoundation>();
		if ( foliage is not null && foliage.IsValid() )
			foliage.Enabled = false;

		var minerals = Components.Get<ThornsMineralFoundation>();
		if ( minerals is not null && minerals.IsValid() )
			minerals.Enabled = false;
	}
}
