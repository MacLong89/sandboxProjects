namespace Terraingen.Player;

using Sandbox;
using Sandbox.Network;
using Terraingen.Combat;
using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.GameData;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Journal, milestones, XP, and discoveries (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	void HostRebuildJournal()
	{
		_journal = new ThornsJournalSnapshotDto { ActiveSection = ThornsJournalSection.Goals };
		foreach ( var def in ThornsMilestoneDefinitions.All.OrderBy( g => g.SortOrder ) )
		{
			_journal.Goals.Add( new ThornsJournalGoalProgressDto
			{
				GoalId = def.Id,
				State = ThornsGoalState.Locked,
				Tasks = def.Tasks.Select( t => new ThornsJournalTaskProgressDto
				{
					TaskId = t.Id,
					Current = 0,
					Target = t.TargetCount
				} ).ToList()
			} );
		}

		_journal.Discoveries = ThornsDefinitionRegistry.DiscoveryTemplatesList
			.Select( d => new ThornsDiscoveryEntryDto
			{
				Id = d.Id,
				Title = d.Title,
				Category = d.Category,
				IconPath = d.IconPath,
				Discovered = false
			} )
			.ToList();

		_journal.SelectedDiscoveryId = _journal.Discoveries.FirstOrDefault()?.Id ?? "";
		HostSyncDiscoveriesFromInventory();
		ThornsJourneyProgression.HostMigrateJournalSnapshot( _journal );
	}

	public void HostRefreshJourneyJournal()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		ThornsJourneyProgression.HostMigrateJournalSnapshot( _journal );
		PushJournalToOwner();
	}

	public bool HostHasSurvivalArmament()
	{
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
		{
			if ( HostSlotIsArmament( _inventory.GetSlot( ThornsContainerKind.Inventory, i ).ItemId ) )
				return true;
		}

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
		{
			if ( HostSlotIsArmament( _inventory.GetSlot( ThornsContainerKind.Hotbar, i ).ItemId ) )
				return true;
		}

		return false;
	}

	static bool HostSlotIsArmament( string itemId )
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

	public void HostMarkDiscovery( string discoveryId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( discoveryId ) )
			return;

		var entry = _journal.Discoveries.FirstOrDefault( d =>
			string.Equals( d.Id, discoveryId, StringComparison.OrdinalIgnoreCase ) );
		if ( entry is null || entry.Discovered )
			return;

		entry.Discovered = true;
		ThornsJourneyProgression.HostNormalizeGoalStates( _journal );
		PushJournalToOwner();
		HostPersistPlayerState();
	}

	public void HostSyncDiscoveriesFromInventory()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var changed = false;
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
			changed |= TryMarkItemDiscovery( _inventory.GetSlot( ThornsContainerKind.Inventory, i ).ItemId );
		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
			changed |= TryMarkItemDiscovery( _inventory.GetSlot( ThornsContainerKind.Hotbar, i ).ItemId );
		foreach ( var kind in new[] { ThornsContainerKind.Head, ThornsContainerKind.Chest, ThornsContainerKind.Legs } )
			changed |= TryMarkItemDiscovery( _inventory.GetSlot( kind, 0 ).ItemId );

		if ( changed )
			PushJournalToOwner();
	}

	bool TryMarkItemDiscovery( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		var id = ThornsDefinitionRegistry.DiscoveryIdForItem( itemId );
		var entry = _journal.Discoveries.FirstOrDefault( d =>
			string.Equals( d.Id, id, StringComparison.OrdinalIgnoreCase ) );
		if ( entry is null || entry.Discovered )
			return false;

		entry.Discovered = true;
		return true;
	}

	public ThornsJournalGoalProgressDto HostGetJournalGoal( string goalId ) =>
		HostFindJournalGoal( goalId );

	public ThornsJournalSnapshotDto HostPeekJournalSnapshot() => _journal;

	public void HostRecordJournalWorldEvent( string journalEventId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !ThornsJournalProgress.HostTryRecordWorldEvent( _journal, journalEventId ) )
			return;

		PushJournalToOwner();
	}

	static ThornsJournalGoalProgressDto HostFindJournalGoal( ThornsJournalSnapshotDto journal, string goalId )
	{
		if ( journal?.Goals is null || string.IsNullOrWhiteSpace( goalId ) )
			return null;

		return journal.Goals.FirstOrDefault( g =>
			string.Equals( g.GoalId, goalId, StringComparison.OrdinalIgnoreCase ) );
	}

	ThornsJournalGoalProgressDto HostFindJournalGoal( string goalId ) =>
		HostFindJournalGoal( _journal, goalId );

	public void HostAdvanceMilestone( string goalId, int amount = 1 )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0 )
			return;

		var goal = HostFindJournalGoal( goalId );
		if ( goal is null || !ThornsJourneyProgression.CanTrackProgress( goal ) )
			return;

		var task = goal.Tasks.FirstOrDefault( t => t.TaskId == "progress" );
		if ( task is null )
			return;

		task.Current = Math.Min( task.Target, task.Current + amount );
		TryCompleteMilestone( goal );
		PushJournalToOwner();
	}

	public void HostCompleteJournalTask( string goalId, string taskId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( goalId ) || string.IsNullOrWhiteSpace( taskId ) )
			return;

		var goal = HostFindJournalGoal( goalId );
		if ( goal is null || !ThornsJourneyProgression.CanTrackProgress( goal ) )
			return;

		var task = goal.Tasks.FirstOrDefault( t => string.Equals( t.TaskId, taskId, StringComparison.OrdinalIgnoreCase ) );
		if ( task is null || task.Current >= task.Target )
			return;

		task.Current = task.Target;
		TryCompleteMilestone( goal );
		PushJournalToOwner();
	}

	public void RequestCompleteJournalTask( string goalId, string taskId )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( goalId ) || string.IsNullOrWhiteSpace( taskId ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCompleteJournalTask( goalId, taskId );
		else
			HostCompleteJournalTask( goalId, taskId );
	}

	[Rpc.Host]
	void RpcCompleteJournalTask( string goalId, string taskId )
	{
		if ( !ValidateCaller() )
			return;

		HostCompleteJournalTask( goalId, taskId );
	}

	public void HostSetMilestoneProgress( string goalId, int current )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var goal = HostFindJournalGoal( goalId );
		if ( goal is null || !ThornsJourneyProgression.CanTrackProgress( goal ) )
			return;

		var task = goal.Tasks.FirstOrDefault( t => t.TaskId == "progress" );
		if ( task is null )
			return;

		task.Current = Math.Min( task.Target, Math.Max( 0, current ) );
		TryCompleteMilestone( goal );
		PushJournalToOwner();
	}

	/// <summary>UI input milestones that cannot be inferred on the host (Tab opened, sprint held).</summary>
	public void RequestMilestoneEvent( string eventToken )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( eventToken ) )
			return;

		if ( !ClientRequestableOneShotMilestoneEvents.Contains( eventToken ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcRequestOneShotMilestoneEvent( eventToken );
		else
			HostTryFireMilestoneEventOnce( eventToken );
	}

	[Rpc.Host]
	void RpcRequestOneShotMilestoneEvent( string eventToken )
	{
		if ( !ValidateCaller() )
			return;

		if ( !ClientRequestableOneShotMilestoneEvents.Contains( eventToken ?? "" ) )
			return;

		HostTryFireMilestoneEventOnce( eventToken );
	}

	public bool HostTryFireMilestoneEventOnce( string eventToken )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( eventToken ) )
			return false;

		if ( !_hostConsumedMilestoneEvents.Add( eventToken ) )
			return false;

		ThornsMilestoneTracker.OnEvent( this, eventToken );
		if ( ThornsJournalProgress.HostTryRecordWorldEventFromToken( _journal, eventToken ) )
			PushJournalToOwner();
		return true;
	}

	public void HostNotifyMilestoneEvent( string eventToken )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( eventToken ) )
			return;

		ThornsMilestoneTracker.OnEvent( this, eventToken );
		if ( ThornsJournalProgress.HostTryRecordWorldEventFromToken( _journal, eventToken ) )
			PushJournalToOwner();
	}

	public void HostNotifyStructurePlaced( string placeableId, int count = 1 ) =>
		ThornsMilestoneTracker.OnBuilt( this, placeableId, count );

	public void HostNotifyContainerLootTaken( string containerKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( containerKey ) )
			return;

		if ( containerKey.StartsWith( "air:", StringComparison.Ordinal ) )
			return;

		if ( containerKey.StartsWith( "death:", StringComparison.Ordinal )
		     || containerKey.StartsWith( "struct:", StringComparison.Ordinal ) )
		{
			HostTryFireMilestoneEventOnce( "loot_crate" );
			return;
		}

		if ( containerKey.StartsWith( "furn:", StringComparison.Ordinal ) )
		{
			var service = Terraingen.World.ThornsWorldLootContainerService.Instance;
			if ( service is not null && service.TryGet( containerKey, out var record )
			     && IsMilitaryLootTable( record.LootTable ) )
				HostTryFireMilestoneEventOnce( "loot_military" );
			else
				HostTryFireMilestoneEventOnce( "loot_crate" );
		}
	}

	static bool IsMilitaryLootTable( string lootTable )
	{
		if ( string.IsNullOrWhiteSpace( lootTable ) )
			return false;

		var table = lootTable.Trim().ToLowerInvariant();
		return table is "military" or "weapons" or "ammo" or "armor"
		       || table.StartsWith( "military_", StringComparison.Ordinal );
	}

	void TryCompleteMilestone( ThornsJournalGoalProgressDto goal )
	{
		if ( goal.State == ThornsGoalState.Completed )
			return;

		if ( !goal.Tasks.All( t => t.Current >= t.Target ) )
			return;

		goal.State = ThornsGoalState.Completed;
		var def = ThornsDefinitionRegistry.GetGoal( goal.GoalId );
		if ( def is null )
			return;

		ThornsJourneyProgression.HostOnGoalCompleted( _journal, goal.GoalId );
		ThornsJourneyWaypointSync.HostClearGoalWaypointForAccount( AccountKey );
		ThornsJournalProgress.HostTryRecordAchievement( _journal, goal.GoalId );

		HostGrantXp( def.XpReward );
		PushJournalToOwner();
		PushMilestoneToastToOwner( def.Title, def.XpReward );
		NotifyGoalCompletionGuidance( goal.GoalId );
		ThornsMenuTabUnlock.NotifyMilestoneCompleted();
		HostPersistPlayerState();
	}

	void NotifyGoalCompletionGuidance( string goalId )
	{
		if ( string.Equals( goalId, "goal_discover_guild_outpost", StringComparison.OrdinalIgnoreCase ) )
		{
			PushClientToastToOwner(
				"Rival factions compete for territory — open Tab → Guild to track them.",
				"info",
				6f );
			return;
		}

		if ( string.Equals( goalId, "goal_place_workbench", StringComparison.OrdinalIgnoreCase ) )
		{
			PushClientToastToOwner(
				"Place a research station later to climb the tech ladder. Server Victory Paths are in the Guild tab.",
				"info",
				6f );
		}
	}

	public void PushClientToastToOwner( string message, string kind = "info", float seconds = 4f )
	{
		if ( IsLocalPlayer() )
			ThornsNotificationBus.Push( message, kind, seconds );
		else if ( Networking.IsActive )
			RpcOwnerToast( message, kind, seconds );
	}

	[Rpc.Owner]
	void RpcOwnerToast( string message, string kind, float seconds )
	{
		ThornsNotificationBus.Push( message, kind, seconds );
	}

	public void PushLevelUpMomentToOwner( int level )
	{
		if ( IsLocalPlayer() )
			ThornsLevelUpMomentBus.Show( level );
		else if ( Networking.IsActive )
			RpcOwnerLevelUpMoment( level );
	}

	[Rpc.Owner]
	void RpcOwnerLevelUpMoment( int level )
	{
		ThornsLevelUpMomentBus.Show( level );
	}

	public void PushCraftCompleteToOwner( string itemDisplayName )
	{
		if ( string.IsNullOrWhiteSpace( itemDisplayName ) )
			return;

		if ( IsLocalPlayer() )
			NotifyCraftCompleteLocal( itemDisplayName );
		else if ( Networking.IsActive )
			RpcOwnerCraftComplete( itemDisplayName );
	}

	[Rpc.Owner]
	void RpcOwnerCraftComplete( string itemDisplayName )
	{
		NotifyCraftCompleteLocal( itemDisplayName );
	}

	static void NotifyCraftCompleteLocal( string itemDisplayName )
	{
		ThornsGameplaySfx.PlayAtPawnEar( ThornsPlayerGameplay.Local?.GameObject, ThornsGameplaySfx.BuildMenuOrPlace, 0.85f );
		ThornsNotificationBus.Push( $"Crafted {itemDisplayName}", "success", 3f );
	}

	public void PushMilestoneToastToOwner( string title, int xpReward = 0 )
	{
		if ( IsLocalPlayer() )
			ThornsMilestoneHudBus.Push( title, xpReward );
		else if ( Networking.IsActive )
			RpcOwnerMilestoneToast( title, xpReward );
	}

	[Rpc.Owner]
	void RpcOwnerMilestoneToast( string title, int xpReward )
	{
		ThornsMilestoneHudBus.Push( title, xpReward );
	}

	public void HostGrantXp( int xp )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || xp <= 0 )
			return;

		_totalXp += xp;
		var newLevel = 1 + (_totalXp / XpPerLevel);
		if ( newLevel > _playerLevel )
		{
			_playerLevel = newLevel;
			HostApplySurvivalCaps();
			PlayOwnerSfx( ThornsGameplaySfx.LevelUp );
			PushLevelUpMomentToOwner( _playerLevel );
		}

		HostRecalculateUpgradePoints();
		PushSkillsToOwner();
	}

	void PushJournalToOwner()
	{
		if ( !CanPushOwnerRpcs() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			ThornsJourneyProgression.HostMigrateJournalSnapshot( _journal );

		if ( !Networking.IsActive )
		{
			ThornsUiClientState.ApplyPartialJournal( _journal );
			return;
		}

		RpcSyncJournalJson( Json.Serialize( _journal ) );
	}

	[Rpc.Owner]
	void RpcSyncJournalJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsJournalSnapshotDto journal ) )
			return;

		ThornsUiClientState.ApplyPartialJournal( journal );
	}

	public void PinGoalToHud( string goalId )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( goalId ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
		{
			RpcPinGoalToHud( goalId );
			return;
		}

		HostPinGoalToHud( goalId );
	}

	[Rpc.Host]
	void RpcPinGoalToHud( string goalId )
	{
		if ( !ValidateCaller() )
			return;

		HostPinGoalToHud( goalId );
	}

	void HostPinGoalToHud( string goalId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( goalId ) )
			return;

		var goal = HostFindJournalGoal( goalId );
		if ( goal is null || goal.State != ThornsGoalState.Active )
			return;

		_journal.HudPinnedGoalId = goal.GoalId;
		PushJournalToOwner();
		HostPersistPlayerState();
		UiRevisionBus.Publish( UiRevisionChannel.Journal );
	}

	public void SetJournalUiState( ThornsJournalSection? section, string selectedGoalId, string selectedDiscoveryId = null )
	{
		if ( !IsLocalPlayer() || !ThornsUiClientState.HasSnapshot )
			return;

		if ( section.HasValue )
			ThornsUiClientState.Snapshot.Journal.ActiveSection = section.Value;
		if ( selectedGoalId is not null )
			ThornsUiClientState.Snapshot.Journal.SelectedGoalId = selectedGoalId;
		if ( selectedDiscoveryId is not null )
			ThornsUiClientState.Snapshot.Journal.SelectedDiscoveryId = selectedDiscoveryId;

		if ( ThornsMultiplayer.IsHostOrOffline )
		{
			if ( section.HasValue )
				_journal.ActiveSection = section.Value;
			if ( selectedGoalId is not null && !string.IsNullOrEmpty( selectedGoalId ) )
				_journal.SelectedGoalId = selectedGoalId;
			if ( selectedDiscoveryId is not null && !string.IsNullOrEmpty( selectedDiscoveryId ) )
				_journal.SelectedDiscoveryId = selectedDiscoveryId;
		}

		UiRevisionBus.Publish( UiRevisionChannel.Journal );
	}
}
