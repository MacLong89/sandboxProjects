namespace Terraingen.UI.Hud;

/// <summary>Shared gameplay prompt payload (interaction, loot, mount, etc.).</summary>
public readonly struct ThornsHudPromptState
{
	public string Message { get; init; }
	public string AlertGlyph { get; init; }
	public float HoldFraction { get; init; }

	public bool IsVisible => !string.IsNullOrWhiteSpace( Message );

	public static ThornsHudPromptState HoldAction( string verbPhrase, string keyHint = "E", float holdFraction = 0f ) =>
		new()
		{
			AlertGlyph = "!",
			Message = $"Hold {keyHint} to {verbPhrase}",
			HoldFraction = holdFraction
		};

	public static ThornsHudPromptState PressAction( string verbPhrase, string keyHint = "E" ) =>
		new()
		{
			AlertGlyph = "!",
			Message = $"Press {keyHint} to {verbPhrase}",
			HoldFraction = 0f
		};

	public static ThornsHudPromptState AttackAction( string verbPhrase, string keyHint = "LMB" ) =>
		PressAction( verbPhrase, keyHint );
}
