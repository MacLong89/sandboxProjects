namespace Fauna2;

/// <summary>Validates that an incoming host RPC came from the zoo owner connection.</summary>
public static class RpcAuthorization
{
	public static bool IsOwnerCaller()
	{
		var callerId = Rpc.Caller?.SteamId.Value ?? 0;
		if ( callerId == 0 )
			return Networking.IsHost;

		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			if ( ps.SteamId == callerId ) return true;
		}

		return false;
	}
}
