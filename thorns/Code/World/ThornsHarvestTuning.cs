namespace Sandbox;

/// <summary>
/// THORNS_EVERYTHING_DOCUMENT §Harvest & resources — two reach bands + future tool multipliers (host-only math).
/// </summary>
public static class ThornsHarvestTuning
{
	/// <summary>Normal placement — longer reach than dense hand-placed nodes.</summary>
	public const float DefaultBandHarvestRange = 108f;

	/// <summary>Dense / POI-adjacent nodes — shorter authorized reach.</summary>
	public const float DenseBandHarvestRange = 82f;

	/// <summary>Future: axe/pick equipped reduces reach vs fist (multiplier &lt; 1).</summary>
	public const float FutureToolReachMultiplier = 1f;

	/// <summary>Future: upgraded harvesting / torches could extend beyond base band.</summary>
	public const float FutureExtendedReachMultiplier = 1.38f;
}
