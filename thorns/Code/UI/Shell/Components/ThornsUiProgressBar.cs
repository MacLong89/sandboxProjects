using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiProgressBar : Panel
{
	readonly Panel _fill;

	public ThornsUiProgressBar( string trackClass = "thorns-progress" )
	{
		AddClass( trackClass );
		_fill = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-progress-fill" );
		_fill.Style.PointerEvents = PointerEvents.None;
		_fill.Style.Position = PositionMode.Absolute;
		_fill.Style.Left = 0;
		_fill.Style.Top = 0;
		_fill.Style.Bottom = 0;
	}

	public void SetFraction01( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		_fill.Style.Width = Length.Fraction( t );
	}
}
