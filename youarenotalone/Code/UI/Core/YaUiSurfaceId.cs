namespace Sandbox;

/// <summary>Every major UI surface — visibility is owned by <see cref="YaUiManager"/> only.</summary>
public enum YaUiSurfaceId
{
	HudCombat,
	HudTopObjective,
	HudTopLeftHints,
	HudCrosshair,
	PassiveParanoia,
	PassiveDamage,
	NotificationEventFeed,
	NotificationRoundStart,
	NotificationLobbyHint,
	NotificationFloatingStack,
	ModalScrim,
	FullscreenPracticeChoice,
	FullscreenControlsTutorial,
	FullscreenScoreboard,
	FullscreenRoundVictory,
	CriticalDeathOverlay
}
