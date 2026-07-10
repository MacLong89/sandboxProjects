namespace FinalOutpost;

public static class SaveManager
{
	private static PlayerProfile _profile;
	private static SaveData _survival;
	private static SaveData _cure;

	public static PlayerProfile Profile => _profile ??= LoadProfile();

	public static SaveData SurvivalSave => _survival ??= LoadMode( GameModeId.Survival );
	public static SaveData CureSave => _cure ??= LoadMode( GameModeId.RoadToCure );

	public static SaveData Load( GameModeId mode = GameModeId.Survival )
	{
		_profile ??= LoadProfile();
		return mode switch
		{
			GameModeId.RoadToCure => CureSave,
			_ => SurvivalSave
		};
	}

	/// <summary>Legacy entry — loads survival save.</summary>
	public static SaveData Load() => Load( GameModeId.Survival );

	public static void Save( SaveData data, GameModeId mode )
	{
		try
		{
			data.LastPlayedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var path = PathFor( mode );
			WriteJson( path, data );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Save write failed ({mode}): {e.Message}" );
		}
	}

	public static void Save( SaveData data ) =>
		Save( data, GameCore.Instance?.ActiveMode ?? GameModeId.Survival );

	public static void SaveProfile( PlayerProfile profile )
	{
		try
		{
			WriteJson( GameConstants.ProfileFile, profile );
			_profile = profile;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Profile write failed: {e.Message}" );
		}
	}

	public static void ReloadMode( GameModeId mode )
	{
		if ( mode == GameModeId.RoadToCure )
			_cure = LoadModeFile( GameModeId.RoadToCure );
		else
			_survival = LoadModeFile( GameModeId.Survival );
	}

	private static PlayerProfile LoadProfile()
	{
		MigrateLegacySave();

		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.ProfileFile ) )
			{
				var json = FileSystem.Data.ReadAllText( GameConstants.ProfileFile );
				var profile = Json.Deserialize<PlayerProfile>( json );
				if ( profile is not null )
					return profile;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Profile load failed: {e.Message}" );
		}

		var fromSurvival = LoadModeFile( GameModeId.Survival );
		var created = new PlayerProfile();
		created.PullAudioFrom( fromSurvival );
		if ( fromSurvival.HasRunInProgress )
			created.HasEverStartedSurvival = true;
		SaveProfile( created );
		return created;
	}

	private static SaveData LoadMode( GameModeId mode )
	{
		MigrateLegacySave();
		return LoadModeFile( mode );
	}

	private static SaveData LoadModeFile( GameModeId mode )
	{
		var path = PathFor( mode );
		try
		{
			if ( FileSystem.Data.FileExists( path ) )
			{
				var json = FileSystem.Data.ReadAllText( path );
				var data = Json.Deserialize<SaveData>( json );
				if ( data is not null )
				{
					data.Upgrades ??= new Dictionary<string, int>();
					data.Buildings ??= new List<SavedBuilding>();
					data.Migrate();
					Profile.ApplyAudioTo( data );
					return data;
				}
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Save load failed ({mode}): {e.Message}" );
		}

		return new SaveData();
	}

	private static void MigrateLegacySave()
	{
		if ( FileSystem.Data.FileExists( GameConstants.SurvivalSaveFile ) )
			return;
		if ( !FileSystem.Data.FileExists( GameConstants.SaveFile ) )
			return;

		try
		{
			var json = FileSystem.Data.ReadAllText( GameConstants.SaveFile );
			var data = Json.Deserialize<SaveData>( json );
			if ( data is null ) return;

			data.Migrate();
			WriteJson( GameConstants.SurvivalSaveFile, data );

			var profile = new PlayerProfile();
			profile.PullAudioFrom( data );
			if ( data.HasRunInProgress || data.HasStartedRun )
				profile.HasEverStartedSurvival = true;
			WriteJson( GameConstants.ProfileFile, profile );
			_profile = profile;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[FinalOutpost] Legacy save migration failed: {e.Message}" );
		}
	}

	private static string PathFor( GameModeId mode ) => mode switch
	{
		GameModeId.RoadToCure => GameConstants.CureSaveFile,
		_ => GameConstants.SurvivalSaveFile
	};

	private static void WriteJson( string path, object data )
	{
		var dir = System.IO.Path.GetDirectoryName( path )?.Replace( '\\', '/' );
		if ( !string.IsNullOrEmpty( dir ) )
			FileSystem.Data.CreateDirectory( dir );

		FileSystem.Data.WriteAllText( path, Json.Serialize( data ) );
	}
}
