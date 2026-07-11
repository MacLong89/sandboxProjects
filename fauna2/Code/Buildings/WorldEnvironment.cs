namespace Fauna2;



/// <summary>
/// Client-local pixel-art world: tiled ground, cliff borders, and scatter props.
/// </summary>

public sealed class WorldEnvironment : Component
{
	public static WorldEnvironment Instance { get; private set; }

	private readonly List<GameObject> _baseSpawned = new();
	private readonly List<GameObject> _ownedGrass = new();
	private readonly HashSet<string> _grassPlotKeys = new();

	private int _baseBiomeHash = int.MinValue;
	private const int GroundBuildVersion = 46;
	private int _groundBuildVersion;

	private TimeUntil _nextCheck;

	private bool _legacyGroundHidden;
	private bool _wasGameStarted;
	private bool _sessionBootstrapPending;
	private bool _grassRefreshPending;

	private TimeUntil _renderDumpDelay;

	private bool _renderDumpPending;



	protected override void OnAwake()
	{
		Instance = this;
		GameEvents.ZooReset += OnZooReset;
		Fauna2Debug.Info( "World", "WorldEnvironment OnAwake" );
	}

	private void OnZooReset()
	{
		ClearWorld();
		_baseBiomeHash = int.MinValue;
		_groundBuildVersion = 0;
		_wasGameStarted = false;
		_sessionBootstrapPending = false;
	}

	protected override void OnDestroy()
	{
		GameEvents.ZooReset -= OnZooReset;
		if ( Instance == this ) Instance = null;
		ClearWorld();
	}

	protected override void OnStart()
	{
		Fauna2Debug.Info( "World", $"OnStart gameStarted={GameManager.Instance?.GameStarted}" );
	}

	protected override void OnUpdate()
	{
		if ( GameManager.Instance?.GameStarted != true )
			return;

		GroundVisibilityCuller.Tick();
	}

	protected override void OnFixedUpdate()

	{

		if ( _renderDumpPending && !_renderDumpDelay )
		{
			_renderDumpPending = false;
			if ( Fauna2Debug.Enabled )
				Fauna2RenderDiagnostics.DumpScene( Scene, "post-rebuild" );
		}

		var gameStarted = GameManager.Instance?.GameStarted == true;
		if ( gameStarted && !_wasGameStarted )
		{
			_wasGameStarted = true;
			if ( !UI.UiState.SessionLoading )
				UI.UiState.BeginSessionLoading();
			_sessionBootstrapPending = true;
			_nextCheck = 0f;
		}
		else if ( !gameStarted )
		{
			_wasGameStarted = false;
			_sessionBootstrapPending = false;
		}

		if ( _sessionBootstrapPending )
			_nextCheck = 0f;

		if ( !_nextCheck ) return;

		_nextCheck = _sessionBootstrapPending ? 0.05f : 0.5f;



		if ( GameManager.Instance is null || !GameManager.Instance.GameStarted )
		{
			return;
		}

		var state = ZooState.Instance;
		var plots = PlotSystem.Instance;
		if ( !state.IsValid() || !plots.IsValid() )
		{
			Fauna2Debug.Warn( "World", $"waiting for zoo core state={Fmt(state)} plots={Fmt(plots)}" );
			return;
		}



		var biomeHash = (int)state.StarterBiome;
		var rebuilt = false;

		if ( biomeHash != _baseBiomeHash || _groundBuildVersion != GroundBuildVersion || _sessionBootstrapPending )
		{
			Fauna2Debug.Info( "World", $"base rebuild biome={state.StarterBiome}" );
			ClearWorld();
			RebuildBase( state.StarterBiome, plots );
			_baseBiomeHash = biomeHash;
			_groundBuildVersion = GroundBuildVersion;
			ScheduleRenderDump();
			rebuilt = true;
		}

		SyncOwnedGrass( state.StarterBiome, plots );
		RefreshGrassUnderHabitats();

		if ( rebuilt )
		{
			if ( _sessionBootstrapPending )
			{
				_sessionBootstrapPending = false;
				UI.UiState.NotifyWorldReady();
			}

			RefreshVisibilityTargets();
		}
		else if ( _grassRefreshPending )
		{
			_grassRefreshPending = false;
			RefreshVisibilityTargets();
		}

		GroundDiagnostics.TickViewportProbe();
	}

