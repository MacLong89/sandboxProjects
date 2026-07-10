namespace Sandbox;

/// <summary>
/// Host helpers so runtime-built hierarchies replicate renderers and children to joining clients.
/// Only setting <see cref="GameObject.NetworkMode"/> on the root leaves children in snapshot mode and
/// proxies often never receive <see cref="ModelRenderer"/> / <see cref="SkinnedModelRenderer"/> state.
/// </summary>
public static class ThornsNetworkReplication
{
	public static void SetSubtreeNetworkModeObject( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		root.NetworkMode = NetworkMode.Object;
		foreach ( var child in root.Children )
			SetSubtreeNetworkModeObject( child );
	}

	/// <summary>Dev galleries / floorplan tests — keep runtime-built shells local (avoids trim children failing to replicate).</summary>
	public static void SetSubtreeNetworkModeNever( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		root.NetworkMode = NetworkMode.Never;
		foreach ( var child in root.Children )
			SetSubtreeNetworkModeNever( child );
	}

	/// <summary>Marks the full hierarchy <see cref="NetworkMode.Object"/> then spawns with host ownership.</summary>
	public static bool TryNetworkSpawnHostOwned( GameObject root )
	{
		if ( !root.IsValid() )
			return false;

		if ( !Networking.IsActive )
			return true;

		SetSubtreeNetworkModeObject( root );
		var opts = new NetworkSpawnOptions
		{
			Owner = Connection.Host,
			OrphanedMode = NetworkOrphaned.Host
		};

		return root.NetworkSpawn( opts );
	}
}
