namespace RunGun;

public static class SaveManager
{
	public static SaveData Load()
	{
		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.SaveFile ) )
			{
				var json = FileSystem.Data.ReadAllText( GameConstants.SaveFile );
				var data = Json.Deserialize<SaveData>( json );
				if ( data is not null )
					return Migrate( data );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[RunGun] Save load failed, starting fresh: {e.Message}" );
		}

		return new SaveData();
	}

	public static void Save( SaveData data )
	{
		try
		{
			data.LastPlayedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var dir = System.IO.Path.GetDirectoryName( GameConstants.SaveFile )?.Replace( '\\', '/' );
			if ( !string.IsNullOrEmpty( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteAllText( GameConstants.SaveFile, Json.Serialize( data ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[RunGun] Save write failed: {e.Message}" );
		}
	}

	private static SaveData Migrate( SaveData data )
	{
		data.Upgrades ??= new Dictionary<string, int>();
		data.UnlockedCharacters ??= new HashSet<string> { "runner" };
		data.CompletedAchievements ??= new HashSet<string>();

		if ( string.IsNullOrEmpty( data.SelectedCharacter ) )
			data.SelectedCharacter = "runner";

		if ( !data.UnlockedCharacters.Contains( "runner" ) )
			data.UnlockedCharacters.Add( "runner" );

		if ( data.Version < 2 )
			data.Version = 2;

		return data;
	}
}
