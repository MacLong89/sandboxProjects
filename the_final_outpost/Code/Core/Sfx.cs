using System.IO;
using System.Runtime.CompilerServices;

namespace FinalOutpost;

public static class Sfx
{
	// Per-weapon fire sounds, correlated to terraingen's own weapon->sound mapping
	// (shotgun -> shotgun_shot; USP/MP5/M4/M700 -> m4_shot), differentiated by pitch.
	public const string Shoot = "sounds/fo_shoot.sound";        // Assault rifle (M4A1) -> m4_shot
	public const string ShootPistol = "sounds/fo_pistol.sound"; // USP -> m4_shot
	public const string ShootSmg = "sounds/fo_smg.sound";       // MP5 -> m4_shot
	public const string ShootShotgun = "sounds/fo_shotgun.sound"; // Spaghelli -> shotgun_shot
	public const string ShootSniper = "sounds/fo_sniper.sound"; // M700 -> m4_shot
	public const string Turret = "sounds/fo_turret.sound";
	public const string ZombieHit = "sounds/fo_zombie_hit.sound";
	public const string ZombieDeath = "sounds/fo_zombie_death.sound";
	public const string WallHit = "sounds/fo_wall_hit.sound";
	public const string WaveStart = "sounds/fo_wave_start.sound";
	public const string WaveClear = "sounds/fo_wave_clear.sound";
	public const string Purchase = "sounds/fo_purchase.sound";
	public const string GameOver = "sounds/fo_game_over.sound";
	public const string UiClick = "sounds/fo_button.sound";

	/// <summary>When true, every <see cref="Play"/> call writes a line to the console.</summary>
	/// <summary>
	/// AUDIT FIX M4 (2026-07): defaulted true and spammed every Sfx.Play in production.
	/// Flip true only when diagnosing missing/duplicate audio.
	/// </summary>
	public static bool LogPlays { get; set; } = false;

	private static readonly HashSet<string> _missing = new();
	private static string _lastDupKey;
	private static double _lastDupTime;

	private static bool BlocksNightGameplaySounds =>
		GameConstants.UseNightCombatMusicLoop && GameCore.Instance?.Phase == GamePhase.Night;

	public static void Play(
		string path,
		string tag = null,
		[CallerMemberName] string caller = "",
		[CallerFilePath] string file = "",
		[CallerLineNumber] int line = 0 )
	{
		if ( BlocksNightGameplaySounds )
			return;

		TryPlay( path, VolumeScaleFor( path ), 1f, tag, caller, file, line );
	}

	/// <summary>Combat director entry — extra scale and pitch without legacy per-path attenuation.</summary>
	public static bool TryPlay(
		string path,
		float volumeScale = 1f,
		float pitch = 1f,
		string tag = null,
		[CallerMemberName] string caller = "",
		[CallerFilePath] string file = "",
		[CallerLineNumber] int line = 0 )
	{
		if ( BlocksNightGameplaySounds )
			return false;

		return PlayInternal( path, AudioSettings.EffectiveSfx * volumeScale, pitch, tag, caller, file, line );
	}

	/// <summary>UI open/click feedback — uses the SFX volume slider.</summary>
	public static void PlayUi( string tag = null ) => Play( UiClick, tag ?? "UiClick" );

	private static bool PlayInternal(
		string path,
		float volume,
		float pitch,
		string tag,
		string caller,
		string file,
		int line )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return false;

		var source = FormatSource( tag, caller, file, line );
		var label = SoundLabel( path );

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null )
			{
				_missing.Add( path );
				if ( LogPlays )
					Log.Warning( $"[Sfx] FAILED {label} — {source}" );
				return false;
			}

			handle.Volume = volume;
			if ( MathF.Abs( pitch - 1f ) > 0.001f )
				handle.Pitch = pitch;

			if ( LogPlays )
				WriteLog( label, source );

			return true;
		}
		catch ( Exception e )
		{
			_missing.Add( path );
			if ( LogPlays )
				Log.Warning( $"[Sfx] ERROR {label} — {source}: {e.Message}" );
			return false;
		}
	}

	private static float VolumeScaleFor( string path )
	{
		if ( path is ShootShotgun or Turret )
			return GameConstants.ShotgunVolumeScale;

		if ( path is WallHit )
			return GameConstants.ZombieImpactVolumeScale;

		return 1f;
	}

	private static void WriteLog( string label, string source )
	{
		var now = Time.Now;
		var dupKey = $"{label}|{source}";

		if ( dupKey == _lastDupKey && (now - _lastDupTime) < 0.02 )
			Log.Warning( $"[Sfx] x2 {label} — {source} (within 20ms — possible double-play)" );
		else
			Log.Info( $"[Sfx] {label} — {source}" );

		_lastDupKey = dupKey;
		_lastDupTime = now;
	}

	private static string SoundLabel( string path )
	{
		var file = Path.GetFileName( path );
		if ( string.IsNullOrEmpty( file ) ) return path;
		return file.EndsWith( ".sound", StringComparison.OrdinalIgnoreCase )
			? file[..^6]
			: file;
	}

	private static string FormatSource( string tag, string caller, string file, int line )
	{
		if ( !string.IsNullOrWhiteSpace( tag ) )
			return tag;

		var shortFile = Path.GetFileName( file );
		return $"{shortFile}:{line} ({caller})";
	}
}
