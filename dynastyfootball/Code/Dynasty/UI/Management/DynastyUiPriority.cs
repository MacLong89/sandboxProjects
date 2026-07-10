namespace Dynasty.UI.Management;

/// <summary>
/// Strict render hierarchy. Higher values always render above lower values.
/// No component may assign z-index outside this scale.
/// </summary>
public enum DynastyUiPriority
{
	PassiveOverlay = 10,
	Hud = 20,
	Notification = 30,
	Tooltip = 40,
	Journal = 50,
	Dialog = 60,
	Screen = 70,
	Fullscreen = 80,
	Confirmation = 90,
	Critical = 100
}
