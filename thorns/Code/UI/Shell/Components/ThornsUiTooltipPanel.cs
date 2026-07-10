using Sandbox.UI;

namespace Sandbox;

/// <summary>Stub host for future hover tooltips — structure only.</summary>
public sealed class ThornsUiTooltipPanel : Panel
{
	public ThornsUiTooltipPanel()
	{
		AddClass( "thorns-tooltip-root" );
		AddClass( "thorns-tooltip-root--hidden" );
	}

	public void ShowNear( string text )
	{
		DeleteChildren();
		RemoveClass( "thorns-tooltip-root--hidden" );
		var l = AddChild( new Label( text, "thorns-tooltip-text" ) );
		l.Style.PointerEvents = PointerEvents.None;
	}

	public void Hide()
	{
		AddClass( "thorns-tooltip-root--hidden" );
	}
}
