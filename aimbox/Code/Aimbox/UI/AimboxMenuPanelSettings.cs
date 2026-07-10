using Sandbox.UI;

namespace Sandbox;

public static class AimboxMenuPanelSettings
{
	public static void DisableDragScroll( Panel panel )
	{
		if ( panel is null || !panel.IsValid() )
			return;

		panel.CanDragScroll = false;

		foreach ( var child in panel.Children )
			DisableDragScroll( child );
	}
}
