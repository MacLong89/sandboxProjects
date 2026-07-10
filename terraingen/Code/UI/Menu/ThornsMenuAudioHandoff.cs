namespace Terraingen.UI.Menu;

/// <summary>Keeps menu audio alive across scene load long enough for the gameplay fade.</summary>
public static class ThornsMenuAudioHandoff
{
	public static SoundHandle Music;
	public static SoundHandle Ambience;
	public static float MusicVolume = 1f;
	public static float AmbienceVolume = 1f;

	static bool _armed;

	public static bool IsArmed => _armed;
	public static bool HasAny => Music is { IsValid: true } || Ambience is { IsValid: true };

	public static void ArmForGameplayTransition()
	{
		_armed = true;
	}

	public static bool TryTakeFromMenuAtmosphere(
		ref SoundHandle music,
		ref SoundHandle ambience,
		float musicVolume,
		float ambienceVolume )
	{
		if ( !_armed )
			return false;

		Music = music;
		Ambience = ambience;
		MusicVolume = MathF.Max( 0f, musicVolume );
		AmbienceVolume = MathF.Max( 0f, ambienceVolume );
		music = default;
		ambience = default;
		return HasAny;
	}

	public static void Clear()
	{
		_armed = false;
		Music = default;
		Ambience = default;
		MusicVolume = 1f;
		AmbienceVolume = 1f;
	}

	public static void Cancel()
	{
		_armed = false;
		StopHandle( ref Music );
		StopHandle( ref Ambience );
		Clear();
	}

	static void StopHandle( ref SoundHandle handle )
	{
		var h = handle;
		handle = default;
		if ( h is { IsValid: true } )
			h.Stop( 0.25f );
	}
}
