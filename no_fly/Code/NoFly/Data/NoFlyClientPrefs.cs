namespace NoFly;

/// <summary>Local client prefs (tutorial tips). Not synced.</summary>
public static class NoFlyClientPrefs
{
	const string FilePath = "no_fly/client-prefs.json";

	static NoFlyClientData _data;
	static bool _loaded;

	public static bool HideTutorialTips
	{
		get
		{
			EnsureLoaded();
			return _data.HideTutorialTips;
		}
		set
		{
			EnsureLoaded();
			_data.HideTutorialTips = value;
			Save();
		}
	}

	public static List<string> TutorialTipsShown
	{
		get
		{
			EnsureLoaded();
			_data.TutorialTipsShown ??= new List<string>();
			return _data.TutorialTipsShown;
		}
	}

	public static void MarkTipShown( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return;

		EnsureLoaded();
		_data.TutorialTipsShown ??= new List<string>();
		if ( !_data.TutorialTipsShown.Contains( id ) )
			_data.TutorialTipsShown.Add( id );
		Save();
	}

	static void EnsureLoaded()
	{
		if ( _loaded ) return;
		_loaded = true;

		try
		{
			if ( FileSystem.Data.FileExists( FilePath ) )
			{
				var json = FileSystem.Data.ReadAllText( FilePath );
				_data = Json.Deserialize<NoFlyClientData>( json ) ?? new NoFlyClientData();
			}
			else
			{
				_data = new NoFlyClientData();
			}
		}
		catch
		{
			_data = new NoFlyClientData();
		}

		_data.TutorialTipsShown ??= new List<string>();
	}

	static void Save()
	{
		try
		{
			var dir = System.IO.Path.GetDirectoryName( FilePath )?.Replace( '\\', '/' );
			if ( !string.IsNullOrEmpty( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteAllText( FilePath, Json.Serialize( _data ) );
		}
		catch
		{
			// Non-critical client prefs.
		}
	}
}

public sealed class NoFlyClientData
{
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();
}
