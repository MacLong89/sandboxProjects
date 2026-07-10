namespace Terraingen.Editor;

using System;
using System.IO;

/// <summary>Warns in the editor when publish packaging will omit loose UI/map assets.</summary>
public static class ThornsProjectResourcesGuard
{
	const string DefaultResources =
		"*.png\n*.scss\n*.json\nmap/*\nui/**/*\nnews.json\nsounds/*\ntemplates/*\nmodels/**/*\nmaterials/**/*\nterrain_materials/**/*\n*.vmdl\n*.vmat\n*.vtex";

	static bool _loggedForSession;

	[Event( "tools.package.loaded" )]
	static void OnPackageLoaded()
	{
		var project = Project.Current;
		if ( project?.Config is null )
			return;

		if ( !string.Equals( project.Config.Ident, "terraingen", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( _loggedForSession )
			return;

		_loggedForSession = true;

		var resources = project.Config.Resources;
		if ( string.IsNullOrWhiteSpace( resources ) )
		{
			Log.Warning(
				"[Thorns Publish] terraingen.sbproj Resources is empty. " +
				"Published/live builds will NOT include menu PNGs, HUD icons, heightmaps, or SCSS. " +
				"Open Project Settings → Resource Files, add wildcards (*.png, map/*, ui/**/*), Save, then Publish." );
			return;
		}

		var patterns = resources.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		Log.Info( $"[Thorns Publish] Resource Files: {patterns.Length} pattern(s) configured. Republish after changing these." );

		LogDiskChecks( project.Config.AssetsDirectory?.FullName );
	}

	static void LogDiskChecks( string assetsDirectory )
	{
		if ( string.IsNullOrWhiteSpace( assetsDirectory ) )
			return;

		CheckDiskFile( assetsDirectory, "ui/menu/menu_background.png", "main menu backdrop" );
		CheckDiskFile( assetsDirectory, "map/co_height.png", "terrain heightmap" );
		CheckDiskFolderPngCount( assetsDirectory, "ui/iconsv8", "HUD icons" );
	}

	static void CheckDiskFile( string assetsDir, string relativePath, string label )
	{
		var fullPath = Path.Combine( assetsDir, relativePath.Replace( '/', Path.DirectorySeparatorChar ) );
		if ( File.Exists( fullPath ) )
			Log.Info( $"[Thorns Publish] On disk: {label} OK ({relativePath})." );
		else
			Log.Warning( $"[Thorns Publish] On disk: {label} MISSING at Assets/{relativePath}." );
	}

	static void CheckDiskFolderPngCount( string assetsDir, string folder, string label )
	{
		var fullPath = Path.Combine( assetsDir, folder.Replace( '/', Path.DirectorySeparatorChar ) );
		if ( !Directory.Exists( fullPath ) )
		{
			Log.Warning( $"[Thorns Publish] On disk: {label} folder missing (Assets/{folder})." );
			return;
		}

		var count = Directory.GetFiles( fullPath, "*.png", SearchOption.TopDirectoryOnly ).Length;
		Log.Info( $"[Thorns Publish] On disk: {count} PNG(s) in Assets/{folder}/ ({label})." );
	}
}
