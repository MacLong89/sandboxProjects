namespace Sandbox;

/// <summary>Shared spawn eligibility checks for periodic NPC spawners — budget + anchor selection only (no spawn side effects).</summary>
public static class ThornsPopulationSpawnEligibility
{
	/// <summary>
	/// Validates population budget and picks a random host spawn-anchor player.
	/// Does not mutate world state.
	/// </summary>
	public static bool HostTryEvaluatePeriodicSpawn(
		ThornsPopulationKind kind,
		in ThornsPopulationSpawnRequest request,
		out GameObject anchor,
		out string denyReason )
	{
		anchor = default;
		denyReason = null;

		if ( !ThornsPopulationDirector.HostTryRequestSpawnSlot( kind, request, out denyReason ) )
			return false;

		var roots = ThornsPopulationDirector.HostGetCachedPlayerRoots();
		if ( roots.Count == 0 )
		{
			denyReason = "zero player roots (no ThornsPawn yet, or director cache empty)";
			return false;
		}

		if ( !ThornsHealth.HostTryPickRandomNpcSpawnAnchorPlayer( roots, out anchor ) || !anchor.IsValid() )
		{
			denyReason = "no spawn-anchor players (all in post-spawn spawn cooldown)";
			return false;
		}

		if ( request.PerPlayerNearbyCap > 0 && request.PerPlayerNearbyRadius > 0f )
		{
			var withAnchor = request with { AnchorWorldPosition = anchor.WorldPosition };
			if ( !ThornsPopulationDirector.HostTryRequestSpawnSlot( kind, withAnchor, out denyReason ) )
				return false;
		}

		return true;
	}

	/// <summary>Future reservation hook — no-op until event/NPC burst budgets land.</summary>
	public static bool HostTryReserveSpawnSlot( ThornsPopulationKind kind, in ThornsPopulationSpawnRequest request ) =>
		ThornsPopulationDirector.HostTryRequestSpawnSlot( kind, request, out _ );
}
