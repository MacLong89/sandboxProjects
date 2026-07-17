namespace Deep;

/// <summary>
/// Explicit DEEP game states. Only one active phase at a time.
/// Reserved values exist so later phases can expand without reshuffling.
/// </summary>
public enum GamePhase
{
	Boot,
	Loading,
	SurfaceIdle,
	PreparingDive,
	Diving,
	AtCheckpoint,
	DiveSuccess,
	DiveFailed,
	ReturningToSurface,
	Selling,
	UpgradeMenu,
	EquipmentMenu,
	DiverHub,
	JournalMenu,
	ZoneSelection,
	VehicleSelection,
	Paused,
	GameOver
}
