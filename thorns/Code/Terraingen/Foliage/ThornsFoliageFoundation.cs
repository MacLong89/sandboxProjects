namespace Terraingen.Foliage;

using Terraingen;
using Terraingen.TerrainGen;
using Sandbox;

/// <summary>
/// Full-world ecosystem foliage — forest masses, treelines, river corridors, scenic openings.
/// </summary>
[Title( "Thorns Foliage World" )]
[Category( "Terrain" )]
[Icon( "park" )]
public sealed class ThornsFoliageFoundation : Component
{
	[Property] public ThornsFoliageConfig Config { get; set; } = new();

	readonly List<ThornsFoliageChunkData> _chunks = new();
	readonly ThornsFoliageDebugStats _stats = new();

	ThornsFoliagePopulateSession _session;
	Terrain _terrain;
	int _totalInstances;
	int _lodChunkCursor;
	bool _populating;
	bool _ready;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	Vector3 _lastObserverPosition;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextChunkCull;
	TimeUntil _nextProximityStats;
	TimeUntil _nextInstanceLod;
	Vector3 _lastLodObserverPosition;
	bool _hasLodObserverPosition;

	public void BeginPopulate( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig, ThornsFoliageBiomeSampler sharedSampler = null )
	{
		_stats.PopulateStarted = true;
		_stats.PopulateComplete = false;
		_stats.LastError = "";

		if ( !Config.SpawnOnTerrainReady )
		{
			_stats.LastError = "SpawnOnTerrainReady is false";
			Log.Warning( "[Thorns Foliage] BeginPopulate skipped — SpawnOnTerrainReady is false." );
			return;
		}

		if ( _populating )
		{
			_stats.LastError = "already populating";
			Log.Warning( "[Thorns Foliage] BeginPopulate skipped — already populating." );
			return;
		}

		if ( !terrain.IsValid() || field is null )
		{
			_stats.LastError = "invalid terrain or field";
			Log.Error( "[Thorns Foliage] BeginPopulate failed — invalid terrain or heightfield." );
			return;
		}

		_terrain = terrain;

		if ( Config.VerboseDebug )
		{
			Log.Info( $"[Thorns Foliage] BeginPopulate terrainOrigin={terrain.GameObject.WorldPosition} size={terrain.TerrainSize:F0} height={terrain.TerrainHeight:F0}" );
			Log.Info( $"[Thorns Foliage] Chunk grid ~{ThornsFoliagePlacer.BuildChunkGrid( terrain, Config ).Count} cells, density={Config.GlobalDensity}, maxTreeSlopeDeg={Config.MaxTreeSlopeDegrees}" );
		}

		var models = ThornsFoliagePlacer.LoadModels( Config, _stats );
		if ( !models.IsValid )
		{
			_stats.ModelsLoaded = false;
			_stats.LastError = "model load failed";
			return;
		}

		_stats.ModelsLoaded = true;
		Clear();
		_populating = true;
		_totalInstances = 0;
		_session = new ThornsFoliagePopulateSession( Scene, GameObject, terrain, field, terrainConfig, Config, models, _stats, sharedSampler );
	}

	protected override void OnUpdate()
	{
		using ( ThornsPerfDebug.Scope( "ThornsFoliageFoundation" ) )
		{
			if ( _populating && _session is not null )
				TickPopulate();

			var observer = ResolveObserverPosition();

			if ( _ready && Config.VerboseDebug && _nextProximityStats )
				UpdatePlayerProximityStats();

			if ( !_ready || _chunks.Count == 0 )
				return;

			if ( _nextChunkCull )
				UpdateChunkCulling( observer );

			if ( ShouldUpdateInstanceLod( observer ) )
				UpdateStaggeredInstanceLod( observer );

			ThornsPerfDebug.FoliageChunksLoaded = _chunks.Count;
			ThornsPerfDebug.FoliageInstancesVisible = _totalInstances;
		}
	}

	bool ShouldUpdateInstanceLod( Vector3 observer )
	{
		if ( !_nextInstanceLod )
		{
			_nextInstanceLod = Config.InstanceLodIntervalSeconds;
			_lastLodObserverPosition = observer;
			_hasLodObserverPosition = true;
			return true;
		}

		if ( !_hasLodObserverPosition )
		{
			_lastLodObserverPosition = observer;
			_hasLodObserverPosition = true;
			return true;
		}

		if ( (observer - _lastLodObserverPosition).LengthSquared >= Config.InstanceLodMinMoveInches * Config.InstanceLodMinMoveInches )
		{
			_lastLodObserverPosition = observer;
			return true;
		}

		return false;
	}

	void TickPopulate()
	{
		if ( _session.IsComplete )
		{
			_stats.PopulateComplete = true;
			_stats.InstancesSpawned = _totalInstances;
			_session.LogSummary( _totalInstances, _chunks.Count, _stats );

			if ( _totalInstances == 0 )
				Log.Warning( "[Thorns Foliage] No instances spawned — check log for ray misses / biome rejects." );

			UpdatePlayerProximityStats();

			_session = null;
			_populating = false;
			_ready = true;
			return;
		}

		var batch = _session.ProcessChunks( Config.ChunksPerFrame );
		ThornsPerfDebug.FoliageChunksGeneratedThisFrame += batch.Count;
		foreach ( var chunk in batch )
		{
			_chunks.Add( chunk );
			_totalInstances += chunk.InstanceCount;
			_stats.ChunksProcessed++;

			if ( Config.VerboseDebug && (chunk.InstanceCount == 0 || _stats.ChunksProcessed <= 3) )
				Log.Info( $"[Thorns Foliage] Chunk {chunk.Cell.x},{chunk.Cell.y} center={chunk.Center} instances={chunk.InstanceCount}" );
		}
	}

