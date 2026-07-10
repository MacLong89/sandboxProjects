namespace Dynasty.UI.Management;

/// <summary>
/// Single owner of input context. Only the active context receives keyboard input.
/// </summary>
public sealed class DynastyUiInputManager
{
	public static DynastyUiInputManager Instance { get; } = new();

	public DynastyUiInputContext ActiveContext { get; private set; } = DynastyUiInputContext.Hud;

	public event Action ContextChanged;

	public void UpdateFromManager( DynastyUiManager manager )
	{
		var next = ResolveContext( manager );
		if ( next == ActiveContext )
			return;

		ActiveContext = next;
		ContextChanged?.Invoke();
	}

	public bool HandleEscape( DynastyUiManager manager )
	{
		if ( ActiveContext == DynastyUiInputContext.Menu )
			return false;

		return manager.CloseTopmost();
	}

	static DynastyUiInputContext ResolveContext( DynastyUiManager manager )
	{
		var top = manager.TopmostWindow;
		if ( top?.Definition != null )
			return top.Definition.InputContext;

		return DynastyUiInputContext.Hud;
	}
}
