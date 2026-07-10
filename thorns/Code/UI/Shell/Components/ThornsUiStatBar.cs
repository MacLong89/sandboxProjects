using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiStatBar : Panel
{
	readonly Label _cap;
	readonly ThornsUiProgressBar _bar;

	public ThornsUiStatBar( string caption, string accentClass )
	{
		AddClass( "thorns-stat-bar" );
		AddClass( accentClass );

		var row = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-stat-bar-row" );
		_cap = row.AddChild( new Label( caption, "thorns-stat-bar-cap" ) );
		_cap.Style.PointerEvents = PointerEvents.None;
		_bar = row.AddChild( new ThornsUiProgressBar( "thorns-stat-bar-track" ) );
	}

	public void SetCaption( string c ) => _cap.Text = c;
	public void SetFraction01( float t ) => _bar.SetFraction01( t );
}
