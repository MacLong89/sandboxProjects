namespace Terraingen;

/// <summary>Loose assets that must exist on disk and ship in published packages.</summary>
public static class ThornsRequiredPublishAssets
{
	public const string MainMenuBackdrop = "ui/menu/menu_background.png";
	public const string TabMenuBackdrop = "ui/menu/chrome/menu_backdrop.png";
	public const string TerrainHeightmap = "map/co_height.png";
	public const string HudIconProbe = "ui/iconsv8/deer.png";

	public static readonly string[] RequiredPaths =
	[
		MainMenuBackdrop,
		TabMenuBackdrop,
		TerrainHeightmap,
		HudIconProbe,
		"ui/hud/thornshudroot.cs.scss",
		"ui/menu/mainmenuhost.cs.scss"
	];

	/// <summary>Minimum flat HUD/inventory icons under <c>ui/iconsv8/</c>.</summary>
	public static readonly string[] RequiredIconStems =
	[
		"deer", "wolf", "panther", "moose",
		"health", "stamina", "thirst", "hunger",
		"inventory", "journal", "tames", "map", "guild", "settings", "home", "special",
		"survival", "combat", "building", "notification", "you", "lineage",
		"exploration", "city", "town", "events", "quest", "dominion", "discoveries", "achievements",
		"craft_armor", "craft_tools", "craft_forge", "craft_build", "craft_medical", "craft_ammo",
		"damage", "stay", "scavenger", "apple", "m4",
		"filter_all", "filter_building", "filter_weapons", "filter_tools", "filter_apparel", "filter_consumables",
		"craft_building", "craft_weapons", "craft_food"
	];

	/// <summary>
	/// BOOT FIX: Full publish checklist (icons + grass). Used for diagnostics / republish quality.
	/// Do NOT block menu boot on this — use <see cref="AreBootCriticalAssetsMounted"/> for waits.
	/// </summary>
	public static bool AreRequiredAssetsMounted()
	{
		if ( !AreBootCriticalAssetsMounted() )
			return false;

		foreach ( var stem in RequiredIconStems )
		{
			if ( !ThornsMountedFiles.Exists( $"ui/iconsv8/{stem}.png" ) )
				return false;
		}

		return ThornsModelResourceLoad.TryLoadUsable( "models/clutter/grass_common_short.vmdl", out _ );
	}

	/// <summary>
	/// BOOT FIX: Minimal set so menu/HUD can render without waiting 12s for every icon stem.
	/// Missing chrome PNGs fall back to solid colors; SCSS + heightmap are the hard kernels.
	/// </summary>
	public static bool AreBootCriticalAssetsMounted()
	{
		if ( !ThornsMountedFiles.IsAvailable )
			return false;

		// Stylesheets matter more than PNGs for "won't boot" (black screen with zero UI chrome).
		string[] bootCritical =
		[
			"ui/hud/thornshudroot.cs.scss",
			"ui/menu/mainmenuhost.cs.scss"
		];

		foreach ( var path in bootCritical )
		{
			if ( !ThornsMountedFiles.Exists( path ) )
				return false;
		}

		return true;
	}

	public static void LogMissingMounted( string context )
	{
		if ( !ThornsMountedFiles.IsAvailable )
		{
			Log.Error( $"[Thorns Publish] FileSystem.Mounted unavailable ({context}). Republish the game package." );
			return;
		}

		var missing = new List<string>();
		foreach ( var path in RequiredPaths )
		{
			if ( !ThornsMountedFiles.Exists( path ) )
				missing.Add( path );
		}

		foreach ( var stem in RequiredIconStems )
		{
			var path = $"ui/iconsv8/{stem}.png";
			if ( !ThornsMountedFiles.Exists( path ) )
				missing.Add( path );
		}

		if ( !ThornsModelResourceLoad.TryLoadUsable( "models/clutter/grass_common_short.vmdl", out _ ) )
			missing.Add( "models/clutter/grass_common_short.vmdl" );

		if ( missing.Count == 0 )
			return;

		Log.Error(
			$"[Thorns Publish] {missing.Count} required asset(s) missing on mount ({context}): " +
			$"{string.Join( ", ", missing )}. Add files under Assets/, confirm terraingen.sbproj Resources, then Publish." );
	}
}
