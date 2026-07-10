namespace Terraingen.Clutter;

using System.Runtime.InteropServices;
using Terraingen;
using Terraingen.UI.Core;

/// <summary>GPU-instanced draws for mesh clutter (grass, ferns, decorative rocks).</summary>
[Title( "Thorns Clutter Instanced Renderer" )]
[Category( "Terrain" )]
public sealed class ThornsClutterInstancedRenderer : Component
{
	readonly Dictionary<Vector2Int, ThornsClutterChunkInstances> _chunks = new();
	readonly Dictionary<Model, int> _modelIndex = new();
	readonly List<Transform> _drawScratch = new( 256 );

	Model[] _models = Array.Empty<Model>();
	ThornsClutterConfig _config;
	ClutterInstancedSceneObject _sceneObject;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Vector3 _observer;
	bool _ready;

	public void Begin( ThornsClutterConfig config, IEnumerable<Model> models )
	{
		_config = config;
		_modelIndex.Clear();
		var list = new List<Model>();

		foreach ( var model in models )
		{
			if ( !IsUsableModel( model ) )
				continue;

			if ( _modelIndex.ContainsKey( model ) )
				continue;

			_modelIndex[model] = list.Count;
			list.Add( model );
		}

		_models = list.ToArray();
		_ready = _models.Length > 0;
	}

	public int GetModelIndex( Model model )
	{
		if ( !IsUsableModel( model ) )
			return -1;

		return _modelIndex.TryGetValue( model, out var index ) ? index : -1;
	}

	public void RegisterChunk( ThornsClutterChunkInstances chunk )
	{
		if ( chunk is null || chunk.TotalCount == 0 )
			return;

		_chunks[chunk.Cell] = chunk;

		if ( _ready && !Application.IsDedicatedServer && !Application.IsHeadless )
			EnsureSceneObject();
	}

	public void UnregisterChunk( Vector2Int cell ) => _chunks.Remove( cell );

	public void Clear()
	{
		_chunks.Clear();
		InvalidateSceneObject();
		_modelIndex.Clear();
		_models = Array.Empty<Model>();
		_ready = false;
	}

	protected override void OnUpdate()
	{
		if ( ThornsMenuPerformance.IsOverlayUiOpen || !_ready || _config is null )
			return;

		if ( _chunks.Count > 0 )
			EnsureSceneObject();

		_observer = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		UpdateCulling( _observer );

		if ( _sceneObject is not null && _sceneObject.IsValid() )
			_sceneObject.Observer = _observer;
	}

	void UpdateCulling( Vector3 observer )
	{
		var cullSq = _config.ClutterRadius * _config.ClutterRadius;

		foreach ( var chunk in _chunks.Values )
			chunk.Culled = (chunk.Center - observer).LengthSquared > cullSq;
	}

	void EnsureSceneObject()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( _sceneObject is not null && _sceneObject.IsValid() )
			return;

		if ( Scene?.SceneWorld is not { } world )
			return;

		_sceneObject = new ClutterInstancedSceneObject( world, _models, _chunks );
		_sceneObject.Observer = _observer;
	}

	void InvalidateSceneObject()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnDisabled() => Clear();

	protected override void OnDestroy() => InvalidateSceneObject();

	static bool IsUsableModel( Model model ) => ThornsModelResourceLoad.IsUsable( model );
}

sealed class ClutterInstancedSceneObject : SceneCustomObject
{
	readonly Model[] _models;
	readonly Dictionary<Vector2Int, ThornsClutterChunkInstances> _chunks;
	readonly List<Transform> _drawScratch = new( 256 );

	public Vector3 Observer { get; set; }

	public ClutterInstancedSceneObject(
		SceneWorld world,
		Model[] models,
		Dictionary<Vector2Int, ThornsClutterChunkInstances> chunks ) : base( world )
	{
		_models = models;
		_chunks = chunks;
		RenderLayer = SceneRenderLayer.Default;
		Bounds = new BBox( new Vector3( -500000f ), new Vector3( 500000f ) );
		Flags.CastShadows = false;
	}

	public override void RenderSceneObject()
	{
		foreach ( var chunk in _chunks.Values )
		{
			if ( chunk.Culled )
				continue;

			foreach ( var (modelIndex, transforms) in chunk.ByModelIndex )
			{
				if ( modelIndex < 0 || modelIndex >= _models.Length || transforms.Count == 0 )
					continue;

				var model = _models[modelIndex];
				if ( !ThornsModelResourceLoad.IsUsable( model ) )
					continue;

				Graphics.DrawModelInstanced( model, CollectionsMarshal.AsSpan( transforms ) );
			}
		}
	}
}
