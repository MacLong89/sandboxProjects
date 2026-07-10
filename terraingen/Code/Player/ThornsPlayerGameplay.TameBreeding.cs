namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;

/// <summary>Host-authoritative tame breeding requests.</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestTameBreed( ThornsTameBreedRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcTameBreed( req );
		else
			HostTameBreed( req );
	}

	[Rpc.Host]
	void RpcTameBreed( ThornsTameBreedRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostTameBreed( req );
	}

	public void HostTameBreed( ThornsTameBreedRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		var ok = ThornsTameBreedingHost.TryBreed( Scene, AccountKey, this, req, out var message );
		if ( !string.IsNullOrWhiteSpace( message ) )
			PushOwnerNotification( message, ok ? "success" : "warning" );

		if ( !ok )
		{
			HostRebuildTames();
			PushTamesToOwner();
		}
	}
}
