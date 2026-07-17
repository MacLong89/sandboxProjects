namespace Fauna2;

/// <summary>
/// Validates that an incoming host RPC came from the zoo owner connection.
/// Pair with host-side range / rule checks — auth alone is not enough.
/// </summary>
public static class RpcAuthorization
{
	/// <summary>
	/// True when Rpc.Caller is allowed to mutate the shared zoo
	/// (build, catch, economy, clear terrain, etc.).
	/// </summary>
	public static bool IsOwnerCaller()
	{
		// Offline / no RPC caller context → only the host process may proceed.
		var caller = Rpc.Caller;
		var callerId = caller?.SteamId.Value ?? 0;
		if ( caller is null || callerId == 0 )
			return Networking.IsHost;

		// AUDIT FIX B1 (2026-07): Prefer lobby-host identity.
		// Fauna2's product model is listen-server: Networking host == zoo owner.
		// Connection.Host is the authoritative lobby host when multiplayer is active.
		// This catches ownership even if IsZooOwner Sync was somehow wrong mid-boot.
		// Revert hint: if Connection.Host is null in your s&box build, the SteamId
		// path below (FromHost-stamped IsZooOwner) is the fallback.
		if ( Connection.Host is not null && caller == Connection.Host )
			return true;

		// Primary stamp: PlayerState.IsZooOwner is FromHost-only (see PlayerState).
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			if ( ps.SteamId == callerId ) return true;
		}

		return false;
	}

	/// <summary>Host-side: feet position of the stamped zoo owner, if present.</summary>
	public static bool TryGetOwnerFeet( out Vector3 feet )
	{
		feet = default;
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			feet = ps.FeetPosition;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Host-side: feet of the RPC caller (matched by SteamId). Used for range checks
	/// so clients cannot forge interactions at arbitrary world positions.
	/// </summary>
	public static bool TryGetCallerFeet( out Vector3 feet )
	{
		feet = default;
		var callerId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( callerId == 0 )
			return TryGetOwnerFeet( out feet );

		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || ps.SteamId != callerId ) continue;
			feet = ps.FeetPosition;
			return true;
		}

		return false;
	}

	/// <summary>Horizontal (XY) distance helper — matches WorldInteractSystem checks.</summary>
	public static float HorizontalDistance( Vector3 a, Vector3 b )
	{
		var dx = a.x - b.x;
		var dy = a.y - b.y;
		return MathF.Sqrt( dx * dx + dy * dy );
	}

	public static bool IsCallerWithinRange( Vector3 worldPoint, float range )
	{
		if ( !TryGetCallerFeet( out var feet ) )
			return false;

		return HorizontalDistance( feet, worldPoint ) <= range;
	}
}
