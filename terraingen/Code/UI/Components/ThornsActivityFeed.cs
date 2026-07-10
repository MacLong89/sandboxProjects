namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;

using Terraingen.UI.Core;

public sealed class ThornsActivityFeed : Panel
{
	public ThornsActivityFeed( Panel parent )
	{
		Parent = parent;
		ThornsUiPanelDefaults.DisableDragScroll( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.Overflow = OverflowMode.Scroll;
	}

	public void SetEntries( IEnumerable<ThornsGuildActivityDto> entries )
	{
		DeleteChildren( true );
		foreach ( var entry in entries )
			ThornsUiFactory.AddLabel( this, $"{entry.Message} — {entry.TimestampUtc}", "thorns-muted" );
	}
}
