namespace UnderPressure;

/// <summary>Loads/saves <see cref="SaveData"/> as JSON under FileSystem.Data.</summary>
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
			Log.Warning( $"[UnderPressure] Save load failed, starting fresh: {e.Message}" );
		}

		return new SaveData();
	}

	/// <summary>Delete the on-disk save and return a fresh default snapshot.</summary>
	public static SaveData Wipe()
	{
		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.SaveFile ) )
				FileSystem.Data.DeleteFile( GameConstants.SaveFile );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UnderPressure] Save wipe failed: {e.Message}" );
		}

		return new SaveData();
	}

	public static void Save( SaveData data )
	{
		try
		{
			var dir = System.IO.Path.GetDirectoryName( GameConstants.SaveFile )?.Replace( '\\', '/' );
			if ( !string.IsNullOrEmpty( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteAllText( GameConstants.SaveFile, Json.Serialize( data ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[UnderPressure] Save write failed: {e.Message}" );
		}
	}

	private static SaveData Migrate( SaveData data )
	{
		if ( data.Version < 3 )
		{
			Log.Info( "[UnderPressure] Save schema outdated — resetting progress." );
			return new SaveData();
		}

		data.Upgrades ??= new Dictionary<string, int>();
		data.OwnedTools ??= new List<string>();
		if ( !data.OwnedTools.Contains( "PressureWasher" ) )
			data.OwnedTools.Add( "PressureWasher" );

		if ( data.Version < 6 )
		{
			Log.Info( "[UnderPressure] 25-level campaign overhaul — resetting progress." );
			return new SaveData();
		}

		data.DiscoveredSecrets ??= new List<string>();
		data.TutorialTipsShown ??= new List<string>();
		data.Version = SaveData.CurrentVersion;
		return data;
	}
}
