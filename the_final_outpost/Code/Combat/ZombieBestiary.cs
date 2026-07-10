namespace FinalOutpost;

/// <summary>Tracks which zombie types the player has encountered and exposes catalog helpers.</summary>
public static class ZombieBestiary
{
	public static void MarkEncountered( SaveData save, ZombieKind kind )
	{
		if ( save is null ) return;

		save.DiscoveredZombies ??= new List<string>();
		var key = kind.ToString();
		if ( !save.DiscoveredZombies.Contains( key ) )
			save.DiscoveredZombies.Add( key );
	}

	public static void RecordKill( SaveData save, ZombieKind kind )
	{
		if ( save is null ) return;

		MarkEncountered( save, kind );

		save.ZombieKills ??= new Dictionary<string, int>();
		var key = kind.ToString();
		save.ZombieKills.TryGetValue( key, out var n );
		save.ZombieKills[key] = n + 1;
	}

	public static bool IsDiscovered( SaveData save, ZombieKind kind ) =>
		save?.DiscoveredZombies?.Contains( kind.ToString() ) ?? false;

	public static int DiscoveredCount( SaveData save ) =>
		save?.DiscoveredZombies?.Count ?? 0;

	public static int KillCount( SaveData save, ZombieKind kind ) =>
		save?.ZombieKills is not null && save.ZombieKills.TryGetValue( kind.ToString(), out var n ) ? n : 0;

	public static float HpAtNight( ZombieTypeDef def, int night ) =>
		GameConstants.ZombieHp( night, def.HpMult );

	public static float DamageAtNight( ZombieTypeDef def, int night ) =>
		(GameConstants.ZombieBaseDamage + night * GameConstants.ZombieDamagePerNight) * def.DamageMult;

	public static float SpeedAtNight( ZombieTypeDef def, int night ) =>
		(GameConstants.ZombieBaseSpeed + night * GameConstants.ZombieSpeedPerNight) * def.SpeedMult;

	public static IEnumerable<string> AbilityLines( ZombieTypeDef def )
	{
		if ( def.CanJumpWalls )
			yield return "Vaults walls";

		if ( def.BeeLinesCore )
			yield return "Charges command post";

		if ( def.CoreExplosionDamage > 0f )
			yield return $"{(int)def.CoreExplosionDamage} core explosion";

		if ( def.DamageTakenMult < 0.999f )
			yield return $"{(int)MathF.Round( (1f - def.DamageTakenMult) * 100f )}% bullet resist";

		if ( def.SplitCount > 0 )
			yield return $"Splits into {def.SplitCount} on death";

		if ( def.Scale >= 1.35f )
			yield return "Large frame";
		else if ( def.Scale <= 0.65f )
			yield return "Small frame";
	}

	public static string TintStyle( Color tint )
	{
		var r = (int)(tint.r * 255f);
		var g = (int)(tint.g * 255f);
		var b = (int)(tint.b * 255f);
		return $"background-color: rgb({r}, {g}, {b});";
	}

	/// <summary>Backfill discovery for saves that predated the bestiary.</summary>
	public static void BackfillFromProgress( SaveData save )
	{
		if ( save is null ) return;

		var progress = Math.Max( save.CurrentNight, save.BestNight );
		foreach ( var def in ZombieCatalog.All )
		{
			if ( def.IntroNight <= progress )
				MarkEncountered( save, def.Kind );
		}
	}
}
