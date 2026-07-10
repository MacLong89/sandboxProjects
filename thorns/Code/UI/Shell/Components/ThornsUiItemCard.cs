using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiItemCard : ThornsUiCardPanel
{
	public ThornsUiItemCard( string title, string subtitle ) : base( "thorns-item-card" )
	{
		var t = AddChild( new Label( title, "thorns-item-card-title" ) );
		t.Style.PointerEvents = PointerEvents.None;
		var s = AddChild( new Label( subtitle, "thorns-item-card-sub" ) );
		s.Style.PointerEvents = PointerEvents.None;
	}
}
