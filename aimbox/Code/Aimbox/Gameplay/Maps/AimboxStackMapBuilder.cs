namespace Sandbox;

/// <summary>Container yard arena with staggered stacks and tight three-lane route choices.</summary>
public static class AimboxStackMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Asphalt );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaPalette.Barrier );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;

		var longContainer = new Vector3( hw * 0.48f, t * 2.65f, tall * 1.16f );
		TintBlock( root, "Red Container North", Ground( new Vector3( -hw * 0.32f, hl * 0.40f, 0 ), longContainer ), longContainer, AimboxArenaSurface.CorrugatedMetal, AimboxArenaPalette.ContainerRed );
		Block( root, "Blue Container South", Ground( new Vector3( hw * 0.32f, -hl * 0.40f, 0 ), longContainer ), longContainer, AimboxArenaSurface.SheetMetal );
		Block( root, "Center Container East", Ground( new Vector3( hw * 0.21f, hl * 0.02f, 0 ), longContainer ), longContainer, AimboxArenaSurface.SheetMetal );
		TintBlock( root, "Center Container West", Ground( new Vector3( -hw * 0.21f, -hl * 0.02f, 0 ), longContainer ), longContainer, AimboxArenaSurface.CorrugatedMetal, AimboxArenaPalette.ContainerRed );

		var crossContainer = new Vector3( t * 2.8f, hl * 0.37f, tall * 1.08f );
		Block( root, "Vertical Stack North", Ground( new Vector3( t * 0.5f, hl * 0.34f, 0 ), crossContainer ), crossContainer, AimboxArenaPalette.Metal );
		Block( root, "Vertical Stack South", Ground( new Vector3( -t * 0.5f, -hl * 0.34f, 0 ), crossContainer ), crossContainer, AimboxArenaPalette.Metal );

		var gate = new Vector3( t * 1.05f, hl * 0.27f, lane );
		Block( root, "Yard Gate West", Ground( new Vector3( -hw * 0.55f, hl * 0.02f, 0 ), gate ), gate, AimboxArenaPalette.Barrier );
		Block( root, "Yard Gate East", Ground( new Vector3( hw * 0.55f, -hl * 0.02f, 0 ), gate ), gate, AimboxArenaPalette.Barrier );

		var barrier = new Vector3( t * 2.1f, t * 1.5f, waist );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.48f, -hl * 0.30f ),
				new Vector2( hw * 0.48f, hl * 0.30f ),
				new Vector2( -hw * 0.10f, hl * 0.58f ),
				new Vector2( hw * 0.10f, -hl * 0.58f ),
				new Vector2( -hw * 0.04f, -hl * 0.22f ),
				new Vector2( hw * 0.04f, hl * 0.22f )
			],
			barrier,
			AimboxArenaPalette.Barrier,
			"Lane Barrier" );

		var pallet = AimboxMapBuilderCommon.LooseCoverSize( new Vector3( t * 2.2f, t * 1.8f, waist * 0.7f ) );
		Block( root, "Pallet North Pocket", Ground( new Vector3( hw * 0.30f, hl * 0.58f, 0 ), pallet ), pallet, AimboxArenaPalette.Crate );
		Block( root, "Pallet South Pocket", Ground( new Vector3( -hw * 0.30f, -hl * 0.58f, 0 ), pallet ), pallet, AimboxArenaPalette.Crate );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaSurface.SheetMetal,
			AimboxArenaPalette.Crate,
			AimboxArenaPalette.Barrier,
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