	/// <summary>Force an immediate ground rebuild when a session starts.</summary>
	public void BootstrapSessionWorld()
	{
		_sessionBootstrapPending = true;
		_nextCheck = 0f;
	}

	private void RefreshVisibilityTargets()
	{
		var targets = new List<GameObject>( _baseSpawned.Count + _ownedGrass.Count );
		foreach ( var go in _baseSpawned )
		{
			if ( go.IsValid() && go.Tags.Has( "ground" ) && WorldSprites.GetGroundSpriteRenderer( go ).IsValid() )
				targets.Add( go );
		}

		foreach ( var go in _ownedGrass )
		{
			if ( go.IsValid() && go.Tags.Has( "ground" ) && WorldSprites.GetGroundSpriteRenderer( go ).IsValid() )
				targets.Add( go );
		}

		GroundDiagnostics.AuditGroundList( "pre-cull-targets", targets );
		GroundVisibilityCuller.SetTargets( targets );
		GroundVisibilityCuller.ApplyInitialVisibility();
	}

	internal IEnumerable<GameObject> EnumerateGroundObjects()
	{
		foreach ( var go in _baseSpawned )
		{
			if ( go.IsValid() )
				yield return go;
		}

		foreach ( var go in _ownedGrass )
		{
			if ( go.IsValid() )
				yield return go;
		}
	}

	/// <summary>Hide owned grass under a habitat interior so biome floor can replace it.</summary>
	public int HideOwnedGrassInRect( Vector3 center, Vector2 size )
	{
		if ( size.x <= 0f || size.y <= 0f )
			return 0;

		var habitatHalf = size * 0.5f;
		var grassHalf = GroundGrid.BaseDrawSize * 0.5f;
		var hidden = 0;
		var overlapping = 0;

		foreach ( var go in _ownedGrass )
		{
			if ( !go.IsValid() || !go.Name.StartsWith( "Owned", StringComparison.Ordinal ) )
				continue;

			var pos = go.WorldPosition;
			if ( MathF.Abs( pos.x - center.x ) >= habitatHalf.x + grassHalf
			     || MathF.Abs( pos.y - center.y ) >= habitatHalf.y + grassHalf )
				continue;

			overlapping++;
			if ( !go.Enabled )
				continue;

			go.Enabled = false;
			hidden++;
		}

		if ( Fauna2Debug.Enabled )
			Fauna2Debug.Info( "World", $"hid owned grass under habitat at ({center.x:0.##},{center.y:0.##}): {hidden}/{overlapping} overlapping tiles" );

		return hidden;
	}

	/// <summary>Re-hide owned grass under every habitat after plot grass is rebuilt.</summary>
	public void RefreshGrassUnderHabitats()
	{
		var total = 0;
		foreach ( var habitat in HabitatRegistry.All )
		{
			if ( habitat is null || !habitat.GameObject.IsValid() )
				continue;

			var footprint = HabitatSizing.EffectiveFootprint( habitat.Size );
			total += HideOwnedGrassInRect( habitat.GameObject.WorldPosition, footprint );
		}

		if ( total > 0 && Fauna2Debug.Enabled )
			Fauna2Debug.Info( "World", $"refreshed habitat grass hide: {total} tiles across {HabitatRegistry.Count} habitats" );
	}

	private void ClearWorld()
	{
		foreach ( var go in _baseSpawned ) go?.Destroy();
		foreach ( var go in _ownedGrass ) go?.Destroy();
		_baseSpawned.Clear();
		_ownedGrass.Clear();
		_grassPlotKeys.Clear();
		GroundVisibilityCuller.ClearTargets();
	}

