namespace Dynasty.UI.Management;

/// <summary>
/// Every openable UI surface in the game. No ad-hoc modal flags allowed outside the manager.
/// </summary>
public enum UiWindowType
{
	None = 0,
	GameViewer,
	DraftCeremony,
	WeekSummary,
	PostGameCelebration,
	FormationPicker,
	TeamReleaseConfirm,
	TeamExtendDialog,
	TeamTradeDialog,
	MainMenuConfirm,
	AdvanceTimeMenu,
	TeamProfile,
	SessionSummary,
	FourthDownDecision,
	TutorialTip
}
