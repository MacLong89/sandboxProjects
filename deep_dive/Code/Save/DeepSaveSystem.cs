namespace DeepDive;

public static class DeepDiveSaveSystem
{
	public const string SaveFile = "deep/save.json";

	public static bool TryLoad( PlayerProgressionData progression )
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SaveFile ) )
				return false;

			var data = FileSystem.Data.ReadJson<DeepDiveSaveData>( SaveFile );
			if ( data is null )
				return false;

			var migrated = progression.ApplySaveData( data );
			Log.Info( $"[DeepDive Save] Loaded '{SaveFile}'." );

			if ( migrated )
				Save( progression );

			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[DeepDive Save] Load failed: {e.Message}" );
			return false;
		}
	}

	public static void Save( PlayerProgressionData progression )
	{
		try
		{
			if ( !FileSystem.Data.DirectoryExists( "DEEP DIVE" ) )
				FileSystem.Data.CreateDirectory( "DEEP DIVE" );

			var data = progression.ToSaveData();
			FileSystem.Data.WriteJson( SaveFile, data );
			Log.Info( $"[DeepDive Save] Wrote '{SaveFile}'." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[DeepDive Save] Save failed: {e.Message}" );
		}
	}
}
