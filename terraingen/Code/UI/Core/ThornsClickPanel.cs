namespace Terraingen.UI;

using Sandbox.UI;

/// <summary>Click target without lambda-based event listeners (hotload-safe).</summary>
public class ThornsClickPanel : Panel
{
	Action _onClick;

	public ThornsClickPanel()
	{
		AddClass( "thorns-click" );
		Style.PointerEvents = PointerEvents.All;
		Core.ThornsUiPanelDefaults.DisableDragScroll( this );
	}

	public void SetClick( Action onClick ) => _onClick = onClick;

	protected override void OnMouseDown( MousePanelEvent e )
	{
		if ( !IsLeftMouse( e ) )
		{
			base.OnMouseDown( e );
			return;
		}

		e.StopPropagation();
		ThornsUiSfx.PlayButtonClick();

		try
		{
			_onClick?.Invoke();
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, "[Thorns UI] Click handler failed." );
		}
	}

	static bool IsLeftMouse( MousePanelEvent e ) =>
		e.MouseButton == MouseButtons.Left
		|| string.Equals( e.Button, "mouseleft", StringComparison.OrdinalIgnoreCase );
}
