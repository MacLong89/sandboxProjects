namespace Terraingen.Foliage;

using System.Runtime.InteropServices;
using Terraingen.UI.Core;

/// <summary>GPU-instanced tree draws — replaces per-tree GameObjects when enabled.</summary>
[Title( "Thorns Foliage Instanced Renderer" )]
[Category( "Terrain" )]
public sealed class ThornsFoliageInstancedRenderer : Component
{
	readonly Dictionary<Vector2Int, ThornsFoliageChunkInstances> _chunks = new();

	ThornsFoliagePlacer.FoliageModelSet _models;
	ThornsFoliageConfig _config;
	FoliageInstancedSceneObject _sceneObject;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Vector3 _observer;
	bool _ready;

	public void Begin( ThornsFoliagePlacer.FoliageModelSet models, ThornsFoliageConfig config )
	{
		_models = models;
		_config = config;
		_ready = models.IsValid;

		if ( config.UseTreeBillboardLod )
			ThornsTreeBillboardAssets.Configure( config );
	}

	public void RegisterChunk( ThornsFoliageChunkInstances chunk )
	{
		if ( chunk is null || chunk.TotalCount == 0 )
			return;

		_chunks[chunk.Cell] = chunk;

		if ( _ready && !Application.IsDedicatedServer && !Application.IsHeadless )
			EnsureSceneObject();
	}

	public void Clear()
	{
		_chunks.Clear();
		InvalidateSceneObject();
		_ready = false;
	}

	protected override void OnUpdate()
	{
		if ( ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( !_ready || _config is null )
			return;

		if ( _chunks.Count > 0 )
			EnsureSceneObject();

		_observer = ResolveObserver();
		UpdateCulling( _observer );

		if ( _sceneObject is not null && _sceneObject.IsValid() )
			_sceneObject.Observer = _observer;
	}

	Vector3 ResolveObserver()
	{
		return ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
	}

	void UpdateCulling( Vector3 observer )
	{
		var cullSq = _config.CullDistanceInches * _config.CullDistanceInches;

		foreach ( var (_, chunk) in _chunks )
		{
			var distSq = (chunk.Center - observer).LengthSquared;
			chunk.Culled = distSq > cullSq;
		}
	}

	void EnsureSceneObject()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( _sceneObject is not null && _sceneObject.IsValid() )
			return;

		if ( Scene is null || !Scene.IsValid() )
			return;

		var world = Scene.SceneWorld;
		if ( world is null )
			return;

		_sceneObject = new FoliageInstancedSceneObject( world, _models, _chunks, _config );
		_sceneObject.Observer = _observer;
	}

	void InvalidateSceneObject()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnDisabled() => Clear();

	protected override void OnDestroy() => InvalidateSceneObject();
}

sealed class FoliageInstancedSceneObject : SceneCustomObject
{
	readonly ThornsFoliagePlacer.FoliageModelSet _models;
	readonly Dictionary<Vector2Int, ThornsFoliageChunkInstances> _chunks;
	readonly ThornsFoliageConfig _config;
	readonly List<Transform> _meshShadowScratch = new( 256 );
	readonly List<Transform> _meshNoShadowScratch = new( 256 );
	readonly List<Transform> _billboardScratch = new( 128 );

	public Vector3 Observer { get; set; }

	public FoliageInstancedSceneObject(
		SceneWorld world,
		ThornsFoliagePlacer.FoliageModelSet models,
		Dictionary<Vector2Int, ThornsFoliageChunkInstances> chunks,
		ThornsFoliageConfig config ) : base( world )
	{
		_models = models;
		_chunks = chunks;
		_config = config;
		RenderLayer = SceneRenderLayer.Default;
		Bounds = new BBox( new Vector3( -500000f ), new Vector3( 500000f ) );
	}

	public override void RenderSceneObject()
	{
		if ( !_models.IsValid || _config is null )
			return;

		var service = ThornsTreeWorldService.ResolveInstance();
		var observer = Observer;
		var shadowSq = _config.TreeLodShadowDistanceInches * _config.TreeLodShadowDistanceInches;
		var billboardSq = _config.TreeLodBillboardDistanceInches * _config.TreeLodBillboardDistanceInches;
		var hideSq = _config.TreeLodHideDistanceInches * _config.TreeLodHideDistanceInches;
		var useBillboards = _config.UseTreeBillboardLod && ThornsTreeBillboardAssets.IsReady;

		DrawSpecies( FoliageSpecies.Pine, _models.Get( FoliageSpecies.Pine ), service, observer, shadowSq, billboardSq, hideSq, useBillboards );
		DrawSpecies( FoliageSpecies.Aspen, _models.Get( FoliageSpecies.Aspen ), service, observer, shadowSq, billboardSq, hideSq, useBillboards );
		DrawSpecies( FoliageSpecies.Oak, _models.Get( FoliageSpecies.Oak ), service, observer, shadowSq, billboardSq, hideSq, useBillboards );
	}

	void DrawSpecies(
		FoliageSpecies species,
		Model model,
		ThornsTreeWorldService service,
		Vector3 observer,
		float shadowSq,
		float billboardSq,
		float hideSq,
		bool useBillboards )
	{
		if ( !model.IsValid )
			return;

		foreach ( var chunk in _chunks.Values )
		{
			if ( chunk.Culled )
				continue;

			var list = chunk.GetList( species );
			if ( list.Count == 0 )
				continue;

			_meshShadowScratch.Clear();
			_meshNoShadowScratch.Clear();
			_billboardScratch.Clear();

			for ( var i = 0; i < list.Count; i++ )
			{
				if ( service is not null && service.IsValid() && service.IsDepleted( chunk.Cell, species, i ) )
					continue;

				var xf = list[i];
				var distSq = (xf.Position - observer).LengthSquared;
				if ( distSq > hideSq )
					continue;

				if ( useBillboards && distSq > billboardSq )
				{
					_billboardScratch.Add( ThornsTreeBillboardAssets.BuildInstancedTransform(
						xf.Position,
						observer,
						xf.Scale,
						species,
						_config,
						model ) );
					continue;
				}

				if ( distSq > shadowSq )
					_meshNoShadowScratch.Add( xf );
				else
					_meshShadowScratch.Add( xf );
			}

			if ( _meshShadowScratch.Count > 0 )
			{
				Flags.CastShadows = true;
				Graphics.DrawModelInstanced( model, CollectionsMarshal.AsSpan( _meshShadowScratch ) );
			}

			if ( _meshNoShadowScratch.Count > 0 )
			{
				Flags.CastShadows = false;
				Graphics.DrawModelInstanced( model, CollectionsMarshal.AsSpan( _meshNoShadowScratch ) );
			}

			if ( _billboardScratch.Count > 0 && ThornsTreeBillboardAssets.PlaneModel.IsValid )
			{
				Flags.CastShadows = false;
				var material = ThornsTreeBillboardAssets.GetMaterial( species );
				if ( material is not null && material.IsValid )
					SetMaterialOverride( material );

				Graphics.DrawModelInstanced(
					ThornsTreeBillboardAssets.PlaneModel,
					CollectionsMarshal.AsSpan( _billboardScratch ) );

				ClearMaterialOverride();
			}
		}
	}
}
