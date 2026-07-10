namespace Sandbox;

/// <summary>Canonical XP lump rewards (host applies via <see cref="ThornsVitals.AddXp"/> only).</summary>
public static class ThornsXpBalance
{
	public const int PvpKillPlayerReward = 750;

	/// <summary>Player character XP when landing the killing blow on wild creatures (see <see cref="ThornsHealth"/>).</summary>
	public const int WildlifeKillReward = 48;

	/// <summary>Multiplier for player XP and tame XP when the victim is boss wildlife (<see cref="ThornsWildlifeIdentity.IsBossWildlifeSync"/>).</summary>
	public const int BossWildlifeXpRewardMultiplier = 10;

	/// <summary>Player character XP when landing the killing blow on bandit NPCs.</summary>
	public const int BanditKillReward = 72;

	/// <summary>Granted to the attacking <see cref="ThornsWildlifeIdentity"/> tame when it lands a lethal blow on a creature (not players).</summary>
	public const int TameKillCreatureReward = 140;

	/// <summary>Host grants after a validated harvest strike (activities / subsystem hook).</summary>
	public const int HarvestStrikeActivity = 4;

	/// <summary>Unspent upgrade points granted per character level crossed when gaining XP.</summary>
	public const int UpgradePointsPerLevelGained = 1;
}
