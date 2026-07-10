namespace UnderPressure;

/// <summary>
/// Centralised sound hooks. Each constant points at a .sound asset under Assets/sounds/.
/// The audio was sourced from the shared sound library and mapped to the game's events.
/// Any path that fails to load warns once, then is suppressed to keep the console clean.
/// </summary>
public static class Sfx
{
	// Sound assets live under Assets/sounds/ (mp3 + compiled .sound_c/.vsnd_c).
	public const string Spray = "sounds/spray.sound";
	public const string CleanTick = "sounds/button.sound";
	public const string Purchase = "sounds/economy.sound";
	public const string Prestige = "sounds/level_up.sound";
	public const string JobComplete = "sounds/skill_upgrade.sound";
	public const string Reward = "sounds/tame.sound";
	public const string Footstep = "sounds/footsteps.sound";
	public const string Gunshot = "sounds/button.sound";
	public const string VanDepart = "sounds/van.sound";

	private static readonly HashSet<string> _missing = new();

	public const float CleanTickVolume = 0.2f;

	public static void Play( string path, float volume = 1f )
	{
		var handle = PlayHandle( path );
		if ( handle is { IsValid: true } )
			handle.Volume = volume;
	}

	/// <summary>
	/// Play a 2D sound and return its handle so the caller can stop it (e.g. hold-to-spray
	/// that should cut off the moment the trigger is released). Returns null if the asset is
	/// missing; that path is then suppressed to keep the console clean.
	/// </summary>
	public static SoundHandle PlayHandle( string path )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return null;

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null )
				_missing.Add( path );
			return handle;
		}
		catch
		{
			_missing.Add( path );
			return null;
		}
	}

	public static void PlayAt( string path, Vector3 position )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return;

		try
		{
			var handle = Sound.Play( path, position );
			if ( handle is null )
				_missing.Add( path );
		}
		catch
		{
			_missing.Add( path );
		}
	}
}
