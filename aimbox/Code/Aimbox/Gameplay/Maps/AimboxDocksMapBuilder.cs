namespace Sandbox;

/// <summary>Shipping pier arena with offset containers, forklift cover, and broken dock lanes.</summary>
public static class AimboxDocksMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Road );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaPalette.Metal );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;

		var container = new Vector3( t * 8.4f, t * 2.6f, tall * 1.12f );
		TintBlock( root, "Container Mid A", Ground( new Vector3( -hw * 0.18f, hl * 0.08f, 0 ), container ), container, AimboxArenaSurface.CorrugatedMetal, AimboxArenaPalette.ContainerRed );
		TintBlock( root, "Container Mid B", Ground( new Vector3( hw * 0.22f, -hl * 0.12f, 0 ), container ), container, AimboxArenaSurface.CorrugatedMetal, AimboxArenaPalette.ContainerRed );

		var sideContainer = new Vector3( t * 3.0f, hl * 0.35f, tall * 1.05f );
		Block( root, "Container North Stack", Ground( new Vector3( -hw * 0.02f, hl * 0.48f, 0 ), sideContainer ), sideContainer, AimboxArenaSurface.SheetMetal );
		Block( root, "Container South Stack", Ground( new Vector3( hw * 0.04f, -hl * 0.48f, 0 ), sideContainer ), sideContainer, AimboxArenaSurface.SheetMetal );

		var dock = new Vector3( t * 4.8f, hl * 0.82f, t * 0.62f );
		Block( root, "West Loading Dock", Ground( new Vector3( -hw * 0.58f, 0, 0 ), dock ), dock, AimboxArenaPalette.Concrete );

		var crane = new Vector3( t * 4.1f, t * 4.1f, tall );
		Block( root, "Crane Base", Ground( new Vector3( hw * 0.55f, hl * 0.28f, 0 ), crane ), crane, AimboxArenaPalette.Metal );

		var fence = new Vector3( hw * 0.22f, t * 0.75f, lane );
		Block( root, "Pier Rail North", Ground( new Vector3( hw * 0.27f, hl * 0.63f, 0 ), fence ), fence, AimboxArenaPalette.Barrier );
		Block( root, "Pier Rail South", Ground( new Vector3( -hw * 0.27f, -hl * 0.63f, 0 ), fence ), fence, AimboxArenaPalette.Barrier );

		var pallet = new Vector3( t * 2.15f, t * 2.0f, waist * 0.8f );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.34f, hl * 0.31f ),
				new Vector2( hw * 0.36f, -hl * 0.34f ),
				new Vector2( -hw * 0.08f, -hl * 0.29f ),
				new Vector2( hw * 0.09f, hl * 0.30f ),
				new Vector2( -hw * 0.46f, -hl * 0.08f ),
				new Vector2( hw * 0.47f, hl * 0.04f )
			],
			pallet,
			AimboxArenaPalette.Crate,
			"Pallet Stack" );

		var forklift = new Vector3( t * 3.3f, t * 2.3f, waist );
		Block( root, "Forklift Cage East", Ground( new Vector3( hw * 0.25f, hl * 0.05f, 0 ), forklift ), forklift, AimboxArenaPalette.Barrier, Rotation.FromYaw( -12f ) );
		Block( root, "Forklift Cage West", Ground( new Vector3( -hw * 0.25f, -hl * 0.05f, 0 ), forklift ), forklift, AimboxArenaPalette.Barrier, Rotation.FromYaw( 12f ) );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaSurface.CorrugatedMetal,
			AimboxArenaPalette.Crate,
			AimboxArenaPalette.Metal,
			AimboxArenaPalette.ContainerRed );

		AimboxMapBuilderCommon.BuildSpawnAlcoves( root, cfg );
		AimboxMapBuilderCommon.BuildSpawnAccents( root, cfg );
	}

	static Vector3 Ground( Vector3 position, Vector3 size ) => AimboxMapBuilderCommon.OnGround( position, size );
	static void Block( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxMapBuilderCommon.AddBlock( root, name, center, size, surface, rotation );
	static void TintBlock( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Color tint, Rotation rotation = default ) =>
		AimboxArenaGeometry.AddBlock( root, name, center, size, surface, tint, rotation );
}
