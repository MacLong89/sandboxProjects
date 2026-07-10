namespace Sandbox;

/// <summary>Server-assigned role for the current round. Clients read-only via sync.</summary>
[Title( "YouAreNotAlone — Player role" )]
[Category( "YouAreNotAlone" )]
[Icon( "groups" )]
[Order( 35 )]
public sealed class YaPlayerRoleComponent : Component
{
	[Sync( SyncFlags.FromHost )]
	public YaPlayerRole Role { get; set; } = YaPlayerRole.Unassigned;

	/// <summary>Host-only: set role and replicate.</summary>
	public void HostSetRole( YaPlayerRole role )
	{
		if ( !Networking.IsHost )
			return;
		Role = role;
	}

	/// <summary>Host-only: reset to neutral between rounds.</summary>
	public void HostClearRole()
	{
		if ( !Networking.IsHost )
			return;
		Role = YaPlayerRole.Unassigned;
	}
}
