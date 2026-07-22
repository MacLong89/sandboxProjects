namespace Fauna2;

/// <summary>Persisted client preferences (not part of zoo saves).</summary>
public sealed class GameSettingsData
{
	public bool BuildDebug { get; set; }
	public float CameraPanSpeed { get; set; } = 1.1f;
	public float CameraZoomSpeed { get; set; } = 0.15f;
	public bool ShowTutorialHints { get; set; } = true;
	/// <summary>Retired — tip onboarding replaced the controls card. Kept for save migration.</summary>
	public bool ShowControlsHint { get; set; }
	/// <summary>Retired — tip onboarding replaced the welcome intro. Kept for save migration.</summary>
	public bool ShowWelcomeIntro { get; set; }
	/// <summary>Versioned onboarding migration; lets corrected teaching appear once for existing players.</summary>
	public int OnboardingVersion { get; set; }
	/// <summary>Ids of onboarding coach tips the player has already dismissed.</summary>
	public List<string> OnboardingTipsShown { get; set; } = new();
	/// <summary>Player chose Hide tips — stop showing onboarding coach cards.</summary>
	public bool HideOnboardingTips { get; set; }
	/// <summary>Master volume for music, ambience, and SFX (0–1).</summary>
	public float MasterVolume { get; set; } = GameSettings.DefaultMasterVolume;
	/// <summary>Scales guest ticket revenue. 1.0 = original balance.</summary>
	public float GuestRevenueMultiplier { get; set; } = GameConstants.DefaultGuestRevenueMultiplier;
	/// <summary>Local reward earned by visiting another player's zoo; claimed next time this player hosts.</summary>
	public int PendingVisitorCredits { get; set; }
	/// <summary>UTC day of the last reciprocal visitor reward, preventing lobby-hop farming.</summary>
	public long LastVisitorRewardUnixDay { get; set; }
	/// <summary>Retired — living sprites always use Bounce (idle + bob). Kept for save migration.</summary>
	public string SpriteMotionMode { get; set; } = "Bounce";
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

				// Force-retire legacy welcome / controls cards — tips are the only onboarding path.
				data.ShowWelcomeIntro = false;
				data.ShowControlsHint = false;
				// Walk cycle clips were retired; always Bounce (directional idle + bob).
				data.SpriteMotionMode = "Bounce";

				data.MasterVolume = data.MasterVolume.Clamp( 0f, 1f );
				data.OnboardingTipsShown ??= new List<string>();
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
