namespace Dynasty.UI;

internal sealed class TeamPickOption
{
	public string Abbr { get; }
	public string Label { get; }

	public TeamPickOption( string abbr, string label )
	{
		Abbr = abbr;
		Label = label;
	}
}
