namespace Terraingen.Minerals;

using Terraingen;
using Terraingen.Foliage;
using Terraingen.TerrainGen;
using Terraingen.World;

[Title( "Thorns Mineral Scatter" )]
[Category( "Terrain" )]
[Icon( "terrain" )]
public sealed class ThornsMineralFoundation : Component
{
	[Property] public ThornsMineralConfig Config { get; set; } = new();

	readonly List<ThornsMineralChunkData> _chunks = new();
	readonly ThornsMineralDebugStats _stats = new();

	readonly List<ThornsMineralChunkData> _populateBatchScratch = new( 64 );
	ThornsMineralPopulateSession _session;
	ThornsMineralWorldService _mineralWorld;
	ThornsMineralInstancedRenderer _instancedRenderer;
	ThornsFoliageBiomeSampler _sampler;
	Model _model;
	GameObject _mineralRoot;
	Terrain _terrain;
	int _totalInstances;
	bool _populating;
	bool _ready;
	bool _playerPocketSpawned;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	Vector3 _lastObserverPosition;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextChunkCull;
	TimeUntil _nextColliderSync;
	int _chunkCullCursor;

	public bool IsPopulatingNearPlayer =>
		_populating
		&& _session is not null
		&& _session.CellsRemaining > 0
		&& _stats.ChunksProcessed < Math.Min( 20, Math.Max( 1, _stats.ChunksTotal ) );

	public bool HasPlayerPocket => _playerPocketSpawned;

	public bool IsReadyForPlayerPocket =>
		_ready
		&& _terrain is not null && _terrain.IsValid()
		&& _model.IsValid
		&& _mineralRoot is not null && _mineralRoot.IsValid()
		&& _sampler is not null;

	public void BeginPopulate(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageConfig foliageConfig,
		ThornsFoliageBiomeSampler sharedSampler = null )
	{
		_stats.PopulateStarted = true;
		_stats.PopulateComplete = false;
		_stats.LastError = "";
		Config.NormalizeScatterModel();

		if ( !Config.SpawnOnTerrainReady )
		{
			_stats.LastError = "SpawnOnTerrainReady is false";
			Log.Warning( "[Thorns Minerals] Skipped — SpawnOnTerrainReady is false." );
			return;
		}

		if ( _populating )
			return;

		if ( !terrain.IsValid() || field is null )
		{
			_stats.LastError = "invalid terrain or field";
			Log.Error( "[Thorns Minerals] BeginPopulate failed — invalid terrain or heightfield." );
			return;
		}

		_terrain = terrain;
		ThornsMineralPerformance.ApplyTerrainScaledDistances( Config, terrain.TerrainSize );
		var model = ThornsMineralPlacer.LoadModel( Config, _stats );
		if ( !model.IsValid )
		{
			_stats.ModelsLoaded = false;
			Log.Error( $"[Thorns Minerals] Aborting populate — {_stats.LastError}" );
			return;
		}

		_stats.ModelsLoaded = true;
		Clear();

		_mineralWorld = Components.Get<ThornsMineralWorldService>() ?? Components.Create<ThornsMineralWorldService>();
		_mineralWorld.Begin();
		MineralPlacerContext.ActiveWorld = _mineralWorld;

		if ( Config.UseInstancedMinerals )
		{
			_instancedRenderer = Components.Get<ThornsMineralInstancedRenderer>()
			                       ?? Components.Create<ThornsMineralInstancedRenderer>();
			_instancedRenderer.Begin( model, Config );
		}

		_populating = true;
		_totalInstances = 0;
		Config.WorldSeed = terrainConfig.WorldSeed;

		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, foliageConfig );
		_model = model;

		_session = new ThornsMineralPopulateSession(
			Scene,
			GameObject,
			terrain,
			field,
			terrainConfig,
			foliageConfig,
			model,
			Config,
			_stats,
			_sampler,
			out _mineralRoot );

