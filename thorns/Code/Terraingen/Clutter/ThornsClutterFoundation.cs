namespace Terraingen.Clutter;

using System.Diagnostics;
using Terraingen;
using Terraingen.Foliage;
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
	[Property] public ThornsClutterConfig Config { get; set; } = new();

	readonly Dictionary<Vector2Int, ThornsClutterChunk> _chunks = new();
	readonly Queue<GameObject> _pool = new();
	readonly ThornsClutterStats _stats = new();
	readonly List<Vector2Int> _wantedScratch = new();
	readonly HashSet<Vector2Int> _wantedCells = new();
	readonly List<Vector2Int> _removeScratch = new();
	readonly List<GameObject> _childScratch = new();

	Terrain _terrain;
	ThornsTerrainConfig _terrainConfig;
	ThornsFoliageBiomeSampler _sampler;
	GameObject _root;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Model _grass;
	Model _rockA;
	Model _rockB;
	TimeUntil _nextRefresh;
	TimeUntil _nextDebug;
	bool _ready;
	bool _loggedPlacement;
	bool _pawnLocalBubble;
	GameObject _pawnAnchor;

	public void BeginStreaming( Terrain terrain, HeightmapField field, ThornsTerrainConfig terrainConfig, ThornsFoliageBiomeSampler sharedSampler = null )
	{
		Clear();

		if ( Scene.IsEditor || !Config.Enabled || !terrain.IsValid() || field is null )
			return;

		_terrain = terrain;
		_terrainConfig = terrainConfig;
		_sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, BuildSamplerConfig() );
		if ( !ThornsFoliageScatter.IsClutterGrassDecorPath( Config.GrassModel ) )
			Log.Warning(
				$"[Thorns Clutter] GrassModel '{Config.GrassModel}' is not clutter grass — expected '{ThornsFoliageScatter.DefaultGrassModelPath}'." );

		_grass = ThornsFoliageScatter.IsClutterGrassDecorPath( Config.GrassModel )
			? ThornsFoliageModelCache.Load( Config.GrassModel )
			: default;
		_rockA = ThornsFoliageModelCache.Load( Config.RockModelA );
		_rockB = ThornsFoliageModelCache.Load( Config.RockModelB );

		if ( !_grass.IsValid && !_rockA.IsValid && !_rockB.IsValid )
		{
			Log.Warning( "[Thorns Clutter] No valid clutter .vmdl assets found under models/clutter." );
			return;
		}

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Client Clutter";
		_root.Parent = GameObject;
		TryAttachRootToLocalPawn();
		_ready = true;
		_loggedPlacement = false;
		RefreshStreaming();

		if ( _grass.IsValid )
		{
			var mode = _pawnLocalBubble ? "pawn-local" : "world-stream";
			Log.Info( $"[Thorns Clutter] Ready ({mode}) — grass='{Config.GrassModel}' radius≈{Config.ClutterRadius:F0} in (~{Config.ClutterRadius / (1968f / 50f):F0} m)" );
		}
		else
			Log.Info( $"[Thorns Clutter] Ready — grass model failed to load '{Config.GrassModel}'" );

		if ( _rockA.IsValid || _rockB.IsValid )
			Log.Info( $"[Thorns Clutter] Rocks rock1={_rockA.IsValid} rock2={_rockB.IsValid} mix={Config.RockInstanceMix:P0}" );
		else
			Log.Warning( $"[Thorns Clutter] Rock models failed — '{Config.RockModelA}' / '{Config.RockModelB}'" );
	}

	protected override void OnUpdate()
	{
		if ( !_ready || !Config.Enabled )
			return;

		if ( _nextRefresh )
			RefreshStreaming();

		if ( Config.ShowDebug && _nextDebug )
		{
			_nextDebug = 0.5f;
			Log.Info( GetDebugSummary() );
		}
	}

	void RefreshStreaming()
	{
		TryAttachRootToLocalPawn();
		_nextRefresh = _pawnLocalBubble ? 0.35f : 0.12f;
		var observer = _pawnLocalBubble ? Vector3.Zero : ResolveObserverPosition();
		var observerCell = _pawnLocalBubble ? Vector2Int.Zero : PlanarToCell( observer );
		var radiusChunks = Math.Max( 1, (int)MathF.Ceiling( Config.ClutterRadius / Config.ChunkSize ) );

		_wantedScratch.Clear();
		_wantedCells.Clear();
		var chunksBuilt = 0;
		for ( var y = -radiusChunks; y <= radiusChunks; y++ )
		{
			for ( var x = -radiusChunks; x <= radiusChunks; x++ )
			{
				var cell = _pawnLocalBubble
					? new Vector2Int( x, y )
					: new Vector2Int( observerCell.x + x, observerCell.y + y );
				var center = CellCenter( cell );
				if ( DistanceSquared2D( center, observer ) > Config.ClutterRadius * Config.ClutterRadius )
					continue;

				_wantedScratch.Add( cell );
				_wantedCells.Add( cell );
				if ( !_chunks.ContainsKey( cell ) )
				{
					if ( chunksBuilt >= Config.ChunksBuiltPerRefresh )
						continue;

					BuildChunk( cell, observer );
					chunksBuilt++;
				}
			}
		}

		if ( !_pawnLocalBubble )
		{
			_removeScratch.Clear();
			foreach ( var pair in _chunks )
			{
				if ( !_wantedCells.Contains( pair.Key ) )
					_removeScratch.Add( pair.Key );
			}

			foreach ( var cell in _removeScratch )
				DestroyChunk( cell );
		}

		_stats.ActiveChunks = _chunks.Count;
	}

	bool TryAttachRootToLocalPawn()
	{
		if ( !Config.FollowLocalPawn || !_root.IsValid() )
			return _pawnLocalBubble;

		var pawn = ThornsPawn.Local;
		if ( !pawn.IsValid() || !pawn.GameObject.IsValid() )
			return _pawnLocalBubble;

		if ( _pawnLocalBubble && _pawnAnchor == pawn.GameObject )
			return true;

		_pawnAnchor = pawn.GameObject;
		_root.SetParent( _pawnAnchor );
		_root.LocalPosition = Vector3.Zero;
		_root.LocalRotation = Rotation.Identity;
		_pawnLocalBubble = true;
		return true;
	}

	Vector3 PlanarLocalToWorld( float localX, float localY )
	{
		if ( _pawnLocalBubble && _pawnAnchor.IsValid() )
			return _pawnAnchor.WorldTransform.PointToWorld( new Vector3( localX, localY, 0f ) );

		return new Vector3( localX, localY, 0f );
	}

	Vector3 WorldToClutterLocal( Vector3 worldPos )
	{
		if ( _pawnLocalBubble && _pawnAnchor.IsValid() )
			return _pawnAnchor.WorldRotation.Inverse * (worldPos - _pawnAnchor.WorldPosition);

		return worldPos;
	}

	void BuildChunk( Vector2Int cell, Vector3 observer )
	{
		var watch = Stopwatch.StartNew();
		var center = CellCenter( cell );
		var root = Scene.CreateObject( true );
		root.Name = $"Clutter {cell.x}_{cell.y}";
		root.Parent = _root;

		var rng = new Random( StableHash( Config.WorldSeed, cell.x, cell.y ) );
		var distance = MathF.Sqrt( DistanceSquared2D( center, observer ) );
		var band = DistanceBandDensity( distance );
		if ( distance <= Config.DistanceFadeStart )
			band *= Config.NearPlayerGrassBoost;
		var baseTarget = (int)(Config.MaxInstancesPerChunk * Config.DensityMultiplier * band);
		var totalTarget = (int)(baseTarget * Config.GrassInstanceMultiplier);
		totalTarget = Math.Min( totalTarget, Config.MaxInstancesPerChunk );
		var rockMix = distance <= Config.DistanceFadeStart
			? Math.Min( Config.RockInstanceMix, 0.04f )
			: Config.RockInstanceMix.Clamp( 0.05f, 0.55f );
		var rockTarget = Math.Max( 0, (int)(totalTarget * rockMix) );
		var grassTarget = Math.Max( 1, totalTarget - rockTarget );
		var count = 0;
		var grassCount = 0;
		var rockCount = 0;
		var attempts = Math.Max( totalTarget * 10, 64 );

		for ( var i = 0; i < attempts && count < totalTarget; i++ )
		{
			var wx = center.x + (rng.NextSingle() - 0.5f) * Config.ChunkSize;
			var wy = center.y + (rng.NextSingle() - 0.5f) * Config.ChunkSize;
			var planarDist = MathF.Sqrt( wx * wx + wy * wy );
			if ( planarDist > Config.ClutterRadius )
				continue;

			var worldPlanar = PlanarLocalToWorld( wx, wy );
			var sample = _sampler.Sample( worldPlanar.x, worldPlanar.y );
			var preferGrass = grassCount < grassTarget;
			var preferRock = !preferGrass && rockCount < rockTarget;
			var placementBoost = planarDist <= Config.DistanceFadeStart ? Config.NearPlayerGrassBoost : 1f;
			if ( !AcceptSample( sample, rng, preferGrass, preferRock, placementBoost, out var model, out var isGrass ) )
				continue;

			var scale = ThornsClutterSurface.ComputeUniformScale( model, isGrass, Config, rng );
			if ( !ThornsClutterSurface.TrySampleWorld( _terrain, _sampler, worldPlanar.x, worldPlanar.y, model, scale, isGrass, Config, out var pos ) )
			{
				_stats.RejectedRay++;
				continue;
			}

			var yaw = rng.NextSingle() * 360f;
			var tilt = Config.RandomTiltDegrees * (rng.NextSingle() - 0.5f );
			var clusterCount = isGrass
				? Math.Clamp( Config.GrassPlacementClusterSize, 1, 4 )
				: 1;

			for ( var blade = 0; blade < clusterCount && count < totalTarget; blade++ )
			{
				var jitter = blade == 0
					? Vector3.Zero
					: new Vector3(
						(rng.NextSingle() - 0.5f) * 14f,
						(rng.NextSingle() - 0.5f) * 14f,
						0f );

				var instance = AcquireInstance();
				instance.Parent = _pawnLocalBubble ? _root : root;
				instance.Name = isGrass ? "Clutter Grass" : "Clutter Rock";
				var worldPos = pos + jitter;
				if ( _pawnLocalBubble )
				{
					instance.LocalPosition = WorldToClutterLocal( worldPos );
					instance.LocalRotation = Rotation.FromYaw( yaw ) * new Angles( tilt, 0f, 0f ).ToRotation();
				}
				else
				{
					instance.WorldPosition = worldPos;
					instance.WorldRotation = Rotation.FromYaw( yaw ) * new Angles( tilt, 0f, 0f ).ToRotation();
				}
				instance.LocalScale = new Vector3( scale );
				instance.Enabled = true;

				var renderer = instance.Components.Get<ModelRenderer>();
				renderer.Model = model;
				renderer.Enabled = true;
				renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
				if ( isGrass )
					ThornsModelMaterialUvScale.ApplyForScaledModel( renderer, instance, model, model.Name );
				else
					ThornsModelMaterialUvScale.ApplyClutterRockMaterial( renderer, instance, model, model.Name );

				count++;
				if ( isGrass )
					grassCount++;
				else
					rockCount++;

				if ( !_loggedPlacement )
				{
					_loggedPlacement = true;
					var estHeight = model.Bounds.Size.z * scale;
					Log.Info( $"[Thorns Clutter] First {(isGrass ? "grass" : "rock")} at {pos + jitter}, scale={scale:F2}, estHeight≈{estHeight:F0} in, bounds={model.Bounds.Size}" );
				}
			}
		}

		watch.Stop();
		_chunks[cell] = new ThornsClutterChunk
		{
			Cell = cell,
			Center = center,
			Root = root,
			InstanceCount = count,
			GrassCount = grassCount,
			RockCount = rockCount,
			LastBuildMilliseconds = (float)watch.Elapsed.TotalMilliseconds,
		};

		_stats.GeneratedChunks++;
		_stats.LastGenerationMs = (float)watch.Elapsed.TotalMilliseconds;
		_stats.ActiveInstances += count;
		_stats.ActiveGrass += grassCount;
		_stats.ActiveRocks += rockCount;
	}

	bool AcceptSample(
		FoliageBiomeSample sample,
		Random rng,
		bool preferGrass,
		bool preferRock,
		float placementDensityBoost,
		out Model model,
		out bool isGrass )
	{
		model = null;
		isGrass = false;

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

		var hasRockModel = _rockA.IsValid || _rockB.IsValid;
		var maxRockSlope = MathF.Min( Config.SlopeReject * 2.5f, 0.85f );
		var slopeOnlyAllowsRock = sample.Slope > Config.SlopeReject;
		if ( slopeOnlyAllowsRock && (!hasRockModel || sample.Slope > maxRockSlope) )
		{
			_stats.RejectedSlope++;
			return false;
		}

		if ( preferGrass && _grass.IsValid && !slopeOnlyAllowsRock )
		{
			model = _grass;
			isGrass = true;
			return true;
		}

		if ( preferRock && hasRockModel )
		{
			model = _rockA.IsValid && (!_rockB.IsValid || rng.NextSingle() < 0.5f) ? _rockA : _rockB;
			isGrass = false;
			return true;
		}

		var meadow = (1f - sample.ForestMass) * (1f - sample.Alpine) * (1f - sample.Cliff) * Config.MeadowDensity;
		var forest = sample.ForestMass * (1f - sample.Opening) * Config.ForestDensity;
		var alpine = sample.Alpine * Config.AlpineDensity;
		var rocky = MathF.Max( sample.Slope * 5f, sample.Cliff ) * Config.SlopeRockDensity;
		var density = (meadow + forest * 0.35f + alpine * 0.35f + rocky * 0.45f).Clamp( 0f, 1.5f );
		density = (density * Config.GrassDensityMultiplier).Clamp( 0f, 10f );

		var patchNoise = ValueNoise( sample.Height * 37.1f + rng.NextSingle() * 2.7f, sample.Moisture * 19.3f + rng.NextSingle() * 3.1f );
		var acceptThreshold = (density * MathX.Lerp( 0.75f, 1f, patchNoise ) * placementDensityBoost).Clamp( 0f, 1f );
		if ( rng.NextSingle() > acceptThreshold )
			return false;

		if ( _grass.IsValid )
		{
			model = _grass;
			isGrass = true;
			return true;
		}

		model = _rockA.IsValid ? _rockA : _rockB;
		isGrass = false;
		return model.IsValid;
	}

	GameObject AcquireInstance()
	{
		if ( _pool.Count > 0 )
		{
			_stats.ReusedObjects++;
			return _pool.Dequeue();
		}

		var instance = Scene.CreateObject( true );
		var renderer = instance.Components.Create<ModelRenderer>();
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		return instance;
	}

	void DestroyChunk( Vector2Int cell )
	{
		if ( !_chunks.TryGetValue( cell, out var chunk ) )
			return;

		_childScratch.Clear();
		foreach ( var child in chunk.Root.Children )
			_childScratch.Add( child );

		foreach ( var child in _childScratch )
		{
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
	{
		if ( ThornsPawn.Local is { IsValid: true } pawn )
			return pawn.GameObject.WorldPosition;

		return ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
	}

	Vector2Int PlanarToCell( Vector3 planar )
	{
		if ( _pawnLocalBubble )
		{
			return new Vector2Int(
				(int)MathF.Floor( planar.x / Config.ChunkSize ),
				(int)MathF.Floor( planar.y / Config.ChunkSize ) );
		}

		var origin = _terrain.GameObject.WorldPosition;
		return new Vector2Int(
			(int)MathF.Floor( (planar.x - origin.x) / Config.ChunkSize ),
			(int)MathF.Floor( (planar.y - origin.y) / Config.ChunkSize ) );
	}

	Vector3 CellCenter( Vector2Int cell )
	{
		if ( _pawnLocalBubble )
		{
			return new Vector3(
				(cell.x + 0.5f) * Config.ChunkSize,
				(cell.y + 0.5f) * Config.ChunkSize,
				0f );
		}

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

	float DistanceBandDensity( float distance )
	{
		if ( distance >= Config.DistanceFadeEnd )
			return 0f;

		if ( distance <= Config.DistanceFadeStart )
			return 1f;

		var t = ((distance - Config.DistanceFadeStart) / Math.Max( Config.DistanceFadeEnd - Config.DistanceFadeStart, 1f )).Clamp( 0f, 1f );
		return 1f - t * t * (3f - 2f * t);
	}

	ThornsFoliageConfig BuildSamplerConfig()
	{
		return new ThornsFoliageConfig
		{
			VerboseDebug = false,
			MaxTreeSlopeDegrees = ThornsTerrainSlope.DefaultMaxTreeSlopeDegrees,
			MaxSlopeForGrass = Config.SlopeReject,
			MinHeightAboveSea = 0.005f,
			FoliageSeed = Config.WorldSeed,
		};
	}

	static float ValueNoise( float x, float y )
	{
		var xi = (int)MathF.Floor( x * 12.9898f );
		var yi = (int)MathF.Floor( y * 78.233f );
		var n = Math.Sin( xi * 127.1 + yi * 311.7 ) * 43758.5453;
		return (float)(n - Math.Floor( n ));
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

		_pawnLocalBubble = false;
		_pawnAnchor = null;
		_ready = false;
	}

	protected override void OnDestroy()
	{
		Clear();
	}
}
