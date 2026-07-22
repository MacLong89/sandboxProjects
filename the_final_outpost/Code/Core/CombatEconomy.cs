namespace FinalOutpost;

/// <summary>Shared scrap helpers — place/recruit prices are fixed on each catalog entry.</summary>
public static class CombatEconomy
{
	/// <summary>
	/// Baseline engagement range in design units (pre-<see cref="GameConstants.RangeScale"/>).
	/// Used for gameplay comparisons, not pricing.
	/// </summary>
	public const float ReferenceRangeDesign = 320f;

	/// <summary>World-space baseline for gameplay comparisons.</summary>
	public static float ReferenceRange => ReferenceRangeDesign * GameConstants.RangeScale;

	/// <summary>Default scrap added to the next copy after each owned unit of that type.</summary>
	public const double DefaultRepeatBump = 10;

	public static float Dps( float damagePerShot, float fireInterval, int pellets = 1 ) =>
		fireInterval > 0f ? damagePerShot * pellets / fireInterval : 0f;

	/// <summary>First copy costs <paramref name="baseCost"/>; each already-owned copy adds <paramref name="bump"/>.</summary>
	public static double EscalatedCost( double baseCost, int alreadyOwned, double bump = DefaultRepeatBump )
	{
		if ( alreadyOwned < 0 ) alreadyOwned = 0;
		if ( bump < 0 ) bump = 0;
		// Never allow a free purchase from a missing/zero catalog entry.
		var cost = baseCost + alreadyOwned * bump;
		return cost > 0 ? cost : Math.Max( baseCost, DefaultRepeatBump );
	}
}
