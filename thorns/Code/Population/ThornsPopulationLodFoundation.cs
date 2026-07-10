namespace Sandbox;

/// <summary>
/// LOD governance foundation — documents future PopulationDirector ownership of think/anim/perception LOD tiers.
/// Today: passthrough to existing wildlife/bandit LOD without behavior changes.
/// </summary>
public static class ThornsPopulationLodFoundation
{
	public static ThornsWildlifeLodTier HostComputeWildlifeLodTier( float nearestPlayerDistSq ) =>
		ThornsWildlifeLOD.ComputeTier( nearestPlayerDistSq );

	public static float HostWildlifeThinkIntervalMultiplier( ThornsWildlifeLodTier tier ) =>
		ThornsWildlifeLOD.ThinkIntervalMultiplier( tier );

	/// <summary>Bandit dormant think is distance-driven in <see cref="ThornsBanditBrain"/> — future central policy hook.</summary>
	public static bool HostBanditShouldUseDormantThink( float nearestInterestDistSq, float loseRadiusWorld ) =>
		nearestInterestDistSq > loseRadiusWorld * loseRadiusWorld;

	/// <summary>Future: anim skip radius per population kind. Not enforced here yet.</summary>
	public static bool HostShouldSkipLocomotionPresentation( ThornsWildlifeLodTier tier ) =>
		tier == ThornsWildlifeLodTier.Dormant;
}
