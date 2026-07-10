namespace Sandbox;

/// <summary>Shared arena root lookup under the non-networked world anchor.</summary>
public static class AimboxArenaWorld
{
	public static string[] ArenaRootNames => AimboxMapCatalog.ArenaRootNames;

	public static GameObject EnsureAnchor()
	{
		var game = AimboxGame.Instance;
		return game is null ? default : game.EnsureArenaAnchor();
	}

	public static GameObject FindArenaRoot( string arenaRootName )
	{
		var anchor = EnsureAnchor();
		if ( !anchor.IsValid() )
			return default;

		GameObject newest = default;
		foreach ( var child in anchor.Children )
		{
			if ( child.IsValid() && string.Equals( child.Name, arenaRootName, StringComparison.OrdinalIgnoreCase ) )
				newest = child;
		}

		return newest;
	}

	/// <summary>Destroy stale roots and create a fresh arena root (avoids deferred-destroy duplicate children).</summary>
	public static GameObject RecreateArenaRoot( string arenaRootName )
	{
		DestroyArenaRootsNamed( arenaRootName );

		var anchor = EnsureAnchor();
		if ( !anchor.IsValid() )
			return default;

		var root = new GameObject( true, arenaRootName );
		root.SetParent( anchor );
		root.NetworkMode = NetworkMode.Never;
		return root;
	}

	public static void DestroyArenaRoot( string arenaRootName ) => DestroyArenaRootsNamed( arenaRootName );

	static void DestroyArenaRootsNamed( string arenaRootName )
	{
		var anchor = EnsureAnchor();
		if ( !anchor.IsValid() )
			return;

		foreach ( var child in anchor.Children.ToArray() )
		{
			if ( !child.IsValid() )
				continue;

			if ( !string.Equals( child.Name, arenaRootName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			child.Enabled = false;
			child.Destroy();
		}
	}

	public static void DestroyAllArenaRoots()
	{
		foreach ( var rootName in ArenaRootNames )
			DestroyArenaRoot( rootName );
	}

	public static int CountArenaBlocks()
	{
		var count = 0;
		foreach ( var rootName in ArenaRootNames )
		{
			var root = FindArenaRoot( rootName );
			if ( !root.IsValid() )
				continue;

			count += root.Children.Count( child =>
				child.IsValid() && child.Components.Get<ModelRenderer>() is { Enabled: true } );
		}

		return count;
	}

	public static string DescribeArenaInventory()
	{
		var parts = new List<string>();
		foreach ( var rootName in ArenaRootNames )
		{
			var root = FindArenaRoot( rootName );
			if ( !root.IsValid() )
				continue;

			parts.Add( $"{rootName}={root.Children.Count( c => c.IsValid() )}" );
		}

		return parts.Count > 0 ? string.Join( ", ", parts ) : "none";
	}
}
