using Sandbox.UI;

namespace Sandbox;

public static class AimboxCursor
{
	public static bool GameplayMenuOpen { get; private set; }
	public static bool DebugVerbose { get; private set; }

	static MouseVisibility _lastApplied = MouseVisibility.Auto;
	static TimeUntil _nextDiagnosticLog;
	static bool _loggedEditorMouseDownHint;

	[ConCmd( "aimbox_cursor_debug" )]
	public static void SetDebugVerbose( bool enabled = true )
	{
		DebugVerbose = enabled;
		_nextDiagnosticLog = 0;
		Log.Info( $"[Aimbox Cursor] Verbose diagnostics {(enabled ? "enabled" : "disabled")}." );
	}

	public static void ApplyGameplayMenuOpen( bool menuOpen ) =>
		SetNeedsCursor( menuOpen );

	/// <summary>Apply cursor visibility from current meta UI state.</summary>
	public static void Sync()
	{
		SetNeedsCursor( AimboxMetaNavigation.RequiresMouseCursor() );
		MaybeLogDiagnostic( afterUi: false );
	}

	/// <summary>Re-apply after UI updates so ScreenPanel cannot leave the cursor visible during FPS play.</summary>
	public static void SyncAfterUi()
	{
		SetNeedsCursor( AimboxMetaNavigation.RequiresMouseCursor() );
		MaybeLogDiagnostic( afterUi: true );
	}

	static void SetNeedsCursor( bool needsCursor )
	{
		GameplayMenuOpen = needsCursor;

		if ( needsCursor )
		{
			_lastApplied = MouseVisibility.Visible;
			Mouse.Visibility = MouseVisibility.Visible;
			return;
		}

		if ( InputFocus.Current is not null )
			InputFocus.Clear();

		// Force hidden — Auto would re-show the cursor whenever HUD panels exist.
		_lastApplied = MouseVisibility.Hidden;
		Mouse.Visibility = MouseVisibility.Hidden;
	}

	static void MaybeLogDiagnostic( bool afterUi )
	{
		if ( GameplayMenuOpen )
			return;

		var actual = Mouse.Visibility;
		var mouseDown = Input.Down( "Attack1" ) || Input.Down( "Attack2" ) || Input.Down( "Use" );
		var editor = Application.IsEditor;
		var mismatch = actual != MouseVisibility.Hidden;
		var editorMouseDown = editor && mouseDown;

		if ( editorMouseDown && !_loggedEditorMouseDownHint )
		{
			_loggedEditorMouseDownHint = true;
			Log.Warning( "[Aimbox Cursor] Editor play mode can show the OS cursor while shooting (s&box issue #11221). Launch standalone (F5) to verify cursor hide." );
		}

		if ( !DebugVerbose && !mismatch && !editorMouseDown )
			return;

		if ( !DebugVerbose && editorMouseDown && !mismatch )
			return;

		if ( _nextDiagnosticLog )
			return;

		_nextDiagnosticLog = DebugVerbose ? 0.5f : 2f;

		var stage = afterUi ? "after-ui" : "sync";
		Log.Info(
			$"[Aimbox Cursor] {stage}: actual={actual} applied={_lastApplied} menu={GameplayMenuOpen} blocks={AimboxMetaNavigation.BlocksGameplay} metaUi={AimboxGame.Instance?.IsMetaUiActive} screen={AimboxMetaNavigation.CurrentScreen} mouseDown={mouseDown} editor={editor}" );
	}
}
