namespace Terraingen.UI;

using Sandbox.UI;

/// <summary>Shared keybind rows for main-menu and in-game settings.</summary>
public static class ThornsKeybindSettingsUi
{
	public static void Build( Panel parent )
	{
		ThornsUiFactory.AddLabel( parent, "KEYBINDS — click a row, then press a key (Esc cancels)", "thorns-header" );

		foreach ( var (action, label) in ThornsKeybindService.RebindableActions )
		{
			var row = ThornsUiFactory.AddPanel( parent, "keybind-row" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.JustifyContent = Justify.SpaceBetween;
			row.Style.MarginTop = Length.Pixels( 6 );

			ThornsUiFactory.AddLabel( row, label, "thorns-muted" );

			var captureAction = action;
			var btn = ThornsUiFactory.AddClickable(
				row,
				"thorns-btn-primary keybind-btn",
				ThornsKeybindService.GetDisplayKey( action ),
				() => ThornsKeybindService.BeginListening( captureAction ) );
			btn.Style.MinWidth = Length.Pixels( 96 );
			btn.SetClass( "listening", ThornsKeybindService.IsListening && string.Equals(
				ThornsKeybindService.ListeningAction, captureAction, StringComparison.OrdinalIgnoreCase ) );
		}

		ThornsUiFactory.AddClickable( parent, "thorns-btn-secondary", "Reset keybinds to defaults", () =>
		{
			ThornsLocalSettings.Current.KeybindOverrides = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			ThornsLocalSettings.Save();
		} );
	}

	public static void RefreshButtons( Panel parent )
	{
		if ( parent is null || !parent.IsValid )
			return;

		foreach ( var child in parent.Children )
		{
			if ( child is not Panel row || !row.HasClass( "keybind-row" ) )
				continue;

			foreach ( var btn in row.Children )
			{
				if ( btn is not ThornsClickPanel clickable || !clickable.HasClass( "keybind-btn" ) )
					continue;

				// Rebuild is simpler than tracking labels — settings screens are low traffic.
			}
		}
	}
}
