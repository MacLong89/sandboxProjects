namespace OffshoreFishing.Core;

/// <summary>Deterministic xorshift64* RNG for portable simulation.</summary>
public sealed class SeededRng
{
	private ulong _state;

	public SeededRng( long seed )
	{
		_state = seed == 0 ? 0x9E3779B97F4A7C15UL : (ulong)seed;
		if ( _state == 0 ) _state = 1;
	}

	public long State => unchecked( (long)_state );

	public ulong NextULong()
	{
		var x = _state;
		x ^= x >> 12;
		x ^= x << 25;
		x ^= x >> 27;
		_state = x;
		return x * 0x2545F4914F6CDD1DUL;
	}

	public int NextInt( int minInclusive, int maxExclusive )
	{
		if ( maxExclusive <= minInclusive ) return minInclusive;
		var span = (uint)(maxExclusive - minInclusive);
		return minInclusive + (int)(NextULong() % span);
	}

	public float NextFloat() => (NextULong() >> 40) / (float)(1 << 24);

	public float NextFloat( float min, float max ) => min + (max - min) * NextFloat();

	public bool Chance( float probability ) => NextFloat() < probability;

	public T PickWeighted<T>( IReadOnlyList<T> items, Func<T, float> weight )
	{
		if ( items == null || items.Count == 0 )
			throw new InvalidOperationException( "No items to pick." );

		var total = 0f;
		for ( var i = 0; i < items.Count; i++ )
			total += Math.Max( 0f, weight( items[i] ) );

		if ( total <= 0f )
			return items[NextInt( 0, items.Count )];

		var roll = NextFloat() * total;
		var acc = 0f;
		for ( var i = 0; i < items.Count; i++ )
		{
			acc += Math.Max( 0f, weight( items[i] ) );
			if ( roll <= acc )
				return items[i];
		}

		return items[^1];
	}
}
