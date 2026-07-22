namespace FinalOutpost;

/// <summary>Special plot types for Road to a Cure — civ-lite world tiles beyond basic wood/stone.</summary>
public enum PlotKind
{
	Standard,
	FoodCache,
	SupplyDepot,
	TechRuins,
	NeutralCiv
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
		new() { Kind = PlotKind.Standard, Name = "Standard Plot", Icon = "terrain", Description = "Wood or stone — forage while clearing, then claim materials for building.", MarkerTint = Color.White },
		new() { Kind = PlotKind.FoodCache, Name = "Food Cache", Icon = "restaurant", Description = "Rich farmland — large food payout when cleared.", MarkerTint = new Color( 0.45f, 0.85f, 0.4f ) },
		new() { Kind = PlotKind.SupplyDepot, Name = "Supply Depot", Icon = "inventory_2", Description = "Abandoned stockpile — supplies and scrap when cleared.", MarkerTint = new Color( 0.85f, 0.65f, 0.35f ) },
		new() { Kind = PlotKind.TechRuins, Name = "Tech Ruins", Icon = "biotech", Description = "Pre-collapse lab — clear for Knowledge to spend in the Tech Tree.", MarkerTint = new Color( 0.55f, 0.75f, 0.95f ) },
		new() { Kind = PlotKind.NeutralCiv, Name = "Neighboring Colony", Icon = "location_city", Description = "Trade, ally for steady support, or raid for a one-time haul.", MarkerTint = new Color( 0.7f, 0.55f, 0.9f ) }
	};
}

public static class PlotFeatureGrid
{
	const int CacheRollMod = 24;
	const int NeutralCivRollMod = 36;

	/// <summary>Deterministic special plot assignment (home is always standard).</summary>
	public static PlotKind KindAt( int x, int y )
	{
		if ( PlotGrid.IsHome( x, y ) ) return PlotKind.Standard;
		if ( GameCore.Instance?.IsCure != true ) return PlotKind.Standard;

		var h = PlotWorldRolls.Hash( x, y );

		// Legacy-style caches scattered on the larger map.
		if ( h % CacheRollMod == 0 )
		{
			var roll = (h / CacheRollMod) % 3;
			return roll switch
			{
				0 => PlotKind.FoodCache,
				1 => PlotKind.SupplyDepot,
				_ => PlotKind.TechRuins
			};
		}

		// Old neutral civ tiles (trade/ally) — still appear occasionally.
		if ( PlotGrid.Ring( x, y ) >= 2 && h % NeutralCivRollMod == 0 )
			return PlotKind.NeutralCiv;

		return PlotKind.Standard;
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
}
