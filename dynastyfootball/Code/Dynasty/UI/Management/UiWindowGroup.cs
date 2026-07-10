namespace Dynasty.UI.Management;

/// <summary>
/// Mutual exclusivity groups. Only one window per group may be open at a time.
/// </summary>
public enum UiWindowGroup
{
	None,
	StandardDialog,
	FullscreenExperience,
	CriticalTakeover
}
