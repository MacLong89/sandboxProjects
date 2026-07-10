namespace Terraingen.AI;

/// <summary>Aimbox-style bandit engage tuning (spread, strafe, burst, reaction).</summary>
public static class ThornsBanditCombatTuning
{
	public const float ReactionDelaySeconds = 0.22f;
	public const float HipAimErrorDegrees = 1.5f;
	public const float AdsAimErrorDegrees = 0.6f;
	public const float AimErrorReferenceDistance = 420f;
	public const float MinAimErrorScale = 0.32f;
	public const float LongRangeEngageDistance = 560f;
	public const float LongRangeAimSmoothSpeed = 11f;
	public const float BotAdsPelletSpreadMultiplier = 0.9f;
	public const float StrafeFlipMinSeconds = 1.2f;
	public const float StrafeFlipMaxSeconds = 2f;
	public const float StrafeSignSmoothSpeed = 5f;
	public const int BurstMinRounds = 3;
	public const int BurstMaxRounds = 5;
	public const float BurstPauseMinSeconds = 0.35f;
	public const float BurstPauseMaxSeconds = 0.72f;
	public const float AimSmoothSpeed = 9f;
	public const float AdsMinDistance = 350f;
	public const float EngageIdealDistance = 380f;
	public const float EngageTooCloseDistance = 240f;
	public const float EngageTooFarDistance = 520f;

	public const float EngageMinHoldSeconds = 0.45f;
	public const float MemoryKeepSeconds = 6f;
	public const float MemoryForgetSeconds = 15f;
	public const float MemorySearchSeconds = 4.5f;
	public const float MemorySearchRadius = 220f;
	public const float InvestigateArrivalDistance = 72f;
	public const float CloseNoticeDistance = 360f;
	/// <summary>Within this range, bandits treat targets as visible without a LOS trace.</summary>
	public const float ImmediateNoticeNoLosDistance = 128f;
	public const float LosCheckInterval = 0.12f;
	public const float LosCheckCloseInterval = 0.04f;
	public const float CrouchStationarySeconds = 0.4f;
	public const float HuntSprintDistance = 420f;
}
