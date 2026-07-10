namespace Terraingen.Clutter;

using Terraingen.Core;

using System.Diagnostics;
using Terraingen;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.Rendering;
using Terraingen.TerrainGen;

/// <summary>
/// Client-only cosmetic chunk streamer for grass, stones, and small ground detail.
/// It owns clutter. Tree foliage remains in the foliage system.
/// </summary>
[Title( "Thorns Clutter" )]
[Category( "Terrain" )]
[Icon( "grass" )]
public sealed class ThornsClutterFoundation : Component
{
	readonly struct ClutterModelChoice
	{
		public Model Model { get; init; }
		public bool IsRock { get; init; }
		public string Label { get; init; }
	}

	[Property] public ThornsClutterConfig Config { get; set; } = new();

	readonly Dictionary<Vector2Int, ThornsClutterChunk> _chunks = new();
	readonly Queue<GameObject> _pool = new();
	readonly ThornsClutterStats _stats = new();
	readonly List<Vector2Int> _wantedScratch = new();
	readonly HashSet<Vector2Int> _wantedCells = new();
	readonly List<Vector2Int> _removeScratch = new();
	readonly List<GameObject> _childScratch = new();
	readonly List<(Vector2Int Cell, float DistSq)> _buildQueueScratch = new();
	readonly ThornsClutterReveal _reveal = new();
	int _pendingChunkBuildCount;
	TimeUntil _spawnBuildWarmup;

	public int PendingChunkBuildCount => _pendingChunkBuildCount;
	public int ActiveRevealCount => _reveal.ActiveCount;
	public bool IsNearbyStreamingSettled => _pendingChunkBuildCount <= 0 && _reveal.ActiveCount <= 0;
	public bool IsSpawnWarmupActive => _spawnBuildWarmup > 0f || ThornsNearbyCosmeticsReadiness.IsWaiting;

	Terrain _terrain;
	ThornsTerrainConfig _terrainConfig;
	ThornsFoliageBiomeSampler _sampler;
	GameObject _root;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Model[] _grassModels = Array.Empty<Model>();
	Model _localGrassModel;
	Model[] _extraDetailModels = Array.Empty<Model>();
	readonly List<ClutterModelChoice> _modelChoices = new();
	readonly ThornsClutterMixDebug _mixDebug = new();
	TimeUntil _nextMixDebugLog;
	Model _rockA;
	Model _rockB;
	ThornsMineralConfig _rockTintConfig = new();
	TimeUntil _nextRefresh;
	TimeUntil _nextDebug;
	bool _ready;
	bool _loggedPlacement;
	bool _pendingInitialRefresh;
	bool _pendingStreamStart;
	bool _pendingStreamStop;
	Terrain _queuedTerrain;
	HeightmapField _queuedField;
	ThornsTerrainConfig _queuedTerrainConfig;
	ThornsFoliageBiomeSampler _queuedSampler;
	ThornsClutterInstancedRenderer _instancedRenderer;
	ThornsClutterChunkInstances _buildingChunkInstances;

	static readonly string[] LocalGrassFallbackPaths =
	{
		"models/clutter/grass_common_short.vmdl",
		"models/clutter/grass_common_short1.vmdl",
		"models/clutter/grass_common_short2.vmdl",
	};

	void EnsureConfig()
	{
		Config ??= new ThornsClutterConfig();
		Config.RockModelA ??= ThornsMineralConfig.DefaultScatterModel;
		Config.RockModelB ??= "";
	}

	static bool IsUsableModel( Model model )
		=> ThornsModelResourceLoad.IsUsable( model ) && ThornsFoliageCloudModels.HasRenderableMesh( model );

	static bool IsUsableRockModel( Model model ) => ThornsModelResourceLoad.IsUsable( model );

	/// <summary>Queue streaming — scene objects are created on the next Update tick (safe during bootstrap Start).</summary>
	public void BeginStreaming( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig, ThornsFoliageBiomeSampler sharedSampler = null )
	{
		EnsureConfig();
		_pendingStreamStop = false;
		_queuedTerrain = terrain;
		_queuedField = field;
		_queuedTerrainConfig = terrainConfig;
		_queuedSampler = sharedSampler;
		_pendingStreamStart = true;
	}

	/// <summary>Queue a teardown — avoids Destroy during bootstrap Start.</summary>
	public void RequestStop()
	{
		_pendingStreamStart = false;
		_pendingStreamStop = true;
	}

	void ActivateStreaming()
	{
		Clear();
		EnsureConfig();

		var terrain = _queuedTerrain;
		var field = _queuedField;
		var terrainConfig = _queuedTerrainConfig;
		var sharedSampler = _queuedSampler;

		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( Scene.IsEditor || !Config.Enabled || !terrain.IsValid() || field is null )
			return;

		if ( !Config.PlaceGrass && !Config.PlaceCloudDetail && !Config.PlaceRocks )
			return;

		_terrain = terrain;
		_terrainConfig = terrainConfig;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, BuildSamplerConfig() );
		_grassModels = LoadDetailModels();
		LoadRockModels();
		BuildModelChoiceTable();

