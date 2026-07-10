namespace Terraingen.UI;

using Sandbox.UI;

using Terraingen.UI.Core;

/// <summary>Map viewport that forwards mouse wheel to <see cref="ThornsMapView"/> zoom.</summary>
public sealed class ThornsMapViewportPanel : Panel
{
	public ThornsMapView MapView { get; set; }

	public ThornsMapViewportPanel( Panel parent, string cssClass )
	{
		Parent = parent;
		ThornsUiPanelDefaults.DisableDragScroll( this );
		AddClass( cssClass );
	}

	public override void OnMouseWheel( Vector2 value )
	{
		if ( MapView is null || !MapView.ScrollZoomEnabled )
		{
			base.OnMouseWheel( value );
			return;
		}

		MapView.ApplyScrollZoom( -value.y, MousePosition );
	}
}
