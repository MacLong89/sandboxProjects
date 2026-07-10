namespace Terraingen.UI.Core;

using Terraingen.Player;

/// <summary>Mouse cursor visibility for main menu vs captured gameplay.</summary>
public static class ThornsUiCursor
{
	public static void EnsureMainMenuVisible()
	{
		if ( Mouse.Visibility != MouseVisibility.Visible )
			Mouse.Visibility = MouseVisibility.Visible;
	}

	public static void ApplyGameplayMenuOpen( bool menuOpen )
	{
		Mouse.Visibility = menuOpen ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	/// <summary>True when gameplay should show a free cursor (menus/overlays), not during FPS aim.</summary>
	public static bool GameplayWantsVisibleCursor()
	{
		if ( !Game.IsPlaying )
			return false;

		if ( ThornsUiManager.ActiveContext == ThornsUiManager.UiContext.MainMenu )
			return true;

		return ThornsMenuHost.IsOpen
		       || ThornsMenuHost.IsWorldContainerOpen
		       || ThornsPlayerGameplay.Local?.IsAwaitingWorldContainerUi == true
		       || ThornsMenuHost.IsRadioShopOpen
		       || ThornsMenuHost.IsResearchOpen
		       || ThornsMenuHost.IsCampfireOpen
		       || ThornsMenuHost.IsWorkbenchOpen
		       || ThornsMenuHost.IsVictoryIntroOpen
		       || ThornsUiManager.IsModalOpen;
	}

	/// <summary>Reconcile cursor visibility with the active UI context every frame.</summary>
	public static void SyncForActiveContext()
	{
		if ( !Game.IsPlaying )
			return;

		if ( ThornsUiManager.ActiveContext == ThornsUiManager.UiContext.MainMenu )
		{
			EnsureMainMenuVisible();
			return;
		}

		ApplyGameplayMenuOpen( GameplayWantsVisibleCursor() );
	}
}
