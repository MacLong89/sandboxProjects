namespace Dynasty.Core.Stats;

public static class StatAggregator
{
	public static void Merge( Dictionary<string, int> target, IReadOnlyDictionary<string, int> delta )
	{
		if ( target == null || delta == null )
			return;

		foreach ( var (key, value) in delta )
		{
			if ( value == 0 )
				continue;

			target[key] = target.GetValueOrDefault( key ) + value;
		}
	}

	public static void Add( Dictionary<string, int> stats, string key, int value )
	{
		if ( stats == null || value == 0 )
			return;

		stats[key] = stats.GetValueOrDefault( key ) + value;
	}
}
