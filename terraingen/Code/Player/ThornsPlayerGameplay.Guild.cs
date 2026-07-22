namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Multiplayer;

/// <summary>Guild requests and host RPCs (extracted from gameplay god-class).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void RequestGuildLeave()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildLeave();
		else
			ThornsGuildWorldService.Instance?.HostRequestLeaveGuild( this );
	}

	[Rpc.Host]
	void RpcGuildLeave()
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostRequestLeaveGuild( this );
	}

	public void RequestGuildCreate( string name )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( name ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildCreate( name );
		else
			ThornsGuildWorldService.Instance?.HostCreateGuild( this, name );
	}

	public void RequestGuildJoin( string guildId )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( guildId ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildJoin( guildId );
		else
			ThornsGuildWorldService.Instance?.HostJoinGuild( this, guildId );
	}

	public void RequestGuildRename( string name )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( name ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildRename( name );
		else
			ThornsGuildWorldService.Instance?.HostRenameGuild( this, name );
	}

	public void RequestGuildInvite( string targetAccountKey )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( targetAccountKey ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildInvite( targetAccountKey );
		else
			ThornsGuildWorldService.Instance?.HostInviteToGuild( this, targetAccountKey );
	}

	public void RequestGuildAnnouncement( string message )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcGuildAnnouncement( message ?? "" );
		else
			ThornsGuildWorldService.Instance?.HostUpdateAnnouncement( this, message );
	}

	[Rpc.Host]
	void RpcGuildCreate( string name )
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostCreateGuild( this, name );
	}

	[Rpc.Host]
	void RpcGuildJoin( string guildId )
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostJoinGuild( this, guildId );
	}

	[Rpc.Host]
	void RpcGuildRename( string name )
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostRenameGuild( this, name );
	}

	[Rpc.Host]
	void RpcGuildInvite( string targetAccountKey )
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostInviteToGuild( this, targetAccountKey );
	}

	[Rpc.Host]
	void RpcGuildAnnouncement( string message )
	{
		if ( !ValidateCaller() )
			return;

		ThornsGuildWorldService.Instance?.HostUpdateAnnouncement( this, message );
	}
}
