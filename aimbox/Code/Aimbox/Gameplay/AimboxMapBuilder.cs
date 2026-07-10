namespace Sandbox;

public static class AimboxMapBuilder
{
	public static void Ensure( Scene scene, AimboxArenaMap map, bool skip )
	{
		if ( scene is null || !scene.IsValid() || skip )
			return;

		var def = AimboxMapCatalog.Get( map );
		var root = AimboxArenaWorld.RecreateArenaRoot( def.RootName );
		if ( !root.IsValid() )
			return;

		BuildLayout( root, def );
		Log.Info( $"[Aimbox] {def.DisplayName} arena built ({def.WidthMeters:0.#}m x {def.DepthMeters:0.#}m)." );
	}

	public static void Destroy( Scene scene, AimboxArenaMap map )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		AimboxArenaWorld.DestroyArenaRoot( AimboxMapCatalog.Get( map ).RootName );
	}

	static void BuildLayout( GameObject root, AimboxMapDefinition def )
	{
		switch ( def.Map )
		{
			case AimboxArenaMap.Yard:
				AimboxYardMapBuilder.Build( root, def.Layout );
				break;
			case AimboxArenaMap.Docks:
				AimboxDocksMapBuilder.Build( root, def.Layout );
				break;
			case AimboxArenaMap.Vault:
				AimboxVaultMapBuilder.Build( root, def.Layout );
				break;
			case AimboxArenaMap.Junction:
				AimboxJunctionMapBuilder.Build( root, def.Layout );
				break;
			case AimboxArenaMap.Stack:
				AimboxStackMapBuilder.Build( root, def.Layout );
				break;
			case AimboxArenaMap.Canal:
				AimboxCanalMapBuilder.Build( root, def.Layout );
				break;
		}
	}
}
