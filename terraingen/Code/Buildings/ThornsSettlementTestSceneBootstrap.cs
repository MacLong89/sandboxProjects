namespace Terraingen.Buildings;

using System.Threading.Tasks;
using Terraingen.Animals;
using Terraingen.Buildings.Settlement;
using Terraingen.Combat;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Multiplayer;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.World;

/// <summary>
/// Flat gallery of every POI identity on block grids. Open <see cref="ScenePath"/> and press Play.
/// </summary>
[Title( "Thorns Settlement Test Scene" )]
[Category( "Terrain/Buildings" )]
[Icon( "location_city" )]
public sealed class ThornsSettlementTestSceneBootstrap : Component
{
	public const string ScenePath = "scenes/thorns_settlement_test.scene";

	public const int GalleryColumns = 4;
	public const float GallerySpacingInches = 5400f;
	public const float GalleryRowSpacingInches = 5200f;

	public static int GallerySettlementCount => ThornsPoiIdentityCatalog.GalleryIdentities.Count;

	public static bool IsActive { get; private set; }

	[Property] public int WorldSeed { get; set; } = 42069;

	[Property, Group( "Flat terrain" )]
	public int TerrainResolution { get; set; } = 256;

	[Property, Group( "Flat terrain" )]
	public int TerrainWorldResolution { get; set; } = 768;

	[Property, Group( "Flat terrain" )]
	public float FlatHeightNormalized { get; set; } = 0.07f;

	[Property, Group( "Movement test" )]
	public bool EnableMovementTest { get; set; } = true;

	[Property, Group( "Movement test" )]
	public int AnimalsPerPoi { get; set; } = 3;

	[Property, Group( "Movement test" )]
	public string TestSpeciesKey { get; set; } = ThornsSettlementAnimalTestScene.DefaultTestSpeciesKey;

	[Property, Group( "Movement test" )]
	public string PredatorTestSpeciesKey { get; set; } = "wolf";

	[Property, Group( "Movement test" )]
	public int PredatorsPerPoi { get; set; } = 1;

	[Property, Group( "Movement test" )]
	public bool EnablePredatorPreyBehavior { get; set; }

	[Property, Group( "Movement test" )]
	public bool ScatterObstacles { get; set; } = true;

	[Property, Group( "Movement test" )]
	public int TreesPerPoi { get; set; } = 5;

	[Property, Group( "Movement test" )]
	public int BouldersPerPoi { get; set; } = 3;

	ThornsTerrainConfig _terrainConfig;
	HeightmapField _field;
	Terrain _terrain;
	GameObject _worldBoundary;

	protected override void OnAwake()
	{
		IsActive = true;
		ThornsCollisionDebug.EnsureOn( GameObject );
		DisableProcTerrainBootstrap();
		StripWorld();
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !Game.IsPlaying )
			return;

		BuildFlatWorld();

