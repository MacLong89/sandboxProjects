namespace Terraingen.GameData;

using Terraingen.Buildings;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Auto-sets map/compass waypoints for active pinned journey goals.</summary>
public static class ThornsJourneyWaypointSync
{
	sealed class SyncState
	{
		public string GoalId = "";
		public Vector3 Target;
	}

	static readonly Dictionary<string, SyncState> SyncedByAccount = new( StringComparer.OrdinalIgnoreCase );

	public static void HostTick( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return;

		var journal = gameplay.HostPeekJournalSnapshot();
		if ( journal is null )
			return;

		var goalId = ThornsJourneyProgression.HostResolveHudPinnedGoalId( journal );
		if ( string.IsNullOrWhiteSpace( goalId ) || !TryResolveTarget( gameplay, goalId, out var target ) )
		{
			ClearGoalWaypointIfSynced( gameplay.AccountKey );
			return;
		}

		var state = GetOrCreateState( gameplay.AccountKey );
		if ( string.Equals( goalId, state.GoalId, StringComparison.OrdinalIgnoreCase )
		     && (target - state.Target).LengthSquared < 64f )
			return;

		state.GoalId = goalId;
		state.Target = target;
		var goal = ThornsDefinitionRegistry.GetGoal( goalId );
		ThornsMapWorldService.Instance?.HostSetGoalWaypoint( gameplay.AccountKey, target.x, target.y, goal?.Title ?? "Goal" );
	}

	public static void HostClearGoalWaypointForAccount( string accountKey )
	{
		if ( string.IsNullOrWhiteSpace( accountKey ) )
			return;

		ClearGoalWaypointIfSynced( accountKey );
	}

	static void ClearGoalWaypointIfSynced( string accountKey )
	{
		if ( !SyncedByAccount.TryGetValue( accountKey, out var state ) || string.IsNullOrWhiteSpace( state.GoalId ) )
			return;

		state.GoalId = "";
		state.Target = default;
		ThornsMapWorldService.Instance?.HostClearGoalWaypoint( accountKey );
	}

	static SyncState GetOrCreateState( string accountKey )
	{
		if ( !SyncedByAccount.TryGetValue( accountKey, out var state ) )
		{
			state = new SyncState();
			SyncedByAccount[accountKey] = state;
		}

		return state;
	}

	static bool TryResolveTarget( ThornsPlayerGameplay gameplay, string goalId, out Vector3 target )
	{
		target = default;

		if ( string.Equals( goalId, "goal_visit_town", StringComparison.OrdinalIgnoreCase ) )
			return TryNearestTown( gameplay.GameObject.WorldPosition, out target );

		return false;
	}

	static bool TryNearestTown( Vector3 from, out Vector3 target )
	{
		target = default;
		var bestSq = float.MaxValue;
		var found = false;

		foreach ( var center in ThornsTownNodeRegistry.TownCenters )
		{
			var sq = (center - from).LengthSquared;
			if ( sq >= bestSq )
				continue;

			bestSq = sq;
			target = center;
			found = true;
		}

		return found;
	}
}
