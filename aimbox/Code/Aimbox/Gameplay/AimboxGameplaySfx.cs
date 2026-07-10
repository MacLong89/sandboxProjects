namespace Sandbox;

public static class AimboxGameplaySfx
{
	public const string M4Fire = "sounds/m4_shot.sound";
	public const string ShotgunFire = "sounds/shotgun_shot.sound";
	public const string M4Reload = "sounds/m4_reload.sound";
	public const string ShotgunReload = "sounds/shotgun_reload.sound";
	public const string GunDeploy = "sounds/gun_deploy.sound";
	public const string KnifeLight = "sounds/knife_stab_light.sound";
	public const string KnifeHeavy = "sounds/knife_stab_heavy.sound";
	public const string MeleeMiss = "sounds/melee_miss.sound";
	public const string BowDraw = "sounds/bow_draw.sound";
	public const string BowShoot = "sounds/bow_shoot.sound";
	public const string Footstep = "sounds/footsteps_sound.sound";

	static readonly HashSet<string> _loggedMissing = [];

	public static void PlayAtActor( IAimboxCombatActor actor, string resourcePath, float volume = 1f )
	{
		if ( actor is null || !actor.GameObject.IsValid() || string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		if ( Application.IsDedicatedServer || Application.IsHeadless || !Game.IsPlaying )
			return;

		var position = ResolvePlayPosition( actor );
		var handle = Sound.Play( resourcePath.Trim(), position );
		if ( !handle.IsValid() )
		{
			LogMissingOnce( resourcePath );
			return;
		}

		handle.Volume = Math.Clamp( volume, 0f, 4f );

		if ( IsLocalHuman( actor ) )
		{
			handle.OcclusionEnabled = false;
			handle.SpacialBlend = 0.35f;
		}
	}

	public static void PlayBowDraw( IAimboxCombatActor actor ) =>
		PlayAtActor( actor, BowDraw, 0.9f );

	public static void PlayEquip( IAimboxCombatActor actor, AimboxWeaponDefinition weapon )
	{
		if ( weapon is null )
			return;

		if ( weapon.Id is AimboxWeaponId.M4A1
		    or AimboxWeaponId.Mp5
		    or AimboxWeaponId.Usp
		    or AimboxWeaponId.M700
		    or AimboxWeaponId.SpaghelliM4 )
			PlayAtActor( actor, GunDeploy, 0.78f );
	}

	public static void PlayFire( IAimboxCombatActor actor, AimboxWeaponDefinition weapon, float volumeMultiplier = 1f )
	{
		var path = FireSoundForWeapon( weapon );
		if ( !string.IsNullOrWhiteSpace( path ) )
			PlayAtActor( actor, path, Math.Clamp( volumeMultiplier, 0f, 1f ) );
	}

	public static void PlayReload( IAimboxCombatActor actor, AimboxWeaponDefinition weapon, float volumeMultiplier = 1f )
	{
		var path = ReloadSoundForWeapon( weapon );
		if ( !string.IsNullOrWhiteSpace( path ) )
			PlayAtActor( actor, path, Math.Clamp( volumeMultiplier, 0f, 1f ) );
	}

	public static void PlayMeleeSwing( IAimboxCombatActor actor, bool heavy )
	{
		PlayAtActor( actor, heavy ? KnifeHeavy : MeleeMiss, heavy ? 0.85f : 0.75f );
	}

	public static void PlayMeleeContact( IAimboxCombatActor actor, bool heavy )
	{
		PlayAtActor( actor, heavy ? KnifeHeavy : KnifeLight, heavy ? 0.95f : 0.72f );
	}

	public static void PlayGrenadeThrow( IAimboxCombatActor actor, AimboxWeaponId grenadeId ) =>
		PlayAtActor( actor, MeleeMiss, 0.48f );

	public static void PlayGrenadeDetonate( IAimboxCombatActor actor, AimboxWeaponId grenadeId )
	{
		var (path, volume) = grenadeId switch
		{
			AimboxWeaponId.FlashGrenade => (MeleeMiss, 0.55f),
			AimboxWeaponId.IncendiaryGrenade => (BowDraw, 0.72f),
			AimboxWeaponId.SmokeGrenade => (M4Reload, 0.62f),
			_ => (ShotgunFire, 0.85f)
		};

		PlayAtActor( actor, path, volume );
	}

	public static void PlayUnlockCelebration( IAimboxCombatActor actor, AimboxUnlockCelebrationKind kind )
	{
		var (path, volume) = kind switch
		{
			AimboxUnlockCelebrationKind.RankUp => (GunDeploy, 0.92f),
			AimboxUnlockCelebrationKind.WeaponUnlock => (GunDeploy, 0.88f),
			AimboxUnlockCelebrationKind.AttachmentUnlock => (M4Reload, 0.82f),
			AimboxUnlockCelebrationKind.MasteryUp => (BowDraw, 0.78f),
			_ => (GunDeploy, 0.75f)
		};

		PlayAtActor( actor, path, volume );
	}

	public static void PlayFootstep( IAimboxCombatActor actor, bool sprinting, bool crouching, float noiseMultiplier = 1f )
	{
		if ( actor is null || !actor.GameObject.IsValid() )
			return;

		if ( Application.IsDedicatedServer || Application.IsHeadless || !Game.IsPlaying )
			return;

		var volume = sprinting ? 0.68f : 0.48f;
		if ( crouching )
			volume *= 0.5f;
		volume *= Math.Clamp( noiseMultiplier, 0.08f, 1f );

		var handle = Sound.Play( Footstep, actor.WorldPosition );
		if ( !handle.IsValid() )
		{
			LogMissingOnce( Footstep );
			return;
		}

		handle.Volume = volume;

		if ( IsLocalHuman( actor ) )
		{
			handle.OcclusionEnabled = false;
			handle.SpacialBlend = 0.22f;
		}
	}

	static Vector3 ResolvePlayPosition( IAimboxCombatActor actor )
	{
		if ( IsLocalHuman( actor ) )
			return actor.EyePosition + actor.AimForward * 12f;

		if ( actor.ShowThirdPersonBody || actor is AimboxBotController )
		{
			if ( AimboxCombatMuzzleResolve.TryResolveThirdPersonMuzzleWorld(
				     actor,
				     actor.ActiveWeapon,
				     actor.AimForward,
				     out var muzzle ) )
				return muzzle;

			return AimboxCombatMuzzleResolve.ResolveThirdPersonTracerFallback( actor, actor.AimForward );
		}

		return actor.EyePosition;
	}

	static bool IsLocalHuman( IAimboxCombatActor actor ) =>
		actor.IsHumanPlayer && actor is AimboxPlayerController { IsProxy: false };

	static void LogMissingOnce( string resourcePath )
	{
		if ( !_loggedMissing.Add( resourcePath ) )
			return;

		Log.Warning( $"[Aimbox SFX] Failed to play '{resourcePath}' — check Assets/sounds audio files are mounted." );
	}

	static string FireSoundForWeapon( AimboxWeaponDefinition weapon )
	{
		if ( weapon is null )
			return "";

		return weapon.Id switch
		{
			AimboxWeaponId.SpaghelliM4 => ShotgunFire,
			AimboxWeaponId.M4A1 or AimboxWeaponId.Mp5 or AimboxWeaponId.Usp or AimboxWeaponId.M700 => M4Fire,
			AimboxWeaponId.M9Bayonet or AimboxWeaponId.Trenchknife or AimboxWeaponId.Crowbar => KnifeLight,
			_ => ""
		};
	}

	static string ReloadSoundForWeapon( AimboxWeaponDefinition weapon )
	{
		if ( weapon is null )
			return "";

		return weapon.Id switch
		{
			AimboxWeaponId.SpaghelliM4 => ShotgunReload,
			AimboxWeaponId.M4A1 or AimboxWeaponId.Mp5 or AimboxWeaponId.Usp or AimboxWeaponId.M700 => M4Reload,
			_ => ""
		};
	}
}
