#nullable disable

using System.Diagnostics;
using System.Runtime.InteropServices;
using Sandbox;
using Terraingen.Foliage;
using Terraingen.TerrainGen;

namespace Terraingen.Clutter;

/// <summary>Stable client-only grass tiles rendered as one GPU-instanced draw per model variant.</summary>
[Title( "Thorns Client Grass Renderer" )]
[Category( "Terrain" )]
[Icon( "grass" )]
public sealed class ClientGrassRenderer : Component
{
	const float GrassModelScaleCorrection = 0.2f;
	const float GrassHeightScaleMultiplier = 0.5f;
	const int GrassPlacementRevision = 10;
	const int DefaultTilesBuiltPerFrame = 1;
	const double DefaultBuildBudgetMilliseconds = 2.0;
	static int _tilesBuiltPerFrame = DefaultTilesBuiltPerFrame;
	static double _buildBudgetMilliseconds = DefaultBuildBudgetMilliseconds;

	public static void ApplyQualityBudget( int tilesPerFrame, float buildBudgetMs )
	{
		_tilesBuiltPerFrame = Math.Max( 1, tilesPerFrame );
		_buildBudgetMilliseconds = Math.Max( 0.5, buildBudgetMs );
	}

	static readonly string[] GrassModelPaths =
	[
		"models/clutter/grass_common_short.vmdl",
		"models/clutter/grass_common_short1.vmdl",
		"models/clutter/grass_common_short2.vmdl",
	];

	[ConVar( "thorns_grass_enabled" )] public static bool GrassEnabled { get; set; } = true;
	[ConVar( "thorns_grass_radius" )] public static float GrassRadiusMeters { get; set; } = 70f;
	[ConVar( "thorns_grass_full_radius" )] public static float GrassFullRadiusMeters { get; set; } = 28f;
	[ConVar( "thorns_grass_cell_size" )] public static float GrassCellSizeMeters { get; set; } = 8f;
	[ConVar( "thorns_grass_density_near" )] public static int GrassDensityNear { get; set; } = 270;
	[ConVar( "thorns_grass_density_mid" )] public static int GrassDensityMid { get; set; } = 135;
	[ConVar( "thorns_grass_density_far" )] public static int GrassDensityFar { get; set; } = 45;
	[ConVar( "thorns_grass_slope_cutoff" )] public static float GrassSlopeCutoffDegrees { get; set; } = 32f;
	[ConVar( "thorns_grass_fade_start" )] public static float GrassFadeStartMeters { get; set; } = 48f;
	[ConVar( "thorns_grass_fade_end" )] public static float GrassFadeEndMeters { get; set; } = 70f;
	[ConVar( "thorns_grass_scale_min" )] public static float GrassMinScale { get; set; } = 0.75f;
	[ConVar( "thorns_grass_scale_max" )] public static float GrassMaxScale { get; set; } = 1.35f;
	[ConVar( "thorns_grass_seed" )] public static int GrassSeedSalt { get; set; } = 17391;
	[ConVar( "thorns_grass_debug" )] public static bool GrassDebug { get; set; }

	readonly Dictionary<Vector2Int, GrassTile> _tiles = new();
	readonly Queue<Vector2Int> _pendingTiles = new();
	readonly HashSet<Vector2Int> _pendingSet = new();
	readonly HashSet<Vector2Int> _wantedSet = new();
	readonly List<Vector2Int> _remove = new();
	readonly List<Vector2Int> _wanted = new();
	readonly Stopwatch _watch = new();

	ThornsClutterConfig _config;
	Terrain _terrain;
	ThornsFoliageBiomeSampler _sampler;
	ThornsTerrainNetSpec _spec;
	GameObject _chunkRoot;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	GrassInstancedSceneObject _batch;
	Model[] _grassModels = [];
	Vector2Int _lastObserverCell = new( int.MinValue, int.MinValue );
	int _lastTuningHash;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextDebug;
	float _lastRefreshMs;
	float _lastBuildMs;
	int _instanceCount;
	int _rejectedWater;
	int _rejectedMaterial;
	int _rejectedMask;

	public void BeginStreaming( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig,
		ThornsTerrainNetSpec spec, GameObject chunkRoot, ThornsClutterConfig config,
		ThornsFoliageBiomeSampler sharedSampler = null )
	{
		Clear();
		_config = config ?? new ThornsClutterConfig();
		if ( Scene.IsEditor || !Game.IsPlaying || !GrassEnabled || !_config.Enabled || !terrain.IsValid() || field is null )
			return;

		_terrain = terrain;
		_spec = spec;
		_chunkRoot = chunkRoot;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, BuildSamplerConfig() );
		_grassModels = LoadGrassModels();
		if ( _grassModels.Length == 0 )
		{
			Log.Warning( "[Thorns Grass] Could not load any grass_common_short clutter models." );
			return;
		}
		if ( _grassModels.Length < GrassModelPaths.Length )
			Log.Warning( $"[Thorns Grass] Loaded {_grassModels.Length}/{GrassModelPaths.Length} grass_common_short clutter models." );

