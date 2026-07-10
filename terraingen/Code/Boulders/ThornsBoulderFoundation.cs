namespace Terraingen.Boulders;

using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.Core;
using Terraingen.Foliage;
using Terraingen.Physics;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.World;

[Title( "Thorns Boulder Field" )]
[Category( "Terrain" )]
[Icon( "terrain" )]
public sealed class ThornsBoulderFoundation : Component
{
	[Property] public ThornsBoulderConfig Config { get; set; } = new();

	readonly List<GameObject> _chunkRoots = new();
	readonly List<ThornsBoulderInstance> _instances = new();

	GameObject _root;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	Vector3 _lastLodObserverPosition;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextShadowLod;
	int _placed;
	int _shadowLodCursor;
	int _rejectedBiome;
	int _rejectedTown;
	int _rejectedSpacing;
	int _rayMisses;
	bool _hasLodObserverPosition;

	public void BeginPopulate(
		Terrain terrain,
		HeightmapField field,
		ThornsTerrainConfig terrainConfig,
		ThornsFoliageBiomeSampler sharedSampler = null )
	{
		Config ??= new ThornsBoulderConfig();

		if ( !Config.SpawnOnTerrainReady )
		{
			Log.Warning( "[Thorns Boulders] Skipped - SpawnOnTerrainReady is false." );
			return;
		}

		if ( !terrain.IsValid() || field is null )
		{
			Log.Error( "[Thorns Boulders] BeginPopulate failed - invalid terrain or heightfield." );
			return;
		}

		var models = LoadModels();
		if ( models.Count == 0 )
		{
			Log.Error( "[Thorns Boulders] No valid boulder models found under models/boulders." );
			return;
		}

		Clear();

		if ( Config.CollisionRadiusScaleOverride > 0f )
			ThornsBoulderSphereCollision.SyncLiveRadiusScale( Config.CollisionRadiusScaleOverride );
		else
			ThornsBoulderSphereCollision.SyncLiveRadiusScale( 0f );

		Config.WorldSeed = terrainConfig.WorldSeed;
		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Boulder Field";
		_root.Parent = GameObject;

		var foliageConfig = new ThornsFoliageConfig { FoliageSeed = terrainConfig.WorldSeed };
		var sampler = sharedSampler ?? new ThornsFoliageBiomeSampler( field, terrain, terrainConfig, foliageConfig );
		var cells = ThornsChunkGrid.BuildFullGrid( terrain.TerrainSize, Config.ChunkSizeInches );

		foreach ( var cell in cells )
			PopulateChunk( terrain, cell, sampler, models );

		Log.Info(
			$"[Thorns Boulders] Placed {_placed} solid boulder(s) across {cells.Count} chunk(s). radiusScale={Config.ResolveCollisionRadiusScale():F3} (codeDefault={ThornsBoulderSphereCollision.DefaultRadiusScale:F3}) rejects biome/town/spacing/ray={_rejectedBiome}/{_rejectedTown}/{_rejectedSpacing}/{_rayMisses}." );

		ThornsBoulderSphereCollision.RefreshAllInScene( Scene, Config.VerboseDebug, force: true );
	}

	protected override void OnStart()
	{
		ThornsBoulderSphereCollision.RefreshAllInScene( Scene );
	}

	public void RefreshAllColliderRadii() =>
		ThornsBoulderSphereCollision.RefreshAllInScene( Scene, Config?.VerboseDebug == true );

	public void Clear()
	{
		foreach ( var chunk in _chunkRoots )
		{
			if ( chunk.IsValid() )
				chunk.Destroy();
		}

		_chunkRoots.Clear();
		_instances.Clear();
		_placed = 0;
		_shadowLodCursor = 0;
		_rejectedBiome = 0;
		_rejectedTown = 0;
		_rejectedSpacing = 0;
		_rayMisses = 0;

		if ( _root.IsValid() )
			_root.Destroy();

		_root = null;
		_hasLodObserverPosition = false;
	}

