namespace Fauna2;

/// <summary>Persisted client preferences (not part of zoo saves).</summary>
public sealed class GameSettingsData
{
	public bool BuildDebug { get; set; }
	public float CameraPanSpeed { get; set; } = 1.1f;
	public float CameraZoomSpeed { get; set; } = 0.15f;
	public bool ShowTutorialHints { get; set; } = true;
	/// <summary>Show the first-session controls overlay once.</summary>
	public bool ShowControlsHint { get; set; } = true;
	/// <summary>Show the centered welcome intro once when a zoo session starts.</summary>
	public bool ShowWelcomeIntro { get; set; } = true;
	/// <summary>Master volume for music, ambience, and SFX (0–1).</summary>
	public float MasterVolume { get; set; } = GameSettings.DefaultMasterVolume;
	/// <summary>Scales guest ticket revenue. 1.0 = original balance.</summary>
	public float GuestRevenueMultiplier { get; set; } = GameConstants.DefaultGuestRevenueMultiplier;
}

/// <summary>Loads and applies local game settings from disk.</summary>
public static class GameSettings
{
	public const string SettingsFile = "fauna/settings.json";
	public const float DefaultMasterVolume = 0.3f;

	public static GameSettingsData Current { get; set; } = new();

	public static float VolumeMultiplier => Current.MasterVolume.Clamp( 0f, 1f );

	public static void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SettingsFile ) ) return;

			var json = FileSystem.Data.ReadAllText( SettingsFile );
			var data = Json.Deserialize<GameSettingsData>( json );
			if ( data is not null )
			{
				if ( data.GuestRevenueMultiplier <= 0f )
					data.GuestRevenueMultiplier = GameConstants.DefaultGuestRevenueMultiplier;

				if ( !json.Contains( "MasterVolume", StringComparison.OrdinalIgnoreCase ) )
					data.MasterVolume = DefaultMasterVolume;

				if ( !json.Contains( "ShowWelcomeIntro", StringComparison.OrdinalIgnoreCase ) && !data.ShowControlsHint )
					data.ShowWelcomeIntro = false;

				data.MasterVolume = data.MasterVolume.Clamp( 0f, 1f );
				Current = data;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: settings load failed — {e.Message}" );
		}

		Apply();
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.CreateDirectory( "fauna" );
			FileSystem.Data.WriteAllText( SettingsFile, Json.Serialize( Current ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Fauna: settings save failed — {e.Message}" );
		}

		Apply();
	}

	public static void Apply()
	{
		UI.UiState.BuildDebug = Current.BuildDebug;

		if ( ZooCameraController.Instance.IsValid() )
			ZooCameraController.Instance.ZoomSpeed = Current.CameraZoomSpeed;

		ZooAudioController.Instance?.RefreshLoopVolumes();
	}
}
