using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-side planar (XY) hash grid of player roots — owned by <see cref="ThornsPopulationDirector"/> cache rebuild cadence.
/// </summary>
public sealed class ThornsHostPlayerSpatialIndex
{
	readonly Dictionary<long, List<GameObject>> _buckets = new();
	readonly Stack<List<GameObject>> _bucketPool = new();

	public float CellSize { get; set; } = ThornsPerformanceBudgets.HostPlayerSpatialCellSizeWorld;

	public int LastRebuildBucketCount { get; private set; }

	public int LastRebuildPlayerCount { get; private set; }

	public void Clear()
	{
		foreach ( var kv in _buckets )
		{
			kv.Value.Clear();
			_bucketPool.Push( kv.Value );
		}

		_buckets.Clear();
		LastRebuildBucketCount = 0;
		LastRebuildPlayerCount = 0;
	}

	public void Rebuild( IReadOnlyList<GameObject> roots )
	{
		Clear();
		if ( roots is null || roots.Count == 0 )
			return;

		LastRebuildPlayerCount = roots.Count;
		var cs = Math.Max( 64f, CellSize );

		for ( var ri = 0; ri < roots.Count; ri++ )
		{
			var go = roots[ri];
			if ( go is null || !go.IsValid() )
				continue;

			var p = go.WorldPosition;
			var ix = (int)MathF.Floor( p.x / cs );
			var iz = (int)MathF.Floor( p.y / cs );
			var key = CellKey( ix, iz );
			if ( !_buckets.TryGetValue( key, out var list ) )
			{
				list = _bucketPool.Count > 0
					? _bucketPool.Pop()
					: new List<GameObject>( 4 );
				_buckets[key] = list;
			}

			list.Add( go );
		}

		LastRebuildBucketCount = _buckets.Count;
	}

	static long CellKey( int ix, int iz ) =>
		( (long)ix << 32 ) ^ (uint)iz;

	/// <summary>Collects alive-ish roots within planar radius (XY); caller supplies cleared <paramref name="results"/>.</summary>
	public void QueryNearPlanar( Vector3 flat, float radius, List<GameObject> results )
	{
		results.Clear();
		var cs = Math.Max( 64f, CellSize );
		var r = Math.Max( 1f, radius );
		var minX = (int)MathF.Floor( (flat.x - r) / cs );
		var maxX = (int)MathF.Floor( (flat.x + r) / cs );
		var minZ = (int)MathF.Floor( (flat.y - r) / cs );
		var maxZ = (int)MathF.Floor( (flat.y + r) / cs );
		var r2 = r * r;

		for ( var xi = minX; xi <= maxX; xi++ )
		{
			for ( var zi = minZ; zi <= maxZ; zi++ )
			{
				if ( !_buckets.TryGetValue( CellKey( xi, zi ), out var list ) )
					continue;

				for ( var i = 0; i < list.Count; i++ )
				{
					var go = list[i];
					if ( go is null || !go.IsValid() )
						continue;

					var w = go.WorldPosition;
					var dx = w.x - flat.x;
					var dy = w.y - flat.y;
					if ( dx * dx + dy * dy <= r2 )
						results.Add( go );
				}
			}
		}
	}

	public static float MinDistSqAlive( Vector3 flat, IReadOnlyList<GameObject> roots )
	{
		var best = float.MaxValue;
		if ( roots is null )
			return best;

		for ( var i = 0; i < roots.Count; i++ )
		{
			var go = roots[i];
			if ( go is null || !go.IsValid() )
				continue;

			var hp = go.Components.Get<ThornsHealth>();
			if ( hp.IsValid() && ( !hp.IsAlive || hp.IsDeadState ) )
				continue;

			if ( ThornsWildlifeMountRules.PawnIsMounted( go ) )
				continue;

			var p = go.WorldPosition;
			var dx = p.x - flat.x;
			var dy = p.y - flat.y;
			var d2 = dx * dx + dy * dy;
			if ( d2 < best )
				best = d2;
		}

		return best;
	}
}
