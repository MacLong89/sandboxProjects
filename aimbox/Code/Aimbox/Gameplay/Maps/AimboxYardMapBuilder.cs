namespace Sandbox;

/// <summary>Backyard arena with two house-side lanes, a garden spine, and short crate routes.</summary>
public static class AimboxYardMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Gravel );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaPalette.BarnWood );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;

		var gardenWall = new Vector3( t * 0.9f, hl * 0.42f, lane );
		Block( root, "Garden Spine North", Ground( new Vector3( -t * 0.5f, hl * 0.19f, 0 ), gardenWall ), gardenWall, AimboxArenaPalette.BarnWood );
		Block( root, "Garden Spine South", Ground( new Vector3( t * 0.5f, -hl * 0.22f, 0 ), gardenWall ), gardenWall, AimboxArenaPalette.BarnWood );

		var shed = new Vector3( t * 4.7f, t * 3.9f, tall * 1.08f );
		Block( root, "North Tool Shed", Ground( new Vector3( -hw * 0.36f, hl * 0.39f, 0 ), shed ), shed, AimboxArenaPalette.BarnWood );
		Block( root, "South Patio Shed", Ground( new Vector3( hw * 0.36f, -hl * 0.38f, 0 ), shed ), shed, AimboxArenaPalette.BuildingTrim );

		var fence = new Vector3( hw * 0.28f, t * 0.7f, lane );
		Block( root, "North Fence Left", Ground( new Vector3( -hw * 0.22f, hl * 0.62f, 0 ), fence ), fence, AimboxArenaPalette.BarnWood );
		Block( root, "North Fence Right", Ground( new Vector3( hw * 0.28f, hl * 0.58f, 0 ), fence ), fence, AimboxArenaPalette.BarnWood );
		Block( root, "South Planter Left", Ground( new Vector3( -hw * 0.28f, -hl * 0.57f, 0 ), fence ), fence, AimboxArenaPalette.Wood );
		Block( root, "South Planter Right", Ground( new Vector3( hw * 0.22f, -hl * 0.62f, 0 ), fence ), fence, AimboxArenaPalette.Wood );

		var patio = new Vector3( t * 4.2f, t * 2.3f, waist );
		Block( root, "Brick Grill", Ground( new Vector3( hw * 0.54f, hl * 0.12f, 0 ), patio ), patio, AimboxArenaSurface.Brick );
		Block( root, "Garden Bench", Ground( new Vector3( -hw * 0.52f, -hl * 0.12f, 0 ), patio ), patio, AimboxArenaPalette.Wood );

		var crate = new Vector3( t * 2.15f, t * 2.0f, waist );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.19f, hl * 0.03f ),
				new Vector2( hw * 0.17f, -hl * 0.02f ),
				new Vector2( -hw * 0.14f, -hl * 0.39f ),
				new Vector2( hw * 0.12f, hl * 0.36f ),
				new Vector2( -hw * 0.46f, hl * 0.18f ),
				new Vector2( hw * 0.45f, -hl * 0.22f )
			],
			crate,
			AimboxArenaPalette.Crate,
			"Yard Crate" );

		var planter = new Vector3( t * 1.6f, t * 4.2f, waist * 0.78f );
		Block( root, "East Flower Bed", Ground( new Vector3( hw * 0.24f, hl * 0.03f, 0 ), planter ), planter, AimboxArenaPalette.BarnWood, Rotation.FromYaw( 7f ) );
		Block( root, "West Flower Bed", Ground( new Vector3( -hw * 0.24f, -hl * 0.03f, 0 ), planter ), planter, AimboxArenaPalette.BarnWood, Rotation.FromYaw( -7f ) );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaPalette.BarnWood,
			AimboxArenaPalette.Crate,
			AimboxArenaPalette.BarnWood );

		AimboxMapBuilderCommon.BuildSpawnAlcoves( root, cfg, AimboxArenaPalette.BarnWood );
		AimboxMapBuilderCommon.BuildSpawnAccents( root, cfg );
	}

	static Vector3 Ground( Vector3 position, Vector3 size ) => AimboxMapBuilderCommon.OnGround( position, size );
	static void Block( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxMapBuilderCommon.AddBlock( root, name, center, size, surface, rotation );
}
