namespace Terraingen.Animals;

/// <summary>Distance bands for AI update scheduling (animals, future bandits).</summary>
public enum ThornsNpcLodTier : byte
{
	Full = 0,
	Reduced = 1,
	Sleeping = 2
}

public static class ThornsNpcLod
{
	public const float FullDistance = 2200f;
	public const float ReducedDistance = 5200f;
	public const float FullDistanceSq = FullDistance * FullDistance;
	public const float ReducedDistanceSq = ReducedDistance * ReducedDistance;

	public static ThornsNpcLodTier TierForDistance( float distance ) =>
		TierForDistanceSquared( distance * distance );

	public static ThornsNpcLodTier TierForDistanceSquared( float distanceSq )
	{
		if ( distanceSq <= FullDistanceSq )
			return ThornsNpcLodTier.Full;

		if ( distanceSq <= ReducedDistanceSq )
			return ThornsNpcLodTier.Reduced;

		return ThornsNpcLodTier.Sleeping;
	}

	public static float DetectionIntervalScale( ThornsNpcLodTier tier ) => tier switch
	{
		ThornsNpcLodTier.Full => 1f,
		ThornsNpcLodTier.Reduced => 2.5f,
		ThornsNpcLodTier.Sleeping => 8f,
		_ => 4f
	};

	public static float TickIntervalScale( ThornsNpcLodTier tier ) => tier switch
	{
		ThornsNpcLodTier.Full => 1f,
		ThornsNpcLodTier.Reduced => 2f,
		ThornsNpcLodTier.Sleeping => 6f,
		_ => 3f
	};

	public static bool ShouldRunSeparation( ThornsNpcLodTier tier ) => tier != ThornsNpcLodTier.Sleeping;

	public static bool ShouldRunBanditAi( ThornsNpcLodTier tier ) => tier != ThornsNpcLodTier.Sleeping;
}
