namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.World;

/// <summary>Bloom Seed purification requests.</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestPurifyBloomSeed()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcPurifyBloomSeed();
		else
			ThornsBloomSeedWorldService.Instance?.HostTryPurifyResolved( this );
	}

	[Rpc.Host]
	void RpcPurifyBloomSeed()
	{
		if ( !ValidateCaller() )
			return;

		ThornsBloomSeedWorldService.Instance?.HostTryPurifyResolved( this );
	}
}
