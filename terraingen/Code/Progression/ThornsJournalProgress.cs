namespace Terraingen.Progression;

using Terraingen.GameData;

/// <summary>Host-side journal tabs that are separate from Survivor Journey goals.</summary>
public static class ThornsJournalProgress
{
	public static bool HostTryRecordWorldEventFromToken( ThornsJournalSnapshotDto journal, string eventToken )
	{
		if ( !ThornsJournalEventCatalog.TryJournalEventIdForMilestoneToken( eventToken, out var journalEventId ) )
			return false;

		return HostTryRecordWorldEvent( journal, journalEventId );
	}

	public static bool HostTryRecordWorldEvent( ThornsJournalSnapshotDto journal, string journalEventId )
	{
		if ( journal is null || string.IsNullOrWhiteSpace( journalEventId ) )
			return false;

		journal.CompletedEventIds ??= new List<string>();
		if ( journal.CompletedEventIds.Any( id => string.Equals( id, journalEventId, StringComparison.OrdinalIgnoreCase ) ) )
			return false;

		journal.CompletedEventIds.Add( journalEventId );
		return true;
	}

	public static bool HostTryRecordAchievement( ThornsJournalSnapshotDto journal, string goalId )
	{
		if ( journal is null || string.IsNullOrWhiteSpace( goalId ) )
			return false;

		journal.UnlockedAchievementIds ??= new List<string>();
		if ( journal.UnlockedAchievementIds.Any( id => string.Equals( id, goalId, StringComparison.OrdinalIgnoreCase ) ) )
			return false;

		journal.UnlockedAchievementIds.Add( goalId );
		return true;
	}

	public static string AchievementDisplayName( string goalId )
	{
		var def = ThornsDefinitionRegistry.GetGoal( goalId );
		return def?.Title ?? goalId;
	}
}
