namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ThornsStatRow : Panel
{
	public ThornsStatRow( Panel parent, string label, string value )
	{
		Parent = parent;
		AddClass( "thorns-stat-row" );
		ThornsUiFactory.AddLabel( this, label, "thorns-muted" );
		ThornsUiFactory.AddLabel( this, value, "value" );
	}
}
