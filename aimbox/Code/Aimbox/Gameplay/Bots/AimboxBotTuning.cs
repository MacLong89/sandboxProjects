namespace Sandbox;

public static class AimboxBotTuning
{
	static float DistanceScale => AimboxArenaConfig.MapScale * AimboxArenaConfig.ActiveCombatScale;

	public static float SightRange => 4500f * AimboxArenaConfig.ActiveCombatScale;
	public const float SightFovDegrees = 130f;
	public static float CloseNoticeDistance => 1800f * AimboxArenaConfig.ActiveCombatScale;
	public const float LosCheckInterval = 0.12f;
	public const float LosCheckCloseInterval = 0.04f;
	public const float HearingRadiusRifle = 1200f;
	public const float HearingRadiusPistol = 800f;
	public static float HearingRadiusSprintFootsteps => 420f * DistanceScale;
	public static float HearingRadiusWalkFootsteps => 260f * DistanceScale;
	public const float MovementNoiseEmitInterval = 0.35f;
	public const float MemoryKeepSeconds = 6f;
	public const float MemoryForgetSeconds = 15f;
	public const float MemorySearchSeconds = 4.5f;
	public static float MemorySearchRadius => 220f * DistanceScale;
	public static float InvestigateArrivalDistance => 400f * AimboxArenaConfig.ActiveCombatScale;
	public static float HuntSprintDistance => 3000f * AimboxArenaConfig.ActiveCombatScale;
	public static float AdsMinDistance => 350f * AimboxArenaConfig.ActiveCombatScale;
	public const float ReactionDelaySeconds = 0.22f;
	public const float HipAimErrorDegrees = 1.5f;
	public const float AdsAimErrorDegrees = 0.6f;
	public static float AimErrorReferenceDistance => 420f * DistanceScale;
	public const float MinAimErrorScale = 0.32f;
	public static float LongRangeEngageDistance => 560f * DistanceScale;
	public const float LongRangeAimSmoothSpeed = 11f;
	public const float BotAdsPelletSpreadMultiplier = 0.9f;
	public const float StrafeFlipMinSeconds = 1.2f;
	public const float StrafeFlipMaxSeconds = 2f;
	public const float CrouchStationarySeconds = 0.4f;
	public const int BurstMinRounds = 3;
	public const int BurstMaxRounds = 5;
	public const float BurstPauseMinSeconds = 0.35f;
	public const float BurstPauseMaxSeconds = 0.72f;
	public const float ReloadMagFractionThreshold = 0.25f;
	public const float AimSmoothSpeed = 9f;
	public static float WanderLeashRadius => 300f * DistanceScale;
	public static float WanderMinGoalDistance => 120f * DistanceScale;
	public static float WanderArrivalDistance => 48f * DistanceScale;
	public const float WanderPauseMinSeconds = 1.75f;
	public const float WanderPauseMaxSeconds = 4.25f;
	public const float WanderSpeedMultiplier = 0.72f;
	public const float WanderLookSmoothSpeed = 2.75f;
	public const float WanderTurnSpeed = 3.25f;
	public const float WanderMaxWalkAngle = 32f;
	public const float WanderStuckSeconds = 2.25f;
	public static float WanderStuckMinProgress => 28f * DistanceScale;
	public static float WanderWalkProbeDistance => 56f * DistanceScale;
	public static float CenterObjectiveArrivalDistance => 420f * AimboxArenaConfig.ActiveCombatScale;
	public static float CenterLegArrivalDistance => 70f * DistanceScale;
	public static float CenterLegMinDistance => 150f * DistanceScale;
	public static float CenterLegMaxDistance => 300f * DistanceScale;
	public static float CenterLateralMinOffset => 90f * DistanceScale;
	public static float CenterLateralMaxOffset => 165f * DistanceScale;
	public static float CenterAdvanceSpeedMultiplier => 0.86f;
	public const float CenterPauseMinSeconds = 0.55f;
	public const float CenterPauseMaxSeconds = 1.35f;
	public static float CenterStuckMinProgress => 24f * DistanceScale;
	public const float CenterStuckSeconds = 1.15f;
	public static float CenterSteerProbeDistance => 110f * DistanceScale;
	public static float CenterLegClearanceSearchRadius => 100f * DistanceScale;
	public static float CenterGoalClearanceSearchRadius => 160f * DistanceScale;
	public static float CenterLookDistance => 320f * DistanceScale;
	public const float CenterSpawnPocketExitYFraction = 0.14f;
	public const float CenterSpawnPocketActiveWallThicknesses = 7f;
	/// <summary>Smooths world wish direction changes (strafe flips, steering snaps).</summary>
	public const float WishDirectionSmoothSpeed = 11f;
	/// <summary>Smooths obstacle-steering output so probes don't snap 45–90°.</summary>
	public const float SteeringSmoothSpeed = 9f;
	/// <summary>Smooths engage strafe side changes instead of instant ± flip.</summary>
	public const float StrafeSignSmoothSpeed = 5f;
	/// <summary>Grounded horizontal velocity blend rate (per second).</summary>
	public const float MotorGroundAcceleration = 11f;
	public const float MotorAirAcceleration = 4f;
	/// <summary>Minimum time before dropping Engage after LOS loss (reduces mode flicker).</summary>
	public const float EngageMinHoldSeconds = 0.45f;
	public static float EngageIdealDistance => 380f * DistanceScale;
	public static float EngageTooCloseDistance => 240f * DistanceScale;
	public static float EngageTooFarDistance => 520f * DistanceScale;
}
