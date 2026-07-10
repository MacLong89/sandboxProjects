#nullable disable

using Sandbox.UI;

namespace Sandbox;

/// <summary>Full-screen inventory overlay root — clears drag when releasing over dimmed backdrop.</summary>
public sealed class InventoryOverlayRootPanel : Panel
{
	public Action OnBackdropMouseUp;

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		if ( e.MouseButton == MouseButtons.Left )
			OnBackdropMouseUp?.Invoke();
	}
}
