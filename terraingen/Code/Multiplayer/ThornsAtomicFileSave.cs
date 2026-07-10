namespace Terraingen.Multiplayer;

/// <summary>Crash-safe JSON writes for world and profile saves.</summary>
public static class ThornsAtomicFileSave
{
	public static bool TryWriteJson<T>( string relativePath, T data, out string error )
	{
		error = "";
		if ( string.IsNullOrWhiteSpace( relativePath ) )
		{
			error = "path empty";
			return false;
		}

		var tempPath = $"{relativePath}.tmp";
		try
		{
			if ( FileSystem.Data.FileExists( relativePath ) )
			{
				var backupPath = $"{relativePath}.bak";
				FileSystem.Data.WriteJson( backupPath, FileSystem.Data.ReadJson<T>( relativePath ) );
			}

			FileSystem.Data.WriteJson( tempPath, data );

			var committed = FileSystem.Data.ReadJson<T>( tempPath );
			if ( committed is null )
			{
				error = "temp write verification failed";
				return false;
			}

			FileSystem.Data.WriteJson( relativePath, committed );
			if ( FileSystem.Data.FileExists( tempPath ) )
				FileSystem.Data.DeleteFile( tempPath );

			return true;
		}
		catch ( Exception e )
		{
			error = e.Message;
			try
			{
				if ( FileSystem.Data.FileExists( tempPath ) )
					FileSystem.Data.DeleteFile( tempPath );
			}
			catch
			{
				// Best-effort cleanup.
			}

			return false;
		}
	}

	public static bool TryReadJson<T>( string relativePath, out T data, out string error ) where T : class
	{
		data = null;
		error = "";
		if ( string.IsNullOrWhiteSpace( relativePath ) || !FileSystem.Data.FileExists( relativePath ) )
		{
			error = "missing";
			return false;
		}

		try
		{
			data = FileSystem.Data.ReadJson<T>( relativePath );
			return data is not null;
		}
		catch ( Exception e )
		{
			error = e.Message;
			return false;
		}
	}
}
