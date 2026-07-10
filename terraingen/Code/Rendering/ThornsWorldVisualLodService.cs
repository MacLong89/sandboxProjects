namespace Terraingen.Rendering;

using Terraingen.UI.Core;

/// <summary>Distance-based shadow LOD for proc buildings and other static world props (visual only).</summary>
[Title( "Thorns World Visual LOD" )]
[Category( "Thorns/Rendering" )]
public sealed class ThornsWorldVisualLodService : Component
{
	public static ThornsWorldVisualLodService Instance { get; private set; }

	[Property, Group( "Shadow LOD" )]
	public float ProcBuildingShadowDistanceInches { get; set; } = ThornsVisualPerformanceDistances.ProcBuildingShadowInches;

	[Property, Group( "Shadow LOD" )]
	public float ShadowLodHysteresisInches { get; set; } = ThornsVisualPerformanceDistances.ShadowLodHysteresisInches;

	[Property, Group( "Shadow LOD" ), Range( 1, 96 )]
	public int BuildingsUpdatedPerFrame { get; set; } = 48;

	[Property, Group( "Shadow LOD" ), Range( 0.05f, 1f )]
	public float UpdateIntervalSeconds { get; set; } = 0.08f;

	[Property, Group( "Shadow LOD" ), Range( 0f, 8000f )]
	public float MinObserverMoveInches { get; set; } = 1200f;

	readonly List<ProcBuildingLodEntry> _buildings = new();

	GameObject _observerObject;
	CameraComponent _observerCamera;
	TimeUntil _nextObserverRefresh;
	TimeUntil _nextUpdate;
	Vector3 _lastObserverPosition;
	bool _hasLastObserverPosition;
	int _buildingCursor;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public static ThornsWorldVisualLodService EnsureForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return null;

		foreach ( var existing in scene.GetAllComponents<ThornsWorldVisualLodService>() )
		{
			if ( existing is not null && existing.IsValid() )
				return existing;
		}

		var root = scene.CreateObject( true );
		root.Name = "Thorns Visual LOD";
		return root.Components.Create<ThornsWorldVisualLodService>();
	}

	public void RegisterProcBuildingField( GameObject buildingsRoot )
	{
		if ( buildingsRoot is null || !buildingsRoot.IsValid() )
			return;

		foreach ( var child in buildingsRoot.Children )
		{
			if ( child.IsValid() && child.Name.StartsWith( "ProcBuilding_", StringComparison.Ordinal ) )
				RegisterProcBuildingRoot( child );
		}
	}

	public void RegisterProcBuildingRoot( GameObject buildingRoot )
	{
		if ( buildingRoot is null || !buildingRoot.IsValid() )
			return;

		foreach ( var entry in _buildings )
		{
			if ( entry.Root == buildingRoot )
				return;
		}

		var renderers = new List<ModelRenderer>( 64 );
		foreach ( var renderer in buildingRoot.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer is not null && renderer.IsValid() )
				renderers.Add( renderer );
		}

		if ( renderers.Count == 0 )
			return;

		_buildings.Add( new ProcBuildingLodEntry( buildingRoot, renderers ) );
	}

	public void ClearProcBuildings() => _buildings.Clear();

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless || ThornsMenuPerformance.IsOverlayUiOpen )
			return;

		if ( _buildings.Count == 0 )
			return;

		var observer = ThornsSceneObserver.Resolve( Scene, ref _observerObject, ref _observerCamera, ref _nextObserverRefresh );
		if ( !ShouldUpdate( observer ) )
			return;

		UpdateBuildingShadowLod( observer );
	}

	bool ShouldUpdate( Vector3 observer )
	{
		if ( !_nextUpdate )
		{
			_nextUpdate = UpdateIntervalSeconds;
			_lastObserverPosition = observer;
			_hasLastObserverPosition = true;
			return true;
		}

		if ( !_hasLastObserverPosition )
		{
			_lastObserverPosition = observer;
			_hasLastObserverPosition = true;
			return true;
		}

		var minMove = MinObserverMoveInches;
		if ( minMove <= 0f || (observer - _lastObserverPosition).LengthSquared >= minMove * minMove )
		{
			_lastObserverPosition = observer;
			return true;
		}

		return false;
	}

	void UpdateBuildingShadowLod( Vector3 observer )
	{
		var hysteresis = ShadowLodHysteresisInches;
		var shadowOuter = ProcBuildingShadowDistanceInches + hysteresis;
		var shadowOuterSq = shadowOuter * shadowOuter;
		var shadowInner = MathF.Max( ProcBuildingShadowDistanceInches - hysteresis, 0f );
		var shadowInnerSq = shadowInner * shadowInner;
		var nearBandSq = shadowOuterSq * 1.15f * 1.15f;
		var farBudget = Math.Max( 1, BuildingsUpdatedPerFrame / 2 );

		for ( var i = 0; i < _buildings.Count; i++ )
		{
			var entry = _buildings[i];
			if ( !entry.Root.IsValid() )
				continue;

			var distSq = (entry.Center - observer).LengthSquared;
			if ( distSq > nearBandSq )
				continue;

			ApplyShadowStateForDistance( entry, distSq, shadowOuterSq, shadowInnerSq );
		}

		for ( var i = 0; i < farBudget && _buildings.Count > 0; i++ )
		{
			var idx = (_buildingCursor + i) % _buildings.Count;
			var entry = _buildings[idx];
			if ( !entry.Root.IsValid() )
				continue;

			var distSq = (entry.Center - observer).LengthSquared;
			if ( distSq <= nearBandSq )
				continue;

			ApplyShadowStateForDistance( entry, distSq, shadowOuterSq, shadowInnerSq );
		}

		_buildingCursor = _buildings.Count > 0 ? (_buildingCursor + farBudget) % _buildings.Count : 0;
	}

	static void ApplyShadowStateForDistance(
		ProcBuildingLodEntry entry,
		float distSq,
		float shadowOuterSq,
		float shadowInnerSq )
	{
		var wantShadows = entry.ShadowsEnabled
			? distSq <= shadowOuterSq
			: distSq <= shadowInnerSq;

		ApplyShadowState( entry, wantShadows );
	}

	static void ApplyShadowState( ProcBuildingLodEntry entry, bool wantShadows )
	{
		if ( entry.ShadowsEnabled == wantShadows )
			return;

		entry.ShadowsEnabled = wantShadows;
		var renderType = wantShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;
		foreach ( var renderer in entry.Renderers )
		{
			if ( renderer is null || !renderer.IsValid() )
				continue;

			renderer.RenderType = renderType;
		}
	}

	sealed class ProcBuildingLodEntry
	{
		public ProcBuildingLodEntry( GameObject root, List<ModelRenderer> renderers )
		{
			Root = root;
			Renderers = renderers;
			Center = root.WorldPosition;
			ShadowsEnabled = true;
		}

		public GameObject Root { get; }
		public List<ModelRenderer> Renderers { get; }
		public Vector3 Center { get; }
		public bool ShadowsEnabled { get; set; }
	}
}
