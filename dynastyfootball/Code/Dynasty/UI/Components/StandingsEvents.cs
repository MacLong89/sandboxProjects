namespace Dynasty.UI.Components;

/// <summary>
/// Standings rows raise this event because s&box child panels do not reliably receive Action callbacks from the HUD.
/// </summary>
public static class StandingsEvents
{
	public static event Action<string> TeamAbbreviationClicked;

	public static void RaiseTeamClicked( string abbreviation )
		=> TeamAbbreviationClicked?.Invoke( abbreviation );
}
