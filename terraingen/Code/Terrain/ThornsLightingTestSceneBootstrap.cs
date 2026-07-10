namespace Terraingen.TerrainGen;

using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Multiplayer;
using Terraingen.UI;

/// <summary>
/// Small terrain + sparse trees for <c>scenes/thorns_lighting_test.scene</c>.
/// No sun, sky, fog, time-of-day, or lighting — add those in the scene yourself.
/// </summary>
[Title( "Thorns Lighting Test Scene" )]
[Category( "Terrain" )]
[Icon( "landscape" )]
public sealed class ThornsLightingTestSceneBootstrap : Component
{
	// Linear scale of terrain footprint vs full world (0.32 ≈ 10% area).
	public const float TerrainLinearScale = 0.32f;

	public const int LightingTestWorldBuildVersion = 9001;

	public const float MovementSpeedMultiplier = 3f;

	// When true, world populate skips buildings, grass clutter, minerals, and MP lobby.
	public static bool IsActive { get; private set; }

	[Property, Group( "Terrain" ), Range( 0.1f, 0.5f ), Title( "Terrain linear scale" )]
	public float LinearScale { get; set; } = TerrainLinearScale;

	[Property, Group( "Trees" )]
	public bool SpawnCenterTreeRing { get; set; } = true;

	[Property, Group( "Trees" )]
	public int CenterTreeRingCount { get; set; } = 12;

	[Property, Group( "Trees" )]
	public bool SpawnGuaranteedTreesAtOrigin { get; set; } = true;

	[Property, Group( "Trees" ), Range( 0.05f, 1f )]
	public float TreeGlobalDensity { get; set; } = 0.38f;

	protected override void OnAwake()
	{
		IsActive = true;
		Apply();
	}

	protected override void OnDestroy()
	{
		if ( IsActive )
			IsActive = false;
	}

	void Apply()
	{
		var terrainBootstrap = Components.Get<ThornsTerrainBootstrap>( FindMode.EverythingInSelf );
		if ( terrainBootstrap is null || !terrainBootstrap.IsValid() )
		{
			Log.Warning( "[Thorns Lighting Test] No ThornsTerrainBootstrap on this object — scene not configured." );
			return;
		}

		ApplyTerrainConfig( terrainBootstrap.Config, LinearScale );
		ApplyFoliage( terrainBootstrap.Foliage );
		ApplyExplorer( terrainBootstrap.Explorer );
		DisableHeavySystems( terrainBootstrap );

		Log.Info(
			$"[Thorns Lighting Test] Scene ready — terrain linear scale {LinearScale:P0}, " +
			$"world res {terrainBootstrap.Config.TerrainWorldResolution}." );
	}

	static void ApplyTerrainConfig( ThornsTerrainConfig config, float linearScale )
	{
		if ( config is null )
			return;

		config.WorldBuildVersion = LightingTestWorldBuildVersion;
		config.TerrainWorldResolution = ScaleResolution( config.TerrainWorldResolution, linearScale );
		config.TerrainResolution = ScaleResolution( config.TerrainResolution, linearScale );
		config.GenerateOnStart = true;
		config.HostAuthoritative = true;
		config.ClientsGenerateDeterministic = false;
		config.FrameCameraOnGenerate = true;
		config.CenterAtWorldOrigin = true;
		config.CreateWaterSheet = true;
	}

	void ApplyFoliage( ThornsFoliageFoundation foliage )
	{
		if ( foliage is null || !foliage.IsValid() )
			return;

		var cfg = foliage.Config;
		cfg.SpawnOnTerrainReady = true;
		cfg.UseInstancedTrees = false;
		cfg.VerboseDebug = false;
		cfg.GlobalDensity = TreeGlobalDensity;
		cfg.MaxTreeClustersPerChunk = 3;
		cfg.MaxForestMassesPerChunk = 1;
		cfg.ChunkSizeInches = 4000f;
		cfg.ChunksPerFrame = 8;
		cfg.LimitSpawnToCenterRadius = true;
		cfg.SpawnRadiusFromCenterInches = 5500f;
		cfg.DebugForceCenterRing = SpawnCenterTreeRing;
		cfg.DebugCenterRingCount = Math.Max( 4, CenterTreeRingCount );
		cfg.SpawnGuaranteedTreesAtOrigin = SpawnGuaranteedTreesAtOrigin;
		cfg.CullDistanceInches = 28000f;
		cfg.TreeLodHideDistanceInches = 32000f;
	}

	static void ApplyExplorer( ThornsTerrainExplorer explorer )
	{
		if ( explorer is null || !explorer.IsValid() )
			return;

		explorer.SpawnOnTerrainReady = true;
		explorer.UseFixedSpawnPoint = false;
		explorer.UseSceneCameraForPlayer = true;
		explorer.FirstPerson = true;
		explorer.SpawnHeightOffset = 72f;
		explorer.WalkSpeedMultiplier = MovementSpeedMultiplier;
		explorer.SprintSpeedMultiplier = 1f;
	}

	static void DisableHeavySystems( ThornsTerrainBootstrap terrainBootstrap )
	{
		var net = terrainBootstrap.Components.Get<ThornsNetworkGameManager>();
		if ( net is not null && net.IsValid() )
			net.CreateLobbyOnLoad = false;

		var menuLauncher = terrainBootstrap.Components.Get<ThornsServerMenuLauncher>();
		if ( menuLauncher is not null && menuLauncher.IsValid() )
			menuLauncher.CreateUiOnStart = false;

		var minerals = terrainBootstrap.Minerals ?? terrainBootstrap.Components.Get<ThornsMineralFoundation>();
		if ( minerals is not null && minerals.IsValid() )
			minerals.Config.SpawnOnTerrainReady = false;

		var clutter = terrainBootstrap.Clutter ?? terrainBootstrap.Components.Get<ThornsClutterFoundation>();
		if ( clutter is not null && clutter.IsValid() )
			clutter.Config.Enabled = false;

		var grass = terrainBootstrap.ClientGrass ?? terrainBootstrap.Components.Get<ClientGrassRenderer>();
		if ( grass is not null && grass.IsValid() )
			grass.Enabled = false;
	}

	static int ScaleResolution( int fullResolution, float linearScale )
	{
		var scaled = (int)MathF.Round( fullResolution * linearScale );
		return Math.Max( 128, ThornsTerrainGenerator.RoundDownToPowerOfTwo( scaled ) );
	}
}
