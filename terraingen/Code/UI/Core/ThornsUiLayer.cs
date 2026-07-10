namespace Terraingen.UI.Core;

/// <summary>Central UI layering priorities — higher values render above lower values.</summary>
public enum ThornsUiPriority
{
	PassiveOverlay = 10,
	Hotbar = 20,
	Hud = 30,
	Tooltip = 40,
	Toast = 50,
	Journal = 60,
	NpcDialog = 70,
	InventoryBuild = 80,
	FullscreenMenu = 90,
	CriticalPopup = 100
}

/// <summary>Maps logical UI priorities to z-index values. All panels must use these — no ad-hoc z-index.</summary>
public static class ThornsUiLayer
{
	public static int ZIndex( ThornsUiPriority priority ) => priority switch
	{
		ThornsUiPriority.PassiveOverlay => 500,
		ThornsUiPriority.Hotbar => 1000,
		ThornsUiPriority.Hud => 1000,
		ThornsUiPriority.Tooltip => 4000,
		ThornsUiPriority.Toast => 6000,
		ThornsUiPriority.Journal => 7000,
		ThornsUiPriority.NpcDialog => 8000,
		ThornsUiPriority.InventoryBuild => 11000,
		ThornsUiPriority.FullscreenMenu => 12000,
		ThornsUiPriority.CriticalPopup => 13000,
		_ => 1000
	};

	public static void Apply( Sandbox.UI.Panel panel, ThornsUiPriority priority )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.Style.ZIndex = ZIndex( priority );
	}

	/// <summary>Force full opacity on active panels — no accidental transparency on modals.</summary>
	public static void ApplyModalSurface( Sandbox.UI.Panel panel, ThornsUiPriority priority )
	{
		if ( panel is null || !panel.IsValid )
			return;

		Apply( panel, priority );
		panel.Style.Opacity = 1f;
		panel.Style.PointerEvents = Sandbox.UI.PointerEvents.All;
	}

	public static void ApplyPassive( Sandbox.UI.Panel panel, ThornsUiPriority priority = ThornsUiPriority.Hud )
	{
		if ( panel is null || !panel.IsValid )
			return;

		Apply( panel, priority );
		panel.Style.PointerEvents = Sandbox.UI.PointerEvents.None;
	}

	/// <summary>Order elements within a single parent panel (1–99). Never use for cross-layer stacking.</summary>
	public static void ApplyLocalOrder( Sandbox.UI.Panel panel, int localOrder )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.Style.ZIndex = Math.Clamp( localOrder, 1, 99 );
	}
}