	private void SyncOwnedGrass( Biome biome, PlotSystem plots )
	{
		var tile = GameConstants.GroundRenderTileSize;
		var playTile = PlayableTile( biome );

		foreach ( var key in plots.OwnedPlots )
		{
			if ( !_grassPlotKeys.Add( key ) ) continue;

			var removed = TileGridOverlay.RemoveWildernessGroundUnderPlot( _baseSpawned, key );
			var added = TileGridOverlay.SpawnOwnedPlotGround(
				_ownedGrass,
				key,
				playTile,
				tile,
				"Owned" );

			if ( Fauna2Debug.Enabled && (added > 0 || removed > 0) )
				Fauna2Debug.Info( "World", $"grass overlay plot={key} sprites={added} removedWilderness={removed}" );

			if ( added > 0 )
				_grassRefreshPending = true;
		}
	}



	private void ScheduleRenderDump()
	{
		_renderDumpPending = true;
		_renderDumpDelay = 2f;
	}



	private void RebuildBase( Biome biome, PlotSystem plots )

	{
		if ( Fauna2Debug.Enabled )
			Log.Info( $"[Fauna2 World] Base rebuild: biome={biome}, worldHalf={GameConstants.WorldHalfExtent:0.##}, groundChunk={GameConstants.GroundRenderTileSize:0.##}." );

		HideLegacyGround();
		GroundDiagnostics.ResetSession();

		var worldHalf = GameConstants.WorldHalfExtent;
		var playableHalf = GameConstants.PlayableHalfExtent;
		var tile = GameConstants.GroundRenderTileSize;

		GroundDiagnostics.LogTileManifest();

		TileGridOverlay.SpawnWildernessBiomeGrid( _baseSpawned, worldHalf, tile, "Wilderness", WorldSprites.WildernessLayer, plots, biome );
		GroundDiagnostics.LogBiomeDistribution( biome, plots, worldHalf, tile );
		Log.Info( $"[Fauna2 World] Wilderness ground rebuilt (v{GroundBuildVersion}): tileSize={tile:0.##}, objects={_baseSpawned.Count}." );
		GroundDiagnostics.AuditGroundList( "post-rebuild", _baseSpawned );

		SpawnGroundCollider( worldHalf );
		BuildCliffRing( worldHalf, tile );
		BuildWorldBorderColliders( worldHalf, GameConstants.PlotSize * 0.35f );

		var scatterProps = ScatterDecorations( biome, plots, playableHalf, worldHalf );
		if ( Fauna2Debug.Enabled )
			Log.Info( $"[Fauna2 World] Base rebuild complete: baseObjects={_baseSpawned.Count}, scatterProps={scatterProps}." );

	}



	private void HideLegacyGround()

	{

		if ( _legacyGroundHidden ) return;



		foreach ( var go in Scene.GetAllObjects( true ) )

		{

			if ( go.Name != "Ground" ) continue;

			go.Enabled = false;

			_legacyGroundHidden = true;
			Log.Info( "[Fauna2 World] Disabled legacy scene Ground object so sprite tiles are visible." );

			return;

		}

		Log.Warning( "[Fauna2 World] No legacy scene Ground object found to hide." );
	}



	private static Texture PlayableTile( Biome biome ) => biome switch
	{
		Biome.Rainforest => PixelArt.TileRainforest,
		Biome.Forest => PixelArt.TileForest,
		Biome.Grassland => PixelArt.TileGrass,
		Biome.Desert => PixelArt.TileSand,
		Biome.Arctic => PixelArt.TileSnow,
		Biome.Swamp => PixelArt.TileMud,
		Biome.Alpine => PixelArt.TileRock,
		Biome.Coastal => PixelArt.TileBeach,
		_ => PixelArt.TileGrass,
	};

	private void SpawnGroundCollider( float halfExtent )
	{
		var go = new GameObject( true, "Ground Collider" );

		go.WorldPosition = Vector3.Zero;

		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = new Vector3( halfExtent * 2f, halfExtent * 2f, 8f );
		collider.Static = true;

		_baseSpawned.Add( go );
	}



	private void BuildCliffRing( float worldHalf, float tileSize )

