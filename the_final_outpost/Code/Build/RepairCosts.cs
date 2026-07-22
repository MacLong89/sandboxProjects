namespace FinalOutpost;

/// <summary>
/// Cure repair pricing — Supplies can offset scrap when stocked, but repairs never hard-lock
/// if Supplies are empty (full scrap cost still works).
/// </summary>
public static class RepairCosts
{
	/// <summary>Base scrap from missing HP (no Supplies discount).</summary>
	public static double BaseScrapCost( float missingHp ) =>
		missingHp * GameConstants.RepairCostPerHp;

	public static double EffectiveScrapCost( double fullScrapCost )
	{
		if ( fullScrapCost <= 0 ) return 0;

		var core = GameCore.Instance;
		if ( core?.IsCure != true || core.Resources is null )
			return fullScrapCost;

		var supplies = core.Resources.Get( ResourceKind.Supplies );
		if ( supplies <= 0 ) return fullScrapCost;

		var maxCover = fullScrapCost * CureConstants.SuppliesRepairMaxShare;
		var cover = Math.Min( maxCover, supplies * CureConstants.SuppliesScrapValue );
		return Math.Max( 0, fullScrapCost - cover );
	}

	public static double SuppliesToSpend( double fullScrapCost )
	{
		var scrap = EffectiveScrapCost( fullScrapCost );
		var covered = fullScrapCost - scrap;
		if ( covered <= 0 ) return 0;
		return covered / CureConstants.SuppliesScrapValue;
	}
}
