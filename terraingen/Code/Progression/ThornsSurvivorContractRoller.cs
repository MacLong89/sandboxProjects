namespace Terraingen.Progression;

using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Shared journal-goal selection for daily / weekly survivor contracts.</summary>
public static class ThornsSurvivorContractRoller
{
	public static List<string> PickActiveGoals( ThornsPlayerGameplay gameplay, int count, bool excludeWorldCategory = true )
	{
		if ( gameplay is null || count <= 0 )
			return new List<string>();

		var candidates = ThornsMilestoneDefinitions.All
			.Where( d => !excludeWorldCategory || d.JourneyCategory is not ThornsJourneyCategory.World )
			.Where( d => gameplay.HostGetJournalGoal( d.Id )?.State == ThornsGoalState.Active )
			.OrderBy( d => d.SortOrder )
			.Select( d => d.Id )
			.ToList();

		if ( candidates.Count == 0 )
		{
			candidates = ThornsMilestoneDefinitions.All
				.OrderBy( d => d.SortOrder )
				.Select( d => d.Id )
				.Take( count )
				.ToList();
		}

		return candidates.Take( count ).ToList();
	}

	public static string PickDailyGoal( ThornsPlayerGameplay gameplay )
	{
		var quick = ThornsMilestoneDefinitions.All
			.Where( d => d.JourneyCategory is not ThornsJourneyCategory.World )
			.Where( d => gameplay.HostGetJournalGoal( d.Id )?.State == ThornsGoalState.Active )
			.Where( d => d.MilestoneType is ThornsMilestoneType.Event or ThornsMilestoneType.Craft or ThornsMilestoneType.Collect )
			.Where( d => d.TargetCount <= 15 )
			.OrderBy( d => d.SortOrder )
			.Select( d => d.Id )
			.FirstOrDefault();

		if ( !string.IsNullOrWhiteSpace( quick ) )
			return quick;

		return PickActiveGoals( gameplay, 1 ).FirstOrDefault() ?? "";
	}
}
