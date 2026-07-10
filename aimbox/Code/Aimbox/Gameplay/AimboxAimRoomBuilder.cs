namespace Sandbox;

/// <summary>Builds the compact enclosed room used by aim trainer game modes.</summary>
public static class AimboxAimRoomBuilder
{
	public static void Ensure( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var root = AimboxArenaWorld.RecreateArenaRoot( AimboxAimRoomLayout.RootName );
		if ( !root.IsValid() )
			return;

		var cfg = AimboxAimRoomLayout.Layout;
		var floor = AimboxMapDesignRules.FloorSlabThickness;

		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Floor );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaSurface.ConcreteDark );

		var backWallY = cfg.ArenaHalfLength - cfg.WallThickness * 0.35f;
		var panelSize = new Vector3( cfg.ArenaHalfWidth * 1.35f, cfg.WallThickness * 0.55f, cfg.WallHeight * 0.82f );
		AimboxMapBuilderCommon.AddAccent(
			root,
			"AIM Target Wall",
			AimboxMapBuilderCommon.OnGround( new Vector3( 0f, backWallY, 0f ), panelSize ),
			panelSize,
			new Color( 0.18f, 0.22f, 0.28f ) );

		Log.Info( $"[Aimbox AIM] Trainer room ready ({AimboxAimRoomLayout.RoomWidthMeters:0.#}m x {AimboxAimRoomLayout.RoomDepthMeters:0.#}m)." );
	}

	public static void Destroy()
	{
		AimboxArenaWorld.DestroyArenaRoot( AimboxAimRoomLayout.RootName );
	}
}
