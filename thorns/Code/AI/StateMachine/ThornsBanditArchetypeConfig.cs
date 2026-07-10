namespace Sandbox;

/// <summary>Config-driven bandit tuning — one framework, three archetypes.</summary>
public sealed class ThornsBanditArchetypeConfig
{
	public ThornsBanditType Type { get; init; } = ThornsBanditType.Scavenger;

	public float VisionRangeWorld { get; init; } = 500f;
	public float VisionConeDegrees { get; init; } = 120f;

	public float HearGunshotRangeWorld { get; init; } = 2000f;
	public float HearExplosionRangeWorld { get; init; } = 3000f;
	public float HearSprintFootstepRangeWorld { get; init; } = 250f;
	public float HearAnimalAttackRangeWorld { get; init; } = 400f;

	public float RoamRadiusWorld { get; init; } = 1000f;
	public float PatrolRadiusWorld { get; init; } = 420f;
	public float LeashRadiusWorld { get; init; } = 400f;

	public float ChaseMaxDistanceWorld { get; init; } = 500f;
	public float AlertRadiusWorld { get; init; } = 500f;
	public float AggressionIgnoreDistanceWorld { get; init; } = 400f;

	public float ReactionTimeMinSeconds { get; init; } = 0.4f;
	public float ReactionTimeMaxSeconds { get; init; } = 1.5f;

	public float HitChance { get; init; } = ThornsBanditCombat.HumanNpcPlayerHitChanceDefault;
	public float ExtraSpreadHalfAngleDegrees { get; init; } = 0.55f;

	public bool CanFlee { get; init; }
	public float FleeHealthFraction { get; init; } = 0.30f;
	public float SeekCoverHealthFraction { get; init; } = 0.50f;

	public float DetectionRefreshIntervalSeconds { get; init; } = 0.35f;
	public float ThreatRefreshIntervalSeconds { get; init; } = 0.25f;
	public float StateThinkIntervalSeconds { get; init; } = 0.35f;
	public float SearchTimeoutSeconds { get; init; } = 20f;
	public float InvestigateTimeoutSeconds { get; init; } = 12f;

	public float DespawnNoPlayerRadiusWorld { get; init; } = 3000f;
	public float DespawnNoPlayerSeconds { get; init; } = 300f;

	public static ThornsBanditArchetypeConfig Scavenger() => new()
	{
		Type = ThornsBanditType.Scavenger,
		VisionRangeWorld = 500f,
		RoamRadiusWorld = 1000f,
		ChaseMaxDistanceWorld = 500f,
		AlertRadiusWorld = 500f,
		AggressionIgnoreDistanceWorld = 400f,
		ReactionTimeMinSeconds = 0.7f,
		ReactionTimeMaxSeconds = 1.5f,
		HitChance = 0.28f,
		ExtraSpreadHalfAngleDegrees = 0.85f,
		CanFlee = true,
		DespawnNoPlayerRadiusWorld = 3000f,
		DespawnNoPlayerSeconds = 300f,
	};

	public static ThornsBanditArchetypeConfig CityDefender() => new()
	{
		Type = ThornsBanditType.CityDefender,
		VisionRangeWorld = 750f,
		PatrolRadiusWorld = 520f,
		LeashRadiusWorld = 520f,
		ChaseMaxDistanceWorld = 1000f,
		AlertRadiusWorld = 1000f,
		AggressionIgnoreDistanceWorld = 0f,
		ReactionTimeMinSeconds = 0.45f,
		ReactionTimeMaxSeconds = 1.1f,
		HitChance = 0.34f,
		ExtraSpreadHalfAngleDegrees = 0.55f,
		CanFlee = false,
	};

	public static ThornsBanditArchetypeConfig AirdropDefender() => new()
	{
		Type = ThornsBanditType.AirdropDefender,
		VisionRangeWorld = 1000f,
		PatrolRadiusWorld = 450f,
		LeashRadiusWorld = 450f,
		ChaseMaxDistanceWorld = 1500f,
		AlertRadiusWorld = 1500f,
		AggressionIgnoreDistanceWorld = 0f,
		ReactionTimeMinSeconds = 0.35f,
		ReactionTimeMaxSeconds = 0.85f,
		HitChance = 0.38f,
		ExtraSpreadHalfAngleDegrees = 0.45f,
		CanFlee = false,
	};
}
