namespace Terraingen.Foliage;

using Terraingen.TerrainGen;
using Terraingen.World;

/// <summary>
/// Spread foliage chunk work across frames (no Task/async).
/// </summary>
public sealed class ThornsFoliagePopulateSession
{
	readonly Scene _scene;
	readonly GameObject _parent;
	readonly Terrain _terrain;
	readonly ThornsFoliageBiomeSampler _sampler;
	readonly ThornsFoliagePlacer.FoliageModelSet _models;
	readonly ThornsFoliageConfig _config;
	readonly List<Vector2Int> _cells;
	readonly List<Vector2Int> _deferred = new();
	readonly List<ThornsFoliageChunkData> _batch = new();
	readonly GameObject _foliageRoot;
	readonly ThornsFoliageDebugStats _stats;

	int _cellIndex;

	public ThornsFoliagePopulateSession(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig config,
		ThornsFoliagePlacer.FoliageModelSet models,
		ThornsFoliageDebugStats stats,
		ThornsFoliageBiomeSampler sharedSampler = null )
	{
		_scene = scene;
		_parent = parent;
		_terrain = terrain;
		_config = config;
		_models = models;
		_stats = stats;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, config );
		_cells = ThornsFoliagePlacer.BuildChunkGrid( terrain, config );
		SortCellsFromTerrainCenter();
		_stats.ChunksTotal = _cells.Count;

		_foliageRoot = scene.CreateObject( true );
		_foliageRoot.Name = "Thorns Foliage";
		_foliageRoot.Parent = parent;

		ThornsFoliagePlacer.SpawnDebugCenterRing( scene, _foliageRoot, terrain, _sampler, config, models, stats );
		ThornsFoliagePlacer.SpawnGuaranteedTreesAtOrigin( scene, _foliageRoot, terrain, _sampler, config, models, stats );
	}

	public bool IsComplete => _cellIndex >= _cells.Count && _deferred.Count == 0;

	public int CellsRemaining => Math.Max( 0, _cells.Count - _cellIndex );

	public int DeferredCount => _deferred.Count;

	public List<ThornsFoliageChunkData> ProcessChunks( int maxChunks )
	{
		_batch.Clear();

		while ( _cellIndex < _cells.Count && _batch.Count < maxChunks )
		{
			var cell = _cells[_cellIndex++];
			_batch.Add( Populate( cell ) );
		}

		return _batch;
	}

	public List<ThornsFoliageChunkData> ProcessDeferred( int maxChunks )
	{
		_batch.Clear();

		for ( var i = _deferred.Count - 1; i >= 0 && _batch.Count < maxChunks; i-- )
		{
			var cell = _deferred[i];
			_deferred.RemoveAt( i );
			_batch.Add( Populate( cell ) );
		}

		return _batch;
	}

	public void PrioritizeCellsNear( Vector3 worldPosition, ThornsFoliageConfig config )
	{
		if ( _cellIndex >= _cells.Count )
			return;

		var tail = _cells.GetRange( _cellIndex, _cells.Count - _cellIndex );
		tail.Sort( ( a, b ) =>
		{
			var ca = ThornsFoliagePlacer.GetChunkCenter( _terrain, config, a );
			var cb = ThornsFoliagePlacer.GetChunkCenter( _terrain, config, b );
			var da = (ca - worldPosition).LengthSquared;
			var db = (cb - worldPosition).LengthSquared;
			return da.CompareTo( db );
		} );

		for ( var i = 0; i < tail.Count; i++ )
			_cells[_cellIndex + i] = tail[i];
	}

	void SortCellsFromTerrainCenter()
	{
		var center = ThornsWorldInterest.ResolveTerrainCenter( _terrain );
		_cells.Sort( ( a, b ) =>
		{
			var ca = ThornsFoliagePlacer.GetChunkCenter( _terrain, _config, a );
			var cb = ThornsFoliagePlacer.GetChunkCenter( _terrain, _config, b );
			var da = (ca - center).LengthSquared;
			var db = (cb - center).LengthSquared;
			return da.CompareTo( db );
		} );
	}

	ThornsFoliageChunkData Populate( Vector2Int cell ) =>
		ThornsFoliagePlacer.PopulateChunk(
			_scene,
			_foliageRoot,
			_terrain,
			cell,
			_sampler,
			_models,
			_config,
			_stats );

	public void LogSummary( int totalInstances, int totalChunks, ThornsFoliageDebugStats stats )
	{
		Log.Info( $"[Thorns Foliage] Placed {totalInstances} instances across {totalChunks} chunks." );
		Log.Info( $"[Thorns Foliage] Debug: trees={stats.TreesSpawned}, clusters {stats.ClustersPlaced}/{stats.ClustersAttempted}, rayMiss={stats.RayMisses}, biomeReject={stats.BiomeRejected}, buildingReject={stats.BuildingRejected}, slopeFlatReject={stats.SlopeFlatRejected}, weightReject={stats.WeightRejected}" );

		if ( stats.LastSpawnPosition.HasValue )
			Log.Info( $"[Thorns Foliage] Last spawn @ {stats.LastSpawnPosition.Value}" );
	}
}
