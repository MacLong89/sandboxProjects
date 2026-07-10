namespace Sandbox;

/// <summary>
/// Distance-based LOD tiers — drives think cadence only (THORNS_EVERYTHING_DOCUMENT §8 / §perf).
/// No per-frame global scans: callers pass nearest-player distance squared from cache.
/// </summary>
public enum ThornsWildlifeLodTier
{
	Near,
	Mid,
	Far,
	Dormant,
}

public static class ThornsWildlifeLOD
{
	public const float NearSq = 1400f * 1400f;
	public const float MidSq = 3200f * 3200f;
	public const float FarSq = 7200f * 7200f;

	public static ThornsWildlifeLodTier ComputeTier( float nearestPlayerDistSq )
	{
		if ( nearestPlayerDistSq <= NearSq )
			return ThornsWildlifeLodTier.Near;
		if ( nearestPlayerDistSq <= MidSq )
			return ThornsWildlifeLodTier.Mid;
		if ( nearestPlayerDistSq <= FarSq )
			return ThornsWildlifeLodTier.Far;
		return ThornsWildlifeLodTier.Dormant;
	}

	/// <summary>Multiplier applied to species base think intervals.</summary>
	public static float ThinkIntervalMultiplier( ThornsWildlifeLodTier tier ) =>
		tier switch
		{
			ThornsWildlifeLodTier.Near => 1f,
			ThornsWildlifeLodTier.Mid => 2.4f,
			ThornsWildlifeLodTier.Far => 5.5f,
			ThornsWildlifeLodTier.Dormant => 14f,
			_ => 1f
		};
}