		Log.Info(
			$"[Thorns Minerals] Populating chunks={_stats.ChunksTotal} using '{_stats.LoadedModelPath}' (config={Config.ScatterModel})." );
	}

	protected override void OnValidate()
	{
		Config?.NormalizeScatterModel();
	}

	/// <summary>Spawn guaranteed stone/ore nodes around the local player after they join.</summary>
	public void TryScatterPlayerPocket()
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		var player = ThornsSceneObserver.FindLocalPlayerObject( Scene );
		if ( player is null || !player.IsValid() )
			return;

		ScatterPlayerPocket( player.WorldPosition );
	}

	public void ScatterPlayerPocket( Vector3 worldPosition )
	{
		if ( _playerPocketSpawned || _terrain is null || !_terrain.IsValid() || _sampler is null )
			return;

		if ( !_model.IsValid || _mineralRoot is null || !_mineralRoot.IsValid() )
			return;

		var pocket = ThornsMineralPlacer.ScatterPlayerPocket(
			Scene,
			_mineralRoot,
			_terrain,
			_sampler,
			_model,
			Config,
			_stats,
			worldPosition );

		_playerPocketSpawned = true;
		_totalInstances += pocket.Stone + pocket.Ore;

		if ( pocket.Stone + pocket.Ore > 0 )
		{
			Log.Info(
				$"[Thorns Minerals] Player pocket near {worldPosition:F0}: +{pocket.Stone} stone, +{pocket.Ore} ore (radius {Config.PlayerPocketRadiusMeters:F0}m)." );
		}
	}

	protected override void OnUpdate()
	{
		if ( _populating && _session is not null )
			TickPopulate();

		if ( _ready && !_playerPocketSpawned )
			TryScatterPlayerPocket();

		if ( !_ready || _chunks.Count == 0 )
			return;

		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( _nextChunkCull )
			UpdateChunkCulling( ResolveObserverPosition() );

		if ( _nextColliderSync )
			SyncNearbyHarvestColliders( ResolveObserverPosition() );
	}

	void SyncNearbyHarvestColliders( Vector3 observer )
	{
		_nextColliderSync = 0.35f;
		_mineralWorld?.SyncHarvestCollisionProximity( observer, Config.HarvestCollisionDistanceInches );
	}

	void TickPopulate()
	{
		if ( _session.IsComplete )
		{
			MarkPopulateReady();
			TryScatterPlayerPocket();
			_session = null;
			_populating = false;
			return;
		}

		var chunkBudget = ResolvePopulateChunkBudget();
		_populateBatchScratch.Clear();
		_populateBatchScratch.AddRange( _session.ProcessChunks( chunkBudget ) );
		_populateBatchScratch.AddRange( _session.ProcessDeferred( Config.DeferredChunksPerFrame ) );
		foreach ( var chunk in _populateBatchScratch )
		{
			_chunks.Add( chunk );
			_totalInstances += chunk.InstanceCount;
			_stats.ChunksProcessed++;
			if ( chunk.Instances is not null )
				_instancedRenderer?.RegisterChunk( chunk.Instances );
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

	void MarkPopulateReady()
	{
		if ( _ready )
			return;

		_stats.PopulateComplete = true;
		_stats.InstancesSpawned = _totalInstances;
		_session?.LogSummary( _totalInstances, _chunks.Count, _stats );

		if ( _totalInstances == 0 )
			Log.Warning( "[Thorns Minerals] No stone/ore props spawned — check biome filters and scatter model." );
		if ( _stats.ChunksProcessed < _stats.ChunksTotal )
			Log.Warning( $"[Thorns Minerals] Populate finished early — {_stats.ChunksProcessed}/{_stats.ChunksTotal} chunks." );

		_ready = true;

		if ( _mineralWorld is not null && _mineralWorld.IsValid()
		     && _mineralWorld.RegisteredNodeCount == 0 && _totalInstances > 0 )
		{
			_mineralWorld.RescanFromRoot( _mineralRoot, _model );
			Log.Warning(
				$"[Thorns Minerals] Rescanned {_mineralWorld.RegisteredNodeCount} harvest nodes (late world service bind)." );
		}

		TryScatterPlayerPocket();

		var observer = ResolveObserverPosition();
		if ( observer.LengthSquared > 1f )
			_mineralWorld?.SyncHarvestCollisionProximity( observer, Config.HarvestCollisionDistanceInches );
	}

	/// <summary>Prioritize and fill chunks near the joining player.</summary>
	public void OnLocalPlayerReady( Vector3 playerPosition )
	{
		if ( _session is null )
			return;

		_session.PrioritizeCellsNear( playerPosition );
		var burst = Math.Min( _session.CellsRemaining, 48 );
		var batch = _session.ProcessChunks( burst );

		foreach ( var chunk in batch )
		{
			_chunks.Add( chunk );
			_totalInstances += chunk.InstanceCount;
			_stats.ChunksProcessed++;
			if ( chunk.Instances is not null )
				_instancedRenderer?.RegisterChunk( chunk.Instances );
		}

		if ( batch.Count > 0 )
		{
			Log.Info(
				$"[Thorns Minerals] Player join: {batch.Count} chunk(s) near spawn, {_totalInstances} props ({_stats.ChunksProcessed}/{_stats.ChunksTotal} total)." );
		}

		_mineralWorld?.SyncHarvestCollisionProximity( playerPosition, Config.HarvestCollisionDistanceInches );
	}

	void UpdateChunkCulling( Vector3 observer )
	{
		_nextChunkCull = 0.35f;
		_lastObserverPosition = observer;
		var cullSq = Config.CullDistanceInches * Config.CullDistanceInches;
		var outerSq = (Config.CullDistanceInches + Config.CullHysteresisInches)
		              * (Config.CullDistanceInches + Config.CullHysteresisInches);
		var passCount = Math.Max( 1, Config.ChunksUpdatedPerCullPass );

		for ( var i = 0; i < passCount && _chunks.Count > 0; i++ )
		{
			var idx = (_chunkCullCursor + i) % _chunks.Count;
			var chunk = _chunks[idx];
			if ( !chunk.Root.IsValid() )
				continue;

			var distanceSq = (chunk.Center - observer).LengthSquared;
			var enabled = chunk.Root.Enabled;

			if ( enabled && distanceSq > outerSq )
				chunk.Root.Enabled = false;
			else if ( !enabled && distanceSq < cullSq )
				chunk.Root.Enabled = true;

			if ( Config.UseInstancedMinerals )
				continue;

			UpdateNodeShadows( chunk, observer );
		}

		_chunkCullCursor = _chunks.Count > 0 ? (_chunkCullCursor + passCount) % _chunks.Count : 0;
	}

	void UpdateNodeShadows( ThornsMineralChunkData chunk, Vector3 observer )
	{
		foreach ( var child in chunk.Root.Children )
		{
			if ( !child.IsValid() )
				continue;

			var tag = child.Components.Get<ThornsMineralInstance>();
			if ( tag is null )
				continue;

			var renderer = child.Components.Get<ModelRenderer>();
			if ( !renderer.IsValid() || !renderer.Enabled )
				continue;

			var distSq = (child.WorldPosition - observer).LengthSquared;
			var wantShadows = ThornsMineralShadowLod.ShouldCastShadow(
				distSq,
				tag.ShadowsEnabled,
				Config,
				tag.Kind == MineralKind.Stone );
			if ( tag.ShadowsEnabled == wantShadows )
				continue;

			tag.ShadowsEnabled = wantShadows;
			renderer.RenderType = wantShadows
				? ModelRenderer.ShadowRenderType.On
				: ModelRenderer.ShadowRenderType.Off;
		}
	}

	Vector3 ResolveObserverPosition()
		=> ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );

	public string GetDebugSummary() =>
		$"[Thorns Minerals] chunks={_chunks.Count} instances={_totalInstances} stone={_stats.StoneSpawned} ore={_stats.OreSpawned} reject biome/mat/ray={_stats.BiomeRejected}/{_stats.MaterialRejected}/{_stats.RayMisses}";

	public void Clear()
	{
		MineralPlacerContext.ActiveWorld = null;
		_mineralWorld?.Clear();
		_instancedRenderer?.Clear();

		foreach ( var chunk in _chunks )
		{
			if ( chunk.Root.IsValid() )
				chunk.Root.Destroy();
		}

		_chunks.Clear();
		_totalInstances = 0;
		_populating = false;
		_ready = false;
		_session = null;
		ThornsMineralTintMaterials.ClearCache();
		_sampler = null;
		_model = default;
		_mineralRoot = null;
		_mineralWorld = null;
		_playerPocketSpawned = false;
	}

	protected override void OnDestroy() => Clear();
}
