namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ThornsCategoryList : Panel
{
	public ThornsCategoryList( Panel parent )
	{
		Parent = parent;
		Style.FlexDirection = FlexDirection.Column;
	}

	public void SetCategories( IEnumerable<string> labels, Action<string> onSelect, string active )
	{
		DeleteChildren( true );
		foreach ( var label in labels )
		{
			var captured = label;
			var btn = ThornsUiFactory.AddClickable( this, "cat", captured, () => onSelect( captured ) );
			btn.SetClass( "active", label == active );
		}
	}
}
