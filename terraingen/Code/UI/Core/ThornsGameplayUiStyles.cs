namespace Terraingen.UI.Core;

using System.Collections.Generic;
using System.Linq;
using Sandbox.UI;
using Terraingen.UI;

/// <summary>Loads mounted gameplay UI stylesheets (s&amp;box expects per-component .cs.scss names).</summary>
public static class ThornsGameplayUiStyles
{
	public const string MenuStylesheet = "ui/core/thornsmenuhost.cs.scss";
	public const string HudStylesheet = "ui/hud/thornshudroot.cs.scss";
	public const string MainMenuStylesheet = "ui/menu/mainmenuhost.cs.scss";
	public const string SurviveSkinStylesheet = "ui/skin/survive.cs.scss";
	public const string FieldSkinStylesheet = "ui/skin/field.cs.scss";
	public const string ClassicSkinStylesheet = "ui/skin/classic.cs.scss";

	public static void LoadGameplayRoot( Panel panel )
	{
		TryLoad( panel, MenuStylesheet );
		TryLoad( panel, HudStylesheet );
		TryLoad( panel, ClassicSkinStylesheet );
	}

	public static void LoadMainMenuRoot( Panel panel )
	{
		TryLoad( panel, MenuStylesheet );
		TryLoad( panel, MainMenuStylesheet );
		TryLoad( panel, ClassicSkinStylesheet );
	}

	static readonly HashSet<(Panel Panel, string Path)> StylesheetsLoaded = new();

	public static void ForgetPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		StylesheetsLoaded.RemoveWhere( entry => entry.Panel == panel );
	}

	public static bool IsGameplayRootReady( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return false;

		return StylesheetsLoaded.Contains( (panel, ThornsContentPath.Normalize( MenuStylesheet )) )
		       && StylesheetsLoaded.Contains( (panel, ThornsContentPath.Normalize( HudStylesheet )) );
	}

	public static void TryLoad( Panel panel, string path )
	{
		if ( panel is null || !panel.IsValid || string.IsNullOrWhiteSpace( path ) )
			return;

		var normalized = ThornsContentPath.Normalize( path );
		if ( !StylesheetsLoaded.Add( (panel, normalized) ) )
			return;

		if ( !ThornsMountedFiles.Exists( normalized ) )
		{
			var candidates = ThornsContentPath.Candidates( normalized ).Select( c => $"{c}={ThornsMountedFiles.Exists( c )}" );
			ThornsGameplayUiDiagnostics.Warn(
				$"Stylesheet not on mount '{normalized}' — candidates: {string.Join( ", ", candidates )}" );
		}

		try
		{
			panel.StyleSheet.Load( path );
			ThornsGameplayUiDiagnostics.Event( $"Stylesheet loaded '{path}'." );
		}
		catch ( Exception e )
		{
			ThornsGameplayUiDiagnostics.Warn( $"Failed to load stylesheet '{path}' (mounted={ThornsMountedFiles.Exists( normalized )})." );
			Log.Warning( e, $"[Thorns UI] Failed to load stylesheet '{path}'." );
		}
	}

	public static void ApplyReadableLabel( Label label )
	{
		if ( label is null || !label.IsValid )
			return;

		// NOTE: deliberately do NOT set FontColor here. Setting it inline made every
		// factory label win the cascade, so stylesheet skins (parchment = dark ink,
		// HUD = light) could never recolor text. Color is now owned by the skins.
		label.Style.FontSize = Length.Pixels( 14 );
	}
}
