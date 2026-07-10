using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-side planar (XY) hash grid of <see cref="ThornsWildlifeBrain"/> — rebuilt once per physics step; used for peer separation instead of scanning every brain per motor tick.
/// </summary>
public sealed class ThornsHostWildlifeSpatialIndex
{
	readonly Dictionary<long, List<ThornsWildlifeBrain>> _buckets = new();
	readonly Stack<List<ThornsWildlifeBrain>> _bucketPool = new();

	public float CellSize { get; set; } = ThornsPerformanceBudgets.HostWildlifeSpatialCellSizeWorld;

	public int LastRebuildBucketCount { get; private set; }

	public int LastRebuildBrainCount { get; private set; }

	public void Clear()
	{
		foreach ( var kv in _buckets )
		{
			kv.Value.Clear();
			_bucketPool.Push( kv.Value );
		}

		_buckets.Clear();
		LastRebuildBucketCount = 0;
		LastRebuildBrainCount = 0;
	}

	public void Rebuild( IReadOnlyList<ThornsWildlifeBrain> brains )
	{
		Clear();
		if ( brains is null || brains.Count == 0 )
			return;

		LastRebuildBrainCount = brains.Count;
		var cs = Math.Max( 32f, CellSize );

		for ( var i = 0; i < brains.Count; i++ )
		{
			var brain = brains[i];
			if ( brain is null || !brain.IsValid() )
				continue;

			var go = brain.GameObject;
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
					: new List<ThornsWildlifeBrain>( 4 );
				_buckets[key] = list;
			}

			list.Add( brain );
		}

		LastRebuildBucketCount = _buckets.Count;
	}

	static long CellKey( int ix, int iz ) =>
		( (long)ix << 32 ) ^ (uint)iz;

	/// <summary>Collects brains within planar radius (XY); caller supplies cleared <paramref name="results"/>.</summary>
	public void QueryNearPlanar(
		Vector3 flat,
		float radius,
		List<ThornsWildlifeBrain> results,
		ThornsWildlifeBrain exclude = null )
	{
		results.Clear();
		var cs = Math.Max( 32f, CellSize );
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
					var brain = list[i];
					if ( brain is null || !brain.IsValid() || ReferenceEquals( brain, exclude ) )
						continue;

					var w = brain.GameObject.WorldPosition;
					var dx = w.x - flat.x;
					var dy = w.y - flat.y;
					if ( dx * dx + dy * dy <= r2 )
						results.Add( brain );
				}
			}
		}
	}
}
