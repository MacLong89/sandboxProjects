using Sandbox.UI;

namespace Sandbox;

/// <summary>Shared card chrome; unsealed so item/journal cards can extend it.</summary>
public class ThornsUiCardPanel : Panel
{
	public ThornsUiCardPanel()
		: this( null )
	{
	}

	public ThornsUiCardPanel( string cardClass )
	{
		AddClass( "thorns-card-panel" );
		if ( !string.IsNullOrEmpty( cardClass ) )
			AddClass( cardClass );
	}
}