	{

		var thickness = GameConstants.PlotSize * 0.35f;

		var cliffSize = tileSize * 0.95f;



		for ( var x = -worldHalf - thickness; x <= worldHalf + thickness; x += tileSize )

		{

			SpawnCliffTile( new Vector3( x, worldHalf + thickness * 0.5f, 1f ), cliffSize );

			SpawnCliffTile( new Vector3( x, -worldHalf - thickness * 0.5f, 1f ), cliffSize );

		}



		for ( var y = -worldHalf; y <= worldHalf; y += tileSize )

		{

			SpawnCliffTile( new Vector3( worldHalf + thickness * 0.5f, y, 1f ), cliffSize );

			SpawnCliffTile( new Vector3( -worldHalf - thickness * 0.5f, y, 1f ), cliffSize );

		}

	}



	private void SpawnCliffTile( Vector3 position, float size )

	{

		var go = WorldSprites.SpawnWorld(
			position,
			PixelArt.TileCliff,
			size * PixelArt.TileCoverage,
			"World Cliff",
			localZ: WorldSprites.WildernessLayer,
			depthSort: false,
			layer: WorldSprites.WildernessLayer,
			sourcePixels: PixelArt.TileSourcePixels );

		go.Tags.Add( "world_border" );

		_baseSpawned.Add( go );

	}



	private void BuildWorldBorderColliders( float worldHalf, float thickness )

	{

		var depth = 96f;
		var outer = worldHalf + thickness;
		var span = outer * 2f + thickness;

		SpawnBorderCollider( new Vector3( 0f, outer, depth * 0.5f ), new Vector3( span, thickness, depth ), "Border North" );
		SpawnBorderCollider( new Vector3( 0f, -outer, depth * 0.5f ), new Vector3( span, thickness, depth ), "Border South" );
		SpawnBorderCollider( new Vector3( outer, 0f, depth * 0.5f ), new Vector3( thickness, worldHalf * 2f, depth ), "Border East" );
		SpawnBorderCollider( new Vector3( -outer, 0f, depth * 0.5f ), new Vector3( thickness, worldHalf * 2f, depth ), "Border West" );

	}



	private void SpawnBorderCollider( Vector3 center, Vector3 scale, string name )

	{

		var go = new GameObject( true, name );
		go.WorldPosition = center;
		go.Tags.Add( "world_border" );
		go.Tags.Add( "walk_block" );

		var collider = go.AddComponent<BoxCollider>();
		collider.Scale = scale;
		collider.Static = true;

		_baseSpawned.Add( go );

	}



	private int ScatterDecorations( Biome starterBiome, PlotSystem plots, float playableHalf, float worldHalf )

	{
		var scatterProps = 0;
		var saveSlot = SaveSystem.Instance?.ActiveSlotId ?? GameManager.Instance?.ActiveSaveSlot ?? 1;
		var rng = new Random( HashCode.Combine( (int)starterBiome, saveSlot, 91823 ) );

		var cell = GameConstants.TileSize * 16f;

		var min = -(int)(worldHalf / cell);

		var max = (int)(worldHalf / cell);



		for ( var gx = min; gx <= max; gx++ )

		{

			for ( var gy = min; gy <= max; gy++ )

			{

				var center = new Vector3( gx * cell + cell * 0.5f, gy * cell + cell * 0.5f, 0f );

				if ( center.Length > worldHalf * 0.98f ) continue;

				if ( IsInsideOwnedPlot( center, plots, GameConstants.TileSize * 6f ) ) continue;

				var onPurchasableLand = IsInsidePurchasablePlotGrid( center, GameConstants.TileSize * 6f );

				var roll = rng.NextSingle();

				var pos = center + Jitter( rng, GameConstants.TileSize * 5f );

				var regionalBiome = WildernessBiomeMap.BiomeAtWorld( center, starterBiome );
				var treeDensity = WildernessBiomeMap.TreeDensityAtWorld( center, starterBiome );

				scatterProps += ScatterCell( rng, pos, roll, regionalBiome, treeDensity, onPurchasableLand, starterBiome );

			}

		}

		return scatterProps;

	}

