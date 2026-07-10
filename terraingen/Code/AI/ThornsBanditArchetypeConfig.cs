namespace Terraingen.AI;

/// <summary>Config-driven bandit tuning.</summary>
public sealed class ThornsBanditArchetypeConfig
{
	public ThornsBanditType Type { get; init; } = ThornsBanditType.Scavenger;
	public ThornsBanditSkillLevel Skill { get; init; } = ThornsBanditSkillLevel.Average;

	public float VisionRangeWorld { get; init; } = 900f;
	public float VisionConeDegrees { get; init; } = 115f;
	/// <summary>Max shoot / sustained combat distance once a target is acquired.</summary>
	public float EngagementRangeWorld { get; init; } = 1400f;
	public float LoseTargetRangeWorld { get; init; } = 1725f;

	public float HearGunshotRangeWorld { get; init; } = 2200f;
	public float HearExplosionRangeWorld { get; init; } = 3200f;
	public float HearHarvestRangeWorld { get; init; } = 280f;
	public float HearAnimalAttackRangeWorld { get; init; } = 450f;
	public float HearBuildingBreakRangeWorld { get; init; } = 600f;

	public float PatrolRadiusWorld { get; init; } = 520f;
	public float LeashRadiusWorld { get; init; } = 420f;
	public float CommunicationRadiusWorld { get; init; } = 650f;

	public float ReactionTimeMinSeconds { get; init; } = 0.22f;
	public float ReactionTimeMaxSeconds { get; init; } = 0.38f;
	public float InvestigateTimeoutSeconds { get; init; } = 14f;
	public float RetreatRecoverSeconds { get; init; } = 8f;
	public float TargetLockSeconds { get; init; } = 6f;

	/// <summary>How long to pursue after the target leaves gun range before returning to leash patrol.</summary>
	public float ChaseDurationSeconds { get; init; } = 9f;

	/// <summary>How far beyond <see cref="LeashRadiusWorld"/> the prey may be before chase is abandoned.</summary>
	public float ChaseGraceBeyondLeashWorld { get; init; } = 160f;

	/// <summary>Extra pursuit distance past max gun range while in chase (bandit-to-target).</summary>
	public float ChaseOvershootBeyondGunRangeWorld { get; init; } = 480f;

	/// <summary>Max extra ally-alert delay for the furthest group member (waterfall alert).</summary>
	public float GroupAlertMaxSpreadSeconds { get; init; } = 1.6f;

	public float VisionTickSeconds { get; init; } = 0.25f;
	public float DecisionTickSeconds { get; init; } = 0.33f;
	public float PathTickSeconds { get; init; } = 0.33f;

	public float RetreatHealthFraction { get; init; } = 0.28f;
	public float CoverSearchRadiusWorld { get; init; } = 420f;
	public bool CanRetreat { get; init; } = true;

	public static ThornsBanditArchetypeConfig Scavenger() => new()
	{
		Type = ThornsBanditType.Scavenger,
		Skill = ThornsBanditSkillLevel.Poor,
		VisionRangeWorld = 1000f,
		EngagementRangeWorld = 1300f,
		LoseTargetRangeWorld = 1600f,
		PatrolRadiusWorld = 380f,
		LeashRadiusWorld = 520f,
		ChaseDurationSeconds = 11f,
		CommunicationRadiusWorld = 550f,
		ReactionTimeMinSeconds = 0.32f,
		ReactionTimeMaxSeconds = 0.52f,
		CanRetreat = true,
	};

	public static ThornsBanditArchetypeConfig CityDefender() => new()
	{
		Type = ThornsBanditType.CityDefender,
		Skill = ThornsBanditSkillLevel.Average,
		VisionRangeWorld = 1050f,
		EngagementRangeWorld = 1450f,
		LoseTargetRangeWorld = 1750f,
		PatrolRadiusWorld = 360f,
		LeashRadiusWorld = 520f,
		ChaseDurationSeconds = 10f,
		ChaseOvershootBeyondGunRangeWorld = 520f,
		GroupAlertMaxSpreadSeconds = 1.85f,
		CommunicationRadiusWorld = 750f,
		ReactionTimeMinSeconds = 0.24f,
		ReactionTimeMaxSeconds = 0.38f,
		CanRetreat = false,
	};

	public static ThornsBanditArchetypeConfig AirdropDefender() => new()
	{
		Type = ThornsBanditType.AirdropDefender,
		Skill = ThornsBanditSkillLevel.Veteran,
		VisionRangeWorld = 1150f,
		EngagementRangeWorld = 1500f,
		LoseTargetRangeWorld = 1800f,
		PatrolRadiusWorld = 320f,
		LeashRadiusWorld = 460f,
		ChaseDurationSeconds = 8f,
		CommunicationRadiusWorld = 900f,
		ReactionTimeMinSeconds = 0.18f,
		ReactionTimeMaxSeconds = 0.28f,
		CanRetreat = false,
	};
}
