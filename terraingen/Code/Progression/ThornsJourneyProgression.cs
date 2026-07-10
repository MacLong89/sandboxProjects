namespace Terraingen.GameData;

using Terraingen.Buildings;
using Terraingen.NpcGuild;
using Terraingen.Player;

/// <summary>Survivor Journey chains: prerequisite unlocks, visibility, and HUD auto-pin.</summary>
public static class ThornsJourneyProgression
{
	public const float TownVisitRadiusInches = 900f;
	public const float TownVisitRadiusSq = TownVisitRadiusInches * TownVisitRadiusInches;
	static TimeSince _worldUnlockTick;

	public static string DisplayJournalText( ThornsJournalGoalDefinition def ) =>
		!string.IsNullOrWhiteSpace( def.JournalEntry )
			? def.JournalEntry
			: def.Description;

	public static void HostMigrateJournalSnapshot( ThornsJournalSnapshotDto journal )
	{
		if ( journal is null )
			return;

		ThornsDefinitionRegistry.EnsureInitialized();
		HostEnsureAllGoalsPresent( journal );
		HostEnsureAllDiscoveriesPresent( journal );
		SyncTaskRowsFromDefinitions( journal );
		HostAutoCompleteRetiredGoals( journal );
		HostNormalizeGoalStates( journal );
		journal.JourneyContentVersion = ThornsJournalSnapshotDto.CurrentJourneyContentVersion;

		var active = journal.Goals.Count( g => g.State == ThornsGoalState.Active );
		var pinnedDef = ThornsDefinitionRegistry.GetGoal( journal.HudPinnedGoalId );
		Log.Info(
			$"[Thorns Journey] Journal ready v{journal.JourneyContentVersion}: active={active}, pinned='{pinnedDef?.Title ?? journal.HudPinnedGoalId}' ({journal.HudPinnedGoalId})." );
	}

	public static bool NeedsJournalMigration( ThornsJournalSnapshotDto journal ) =>
		journal is null
		|| journal.JourneyContentVersion < ThornsJournalSnapshotDto.CurrentJourneyContentVersion;

