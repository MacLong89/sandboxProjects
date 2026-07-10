namespace Sandbox;

/// <summary>Railyard arena with a diagonal boxcar spine and broken quadrant routes.</summary>
public static class AimboxJunctionMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Gravel );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaPalette.Metal );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;

		var boxcar = new Vector3( hw * 0.92f, t * 2.75f, tall * 1.14f );
		Block( root, "Crashed Boxcar Center", Ground( new Vector3( t * 0.4f, 0, 0 ), boxcar ), boxcar, AimboxArenaSurface.CorrugatedMetal, Rotation.FromYaw( 38f ) );

		var car = new Vector3( hw * 0.34f, t * 2.4f, tall * 1.08f );
		Block( root, "North Boxcar", Ground( new Vector3( -hw * 0.28f, hl * 0.50f, 0 ), car ), car, AimboxArenaSurface.CorrugatedMetal, Rotation.FromYaw( -8f ) );
		Block( root, "South Boxcar", Ground( new Vector3( hw * 0.28f, -hl * 0.50f, 0 ), car ), car, AimboxArenaSurface.CorrugatedMetal, Rotation.FromYaw( -8f ) );

		var switchWall = new Vector3( t * 1.0f, hl * 0.26f, lane );
		Block( root, "Switch Wall West", Ground( new Vector3( -hw * 0.42f, -hl * 0.12f, 0 ), switchWall ), switchWall, AimboxArenaPalette.Barrier );
		Block( root, "Switch Wall East", Ground( new Vector3( hw * 0.42f, hl * 0.12f, 0 ), switchWall ), switchWall, AimboxArenaPalette.Barrier );

		var signalBase = new Vector3( t * 2.7f, t * 2.7f, waist );
		Block( root, "Signal Base", Ground( new Vector3( -hw * 0.55f, hl * 0.30f, 0 ), signalBase ), signalBase, AimboxArenaPalette.Metal );
		var signalMast = new Vector3( t * 0.8f, t * 0.8f, tall * 1.35f );
		Block( root, "Signal Mast", Ground( new Vector3( -hw * 0.55f, hl * 0.30f, 0 ), signalMast ), signalMast, AimboxArenaPalette.Barrier );

		var crate = new Vector3( t * 2.15f, t * 2.0f, waist );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.48f, -hl * 0.34f ),
				new Vector2( hw * 0.47f, hl * 0.34f ),
				new Vector2( -hw * 0.12f, hl * 0.30f ),
				new Vector2( hw * 0.14f, -hl * 0.30f ),
				new Vector2( -hw * 0.32f, hl * 0.08f ),
				new Vector2( hw * 0.33f, -hl * 0.07f )
			],
			crate,
			AimboxArenaPalette.Crate,
			"Rail Crate" );

		var tie = new Vector3( t * 4.8f, t * 0.62f, waist * 0.4f );
		Block( root, "Rail Tie North", Ground( new Vector3( hw * 0.18f, hl * 0.64f, 0 ), tie ), tie, AimboxArenaPalette.Wood, Rotation.FromYaw( 5f ) );
		Block( root, "Rail Tie South", Ground( new Vector3( -hw * 0.18f, -hl * 0.64f, 0 ), tie ), tie, AimboxArenaPalette.Wood, Rotation.FromYaw( 5f ) );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaSurface.CorrugatedMetal,
			AimboxArenaPalette.Crate,
			AimboxArenaPalette.Metal );

		AimboxMapBuilderCommon.BuildSpawnAlcoves( root, cfg );
		AimboxMapBuilderCommon.BuildSpawnAccents( root, cfg );
	}

	static Vector3 Ground( Vector3 position, Vector3 size ) => AimboxMapBuilderCommon.OnGround( position, size );
	static void Block( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxMapBuilderCommon.AddBlock( root, name, center, size, surface, rotation );
}
