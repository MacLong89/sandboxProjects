using System.Collections.Generic;

namespace Sandbox;

/// <summary>Client-side distance culling for decorative foliage (grass/mushrooms).</summary>
[Title( "Thorns — Foliage Distance Culler" )]
[Category( "Thorns/World" )]
[Icon( "visibility_off" )]
public sealed class ThornsFoliageDistanceCullSystem : Component
{
	public const float DefaultHideDistance = 6800f;
	public const float DefaultShowDistance = 5600f;
	public const int DefaultMaxProcessedPerStep = 2400;
	public const float DefaultUpdateIntervalSeconds = 0.2f;

	const float SpatialCellSize = 4096f;
	const int NearBucketRadius = 1;

	[Property] public bool CullEnabled { get; set; } = true;
	[Property] public float HideDistance { get; set; } = DefaultHideDistance;
	[Property] public float ShowDistance { get; set; } = DefaultShowDistance;
	/// <summary>How many <see cref="ThornsFoliageCullProxy"/> entries get distance checks per tick.</summary>
	[Property] public int MaxProcessedPerStep { get; set; } = DefaultMaxProcessedPerStep;

	[Property] public float UpdateIntervalSeconds { get; set; } = DefaultUpdateIntervalSeconds;

	double _nextTick;
	double _nextCompact;
	int _cursor;

	readonly List<ThornsFoliageCullProxy> _proxies = new();
	readonly HashSet<ThornsFoliageCullProxy> _proxySet = new();
	readonly Dictionary<long, List<ThornsFoliageCullProxy>> _spatialBuckets = new();
	readonly List<ThornsFoliageCullProxy> _nearScratch = new();
	readonly HashSet<ThornsFoliageCullProxy> _nearProcessed = new();

	static ThornsFoliageDistanceCullSystem _sceneInstance;

	protected override void OnAwake()
	{
		_sceneInstance = this;
		ApplyQualityBudget( ThornsPerformanceQualityPresets.ActiveQuality );
	}

	public static void ApplyQualityBudget( ThornsPerformanceQuality quality )
	{
		var s = ThornsPerformanceQualityPresets.Get( quality );
		var maxProcessed = Math.Max( 256, s.FoliageCullMaxProcessedPerStep );
		var updateInterval = Math.Max( 0.08f, s.FoliageCullUpdateIntervalSeconds );

		if ( _sceneInstance is null || !_sceneInstance.IsValid() )
			return;

		_sceneInstance.MaxProcessedPerStep = maxProcessed;
		_sceneInstance.UpdateIntervalSeconds = updateInterval;
	}

	protected override void OnDestroy()
	{
		if ( _sceneInstance == this )
			_sceneInstance = null;

		_proxies.Clear();
		_proxySet.Clear();
		_spatialBuckets.Clear();
		base.OnDestroy();
	}

	internal static void RegisterProxy( ThornsFoliageCullProxy proxy )
	{
		if ( proxy is null || !proxy.IsValid() || _sceneInstance is null || !_sceneInstance.IsValid() )
			return;

		if ( !_sceneInstance._proxySet.Add( proxy ) )
			return;

		_sceneInstance._proxies.Add( proxy );
		_sceneInstance.AddToSpatialBucket( proxy );
	}

	internal static void UnregisterProxy( ThornsFoliageCullProxy proxy )
	{
		if ( proxy is null || _sceneInstance is null || !_sceneInstance.IsValid() )
			return;

		if ( !_sceneInstance._proxySet.Remove( proxy ) )
			return;

		_sceneInstance._proxies.Remove( proxy );
		_sceneInstance.RemoveFromSpatialBucket( proxy );
	}

	internal static bool TryGetCachedViewerPosition( out Vector3 pos )
	{
		if ( _sceneInstance is not null && _sceneInstance.IsValid() )
			return _sceneInstance.TryGetLocalViewerPosition( out pos );

		pos = default;
		return false;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !CullEnabled )
			return;

		if ( Time.Now < _nextTick )
			return;

		_nextTick = Time.Now + Math.Max( 0.03f, UpdateIntervalSeconds );

