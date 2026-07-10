namespace Sandbox;

/// <summary>Tagged diagnostics — only spawn + despawn/dying noise for now.</summary>
public static class ThornsWildlifeLog
{
	public static void Spawn( string species, Vector3 pos )
	{
		Log.Info( $"[Thorns] Wildlife spawned species={species} pos={pos}" );
	}

	public static void Despawn( string name )
	{
		Log.Info( $"[Thorns] Wildlife despawned name={name}" );
	}

	public static void Lod( string name, ThornsWildlifeLodTier tier, float distSq )
	{
		_ = name;
		_ = tier;
		_ = distSq;
	}

	public static void Transition( string name, ThornsWildlifeAiState from, ThornsWildlifeAiState to )
	{
		_ = name;
		_ = from;
		_ = to;
	}

	public static void Target( string name, string targetName )
	{
		_ = name;
		_ = targetName;
	}

	public static void Attack( string attackerName, string victimName, float dmg )
	{
		_ = attackerName;
		_ = victimName;
		_ = dmg;
	}

	public static void PlayerWeaponDamage( string victimName, string species, float amount, string kind, bool killingBlow )
	{
		_ = victimName;
		_ = species;
		_ = amount;
		_ = kind;
		_ = killingBlow;
	}
}
