namespace Terraingen.UI.Core;

/// <summary>Global mutual-exclusivity rules — no window decides compatibility locally.</summary>
public static class ThornsUiCompatibility
{
	public static bool CanCoexist( ThornsUiWindowKind a, ThornsUiWindowKind b )
	{
		if ( a == b )
			return false;

		// Tooltip and toasts may overlay most HUD surfaces.
		if ( a is ThornsUiWindowKind.Tooltip or ThornsUiWindowKind.Toast )
			return !IsExclusiveFullscreen( b );

		if ( b is ThornsUiWindowKind.Tooltip or ThornsUiWindowKind.Toast )
			return !IsExclusiveFullscreen( a );

		// HUD elements coexist with each other and transient feedback.
		if ( IsPersistentHud( a ) && IsPersistentHud( b ) )
			return true;

		if ( IsPersistentHud( a ) && b is ThornsUiWindowKind.Toast )
			return true;

		if ( IsPersistentHud( b ) && a is ThornsUiWindowKind.Toast )
			return true;

		// Build menu replaces hotbar — mutually exclusive by design.
		if ( (a, b) is (ThornsUiWindowKind.BuildMenu, ThornsUiWindowKind.Hotbar)
		     or (ThornsUiWindowKind.Hotbar, ThornsUiWindowKind.BuildMenu) )
			return false;

		// Fullscreen / modal surfaces never coexist with competing major panels.
		if ( IsExclusiveFullscreen( a ) || IsExclusiveFullscreen( b ) )
			return false;

		// Tab menu + world overlays are never allowed together.
		if ( a is ThornsUiWindowKind.TabMenu || b is ThornsUiWindowKind.TabMenu )
			return false;

		return true;
	}

	public static bool CanOpen( ThornsUiWindowKind kind )
	{
		foreach ( var open in ThornsUiManager.OpenWindowKinds )
		{
			if ( !CanCoexist( kind, open ) )
				return false;
		}

		return true;
	}

	public static ThornsUiPriority DefaultPriority( ThornsUiWindowKind kind ) => kind switch
	{
		ThornsUiWindowKind.Hud => ThornsUiPriority.Hud,
		ThornsUiWindowKind.Hotbar => ThornsUiPriority.Hotbar,
		ThornsUiWindowKind.BuildMenu => ThornsUiPriority.Hotbar,
		ThornsUiWindowKind.Tooltip => ThornsUiPriority.Tooltip,
		ThornsUiWindowKind.Toast => ThornsUiPriority.Toast,
		ThornsUiWindowKind.TabMenu => ThornsUiPriority.FullscreenMenu,
		ThornsUiWindowKind.WorldContainer => ThornsUiPriority.InventoryBuild,
		ThornsUiWindowKind.RadioShop => ThornsUiPriority.NpcDialog,
		ThornsUiWindowKind.ResearchStation => ThornsUiPriority.InventoryBuild,
		ThornsUiWindowKind.Campfire => ThornsUiPriority.InventoryBuild,
		ThornsUiWindowKind.LevelUpMoment => ThornsUiPriority.Toast,
		ThornsUiWindowKind.SessionRecap => ThornsUiPriority.CriticalPopup,
		ThornsUiWindowKind.VictoryIntro => ThornsUiPriority.CriticalPopup,
		ThornsUiWindowKind.MainMenuConfirm => ThornsUiPriority.CriticalPopup,
		ThornsUiWindowKind.MainMenuWorldName => ThornsUiPriority.CriticalPopup,
		ThornsUiWindowKind.GameplayModal => ThornsUiPriority.CriticalPopup,
		_ => ThornsUiPriority.Hud
	};

	public static bool SuppressesHud( ThornsUiWindowKind kind ) =>
		IsExclusiveFullscreen( kind ) || kind is ThornsUiWindowKind.BuildMenu;

	static bool IsPersistentHud( ThornsUiWindowKind kind ) =>
		kind is ThornsUiWindowKind.Hud or ThornsUiWindowKind.Hotbar;

	static bool IsExclusiveFullscreen( ThornsUiWindowKind kind ) =>
		kind is ThornsUiWindowKind.TabMenu
			or ThornsUiWindowKind.WorldContainer
			or ThornsUiWindowKind.RadioShop
			or ThornsUiWindowKind.ResearchStation
			or ThornsUiWindowKind.Campfire
			or ThornsUiWindowKind.SessionRecap
			or ThornsUiWindowKind.VictoryIntro
			or ThornsUiWindowKind.MainMenuConfirm
			or ThornsUiWindowKind.MainMenuWorldName
			or ThornsUiWindowKind.GameplayModal;
}
