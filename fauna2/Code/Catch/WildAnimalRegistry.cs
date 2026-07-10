namespace Fauna2;

/// <summary>Local registry of wilderness animals — avoids scene scans each frame.</summary>
public static class WildAnimalRegistry
{
	private static readonly List<WildAnimalComponent> _all = new();
	private static readonly Dictionary<(int x, int y), int> _plotCounts = new();

	public static IReadOnlyList<WildAnimalComponent> All => _all;

	public static void Register( WildAnimalComponent wild )
	{
		if ( wild is null || !wild.IsValid() || _all.Contains( wild ) )
			return;

		_all.Add( wild );
		IncrementPlot( wild.PlotX, wild.PlotY );
	}

	public static void Unregister( WildAnimalComponent wild )
	{
		if ( wild is null || !_all.Remove( wild ) )
			return;

		DecrementPlot( wild.PlotX, wild.PlotY );
	}

	public static int CountOnPlot( int plotX, int plotY ) =>
		_plotCounts.GetValueOrDefault( (plotX, plotY) );

	public static int ActiveCount
	{
		get
		{
			var count = 0;
			foreach ( var wild in _all )
			{
				if ( wild.IsValid() )
					count++;
			}

			return count;
		}
	}

	public static WildAnimalComponent FindById( string wildId ) =>
		string.IsNullOrEmpty( wildId )
			? null
			: _all.FirstOrDefault( w => w.IsValid() && w.WildId == wildId );

	public static void Clear()
	{
		_all.Clear();
		_plotCounts.Clear();
	}

	private static void IncrementPlot( int plotX, int plotY )
	{
		var key = (plotX, plotY);
		_plotCounts[key] = _plotCounts.GetValueOrDefault( key ) + 1;
	}

	private static void DecrementPlot( int plotX, int plotY )
	{
		var key = (plotX, plotY);
		if ( !_plotCounts.TryGetValue( key, out var count ) )
			return;

		if ( count <= 1 )
			_plotCounts.Remove( key );
		else
			_plotCounts[key] = count - 1;
	}
}