		Refresh( true );
	}

	protected override void OnUpdate()
	{
		if ( _terrain is null || !_terrain.IsValid() )
			return;
		if ( !GrassEnabled || !_config.Enabled )
		{
			Suspend();
			return;
		}

		using ( ThornsPerfDebug.Scope( "ClientGrassRenderer" ) )
		{
			Refresh( false );
			ProcessTileQueue();
		}

		ThornsPerfDebug.GrassInstancesVisible = _instanceCount;
		ThornsPerfDebug.GrassTilesPending = _pendingTiles.Count;
		if ( GrassDebug && _nextDebug )
		{
			_nextDebug = 0.5f;
			Log.Info( GetDebugSummary() );
		}
	}

	void Refresh( bool force )
	{
		if ( !TryResolveObserverPosition( out var observer ) )
			return;
		EnsureBatch();

		var size = CellSize;
		var observerCell = ToCell( observer, size );
		var tuningHash = ComputeTuningHash();
		var tuningChanged = tuningHash != _lastTuningHash;
		if ( !force && observerCell == _lastObserverCell && !tuningChanged )
			return;

		_watch.Restart();
		if ( tuningChanged )
			Suspend();
		_lastObserverCell = observerCell;
		_lastTuningHash = tuningHash;

		var radius = Radius;
		var cellRadius = Math.Max( 1, (int)MathF.Ceiling( radius / size ) );
		_wantedSet.Clear();
		for ( var y = -cellRadius; y <= cellRadius; y++ )
		for ( var x = -cellRadius; x <= cellRadius; x++ )
		{
			var coord = new Vector2Int( observerCell.x + x, observerCell.y + y );
			if ( Distance2DSquared( CellCenter( coord, size ), observer ) > radius * radius )
				continue;
			_wantedSet.Add( coord );
		}

		_remove.Clear();
		foreach ( var coord in _tiles.Keys )
			if ( !_wantedSet.Contains( coord ) )
				_remove.Add( coord );
		foreach ( var coord in _remove )
		{
			if ( _tiles.Remove( coord, out var tile ) )
				_instanceCount -= tile.Count;
		}
		if ( _remove.Count > 0 )
			_batch?.MarkTilesDirty();

		_wanted.Clear();
		foreach ( var coord in _wantedSet )
			if ( !_tiles.ContainsKey( coord ) && !_pendingSet.Contains( coord ) )
				_wanted.Add( coord );
		_wanted.Sort( ( a, b ) =>
			Distance2DSquared( CellCenter( a, size ), observer )
				.CompareTo( Distance2DSquared( CellCenter( b, size ), observer ) ) );
		foreach ( var coord in _wanted )
		{
			_pendingSet.Add( coord );
			_pendingTiles.Enqueue( coord );
		}

		_watch.Stop();
		_lastRefreshMs = (float)_watch.Elapsed.TotalMilliseconds;
	}

	void ProcessTileQueue()
	{
		if ( _pendingTiles.Count == 0 )
			return;

		_watch.Restart();
		var built = 0;
		while ( built < _tilesBuiltPerFrame && _pendingTiles.Count > 0 && _watch.Elapsed.TotalMilliseconds < _buildBudgetMilliseconds )
		{
			var coord = _pendingTiles.Dequeue();
			_pendingSet.Remove( coord );
			if ( _tiles.ContainsKey( coord ) || !IsCellInsideCurrentRadius( coord ) )
				continue;

			var tile = GenerateTile( coord );
			_tiles[coord] = tile;
			_instanceCount += tile.Count;
			_batch?.MarkTilesDirty();
			built++;
		}
		_watch.Stop();
		_lastBuildMs = (float)_watch.Elapsed.TotalMilliseconds;
	}

	GrassTile GenerateTile( Vector2Int coord )
	{
		var size = CellSize;
		var center = CellCenter( coord, size );
		var rng = new Random( StableHash( _spec?.Seed ?? 0, GrassSeedSalt, coord.x, coord.y ) );
		var target = Math.Max( 1, DensityNear );
		var side = Math.Max( 1, (int)MathF.Ceiling( MathF.Sqrt( target ) ) );
		var spacing = size / side;
		var tile = new GrassTile( _grassModels.Length );

		for ( var index = 0; index < target && _instanceCount + tile.Count < _config.GrassMaxVisibleInstances; index++ )
		{
			var gx = index % side;
			var gy = index / side;
			var wx = center.x - size * 0.5f + (gx + 0.5f + (rng.NextSingle() - 0.5f) * 0.86f) * spacing;
			var wy = center.y - size * 0.5f + (gy + 0.5f + (rng.NextSingle() - 0.5f) * 0.86f) * spacing;
			if ( !_sampler.TrySampleGrassPlacement( wx, wy, out _, out _ ) )
			{
				_rejectedWater++;
				continue;
			}
			if ( !_sampler.IsDominantTerrainMaterialGrass( wx, wy ) )
			{
				_rejectedMaterial++;
				continue;
			}
			if ( IsExcludedFromStructures( wx, wy ) )
			{
				_rejectedMask++;
				continue;
			}

			var normal = SampleNormal( wx, wy );

			var scale = MathX.Lerp( MinScale, MaxScale, rng.NextSingle() ) * GrassModelScaleCorrection * GrassHeightScaleMultiplier;
			var modelIndex = rng.Next( _grassModels.Length );
			var grassModel = _grassModels[modelIndex];
			var pos = new Vector3( wx, wy, _terrain.GameObject.WorldPosition.z + _sampler.SampleWorldHeight( wx, wy ) );
			pos += Vector3.Up * ComputeGrassVerticalOffset( grassModel, scale );
			var rot = Rotation.LookAt( normal, Vector3.Up ) * Rotation.From( 90f, 0f, 0f ) * Rotation.FromYaw( rng.NextSingle() * 360f );
			tile.Add( modelIndex, new Transform( pos, rot, scale ) );
		}

		return tile;
	}

	/// <summary>Buildings/roads only — elevation and biome are handled by terrain material + sea-level gate.</summary>
	bool IsExcludedFromStructures( float worldX, float worldY )
	{
		if ( _spec is null || !_chunkRoot.IsValid() )
			return false;

		var local = _chunkRoot.WorldRotation.Inverse * (new Vector3( worldX, worldY, 0f ) - _chunkRoot.WorldPosition);
		return ThornsTerrainDecorScatter.ChunkPointOverlapsAnyProcBuildingFootprintFromPads( local.x, local.y, _spec.ProcBuildingTerrainPads )
		       || ThornsWorldRoadTerrain.PointInFoliageClearance( local.x, local.y, in _spec );
	}

	Vector3 SampleNormal( float x, float y )
	{
		const float d = 24f;
		var dx = _sampler.SampleWorldHeight( x + d, y ) - _sampler.SampleWorldHeight( x - d, y );
		var dy = _sampler.SampleWorldHeight( x, y + d ) - _sampler.SampleWorldHeight( x, y - d );
		return new Vector3( -dx, -dy, d * 2f ).Normal;
	}

	float ComputeGrassVerticalOffset( Model grassModel, float scale )
	{
		var lift = Math.Max( 0f, -grassModel.Bounds.Mins.z * scale );
		return _config.GrassSurfaceOffset + lift - _config.GrassGroundEmbedInches;
	}

	void EnsureBatch()
	{
		if ( _batch.IsValid() )
			return;
		_batch = new GrassInstancedSceneObject( Scene.SceneWorld, _grassModels, _tiles );
	}

	void Suspend()
	{
		_tiles.Clear();
		_pendingTiles.Clear();
		_pendingSet.Clear();
		_wantedSet.Clear();
		_batch?.MarkTilesDirty();
		_instanceCount = 0;
		_lastObserverCell = new Vector2Int( int.MinValue, int.MinValue );
		_lastTuningHash = 0;
	}

	bool IsCellInsideCurrentRadius( Vector2Int coord )
	{
		if ( !TryResolveObserverPosition( out var observer ) )
			return false;
		return Distance2DSquared( CellCenter( coord, CellSize ), observer ) <= Radius * Radius;
	}

	bool TryResolveObserverPosition( out Vector3 observer )
	{
		if ( ThornsPawn.Local is { IsValid: true } pawn )
		{
			observer = pawn.GameObject.WorldPosition;
			return true;
		}

		ThornsSceneObserver.Refresh( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		if ( _observerCamera is not null && _observerCamera.IsValid() )
		{
			observer = _observerCamera.GameObject.WorldPosition;
			return true;
		}

		observer = default;
		return false;
	}

	static Model[] LoadGrassModels()
	{
		var loaded = new List<Model>( GrassModelPaths.Length );
		for ( var i = 0; i < GrassModelPaths.Length; i++ )
		{
			var model = ThornsFoliageModelCache.Load( GrassModelPaths[i] );
			if ( model.IsValid && !model.IsError )
				loaded.Add( model );
		}

		return loaded.ToArray();
	}

	ThornsFoliageConfig BuildSamplerConfig() => new()
	{
		VerboseDebug = false,
		MaxTreeSlopeDegrees = ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees,
		MaxSlopeForGrass = float.MaxValue,
		MinHeightAboveSea = 0.005f,
		FoliageSeed = (_spec?.Seed ?? 0) + GrassSeedSalt,
	};

	public string GetDebugSummary() =>
		$"[Thorns Grass] gpu-instanced tiles={_tiles.Count} batches={_grassModels.Length} pending={_pendingSet.Count} instances={_instanceCount}/{_config.GrassMaxVisibleInstances} refreshMs={_lastRefreshMs:F2} buildMs={_lastBuildMs:F2} reject water/material/structure={_rejectedWater}/{_rejectedMaterial}/{_rejectedMask}";

	public void Clear()
	{
		Suspend();
		if ( _batch.IsValid() )
			_batch.Delete();
		_batch = null;
		_grassModels = [];
	}

	protected override void OnDestroy() => Clear();

	float Radius => Math.Max( 1f, Math.Min( GrassRadiusMeters * ThornsClutterConfig.InchesPerMeter, _config.GrassRenderRadius ) );
	float CellSize => Math.Max( 64f, GrassCellSizeMeters * ThornsClutterConfig.InchesPerMeter );
	float MinScale => Math.Max( 0.05f, Math.Max( GrassMinScale, _config.GrassMinScale ) );
	float MaxScale => Math.Max( MinScale, Math.Min( GrassMaxScale, _config.GrassMaxScale ) );
	int DensityNear => Math.Min( GrassDensityNear, _config.BladesPerCellNear );

	int ComputeTuningHash() => HashCode.Combine( Radius, CellSize, DensityNear, MinScale, MaxScale, GrassSeedSalt, GrassPlacementRevision );
	static Vector2Int ToCell( Vector3 world, float size ) => new( (int)MathF.Floor( world.x / size ), (int)MathF.Floor( world.y / size ) );
	static Vector3 CellCenter( Vector2Int cell, float size ) => new( (cell.x + 0.5f) * size, (cell.y + 0.5f) * size, 0f );
	static float Distance2DSquared( Vector3 a, Vector3 b ) { var x = a.x - b.x; var y = a.y - b.y; return x * x + y * y; }
	static int StableHash( int seed, int salt, int x, int y )
	{
		unchecked
		{
			var h = 2166136261u;
			h = (h ^ (uint)seed) * 16777619u;
			h = (h ^ (uint)salt) * 16777619u;
			h = (h ^ (uint)x) * 16777619u;
			h = (h ^ (uint)y) * 16777619u;
			return (int)(h & 0x7fffffffu);
		}
	}
}