		if ( EnableMovementTest && ThornsMultiplayer.IsHostOrOffline )
			_ = SetupMovementSandboxAsync();
		else
			LogGalleryReady( 0, 0 );
	}

	async Task SetupMovementSandboxAsync()
	{
		try
		{
			ThornsSettlementAnimalTestScene.EnsureFlatNavFloor( Scene, GameObject, _terrain );

			var obstacles = 0;
			if ( ScatterObstacles )
			{
				obstacles = ThornsSettlementTestObstacleScatter.HostScatterGalleryObstacles(
					Scene,
					GameObject,
					_terrain,
					WorldSeed,
					TreesPerPoi,
					BouldersPerPoi );
			}

			var manager = ThornsSettlementAnimalTestScene.EnsureAnimalManager(
				GameObject,
				ignoreAnimals: !EnablePredatorPreyBehavior );
			manager?.OnWorldReady( _terrain, _terrainConfig );
			var navReady = await ThornsSettlementAnimalTestScene.WaitForNavMeshReadyAsync( Scene );

			var animals = ThornsSettlementAnimalTestScene.HostSpawnGalleryAnimals(
				Scene,
				_terrain,
				TestSpeciesKey,
				AnimalsPerPoi,
				PredatorTestSpeciesKey,
				PredatorsPerPoi );

			LogGalleryReady( animals, obstacles, navReady );
		}
		catch ( Exception ex )
		{
			Log.Error( ex, "[Thorns Settlement Test] Movement sandbox setup failed." );
			LogGalleryReady( 0, 0 );
		}
	}

	void LogGalleryReady( int animals, int obstacles, bool navReady = false )
	{
		var movement = EnableMovementTest
			? $" Movement test: {animals} animal(s), {obstacles} obstacle(s), nav={(navReady ? "ready" : "fallback")}. Console: animal_debug 1."
			: "";
		Log.Info(
			$"[Thorns Settlement Test] POI gallery ({GallerySettlementCount} identities), seed={WorldSeed}.{movement}" );
	}

	protected override void OnDestroy()
	{
		if ( IsActive )
			IsActive = false;
	}

	/// <summary>XY offset from terrain center for gallery slot <paramref name="index"/>.</summary>
	public static Vector3 GetGalleryOffset( int index )
	{
		var cols = GalleryColumns;
		var row = index / cols;
		var col = index % cols;
		var rows = (GallerySettlementCount + cols - 1) / cols;
		var gridW = (cols - 1) * GallerySpacingInches;
		var gridH = (rows - 1) * GalleryRowSpacingInches;
		var x = -gridW * 0.5f + col * GallerySpacingInches;
		var y = gridH * 0.5f - row * GalleryRowSpacingInches;
		return new Vector3( x, y, 0f );
	}

	public static string DescribeGallerySlot( int index )
	{
		var identities = ThornsPoiIdentityCatalog.GalleryIdentities;
		if ( index < 0 || index >= identities.Count )
			return "?";

		var identity = identities[index];
		var def = ThornsPoiIdentityCatalog.Get( identity );
		return $"{def.DisplayName} ({def.GalleryBuildingCount} bld, max {def.MaxStories}F)";
	}

	void DisableProcTerrainBootstrap()
	{
		var terrainBootstrap = Components.Get<ThornsTerrainBootstrap>( FindMode.EverythingInSelf );
		if ( terrainBootstrap is not null && terrainBootstrap.IsValid() )
			terrainBootstrap.Enabled = false;
	}

	void BuildFlatWorld()
	{
		_terrainConfig = CreateFlatTerrainConfig();
		_field = CreateFlatHeightField( _terrainConfig, FlatHeightNormalized );
		_terrain = ApplyFlatTerrain( GameObject, _terrainConfig, _field );
		_worldBoundary = ThornsWorldBoundary.Sync( Scene, GameObject, _worldBoundary, _terrain );

		var buildings = Components.Get<ThornsWorldBuildingGenerator>()
		                ?? Components.Create<ThornsWorldBuildingGenerator>();
		ConfigureBuildings( buildings );
		buildings.Generate( _terrain, _field, _terrainConfig );

		var explorer = Components.Get<ThornsTerrainExplorer>();
		if ( explorer is not null && explorer.IsValid() )
		{
			explorer.SpawnOnTerrainReady = true;
			explorer.UseSceneCameraForPlayer = true;
			explorer.FirstPerson = true;
			explorer.WalkSpeedMultiplier = 2.5f;
			explorer.SpawnAtTerrainCenter( _terrain, _terrain.TerrainHeight );
		}

		TerrainPlacement.FramePreviewCamera( Scene, _terrain.TerrainSize, _terrain.TerrainHeight );
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

	void ConfigureBuildings( ThornsWorldBuildingGenerator buildings )
	{
		var cfg = buildings.Config ??= new ThornsProcBuildingConfig();
		cfg.Enabled = true;
		cfg.TownCount = GallerySettlementCount;
		cfg.MicroTownsEnabled = false;
		cfg.StreetFirstSettlements = true;
		cfg.PaintDirtPaths = true;
		cfg.RouteDirtPathsByElevation = false;
		cfg.DebugLogging = true;
		cfg.SettlementDebugOverlay = true;
		cfg.ProcBuildingStories = ThornsProcBuildingSpawnDefaults.MaxStories;
		SettlementDebugOverlay.Enabled = true;
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
