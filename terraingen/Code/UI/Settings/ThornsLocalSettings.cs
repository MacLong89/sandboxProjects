namespace Terraingen.UI;

/// <summary>Client-local persisted settings (graphics, audio, UI, crosshair, accessibility).</summary>
public sealed class ThornsLocalSettingsDto
{
	public float UiScale { get; set; } = 1f;
	public string UiSkin { get; set; } = "Classic";
	public float MasterVolume { get; set; } = 1f;
	public float SfxVolume { get; set; } = 1f;
	public float MusicVolume { get; set; } = 1f;
	public string GraphicsPreset { get; set; } = "High";
	public bool Vsync { get; set; } = true;
	public float MouseSensitivity { get; set; } = 0.5f;
	public float ControllerSensitivity { get; set; } = 0.5f;
	public bool ColorblindMode { get; set; }
	public string ColorblindPalette { get; set; } = "None";
	public float CrosshairScale { get; set; } = 1f;
	public string CrosshairStyle { get; set; } = "Default";
	public bool ShowDamageNumbers { get; set; } = true;
	public bool ReduceMotion { get; set; }
	public string LastMenuTab { get; set; } = "Inventory";
	public bool VictoryPathIntroDismissed { get; set; }
	public bool FirstSessionTutorialDismissed { get; set; }
	public bool ContainerShiftHintSeen { get; set; }
	public Dictionary<string, string> KeybindOverrides { get; set; } = new( StringComparer.OrdinalIgnoreCase );
}

public static class ThornsLocalSettings
{
	public const string RelativePath = "Terraingen/thorns_local_settings.json";

	public static ThornsLocalSettingsDto Current { get; private set; } = new();

	public static void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( RelativePath ) )
			{
				Current = new ThornsLocalSettingsDto();
				ApplyRuntime();
				return;
			}

			Current = FileSystem.Data.ReadJson<ThornsLocalSettingsDto>( RelativePath ) ?? new ThornsLocalSettingsDto();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Failed to load local settings." );
			Current = new ThornsLocalSettingsDto();
		}

		ApplyRuntime();
		UiRevisionBus.Publish( UiRevisionChannel.Settings );
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.WriteJson( RelativePath, Current );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Failed to save local settings." );
		}

		ApplyRuntime();
		UiRevisionBus.Publish( UiRevisionChannel.Settings );
	}

	public static void ApplyRuntime()
	{
		// Keep exposed Master Volume driving SFX until a separate SFX control ships.
		Current.SfxVolume = Math.Clamp( Current.MasterVolume, 0f, 1f );
		Current.MusicVolume = Math.Clamp( Current.MasterVolume, 0f, 1f );
		ThornsCrosshairSettings.Apply( Current );
		ThornsAudioSettings.Apply( Current );
	}
}

public static class ThornsCrosshairSettings
{
	public static float Scale { get; private set; } = 1f;
	public static string Style { get; private set; } = "Default";

	public static void Apply( ThornsLocalSettingsDto dto )
	{
		Scale = Math.Clamp( dto?.CrosshairScale ?? 1f, 0.5f, 2f );
		Style = dto?.CrosshairStyle ?? "Default";
		UiRevisionBus.Publish( UiRevisionChannel.Vitals );
	}
}