	void UpdatePlayerProximityStats()
	{
		_nextProximityStats = 0.5f;
		var observer = _lastObserverPosition;
		_stats.EnabledChunksNearPlayer = 0;
		_stats.NearestInstanceDistance = -1f;
		_stats.NearestInstancePosition = null;

		foreach ( var chunk in _chunks )
		{
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( observer.Distance( chunk.Center ) < Config.ChunkSizeInches * 1.5f )
				_stats.EnabledChunksNearPlayer++;

			foreach ( var child in chunk.Root.Children )
			{
				if ( !child.IsValid() )
					continue;

				var dist = observer.Distance( child.WorldPosition );
				if ( _stats.NearestInstanceDistance < 0f || dist < _stats.NearestInstanceDistance )
				{
					_stats.NearestInstanceDistance = dist;
					_stats.NearestInstancePosition = child.WorldPosition;
				}
			}
		}
	}

	void UpdateChunkCulling()
	{
		UpdateChunkCulling( ResolveObserverPosition() );
	}

	void UpdateChunkCulling( Vector3 observer )
	{
		_nextChunkCull = 0.2f;
		var cull = Config.CullDistanceInches;
		var hysteresis = Config.CullHysteresisInches;
		var cullSq = cull * cull;
		var outer = cull + hysteresis;
		var outerSq = outer * outer;

		foreach ( var chunk in _chunks )
		{
			if ( !chunk.Root.IsValid() )
				continue;

			var distanceSq = (chunk.Center - observer).LengthSquared;
			var enabled = chunk.Root.Enabled;

			if ( enabled && distanceSq > outerSq )
			{
				chunk.Root.Enabled = false;
				ResetChunkLodState( chunk );
			}
			else if ( !enabled && distanceSq < cullSq )
				chunk.Root.Enabled = true;
		}
	}

	void UpdateStaggeredInstanceLod( Vector3 observer )
	{
		var hideOuter = Config.TreeLodHideDistanceInches + Config.LodDistanceHysteresisInches;
		var hideOuterSq = hideOuter * hideOuter;
		var enabledChunks = 0;
		for ( int i = 0; i < _chunks.Count; i++ )
		{
			var idx = (_lodChunkCursor + i) % _chunks.Count;
			var chunk = _chunks[idx];
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( (chunk.Center - observer).LengthSquared > hideOuterSq )
				continue;

			ThornsFoliageLod.ApplyChunk( chunk, observer, Config );
			enabledChunks++;
			if ( enabledChunks >= Config.LodChunksUpdatedPerFrame )
				break;
		}

		_lodChunkCursor = _chunks.Count > 0 ? (_lodChunkCursor + Config.LodChunksUpdatedPerFrame) % _chunks.Count : 0;
	}

	static void ResetChunkLodState( ThornsFoliageChunkData chunk )
	{
		if ( !chunk.Root.IsValid() )
			return;

		foreach ( var child in chunk.Root.Children )
		{
			if ( !child.IsValid() )
				continue;

			var tag = child.Components.Get<ThornsFoliageInstance>();
			if ( tag is null )
				continue;

			tag.LodState = 0;
			child.Enabled = true;

			var renderer = tag.Renderer;
			if ( renderer is not null && renderer.IsValid() )
				renderer.Enabled = true;

			var harvestNode = child.Components.Get<ThornsResourceNode>();
			if ( harvestNode.IsValid && harvestNode.ResourceKind == ThornsResourceKind.Wood && !harvestNode.IsDepleted )
				harvestNode.ApplyFoliageLodVisual( visible: true, castShadows: true );
		}
	}

	Vector3 ResolveObserverPosition()
	{
		_lastObserverPosition = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		return _lastObserverPosition;
	}

	public string GetHudSummary()
	{
		if ( _populating )
			return $"Foliage: spawning {_stats.ChunksProcessed}/{_stats.ChunksTotal} chunks, {_totalInstances} instances";

		if ( !_stats.PopulateStarted )
			return "Foliage: not started";

		if ( !_stats.ModelsLoaded )
			return "Foliage: models failed to load";

		if ( !_ready )
			return "Foliage: waiting";

		return $"Foliage: {_totalInstances} trees / {_chunks.Count} chunks";
	}

	public string GetHudDetail()
	{
		if ( !_ready && !_populating )
			return _stats.LastError;

		var nearest = _stats.NearestInstanceDistance >= 0f
			? $"nearest tree { _stats.NearestInstanceDistance:F0} in"
			: "nearest tree: none";

		var scaleHint = _stats.LastSpawnScale > 0f ? $"lastScale {_stats.LastSpawnScale:F0}" : "lastScale —";
		return $"rays miss {_stats.RayMisses} | biome {_stats.BiomeRejected} | slope flat {_stats.SlopeFlatRejected} | clusters {_stats.ClustersPlaced} | near {_stats.EnabledChunksNearPlayer} | {nearest} | {scaleHint} | last {_stats.LastSpawnSpecies}";
	}

	public void Clear()
	{
		foreach ( var chunk in _chunks )
		{
			if ( chunk.Root.IsValid() )
				chunk.Root.Destroy();
		}

		_chunks.Clear();
		_session = null;
		_populating = false;
		_ready = false;
		_totalInstances = 0;

		foreach ( var child in GameObject.Children )
		{
			if ( child.Name.Equals( "Thorns Foliage", StringComparison.OrdinalIgnoreCase ) )
				child.Destroy();
		}
	}

	protected override void OnDestroy()
	{
		Clear();
	}
}
