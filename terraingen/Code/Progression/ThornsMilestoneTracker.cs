namespace Terraingen.GameData;

using Terraingen;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.Victory;

/// <summary>Host-side milestone evaluation against <see cref="ThornsMilestoneDefinitions"/>.</summary>
public static class ThornsMilestoneTracker
{
	public static void OnInventoryChanged( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		gameplay.HostSyncDiscoveriesFromInventory();

		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Collect )
				continue;

			if ( def.CollectMode == ThornsCollectTrackMode.HoldInInventory )
			{
				var held = gameplay.HostCountItem( def.TargetKey );
				gameplay.HostSetMilestoneProgress( def.Id, held );
			}
		}
	}

	public static void OnItemCollected( ThornsPlayerGameplay gameplay, string itemId, int amount )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline || amount <= 0 )
			return;

		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Collect )
				continue;
			if ( def.CollectMode != ThornsCollectTrackMode.Lifetime )
				continue;
			if ( !string.Equals( def.TargetKey, itemId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !IsGoalActive( gameplay, def.Id ) )
				continue;

			gameplay.HostAdvanceMilestone( def.Id, amount );
		}

		gameplay.HostMarkDiscovery( ThornsDefinitionRegistry.DiscoveryIdForItem( itemId ) );
		OnInventoryChanged( gameplay );
	}

	public static void OnCrafted( ThornsPlayerGameplay gameplay, string outputItemId, int count = 1 )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var advanced = false;
		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Craft )
				continue;
			if ( !ItemMatches( def.TargetKey, outputItemId ) )
				continue;
			if ( !IsGoalActive( gameplay, def.Id ) )
				continue;

			gameplay.HostAdvanceMilestone( def.Id, count );
			advanced = true;
		}

		if ( advanced )
			ThornsVictoryBridge.Report( gameplay, "advanced_craft", count );

		if ( IsSurvivalArmamentCraft( outputItemId ) )
			ThornsJourneyProgression.NotifySurvivalArmamentAcquired( gameplay );
	}

	static bool IsSurvivalArmamentCraft( string itemId ) =>
		ThornsJourneyProgression.IsSurvivalArmamentItem( itemId );

	public static void OnBuilt( ThornsPlayerGameplay gameplay, string placeableId, int count = 1 )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var advanced = false;
		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Build )
				continue;
			if ( !ItemMatches( def.TargetKey, placeableId ) )
				continue;
			if ( !IsGoalActive( gameplay, def.Id ) )
				continue;

			gameplay.HostAdvanceMilestone( def.Id, count );
			advanced = true;
		}

		if ( advanced )
			ThornsVictoryBridge.Report( gameplay, "guild_structure", count );
	}

	public static void OnKill( ThornsPlayerGameplay gameplay, string killCategory, int count = 1 )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var advanced = false;
		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Kill )
				continue;
			if ( !string.Equals( def.TargetKey, killCategory, StringComparison.OrdinalIgnoreCase ) )
				continue;
			if ( !IsGoalActive( gameplay, def.Id ) )
				continue;

			gameplay.HostAdvanceMilestone( def.Id, count );
			advanced = true;
		}

		if ( !advanced )
			return;

		var victorySource = killCategory switch
		{
			"bandit" => "bandit_camp_cleared",
			"corrupted" or "bloom" => "corrupted_creature",
			"bloom_host" => "bloom_host_destroyed",
			"player" => "pvp_victory",
			_ => null
		};
		if ( victorySource is not null )
			ThornsVictoryBridge.Report( gameplay, victorySource, count );
	}

	public static void OnTamed( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		gameplay.HostAdvanceMilestone( "goal_tame_creature", 1 );
		ThornsVictoryBridge.Report( gameplay, "animal_tamed" );
	}

	public static void OnEvent( ThornsPlayerGameplay gameplay, string eventToken )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( eventToken ) )
			return;

		var advanced = false;
		foreach ( var def in ThornsMilestoneDefinitions.All )
		{
			if ( def.MilestoneType != ThornsMilestoneType.Event )
				continue;
			if ( !string.Equals( def.TargetKey, eventToken, StringComparison.OrdinalIgnoreCase ) )
				continue;
			if ( !IsGoalActive( gameplay, def.Id ) )
				continue;

			gameplay.HostAdvanceMilestone( def.Id, 1 );
			advanced = true;
		}

		if ( advanced )
			ThornsVictoryBridge.Report( gameplay, "world_event_completed" );
	}

	static bool IsGoalActive( ThornsPlayerGameplay gameplay, string goalId )
	{
		var row = gameplay.HostGetJournalGoal( goalId );
		return ThornsJourneyProgression.CanTrackProgress( row );
	}

	static bool ItemMatches( string expected, string actual )
	{
		if ( string.Equals( expected, actual, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return string.Equals(
			ThornsItemIdAliases.Canonicalize( expected ),
			ThornsItemIdAliases.Canonicalize( actual ),
			StringComparison.OrdinalIgnoreCase );
	}
}
