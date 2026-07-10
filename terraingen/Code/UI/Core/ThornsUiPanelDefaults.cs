namespace Terraingen.UI.Core;

using Sandbox.UI;

/// <summary>Shared panel behavior for Thorns UI surfaces.</summary>
public static class ThornsUiPanelDefaults
{
	/// <summary>Opt out of s&amp;box drag-scroll (M1 drag panning scrollable panels).</summary>
	public static void DisableDragScroll( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.CanDragScroll = false;
	}
}
