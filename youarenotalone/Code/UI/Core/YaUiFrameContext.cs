namespace Sandbox;

/// <summary>Per-frame gameplay snapshot for UI decisions — built once in HUD OnUpdate.</summary>
public sealed class YaUiFrameContext
{
	public bool IsLocalPlayer { get; set; }
	public bool InRound { get; set; }
	public bool InCombat { get; set; }
	public bool IsDead { get; set; }
	public bool IsSpectating { get; set; }
	public bool ShowPracticeChoice { get; set; }
	public bool ShowControlsTutorial { get; set; }
	public bool ShowScoreboard { get; set; }
	public bool ShowDeathOverlay { get; set; }
	public bool ShowRoundVictory { get; set; }
	public bool ShowHudCombat { get; set; }
	public bool ShowHudTopObjective { get; set; }
	public bool ShowHudTopLeftHints { get; set; }
	public bool ShowCrosshair { get; set; }
	public bool ShowParanoiaOverlays { get; set; }
	public bool ShowDamageFeedback { get; set; }
	public bool ShowEventFeed { get; set; }
	public bool ShowRoundStartAnnouncement { get; set; }
	public bool ShowLobbySoloHint { get; set; }
	public bool ShowFloatingMessages { get; set; }
	public bool RequiresMouse { get; set; }
}
