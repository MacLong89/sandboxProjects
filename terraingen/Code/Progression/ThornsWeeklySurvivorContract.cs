namespace Terraingen.Progression;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI;

/// <summary>Lightweight weekly survivor contract — 3 goals, bonus XP on completion.</summary>
public static class ThornsWeeklySurvivorContract
{
	const int ContractGoalCount = 3;
	const int ContractBonusXp = 250;

	public static void HostTick( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var contract = gameplay.HostPeekSurvivorContracts().Weekly;
		if ( contract is null )
			return;

		EnsureCurrentWeek( contract );
		if ( contract.Completed )
			return;

		if ( contract.GoalIds is null or { Count: 0 } )
			HostRollContract( gameplay, contract );

		if ( contract.GoalIds.All( id => gameplay.HostGetJournalGoal( id )?.State == ThornsGoalState.Completed ) )
		{
			contract.Completed = true;
			gameplay.HostGrantXp( ContractBonusXp );
			gameplay.PushMilestoneToastToOwner( "Weekly survivor contract complete", ContractBonusXp );
			gameplay.HostPersistPlayerState();
			gameplay.HostNotifyContractsChanged();
		}
	}

	static void EnsureCurrentWeek( ThornsWeeklyContractDto contract )
	{
		var week = GetIsoWeekId( DateTime.UtcNow );
		if ( string.Equals( contract.WeekId, week, StringComparison.Ordinal ) )
			return;

		contract.WeekId = week;
		contract.GoalIds = new List<string>();
		contract.Completed = false;
	}

	static void HostRollContract( ThornsPlayerGameplay gameplay, ThornsWeeklyContractDto contract )
	{
		contract.GoalIds = ThornsSurvivorContractRoller.PickActiveGoals( gameplay, ContractGoalCount );
		gameplay.PushClientToastToOwner( "Weekly survivor contract updated — check Journal.", "info", 4f );
		gameplay.HostNotifyContractsChanged();
	}

	static string GetIsoWeekId( DateTime utc ) =>
		$"{utc.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear( utc ):00}";
}
