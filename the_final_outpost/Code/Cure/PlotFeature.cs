namespace FinalOutpost;

/// <summary>Special plot types for Road to a Cure — civ-lite world tiles beyond basic wood/stone/water.</summary>
public enum PlotKind
{
	Standard,
	FoodCache,
	SupplyDepot,
	TechRuins,
	NeutralCiv,
	BossLair
}

public sealed class PlotFeatureDef
{
	public PlotKind Kind { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
	public Color MarkerTint { get; init; }
}

public static class PlotFeatureCatalog
{
	public static PlotFeatureDef Get( PlotKind kind ) => All.FirstOrDefault( p => p.Kind == kind ) ?? All[0];

	public static readonly IReadOnlyList<PlotFeatureDef> All = new List<PlotFeatureDef>
	{
		new() { Kind = PlotKind.Standard, Name = "Standard Plot", Icon = "terrain", Description = "Wood, stone, or water.", MarkerTint = Color.White },
		new() { Kind = PlotKind.FoodCache, Name = "Food Cache", Icon = "restaurant", Description = "Rich farmland — large food payout when cleared.", MarkerTint = new Color( 0.45f, 0.85f, 0.4f ) },
		new() { Kind = PlotKind.SupplyDepot, Name = "Supply Depot", Icon = "inventory_2", Description = "Abandoned stockpile — supplies and scrap when cleared.", MarkerTint = new Color( 0.85f, 0.65f, 0.35f ) },
		new() { Kind = PlotKind.TechRuins, Name = "Tech Ruins", Icon = "biotech", Description = "Pre-collapse lab — knowledge and specimens.", MarkerTint = new Color( 0.55f, 0.75f, 0.95f ) },
		new() { Kind = PlotKind.NeutralCiv, Name = "Neighboring Colony", Icon = "location_city", Description = "Trade, ally, or raid for resources.", MarkerTint = new Color( 0.7f, 0.55f, 0.9f ) },
		new() { Kind = PlotKind.BossLair, Name = "Threat Nest", Icon = "pest_control", Description = "Clearing triggers a boss wave.", MarkerTint = new Color( 0.95f, 0.35f, 0.35f ) }
	};
}

public static class PlotFeatureGrid
{
	/// <summary>Deterministic special plot assignment (home is always standard).</summary>
	public static PlotKind KindAt( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) ) return PlotKind.Standard;

		var ring = PlotGrid.Ring( x, y );
		var h = Hash( x, y );

		// Boss lairs on outer ring corners.
		if ( ring >= PlotGrid.Radius && (Math.Abs( x ) == ring || Math.Abs( y ) == ring) && h % 7 == 0 )
			return PlotKind.BossLair;

		// Neighbor civs on mid-outer rings.
		if ( ring >= 2 && h % 11 == 0 )
			return PlotKind.NeutralCiv;

		return (h % 17) switch
		{
			0 => PlotKind.FoodCache,
			1 => PlotKind.SupplyDepot,
			2 => PlotKind.TechRuins,
			_ => PlotKind.Standard
		};
	}

	public static ResourceKind ResourceAt( int x, int y )
	{
		return KindAt( x, y ) switch
		{
			PlotKind.FoodCache => ResourceKind.Food,
			PlotKind.SupplyDepot => ResourceKind.Supplies,
			PlotKind.TechRuins => ResourceKind.Knowledge,
			_ => PlotGrid.ResourceAt( x, y )
		};
	}

	private static int Hash( int x, int y )
	{
		unchecked
		{
			var h = (x * 73856093) ^ (y * 19349663) ^ 0x5a7c;
			return h & 0x7fffffff;
		}
	}
}
