namespace ThinkDrink;

public static class GameSettings
{
	const int CurrentSettingsVersion = 2;

	public static PlayerSettings Current { get; private set; } = new();

	public static void Load()
	{
		var loadedFromDisk = false;

		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.SettingsFile ) )
			{
				var json = FileSystem.Data.ReadAllText( GameConstants.SettingsFile );
				Current = Json.Deserialize<PlayerSettings>( json ) ?? new PlayerSettings();
				loadedFromDisk = true;
			}
		}
		catch
		{
			Current = new PlayerSettings();
			loadedFromDisk = true;
		}

		if ( loadedFromDisk && Current.SettingsVersion < CurrentSettingsVersion )
			MigrateSettings();
	}

	static void MigrateSettings()
	{
		if ( Current.SettingsVersion < 2 )
		{
			Current.MasterVolume = Math.Clamp( Current.MasterVolume * 0.5f, 0f, 1f );
			Current.SfxVolume = Math.Clamp( Current.SfxVolume * 0.5f, 0f, 1f );
			Current.MusicVolume = Math.Clamp( Current.MusicVolume * 0.5f, 0f, 1f );
		}

		Current.SettingsVersion = CurrentSettingsVersion;
		Save();
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.CreateDirectory( GameConstants.SaveDirectory );
			FileSystem.Data.WriteAllText( GameConstants.SettingsFile, Json.Serialize( Current ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Think & Drink: settings save failed — {e.Message}" );
		}
	}
}