		var hasGrassModels = _modelChoices.Count > 0;
		if ( (Config.PlaceGrass || Config.PlaceCloudDetail) && !hasGrassModels && Config.PlaceRocks && !IsUsableRockModel( _rockA ) && !IsUsableRockModel( _rockB ) )
		{
			Log.Warning( "[Thorns Clutter] No valid clutter models configured." );
			return;
		}

		if ( !hasGrassModels && !IsUsableRockModel( _rockA ) && !IsUsableRockModel( _rockB ) )
		{
			Log.Warning( "[Thorns Clutter] No valid clutter models configured." );
			return;
		}

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Client Clutter";
		_root.Parent = GameObject;
		_ready = true;
		_loggedPlacement = false;
		_pendingInitialRefresh = true;
		_mixDebug.Reset();
		_nextMixDebugLog = 3f;
		LogModelChoicePool();
		if ( Config.UseInstancedMeshDetail )
		{
			_instancedRenderer = Components.Get<ThornsClutterInstancedRenderer>()
			                       ?? Components.Create<ThornsClutterInstancedRenderer>();
			_instancedRenderer.Begin( Config, CollectInstancedModels() );
		}

		if ( Config.UseGpuGrassBlades )
			Log.Info( "[Thorns Clutter] GPU grass blades enabled — grass_common_short may appear twice (mesh + instanced)." );
		else
			Log.Info( "[Thorns Clutter] GPU grass blades disabled — ground detail comes from mesh clutter only." );
	}

	protected override void OnUpdate()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		EnsureConfig();

		if ( _pendingStreamStop )
		{
			_pendingStreamStop = false;
			Clear();
			return;
		}

		if ( _pendingStreamStart )
		{
			_pendingStreamStart = false;
			try
			{
				Log.Info( "[Thorns Clutter] ActivateStreaming begin." );
				ActivateStreaming();
				Log.Info( "[Thorns Clutter] ActivateStreaming complete." );
			}
			catch ( Exception ex )
			{
				Log.Error( ex, "[Thorns Clutter] Rock scatter failed to start." );
				Clear();
			}

			return;
		}

		if ( !_ready || !Config.Enabled )
			return;

		if ( _pendingInitialRefresh )
		{
			if ( !ThornsSceneObserver.FindLocalPlayerObject( Scene ).IsValid() )
				return;

			_pendingInitialRefresh = false;
			Log.Info( "[Thorns Clutter] Local player found — streaming clutter around explorer." );
			_spawnBuildWarmup = 3.5f;
			RefreshStreaming();
		}
		else if ( _nextRefresh )
			RefreshStreaming();

		_reveal.Tick();

		if ( Config.ShowDebug && _nextDebug )
		{
			_nextDebug = 0.5f;
			Log.Info( GetDebugSummary() );
		}

		if ( Config.LogDetailMixDebug && _nextMixDebugLog )
		{
			_nextMixDebugLog = 3f;
			Log.Info( $"[Thorns Clutter Mix] {_mixDebug.BuildSummary( _stats.ActiveInstances )}" );
		}
	}

	void RefreshStreaming()
	{
		var observer = ResolveObserverPosition();
		var streamRadius = Config.ClutterRadius;
		var buildRadius = streamRadius + Config.ChunkSize * Math.Max( 0, Config.PreloadChunkRings );
		var streamRadiusSq = streamRadius * streamRadius;
		var buildRadiusSq = buildRadius * buildRadius;
		var hasPendingBuilds = _pendingChunkBuildCount > 0;
		_nextRefresh = hasPendingBuilds ? 0.08f : 0.15f;

		if ( !_loggedPlacement && _stats.GeneratedChunks == 0 )
			Log.Info( "[Thorns Clutter] RefreshStreaming begin." );

		var observerCell = WorldToCell( observer );
		var radiusChunksBuild = Math.Max( 1, (int)MathF.Ceiling( buildRadius / Config.ChunkSize ) );
		var chunkBudget = Config.ChunksBuiltPerRefresh;
		if ( ThornsNearbyCosmeticsReadiness.IsWaiting )
			chunkBudget = Math.Max( chunkBudget, 10 );
		else if ( _spawnBuildWarmup > 0f )
			chunkBudget = 3;

		_wantedScratch.Clear();
		_wantedCells.Clear();
		_buildQueueScratch.Clear();

		for ( var y = -radiusChunksBuild; y <= radiusChunksBuild; y++ )
		{
			for ( var x = -radiusChunksBuild; x <= radiusChunksBuild; x++ )
			{
				var cell = new Vector2Int( observerCell.x + x, observerCell.y + y );
				var center = CellCenter( cell );
				var distSq = DistanceSquared2D( center, observer );
				if ( distSq > buildRadiusSq )
					continue;

				_wantedCells.Add( cell );
				if ( distSq <= streamRadiusSq )
					_wantedScratch.Add( cell );

				if ( !_chunks.ContainsKey( cell ) )
					_buildQueueScratch.Add( (cell, distSq) );
			}
		}

		_buildQueueScratch.Sort( ( a, b ) => a.DistSq.CompareTo( b.DistSq ) );

		var chunksBuilt = 0;
		foreach ( var (cell, _) in _buildQueueScratch )
		{
			if ( chunksBuilt >= chunkBudget )
				break;

			BuildChunk( cell, observer );
			chunksBuilt++;
		}

		_removeScratch.Clear();
		foreach ( var pair in _chunks )
		{
			if ( !_wantedCells.Contains( pair.Key ) )
				_removeScratch.Add( pair.Key );
		}

		foreach ( var cell in _removeScratch )
			DestroyChunk( cell );

		_pendingChunkBuildCount = 0;
		foreach ( var (cell, _) in _buildQueueScratch )
		{
			if ( !_chunks.ContainsKey( cell ) )
				_pendingChunkBuildCount++;
		}

		_stats.ActiveChunks = _chunks.Count;
	}

	void BuildChunk( Vector2Int cell, Vector3 observer )
	{
		var watch = Stopwatch.StartNew();
		var center = CellCenter( cell );
		var root = Scene.CreateObject( true );
		root.Name = $"Clutter {cell.x}_{cell.y}";
		root.Parent = _root;

		_buildingChunkInstances = Config.UseInstancedMeshDetail && _instancedRenderer is not null
			? new ThornsClutterChunkInstances { Cell = cell, Center = center }
			: null;

		var rng = new Random( StableHash( Config.WorldSeed, cell.x, cell.y ) );
		var distance = ClosestDistanceToChunk( cell, observer );
		var band = DistanceBandDensity( distance );
		var detailTarget = ComputeDetailTarget( band );
		var target = detailTarget;
		if ( target <= 0 )
		{
			_chunks[cell] = new ThornsClutterChunk
			{
				Cell = cell,
				Center = center,
				Root = root,
			};
			return;
		}

		var count = 0;
		var grassCount = 0;
		var rockCount = 0;
		var attempts = Math.Max( target * 10, 48 );

		for ( var i = 0; i < attempts && count < target; i++ )
		{
			var wx = center.x + (rng.NextSingle() - 0.5f) * Config.ChunkSize;
			var wy = center.y + (rng.NextSingle() - 0.5f) * Config.ChunkSize;
			var sample = _sampler.Sample( wx, wy );
			if ( !AcceptSample( sample, rng, out var model, out var isGrass, out var modelLabel ) )
				continue;

			if ( isGrass && !_sampler.CanPlaceGrassOnTerrainMaterial( wx, wy ) )
			{
				_mixDebug.RecordRejectMaterial( modelLabel );
				continue;
			}

			if ( isGrass )
			{
				if ( !TryPlaceGrassClutter( root, model, modelLabel, wx, wy, observer, rng ) )
					continue;

				count++;
				grassCount++;
				continue;
			}

			var scale = ThornsClutterSurface.ComputeUniformScale( model, isGrass: false, Config, rng );
			if ( !ThornsClutterSurface.TrySampleWorld( _terrain, _sampler, wx, wy, model, scale, isGrass: false, Config, out var pos ) )
			{
				_stats.RejectedRay++;
				_mixDebug.RecordRejectRay();
				continue;
			}

			var yaw = rng.NextSingle() * 360f;
			var tilt = Config.RandomTiltDegrees * (rng.NextSingle() - 0.5f );
			var rot = Rotation.FromYaw( yaw ) * new Angles( tilt, 0f, 0f ).ToRotation();

			if ( TryAddInstancedClutter( model, pos, rot, scale ) )
			{
				count++;
				rockCount++;
				_mixDebug.RecordPlaced( modelLabel );
				continue;
			}

			var instance = AcquireInstance();
			instance.Parent = root;
			instance.Name = "Clutter Rock";
			instance.WorldPosition = pos;
			instance.WorldRotation = rot;
			instance.LocalScale = new Vector3( scale );
			instance.Enabled = true;

			var renderer = instance.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
			if ( renderer is null || !renderer.IsValid() )
				continue;

			renderer.Model = model;
			renderer.Enabled = true;
			ApplyRockMaterial( renderer, instance, model );
			ThornsWorldShadowUtil.DisableWorldShadows( renderer );
			QueueInstanceReveal( renderer, Color.White, pos, observer, rng );
			count++;
			rockCount++;
			_mixDebug.RecordPlaced( modelLabel );

			if ( !_loggedPlacement )
			{
				_loggedPlacement = true;
				var estHeight = model.Bounds.Size.z * scale;
				Log.Info( $"[Thorns Clutter] First rock '{modelLabel}' at {pos}, scale={scale:F2}, estHeight≈{estHeight:F0} in, bounds={model.Bounds.Size}" );
			}
		}

		watch.Stop();
		var builtInstances = _buildingChunkInstances;
		if ( builtInstances is not null && builtInstances.TotalCount > 0 )
			_instancedRenderer?.RegisterChunk( builtInstances );

		_buildingChunkInstances = null;
		_chunks[cell] = new ThornsClutterChunk
		{
			Cell = cell,
			Center = center,
			Root = root,
			InstanceCount = count,
			GrassCount = grassCount,
			RockCount = rockCount,
			LastBuildMilliseconds = (float)watch.Elapsed.TotalMilliseconds,
			Instances = builtInstances,
		};

		_stats.GeneratedChunks++;
		_stats.LastGenerationMs = (float)watch.Elapsed.TotalMilliseconds;
		_stats.ActiveInstances += count;
		_stats.ActiveGrass += grassCount;
		_stats.ActiveRocks += rockCount;
	}

	Model[] LoadDetailModels()
	{
		EnsureConfig();
		var models = new List<Model>();
		_localGrassModel = default;
		_extraDetailModels = Array.Empty<Model>();

		if ( Config.PlaceGrass && !string.IsNullOrWhiteSpace( Config.GrassModel ) )
		{
			var local = ThornsFoliageModelCache.Load( Config.GrassModel.Trim() );
			if ( IsUsableModel( local ) )
			{
				_localGrassModel = local;
				models.Add( local );
			}
		}

		if ( Config.PlaceCloudDetail && Config.CloudDetailModels is { Length: > 0 } )
		{
			var extraModels = new List<Model>();
			foreach ( var path in Config.CloudDetailModels )
			{
				if ( string.IsNullOrWhiteSpace( path ) )
					continue;

				var local = ThornsFoliageModelCache.Load( path.Trim() );
				if ( !IsUsableModel( local ) )
				{
					Log.Warning( $"[Thorns Clutter Mix] Extra detail model failed to load: '{path.Trim()}'" );
					continue;
				}

				extraModels.Add( local );
				models.Add( local );
				Log.Info( $"[Thorns Clutter Mix] Extra detail model loaded: {path.Trim()} → {local.ResourcePath ?? local.Name}" );
			}

			_extraDetailModels = extraModels.ToArray();
		}

		if ( models.Count == 0 && Config.PlaceGrass )
			TryEnsureLocalGrassFallback( models );

		return models.ToArray();
	}

	void TryEnsureLocalGrassFallback( List<Model> models )
	{
		var hadModels = models.Count;
		TryAddLocalGrassFallbackModels( models );
		if ( models.Count > hadModels )
			Log.Warning( "[Thorns Clutter] Using local grass fallback because configured grass models are unavailable." );
	}

	void TryAddLocalGrassFallbackModels( List<Model> models )
	{
		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		if ( !string.IsNullOrWhiteSpace( Config.GrassModel ) )
			paths.Add( Config.GrassModel.Trim() );

		foreach ( var path in LocalGrassFallbackPaths )
			paths.Add( path );

		foreach ( var path in paths )
		{
			var local = ThornsFoliageModelCache.Load( path );
			if ( !IsUsableModel( local ) )
				continue;

			if ( !IsUsableModel( _localGrassModel ) )
				_localGrassModel = local;

			if ( models.Any( m => m.IsValid && string.Equals( m.ResourcePath, local.ResourcePath, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			models.Add( local );
		}
	}

	void RefreshInstancedRendererModels()
	{
		if ( !Config.UseInstancedMeshDetail )
			return;

		_instancedRenderer ??= Components.Get<ThornsClutterInstancedRenderer>()
		                        ?? Components.Create<ThornsClutterInstancedRenderer>();
		_instancedRenderer.Begin( Config, CollectInstancedModels() );
	}

	void ClearGeneratedChunks()
	{
		foreach ( var chunk in _chunks.Values )
		{
			if ( chunk.Root.IsValid() )
				chunk.Root.Destroy();
		}

		_chunks.Clear();
		_stats.GeneratedChunks = 0;
		_stats.ActiveChunks = 0;
		_stats.ActiveInstances = 0;
		_stats.ActiveGrass = 0;
		_stats.ActiveRocks = 0;
		_pendingChunkBuildCount = 0;
	}

	void BuildModelChoiceTable()
	{
		_modelChoices.Clear();

		var useLocalGrass = Config.PlaceGrass;
		if ( useLocalGrass )
			AddLocalGrassChoices();

		if ( Config.PlaceCloudDetail )
		{
			foreach ( var extraModel in _extraDetailModels )
			{
				if ( IsUsableModel( extraModel ) )
					AddModelChoice( extraModel, isRock: false, extraModel.ResourcePath ?? extraModel.Name ?? "detail" );
			}
		}

		if ( Config.PlaceRocks )
		{
			if ( IsUsableRockModel( _rockA ) )
				AddModelChoice( _rockA, isRock: true, Config.RockModelA?.Trim() ?? _rockA.ResourcePath ?? "rockA" );
			if ( IsUsableRockModel( _rockB ) )
				AddModelChoice( _rockB, isRock: true, Config.RockModelB?.Trim() ?? _rockB.ResourcePath ?? "rockB" );
		}
	}

	void AddLocalGrassChoices()
	{
		var added = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var model in _grassModels )
		{
			if ( !IsUsableModel( model ) )
				continue;

			var label = model.ResourcePath ?? model.Name ?? Config.GrassModel ?? "grass_common_short";
			if ( !added.Add( label ) )
				continue;

			AddModelChoice( model, isRock: false, label );
		}

		if ( !IsUsableModel( _localGrassModel ) )
			return;

		var primaryLabel = _localGrassModel.ResourcePath ?? _localGrassModel.Name ?? Config.GrassModel ?? "grass_common_short";
		if ( added.Add( primaryLabel ) )
			AddModelChoice( _localGrassModel, isRock: false, primaryLabel );
	}

	void LogModelChoicePool()
	{
		if ( _modelChoices.Count == 0 )
		{
			Log.Warning( "[Thorns Clutter Mix] Model pool empty — no mesh clutter will spawn." );
			return;
		}

		var labels = string.Join( ", ", _modelChoices.Select( c => c.Label ) );
		var expected = 1f / _modelChoices.Count;
		Log.Info( $"[Thorns Clutter Mix] Pool={_modelChoices.Count} model(s), expected pick rate ≈{expected * 100f:F1}% each: {labels}" );

		if ( Config.PlaceGrass && !IsUsableModel( _localGrassModel ) )
			Log.Warning( $"[Thorns Clutter Mix] PlaceGrass=true but local grass failed to load: '{Config.GrassModel}'" );

		if ( Config.PlaceRocks )
		{
			if ( !IsUsableModel( _rockA ) )
				Log.Warning( $"[Thorns Clutter Mix] RockModelA failed to load: '{Config.RockModelA}'" );
			if ( !string.IsNullOrWhiteSpace( Config.RockModelB ) && !IsUsableModel( _rockB ) )
				Log.Warning( $"[Thorns Clutter Mix] RockModelB failed to load: '{Config.RockModelB}'" );
		}
	}

	void AddModelChoice( Model model, bool isRock, string label )
	{
		if ( !IsUsableModel( model ) )
			return;

		_modelChoices.Add( new ClutterModelChoice
		{
			Model = model,
			IsRock = isRock,
			Label = string.IsNullOrWhiteSpace( label ) ? model.ResourcePath ?? model.Name ?? "model" : label.Trim(),
		} );
	}

	int ComputeDetailTarget( float band )
	{
		if ( band <= 0f || _modelChoices.Count == 0 )
			return 0;

		if ( Config.DetailInstancesPerChunk > 0 )
			return Math.Max( 0, (int)MathF.Round( Config.DetailInstancesPerChunk * band ) );

		var legacy = Config.MaxInstancesPerChunk * Config.DensityMultiplier * band;
		if ( Config.PlaceGrass || Config.PlaceCloudDetail )
			legacy *= Config.GrassInstanceMultiplier;

		return Math.Max( 0, (int)MathF.Round( legacy ) );
	}

	bool TryPlaceGrassClutter(
		GameObject chunkRoot,
		Model model,
		string modelLabel,
		float wx,
		float wy,
		Vector3 observer,
		Random rng )
	{
		if ( !IsUsableModel( model ) )
			return false;

		var scale = ThornsClutterSurface.ComputeUniformScale( model, isGrass: true, Config, rng );
		if ( !ThornsClutterSurface.TrySampleWorldForGrass( _terrain, _sampler, wx, wy, model, scale, Config, out var pos, out var surfaceNormal ) )
		{
			_stats.RejectedRay++;
			_mixDebug.RecordRejectRay();
			return false;
		}

		var yaw = rng.NextSingle() * 360f;
		var align = Rotation.LookAt( surfaceNormal, Vector3.Up ) * Rotation.From( 90f, 0f, 0f );
		var rot = align * Rotation.FromYaw( yaw );

		if ( TryAddInstancedClutter( model, pos, rot, scale ) )
		{
			_mixDebug.RecordPlaced( modelLabel );
			if ( !_loggedPlacement )
			{
				_loggedPlacement = true;
				var estHeight = model.Bounds.Size.z * scale;
				Log.Info( $"[Thorns Clutter] First instanced foliage '{modelLabel}' at {pos}, scale={scale:F2}, estHeight≈{estHeight:F0} in" );
			}

			return true;
		}

		var instance = AcquireInstance();
		instance.Parent = chunkRoot;
		instance.Name = "Clutter Grass";
		instance.WorldPosition = pos;
		instance.WorldRotation = rot;
		instance.LocalScale = new Vector3( scale );
		instance.Enabled = true;

		var renderer = instance.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( renderer is null || !renderer.IsValid() )
			return false;

		renderer.Model = model;
		renderer.Enabled = true;
		if ( !IsUsableModel( model ) )
			return false;

		ThornsWorldShadowUtil.DisableWorldShadows( renderer );
		QueueInstanceReveal( renderer, Color.White, pos, observer, rng );
		_mixDebug.RecordPlaced( modelLabel );

		if ( !_loggedPlacement )
		{
			_loggedPlacement = true;
			var estHeight = model.Bounds.Size.z * scale;
			Log.Info( $"[Thorns Clutter] First foliage '{modelLabel}' at {pos}, scale={scale:F2}, estHeight≈{estHeight:F0} in, bounds={model.Bounds.Size}" );
		}

		return true;
	}

	bool TryPickDetailModel( Random rng, bool rocksOnly, out Model model, out bool isGrass, out string label )
	{
		model = null;
		isGrass = false;
		label = null;
		if ( _modelChoices.Count == 0 )
			return false;

		var eligibleCount = 0;
		foreach ( var choice in _modelChoices )
		{
			if ( rocksOnly && !choice.IsRock )
				continue;

			eligibleCount++;
		}

		if ( eligibleCount <= 0 )
			return false;

		var pick = rng.Next( eligibleCount );
		var index = 0;
		foreach ( var choice in _modelChoices )
		{
			if ( rocksOnly && !choice.IsRock )
				continue;

			if ( index == pick )
			{
				model = choice.Model;
				isGrass = !choice.IsRock;
				label = choice.Label;
				_mixDebug.RecordPick( label );
				return IsUsableModel( model );
			}

			index++;
		}

		return false;
	}

	void LoadRockModels()
	{
		EnsureConfig();
		_rockA = default;
		_rockB = default;
		if ( !Config.PlaceRocks )
			return;

		var primaryRock = Config.RockModelA ?? ThornsMineralConfig.DefaultScatterModel;
		var paths = new List<string>();
		if ( !string.IsNullOrWhiteSpace( primaryRock ) )
			paths.Add( primaryRock.Trim() );
		if ( !string.IsNullOrWhiteSpace( Config.RockModelB ) )
			paths.Add( Config.RockModelB.Trim() );

		foreach ( var fallback in new[] { ThornsMineralConfig.DefaultScatterModel, "models/clutter/rock1.vmdl", "models/clutter/rock2.vmdl" } )
		{
			if ( !paths.Contains( fallback, StringComparer.OrdinalIgnoreCase ) )
				paths.Add( fallback );
		}

		foreach ( var path in paths )
		{
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var model = ThornsFoliageModelCache.Load( path );
			if ( !IsUsableRockModel( model ) )
				continue;

			if ( !IsUsableRockModel( _rockA ) )
			{
				_rockA = model;
				continue;
			}

			if ( !IsUsableRockModel( _rockB ) && !string.Equals( path, primaryRock, StringComparison.OrdinalIgnoreCase ) )
			{
				_rockB = model;
				break;
			}
		}
	}

	bool AcceptBiomeForDetail( FoliageBiomeSample sample, Random rng )
	{
		if ( sample.Height <= _terrainConfig.SeaLevelNormalized + 0.01f )
		{
			_stats.RejectedWater++;
			return false;
		}

		if ( sample.Height < Config.MinHeightNormalized || sample.Height > Config.MaxHeightNormalized )
		{
			_stats.RejectedHeight++;
			return false;
		}

		var maxSlope = MathF.Min( Config.SlopeReject * 2.5f, 0.85f );
		if ( sample.Slope > maxSlope )
		{
			_stats.RejectedSlope++;
			return false;
		}

		var patchNoise = ThornsProcNoise.ValueNoise( sample.Height * 37.1f + rng.NextSingle() * 2.7f, sample.Moisture * 19.3f + rng.NextSingle() * 3.1f );
		float acceptThreshold;
		if ( Config.DetailInstancesPerChunk > 0 )
		{
			// Uniform mesh clutter — keep coverage thick and consistent inside the stream bubble.
			acceptThreshold = MathX.Lerp( 0.70f, 0.92f, patchNoise ).Clamp( 0.70f, 0.92f );
		}
		else
		{
			var meadow = (1f - sample.ForestMass) * (1f - sample.Alpine) * (1f - sample.Cliff) * Config.MeadowDensity;
			var forest = sample.ForestMass * (1f - sample.Opening) * Config.ForestDensity;
			var alpine = sample.Alpine * Config.AlpineDensity;
			var rocky = MathF.Max( sample.Slope * 5f, sample.Cliff ) * Config.SlopeRockDensity;
			var density = (meadow + forest * 0.35f + alpine * 0.35f + rocky * 0.45f).Clamp( 0f, 1.5f );
			density = (density * Math.Min( Config.GrassDensityMultiplier, 3f ) * 0.18f + 0.55f).Clamp( 0f, 1f );
			acceptThreshold = MathX.Lerp( Config.MinDetailAcceptRate, Math.Min( 0.82f, density + 0.18f ), patchNoise ).Clamp( Config.MinDetailAcceptRate, 0.85f );
		}

		if ( rng.NextSingle() > acceptThreshold )
		{
			_mixDebug.RecordRejectAccept();
			return false;
		}

		return true;
	}

	bool AcceptSample(
		FoliageBiomeSample sample,
		Random rng,
		out Model model,
		out bool isGrass,
		out string modelLabel )
	{
		model = null;
		isGrass = false;
		modelLabel = null;

		if ( !AcceptBiomeForDetail( sample, rng ) )
			return false;

		if ( !TryPickDetailModel( rng, rocksOnly: false, out model, out isGrass, out modelLabel ) )
		{
			_mixDebug.RecordRejectPick();
			return false;
		}

		return true;
	}

	void ApplyRockMaterial( ModelRenderer renderer, GameObject instance, Model model )
	{
		renderer.Tint = Color.White;

		var modelPath = model.ResourcePath ?? Config.RockModelA ?? "";
		if ( modelPath.Contains( "models/clutter/rock", StringComparison.OrdinalIgnoreCase ) )
		{
			ThornsModelMaterialUvScale.ApplyClutterRockMaterial( renderer, instance, model, modelPath );
			return;
		}

		var tinted = ThornsMineralTintMaterials.Get( model, MineralKind.Stone, _rockTintConfig );
		if ( tinted.IsValid )
			renderer.MaterialOverride = tinted;
	}

	GameObject AcquireInstance()
	{
		if ( _pool.Count > 0 )
		{
			_stats.ReusedObjects++;
			var reused = _pool.Dequeue();
			ResetPooledInstance( reused );
			return reused;
		}

		var instance = Scene.CreateObject( true );
		var renderer = instance.Components.Create<ModelRenderer>();
		ThornsWorldShadowUtil.DisableWorldShadows( renderer );
		return instance;
	}

	void ResetPooledInstance( GameObject instance )
	{
		if ( instance is null || !instance.IsValid() )
			return;

		_reveal.RemoveForObject( instance );
		var renderer = instance.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( renderer is not null && renderer.IsValid() )
		{
			renderer.Tint = Color.White;
			renderer.MaterialOverride = null;
		}
	}

	void QueueInstanceReveal( ModelRenderer renderer, Color targetTint, Vector3 worldPos, Vector3 observer, Random rng )
	{
		if ( !Config.EnableInstanceReveal || Config.InstanceRevealDuration <= 0f )
			return;

		var delay = ComputeRevealDelay( worldPos, observer, rng );
		_reveal.Queue( renderer, targetTint, delay, Config.InstanceRevealDuration );
	}

	float ComputeRevealDelay( Vector3 worldPos, Vector3 observer, Random rng )
	{
		var dist = MathF.Sqrt( DistanceSquared2D( worldPos, observer ) );
		var preloadEdge = Config.ClutterRadius - Config.ChunkSize * 0.35f;
		if ( dist >= preloadEdge )
			return 0f;

		return rng.NextSingle() * Config.InstanceRevealSpread;
	}

	void DestroyChunk( Vector2Int cell )
	{
		if ( !_chunks.TryGetValue( cell, out var chunk ) )
			return;

		_instancedRenderer?.UnregisterChunk( cell );

		_childScratch.Clear();
		foreach ( var child in chunk.Root.Children )
			_childScratch.Add( child );

		foreach ( var child in _childScratch )
		{
			_reveal.RemoveForObject( child );
			child.Enabled = false;
			child.Parent = null;
			_pool.Enqueue( child );
		}
		_childScratch.Clear();

		if ( chunk.Root.IsValid() )
			chunk.Root.Destroy();

		_stats.ActiveInstances -= chunk.InstanceCount;
		_stats.ActiveGrass -= chunk.GrassCount;
		_stats.ActiveRocks -= chunk.RockCount;
		_chunks.Remove( cell );
		_stats.DestroyedChunks++;
	}

	Vector3 ResolveObserverPosition()
		=> ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );

	Vector2Int WorldToCell( Vector3 pos )
	{
		var origin = _terrain.GameObject.WorldPosition;
		return new Vector2Int(
			(int)MathF.Floor( (pos.x - origin.x) / Config.ChunkSize ),
			(int)MathF.Floor( (pos.y - origin.y) / Config.ChunkSize ) );
	}

	Vector3 CellCenter( Vector2Int cell )
	{
		var origin = _terrain.GameObject.WorldPosition;
		return new Vector3(
			origin.x + (cell.x + 0.5f) * Config.ChunkSize,
			origin.y + (cell.y + 0.5f) * Config.ChunkSize,
			0f );
	}

	static float DistanceSquared2D( Vector3 a, Vector3 b )
	{
		var dx = a.x - b.x;
		var dy = a.y - b.y;
		return dx * dx + dy * dy;
	}

	float ClosestDistanceToChunk( Vector2Int cell, Vector3 observer )
	{
		var origin = _terrain.GameObject.WorldPosition;
		var half = Config.ChunkSize * 0.5f;
		var minX = origin.x + cell.x * Config.ChunkSize;
		var minY = origin.y + cell.y * Config.ChunkSize;
		var maxX = minX + Config.ChunkSize;
		var maxY = minY + Config.ChunkSize;

		var closestX = observer.x.Clamp( minX, maxX );
		var closestY = observer.y.Clamp( minY, maxY );
		var dx = observer.x - closestX;
		var dy = observer.y - closestY;
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	float DistanceBandDensity( float distance )
	{
		var buildRadius = Config.ClutterRadius + Config.ChunkSize * Math.Max( 0, Config.PreloadChunkRings );
		if ( distance >= buildRadius )
			return 0f;

		// Uniform density throughout the streaming bubble — radius culling handles LOD range.
		if ( Config.DetailInstancesPerChunk > 0 )
			return 1f;

		if ( distance >= Config.DistanceFadeEnd )
			return 0f;

		if ( distance <= Config.DistanceFadeStart )
			return 1f;

		var t = ((distance - Config.DistanceFadeStart) / Math.Max( Config.DistanceFadeEnd - Config.DistanceFadeStart, 1f )).Clamp( 0f, 1f );
		return 1f - t * t * (3f - 2f * t);
	}

	static int ComputeSparseTarget( float rawTarget, Random rng )
	{
		if ( rawTarget <= 0f )
			return 0;

		var whole = (int)MathF.Floor( rawTarget );
		var fraction = rawTarget - whole;
		return whole + (rng.NextSingle() < fraction ? 1 : 0);
	}

	ThornsFoliageConfig BuildSamplerConfig()
	{
		return new ThornsFoliageConfig
		{
			VerboseDebug = false,
			MaxSlopeForTrees = 0.12f,
			MaxSlopeForGrass = Config.SlopeReject,
			MinHeightAboveSea = 0.005f,
			FoliageSeed = Config.WorldSeed,
		};
	}

	IEnumerable<Model> CollectInstancedModels()
	{
		var models = new List<Model>();
		foreach ( var choice in _modelChoices )
		{
			if ( !choice.IsRock && IsUsableModel( choice.Model ) )
				models.Add( choice.Model );
		}

		if ( IsUsableRockModel( _rockA ) )
			models.Add( _rockA );

		if ( IsUsableRockModel( _rockB ) )
			models.Add( _rockB );

		return models;
	}

	bool TryAddInstancedClutter( Model model, Vector3 position, Rotation rotation, float scale )
	{
		if ( _buildingChunkInstances is null || _instancedRenderer is null || !Config.UseInstancedMeshDetail )
			return false;

		var modelIndex = _instancedRenderer.GetModelIndex( model );
		if ( modelIndex < 0 )
			return false;

		_buildingChunkInstances.GetList( modelIndex ).Add( new Transform( position, rotation, new Vector3( scale ) ) );
		return true;
	}

	static int StableHash( int seed, int x, int y )
	{
		unchecked
		{
			var h = 2166136261u;
			h = (h ^ (uint)seed) * 16777619u;
			h = (h ^ (uint)x) * 16777619u;
			h = (h ^ (uint)y) * 16777619u;
			return (int)(h & 0x7fffffffu);
		}
	}

	public string GetDebugSummary()
	{
		return $"[Thorns Clutter] chunks={_stats.ActiveChunks} instances={_stats.ActiveInstances} grass={_stats.ActiveGrass} rocks={_stats.ActiveRocks} gen={_stats.GeneratedChunks} destroyed={_stats.DestroyedChunks} reused={_stats.ReusedObjects} lastMs={_stats.LastGenerationMs:F2} reject slope/height/water/ray={_stats.RejectedSlope}/{_stats.RejectedHeight}/{_stats.RejectedWater}/{_stats.RejectedRay}";
	}

	public void Clear()
	{
		_instancedRenderer?.Clear();
		_instancedRenderer = null;
		_buildingChunkInstances = null;

		foreach ( var chunk in _chunks.Values )
		{
			if ( chunk.Root.IsValid() )
				chunk.Root.Destroy();
		}

		_chunks.Clear();
		_stats.ActiveChunks = 0;
		_stats.ActiveInstances = 0;
		_stats.ActiveGrass = 0;
		_stats.ActiveRocks = 0;
		while ( _pool.Count > 0 )
		{
			var obj = _pool.Dequeue();
			if ( obj.IsValid() )
				obj.Destroy();
		}

		if ( _root.IsValid() )
			_root.Destroy();

		_ready = false;
		_pendingInitialRefresh = false;
		_pendingStreamStart = false;
		_pendingChunkBuildCount = 0;
		_mixDebug.Reset();
		_reveal.Reset();
	}

	protected override void OnDestroy()
	{
		Clear();
	}
}
