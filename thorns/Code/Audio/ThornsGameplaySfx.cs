using System;

namespace Sandbox;

/// <summary>First-person / local-owner gameplay stingers (paths are s&amp;box <c>.sound</c> under <c>Assets/</c>).</summary>
public static class ThornsGameplaySfx
{
	public const string AxeHit = "sounds/axe.sound";
	public const string PickaxeHit = "sounds/pickaxe.sound";

	/// <summary>Multiplier for <see cref="AxeHit"/> / <see cref="PickaxeHit"/> one-shots (harvest + tool melee contact). <c>axe.sound</c> / <c>pickaxe.sound</c> use halved authored <c>Volume</c> for ~50% quieter strikes.</summary>
	public const float ToolAxePickVolume = 0.5f;
	public const string MeleeMiss = "sounds/melee_miss.sound";
	public const string BuildMenuOrPlace = "sounds/build_menu_or_place.sound";
	public const string OpenBuild = "sounds/open_build.sound";
	public const string AirdropSpawn = "sounds/airdrop.sound";

	public const string ArmorEquip = "sounds/armor_equip.sound";
	public const string LevelUp = "sounds/level_up.sound";
	public const string SkillUpgrade = "sounds/skill_upgrade.sound";
	public const string Tame = "sounds/tame.sound";

	static double _lastToolStrikeDedupeTime;
	static string _lastToolStrikeDedupePath;

	public static float VolumeMultiplierForToolStrikePath( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return 1f;

		var p = resourcePath.Trim();
		if ( string.Equals( p, AxeHit, StringComparison.OrdinalIgnoreCase )
		     || string.Equals( p, PickaxeHit, StringComparison.OrdinalIgnoreCase ) )
			return ToolAxePickVolume;

		return 1f;
	}

	public static void PlayAtPawnEar( GameObject pawnRoot, string resourcePath, float volumeMultiplier = 1f )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		var path = resourcePath.Trim();
		var h = ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var ear, out _ )
			? Sound.Play( path, ear )
			: Sound.Play( path, pawnRoot.WorldPosition );
		if ( Math.Abs( volumeMultiplier - 1f ) > 0.0001f )
			h.Volume = Math.Clamp( volumeMultiplier, 0f, 4f );
	}

	/// <summary>
	/// Axe/pickaxe/primitive strike from harvest RPC and tool melee world-hit can fire the same frame — skip duplicate playback.
	/// </summary>
	public static void PlayToolStrikeContactDeduped( GameObject pawnRoot, string resourcePath, double dedupeWindowSeconds = 0.09 )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		var path = resourcePath.Trim();
		var now = Time.Now;
		if ( string.Equals( path, _lastToolStrikeDedupePath, StringComparison.Ordinal )
		     && now - _lastToolStrikeDedupeTime < dedupeWindowSeconds )
			return;

		_lastToolStrikeDedupeTime = now;
		_lastToolStrikeDedupePath = path;
		PlayAtPawnEar( pawnRoot, path, VolumeMultiplierForToolStrikePath( path ) );
	}

	public static void PlayMeleeMiss( GameObject pawnRoot ) => PlayAtPawnEar( pawnRoot, MeleeMiss );

	public static void PlayBuildMenuOrPlace( GameObject pawnRoot ) => PlayAtPawnEar( pawnRoot, BuildMenuOrPlace );

	public static void PlayOpenBuildAt( Vector3 worldEmit ) =>
		ThornsWorldSpatialSfx.PlayWorldOneShot( OpenBuild, worldEmit, ThornsSpatialSfxCategory.PlayerGunshot );

	public static void PlayOpenBuildAt( GameObject emitRoot )
	{
		if ( emitRoot is not null && emitRoot.IsValid() )
			PlayOpenBuildAt( emitRoot.WorldPosition );
	}

	/// <summary>World sting when a dynamic supply / airdrop lands (longer falloff than most one-shots).</summary>
	public static void PlayAirdropSpawnAt( Vector3 worldEmit ) =>
		ThornsWorldSpatialSfx.PlayWorldOneShot( AirdropSpawn, worldEmit, ThornsSpatialSfxCategory.PlayerGunshot, 1.25f );
}
