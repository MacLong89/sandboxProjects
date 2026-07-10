namespace Dynasty.UI.Management;

/// <summary>
/// Static facade for input. Components must not listen for Escape independently.
/// </summary>
public static class DynastyUiInput
{
	public static DynastyUiInputManager Manager => DynastyUiInputManager.Instance;

	public static DynastyUiInputContext ActiveContext => Manager.ActiveContext;

	public static bool HandleEscape() => Manager.HandleEscape( DynastyUiManager.Instance );
}
