namespace Sandbox;

public sealed record AimboxMatchMasteryXpEntry(
	AimboxWeaponId Weapon,
	int XpEarned,
	int StartingLevel,
	int EndingLevel );

public sealed class AimboxMatchSummary
{
	public string AccountId { get; init; }
	public AimboxGameMode Mode { get; init; }
	public bool Won { get; init; }
	public int Kills { get; init; }
	public int Deaths { get; init; }
	public int RankXpEarned { get; init; }
	public int StartingRank { get; init; }
	public int EndingRank { get; init; }
	public List<AimboxMatchMasteryXpEntry> MasteryXpEntries { get; init; } = [];
	public List<AimboxUnlock> Unlocks { get; init; } = [];
	public List<AimboxMedalId> Medals { get; init; } = [];
	public List<string> CompletedChallenges { get; init; } = [];

	public int TotalMasteryXpEarned
	{
		get
		{
			var total = 0;
			foreach ( var entry in MasteryXpEntries )
				total += entry.XpEarned;

			return total;
		}
	}

	public bool RankedUp => EndingRank > StartingRank;

	public IReadOnlyList<AimboxUnlock> PlayerUnlocks
	{
		get
		{
			var unlocks = new List<AimboxUnlock>();
			foreach ( var unlock in Unlocks )
			{
				if ( AimboxMatchSummaryFilters.IsPlayerUnlock( unlock ) )
					unlocks.Add( unlock );
			}

			return unlocks;
		}
	}

	public IReadOnlyList<AimboxUnlock> MasteryUnlocks
	{
		get
		{
			var unlocks = new List<AimboxUnlock>();
			foreach ( var unlock in Unlocks )
			{
				if ( AimboxMatchSummaryFilters.IsMasteryUnlock( unlock ) )
					unlocks.Add( unlock );
			}

			return unlocks;
		}
	}
}

public static class AimboxMatchSummaryFilters
{
	public static bool IsPlayerUnlock( AimboxUnlock unlock )
	{
		if ( unlock.Label.StartsWith( "Rank " ) )
			return true;

		if ( unlock.Label.Contains( "Mastery" ) )
			return false;

		if ( unlock.Label.Contains( ':' ) )
			return false;

		return true;
	}

	public static bool IsMasteryUnlock( AimboxUnlock unlock ) =>
		unlock.Label.Contains( "Mastery" ) || unlock.Label.Contains( ':' );
}
