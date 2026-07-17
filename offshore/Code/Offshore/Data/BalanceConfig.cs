namespace Offshore;

/// <summary>
/// Central balance values for early phases. Later phases add fish/economy tables here
/// instead of scattering constants across controllers.
/// </summary>
public sealed class BalanceConfig
{
	public static BalanceConfig Defaults { get; } = new();

	public BalanceConfig Clone()
	{
		return new BalanceConfig
		{
			MinAimDegrees = MinAimDegrees,
			MaxAimDegrees = MaxAimDegrees,
			DefaultAimDegrees = DefaultAimDegrees,
			AimSpeedDegrees = AimSpeedDegrees,
			ChargeRate = ChargeRate,
			MinChargeToCast = MinChargeToCast,
			MinCastDistance = MinCastDistance,
			MaxCastDistance = MaxCastDistance,
			CastFlightSeconds = CastFlightSeconds,
			HookSubmerge = HookSubmerge,
			ArcPeakScale = ArcPeakScale,
			CastTimeoutSeconds = CastTimeoutSeconds,
			LandedHoldSeconds = LandedHoldSeconds,
			FailedHoldSeconds = FailedHoldSeconds,
			MinBiteSeconds = MinBiteSeconds,
			MaxBiteSeconds = MaxBiteSeconds,
			BiteReactionSeconds = BiteReactionSeconds,
			ReelProgressPerSecond = ReelProgressPerSecond,
			TensionGainPerSecond = TensionGainPerSecond,
			TensionReleasePerSecond = TensionReleasePerSecond,
			LineBreakTension = LineBreakTension,
			FishStaminaDrainWhileReeling = FishStaminaDrainWhileReeling,
			FishStaminaRecoverPerSecond = FishStaminaRecoverPerSecond,
			FishProgressStealPerSecond = FishProgressStealPerSecond,
			CatchResultHoldSeconds = CatchResultHoldSeconds,
			EscapeHoldSeconds = EscapeHoldSeconds,
			RareFishBonus = RareFishBonus,
			StartingMoney = StartingMoney,
			StartingCoolerCapacity = StartingCoolerCapacity,
			StartingLocationId = StartingLocationId,
			StartingLocationName = StartingLocationName
		};
	}

	// --- Cast ---
	public float MinAimDegrees { get; set; } = -5f;
	public float MaxAimDegrees { get; set; } = 70f;
	public float DefaultAimDegrees { get; set; } = 35f;
	public float AimSpeedDegrees { get; set; } = 55f;
	public float ChargeRate { get; set; } = 0.85f;
	public float MinChargeToCast { get; set; } = 0.08f;
	public float MinCastDistance { get; set; } = 8f;
	public float MaxCastDistance { get; set; } = 28f;
	public float CastFlightSeconds { get; set; } = 0.55f;
	public float HookSubmerge { get; set; } = OffshoreConstants.BobberBelowPlayerZ;
	public float ArcPeakScale { get; set; } = 0.35f;
	public float CastTimeoutSeconds { get; set; } = 2.5f;
	public float LandedHoldSeconds { get; set; } = 1.25f;
	public float FailedHoldSeconds { get; set; } = 0.85f;

	// --- Bite ---
	public float MinBiteSeconds { get; set; } = 1.4f;
	public float MaxBiteSeconds { get; set; } = 4.2f;
	public float BiteReactionSeconds { get; set; } = 1.35f;

	// --- Reel ---
	public float ReelProgressPerSecond { get; set; } = 0.38f;
	public float TensionGainPerSecond { get; set; } = 0.42f;
	public float TensionReleasePerSecond { get; set; } = 0.55f;
	public float LineBreakTension { get; set; } = 0.92f;
	public float FishStaminaDrainWhileReeling { get; set; } = 0.22f;
	public float FishStaminaRecoverPerSecond { get; set; } = 0.18f;
	public float FishProgressStealPerSecond { get; set; } = 0.12f;
	public float CatchResultHoldSeconds { get; set; } = 1.6f;
	public float EscapeHoldSeconds { get; set; } = 1.1f;
	public float RareFishBonus { get; set; } = 0f;

	// --- Starting progression ---
	public float StartingMoney { get; set; } = 0f;
	public int StartingCoolerCapacity { get; set; } = 6;

	// --- Location ---
	public string StartingLocationId { get; set; } = "old_dock";
	public string StartingLocationName { get; set; } = "Old Dock";
}
