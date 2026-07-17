namespace FinalOutpost;

/// <summary>
/// Coordinate math for the large parcels ("plots") that surround the home base.
/// Plot (0,0) is the home base; rings expand outward up to <see cref="GameConstants.PlotGridRadius"/>.
/// </summary>
public static class PlotGrid
{
	public static int Radius =>
		GameCore.Instance?.IsCure == true ? CureConstants.PlotGridRadius : GameConstants.PlotGridRadius;

	public static bool InGrid( int x, int y ) => Math.Abs( x ) <= Radius && Math.Abs( y ) <= Radius;

	public static bool IsHome( int x, int y ) => x == 0 && y == 0;

	public static int Ring( int x, int y ) => Math.Max( Math.Abs( x ), Math.Abs( y ) );

	public static string Key( int x, int y ) => $"{x},{y}";

	public static bool ParseKey( string key, out int x, out int y )
	{
		x = 0; y = 0;
		if ( string.IsNullOrWhiteSpace( key ) ) return false;
		var parts = key.Split( ',' );
		return parts.Length == 2 && int.TryParse( parts[0], out x ) && int.TryParse( parts[1], out y );
	}

	public static Vector3 CenterWorld( int x, int y )
	{
		var wx = x * GameConstants.PlotSize;
		var wy = y * GameConstants.PlotSize;
		return new Vector3( wx, wy, OutpostTerrain.SampleHeight( wx, wy ) );
	}

	public static bool WorldToPlot( Vector3 world, out int x, out int y )
	{
		x = (int)MathF.Round( world.x / GameConstants.PlotSize );
		y = (int)MathF.Round( world.y / GameConstants.PlotSize );
		return InGrid( x, y );
	}

	/// <summary>Deterministic resource assigned to a plot (home plot has none). Wood or stone only.</summary>
	public static ResourceKind ResourceAt( int x, int y )
	{
		if ( IsHome( x, y ) ) return ResourceKind.None;
		return (Hash( x, y ) % 2) == 0 ? ResourceKind.Wood : ResourceKind.Stone;
	}

	/// <summary>Cure mode uses special plot features; survival uses standard resources.</summary>
	public static ResourceKind HarvestResourceAt( int x, int y ) =>
		GameCore.Instance?.IsCure == true ? PlotFeatureGrid.ResourceAt( x, y ) : ResourceAt( x, y );

	public static PlotKind FeatureKindAt( int x, int y ) =>
		GameCore.Instance?.IsCure == true ? PlotFeatureGrid.KindAt( x, y ) : PlotKind.Standard;

	public static double BuyCost( int x, int y )
	{
		var ring = Math.Max( 0, Ring( x, y ) - 1 );
		return GameConstants.PlotBuyBaseCost * Math.Pow( GameConstants.PlotBuyCostPerRing, ring );
	}

	public static double BuyCostEffective( int x, int y )
	{
		var cost = BuyCost( x, y );
		var core = GameCore.Instance;
		if ( core?.IsCure == true )
			cost *= TeamBonuses.PlotClaimCostMult( core );
		return cost;
	}

	private static int Hash( int x, int y )
	{
		unchecked
		{
			var h = (x * 73856093) ^ (y * 19349663) ^ 0x2f3b;
			return h & 0x7fffffff;
		}
	}
}
