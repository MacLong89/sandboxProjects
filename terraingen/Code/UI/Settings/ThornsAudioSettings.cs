namespace Terraingen.UI;

/// <summary>Applies client-local audio/accessibility settings to runtime systems.</summary>
public static class ThornsAudioSettings
{
	public static float MasterVolume { get; private set; } = 1f;
	public static float SfxVolume { get; private set; } = 1f;
	public static float MusicVolume { get; private set; } = 1f;
	public static bool ColorblindMode { get; private set; }
	public static bool ReduceMotion { get; private set; }
	public static bool Vsync { get; private set; } = true;

	public static float EffectiveSfxVolume => Math.Clamp( MasterVolume * SfxVolume, 0f, 1f );
	public static float EffectiveMusicVolume => Math.Clamp( MasterVolume * MusicVolume, 0f, 1f );

	public static void Apply( ThornsLocalSettingsDto dto )
	{
		MasterVolume = Math.Clamp( dto?.MasterVolume ?? 1f, 0f, 1f );
		SfxVolume = Math.Clamp( dto?.SfxVolume ?? 1f, 0f, 1f );
		MusicVolume = Math.Clamp( dto?.MusicVolume ?? 1f, 0f, 1f );
		ColorblindMode = dto?.ColorblindMode ?? false;
		ReduceMotion = dto?.ReduceMotion ?? false;
		Vsync = dto?.Vsync ?? true;

		// VSync preference is stored; engine binding varies by s&box build.
		// Colorblind/reduce-motion flags are available via ThornsAudioSettings for HUD consumers.
	}
}