	public static void HostEnsureAllGoalsPresent( ThornsJournalSnapshotDto journal )
	{
		if ( journal is null )
			return;

		journal.Goals ??= new List<ThornsJournalGoalProgressDto>();

		foreach ( var def in ThornsMilestoneDefinitions.All.OrderBy( g => g.SortOrder ) )
		{
			if ( journal.Goals.Any( g => string.Equals( g.GoalId, def.Id, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			journal.Goals.Add( CreateProgressRow( def ) );
		}
	}

	public static void HostEnsureAllDiscoveriesPresent( ThornsJournalSnapshotDto journal )
	{
		if ( journal is null )
			return;

		journal.Discoveries ??= new List<ThornsDiscoveryEntryDto>();

		foreach ( var template in ThornsDefinitionRegistry.DiscoveryTemplatesList )
		{
			if ( journal.Discoveries.Any( d => string.Equals( d.Id, template.Id, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			journal.Discoveries.Add( new ThornsDiscoveryEntryDto
			{
				Id = template.Id,
				Title = template.Title,
				Category = template.Category,
				IconPath = template.IconPath,
				Discovered = false
			} );
		}

		if ( string.IsNullOrWhiteSpace( journal.SelectedDiscoveryId ) )
			journal.SelectedDiscoveryId = journal.Discoveries.FirstOrDefault()?.Id ?? "";
	}

	public static void HostNormalizeGoalStates( ThornsJournalSnapshotDto journal )
	{
		if ( journal?.Goals is null )
			return;

		HostEnsureAllGoalsPresent( journal );

		foreach ( var def in ThornsMilestoneDefinitions.All.OrderBy( g => g.SortOrder ) )
		{
			var row = journal.Goals.FirstOrDefault( g =>
				string.Equals( g.GoalId, def.Id, StringComparison.OrdinalIgnoreCase ) );
			if ( row is null )
				continue;

			if ( row.State == ThornsGoalState.Completed )
				continue;

			if ( HostPrerequisitesMet( journal, def ) )
				row.State = ThornsGoalState.Active;
			else
				row.State = ThornsGoalState.Locked;
		}

		journal.HudPinnedGoalId = HostResolveHudPinnedGoalId( journal );
		EnsureSelectedGoalVisible( journal );
	}

	public static void HostOnGoalCompleted( ThornsJournalSnapshotDto journal, string completedGoalId )
	{
		if ( journal is null )
			return;

		HostNormalizeGoalStates( journal );

		if ( string.Equals( journal.HudPinnedGoalId, completedGoalId, StringComparison.OrdinalIgnoreCase )
		     || string.IsNullOrWhiteSpace( journal.HudPinnedGoalId ) )
			journal.HudPinnedGoalId = HostResolveHudPinnedGoalId( journal );

		EnsureSelectedGoalVisible( journal );
	}

	public static bool IsVisibleInJournal( ThornsJournalGoalDefinition def, ThornsJournalGoalProgressDto row )
	{
		if ( def is null || row is null )
			return false;

		if ( row.State != ThornsGoalState.Locked )
			return true;

		return !def.HideWhenLocked;
	}

	public static bool CanTrackProgress( ThornsJournalGoalProgressDto row ) =>
		row is not null && row.State == ThornsGoalState.Active;

	public static void HostTickWorldUnlocks( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( _worldUnlockTick < Terraingen.Core.ThornsHudTickRates.JourneyWorldUnlockSeconds )
			return;

		_worldUnlockTick = 0;
		TryNotifyTownVisit( gameplay );
		TryNotifyGuildOutpostDiscovery( gameplay );
		NotifySurvivalArmamentAcquired( gameplay );
	}

	/// <summary>Complete <c>goal_acquire_weapon</c> as soon as a hatchet, pick, or weapon enters the loadout.</summary>
	public static void NotifySurvivalArmamentAcquired( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		TryNotifyWeaponAcquired( gameplay );
	}

	/// <summary>True for weapons and starter stone gathering tools.</summary>
	public static bool IsSurvivalArmamentItem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		if ( def.ItemType == ThornsItemType.Weapon )
			return true;

		return string.Equals( itemId, "stone_hatchet", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( itemId, "stone_pickaxe", StringComparison.OrdinalIgnoreCase );
	}

	static void TryNotifyGuildOutpostDiscovery( ThornsPlayerGameplay gameplay )
	{
		var scene = gameplay?.GameObject?.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var pos = gameplay.GameObject.WorldPosition;
		const float discoverRadiusSq = 1100f * 1100f;

		foreach ( var core in scene.GetAllComponents<ThornsNpcGuildCore>() )
		{
			if ( core is null || !core.IsValid() || !core.Enabled )
				continue;

			if ( (pos - core.CenterWorld).LengthSquared > discoverRadiusSq )
				continue;

			gameplay.HostTryFireMilestoneEventOnce( "discover_guild_outpost" );
			return;
		}
	}

	static void TryNotifyTownVisit( ThornsPlayerGameplay gameplay )
	{
		var pos = gameplay.GameObject.WorldPosition;
		foreach ( var center in ThornsTownNodeRegistry.TownCenters )
		{
			if ( (pos - center).LengthSquared > TownVisitRadiusSq )
				continue;

			gameplay.HostNotifyMilestoneEvent( "visit_town" );
			return;
		}
	}

	static void TryNotifyWeaponAcquired( ThornsPlayerGameplay gameplay )
	{
		var goal = gameplay.HostGetJournalGoal( "goal_acquire_weapon" );
		if ( goal is null || goal.State != ThornsGoalState.Active )
			return;

		if ( !gameplay.HostHasSurvivalArmament() )
			return;

		gameplay.HostNotifyMilestoneEvent( "acquire_weapon" );
	}

	static void HostAutoCompleteRetiredGoals( ThornsJournalSnapshotDto journal )
	{
		if ( journal?.Goals is null )
			return;

		foreach ( var row in journal.Goals )
		{
			if ( row.State == ThornsGoalState.Completed )
				continue;

			if ( row.GoalId is "goal_open_tab" or "goal_sprint_shift" )
			{
				row.State = ThornsGoalState.Completed;
				foreach ( var task in row.Tasks )
					task.Current = task.Target;
				continue;
			}

			if ( string.Equals( row.GoalId, "goal_bare_hands_gather", StringComparison.OrdinalIgnoreCase ) )
			{
				var weapon = journal.Goals.FirstOrDefault( g =>
					string.Equals( g.GoalId, "goal_acquire_weapon", StringComparison.OrdinalIgnoreCase ) );
				var hatchet = journal.Goals.FirstOrDefault( g =>
					string.Equals( g.GoalId, "goal_craft_hatchet", StringComparison.OrdinalIgnoreCase ) );
				if ( weapon?.State == ThornsGoalState.Completed || hatchet?.State == ThornsGoalState.Completed )
				{
					row.State = ThornsGoalState.Completed;
					foreach ( var task in row.Tasks )
						task.Current = task.Target;
				}
			}
		}
	}

	static bool HostPrerequisitesMet( ThornsJournalSnapshotDto journal, ThornsJournalGoalDefinition def )
	{
		if ( !string.IsNullOrWhiteSpace( def.UnlockOnDiscoveryId ) )
		{
			var disc = journal.Discoveries?.FirstOrDefault( d =>
				string.Equals( d.Id, def.UnlockOnDiscoveryId, StringComparison.OrdinalIgnoreCase ) );
			if ( disc?.Discovered == true )
				return true;
		}

		if ( string.IsNullOrWhiteSpace( def.PrerequisiteGoalId ) )
			return true;

		var prereq = journal.Goals.FirstOrDefault( g =>
			string.Equals( g.GoalId, def.PrerequisiteGoalId, StringComparison.OrdinalIgnoreCase ) );
		return prereq?.State == ThornsGoalState.Completed;
	}

	public static void ClientMergeJournalSnapshot( ThornsJournalSnapshotDto journal )
	{
		if ( journal is null )
			return;

		ThornsDefinitionRegistry.EnsureInitialized();
		HostEnsureAllGoalsPresent( journal );
		HostEnsureAllDiscoveriesPresent( journal );
		SyncTaskRowsFromDefinitions( journal );
	}

	public static string HostResolveHudPinnedGoalId( ThornsJournalSnapshotDto journal )
	{
		if ( journal?.Goals is null or { Count: 0 } )
			return "";

		if ( !string.IsNullOrWhiteSpace( journal.HudPinnedGoalId ) )
		{
			var current = journal.Goals.FirstOrDefault( g =>
				string.Equals( g.GoalId, journal.HudPinnedGoalId, StringComparison.OrdinalIgnoreCase )
				&& g.State == ThornsGoalState.Active );
			if ( current is not null )
				return current.GoalId;
		}

		var autoPin = ThornsMilestoneDefinitions.All
			.Where( d => d.AutoPinUntilComplete )
			.OrderBy( d => d.SortOrder )
			.Select( d => journal.Goals.FirstOrDefault( g =>
				string.Equals( g.GoalId, d.Id, StringComparison.OrdinalIgnoreCase )
				&& g.State == ThornsGoalState.Active ) )
			.FirstOrDefault( g => g is not null );

		if ( autoPin is not null )
			return autoPin.GoalId;

		return journal.Goals.FirstOrDefault( g => g.State == ThornsGoalState.Active )?.GoalId ?? "";
	}

	static void EnsureSelectedGoalVisible( ThornsJournalSnapshotDto journal )
	{
		var selected = journal.Goals.FirstOrDefault( g =>
			string.Equals( g.GoalId, journal.SelectedGoalId, StringComparison.OrdinalIgnoreCase ) );
		var def = ThornsDefinitionRegistry.GetGoal( journal.SelectedGoalId );
		if ( selected is not null && def is not null && IsVisibleInJournal( def, selected ) )
			return;

		var fallback = journal.Goals
			.Where( g =>
			{
				var d = ThornsDefinitionRegistry.GetGoal( g.GoalId );
				return d is not null && IsVisibleInJournal( d, g );
			} )
			.OrderBy( g => ThornsDefinitionRegistry.GetGoal( g.GoalId )?.SortOrder ?? 999 )
			.FirstOrDefault();

		journal.SelectedGoalId = fallback?.GoalId ?? "";
	}

	static void SyncTaskRowsFromDefinitions( ThornsJournalSnapshotDto journal )
	{
		foreach ( var row in journal.Goals )
		{
			var def = ThornsDefinitionRegistry.GetGoal( row.GoalId );
			if ( def is null )
				continue;

			row.Tasks ??= new List<ThornsJournalTaskProgressDto>();
			foreach ( var taskDef in def.Tasks )
			{
				var task = row.Tasks.FirstOrDefault( t => t.TaskId == taskDef.Id );
				if ( task is null )
				{
					row.Tasks.Add( new ThornsJournalTaskProgressDto
					{
						TaskId = taskDef.Id,
						Current = 0,
						Target = taskDef.TargetCount
					} );
				}
				else
				{
					task.Target = taskDef.TargetCount;
				}
			}
		}
	}

	static ThornsJournalGoalProgressDto CreateProgressRow( ThornsJournalGoalDefinition def ) =>
		new()
		{
			GoalId = def.Id,
			State = ThornsGoalState.Locked,
			Tasks = def.Tasks.Select( t => new ThornsJournalTaskProgressDto
			{
				TaskId = t.Id,
				Current = 0,
				Target = t.TargetCount
			} ).ToList()
		};
}
