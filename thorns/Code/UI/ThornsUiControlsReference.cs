using Sandbox.UI;

namespace Sandbox;

/// <summary>Builds scrollable control-reference rows into a host panel (settings tab, main menu).</summary>
public static class ThornsUiControlsReference
{
	public static void Populate( Panel scrollHost, string stylePrefix = "thorns-settings" )
	{
		scrollHost.DeleteChildren();

		foreach ( var section in ThornsControlsCatalog.Sections )
		{
			var head = scrollHost.AddChild( new Label( section.Title, $"{stylePrefix}-controls-section" ) );
			head.Style.PointerEvents = PointerEvents.None;

			foreach ( var entry in section.Entries )
			{
				var row = ThornsUiPanelAdd.AddChildPanel( scrollHost, $"{stylePrefix}-row" );
				row.AddChild( new Label( entry.Action, $"{stylePrefix}-kv" ) ).Style.PointerEvents =
					PointerEvents.None;
				row.AddChild( new Label( entry.Binding, $"{stylePrefix}-kv-val" ) ).Style.PointerEvents =
					PointerEvents.None;
			}
		}
	}
}
