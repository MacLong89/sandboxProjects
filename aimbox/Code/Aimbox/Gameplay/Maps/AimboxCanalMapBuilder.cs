namespace Sandbox;

/// <summary>Dry canal arena with bridge screens, pump-house cover, and flanking embankments.</summary>
public static class AimboxCanalMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		var walkZ = AimboxMapBuilderCommon.WalkZ;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Gravel );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaSurface.StoneBrick );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;
		var trenchDepth = waist * 0.48f;
		var trenchFloorZ = walkZ - trenchDepth;

		var trenchFloor = new Vector3( t * 4.6f, hl * 1.38f, floor );
		Block( root, "Canal Floor", Ground( new Vector3( 0, 0, 0 ), trenchFloor, trenchFloorZ ), trenchFloor, AimboxArenaPalette.Road );

		var berm = new Vector3( t * 1.45f, hl * 0.82f, waist * 0.62f );
		Block( root, "East Embankment North", Ground( new Vector3( hw * 0.32f, hl * 0.28f, 0 ), berm ), berm, AimboxArenaPalette.Gravel );
		Block( root, "East Embankment South", Ground( new Vector3( hw * 0.40f, -hl * 0.30f, 0 ), berm ), berm, AimboxArenaPalette.Gravel );
		Block( root, "West Embankment North", Ground( new Vector3( -hw * 0.40f, hl * 0.30f, 0 ), berm ), berm, AimboxArenaPalette.Gravel );
		Block( root, "West Embankment South", Ground( new Vector3( -hw * 0.32f, -hl * 0.28f, 0 ), berm ), berm, AimboxArenaPalette.Gravel );

		var bridge = new Vector3( t * 5.2f, t * 3.2f, t * 0.46f );
		Block( root, "Bridge North", Ground( new Vector3( 0, hl * 0.43f, 0 ), bridge ), bridge, AimboxArenaPalette.Concrete );
		Block( root, "Bridge Center", Ground( Vector3.Zero, bridge * 1.08f ), bridge * 1.08f, AimboxArenaPalette.Concrete );
		Block( root, "Bridge South", Ground( new Vector3( 0, -hl * 0.43f, 0 ), bridge ), bridge, AimboxArenaPalette.Concrete );

		var bridgeScreen = new Vector3( t * 0.9f, t * 4.3f, lane );
		Block( root, "North Bridge Screen", Ground( new Vector3( -t * 2.0f, hl * 0.43f, 0 ), bridgeScreen ), bridgeScreen, AimboxArenaPalette.Barrier );
		Block( root, "Center Bridge Screen", Ground( new Vector3( t * 2.0f, 0, 0 ), bridgeScreen ), bridgeScreen, AimboxArenaPalette.Barrier );
		Block( root, "South Bridge Screen", Ground( new Vector3( -t * 2.0f, -hl * 0.43f, 0 ), bridgeScreen ), bridgeScreen, AimboxArenaPalette.Barrier );

		var pump = new Vector3( t * 4.1f, t * 3.5f, tall );
		Block( root, "Pump House West", Ground( new Vector3( -hw * 0.54f, hl * 0.16f, 0 ), pump ), pump, AimboxArenaPalette.BarnWood );
		Block( root, "Valve House East", Ground( new Vector3( hw * 0.54f, -hl * 0.16f, 0 ), pump ), pump, AimboxArenaSurface.StoneBrick );

		var pipe = new Vector3( t * 2.4f, t * 2.4f, waist * 0.65f );
		Block( root, "Culvert North", Ground( new Vector3( t * 0.9f, hl * 0.20f, 0 ), pipe, trenchFloorZ ), pipe, AimboxArenaPalette.Metal, Rotation.FromYaw( 90f ) );
		Block( root, "Culvert South", Ground( new Vector3( -t * 0.8f, -hl * 0.22f, 0 ), pipe, trenchFloorZ ), pipe, AimboxArenaPalette.Metal, Rotation.FromYaw( 90f ) );

		var sandbag = new Vector3( t * 2.5f, t * 1.45f, waist * 0.72f );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.22f, hl * 0.58f ),
				new Vector2( hw * 0.22f, -hl * 0.58f ),
				new Vector2( -hw * 0.55f, -hl * 0.42f ),
				new Vector2( hw * 0.55f, hl * 0.42f )
			],
			sandbag,
			AimboxArenaPalette.Barrier,
			"Sandbag" );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaSurface.StoneBrick,
			AimboxArenaPalette.Barrier,
			AimboxArenaSurface.StoneBrick );

		AimboxMapBuilderCommon.BuildSpawnAlcoves( root, cfg );
		AimboxMapBuilderCommon.BuildSpawnAccents( root, cfg );
	}

	static Vector3 Ground( Vector3 position, Vector3 size, float floorTopZ = -1f ) =>
		AimboxMapBuilderCommon.OnGround( position, size, floorTopZ );
	static void Block( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxMapBuilderCommon.AddBlock( root, name, center, size, surface, rotation );
}
