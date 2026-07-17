namespace Deep;

public static class DeepSaveSystem
{
	public const string SaveFile = "deep/save.json";

	public static bool TryLoad( PlayerProgressionData progression )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SaveFile ) )
				return false;

			var data = FileSystem.Data.ReadJson<DeepSaveData>( SaveFile );
			if ( data is null )
				return false;

			var migrated = progression.ApplySaveData( data );
			Log.Info( $"[DEEP Save] Loaded '{SaveFile}'." );

			if ( migrated )
				Save( progression );

			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[DEEP Save] Load failed: {e.Message}" );
			return false;
		}
	}

	public static void Save( PlayerProgressionData progression )
	{
		try
		{
			if ( !FileSystem.Data.DirectoryExists( "deep" ) )
				FileSystem.Data.CreateDirectory( "deep" );

			var data = progression.ToSaveData();
			FileSystem.Data.WriteJson( SaveFile, data );
			Log.Info( $"[DEEP Save] Wrote '{SaveFile}'." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[DEEP Save] Save failed: {e.Message}" );
		}
	}
}
