namespace ThinkDrink.Persistence;

/// <summary>s&amp;box FileSystem.Data adapter — swap for cloud/Unity/Roblox stores later.</summary>
public sealed class LocalPersistenceStore : IPersistenceStore
{
	public bool Exists( string path ) => FileSystem.Data.FileExists( path );

	public void WriteText( string path, string content )
	{
		var dir = Path.GetDirectoryName( path )?.Replace( '\\', '/' );
		if ( !string.IsNullOrEmpty( dir ) )
			FileSystem.Data.CreateDirectory( dir );
		FileSystem.Data.WriteAllText( path, content );
	}

	public string ReadText( string path ) => FileSystem.Data.ReadAllText( path );

	public void EnsureDirectory( string path ) => FileSystem.Data.CreateDirectory( path );
}
