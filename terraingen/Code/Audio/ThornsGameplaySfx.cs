namespace Sandbox;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terraingen.Audio;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Gameplay sound event paths and small playback helpers.</summary>
public static class ThornsGameplaySfx
{
	public const string AxeHit = "sounds/axe.sound";
	public const string PickaxeHit = "sounds/pickaxe.sound";
	public const string MeleeMiss = "sounds/melee_miss.sound";
	public const string BuildMenuOrPlace = "sounds/build_menu_or_place.sound";
	public const string PlacementError = "sounds/placement_error.sound";
	public const string Demolish = "sounds/demolish.sound";
	public const string OpenBuild = "sounds/open_build.sound";
	public const string ContainerOpen = OpenBuild;
	public const string AirdropSpawn = "sounds/airdrop.sound";
	public const string ArmorEquip = "sounds/armor_equip.sound";
	public const string LevelUp = "sounds/level_up.sound";
	public const string SkillUpgrade = "sounds/skill_upgrade.sound";
	public const string Tame = "sounds/tame.sound";
	public const string M4Fire = "sounds/m4_shot.sound";
	public const string ShotgunFire = "sounds/shotgun_shot.sound";
	public const string M4Reload = "sounds/m4_reload.sound";
	public const string ShotgunReload = "sounds/shotgun_reload.sound";
	public const string KnifeLight = "sounds/knife_stab_light.sound";
	public const string KnifeHeavy = "sounds/knife_stab_heavy.sound";
	public const string BowDraw = "sounds/bow_draw.sound";
	public const string BowShoot = "sounds/bow_shoot.sound";

	const float ToolStrikeVolume = 0.5f;
	const float PawnCombatLocalSpacialBlend = 0.18f;
	const float PawnBuildLocalSpacialBlend = 0.2f;
	static readonly Vector3 PawnUpperBodyOffset = Vector3.Up * 56f;

	static double _lastToolStrikeTime;
	static string _lastToolStrikePath;
	static readonly HashSet<string> WarmedPaths = new( StringComparer.OrdinalIgnoreCase );

	static readonly string[] GatherCombatWarmPaths =
	[
		AxeHit,
		PickaxeHit,
		MeleeMiss
	];

