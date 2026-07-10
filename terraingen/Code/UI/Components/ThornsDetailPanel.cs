namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ThornsDetailPanel : Panel
{
	public ThornsDetailPanel( Panel parent )
	{
		Parent = parent;
		AddClass( "thorns-detail-panel thorns-glass" );
		ThornsTheme.ApplyGlassPanel( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.Padding = Length.Pixels( 12 );
	}

	public void SetContent( string header, string body )
	{
		DeleteChildren( true );
		ThornsTheme.CreateHeader( this, header );
		ThornsTheme.CreateMuted( this, body );
	}
}
