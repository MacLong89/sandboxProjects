namespace Terraingen.UI.Menu;

using System.Collections.Generic;
using Sandbox.UI;
using Terraingen;

/// <summary>Static menu background — mounted PNG paths under <c>Assets/ui/menu/</c>.</summary>
public static class ThornsMainMenuBackdrop
{
	/// <summary>Mounted asset path (disk: Assets/ui/menu/menu_background.png).</summary>
	public const string DefaultPath = "ui/menu/menu_background.png";

	public const string TabMenuPrimaryPath = "ui/menu/chrome/menu_backdrop.png";
	public const string TabMenuAltPath = "ui/menu/menu_backdrop.png";

	static readonly string[] MainMenuPathCandidates =
	{
		DefaultPath,
		TabMenuPrimaryPath
	};

	static readonly string[] TabMenuPathCandidates =
	{
		TabMenuPrimaryPath,
		TabMenuAltPath,
		DefaultPath
	};

	static bool _loggedTabMenuMountProbe;
	static string _cachedTabMenuBackdropPath;
	static readonly Dictionary<string, Texture> BackdropTextureCache = new( StringComparer.OrdinalIgnoreCase );

	public static void WarmTabMenuBackdrop()
	{
		if ( !EnableImageBackdrop || !string.IsNullOrWhiteSpace( _cachedTabMenuBackdropPath ) )
			return;

		foreach ( var path in TabMenuPathCandidates )
		{
			if ( !ThornsMountedFiles.Exists( path ) )
				continue;

			foreach ( var attempt in ThornsContentPath.Candidates( path ) )
			{
				try
				{
					var tex = Texture.Load( attempt );
					if ( tex is not null && tex.IsValid )
					{
						_cachedTabMenuBackdropPath = ThornsContentPath.Normalize( path );
						return;
					}
				}
				catch
				{
					// Warm is best-effort — ApplyTabMenuBackdrop still retries at runtime.
				}
			}
		}
	}

	public static bool EnableImageBackdrop { get; set; } = true;

	public static void ApplyToPanel( Panel panel, string preferredPath = null )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "mainmenu-backdrop" );

		if ( !EnableImageBackdrop )
		{
			UseFallback( panel, "image backdrop disabled" );
			return;
		}

		var path = ResolveExistingPath( preferredPath, MainMenuPathCandidates );
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			UseFallback( panel, "no backdrop file on mount" );
			return;
		}

		if ( TryApplyPath( panel, path ) )
		{
			panel.SetClass( "mainmenu-backdrop-fallback", false );
			panel.Style.BackgroundColor = Color.Transparent;
			Log.Info( $"[Thorns Menu] Backdrop image: {path}" );
			return;
		}

		UseFallback( panel, $"could not apply '{path}'" );
	}

	/// <summary>Full-screen tab-menu parchment — same load path as the main menu backdrop.</summary>
	public static bool ApplyTabMenuBackdrop( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return false;

		panel.AddClass( "mainmenu-backdrop thorns-menu-world-backdrop thorns-menu-tab-backdrop" );

		if ( !EnableImageBackdrop )
		{
			UseFallback( panel, "tab menu backdrop disabled" );
			return false;
		}

		LogTabMenuMountProbeOnce();

		if ( !string.IsNullOrWhiteSpace( _cachedTabMenuBackdropPath )
		     && TryApplyPath( panel, _cachedTabMenuBackdropPath ) )
		{
			panel.SetClass( "mainmenu-backdrop-fallback", false );
			panel.Style.BackgroundColor = Color.Transparent;
			return true;
		}

		foreach ( var path in TabMenuPathCandidates )
		{
			if ( !TryApplyPath( panel, path ) )
				continue;

			panel.SetClass( "mainmenu-backdrop-fallback", false );
			panel.Style.BackgroundColor = Color.Transparent;
			_cachedTabMenuBackdropPath = ThornsContentPath.Normalize( path );
			Log.Info( $"[Thorns UI] Tab menu backdrop: {path}" );
			return true;
		}

		UseFallback( panel, "no tab menu backdrop mounted" );
		return false;
	}

	static void LogTabMenuMountProbeOnce()
	{
		if ( _loggedTabMenuMountProbe )
			return;

		_loggedTabMenuMountProbe = true;

		foreach ( var path in TabMenuPathCandidates )
		{
			var exists = ThornsMountedFiles.Exists( path );
			var textureOk = false;
			if ( exists )
			{
				try
				{
					var tex = Texture.Load( ThornsContentPath.Normalize( path ) );
					textureOk = tex is not null && tex.IsValid;
				}
				catch
				{
					textureOk = false;
				}
			}

			Log.Info( $"[Thorns UI] Tab menu backdrop probe '{path}': exists={exists} texture={textureOk}" );
		}
	}

	static bool TryApplyPath( Panel panel, string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		foreach ( var attempt in ThornsContentPath.Candidates( path ) )
		{
			if ( ThornsMountedFiles.Exists( attempt ) )
			{
				try
				{
					panel.Style.SetBackgroundImage( attempt );
					return true;
				}
				catch ( Exception e )
				{
					Log.Warning( e, $"[Thorns Menu] SetBackgroundImage failed for '{attempt}'." );
				}
			}

			try
			{
				if ( BackdropTextureCache.TryGetValue( attempt, out var cached ) && cached is not null && cached.IsValid )
				{
					panel.Style.BackgroundImage = cached;
					return true;
				}

				var tex = Texture.Load( attempt );
				if ( tex is not null && tex.IsValid )
				{
					BackdropTextureCache[attempt] = tex;
					panel.Style.BackgroundImage = tex;
					return true;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns Menu] Texture.Load failed for '{attempt}'." );
			}
		}

		return false;
	}

	static void UseFallback( Panel panel, string reason )
	{
		panel.Style.BackgroundImage = null;
		panel.SetClass( "mainmenu-backdrop-fallback", true );
		panel.Style.BackgroundColor = new Color( 217f / 255f, 197f / 255f, 163f / 255f, 1f );
		// BOOT FIX: solid fallback is expected when art is missing — warn once, don't spam Error every panel.
		Log.Warning(
			$"[Thorns Menu] Backdrop missing ({reason}); using solid fallback. " +
			"Run Scripts/EnsureRequiredAssets.ps1 then republish if this is a published build." );
	}

	static string ResolveExistingPath( string preferredPath, string[] defaults )
	{
		if ( !string.IsNullOrWhiteSpace( preferredPath ) )
		{
			foreach ( var attempt in ThornsContentPath.Candidates( preferredPath ) )
			{
				if ( ThornsMountedFiles.Exists( attempt ) )
					return ThornsContentPath.Normalize( attempt );
			}
		}

		foreach ( var candidate in defaults )
		{
			if ( ThornsMountedFiles.Exists( candidate ) )
				return ThornsContentPath.Normalize( candidate );
		}

		return null;
	}
}