	/// <summary>Forest regions get dense trees; open grassland stays sparse.</summary>
	private int ScatterCell( Random rng, Vector3 pos, float roll, Biome regionalBiome, float treeDensity, bool onPurchasableLand, Biome starterBiome )
	{
		var prop = BiomeEcology.PickScatterProp( regionalBiome, rng, roll, treeDensity );
		if ( prop is null )
			return 0;

		if ( !BiomeEcology.CanScatterPropAt( pos, starterBiome, prop ) )
			return 0;

		var scale = prop switch
		{
			"cactus" => Range( rng, 0.75f, 1.05f ),
			"rock" => Range( rng, 0.8f, 1.3f ),
			"bush" => regionalBiome == Biome.Forest ? 0.9f : 0.85f,
			_ when prop.Contains( "tree" ) => regionalBiome == Biome.Forest
				? Range( rng, 0.85f, 1.25f )
				: Range( rng, 0.75f, 1.05f ),
			_ => 1f,
		};

		return SpawnScatterProp( pos, prop, scale, onPurchasableLand );
	}



	private int SpawnProp( Vector3 position, string propName, float scale )

	{

		var size = GameConstants.Tiles( BasePropTiles( propName ) * scale );

		return SpawnPropAtSize( position, propName, size );

	}

	private int SpawnPropAtSize( Vector3 position, string propName, float sizeWorldUnits )
	{
		var go = WorldSprites.SpawnProp( position, propName, sizeWorldUnits, WorldSprites.EnrichmentLayer );
		if ( go is null ) return 0;

		_baseSpawned.Add( go );

		return 1;
	}

	/// <summary>Scatter-only — clearable trees/rocks are spawned by <see cref="TerrainObstacleSystem"/> on purchasable land.</summary>
	private int SpawnScatterProp( Vector3 position, string propName, float scale, bool onPurchasableLand )
	{
		if ( onPurchasableLand && IsClearableScatterProp( propName ) )
			return 0;

		return SpawnProp( position, propName, scale );
	}

	private static bool IsClearableScatterProp( string propName ) =>
		propName.Contains( "tree", StringComparison.OrdinalIgnoreCase )
		|| propName.Contains( "bush", StringComparison.OrdinalIgnoreCase )
		|| propName.Equals( "cactus", StringComparison.OrdinalIgnoreCase )
		|| propName.Contains( "rock", StringComparison.OrdinalIgnoreCase );

	private static float BasePropTiles( string propName )
	{
		if ( propName.Contains( "tree" ) || propName is "pine" or "palm" or "dead_tree" )
			return GameConstants.TreeSpriteTiles;
		if ( propName.Contains( "bush" ) || propName.Equals( "cactus", StringComparison.OrdinalIgnoreCase ) )
			return GameConstants.BushSpriteTiles;
		if ( propName.Contains( "rock" ) )
			return GameConstants.RockSpriteTiles;
		return GameConstants.DecorationSpriteTiles;
	}



	private static bool IsInsideOwnedPlot( Vector3 point, PlotSystem plots, float inset )

	{

		var half = GameConstants.PlotSize * 0.5f - inset;

		foreach ( var key in plots.OwnedPlots )

		{

			if ( !PlotSystem.TryParseKey( key, out var x, out var y ) ) continue;

			var center = PlotSystem.PlotCenter( x, y );

			if ( MathF.Abs( point.x - center.x ) <= half && MathF.Abs( point.y - center.y ) <= half )

				return true;

		}



		return false;

	}

	private static bool IsInsidePurchasablePlotGrid( Vector3 point, float inset )
	{
		var (px, py) = PlotSystem.PlotAt( point );
		if ( Math.Abs( px ) > GameConstants.PlotGridRadius || Math.Abs( py ) > GameConstants.PlotGridRadius )
			return false;

		var center = PlotSystem.PlotCenter( px, py );
		var half = GameConstants.PlotSize * 0.5f - inset;
		return MathF.Abs( point.x - center.x ) <= half && MathF.Abs( point.y - center.y ) <= half;
	}



	private static Vector3 Jitter( Random rng, float amount ) =>

		new( Range( rng, -amount, amount ), Range( rng, -amount, amount ), 0f );



	private static float Range( Random rng, float min, float max ) =>

		min + rng.NextSingle() * (max - min);

	private static string Fmt( Component c ) => c.IsValid() ? "OK" : "null";
}
