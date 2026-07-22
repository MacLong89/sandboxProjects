namespace Dynasty.Core;

/// <summary>Cross-save client UI preferences (tutorial tips, etc.).</summary>
public sealed class DynastyClientSettingsData
{
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();
}

public static class DynastyClientSettings
{
	public const string SettingsFile = "/saves/client_settings.json";

	public static DynastyClientSettingsData Current { get; private set; } = new();

	public static void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SettingsFile ) )
				return;

			var json = FileSystem.Data.ReadAllText( SettingsFile );
			var data = Json.Deserialize<DynastyClientSettingsData>( json );
			if ( data is not null )
			{
				data.TutorialTipsShown ??= new List<string>();
				Current = data;
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Dynasty client settings load failed — {ex.Message}" );
		}
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.CreateDirectory( "/saves" );
			FileSystem.Data.WriteAllText( SettingsFile, Json.Serialize( Current ) );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Dynasty client settings save failed — {ex.Message}" );
		}
	}

	public static void MarkTipShown( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return;

		Current.TutorialTipsShown ??= new List<string>();
		if ( !Current.TutorialTipsShown.Contains( id ) )
			Current.TutorialTipsShown.Add( id );

		Save();
	}
}
