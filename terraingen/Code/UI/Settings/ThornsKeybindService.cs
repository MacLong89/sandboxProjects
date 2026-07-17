namespace Terraingen.UI;

using Sandbox;

/// <summary>Client-local keybind overrides persisted in <see cref="ThornsLocalSettings"/>.</summary>
public static class ThornsKeybindService
{
	public static readonly (string Action, string Label)[] RebindableActions =
	{
		( "Tab", "Menu (Tab)" ),
		( "InventoryMenu", "Inventory (I)" ),
		( "JournalMenu", "Journal (J)" ),
		( "MapMenu", "Map (M)" ),
		( "SkillsMenu", "Skills (K)" ),
		( "GuildMenu", "Guild (G)" ),
		( "Build", "Build (B)" ),
		( "Use", "Interact (E)" ),
		( "Run", "Sprint (Shift)" ),
		( "Jump", "Jump (Space)" ),
		( "Duck", "Crouch (Ctrl)" ),
		( "ToggleHud", "Toggle HUD (0)" )
	};

	static string _listeningAction;

	public static bool IsListening => !string.IsNullOrWhiteSpace( _listeningAction );

	public static string ListeningAction => _listeningAction ?? "";

	public static void BeginListening( string action ) => _listeningAction = action ?? "";

	public static void CancelListening() => _listeningAction = null;

	static readonly string[] CaptureKeyboardCodes =
	{
		"a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
		"n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
		"0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
		"space", "shift", "ctrl", "alt",
		"f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12",
		"mouse4", "mouse5"
	};

	public static void TickCapture()
	{
		if ( string.IsNullOrWhiteSpace( _listeningAction ) )
			return;

		if ( Input.EscapePressed )
		{
			CancelListening();
			return;
		}

		foreach ( var code in CaptureKeyboardCodes )
		{
			if ( !Input.Keyboard.Pressed( code ) )
				continue;

			SetOverride( _listeningAction, code );
			CancelListening();
			return;
		}
	}

	public static string GetDisplayKey( string action )
	{
		if ( TryGetOverride( action, out var key ) )
			return key.ToUpperInvariant();

		return action switch
		{
			"Tab" => "TAB",
			"InventoryMenu" => "I",
			"JournalMenu" => "J",
			"MapMenu" => "M",
			"SkillsMenu" => "K",
			"GuildMenu" => "G",
			"Build" => "B",
			"Use" => "E",
			"Run" => "SHIFT",
			"Jump" => "SPACE",
			"Duck" => "CTRL",
			"ToggleHud" => "0",
			_ => action.ToUpperInvariant()
		};
	}

	public static bool Pressed( string action )
	{
		if ( TryGetOverride( action, out var key ) && !string.IsNullOrWhiteSpace( key ) )
			return Input.Keyboard.Pressed( key );

		return Input.Pressed( action );
	}

	public static bool Down( string action )
	{
		if ( TryGetOverride( action, out var key ) && !string.IsNullOrWhiteSpace( key ) )
			return Input.Keyboard.Down( key );

		return Input.Down( action );
	}

	public static void ClearOverride( string action )
	{
		ThornsLocalSettings.Current.KeybindOverrides ??= new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		ThornsLocalSettings.Current.KeybindOverrides.Remove( action );
		ThornsLocalSettings.Save();
	}

	static void SetOverride( string action, string keyboardCode )
	{
		ThornsLocalSettings.Current.KeybindOverrides ??= new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		ThornsLocalSettings.Current.KeybindOverrides[action] = keyboardCode;
		ThornsLocalSettings.Save();
	}

	static bool TryGetOverride( string action, out string key )
	{
		key = null;
		var map = ThornsLocalSettings.Current.KeybindOverrides;
		if ( map is null || string.IsNullOrWhiteSpace( action ) )
			return false;

		return map.TryGetValue( action, out key ) && !string.IsNullOrWhiteSpace( key );
	}
}
