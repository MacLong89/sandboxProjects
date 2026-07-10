namespace Terraingen.Multiplayer;

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
