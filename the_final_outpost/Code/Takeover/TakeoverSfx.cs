namespace FinalOutpost;

/// <summary>
/// First-person gun SFX. Uses the same mp3s as third-person recruits (proven in this project),
/// played via direct Sound.Play so night combat music does not mute them.
/// Aimbox mapping kept for shotgun/sniper; pistol uses m4_shot (not bow_shoot).
/// </summary>
public static class TakeoverSfx
{
	public const string M4Fire = "sounds/m4_shot.sound";
	public const string ShotgunFire = "sounds/shotgun_shot.sound";
	public const string M4Reload = "sounds/m4_reload.sound";
	public const string ShotgunReload = "sounds/shotgun_reload.sound";
	public const string GunDeploy = "sounds/gun_deploy.sound";

	// Proven outpost wrappers (same mp3s, already imported).
	public const string FoPistol = "sounds/fo_pistol.sound";
	public const string FoSmg = "sounds/fo_smg.sound";
	public const string FoRifle = "sounds/fo_shoot.sound";
	public const string FoShotgun = "sounds/fo_shotgun.sound";
	public const string FoSniper = "sounds/fo_sniper.sound";

	static readonly HashSet<string> _loggedMissing = new();

	public static string FireSoundFor( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.Pistol => FoPistol,
		RecruitWeaponType.Smg => FoSmg,
		RecruitWeaponType.AssaultRifle => FoRifle,
		RecruitWeaponType.Shotgun => FoShotgun,
		RecruitWeaponType.Sniper => FoSniper,
		_ => FoRifle
	};

	public static string FireSoundFallback( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.Shotgun or RecruitWeaponType.Sniper => ShotgunFire,
		_ => M4Fire
	};

	public static string ReloadSoundFor( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.Shotgun => ShotgunReload,
		_ => M4Reload
	};

	public static float FireVolumeFor( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.Pistol => 0.85f,
		RecruitWeaponType.Smg => 0.9f,
		RecruitWeaponType.Sniper => 1.05f,
		RecruitWeaponType.Shotgun => 1f,
		_ => 1f
	};

	public static void PlayFire( TakeoverPawn pawn, TakeoverWeaponDef def )
	{
		if ( pawn is null || def is null ) return;
		var pos = pawn.EyePosition + pawn.EyeRotation.Forward * 12f;
		var vol = FireVolumeFor( def.RecruitType );
		if ( !PlayAt( pos, FireSoundFor( def.RecruitType ), vol ) )
			PlayAt( pos, FireSoundFallback( def.RecruitType ), vol );
	}

	public static void PlayReload( TakeoverPawn pawn, TakeoverWeaponDef def )
	{
		if ( pawn is null || def is null ) return;
		PlayAt( pawn.EyePosition + pawn.EyeRotation.Forward * 12f, ReloadSoundFor( def.RecruitType ), 1f );
	}

	public static void PlayDeploy( TakeoverPawn pawn )
	{
		if ( pawn is null ) return;
		PlayAt( pawn.EyePosition + pawn.EyeRotation.Forward * 12f, GunDeploy, 0.78f );
	}

	static bool PlayAt( Vector3 position, string resourcePath, float volume )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) ) return false;

		var handle = Sound.Play( resourcePath.Trim(), position );
		if ( !handle.IsValid() )
		{
			LogMissingOnce( resourcePath );
			return false;
		}

		handle.Volume = Math.Clamp( volume * AudioSettings.EffectiveSfx, 0f, 4f );
		handle.OcclusionEnabled = false;
		handle.SpacialBlend = 0.35f;
		return true;
	}

	static void LogMissingOnce( string resourcePath )
	{
		if ( !_loggedMissing.Add( resourcePath ) ) return;
		Log.Warning( $"[Takeover SFX] Failed to play '{resourcePath}' — check Assets/sounds." );
	}
}
