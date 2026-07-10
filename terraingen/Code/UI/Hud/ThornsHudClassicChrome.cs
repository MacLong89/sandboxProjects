namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;

/// <summary>Classic fantasy HUD framing — lighter card frames (no menu vine overlays).</summary>
public static class ThornsHudClassicChrome
{
	public static bool IsActive => ThornsUiSkin.Active == ThornsUiSkinKind.Classic;

	public static void ApplyClusterFrame( Panel panel )
	{
		if ( !IsActive || panel is null || !panel.IsValid )
			return;

		ThornsMenuChrome.ApplyHudCardFrame( panel );
		panel.AddClass( "hud-classic-cluster" );
	}

	public static void ApplyKeyHintFrame( Panel panel )
	{
		if ( !IsActive || panel is null || !panel.IsValid )
			return;

		ThornsMenuChrome.ApplyMenuSlot( panel );
		panel.AddClass( "hud-classic-key-hint" );
	}
}
