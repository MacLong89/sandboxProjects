namespace Offshore;

/// <summary>
/// Explicit game states. Phase 1–2 drive Boot through CastFailed / HookInWater;
/// later phases fill bite, menus, travel, and tournaments.
/// </summary>
public enum FishingSessionState
{
	Boot,
	Loading,
	DockIdle,
	AimingCast,
	ChargingCast,
	Casting,
	HookInWater,
	CastFailed,
	WaitingForBite,
	BiteWindow,
	FishHooked,
	Reeling,
	CatchSuccess,
	FishEscaped,
	CoolerFull,
	Selling,
	UpgradeMenu,
	EquipmentMenu,
	BoatMenu,
	JournalMenu,
	LocationSelection,
	Traveling,
	FishingFromBoat,
	Tournament,
	Paused
}
