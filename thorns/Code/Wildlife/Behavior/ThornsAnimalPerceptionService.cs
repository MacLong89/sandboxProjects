namespace Sandbox;

/// <summary>
/// Perception helpers — stealth, threat radius, scan jitter.
/// Wraps <see cref="ThornsWildlifePerception"/> for relationship-aware detection.
/// </summary>
public static class ThornsAnimalPerceptionService
{
	public static float GetEffectiveDetectionRadius(
		ThornsWildlifeSpeciesDefinition observerDef,
		ThornsAnimalBehaviorProfile observerProfile )
	{
		var baseRadius = observerDef.AggroRadius > 0f
			? observerDef.AggroRadius
			: observerDef.FearRadius > 0f
				? observerDef.FearRadius
				: observerDef.LeashRadius * 0.55f;
		return baseRadius * observerProfile.DetectionRadiusMul;
	}

	public static float GetEffectiveThreatRadiusForPrey(
		ThornsWildlifeSpeciesDefinition predatorDef,
		ThornsAnimalBehaviorProfile predatorProfile,
		ThornsAnimalBehaviorProfile preyProfile )
	{
		var chaseRadius = predatorDef.AggroRadius > 0f
			? predatorDef.AggroRadius
			: predatorDef.LoseRadius * 0.55f;
		var radius = chaseRadius * 0.72f;
		radius *= preyProfile.ThreatDetectionMul;
		radius *= predatorProfile.StealthMultiplier;
		return radius;
	}

	public static float JitteredScanInterval( ThornsAnimalBehaviorProfile profile, int entityId )
	{
		var hash = MathF.Abs( entityId % 97 ) / 97f;
		return profile.ScanIntervalSeconds * ( 0.85f + hash * 0.3f );
	}

	public static bool PreyShouldReactEarly(
		ThornsAnimalBehaviorProfile preyProfile,
		ThornsAnimalRelationshipKind relationship )
	{
		if ( !ThornsAnimalRelationshipTable.ShouldFlee( relationship ) )
			return false;

		return preyProfile.Fearfulness >= 0.75f;
	}
}
