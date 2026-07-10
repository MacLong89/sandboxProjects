#nullable disable

using System.Diagnostics;
using System.Runtime.InteropServices;
using Sandbox;
using Terraingen.Core;
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
	const int GrassPlacementRevision = 13;
	const int TilesBuiltPerFrame = 3;
	const double BuildBudgetMilliseconds = 3.5;

	static readonly string[] GrassModelPaths =
	{
		"models/clutter/grass_common_short.vmdl",
		"models/clutter/grass_common_short1.vmdl",
		"models/clutter/grass_common_short2.vmdl",
	};

	[ConVar( "thorns_grass_enabled" )] public static bool GrassEnabled { get; set; } = true;
	[ConVar( "thorns_grass_radius" )] public static float GrassRadiusMeters { get; set; } = 58f;
	[ConVar( "thorns_grass_full_radius" )] public static float GrassFullRadiusMeters { get; set; } = 22f;
	[ConVar( "thorns_grass_cell_size" )] public static float GrassCellSizeMeters { get; set; } = 9f;
	[ConVar( "thorns_grass_density_near" )] public static int GrassDensityNear { get; set; } = 210;
	[ConVar( "thorns_grass_density_mid" )] public static int GrassDensityMid { get; set; } = 90;
	[ConVar( "thorns_grass_density_far" )] public static int GrassDensityFar { get; set; } = 18;
	[ConVar( "thorns_grass_slope_cutoff" )] public static float GrassSlopeCutoffDegrees { get; set; } = 32f;
	[ConVar( "thorns_grass_fade_start" )] public static float GrassFadeStartMeters { get; set; } = 38f;
	[ConVar( "thorns_grass_fade_end" )] public static float GrassFadeEndMeters { get; set; } = 58f;
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
	Vector3 _sortObserver;
	float _sortCellSize;

	ThornsClutterConfig _config;
	ThornsTerrainConfig _terrainConfig;
	Terrain _terrain;
	ThornsFoliageBiomeSampler _sampler;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	GrassInstancedSceneObject _batch;
	Model[] _grassModels = Array.Empty<Model>();
	Vector2Int _lastObserverCell = new( int.MinValue, int.MinValue );
	int _lastTuningHash;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextDebug;
	float _lastRefreshMs;
	float _lastBuildMs;
	int _instanceCount;
	int _rejectedWater;
	int _rejectedMaterial;
	bool _awaitingLocalPlayerObserver = true;
	bool _gpuStreamingActive;
	Vector3 _cachedObserver;
	bool _hasCachedObserver;

	public bool IsGpuStreamingActive => _gpuStreamingActive;
	public int PendingTileCount => _pendingTiles.Count;

	public void BeginStreaming(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsClutterConfig config,
		ThornsFoliageBiomeSampler sharedSampler = null )
	{
		Clear();
		_config = config ?? new ThornsClutterConfig();
		_terrainConfig = terrainConfig;
		_gpuStreamingActive = false;
		if ( Scene.IsEditor || !Game.IsPlaying || !GrassEnabled || !_config.Enabled || !terrain.IsValid() || field is null )
			return;

		_terrain = terrain;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, BuildSamplerConfig() );
		var grassModels = GrassModelPaths
			.Select( ThornsFoliageModelCache.Load )
			.Where( model => model.IsValid && !model.IsError )
			.ToList();

		_grassModels = grassModels.ToArray();
		if ( _grassModels.Length == 0 )
		{
			Log.Warning( "[Thorns Grass] Could not load any grass_common_short clutter models." );
			return;
		}
		if ( _grassModels.Length < GrassModelPaths.Length )
			Log.Warning( $"[Thorns Grass] Loaded {_grassModels.Length}/{GrassModelPaths.Length} grass_common_short clutter models." );

		_awaitingLocalPlayerObserver = true;
		_lastObserverCell = new Vector2Int( int.MinValue, int.MinValue );
		_gpuStreamingActive = true;
		Log.Info( "[Thorns Grass] Streaming armed — waiting for local player before building tiles." );
	}

	/// <summary>Call after the local Terrain Explorer spawns so grass builds around the player, not the editor preview camera.</summary>
	public void ForceObserverRefresh()
	{
		Log.Info( "[Thorns Grass] ForceObserverRefresh — arming tile build around local player." );
		_awaitingLocalPlayerObserver = false;
		_lastObserverCell = new Vector2Int( int.MinValue, int.MinValue );
		Refresh( true );
		Log.Info( $"[Thorns Grass] ForceObserverRefresh — queued {_pendingTiles.Count} tile(s), instances={_instanceCount}." );
	}

	protected override void OnUpdate()
	{
		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( _terrain is null || !_terrain.IsValid() )
			return;
		if ( !GrassEnabled || !_config.Enabled )
		{
			Suspend();
			return;
		}

		_hasCachedObserver = TryResolveObserverPosition( out _cachedObserver );

		if ( _awaitingLocalPlayerObserver )
		{
			if ( ThornsSceneObserver.FindLocalPlayerObject( Scene ) is null )
				return;

			_awaitingLocalPlayerObserver = false;
			_lastObserverCell = new Vector2Int( int.MinValue, int.MinValue );
			Refresh( true );
			Log.Info( "[Thorns Grass] Local player found — building grass around explorer." );
		}

		Refresh( false );
		ProcessTileQueue();
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
		_sortObserver = observer;
		_sortCellSize = size;
		foreach ( var coord in _wantedSet )
			if ( !_tiles.ContainsKey( coord ) && !_pendingSet.Contains( coord ) )
				_wanted.Add( coord );
		_wanted.Sort( CompareWantedCellByDistance );
		foreach ( var coord in _wanted )
		{
			_pendingSet.Add( coord );
			_pendingTiles.Enqueue( coord );
		}

		_watch.Stop();
		_lastRefreshMs = (float)_watch.Elapsed.TotalMilliseconds;
	}

	int ProcessTileQueue()
	{
		if ( _pendingTiles.Count == 0 )
			return 0;

		_watch.Restart();
		var built = 0;
		while ( built < TilesBuiltPerFrame && _pendingTiles.Count > 0 && _watch.Elapsed.TotalMilliseconds < BuildBudgetMilliseconds )
		{
			var coord = _pendingTiles.Dequeue();
			_pendingSet.Remove( coord );
			if ( _tiles.ContainsKey( coord ) || !IsCellInsideCurrentRadius( coord ) )
				continue;

			var tile = GenerateTile( coord );
			_tiles[coord] = tile;
			_instanceCount += tile.Count;
			_batch?.AppendTile( tile );
			built++;
		}
		_watch.Stop();
		_lastBuildMs = (float)_watch.Elapsed.TotalMilliseconds;
		return built;
	}

	GrassTile GenerateTile( Vector2Int coord )
	{
		var size = CellSize;
		var center = CellCenter( coord, size );
		var worldSeed = _terrainConfig?.WorldSeed ?? _config.WorldSeed;
		var rng = new Random( StableHash( worldSeed, GrassSeedSalt, coord.x, coord.y ) );
		var target = DensityForTile( coord, center );
		var side = Math.Max( 1, (int)MathF.Ceiling( MathF.Sqrt( target ) ) );
		var spacing = size / side;
		var tile = new GrassTile( _grassModels.Length );

		for ( var index = 0; index < target && _instanceCount + tile.Count < _config.GrassMaxVisibleInstances; index++ )
		{
			var gx = index % side;
			var gy = index / side;
			var wx = center.x - size * 0.5f + (gx + 0.5f + (rng.NextSingle() - 0.5f) * 0.86f) * spacing;
			var wy = center.y - size * 0.5f + (gy + 0.5f + (rng.NextSingle() - 0.5f) * 0.86f) * spacing;
			if ( !_sampler.CanPlaceGrassOnTerrainMaterial( wx, wy ) )
			{
				_rejectedMaterial++;
				continue;
			}

			var scale = GrassModelScaleCorrection * GrassHeightScaleMultiplier
			            * Math.Max( 0.05f, _config.GlobalScaleMultiplier )
			            * ThornsNatureScaleVariance.Sample( rng );
			var modelIndex = rng.Next( _grassModels.Length );
			var grassModel = _grassModels[modelIndex];
			if ( !ThornsClutterSurface.TrySampleWorldForGrass( _terrain, _sampler, wx, wy, grassModel, scale, _config, out var pos, out var normal ) )
			{
				_rejectedWater++;
				continue;
			}
			var rot = Rotation.LookAt( normal, Vector3.Up ) * Rotation.From( 90f, 0f, 0f ) * Rotation.FromYaw( rng.NextSingle() * 360f );
			tile.Add( modelIndex, new Transform( pos, rot, scale ) );
		}

		return tile;
	}

	int DensityForTile( Vector2Int coord, Vector3 center )
	{
		if ( !_hasCachedObserver )
			return Math.Max( 1, DensityNear );

		var observer = _cachedObserver;
		var dist = MathF.Sqrt( Distance2DSquared( center, observer ) );
		var fullRadius = Math.Min( GrassFullRadiusMeters * ThornsClutterConfig.InchesPerMeter, _config.GrassFullDensityRadius );
		var fadeStart = Math.Min( GrassFadeStartMeters * ThornsClutterConfig.InchesPerMeter, _config.GrassFadeStart );
		var fadeEnd = Math.Max( fadeStart + 1f, Math.Min( GrassFadeEndMeters * ThornsClutterConfig.InchesPerMeter, _config.GrassFadeEnd ) );

		if ( dist <= fullRadius )
			return Math.Min( DensityNear, _config.BladesPerCellNear );

		if ( dist >= fadeEnd )
			return Math.Min( GrassDensityFar, _config.BladesPerCellFar );

		var midRadius = (fullRadius + fadeEnd) * 0.5f;
		if ( dist <= midRadius )
		{
			var t = ((dist - fullRadius) / Math.Max( midRadius - fullRadius, 1f )).Clamp( 0f, 1f );
			var near = Math.Min( DensityNear, _config.BladesPerCellNear );
			var mid = Math.Min( GrassDensityMid, _config.BladesPerCellMid );
			return (int)MathF.Round( MathX.Lerp( near, mid, t ) );
		}

		var tFar = ((dist - midRadius) / Math.Max( fadeEnd - midRadius, 1f )).Clamp( 0f, 1f );
		var midFar = Math.Min( GrassDensityMid, _config.BladesPerCellMid );
		var far = Math.Min( GrassDensityFar, _config.BladesPerCellFar );
		return (int)MathF.Round( MathX.Lerp( midFar, far, tFar ) );
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
		_batch?.ClearInstances();
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
		observer = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		return observer != Vector3.Zero || ThornsSceneObserver.FindLocalPlayerObject( Scene ).IsValid();
	}

	ThornsFoliageConfig BuildSamplerConfig()
	{
		var worldSeed = _terrainConfig?.WorldSeed ?? _config.WorldSeed;
		return new ThornsFoliageConfig
		{
			VerboseDebug = false,
			MaxSlopeForGrass = float.MaxValue,
			MinHeightAboveSea = 0.005f,
			FoliageSeed = worldSeed + GrassSeedSalt,
		};
	}

	public string GetDebugSummary()
	{
		if ( _config is null )
			return "[Thorns Grass] not initialized";

		return $"[Thorns Grass] gpu-instanced tiles={_tiles.Count} batches={_grassModels.Length} pending={_pendingSet.Count} instances={_instanceCount}/{_config.GrassMaxVisibleInstances} refreshMs={_lastRefreshMs:F2} buildMs={_lastBuildMs:F2} reject water/material={_rejectedWater}/{_rejectedMaterial}";
	}

	public void Clear()
	{
		_awaitingLocalPlayerObserver = true;
		_gpuStreamingActive = false;
		Suspend();
		if ( _batch.IsValid() )
			_batch.Delete();
		_batch = null;
		_grassModels = Array.Empty<Model>();
	}

	protected override void OnDestroy() => Clear();

	float Radius => Math.Max( 1f, Math.Min( GrassRadiusMeters * ThornsClutterConfig.InchesPerMeter, _config.GrassRenderRadius ) );
	float CellSize => Math.Max( 64f, GrassCellSizeMeters * ThornsClutterConfig.InchesPerMeter );
	float MinScale => Math.Max( 0.05f, Math.Max( GrassMinScale, _config.GrassMinScale ) );
	float MaxScale => Math.Max( MinScale, Math.Min( GrassMaxScale, _config.GrassMaxScale ) );
	int DensityNear => Math.Min( GrassDensityNear, _config.BladesPerCellNear );

	int ComputeTuningHash()
	{
		var hash = new HashCode();
		hash.Add( Radius );
		hash.Add( CellSize );
		hash.Add( DensityNear );
		hash.Add( GrassDensityMid );
		hash.Add( GrassDensityFar );
		hash.Add( GrassFullRadiusMeters );
		hash.Add( GrassFadeStartMeters );
		hash.Add( GrassFadeEndMeters );
		hash.Add( MinScale );
		hash.Add( MaxScale );
		hash.Add( _config.GlobalScaleMultiplier );
		hash.Add( GrassSeedSalt );
		hash.Add( GrassPlacementRevision );
		return hash.ToHashCode();
	}
	static Vector2Int ToCell( Vector3 world, float size ) => new( (int)MathF.Floor( world.x / size ), (int)MathF.Floor( world.y / size ) );
	static Vector3 CellCenter( Vector2Int cell, float size ) => new( (cell.x + 0.5f) * size, (cell.y + 0.5f) * size, 0f );
	static float Distance2DSquared( Vector3 a, Vector3 b ) { var x = a.x - b.x; var y = a.y - b.y; return x * x + y * y; }

	int CompareWantedCellByDistance( Vector2Int a, Vector2Int b ) =>
		Distance2DSquared( CellCenter( a, _sortCellSize ), _sortObserver )
			.CompareTo( Distance2DSquared( CellCenter( b, _sortCellSize ), _sortObserver ) );
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

	public void ClearInstances()
	{
		for ( var modelIndex = 0; modelIndex < _instancesByModel.Length; modelIndex++ )
			_instancesByModel[modelIndex].Clear();
		_tilesDirty = false;
	}

	public void AppendTile( GrassTile tile )
	{
		for ( var modelIndex = 0; modelIndex < _instancesByModel.Length; modelIndex++ )
			_instancesByModel[modelIndex].AddRange( tile.Models[modelIndex] );
	}

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
