namespace Terraingen.Minerals;

using Terraingen.Foliage;
using Terraingen.TerrainGen;
using Terraingen.World;

public sealed class ThornsMineralPopulateSession
{
	readonly Scene _scene;
	readonly Terrain _terrain;
	readonly ThornsFoliageBiomeSampler _sampler;
	readonly Model _model;
	readonly ThornsMineralConfig _config;
	readonly List<Vector2Int> _cells;
	readonly List<Vector2Int> _deferred = new();
	readonly List<ThornsMineralChunkData> _batch = new();
	readonly GameObject _mineralRoot;
	readonly ThornsMineralDebugStats _stats;

	int _cellIndex;

	public ThornsMineralPopulateSession(
		Scene scene,
		GameObject parent,
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig foliageConfig,
		Model model,
		ThornsMineralConfig config,
		ThornsMineralDebugStats stats,
		ThornsFoliageBiomeSampler sharedSampler,
		out GameObject mineralRoot )
	{
		_scene = scene;
		_terrain = terrain;
		_config = config;
		_model = model;
		_stats = stats;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, foliageConfig );
		_cells = ThornsMineralPlacer.BuildChunkGrid( terrain, config );
		SortCellsFromTerrainCenter();
		_stats.ChunksTotal = _cells.Count;

		mineralRoot = scene.CreateObject( true );
		mineralRoot.Name = "Thorns Minerals";
		mineralRoot.Parent = parent;
		_mineralRoot = mineralRoot;
	}

	public bool IsComplete => _cellIndex >= _cells.Count && _deferred.Count == 0;

	public int CellsRemaining => Math.Max( 0, _cells.Count - _cellIndex );

	public int DeferredCount => _deferred.Count;

	public List<ThornsMineralChunkData> ProcessChunks( int maxChunks )
	{
		_batch.Clear();

		while ( _cellIndex < _cells.Count && _batch.Count < maxChunks )
		{
			var cell = _cells[_cellIndex++];
			_batch.Add( ThornsMineralPlacer.PopulateChunk(
				_scene,
				_mineralRoot,
				_terrain,
				cell,
				_sampler,
				_model,
				_config,
				_stats ) );
		}

		return _batch;
	}

	public List<ThornsMineralChunkData> ProcessDeferred( int maxChunks )
	{
		_batch.Clear();

		for ( var i = _deferred.Count - 1; i >= 0 && _batch.Count < maxChunks; i-- )
		{
			var cell = _deferred[i];
			_deferred.RemoveAt( i );
			_batch.Add( ThornsMineralPlacer.PopulateChunk(
				_scene,
				_mineralRoot,
				_terrain,
				cell,
				_sampler,
				_model,
				_config,
				_stats ) );
		}

		return _batch;
	}

	public void PrioritizeCellsNear( Vector3 worldPosition )
	{
		if ( _cellIndex >= _cells.Count )
			return;

		var tail = _cells.GetRange( _cellIndex, _cells.Count - _cellIndex );
		tail.Sort( ( a, b ) =>
		{
			var ca = ThornsMineralPlacer.GetChunkCenter( _terrain, _config, a );
			var cb = ThornsMineralPlacer.GetChunkCenter( _terrain, _config, b );
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
			var ca = ThornsMineralPlacer.GetChunkCenter( _terrain, _config, a );
			var cb = ThornsMineralPlacer.GetChunkCenter( _terrain, _config, b );
			var da = (ca - center).LengthSquared;
			var db = (cb - center).LengthSquared;
			return da.CompareTo( db );
		} );
	}

	public void LogSummary( int totalInstances, int totalChunks, ThornsMineralDebugStats stats )
	{
		Log.Info( $"[Thorns Minerals] Placed {totalInstances} props across {totalChunks} chunks (stone={stats.StoneSpawned}, ore={stats.OreSpawned})." );
		Log.Info(
			$"[Thorns Minerals] Reject biome/material/ray={stats.BiomeRejected}/{stats.MaterialRejected}/{stats.RayMisses}" );
	}
}
