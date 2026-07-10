namespace Terraingen.Progression;

/// <summary>Single source of truth for player level XP thresholds.</summary>
public static class ThornsXpBalance
{
	public const int XpPerLevel = 300;

	public static int LevelFromTotalXp( int totalXp ) =>
		1 + Math.Max( 0, totalXp ) / XpPerLevel;

	public static int XpFloorForLevel( int level ) =>
		Math.Max( 0, Math.Max( 1, level ) - 1 ) * XpPerLevel;
}
