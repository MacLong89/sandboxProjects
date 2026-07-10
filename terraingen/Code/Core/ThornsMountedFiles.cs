namespace Terraingen;

/// <summary>Null-safe access to <see cref="FileSystem.Mounted"/> — required for published/standalone builds.</summary>
public static class ThornsMountedFiles
{
	public static bool IsAvailable
	{
		get
		{
			try
			{
				return FileSystem.Mounted is not null;
			}
			catch
			{
				return false;
			}
		}
	}

	public static bool Exists( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || !IsAvailable )
			return false;

		foreach ( var attempt in ThornsContentPath.Candidates( path ) )
		{
			try
			{
				if ( FileSystem.Mounted.FileExists( attempt ) )
					return true;
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns] Mounted file existence check failed for '{attempt}'." );
			}
		}

		return false;
	}

	public static bool TryReadJson<T>( string path, out T value ) where T : class
	{
		value = null;
		if ( string.IsNullOrWhiteSpace( path ) || !IsAvailable )
			return false;

		foreach ( var attempt in ThornsContentPath.Candidates( path ) )
		{
			if ( !Exists( attempt ) )
				continue;

			try
			{
				value = FileSystem.Mounted.ReadJson<T>( attempt );
				if ( value is not null )
					return true;
			}
			catch ( Exception e )
			{
				Log.Warning( e, $"[Thorns] Failed to read mounted JSON '{attempt}'." );
			}
		}

		return false;
	}

	public static bool SamplePublishAssetsPresent =>
		Exists( "ui/iconsv8/deer.png" )
		&& Exists( "ui/menu/menu_background.png" )
		&& Exists( "map/co_height.png" )
		&& ThornsModelResourceLoad.TryLoadUsable( "models/clutter/grass_common_short.vmdl", out _ );

	public static bool SampleModelPresent( string vmdlPath ) =>
		ThornsModelResourceLoad.IsUsable( ThornsModelResourceLoad.TryLoad( vmdlPath ) );

	public static void LogMountProbe( string context )
	{
		if ( !IsAvailable )
		{
			Log.Warning( $"[Thorns] FileSystem.Mounted unavailable during {context}." );
			return;
		}

		var probes = new[]
		{
			"ui/iconsv8/deer.png",
			"ui/menu/menu_background.png",
			"ui/menu/chrome/menu_backdrop.png",
			"ui/menu/menu_backdrop.png",
			"map/co_height.png",
			"ui/hud/thornshudroot.cs.scss",
		};

		var found = probes.Where( Exists ).ToArray();
		if ( found.Length < probes.Length )
		{
			Log.Warning( $"[Thorns] Mount probe ({context}): {found.Length}/{probes.Length} sample assets present." );
			ThornsMountDiagnostics.LogFullReport( context );
		}
	}
}
