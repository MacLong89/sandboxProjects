using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

/// <summary>Local client preferences (sensitivity / volume). Not synced; not a progression exploit surface.</summary>
public static class AimboxClientSettings
{
	const string FilePath = "aimbox/client-settings.json";

	static AimboxClientSettingsData _data;
	static bool _loaded;

	public static float MouseSensitivity
	{
		get
		{
			EnsureLoaded();
			return _data.MouseSensitivity;
		}
		set
		{
			EnsureLoaded();
			_data.MouseSensitivity = Math.Clamp( value, 0.25f, 3f );
			Save();
		}
	}

	public static float MasterVolume
	{
		get
		{
			EnsureLoaded();
			return _data.MasterVolume;
		}
		set
		{
			EnsureLoaded();
			_data.MasterVolume = Math.Clamp( value, 0f, 1f );
			Save();
		}
	}

	public static float MusicVolume
	{
		get
		{
			EnsureLoaded();
			return _data.MusicVolume;
		}
		set
		{
			EnsureLoaded();
			_data.MusicVolume = Math.Clamp( value, 0f, 1f );
			Save();
		}
	}

	public static float SfxVolume
	{
		get
		{
			EnsureLoaded();
			return _data.SfxVolume;
		}
		set
		{
			EnsureLoaded();
			_data.SfxVolume = Math.Clamp( value, 0f, 1f );
			Save();
		}
	}

	public static float EffectiveMusicVolume => MasterVolume * MusicVolume;
	public static float EffectiveSfxVolume => MasterVolume * SfxVolume;

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

	public static IReadOnlyList<string> TutorialTipsShown
	{
		get
		{
			EnsureLoaded();
			_data.TutorialTipsShown ??= new List<string>();
			return _data.TutorialTipsShown;
		}
	}

	public static bool VisitedPlayLobby
	{
		get
		{
			EnsureLoaded();
			return _data.VisitedPlayLobby;
		}
		set
		{
			EnsureLoaded();
			if ( _data.VisitedPlayLobby == value )
				return;

			_data.VisitedPlayLobby = value;
			Save();
		}
	}

	public static bool VisitedLoadouts
	{
		get
		{
			EnsureLoaded();
			return _data.VisitedLoadouts;
		}
		set
		{
			EnsureLoaded();
			if ( _data.VisitedLoadouts == value )
				return;

			_data.VisitedLoadouts = value;
			Save();
		}
	}

	public static bool StartedMatchFromOnboarding
	{
		get
		{
			EnsureLoaded();
			return _data.StartedMatchFromOnboarding;
		}
		set
		{
			EnsureLoaded();
			if ( _data.StartedMatchFromOnboarding == value )
				return;

			_data.StartedMatchFromOnboarding = value;
			Save();
		}
	}

	public static void MarkTipShown( string id )
	{
		EnsureLoaded();
		_data.TutorialTipsShown ??= new List<string>();
		if ( !_data.TutorialTipsShown.Contains( id ) )
			_data.TutorialTipsShown.Add( id );
		Save();
	}

	public static void CycleMouseSensitivity()
	{
		var next = MouseSensitivity switch
		{
			<= 0.6f => 0.85f,
			<= 1.1f => 1.35f,
			<= 1.6f => 2f,
			_ => 0.5f
		};
		MouseSensitivity = next;
	}

	public static void CycleMasterVolume()
	{
		var next = MasterVolume switch
		{
			<= 0.01f => 0.35f,
			<= 0.4f => 0.7f,
			<= 0.85f => 1f,
			_ => 0f
		};
		MasterVolume = next;
	}

	public static void CycleMusicVolume()
	{
		var next = MusicVolume switch
		{
			<= 0.01f => 0.35f,
			<= 0.4f => 0.7f,
			<= 0.85f => 1f,
			_ => 0f
		};
		MusicVolume = next;
	}

	public static void CycleSfxVolume()
	{
		var next = SfxVolume switch
		{
			<= 0.01f => 0.35f,
			<= 0.4f => 0.7f,
			<= 0.85f => 1f,
			_ => 0f
		};
		SfxVolume = next;
	}

	public static string FormatSensitivity() => $"{MouseSensitivity:0.00}x";
	public static string FormatVolume( float value ) => value <= 0.01f ? "OFF" : $"{(int)MathF.Round( value * 100f )}%";

	static void EnsureLoaded()
	{
		if ( _loaded )
			return;

		_loaded = true;
		_data = new AimboxClientSettingsData();

		try
		{
			if ( !FileSystem.Data.FileExists( FilePath ) )
				return;

			var json = FileSystem.Data.ReadAllText( FilePath );
			var parsed = JsonSerializer.Deserialize<AimboxClientSettingsData>( json );
			if ( parsed is not null )
				_data = parsed;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Aimbox] Failed to load client settings: {e.Message}" );
		}

		_data.MouseSensitivity = Math.Clamp( _data.MouseSensitivity, 0.25f, 3f );
		_data.MasterVolume = Math.Clamp( _data.MasterVolume, 0f, 1f );
		_data.MusicVolume = Math.Clamp( _data.MusicVolume, 0f, 1f );
		_data.SfxVolume = Math.Clamp( _data.SfxVolume, 0f, 1f );
	}

	static void Save()
	{
		try
		{
			var json = JsonSerializer.Serialize( _data, new JsonSerializerOptions { WriteIndented = true } );
			FileSystem.Data.WriteAllText( FilePath, json );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Aimbox] Failed to save client settings: {e.Message}" );
		}
	}
}

sealed class AimboxClientSettingsData
{
	[JsonPropertyName( "mouseSensitivity" )]
	public float MouseSensitivity { get; set; } = 1f;

	[JsonPropertyName( "masterVolume" )]
	public float MasterVolume { get; set; } = 1f;

	[JsonPropertyName( "musicVolume" )]
	public float MusicVolume { get; set; } = 1f;

	[JsonPropertyName( "sfxVolume" )]
	public float SfxVolume { get; set; } = 1f;

	[JsonPropertyName( "hideTutorialTips" )]
	public bool HideTutorialTips { get; set; }

	[JsonPropertyName( "tutorialTipsShown" )]
	public List<string> TutorialTipsShown { get; set; } = new();

	[JsonPropertyName( "visitedPlayLobby" )]
	public bool VisitedPlayLobby { get; set; }

	[JsonPropertyName( "visitedLoadouts" )]
	public bool VisitedLoadouts { get; set; }

	[JsonPropertyName( "startedMatchFromOnboarding" )]
	public bool StartedMatchFromOnboarding { get; set; }
}
