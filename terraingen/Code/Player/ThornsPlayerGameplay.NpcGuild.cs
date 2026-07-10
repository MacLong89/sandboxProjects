namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.NpcGuild;

/// <summary>NPC rival guild core claim requests.</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestClaimNpcGuildCore( ThornsNpcGuildCore core )
	{
		if ( !IsLocalPlayer() || core is null || !core.IsValid() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcClaimNpcGuildCore();
		else
			ThornsNpcGuildWorldService.EnsureInstance()?.HostTryClaimCoreResolved( this );
	}

	[Rpc.Host]
	void RpcClaimNpcGuildCore()
	{
		if ( !ValidateCaller() )
			return;

		ThornsNpcGuildWorldService.EnsureInstance()?.HostTryClaimCoreResolved( this );
	}

	public void HostNotifyNpcCoreClaimFailed( string reason )
	{
		if ( string.IsNullOrWhiteSpace( reason ) )
			return;

		PushOwnerNotification( reason, "warning" );
	}
}