	/// <summary>Preload gather/melee one-shots; safe to call repeatedly until each path succeeds.</summary>
	public static void WarmHarvestToolSounds()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		foreach ( var path in GatherCombatWarmPaths )
			WarmSoundSilently( path );
	}

	static bool WarmSoundSilently( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return false;

		var path = resourcePath.Trim();
		if ( WarmedPaths.Contains( path ) )
			return true;

		var h = Sound.Play( path, Vector3.Zero );
		if ( !h.IsValid() )
			return false;

		WarmedPaths.Add( path );
		h.Volume = 0f;
		h.Stop( 0f );
		return true;
	}

	public static void PlayAtPawnEar( GameObject pawnRoot, string resourcePath, float volumeMultiplier = 1f )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		if ( Application.IsDedicatedServer || Application.IsHeadless )
			return;

		var path = resourcePath.Trim();
		WarmHarvestToolSounds();

		var h = TryPlayAtPawnEarOnce( pawnRoot, path );
		if ( !h.IsValid() )
		{
			WarmedPaths.Remove( path );
			if ( WarmSoundSilently( path ) )
				h = TryPlayAtPawnEarOnce( pawnRoot, path );
		}

		if ( h.IsValid() )
			h.Volume = Math.Clamp( volumeMultiplier, 0f, 4f );
		else if ( ThornsGatherSalvage.Debug )
			Log.Warning( $"[Thorns Sfx] PlayAtPawnEar failed: '{path}'." );
	}

	static SoundHandle TryPlayAtPawnEarOnce( GameObject pawnRoot, string path )
	{
		var h = ThornsLocalPlayer.TryGetAuthoritativeEye( pawnRoot, out var ear, out _ )
			? Sound.Play( path, ear )
			: Sound.Play( path, pawnRoot.WorldPosition + Vector3.Up * 40f );

		if ( h.IsValid() )
			h.SpacialBlend = 0f;

		return h;
	}

	public static void PlayToolStrikeContactDeduped( GameObject pawnRoot, string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		var path = resourcePath.Trim();
		var now = Time.Now;
		if ( string.Equals( path, _lastToolStrikePath, StringComparison.Ordinal ) && now - _lastToolStrikeTime < 0.09 )
			return;

		_lastToolStrikePath = path;
		_lastToolStrikeTime = now;

		if ( ThornsLocalPlayer.IsLocalConnectionPlayerRoot( pawnRoot ) )
		{
			PlayAtPawnEar( pawnRoot, path, ToolStrikeVolume );
			return;
		}

		PlayNetworkedPawnSound(
			pawnRoot,
			path,
			ThornsSpatialSfxCategory.PlayerToolStrike,
			ToolStrikeVolume,
			Vector3.Up * 28f,
			PawnCombatLocalSpacialBlend );
	}

	public static void PlayNetworkedPawnSound(
		GameObject pawnRoot,
		string resourcePath,
		ThornsSpatialSfxCategory category,
		float volume = 1f,
		Vector3 localOffset = default,
		float localOwnerSpacialBlend = PawnCombatLocalSpacialBlend )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		WarmHarvestToolSounds();

		if ( ThornsLocalPlayer.IsLocalConnectionPlayerRoot( pawnRoot ) )
		{
			PlayAtPawnEar( pawnRoot, resourcePath, volume );
			return;
		}

		var offset = localOffset == default ? PawnUpperBodyOffset : localOffset;
		var interest = ThornsWorldSpatialSfx.WorldPointFromLocalOffset( pawnRoot, offset );
		var path = resourcePath.Trim();

		if ( !Networking.IsActive )
		{
			ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
				pawnRoot,
				path,
				category,
				volume,
				offset,
				localOwnerSpacialBlend );
			return;
		}

		var inst = ThornsAudioWorldService.Instance;
		if ( inst is null || !inst.IsValid() )
		{
			ThornsAudioWorldService.EnsureForScene( Game.ActiveScene );
			inst = ThornsAudioWorldService.Instance;
		}

		if ( inst is null || !inst.IsValid() )
		{
			ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
				pawnRoot,
				path,
				category,
				volume,
				offset,
				localOwnerSpacialBlend );
			return;
		}

		ThornsAudioWorldService.BroadcastFollowing(
			pawnRoot.Id,
			path,
			offset,
			category,
			volume,
			interest,
			localOwnerSpacialBlend );
	}

	public static void PlayNetworkedCombatSound( GameObject pawnRoot, string resourcePath, ThornsSpatialSfxCategory category, float volume = 1f ) =>
		PlayNetworkedPawnSound( pawnRoot, resourcePath, category, volume, PawnUpperBodyOffset, PawnCombatLocalSpacialBlend );

	public static void PlayNetworkedWorldInteraction( Vector3 worldEmit, string resourcePath, float volume = 1f ) =>
		ThornsAudioWorldService.BroadcastWorldOneShot( resourcePath, worldEmit, ThornsSpatialSfxCategory.PlayerInteraction, volume );

	public static void PlayToolStrikeForActiveItem( GameObject pawnRoot, string activeItemId )
	{
		var path = ToolStrikeSoundForItem( activeItemId );
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		PlayToolStrikeContactDeduped( pawnRoot, path );
	}

	public static string SalvageStrikeSoundForKind( ThornsGatherSalvage.SalvageTargetKind kind ) =>
		kind switch
		{
			ThornsGatherSalvage.SalvageTargetKind.Tree => AxeHit,
			ThornsGatherSalvage.SalvageTargetKind.Stone => PickaxeHit,
			_ => ""
		};

	/// <summary>Axe / pickaxe contact SFX for fist-harvest strikes (spatial, host-broadcast).</summary>
	public static void PlaySalvageStrikeSfx( GameObject pawnRoot, ThornsGatherSalvage.SalvageTargetKind kind )
	{
		var path = SalvageStrikeSoundForKind( kind );
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		PlayToolStrikeContactDeduped( pawnRoot, path );
	}

	public static void PlayMeleeMiss( GameObject pawnRoot ) =>
		PlayNetworkedCombatSound( pawnRoot, MeleeMiss, ThornsSpatialSfxCategory.PlayerMelee );

	/// <summary>Confirmed bare-hand / tool-melee body hit (not whiffs or gather strikes).</summary>
	public static void PlayMeleeContact( GameObject pawnRoot ) =>
		PlayNetworkedCombatSound( pawnRoot, KnifeLight, ThornsSpatialSfxCategory.PlayerMelee, 0.55f );

	const float BuildPlacementRepeatDelaySeconds = 0.09f;

	public static void PlayBuildMenuOrPlace( GameObject pawnRoot )
	{
		PlayBuildMenuOrPlaceImpulse( pawnRoot );
		_ = PlayBuildMenuOrPlaceSecondImpulseAsync( pawnRoot );
	}

	static void PlayBuildMenuOrPlaceImpulse( GameObject pawnRoot ) =>
		PlayNetworkedPawnSound( pawnRoot, BuildMenuOrPlace, ThornsSpatialSfxCategory.PlayerBuild, 1f, Vector3.Up * 40f, PawnBuildLocalSpacialBlend );

	static async Task PlayBuildMenuOrPlaceSecondImpulseAsync( GameObject pawnRoot )
	{
		await Task.Delay( TimeSpan.FromSeconds( BuildPlacementRepeatDelaySeconds ) );
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		PlayBuildMenuOrPlaceImpulse( pawnRoot );
	}

	public static void PlayPlacementError( GameObject pawnRoot ) =>
		PlayNetworkedPawnSound( pawnRoot, PlacementError, ThornsSpatialSfxCategory.PlayerBuild, 0.9f, Vector3.Up * 40f, PawnBuildLocalSpacialBlend );

	public static void PlayDemolish( GameObject pawnRoot ) =>
		PlayNetworkedPawnSound( pawnRoot, Demolish, ThornsSpatialSfxCategory.PlayerBuild, 1f, Vector3.Up * 40f, PawnBuildLocalSpacialBlend );

	public static void PlayOpenBuildAt( Vector3 worldEmit ) =>
		PlayNetworkedWorldInteraction( worldEmit, OpenBuild );

	public static void PlayAirdropSpawnAt( Vector3 worldEmit ) =>
		ThornsAudioWorldService.BroadcastWorldOneShot( AirdropSpawn, worldEmit, ThornsSpatialSfxCategory.PlayerGunshot, 1.25f );

	public static string ToolStrikeSoundForItem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) || !ThornsItemRegistry.TryGet( itemId.Trim(), out var def ) )
			return "";

		return def.HarvestToolKind switch
		{
			ThornsHarvestToolKind.Axe => AxeHit,
			ThornsHarvestToolKind.Pickaxe => PickaxeHit,
			_ => ""
		};
	}

	public static void PlayBowDraw( GameObject pawnRoot ) =>
		PlayNetworkedPawnSound( pawnRoot, BowDraw, ThornsSpatialSfxCategory.PlayerInteraction, 0.9f, PawnUpperBodyOffset, PawnCombatLocalSpacialBlend );

	public static string FireSoundForCombatId( string combatId )
	{
		var id = combatId?.Trim() ?? "";
		if ( string.Equals( id, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return ShotgunFire;
		if ( string.Equals( id, "m4", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "mp5", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "usp", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "sniper", StringComparison.OrdinalIgnoreCase ) )
			return M4Fire;
		if ( string.Equals( id, "bow", StringComparison.OrdinalIgnoreCase ) )
			return BowShoot;
		if ( string.Equals( id, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return KnifeLight;
		if ( ThornsFpToolCombat.IsToolMeleeCombatId( id ) )
			return "";

		return "";
	}

	public static string ReloadSoundForCombatId( string combatId )
	{
		var id = combatId?.Trim() ?? "";
		if ( string.Equals( id, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return ShotgunReload;

		if ( string.Equals( id, "m4", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "mp5", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "usp", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( id, "sniper", StringComparison.OrdinalIgnoreCase ) )
			return M4Reload;

		return "";
	}
}
