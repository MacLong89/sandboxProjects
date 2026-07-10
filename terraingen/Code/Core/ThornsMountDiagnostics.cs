namespace Terraingen;

/// <summary>Published-build mount diagnostics — explains missing UI/map assets in live packages.</summary>
public static class ThornsMountDiagnostics
{
	static bool _loggedFullReport;

	public static void LogFullReport( string context )
	{
		var isEditor = false;
		try
		{
			isEditor = Game.IsEditor;
		}
		catch
		{
			// ignore
		}

		var ident = "";
		try
		{
			ident = Game.Ident ?? "";
		}
		catch
		{
			// ignore
		}

		Log.Info(
			$"[Thorns Mount] Report ({context}) editor={isEditor} ident='{ident}' " +
			$"mountedAvailable={ThornsMountedFiles.IsAvailable}" );

		if ( !ThornsMountedFiles.IsAvailable )
		{
			Log.Warning( "[Thorns Mount] FileSystem.Mounted is unavailable — cannot load loose PNG/SCSS/JSON assets." );
			return;
		}

		LogCriticalProbes( context );
		LogModelProbes( context );
		LogFolderCounts();
		LogRepublishHint( once: !_loggedFullReport );
		_loggedFullReport = true;
	}

	static void LogCriticalProbes( string context )
	{
		var probes = new (string Path, string Role)[]
		{
			( "ui/menu/menu_background.png", "main menu backdrop" ),
			( "ui/menu/chrome/menu_backdrop.png", "tab menu backdrop" ),
			( "ui/iconsv8/deer.png", "HUD icon sample" ),
			( "map/co_height.png", "terrain heightmap" ),
			( "ui/core/thornsmenuhost.cs.scss", "Tab menu stylesheet" ),
			( "ui/hud/thornshudroot.cs.scss", "HUD stylesheet" ),
			( "ui/menu/mainmenuhost.cs.scss", "main menu stylesheet" ),
			( "ui/skin/classic.cs.scss", "classic UI kit stylesheet" ),
			( "ui/menu/chrome/frame_panel_9.png", "classic panel frame" ),
			( "news.json", "menu news feed" )
		};

		foreach ( var (path, role) in probes )
		{
			var exists = ThornsMountedFiles.Exists( path );
			var textureOk = false;
			if ( path.EndsWith( ".png", StringComparison.OrdinalIgnoreCase ) )
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

			var status = exists ? "FOUND" : "MISSING";
			if ( path.EndsWith( ".png", StringComparison.OrdinalIgnoreCase ) )
				status += textureOk ? " (Texture.Load ok)" : " (Texture.Load failed)";

			Log.Info( $"[Thorns Mount] [{context}] {status}: {path} ({role})" );

			if ( !exists )
			{
				foreach ( var candidate in ThornsContentPath.Candidates( path ) )
					Log.Info( $"[Thorns Mount]   candidate '{candidate}' => {SafeFileExists( candidate )}" );
			}
		}
	}

	static void LogModelProbes( string context )
	{
		var probes = new (string Path, string Role)[]
		{
			( "models/clutter/grass_common_short.vmdl", "grass clutter" ),
			( "models/tools/bow.vmdl", "bow weapon" ),
			( "models/boulders/boulder1.vmdl", "boulder" ),
			( "models/placeables/chest.vmdl", "placeable furniture" ),
			( "models/wolf/wolf.vmdl", "creature" ),
			( "models/foliage2/pine_tree.vmdl", "local pine tree" ),
			( "materials/skybox/cloud_puff_rgba.png", "cloud sprite rgba" ),
			( "materials/skybox/cloud_puff_trans.png", "cloud sprite alpha" ),
			( "materials/foliage/tree_lod_pine.vmat", "tree billboard material" ),
			( ThornsTextureResourceLoad.TreeLodPinePath, "tree billboard LOD texture" )
		};

		foreach ( var (path, role) in probes )
		{
			var mounted = ThornsMountedFiles.Exists( path );
			if ( path.EndsWith( ".png", StringComparison.OrdinalIgnoreCase ) )
			{
				var textureOk = ThornsTextureResourceLoad.Exists( path );
				Log.Info(
					$"[Thorns Mount] [{context}] texture {path} ({role}): mounted={mounted} Texture.Load={textureOk}" );
				continue;
			}

			if ( path.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase ) )
			{
				var material = Material.Load( path );
				var materialOk = ThornsTextureResourceLoad.IsMaterialUsable( material, ThornsTextureResourceLoad.TreeLodPinePath );
				Log.Info(
					$"[Thorns Mount] [{context}] material {path} ({role}): mounted={mounted} usable={materialOk}" );
				continue;
			}

			var loaded = ThornsModelResourceLoad.TryLoadUsable( path, out var model );
			Log.Info(
				$"[Thorns Mount] [{context}] model {path} ({role}): mounted={mounted} Model.Load={loaded} name={( loaded ? model.Name : "—" )}" );
		}
	}

	static void LogFolderCounts()
	{
		CountMountedPngs( "ui/iconsv8", "gameplay icons" );
		CountMountedPngs( "ui/menu", "menu UI" );
		CountMountedPngs( "ui/menu/chrome", "classic UI chrome" );
		CountMountedPngs( "map", "map data" );

		try
		{
			var anyFiles = FileSystem.Mounted.FindFile( "", "*", true ).Take( 15 ).ToArray();
			if ( anyFiles.Length == 0 )
				Log.Warning( "[Thorns Mount] FindFile('','*') returned 0 files — package mount looks empty for loose assets." );
			else
				Log.Info( $"[Thorns Mount] Sample mount files ({anyFiles.Length} shown): {string.Join( ", ", anyFiles )}" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Mount] FindFile root scan failed." );
		}
	}

	static void CountMountedPngs( string folder, string label )
	{
		try
		{
			var normalized = ThornsContentPath.Normalize( folder );
			var pngs = FileSystem.Mounted.FindFile( normalized, "*.png", true );
			var count = pngs?.Count() ?? 0;
			Log.Info( $"[Thorns Mount] {label}: {count} PNG(s) under '{normalized}/' on mount." );

			if ( count > 0 && count <= 5 )
				Log.Info( $"[Thorns Mount]   files: {string.Join( ", ", pngs )}" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns Mount] Failed to scan '{folder}' for PNGs." );
		}
	}

	static string SafeFileExists( string path )
	{
		try
		{
			return FileSystem.Mounted.FileExists( path ) ? "exists" : "missing";
		}
		catch ( Exception e )
		{
			return $"error ({e.Message})";
		}
	}

	static void LogRepublishHint( bool once )
	{
		if ( !once )
			return;

		Log.Warning(
			"[Thorns Mount] Published builds need loose assets in the package. " +
			"Set Project Settings → Resource Files (or terraingen.sbproj Resources) to include:\n" +
			"  *.png, *.scss, *.json, *.fbx, *.sound, *.tmat, *.scene, map/*, ui/**/*, scenes/**/*, news.json, sounds/*, templates/*,\n" +
			"  models/**/*, materials/**/*, terrain_materials/**/*, shaders/**/*, *.vmdl, *.vmat, *.vtex, *.shader, *.shader_c\n" +
			"Then Publish again from the s&box editor (not just Save)." );
	}
}
