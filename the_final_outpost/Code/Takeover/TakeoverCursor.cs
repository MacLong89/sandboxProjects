namespace FinalOutpost;

/// <summary>
/// Force-hides the OS/UI cursor during FP possession.
/// ScreenPanel / clickable HUD otherwise flips visibility back to Auto/Visible.
/// Matches aimbox <c>AimboxCursor</c>.
/// </summary>
public static class TakeoverCursor
{
	static bool _loggedEditorVerdict;

	public static void Sync() => ApplyHidden();

	/// <summary>Re-apply after HUD / shoot input so ScreenPanel cannot leave the cursor visible.</summary>
	public static void SyncAfterUi() => ApplyHidden();

	/// <summary>Call once when entering possession — clears UI focus then locks the mouse.</summary>
	public static void EnterFps()
	{
		_loggedEditorVerdict = false;
		ApplyHidden();
	}

	/// <summary>Call when leaving FP — restores visible cursor for iso UI.</summary>
	public static void ExitFps( string reason )
	{
		_ = reason;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	/// <summary>Any code that wants the cursor visible should go through here.</summary>
	public static void ForceVisible( string source )
	{
		_ = source;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	static void ApplyHidden()
	{
		if ( RecruitTakeoverController.Instance?.IsPossessing != true )
			return;

		if ( InputFocus.Current is not null )
			InputFocus.Clear();

		Mouse.Visibility = MouseVisibility.Hidden;

		if ( Application.IsEditor && !_loggedEditorVerdict )
		{
			_loggedEditorVerdict = true;
			Log.Warning(
				"[FinalOutpost][TakeoverCursor] Editor play can still draw the OS cursor while Mouse.Visibility is Hidden " +
				"(s&box #11221, especially with a mouse button held). Use standalone F5 to verify." );
		}
	}
}
