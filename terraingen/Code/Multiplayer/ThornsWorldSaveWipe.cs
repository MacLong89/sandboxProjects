namespace Terraingen.Multiplayer;

/// <summary>Deletes or resets on-disk world JSON under <see cref="FileSystem.Data"/>.</summary>
public static class ThornsWorldSaveWipe
{
	/// <summary>Permanently deletes the world save file (no-op if already missing).</summary>
	public static bool TryDeleteWorldFile( string relativePath, out string errorMessage )
	{
		if ( !TryValidateWorldRelativePath( relativePath, out var rel, out errorMessage ) )
			return false;

		try
		{
			if ( !FileSystem.Data.FileExists( rel ) )
			{
				Log.Info( $"[Thorns Terrain] World save already absent (delete no-op): '{rel}'." );
				return true;
			}

			FileSystem.Data.DeleteFile( rel );
			Log.Info( $"[Thorns Terrain] Deleted world save file: '{rel}'." );
			return true;
		}
		catch ( Exception e )
		{
			errorMessage = e.Message;
			Log.Warning( e, $"[Thorns Terrain] Failed to delete world save '{rel}'." );
			return false;
		}
	}

	static bool TryValidateWorldRelativePath( string relativePath, out string rel, out string errorMessage )
	{
		errorMessage = null;
		rel = relativePath?.Trim().Replace( '\\', '/' ) ?? "";
		if ( rel.Length < 1 )
		{
			errorMessage = "No save path.";
			return false;
		}

		if ( !rel.StartsWith( $"{ThornsHostSavePaths.SavesFolderPrefix}/", StringComparison.OrdinalIgnoreCase )
		     || !rel.EndsWith( ".json", StringComparison.OrdinalIgnoreCase )
		     || rel.IndexOf( "..", StringComparison.Ordinal ) >= 0 )
		{
			errorMessage = "Not a valid world save path.";
			return false;
		}

		var file = System.IO.Path.GetFileName( rel );
		if ( string.IsNullOrEmpty( file ) || !file.StartsWith( "world_", StringComparison.OrdinalIgnoreCase ) )
		{
			errorMessage = "Only world_*.json saves can be deleted.";
			return false;
		}

		return true;
	}
}
