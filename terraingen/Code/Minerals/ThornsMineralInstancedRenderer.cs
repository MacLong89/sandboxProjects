namespace Terraingen.Minerals;

using System.Runtime.InteropServices;
using Terraingen.UI.Core;

/// <summary>GPU-instanced mineral draws — colliders stay on lightweight GameObjects.</summary>
[Title( "Thorns Mineral Instanced Renderer" )]
[Category( "Terrain" )]
public sealed class ThornsMineralInstancedRenderer : Component
{
	readonly Dictionary<Vector2Int, ThornsMineralChunkInstances> _chunks = new();
	readonly object _chunksGate = new();

	Model _model;
	ThornsMineralConfig _config;
	MineralInstancedSceneObject _sceneObject;
	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	Vector3 _observer;
	bool _ready;

	public void Begin( Model model, ThornsMineralConfig config )
	{
		_model = model;
		_config = config;
		_ready = model.IsValid;
	}

	public void RegisterChunk( ThornsMineralChunkInstances chunk )
	{
		if ( chunk is null || chunk.TotalCount == 0 )
			return;

		lock ( _chunksGate )
			_chunks[chunk.Cell] = chunk;

		if ( _ready && !Application.IsDedicatedServer && !Application.IsHeadless )
			EnsureSceneObject();
	}

	public void Clear()
	{
		lock ( _chunksGate )
			_chunks.Clear();

		InvalidateSceneObject();
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
		var cullSq = _config.CullDistanceInches * _config.CullDistanceInches;

		lock ( _chunksGate )
		{
			foreach ( var chunk in _chunks.Values )
			{
				var distSq = (chunk.Center - observer).LengthSquared;
				chunk.Culled = distSq > cullSq;
			}
		}
	}

	void EnsureSceneObject()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		if ( _sceneObject is not null && _sceneObject.IsValid() )
			return;

		if ( Scene?.SceneWorld is not { } world )
			return;

		_sceneObject = new MineralInstancedSceneObject( world, _model, _config, _chunks, _chunksGate );
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

sealed class MineralInstancedSceneObject : SceneCustomObject
{
	readonly Model _model;
	readonly ThornsMineralConfig _config;
	readonly Dictionary<Vector2Int, ThornsMineralChunkInstances> _chunks;
	readonly object _chunksGate;

	// Scratch buffers are per-thread: RenderSceneObject can be invoked concurrently
	// across render views (main pass + shadow passes), so instance-shared lists would
	// be corrupted mid-iteration (IndexOutOfRange).
	[ThreadStatic] static List<ThornsMineralChunkInstances> _chunkScratch;
	[ThreadStatic] static List<Transform> _stoneScratch;
	[ThreadStatic] static List<Transform> _oreScratch;
	[ThreadStatic] static List<Transform> _shadowScratch;
	[ThreadStatic] static List<Transform> _noShadowScratch;

	public Vector3 Observer { get; set; }

	public MineralInstancedSceneObject(
		SceneWorld world,
		Model model,
		ThornsMineralConfig config,
		Dictionary<Vector2Int, ThornsMineralChunkInstances> chunks,
		object chunksGate ) : base( world )
	{
		_model = model;
		_config = config;
		_chunks = chunks;
		_chunksGate = chunksGate;
		RenderLayer = SceneRenderLayer.Default;
		Bounds = new BBox( new Vector3( -500000f ), new Vector3( 500000f ) );
	}

	public override void RenderSceneObject()
	{
		if ( !_model.IsValid || _config is null )
			return;

		var service = ThornsMineralWorldService.Instance;

		var chunks = _chunkScratch ??= new List<ThornsMineralChunkInstances>( 128 );
		var stoneScratch = _stoneScratch ??= new List<Transform>( 128 );
		var oreScratch = _oreScratch ??= new List<Transform>( 32 );

		// Snapshot the shared dictionary under lock — it is mutated from the main thread
		// (RegisterChunk/Clear) while this runs on the render thread.
		chunks.Clear();
		lock ( _chunksGate )
		{
			foreach ( var chunk in _chunks.Values )
				chunks.Add( chunk );
		}

		foreach ( var chunk in chunks )
		{
			if ( chunk.Culled )
				continue;

			stoneScratch.Clear();
			oreScratch.Clear();
			CollectVisibleTransforms( chunk, service, MineralKind.Stone, stoneScratch );
			CollectVisibleTransforms( chunk, service, MineralKind.Ore, oreScratch );

			if ( stoneScratch.Count > 0 )
				DrawKindSplit( stoneScratch, MineralKind.Stone );

			if ( oreScratch.Count > 0 )
				DrawKindSplit( oreScratch, MineralKind.Ore );
		}
	}

	static void CollectVisibleTransforms(
		ThornsMineralChunkInstances chunk,
		ThornsMineralWorldService service,
		MineralKind kind,
		List<Transform> output )
	{
		var transforms = kind == MineralKind.Ore ? chunk.Ore : chunk.Stone;
		var nodeIds = kind == MineralKind.Ore ? chunk.OreNodeIds : chunk.StoneNodeIds;

		// These lists are appended to on the main thread while we read here on the render
		// thread. Snapshot the counts once and clamp so a concurrent Add can never push an
		// index past the live count.
		var transformCount = transforms.Count;
		var nodeCount = nodeIds.Count;
		var pairedCount = Math.Min( transformCount, nodeCount );

		for ( var i = 0; i < pairedCount; i++ )
		{
			if ( i >= transforms.Count || i >= nodeIds.Count )
				break;

			if ( service is not null && service.IsNodeDepleted( nodeIds[i] ) )
				continue;

			output.Add( transforms[i] );
		}

		for ( var i = pairedCount; i < transformCount; i++ )
		{
			if ( i >= transforms.Count )
				break;

			output.Add( transforms[i] );
		}
	}

	void DrawKindSplit(
		List<Transform> transforms,
		MineralKind kind )
	{
		if ( transforms.Count == 0 )
			return;

		var shadowScratch = _shadowScratch ??= new List<Transform>( 256 );
		var noShadowScratch = _noShadowScratch ??= new List<Transform>( 256 );

		shadowScratch.Clear();
		noShadowScratch.Clear();
		var count = transforms.Count;

		for ( var i = 0; i < count; i++ )
		{
			var xf = transforms[i];
			var distSq = (xf.Position - Observer).LengthSquared;
			var wantShadows = ThornsMineralShadowLod.WantsShadowAtDistance(
				distSq,
				_config,
				kind == MineralKind.Stone );

			if ( wantShadows )
				shadowScratch.Add( xf );
			else
				noShadowScratch.Add( xf );
		}

		if ( shadowScratch.Count > 0 )
			DrawKindBatch( shadowScratch, kind, castShadows: true );

		if ( noShadowScratch.Count > 0 )
			DrawKindBatch( noShadowScratch, kind, castShadows: false );
	}

	void DrawKindBatch(
		List<Transform> transforms,
		MineralKind kind,
		bool castShadows )
	{
		if ( transforms.Count == 0 )
			return;

		Flags.CastShadows = castShadows;

		var material = ThornsMineralTintMaterials.Get( _model, kind, _config );
		if ( material is not null && material.IsValid )
			SetMaterialOverride( material );

		Graphics.DrawModelInstanced( _model, CollectionsMarshal.AsSpan( transforms ) );
		ClearMaterialOverride();
	}
}