	/// <summary>Sphere colliders when <see cref="ThornsCollisionDebug.OverlayEnabled"/>.</summary>
	public void DrawCollisionDebugOverlay( Vector3 observer, float maxDistance, float duration )
	{
		var maxDistSq = maxDistance * maxDistance;

		foreach ( var marker in Scene.GetAllComponents<ThornsBoulderColliderMarker>() )
		{
			if ( !marker.IsValid() || !marker.GameObject.IsValid() )
				continue;

			if ( (marker.GameObject.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject( DebugOverlay, marker.GameObject, duration );
		}
	}

	protected override void OnUpdate()
	{
		if ( Terraingen.UI.Core.ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( _instances.Count == 0 || Config is null )
			return;

		var observer = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		if ( !ShouldUpdateShadowLod( observer ) )
			return;

		UpdateStaggeredShadowLod( observer );
	}

	List<Model> LoadModels()
	{
		var models = new List<Model>( 3 );
		foreach ( var rawPath in Config.ModelPaths() )
		{
			var path = rawPath?.Trim();
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var model = ThornsFoliageModelCache.Load( path );
			if ( model.IsValid && !model.IsError )
			{
				models.Add( model );
				if ( Config.VerboseDebug )
					Log.Info( $"[Thorns Boulders] Model resolved '{path}' bounds={model.Bounds.Size} render={model.RenderBounds.Size}." );
			}
			else
			{
				Log.Warning( $"[Thorns Boulders] Model unavailable: '{path}'." );
			}
		}

		return models;
	}

	void PopulateChunk(
		Terrain terrain,
		Vector2Int cell,
		ThornsFoliageBiomeSampler sampler,
		IReadOnlyList<Model> models )
	{
		var center = ThornsChunkGrid.CellCenter( terrain.GameObject.WorldPosition, Config.ChunkSizeInches, cell );
		var target = ResolveChunkBudget( sampler, center );
		if ( target <= 0 )
			return;

		var chunkRoot = Scene.CreateObject( true );
		chunkRoot.Name = $"Boulders {cell.x}_{cell.y}";
		chunkRoot.Parent = _root;
		_chunkRoots.Add( chunkRoot );

		var rng = new Random( HashCode.Combine( Config.WorldSeed, cell.x, cell.y, 0xB041D3A ) );
		var attempts = Math.Max( target * 18, 28 );
		var placedInChunk = 0;

		for ( var i = 0; i < attempts && placedInChunk < target; i++ )
		{
			var wx = terrain.GameObject.WorldPosition.x + (cell.x + rng.NextSingle()) * Config.ChunkSizeInches;
			var wy = terrain.GameObject.WorldPosition.y + (cell.y + rng.NextSingle()) * Config.ChunkSizeInches;

			if ( !TryPlaceBoulder(
				     terrain,
				     chunkRoot,
				     sampler,
				     models,
				     rng,
				     wx,
				     wy,
				     requirePatchGate: true,
				     out var worldPosition ) )
				continue;

			placedInChunk++;
			_placed++;

			if ( rng.NextSingle() <= Config.BunchChance )
				placedInChunk += TryPlaceBunchMembers( terrain, chunkRoot, sampler, models, rng, worldPosition, target - placedInChunk );
		}

		if ( placedInChunk == 0 )
		{
			chunkRoot.Destroy();
			_chunkRoots.Remove( chunkRoot );
		}
	}

	int ResolveChunkBudget( ThornsFoliageBiomeSampler sampler, Vector3 center )
	{
		if ( Config.MaxBouldersPerChunk <= 0 || Config.GlobalDensity <= 0f )
			return 0;

		var ecology = sampler.SampleChunkEcology( center, Config.ChunkSizeInches );
		var sample = sampler.Sample( center.x, center.y );
		if ( sample.Slope > Config.MaxSlope )
			return 0;

		var opening = MathX.Lerp( 0.55f, 1.25f, sample.Opening.Clamp( 0f, 1f ) ) * Config.OpeningBias;
		var flat = (1f - sample.Slope / Math.Max( Config.MaxSlope, 0.001f )).Clamp( 0f, 1f );
		var forestEdge = (ecology.ForestMass * (1f - sample.Opening)).Clamp( 0f, 1f ) * 0.35f;
		var suitability = (opening * flat + forestEdge).Clamp( 0f, 1.65f );
		var raw = Config.MaxBouldersPerChunk * Config.GlobalDensity * suitability;
		var whole = (int)MathF.Floor( raw );

		var rng = new Random( HashCode.Combine( Config.WorldSeed, (int)center.x, (int)center.y, 0xC0B1E ) );
		if ( rng.NextSingle() < raw - whole )
			whole++;

		return Math.Clamp( whole, 0, Config.MaxBouldersPerChunk );
	}

	int TryPlaceBunchMembers(
		Terrain terrain,
		GameObject chunkRoot,
		ThornsFoliageBiomeSampler sampler,
		IReadOnlyList<Model> models,
		Random rng,
		Vector3 anchor,
		int remainingBudget )
	{
		if ( remainingBudget <= 0 || Config.MaxBunchExtraBoulders <= 0 )
			return 0;

		var minExtra = Math.Clamp( Config.MinBunchExtraBoulders, 0, Config.MaxBunchExtraBoulders );
		var maxExtra = Math.Max( minExtra, Config.MaxBunchExtraBoulders );
		var desired = Math.Min( remainingBudget, rng.Next( minExtra, maxExtra + 1 ) );
		var attempts = Math.Max( desired * 8, 12 );
		var placed = 0;

		for ( var i = 0; i < attempts && placed < desired; i++ )
		{
			var angle = rng.NextSingle() * MathF.Tau;
			var dist = MathX.Lerp( Config.BunchRadiusMinInches, Config.BunchRadiusMaxInches, MathF.Sqrt( rng.NextSingle() ) );
			var wx = anchor.x + MathF.Cos( angle ) * dist;
			var wy = anchor.y + MathF.Sin( angle ) * dist;

			if ( !TryPlaceBoulder(
				     terrain,
				     chunkRoot,
				     sampler,
				     models,
				     rng,
				     wx,
				     wy,
				     requirePatchGate: false,
				     out var worldPosition ) )
				continue;

			placed++;
			_placed++;
		}

		return placed;
	}

	bool TryPlaceBoulder(
		Terrain terrain,
		GameObject chunkRoot,
		ThornsFoliageBiomeSampler sampler,
		IReadOnlyList<Model> models,
		Random rng,
		float wx,
		float wy,
		bool requirePatchGate,
		out Vector3 worldPosition )
	{
		worldPosition = default;

		var model = models[rng.Next( models.Count )];
		var scale = ComputeScale( model, rng );
		var yawDegrees = rng.NextSingle() * 360f;

		if ( !AcceptPlacement( terrain, sampler, wx, wy, rng, model, scale, yawDegrees, requirePatchGate ) )
			return false;

		if ( !TrySampleWorld( terrain, wx, wy, model, scale, out worldPosition ) )
		{
			_rayMisses++;
			return false;
		}

		CreateBoulder( chunkRoot, terrain, model, worldPosition, yawDegrees, scale );
		return true;
	}

	bool AcceptPlacement(
		Terrain terrain,
		ThornsFoliageBiomeSampler sampler,
		float wx,
		float wy,
		Random rng,
		Model model,
		float scale,
		float yawDegrees,
		bool requirePatchGate )
	{
		var sample = sampler.Sample( wx, wy );
		if ( !sampler.IsAboveSeaLevel( wx, wy )
		     || sample.Height < Config.MinHeightNormalized
		     || sample.Height > Config.MaxHeightNormalized
		     || sample.Slope > Config.MaxSlope )
		{
			_rejectedBiome++;
			return false;
		}

		if ( !ThornsFoliageFlatness.IsFootprintFlat(
			     terrain,
			     sampler,
			     wx,
			     wy,
			     Config.MaxSlope,
			     Config.FootprintSampleRadiusInches,
			     Config.MaxFootprintSlopeDelta,
			     Config.MaxFootprintHeightDeltaInches,
			     out _ ) )
		{
			_rejectedBiome++;
			return false;
		}

		if ( ThornsProcBuildingFootprintRegistry.ContainsWorldPoint( wx, wy, Config.TownExtraMarginInches ) )
		{
			_rejectedTown++;
			return false;
		}

		if ( ThornsWorldScatterFootprintRegistry.WouldBoulderOverlap( wx, wy, yawDegrees, model, scale ) )
		{
			_rejectedSpacing++;
			return false;
		}

		if ( requirePatchGate )
		{
			var sightlinePatch = ThornsProcNoise.ValueNoise( wx * 0.00021f, wy * 0.00021f );
			var threshold = MathX.Lerp( 0.2f, 0.88f, (sample.Opening * 0.75f + sample.Cliff * 0.25f).Clamp( 0f, 1f ) );
			if ( rng.NextSingle() > threshold * MathX.Lerp( 0.72f, 1.1f, sightlinePatch ) )
			{
				_rejectedBiome++;
				return false;
			}
		}

		return true;
	}

	float ComputeScale( Model model, Random rng )
	{
		var bounds = ResolveBounds( model );
		var meshHeight = Math.Max( bounds.Size.z, 1f );
		var targetHeight = MathX.Lerp( Config.MinTargetHeightInches, Config.MaxTargetHeightInches, rng.NextSingle() );
		var scale = targetHeight / meshHeight;
		var minMul = MathF.Max( 1f, Config.MinSizeMultiplier );
		var maxMul = MathF.Max( minMul, Config.MaxSizeMultiplier );
		scale *= MathX.Lerp( minMul, maxMul, rng.NextSingle() );
		return Math.Max( ThornsNatureScaleVariance.Apply( scale, rng ), 0.05f );
	}

	bool TrySampleWorld( Terrain terrain, float worldX, float worldY, Model model, float scale, out Vector3 worldPosition )
	{
		worldPosition = default;
		var maxHeight = terrain.TerrainHeight;
		var rayStart = new Vector3( worldX, worldY, maxHeight * 2.5f + terrain.GameObject.WorldPosition.z );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( !terrain.RayIntersects( ray, maxHeight * 5f, out var localHit ) )
			return false;

		worldPosition = terrain.GameObject.WorldTransform.PointToWorld( localHit );
		var bounds = ResolveBounds( model );
		var lift = Math.Max( 0f, -bounds.Mins.z * scale );
		var embed = bounds.Size.z * scale * Config.GroundEmbedFraction + Config.GroundSinkOffsetInches;
		worldPosition += Vector3.Up * (lift - embed);
		return true;
	}

	void CreateBoulder(
		GameObject parent,
		Terrain terrain,
		Model model,
		Vector3 worldPosition,
		float yawDegrees,
		float scale )
	{
		var obj = Scene.CreateObject( true );
		obj.Name = "Sightline Boulder";
		obj.Parent = parent;
		obj.LocalPosition = terrain.GameObject.WorldTransform.PointToLocal( worldPosition );
		obj.LocalRotation = Rotation.FromYaw( yawDegrees );
		obj.LocalScale = new Vector3( scale );
		obj.Tags.Add( "boulder" );

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.Enabled = true;
		ThornsWorldShadowUtil.DisableWorldShadows( renderer );

		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		var worldRadius = ThornsBoulderSphereCollision.Apply(
			obj,
			model,
			scale,
			Config.ResolveCollisionRadiusScale() );

		if ( Config.VerboseDebug )
			Log.Info( $"[Thorns Boulders] '{obj.Name}' worldRadius≈{worldRadius:F0}in scale={scale:F2} radiusScale={Config.ResolveCollisionRadiusScale():F3}" );

		ThornsWorldScatterFootprintRegistry.RegisterBoulder( worldPosition, yawDegrees, model, scale );
		var instance = new ThornsBoulderInstance( obj, renderer );
		instance.ShadowsEnabled = false;
		_instances.Add( instance );
	}

	bool ShouldUpdateShadowLod( Vector3 observer )
	{
		if ( !_nextShadowLod )
		{
			_nextShadowLod = Config.ShadowLodIntervalSeconds;
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

		var minMove = Config.ShadowLodMinMoveInches;
		if ( minMove <= 0f || (observer - _lastLodObserverPosition).LengthSquared >= minMove * minMove )
		{
			_lastLodObserverPosition = observer;
			return true;
		}

		return false;
	}

	void UpdateStaggeredShadowLod( Vector3 observer )
	{
		var count = Math.Max( 1, Config.LodBouldersUpdatedPerFrame );
		var hysteresis = Config.LodDistanceHysteresisInches;
		var shadowOuter = Config.BoulderLodShadowDistanceInches + hysteresis;
		var shadowOuterSq = shadowOuter * shadowOuter;
		var shadowInner = MathF.Max( Config.BoulderLodShadowDistanceInches - hysteresis, 0f );
		var shadowInnerSq = shadowInner * shadowInner;

		for ( var i = 0; i < count && _instances.Count > 0; i++ )
		{
			var idx = (_shadowLodCursor + i) % _instances.Count;
			var instance = _instances[idx];
			if ( !instance.GameObject.IsValid() || instance.Renderer is null || !instance.Renderer.IsValid() )
				continue;

			var distSq = (instance.GameObject.WorldPosition - observer).LengthSquared;
			var wantShadows = instance.ShadowsEnabled
				? distSq <= shadowOuterSq
				: distSq <= shadowInnerSq;

			if ( instance.Renderer.RenderType != (wantShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off) )
				instance.Renderer.RenderType = wantShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;

			instance.ShadowsEnabled = wantShadows;
		}

		_shadowLodCursor = _instances.Count > 0 ? (_shadowLodCursor + count) % _instances.Count : 0;
	}

	static BBox ResolveBounds( Model model )
	{
		var bounds = TerraingenAnchoredPhysics.GetTightModelBounds( model );
		if ( bounds.Size.LengthSquared > 1e-12f )
			return bounds;

		return new BBox( new Vector3( -24f, -24f, 0f ), new Vector3( 24f, 24f, 72f ) );
	}

	sealed class ThornsBoulderInstance
	{
		public ThornsBoulderInstance( GameObject gameObject, ModelRenderer renderer )
		{
			GameObject = gameObject;
			Renderer = renderer;
			ShadowsEnabled = true;
		}

		public GameObject GameObject { get; }
		public ModelRenderer Renderer { get; }
		public bool ShadowsEnabled { get; set; }
	}
}
