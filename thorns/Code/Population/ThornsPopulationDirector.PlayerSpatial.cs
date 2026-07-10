namespace Sandbox;

public sealed partial class ThornsPopulationDirector
{
	readonly List<GameObject> _playerRoots = new();
	readonly List<GameObject> _playerLodNearestScratch = new();
	readonly ThornsHostPlayerSpatialIndex _playerSpatial = new();
	double _nextPlayerRefreshTime;

	static readonly List<GameObject> FallbackPlayerRoots = new();
	static readonly List<GameObject> FallbackQueryScratch = new();
	static readonly ThornsHostPlayerSpatialIndex FallbackPlayerSpatial = new();
	static double _fallbackNextRefreshTime;

	/// <summary>Cached player pawn roots (host authoritative). Canonical owner — directors are compatibility wrappers.</summary>
	public static IReadOnlyList<GameObject> HostGetCachedPlayerRoots()
	{
		if ( !HostIsAuthoritativeForPlayerCache() )
			return Array.Empty<GameObject>();

		var inst = Instance;
		if ( inst is not null && inst.IsValid() )
			return inst.HostRefreshPlayerCacheIfNeeded();

		return HostRefreshFallbackPlayerCacheIfNeeded();
	}

	public static void HostQueryPlayersNearPlanar( Vector3 selfFlat, float radiusWorld, List<GameObject> results )
	{
		results.Clear();
		if ( !HostIsAuthoritativeForPlayerCache() )
			return;

		var inst = Instance;
		if ( inst is not null && inst.IsValid() )
		{
			inst.HostRefreshPlayerCacheIfNeeded();
			var q = radiusWorld * ThornsPerformanceBudgets.HostPlayerSpatialQueryRadiusInflateMul;
			inst._playerSpatial.QueryNearPlanar( selfFlat, q, results );
			ThornsAiPerceptionMetrics.RecordPlayerSpatialQuery( results.Count );
			return;
		}

		HostRefreshFallbackPlayerCacheIfNeeded();
		var fq = radiusWorld * ThornsPerformanceBudgets.HostPlayerSpatialQueryRadiusInflateMul;
		FallbackPlayerSpatial.QueryNearPlanar( selfFlat, fq, results );
		ThornsAiPerceptionMetrics.RecordPlayerSpatialQuery( results.Count );
	}

	public static float HostNearestPlayerDistSqForWildlifeLod( Vector3 wildlifeFlat )
	{
		if ( !HostIsAuthoritativeForPlayerCache() )
			return float.MaxValue;

		var inst = Instance;
		var scratch = inst is not null && inst.IsValid() ? inst._playerLodNearestScratch : FallbackQueryScratch;
		scratch.Clear();
		var horizon = MathF.Sqrt( ThornsWildlifeLOD.FarSq ) + 64f;
		HostQueryPlayersNearPlanar( wildlifeFlat, horizon, scratch );
		return ThornsHostPlayerSpatialIndex.MinDistSqAlive( wildlifeFlat, scratch );
	}

	public static float HostNearestAlivePlayerDistSqWithin( Vector3 selfFlat, float maxDistanceWorld )
	{
		if ( !HostIsAuthoritativeForPlayerCache() )
			return float.MaxValue;

		var inst = Instance;
		var scratch = inst is not null && inst.IsValid() ? inst._spatialScratch : FallbackQueryScratch;
		scratch.Clear();
		HostQueryPlayersNearPlanar( selfFlat, maxDistanceWorld, scratch );
		return ThornsHostPlayerSpatialIndex.MinDistSqAlive( selfFlat, scratch );
	}

	public static int HostPlayerCacheRootCount => HostGetCachedPlayerRoots().Count;

	public static int HostPlayerSpatialGridCells =>
		Instance is not null && Instance.IsValid()
			? Instance._playerSpatial.LastRebuildBucketCount
			: FallbackPlayerSpatial.LastRebuildBucketCount;

	public static int HostPlayerSpatialGridPlayers =>
		Instance is not null && Instance.IsValid()
			? Instance._playerSpatial.LastRebuildPlayerCount
			: FallbackPlayerSpatial.LastRebuildPlayerCount;

	IReadOnlyList<GameObject> HostRefreshPlayerCacheIfNeeded()
	{
		if ( Time.Now < _nextPlayerRefreshTime )
			return _playerRoots;

		_nextPlayerRefreshTime = Time.Now + Math.Max( 0.35f, PlayerRefreshSeconds );
		HostRebuildPlayerCacheForScene( Scene, _playerRoots, _playerSpatial );
		return _playerRoots;
	}

	static IReadOnlyList<GameObject> HostRefreshFallbackPlayerCacheIfNeeded()
	{
		if ( Time.Now < _fallbackNextRefreshTime )
			return FallbackPlayerRoots;

		_fallbackNextRefreshTime = Time.Now + 2.0;
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			FallbackPlayerRoots.Clear();
			return FallbackPlayerRoots;
		}

		HostRebuildPlayerCacheForScene( scene, FallbackPlayerRoots, FallbackPlayerSpatial );
		return FallbackPlayerRoots;
	}

	static void HostRebuildPlayerCacheForScene(
		Scene scene,
		List<GameObject> roots,
		ThornsHostPlayerSpatialIndex spatial )
	{
		roots.Clear();
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var pawn in scene.GetAllComponents<ThornsPawn>() )
		{
			if ( !pawn.IsValid() )
				continue;

			var root = pawn.GameObject;
			if ( !root.IsValid() )
				continue;

			if ( root.Components.Get<ThornsWildlifeBrain>( FindMode.EnabledInSelf ).IsValid() )
				continue;

			roots.Add( root );
		}

		spatial.CellSize = ThornsPerformanceBudgets.HostPlayerSpatialCellSizeWorld;
		spatial.Rebuild( roots );
		ThornsAiPerceptionMetrics.LastSpatialGridCells = spatial.LastRebuildBucketCount;
		ThornsAiPerceptionMetrics.LastSpatialGridPlayers = spatial.LastRebuildPlayerCount;
	}

	public static int HostCountWildlifeNearAnyPlayer( float radius )
	{
		var roots = HostGetCachedPlayerRoots();
		var best = 0;
		foreach ( var root in roots )
		{
			if ( !root.IsValid() )
				continue;

			var c = HostCountWildlifeNear( root.WorldPosition, radius );
			if ( c > best )
				best = c;
		}

		return best;
	}
}
