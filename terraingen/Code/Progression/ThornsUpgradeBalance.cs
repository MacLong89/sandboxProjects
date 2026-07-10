namespace Terraingen.GameData;

/// <summary>Upgrade point costs — 1 point earned per player level.</summary>
public static class ThornsUpgradeBalance
{
	public static int PointsPerLevel => 1;

	public static int NextPurchaseCost( ThornsSkillDefinition skill, int currentRank )
	{
		if ( skill is null )
			return int.MaxValue;

		// Scales with rank: base cost + current rank (legacy-style curve).
		return skill.BasePointCost + currentRank;
	}

	public static int TotalPointsForLevel( int level ) => Math.Max( 0, level ) * PointsPerLevel;
}
