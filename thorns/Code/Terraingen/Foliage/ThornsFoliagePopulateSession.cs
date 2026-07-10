namespace Terraingen.Foliage;

using Terraingen.TerrainGen;

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
		_stats.ChunksTotal = _cells.Count;

		_foliageRoot = scene.CreateObject( true );
		_foliageRoot.Name = "Thorns Foliage";
		_foliageRoot.Parent = parent;

		ThornsFoliagePlacer.SpawnDebugCenterRing( scene, _foliageRoot, terrain, _sampler, config, models, stats );
		ThornsFoliagePlacer.SpawnGuaranteedTreesAtOrigin( scene, _foliageRoot, terrain, _sampler, config, models, stats );
	}

	public bool IsComplete => _cellIndex >= _cells.Count;

	public List<ThornsFoliageChunkData> ProcessChunks( int maxChunks )
	{
		_batch.Clear();
		var budget = new ThornsFoliageSpawnBudget( _config.MaxInstancesPerPopulateFrame );

		while ( _cellIndex < _cells.Count && _batch.Count < maxChunks )
		{
			if ( !budget.CanSpawn )
				break;

			var cell = _cells[_cellIndex++];
			_batch.Add( ThornsFoliagePlacer.PopulateChunk(
				_scene,
				_foliageRoot,
				_terrain,
				cell,
				_sampler,
				_models,
				_config,
				_stats,
				budget ) );
		}

		return _batch;
	}

	public void LogSummary( int totalInstances, int totalChunks, ThornsFoliageDebugStats stats )
	{
		Log.Info( $"[Thorns Foliage] Placed {totalInstances} instances across {totalChunks} chunks." );
		Log.Info( $"[Thorns Foliage] Debug: trees={stats.TreesSpawned}, clusters {stats.ClustersPlaced}/{stats.ClustersAttempted}, rayMiss={stats.RayMisses}, biomeReject={stats.BiomeRejected}, slopeFlatReject={stats.SlopeFlatRejected}, weightReject={stats.WeightRejected}" );

		if ( stats.LastSpawnPosition.HasValue )
			Log.Info( $"[Thorns Foliage] Last spawn: {stats.LastSpawnSpecies} at {stats.LastSpawnPosition.Value}, scale={stats.LastSpawnScale:F2}, modelBounds={stats.LastModelBoundsSize}" );

		if ( stats.NearestInstancePosition.HasValue )
			Log.Info( $"[Thorns Foliage] Nearest to player: {stats.NearestInstancePosition.Value} dist={stats.NearestInstanceDistance:F0}" );
	}
}
