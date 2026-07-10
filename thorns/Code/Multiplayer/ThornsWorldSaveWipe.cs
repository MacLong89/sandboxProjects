namespace Sandbox;

/// <summary>Resets a on-disk world file to an empty v2 snapshot (no structures, wildlife, or player account rows).</summary>
public static class ThornsWorldSaveWipe
{
	/// <summary>Only <c>Thorns/saves/world_*.json</c> paths are allowed (see <see cref="ThornsHostSavePaths"/>).</summary>
	public static bool TryWipeWorldFile( string relativePath, out string errorMessage )
	{
		if ( !TryValidateListenHostWorldRelativePath( relativePath, out var rel, out errorMessage ) )
			return false;

		try
		{
			var fresh = new ThornsPersistentWorldDto
			{
				Version = 2,
				SavedUtcIso = DateTime.UtcNow.ToString( "o" ),
				Structures = new List<ThornsPersistentStructureDto>(),
				Wildlife = new List<ThornsPersistentWildlifeDto>(),
				PlayersByAccountKey = new Dictionary<string, ThornsPersistentPlayerDto>()
			};

			FileSystem.Data.WriteJson( rel, fresh );
			Log.Info( $"[Thorns] Wiped world save (structures, wildlife, players cleared): '{rel}'" );
			return true;
		}
		catch ( Exception e )
		{
			errorMessage = e.Message;
			Log.Warning( e, $"[Thorns] Failed to wipe world save '{rel}'." );
			return false;
		}
	}

	/// <summary>Permanently deletes the world JSON from <see cref="FileSystem.Data"/> (not recoverable).</summary>
	public static bool TryDeleteWorldFile( string relativePath, out string errorMessage )
	{
		if ( !TryValidateListenHostWorldRelativePath( relativePath, out var rel, out errorMessage ) )
			return false;

		try
		{
			if ( !FileSystem.Data.FileExists( rel ) )
			{
				Log.Info( $"[Thorns] World save already absent (delete no-op): '{rel}'." );
				return true;
			}

			FileSystem.Data.DeleteFile( rel );
			Log.Info( $"[Thorns] Deleted world save file: '{rel}'." );
			return true;
		}
		catch ( Exception e )
		{
			errorMessage = e.Message;
			Log.Warning( e, $"[Thorns] Failed to delete world save '{rel}'." );
			return false;
		}
	}

	static bool TryValidateListenHostWorldRelativePath( string relativePath, out string rel, out string errorMessage )
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
			errorMessage = "Only world_*.json saves are allowed.";
			return false;
		}

		return true;
	}
}
