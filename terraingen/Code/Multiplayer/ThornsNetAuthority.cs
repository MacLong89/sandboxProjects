namespace Terraingen.Multiplayer;

using Sandbox.Network;
using Terraingen.Player;

/// <summary>Unified RPC caller validation and host-only guards.</summary>
public static class ThornsNetAuthority
{
	public static bool IsHostSimulation => ThornsMultiplayer.IsHostOrOffline;

	/// <summary>True when this connection owns the networked pawn.</summary>
	public static bool ValidateOwnerCaller( Component component, Connection caller )
	{
		if ( component is null || !component.IsValid() || caller is null )
			return false;

		return component.Network.OwnerId == caller.Id;
	}

	/// <summary>
	/// Host-only RPC guard. On a listen server, <see cref="Rpc.Caller"/> identifies the remote connection
	/// even when the host simulates locally — do not fall back to <see cref="Connection.Local"/> blindly.
	/// </summary>
	public static bool ValidateHostRpcCallerOwnsPawnRoot( GameObject pawnNetworkRoot )
	{
		if ( !pawnNetworkRoot.IsValid() )
			return false;

		var ownerId = pawnNetworkRoot.Network.OwnerId;
		if ( Rpc.Caller is not null )
			return Rpc.Caller.Id == ownerId;

		var local = Connection.Local;
		return local is not null && local.Id == ownerId;
	}

	/// <summary>Standard [Rpc.Host] gate for player-owned components.</summary>
	public static bool ValidateOwnerCaller( ThornsPlayerGameplay gameplay ) =>
		gameplay.IsValid() && ValidateOwnerCaller( gameplay, Rpc.Caller );

	/// <summary>Standard [Rpc.Host] gate for any component with an owner.</summary>
	public static bool ValidateOwnerCaller( Component component ) =>
		component.IsValid() && ValidateOwnerCaller( component, Rpc.Caller );

	/// <summary>Only the listen-server / dedicated host should invoke world broadcast RPCs.</summary>
	public static bool ValidateHostInvoker()
	{
		if ( !Networking.IsActive )
			return true;

		if ( !Networking.IsHost )
			return false;

		return Rpc.Caller is null || Rpc.Caller == Connection.Host;
	}

	/// <summary>Drop [Rpc.Broadcast] calls that did not originate from host simulation.</summary>
	public static bool RejectClientBroadcastOrigin()
	{
		if ( !Networking.IsActive )
			return false;

		return Rpc.Caller is not null && !ValidateHostInvoker();
	}

	/// <summary>Guard owner JSON sync RPCs against oversized or malformed payloads.</summary>
	public static bool TryDeserializeJson<T>( string json, int maxBytes, out T value ) where T : class
	{
		value = null;
		if ( string.IsNullOrEmpty( json ) || json.Length > maxBytes )
			return false;

		try
		{
			value = Json.Deserialize<T>( json );
			return value is not null;
		}
		catch
		{
			return false;
		}
	}

	public const int DefaultOwnerJsonMaxBytes = 256_000;
}