		using ( ThornsPerfDebug.Scope( "FoliageDistanceCull" ) )
		{
			if ( Time.Now >= _nextCompact )
			{
				_nextCompact = Time.Now + 8f;
				CompactProxyList();
			}

			if ( _proxies.Count == 0 )
				return;

			if ( !TryGetLocalViewerPosition( out var viewerPos ) )
				return;

			var budget = Math.Max( 32, MaxProcessedPerStep );
			var nearBudget = Math.Max( budget / 2, budget - 512 );
			var processed = ProcessNearBucketsFirst( viewerPos, nearBudget );
			processed += ProcessRoundRobin( viewerPos, budget - processed );
		}
	}

	int ProcessNearBucketsFirst( Vector3 viewerPos, int budget )
	{
		if ( budget <= 0 || _spatialBuckets.Count == 0 )
			return 0;

		_nearScratch.Clear();
		_nearProcessed.Clear();
		var viewerCell = WorldToCell( viewerPos );
		for ( var dy = -NearBucketRadius; dy <= NearBucketRadius; dy++ )
		for ( var dx = -NearBucketRadius; dx <= NearBucketRadius; dx++ )
		{
			var key = CellKey( viewerCell.x + dx, viewerCell.y + dy );
			if ( !_spatialBuckets.TryGetValue( key, out var bucket ) )
				continue;

			for ( var i = 0; i < bucket.Count; i++ )
			{
				var p = bucket[i];
				if ( !p.IsValid() || !p.GameObject.IsValid() || !_nearProcessed.Add( p ) )
					continue;

				_nearScratch.Add( p );
			}
		}

		var count = 0;
		for ( var i = 0; i < _nearScratch.Count && count < budget; i++ )
		{
			ApplyDistanceCull( _nearScratch[i], viewerPos );
			count++;
		}

		return count;
	}

	int ProcessRoundRobin( Vector3 viewerPos, int budget )
	{
		if ( budget <= 0 )
			return 0;

		var n = _proxies.Count;
		var steps = Math.Min( budget, n );
		var count = 0;
		for ( var i = 0; i < steps; i++ )
		{
			if ( _cursor >= n )
				_cursor = 0;

			var p = _proxies[_cursor++];
			if ( !p.IsValid() || !p.GameObject.IsValid() )
				continue;

			ApplyDistanceCull( p, viewerPos );
			count++;
		}

		return count;
	}

	void ApplyDistanceCull( ThornsFoliageCullProxy p, Vector3 viewerPos )
	{
		var hide = p.HideDistanceOverride > 0f ? p.HideDistanceOverride : HideDistance;
		var show = p.ShowDistanceOverride > 0f ? p.ShowDistanceOverride : ShowDistance;
		show = Math.Min( hide, Math.Max( 1f, show ) );
		var hideSq = hide * hide;
		var showSq = show * show;
		var d2 = (p.GameObject.WorldPosition - viewerPos).LengthSquared;

		if ( p.IsVisible )
		{
			if ( d2 > hideSq )
				p.SetVisible( false );
		}
		else if ( d2 < showSq )
			p.SetVisible( true );
	}

	void CompactProxyList()
	{
		for ( var i = _proxies.Count - 1; i >= 0; i-- )
		{
			var p = _proxies[i];
			if ( !p.IsValid() || !p.GameObject.IsValid() )
			{
				_proxySet.Remove( p );
				RemoveFromSpatialBucket( p );
				_proxies.RemoveAt( i );
			}
		}

		if ( _cursor >= _proxies.Count )
			_cursor = 0;
	}

	void AddToSpatialBucket( ThornsFoliageCullProxy proxy )
	{
		if ( !proxy.IsValid() || !proxy.GameObject.IsValid() )
			return;

		var cell = WorldToCell( proxy.GameObject.WorldPosition );
		var key = CellKey( cell.x, cell.y );
		if ( !_spatialBuckets.TryGetValue( key, out var bucket ) )
		{
			bucket = new List<ThornsFoliageCullProxy>( 8 );
			_spatialBuckets[key] = bucket;
		}

		bucket.Add( proxy );
	}

	void RemoveFromSpatialBucket( ThornsFoliageCullProxy proxy )
	{
		if ( !proxy.IsValid() || !proxy.GameObject.IsValid() )
			return;

		var cell = WorldToCell( proxy.GameObject.WorldPosition );
		var key = CellKey( cell.x, cell.y );
		if ( !_spatialBuckets.TryGetValue( key, out var bucket ) )
			return;

		bucket.Remove( proxy );
		if ( bucket.Count == 0 )
			_spatialBuckets.Remove( key );
	}

	static Vector2Int WorldToCell( Vector3 world ) =>
		new(
			(int)MathF.Floor( world.x / SpatialCellSize ),
			(int)MathF.Floor( world.y / SpatialCellSize ) );

	static long CellKey( int x, int y ) => ((long)x << 32) | (uint)y;

	bool TryGetLocalViewerPosition( out Vector3 pos ) => ThornsLocalViewer.TryGetWorldPosition( out pos );
}
