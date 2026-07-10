using Sandbox.UI;

namespace Sandbox;

public enum AimboxUiThemeId
{
	Classic,
	Arcade,
	Spectra
}

/// <summary>
/// Switches between Classic (MW2), Arcade (AAA shooter), and Spectra (holographic sci-fi) UI skins.
/// </summary>
public static class AimboxUiTheme
{
	public static AimboxUiThemeId Current { get; private set; } = AimboxUiThemeId.Classic;

	public static bool IsArcade => Current == AimboxUiThemeId.Arcade;

	public static bool IsSpectra => Current == AimboxUiThemeId.Spectra;

	public static bool IsClassic => Current == AimboxUiThemeId.Classic;

	public static string CurrentLabel => Current switch
	{
		AimboxUiThemeId.Arcade => "Arcade",
		AimboxUiThemeId.Spectra => "Spectra",
		_ => "Classic"
	};

	public static string NextLabel
	{
		get
		{
			var next = (AimboxUiThemeId)( (int)( Current + 1 ) % 3 );
			return next switch
			{
				AimboxUiThemeId.Arcade => "Arcade",
				AimboxUiThemeId.Spectra => "Spectra",
				_ => "Classic"
			};
		}
	}

	public static string CssClass => Current switch
	{
		AimboxUiThemeId.Arcade => "theme-arcade",
		AimboxUiThemeId.Spectra => "theme-spectra",
		_ => "theme-classic"
	};

	public static void Set( AimboxUiThemeId theme )
	{
		Current = theme;
		Version++;
	}

	public static void Cycle()
	{
		Current = (AimboxUiThemeId)( (int)( Current + 1 ) % 3 );
		Version++;
	}

	public static void Toggle() => Cycle();

	/// <summary>Bumps when the active theme changes so panel components can invalidate.</summary>
	public static int Version { get; private set; }

	public static void SyncPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid() )
			return;

		panel.SetClass( "theme-classic", IsClassic );
		panel.SetClass( "theme-arcade", IsArcade );
		panel.SetClass( "theme-spectra", IsSpectra );
	}
}