sealed class GrassTile
{
	public readonly List<Transform>[] Models;

	public GrassTile( int modelCount )
	{
		Models = Enumerable.Range( 0, modelCount )
			.Select( _ => new List<Transform>() )
			.ToArray();
	}

	public int Count { get; private set; }

	public void Add( int modelIndex, Transform transform )
	{
		Models[modelIndex].Add( transform );
		Count++;
	}
}

sealed class GrassInstancedSceneObject : SceneCustomObject
{
	readonly Model[] _models;
	readonly Dictionary<Vector2Int, GrassTile> _tiles;
	readonly RenderAttributes _attributes = new();
	readonly List<Transform>[] _instancesByModel;
	bool _tilesDirty = true;

	public GrassInstancedSceneObject( SceneWorld world, Model[] models, Dictionary<Vector2Int, GrassTile> tiles ) : base( world )
	{
		_models = models;
		_tiles = tiles;
		_instancesByModel = Enumerable.Range( 0, models.Length )
			.Select( _ => new List<Transform>() )
			.ToArray();
		RenderLayer = SceneRenderLayer.Default;
		Bounds = new BBox( new Vector3( -1000000f ), new Vector3( 1000000f ) );
	}

	public void MarkTilesDirty() => _tilesDirty = true;

	void RebuildInstanceBatches()
	{
		if ( !_tilesDirty )
			return;

		for ( var modelIndex = 0; modelIndex < _instancesByModel.Length; modelIndex++ )
			_instancesByModel[modelIndex].Clear();

		foreach ( var tile in _tiles.Values )
		for ( var modelIndex = 0; modelIndex < _instancesByModel.Length; modelIndex++ )
			_instancesByModel[modelIndex].AddRange( tile.Models[modelIndex] );

		_tilesDirty = false;
	}

	public override void RenderSceneObject()
	{
		RebuildInstanceBatches();
		for ( var modelIndex = 0; modelIndex < _models.Length; modelIndex++ )
		{
			var model = _models[modelIndex];
			var instances = _instancesByModel[modelIndex];
			if ( model.IsValid && instances.Count > 0 )
				Graphics.DrawModelInstanced( model, CollectionsMarshal.AsSpan( instances ), _attributes );
		}
	}
}
