namespace Terraingen.Progression;

using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>One fast daily goal — primary "come back tomorrow" retention hook.</summary>
public static class ThornsDailySurvivorContract
{
	const int ContractBonusXp = 85;

	public static void HostTick( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var contract = gameplay.HostPeekSurvivorContracts().Daily;
		if ( contract is null )
			return;

		EnsureCurrentDay( contract );
		if ( contract.Completed )
			return;

		if ( string.IsNullOrWhiteSpace( contract.GoalId ) )
			HostRollContract( gameplay, contract );

		if ( string.IsNullOrWhiteSpace( contract.GoalId ) )
			return;

		if ( gameplay.HostGetJournalGoal( contract.GoalId )?.State == ThornsGoalState.Completed )
		{
			contract.Completed = true;
			gameplay.HostGrantXp( ContractBonusXp );
			gameplay.PushMilestoneToastToOwner( "Daily survivor contract complete", ContractBonusXp );
			gameplay.HostPersistPlayerState();
			gameplay.HostNotifyContractsChanged();
		}
	}

	static void EnsureCurrentDay( ThornsDailyContractDto contract )
	{
		var day = GetUtcDayId( DateTime.UtcNow );
		if ( string.Equals( contract.DayId, day, StringComparison.Ordinal ) )
			return;

		contract.DayId = day;
		contract.GoalId = "";
		contract.Completed = false;
	}

	static void HostRollContract( ThornsPlayerGameplay gameplay, ThornsDailyContractDto contract )
	{
		contract.GoalId = ThornsSurvivorContractRoller.PickDailyGoal( gameplay );
		if ( string.IsNullOrWhiteSpace( contract.GoalId ) )
			return;

		var title = ThornsDefinitionRegistry.GetGoal( contract.GoalId )?.Title ?? "Survive";
		gameplay.PushClientToastToOwner( $"Daily contract: {title}", "info", 5f );
		gameplay.HostNotifyContractsChanged();
	}

	static string GetUtcDayId( DateTime utc ) => utc.ToString( "yyyy-MM-dd" );
}
