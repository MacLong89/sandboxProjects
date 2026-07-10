namespace Sandbox;

/// <summary>
/// Keeps boot-menu music/ambience alive across <see cref="Game.ChangeScene"/> until
/// <see cref="ThornsGameplayEnterAudioFade"/> fades them out in gameplay.
/// </summary>
public static class ThornsMenuAudioHandoff
{
	public static bool IsArmed { get; private set; }

	public static SoundHandle Music;
	public static SoundHandle Ambience;
	public static float MusicVolume = 1f;
	public static float AmbienceVolume = 1f;

	public static void ArmForGameplayTransition() => IsArmed = true;

	public static void Cancel()
	{
		IsArmed = false;
		StopHandle( ref Music );
		StopHandle( ref Ambience );
	}

	public static bool TryTakeFromMenuAtmosphere(
		ref SoundHandle music,
		ref SoundHandle ambience,
		float musicVolume,
		float ambienceVolume )
	{
		if ( !IsArmed )
			return false;

		IsArmed = false;
		Music = music;
		Ambience = ambience;
		MusicVolume = MathF.Max( 0f, musicVolume );
		AmbienceVolume = MathF.Max( 0f, ambienceVolume );
		music = default;
		ambience = default;
		return Music.IsValid() || Ambience.IsValid();
	}

	public static bool HasActiveHandles =>
		Music is { IsValid: true } || Ambience is { IsValid: true };

	public static void StopHandle( ref SoundHandle handle )
	{
		var h = handle;
		handle = default;
		if ( h is { IsValid: true } )
			h.Stop( 0.05f );
	}

	public static void Clear()
	{
		IsArmed = false;
		StopHandle( ref Music );
		StopHandle( ref Ambience );
	}
}
