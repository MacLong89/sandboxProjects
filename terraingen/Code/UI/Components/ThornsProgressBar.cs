namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ThornsProgressBar : Panel
{
	readonly Panel _fill;

	public ThornsProgressBar( Panel parent, Color fillColor )
	{
		Parent = parent;
		AddClass( "thorns-progress" );
		Style.Width = Length.Percent( 100 );
		Style.Height = Length.Pixels( 6 );
		_fill = ThornsUiFactory.AddPanel( this, "fill" );
		_fill.Style.BackgroundColor = fillColor;
		_fill.Style.Width = Length.Percent( 0 );
	}

	public void SetFraction( float fraction )
	{
		_fill.Style.Width = Length.Percent( Math.Clamp( fraction, 0f, 1f ) * 100f );
	}
}
