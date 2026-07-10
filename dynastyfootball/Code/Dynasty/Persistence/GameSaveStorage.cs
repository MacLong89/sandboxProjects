namespace Dynasty.Persistence;

/// <summary>
/// s&box-whitelisted save I/O via FileSystem.Data (not System.IO).
/// </summary>
public static class GameSaveStorage
{
	public const string SaveRoot = "/saves";

	public static string GetSlotPath( string slotId ) => $"{SaveRoot}/{slotId}.dynasty.json";

	public static string GetDisplayPath() => FileSystem.Data.GetFullPath( SaveRoot ) ?? SaveRoot;

	public static void EnsureSaveDirectory() => FileSystem.Data.CreateDirectory( SaveRoot );

	public static bool SlotExists( string slotId ) => FileSystem.Data.FileExists( GetSlotPath( slotId ) );

	public static void WriteText( string virtualPath, string contents ) => FileSystem.Data.WriteAllText( virtualPath, contents );

	public static string ReadText( string virtualPath ) => FileSystem.Data.ReadAllText( virtualPath );

	public static void DeleteSlot( string slotId )
	{
		var path = GetSlotPath( slotId );
		if ( FileSystem.Data.FileExists( path ) )
			FileSystem.Data.DeleteFile( path );
	}

	public static IReadOnlyList<string> ListSaveFileNames()
	{
		if ( !FileSystem.Data.DirectoryExists( SaveRoot ) )
			return Array.Empty<string>();

		return FileSystem.Data.FindFile( SaveRoot, "*.dynasty.json" ).ToList();
	}

	public static string SlotIdFromFileName( string fileName )
	{
		const string suffix = ".dynasty.json";
		if ( fileName.EndsWith( suffix, StringComparison.OrdinalIgnoreCase ) )
			return fileName.Substring( 0, fileName.Length - suffix.Length );

		if ( fileName.EndsWith( ".json", StringComparison.OrdinalIgnoreCase ) )
			return fileName.Substring( 0, fileName.Length - ".json".Length );

		return fileName;
	}
}
