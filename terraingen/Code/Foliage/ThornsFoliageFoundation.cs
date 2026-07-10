namespace Terraingen.Foliage;

using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.Physics;
using Terraingen.TerrainGen;
using Terraingen.World;

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

	readonly List<ThornsFoliageChunkData> _populateBatchScratch = new( 64 );
	ThornsFoliagePopulateSession _session;
	ThornsFoliageInstancedRenderer _instancedRenderer;
	ThornsTreeWorldService _treeWorld;
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
	TimeUntil _nextTrunkColliderSync;
	TimeUntil _nextInstancedCollisionSync;

	public bool IsPopulatingNearPlayer =>
		_populating
		&& _session is not null
		&& _session.CellsRemaining > 0
		&& _stats.ChunksProcessed < Math.Min( 24, Math.Max( 1, _stats.ChunksTotal ) );
	Vector3 _lastLodObserverPosition;
	bool _hasLodObserverPosition;
	Vector3 _lastColliderSyncPosition;
	bool _hasColliderSyncPosition;

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
		Config.EnsureScaleLimits();
		ThornsFoliagePerformance.ApplyTerrainScaledDistances( Config, terrain.TerrainSize );
		if ( Config.VerboseDebug )
			Log.Info(
				$"[Thorns Foliage] Terrain LOD hide={Config.TreeLodHideDistanceInches:F0} cull={Config.CullDistanceInches:F0} collision={Config.TreeCollisionDistanceInches:F0} in" );

		if ( Config.VerboseDebug )
		{
			Log.Info( $"[Thorns Foliage] BeginPopulate terrainOrigin={terrain.GameObject.WorldPosition} size={terrain.TerrainSize:F0} height={terrain.TerrainHeight:F0}" );
			Log.Info(
				$"[Thorns Foliage] LOD bands hide={Config.TreeLodHideDistanceInches:F0} cull={Config.CullDistanceInches:F0} collision={Config.TreeCollisionDistanceInches:F0} in" );
			Log.Info( $"[Thorns Foliage] Chunk grid ~{ThornsFoliagePlacer.BuildChunkGrid( terrain, Config ).Count} cells, density={Config.GlobalDensity}, maxSlope={Config.MaxSlopeForTrees}" );
		}

		var models = ThornsFoliagePlacer.LoadModels( Config, _stats );
		if ( !models.IsValid )
		{
			_stats.ModelsLoaded = false;
			_stats.LastError = "model load failed";
			return;
		}

		_stats.ModelsLoaded = true;
		if ( Config.VerboseDebug )
		{
			var pine = models.Get( FoliageSpecies.Pine );
			var sampleScale = ThornsFoliageCloudModels.ComputeUniformScale(
				pine,
				Config.PineTargetHeightInches,
				Config,
				new Random( Config.FoliageSeed ) );
			var sampleHeight = ThornsFoliageCloudModels.EstimateWorldHeightInches(
				pine,
				sampleScale,
				Config.PineTargetHeightInches,
				Config );
			Log.Info(
				$"[Thorns Foliage] Pine spawn scale≈{sampleScale:F1}, estHeight≈{sampleHeight:F0} in "
				+ $"(meshH≈{ThornsFoliageCloudModels.MeshHeightInches( pine, Config ):F1} in, maxScale={Config.MaxTreeRenderScale:F0})." );
		}
		Clear();

		_treeWorld = Components.Get<ThornsTreeWorldService>() ?? Components.Create<ThornsTreeWorldService>();
		_treeWorld.Begin( models, Config, terrain );
		_treeWorld.EnsureActiveInstance();
		FoliagePlacerContext.ActiveTreeService = _treeWorld;

		if ( Config.UseInstancedTrees )
		{
			_instancedRenderer = Components.Get<ThornsFoliageInstancedRenderer>()
			                       ?? Components.Create<ThornsFoliageInstancedRenderer>();
			_instancedRenderer.Begin( models, Config );
		}

		_populating = true;
		_totalInstances = 0;
		TerraingenAnchoredPhysics.ResetTreeTrunkCollisionDebugLog();
		ThornsTreeTrunkCollision.InvalidateRefreshCache();
		ThornsTreeTrunkCollisionTuning.EnsureOn( GameObject );
		_session = new ThornsFoliagePopulateSession( Scene, GameObject, terrain, field, terrainConfig, Config, models, _stats, sharedSampler );
		Log.Info( $"[Thorns Foliage] Populating {_stats.ChunksTotal} chunk(s), density={Config.GlobalDensity}." );
	}

	protected override void OnUpdate()
	{
		if ( !Scene.IsValid() )
			return;

		if ( _populating && _session is not null )
			TickPopulate();

		var observer = ResolveObserverPosition();

		if ( _ready && Config.VerboseDebug && !Config.UseInstancedTrees && _nextProximityStats )
			UpdatePlayerProximityStats();

		if ( _chunks.Count == 0 )
			return;

		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( Config.UseInstancedTrees )
		{
			if ( _ready )
			{
				if ( !_nextInstancedCollisionSync )
				{
					_nextInstancedCollisionSync = 0.12f;
					SyncInstancedTreeCollisions( observer );
				}
			}

			return;
		}

		if ( _ready )
		{
			var updateLod = ShouldUpdateInstanceLod( observer );
			if ( updateLod )
			{
				UpdateNearbyShadowLod( observer );
				UpdateStaggeredInstanceLod( observer );
			}

			if ( _nextChunkCull )
				UpdateChunkCulling( observer );

			if ( _nextTrunkColliderSync )
				SyncNearbyTrunkColliders( observer );
			else if ( ShouldSyncTrunkCollidersOnMove( observer ) )
				SyncNearbyTrunkColliders( observer );
		}
	}

	bool ShouldSyncTrunkCollidersOnMove( Vector3 observer )
	{
		if ( !_hasColliderSyncPosition )
			return true;

		const float moveThreshold = 96f;
		return (observer - _lastColliderSyncPosition).LengthSquared >= moveThreshold * moveThreshold;
	}

	void SyncNearbyTrunkColliders( Vector3 observer )
	{
		_nextTrunkColliderSync = 0.15f;
		_lastColliderSyncPosition = observer;
		_hasColliderSyncPosition = true;
		ThornsFoliageLod.SyncTrunkColliderProximity(
			_chunks,
			observer,
			Config.TreeCollisionDistanceInches );
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
		try
		{
			if ( _session.IsComplete )
			{
				MarkPopulateReady();
				_session = null;
				_populating = false;
				return;
			}

			var chunkBudget = ResolvePopulateChunkBudget();
			_populateBatchScratch.Clear();
			_populateBatchScratch.AddRange( _session.ProcessChunks( chunkBudget ) );
			_populateBatchScratch.AddRange( _session.ProcessDeferred( Config.DeferredChunksPerFrame ) );
			ApplyPopulateBatch( _populateBatchScratch );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns Foliage] Populate tick failed ({e.GetType().Name}: {e.Message}) — stopping foliage spawn." );
			_stats.LastError = e.Message;
			_populating = false;
			_session = null;
		}
	}

	void MarkPopulateReady()
	{
		if ( _ready )
			return;

		_stats.PopulateComplete = true;
		_stats.InstancesSpawned = _totalInstances;
		_session?.LogSummary( _totalInstances, _chunks.Count, _stats );

		if ( _totalInstances == 0 )
			Log.Warning( "[Thorns Foliage] No tree instances spawned — check biome filters and terrain coverage." );
		if ( _stats.ChunksProcessed < _stats.ChunksTotal )
			Log.Warning( $"[Thorns Foliage] Populate finished early — {_stats.ChunksProcessed}/{_stats.ChunksTotal} chunks." );

		if ( !Config.UseInstancedTrees )
		{
			UpdatePlayerProximityStats();
			_treeWorld?.ReconcileSceneTreeRegistry( _chunks );
			ThornsFoliageLod.MigrateLegacyTrunkColliders( _chunks );
			var observer = ResolveObserverPosition();
			if ( observer.LengthSquared > 1f )
			{
				UpdateChunkCulling( observer );
				ThornsFoliageLod.SyncTrunkColliderProximity( _chunks, observer, Config.TreeCollisionDistanceInches );
				UpdateNearbyShadowLod( observer );
			}
		}
		else
		{
			var observer = ResolveObserverPosition();
			if ( observer.LengthSquared > 1f )
				_treeWorld?.SyncHarvestCollisionProximity( observer, Config.TreeCollisionDistanceInches );
		}

		ThornsTreeBillboardAssets.Configure( Config );

		_ready = true;
		ThornsWorldPersistence.Instance?.HostRestoreWorldResourcesOnce();
	}

	/// <summary>Prioritize and fill chunks near the joining player.</summary>
	public void OnLocalPlayerReady( Vector3 playerPosition )
	{
		if ( _session is null )
			return;

		try
		{
			_session.PrioritizeCellsNear( playerPosition, Config );
			var burst = Math.Min( _session.CellsRemaining, 48 );
			_populateBatchScratch.Clear();
			_populateBatchScratch.AddRange( _session.ProcessChunks( burst ) );
			_populateBatchScratch.AddRange( _session.ProcessDeferred( Config.DeferredChunksPerFrame ) );
			ApplyPopulateBatch( _populateBatchScratch );

			if ( !Config.UseInstancedTrees )
			{
				_lastObserverPosition = playerPosition;
				UpdateChunkCulling( playerPosition );
				ThornsFoliageLod.SyncTrunkColliderProximity(
					_chunks,
					playerPosition,
					Config.TreeCollisionDistanceInches );
			}
			else
			{
				_treeWorld?.SyncHarvestCollisionProximity( playerPosition, Config.TreeCollisionDistanceInches );
			}
		}
		catch ( Exception e )
		{
			Log.Error( e, "[Thorns Foliage] Player join populate burst failed." );
			_stats.LastError = e.Message;
		}

		if ( _session.CellsRemaining > 0 )
		{
			Log.Info(
				$"[Thorns Foliage] Player join: {_chunks.Count}/{_stats.ChunksTotal} chunks populated, {_totalInstances} instances — finishing rest in background." );
		}
		else if ( _chunks.Count > 0 )
		{
			Log.Info(
				$"[Thorns Foliage] Player join: all {_chunks.Count} chunks populated, {_totalInstances} instances." );
		}
	}

	int ResolvePopulateChunkBudget()
	{
		if ( _session is null )
			return Config.ChunksPerFrame;

		var remaining = _session.CellsRemaining;
		if ( remaining <= 0 )
			return 0;

		if ( remaining > 24 )
			return Math.Max( Config.ChunksPerFrame, 12 );

		return Math.Max( Config.ChunksPerFrame, remaining );
	}

	void ApplyPopulateBatch( List<ThornsFoliageChunkData> batch )
	{
		foreach ( var chunk in batch )
		{
			_chunks.Add( chunk );
			_totalInstances += chunk.InstanceCount;
			if ( chunk.Instances is not null )
			{
				_instancedRenderer?.RegisterChunk( chunk.Instances );
				_treeWorld?.RegisterChunk( chunk, ResolveFoliageRoot(), _terrain );
			}

			_stats.ChunksProcessed++;

			if ( Config.VerboseDebug && (chunk.InstanceCount == 0 || _stats.ChunksProcessed <= 3) )
				Log.Info( $"[Thorns Foliage] Chunk {chunk.Cell.x},{chunk.Cell.y} center={chunk.Center} instances={chunk.InstanceCount}" );
		}

		if ( !Config.UseInstancedTrees && _lastObserverPosition.LengthSquared > 1f )
		{
			UpdateChunkCulling( _lastObserverPosition );
			ThornsFoliageLod.SyncTrunkColliderProximity( batch, _lastObserverPosition, Config.TreeCollisionDistanceInches );
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
		_nextChunkCull = 0.35f;
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
			{
				chunk.Root.Enabled = true;
				ThornsFoliageLod.RestoreChunkColliders( chunk, observer, Config.TreeCollisionDistanceInches );
			}
		}
	}

	void UpdateNearbyShadowLod( Vector3 observer )
	{
		var shadowOuter = Config.TreeLodShadowDistanceInches + Config.LodDistanceHysteresisInches + Config.ChunkSizeInches;
		var shadowOuterSq = shadowOuter * shadowOuter;

		for ( var i = 0; i < _chunks.Count; i++ )
		{
			var chunk = _chunks[i];
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( (chunk.Center - observer).LengthSquared > shadowOuterSq )
				continue;

			ThornsFoliageLod.ApplyChunk( chunk, observer, Config );
		}
	}

	void UpdateStaggeredInstanceLod( Vector3 observer )
	{
		var enabledChunks = 0;
		var budget = Config.LodChunksUpdatedPerFrame;
		var processed = 0;

		// Nearby chunks get LOD first; distant chunks rotate in on a staggered budget.
		for ( var i = 0; i < _chunks.Count && processed < budget * 2; i++ )
		{
			var chunk = _chunks[i];
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( !IsChunkWithinTreeCollisionRange( chunk, observer ) )
				continue;

			ThornsFoliageLod.ApplyChunk( chunk, observer, Config );
			processed++;
		}

		for ( int i = 0; i < _chunks.Count && enabledChunks < budget; i++ )
		{
			var idx = (_lodChunkCursor + i) % _chunks.Count;
			var chunk = _chunks[idx];
			if ( !chunk.Root.IsValid() || !chunk.Root.Enabled )
				continue;

			if ( IsChunkWithinTreeCollisionRange( chunk, observer ) )
				continue;

			ThornsFoliageLod.ApplyChunk( chunk, observer, Config );
			enabledChunks++;
		}

		_lodChunkCursor = _chunks.Count > 0 ? (_lodChunkCursor + budget) % _chunks.Count : 0;
	}

	bool IsChunkWithinTreeCollisionRange( ThornsFoliageChunkData chunk, Vector3 observer )
	{
		var half = Config.ChunkSizeInches * 0.5f;
		// Expand the chunk AABB so trees on edges still refresh when the player is in chop range.
		var range = Config.TreeCollisionDistanceInches + 120f;
		var minX = chunk.Center.x - half - range;
		var maxX = chunk.Center.x + half + range;
		var minY = chunk.Center.y - half - range;
		var maxY = chunk.Center.y + half + range;
		return observer.x >= minX && observer.x <= maxX && observer.y >= minY && observer.y <= maxY;
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
			{
				renderer.Enabled = true;
				renderer.RenderType = ModelRenderer.ShadowRenderType.On;
			}

			if ( tag.BillboardRenderer is { IsValid: true } billboard )
				billboard.Enabled = false;
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

	void SyncInstancedTreeCollisions( Vector3 observer )
	{
		if ( _treeWorld is null || !_treeWorld.IsValid() || observer.LengthSquared < 1f )
			return;

		_treeWorld.SyncHarvestCollisionProximity( observer, Config.TreeCollisionDistanceInches );
	}

	GameObject ResolveFoliageRoot()
	{
		foreach ( var child in GameObject.Children )
		{
			if ( child.IsValid() && child.Name.Equals( "Thorns Foliage", StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		return GameObject;
	}

	public void Clear()
	{
		FoliagePlacerContext.ActiveTreeService = null;
		_treeWorld?.Clear();
		_instancedRenderer?.Clear();

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
