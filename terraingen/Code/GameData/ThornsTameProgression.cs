namespace Terraingen.GameData;

public enum ThornsTameStat : byte
{
	Strength,
	Defense,
	Stamina,
	Agility,
	Intelligence
}

/// <summary>Level curve and per-stat bonuses for tamed companions.</summary>
public static class ThornsTameProgression
{
	public const int XpPerKill = 40;
	public const int XpPerFeed = 12;
	public const int StatPointsPerLevel = 1;

	public const float StrengthDamagePerRank = 2f;
	public const float DefenseHealthPerRank = 5f;
	public const float StaminaHealthPerRank = 8f;
	public const float AgilitySpeedPerRank = 14f;
	public const float IntelligenceDetectionPerRank = 55f;

	public static int ExperienceForNextLevel( int level )
		=> Math.Max( 100, 200 + Math.Max( 1, level ) * 100 );

	public static bool TryParseStat( string key, out ThornsTameStat stat )
	{
		stat = default;
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		return key.Trim().ToLowerInvariant() switch
		{
			"strength" => Assign( ThornsTameStat.Strength, out stat ),
			"defense" => Assign( ThornsTameStat.Defense, out stat ),
			"stamina" => Assign( ThornsTameStat.Stamina, out stat ),
			"agility" => Assign( ThornsTameStat.Agility, out stat ),
			"intelligence" => Assign( ThornsTameStat.Intelligence, out stat ),
			_ => false
		};
	}

	public static string StatLabel( ThornsTameStat stat ) => stat switch
	{
		ThornsTameStat.Strength => "STRENGTH",
		ThornsTameStat.Defense => "DEFENSE",
		ThornsTameStat.Stamina => "STAMINA",
		ThornsTameStat.Agility => "AGILITY",
		ThornsTameStat.Intelligence => "INTELLIGENCE",
		_ => "STAT"
	};

	public static string StatKey( ThornsTameStat stat ) => stat switch
	{
		ThornsTameStat.Strength => "strength",
		ThornsTameStat.Defense => "defense",
		ThornsTameStat.Stamina => "stamina",
		ThornsTameStat.Agility => "agility",
		ThornsTameStat.Intelligence => "intelligence",
		_ => ""
	};

	static bool Assign( ThornsTameStat value, out ThornsTameStat stat )
	{
		stat = value;
		return true;
	}
}
