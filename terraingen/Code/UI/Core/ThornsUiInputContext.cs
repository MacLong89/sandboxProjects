namespace Terraingen.UI.Core;

/// <summary>Single active input context — only one system owns input at a time.</summary>
public enum ThornsUiInputContextKind
{
	Gameplay,
	Hotbar,
	BuildMenu,
	Dialogue,
	Pause,
	Menu,
	Modal,
	MainMenu
}

/// <summary>Derives the active input context from central UI state.</summary>
public static class ThornsUiInputContext
{
	public static ThornsUiInputContextKind Active
	{
		get
		{
			if ( ThornsUiManager.ActiveContext == ThornsUiManager.UiContext.MainMenu )
				return ThornsUiInputContextKind.MainMenu;

			if ( ThornsUiManager.IsOpen( "tab-menu" ) )
				return ThornsUiInputContextKind.Menu;

			if ( ThornsUiManager.IsOpen( "radio-shop" ) )
				return ThornsUiInputContextKind.Dialogue;

			if ( ThornsUiManager.IsOpen( "world-container" )
			     || ThornsUiManager.IsOpen( "research-station" )
			     || ThornsUiManager.IsOpen( "gameplay-modal" )
			     || ThornsUiManager.IsOpen( "session-recap" ) )
				return ThornsUiInputContextKind.Modal;

			if ( ThornsUiManager.IsOpen( "build-menu" ) )
				return ThornsUiInputContextKind.BuildMenu;

			if ( ThornsUiManager.BlocksGameplayInput )
				return ThornsUiInputContextKind.Pause;

			return ThornsUiInputContextKind.Gameplay;
		}
	}

	public static bool ShouldHandleCancel( ThornsUiInputContextKind handler ) =>
		handler == Active;

	public static bool AllowsHotbarInput =>
		Active is ThornsUiInputContextKind.Gameplay or ThornsUiInputContextKind.Hotbar;

	public static bool AllowsBuildInput =>
		Active is ThornsUiInputContextKind.Gameplay or ThornsUiInputContextKind.BuildMenu;

	public static bool AllowsHudTick =>
		Active is ThornsUiInputContextKind.Gameplay or ThornsUiInputContextKind.Hotbar or ThornsUiInputContextKind.BuildMenu;

	public static bool AllowsTransientFeedback =>
		( Active is ThornsUiInputContextKind.Gameplay
			or ThornsUiInputContextKind.Hotbar
			or ThornsUiInputContextKind.BuildMenu )
		&& ThornsUiManager.TopPriority < ThornsUiPriority.CriticalPopup;
}
