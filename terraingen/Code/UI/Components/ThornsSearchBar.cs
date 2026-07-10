namespace Terraingen.UI.Components;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ThornsSearchBar : Panel
{
	public TextEntry Entry { get; }

	public ThornsSearchBar( Panel parent, Action<string> onChanged )
	{
		Parent = parent;
		Entry = AddChild( new TextEntry() );
		Entry.AddClass( "search" );
		Entry.Placeholder = "Search…";
		Entry.AddEventListener( "onvaluechanged", () => onChanged?.Invoke( Entry.Text ) );
	}
}
