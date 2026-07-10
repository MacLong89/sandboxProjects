using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host: bandits in chase/attack — refreshed periodically for music suppression (one scan, many listeners).
/// </summary>
static class ThornsHostileAwarenessCache
{
	static readonly List<Vector3> HostilePositions = new();
	static double _nextRefreshRealtime;

	const float RefreshIntervalSeconds = 0.5f;

	public static bool AnyHostileWithinRadius( Vector3 pos, float radius )
	{
		if ( !Networking.IsHost && Networking.IsActive )
			return false;

		HostRefreshIfStale();

		var r2 = radius * radius;
		foreach ( var hp in HostilePositions )
		{
			var dx = hp.x - pos.x;
			var dy = hp.y - pos.y;
			if ( dx * dx + dy * dy <= r2 )
				return true;
		}

		return false;
	}

	static void HostRefreshIfStale()
	{
		var now = Time.Now;
		if ( now < _nextRefreshRealtime )
			return;

		_nextRefreshRealtime = now + RefreshIntervalSeconds;
		HostilePositions.Clear();

		foreach ( var brain in ThornsPopulationDirector.HostBanditBrainsReadOnly )
		{
			if ( !brain.IsValid() || !brain.Enabled )
				continue;

			var state = brain.State;
			if ( state is not (ThornsBanditAiState.Chase or ThornsBanditAiState.Attack or ThornsBanditAiState.Alert
				    or ThornsBanditAiState.SeekCover or ThornsBanditAiState.Investigate) )
				continue;

			var root = brain.GameObject;
			if ( !root.IsValid() )
				continue;

			HostilePositions.Add( root.WorldPosition );
		}
	}
}
