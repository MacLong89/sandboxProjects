namespace Terraingen.UI.Components;

using Sandbox.UI;

using Terraingen.UI.Core;

public sealed class ThornsScrollList : Panel
{
	public ThornsScrollList( Panel parent )
	{
		Parent = parent;
		ThornsUiPanelDefaults.DisableDragScroll( this );
		Style.Overflow = OverflowMode.Scroll;
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
	}
}
