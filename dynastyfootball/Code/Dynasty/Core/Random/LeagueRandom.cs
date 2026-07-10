namespace Dynasty.Core.Random;

/// <summary>
/// Deterministic-friendly RNG wrapper. League seeds can be stored for replay and multiplayer sync.
/// </summary>
public sealed class LeagueRandom : Interfaces.ILeagueRandom
{
	private readonly System.Random _random;

	public LeagueRandom( int seed ) => _random = new System.Random( seed );

	public LeagueRandom() => _random = new System.Random();

	public int NextInt( int minInclusive, int maxExclusive )
	{
		if ( maxExclusive <= minInclusive )
			return minInclusive;

		return _random.Next( minInclusive, maxExclusive );
	}

	public float NextFloat() => (float)_random.NextDouble();

	public bool Chance( float probability ) => NextFloat() < probability;

	public T Pick<T>( IReadOnlyList<T> items )
	{
		if ( items == null || items.Count == 0 )
			throw new ArgumentException( "Cannot pick from empty list.", nameof( items ) );

		return items[NextInt( 0, items.Count )];
	}
}
