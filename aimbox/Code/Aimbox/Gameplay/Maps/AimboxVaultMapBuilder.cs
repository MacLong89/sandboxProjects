namespace Sandbox;

/// <summary>Bank interior arena built around a central vault and broken teller lanes.</summary>
public static class AimboxVaultMapBuilder
{
	public static void Build( GameObject root, AimboxMapLayout cfg )
	{
		var floor = AimboxMapDesignRules.FloorSlabThickness;
		AimboxMapBuilderCommon.BuildFloorSlab( root, cfg, floor, AimboxArenaPalette.Tile );
		AimboxMapBuilderCommon.BuildPerimeter( root, cfg, AimboxArenaSurface.Brick );

		var t = cfg.WallThickness;
		var hw = cfg.ArenaHalfWidth;
		var hl = cfg.ArenaHalfLength;
		var waist = cfg.WaistCoverHeight;
		var tall = cfg.TallCoverHeight;
		var lane = cfg.LaneDividerHeight;

		var vaultCore = new Vector3( t * 4.1f, t * 4.1f, tall * 1.04f );
		Block( root, "Vault Core", Ground( Vector3.Zero, vaultCore ), vaultCore, AimboxArenaSurface.ConcreteDark );

		var segment = new Vector3( t * 5.4f, t * 1.25f, lane );
		Block( root, "Vault Door North", Ground( new Vector3( -t * 0.35f, t * 4.05f, 0 ), segment ), segment, AimboxArenaPalette.Metal );
		Block( root, "Vault Door South", Ground( new Vector3( t * 0.35f, -t * 4.05f, 0 ), segment ), segment, AimboxArenaPalette.Metal );
		Block( root, "Vault Door West", Ground( new Vector3( -t * 4.05f, -t * 0.35f, 0 ), segment ), segment, AimboxArenaPalette.Metal, Rotation.FromYaw( 90f ) );
		Block( root, "Vault Door East", Ground( new Vector3( t * 4.05f, t * 0.35f, 0 ), segment ), segment, AimboxArenaPalette.Metal, Rotation.FromYaw( 90f ) );

		var teller = new Vector3( hw * 0.36f, t * 1.2f, waist );
		Block( root, "Teller Counter NW", Ground( new Vector3( -hw * 0.25f, hl * 0.56f, 0 ), teller ), teller, AimboxArenaPalette.BarnWood );
		Block( root, "Teller Counter NE", Ground( new Vector3( hw * 0.29f, hl * 0.51f, 0 ), teller ), teller, AimboxArenaPalette.BarnWood );
		Block( root, "Teller Counter SW", Ground( new Vector3( -hw * 0.30f, -hl * 0.51f, 0 ), teller ), teller, AimboxArenaPalette.BarnWood );
		Block( root, "Teller Counter SE", Ground( new Vector3( hw * 0.24f, -hl * 0.56f, 0 ), teller ), teller, AimboxArenaPalette.BarnWood );

		var office = new Vector3( t * 1.05f, hl * 0.28f, lane );
		Block( root, "Office Wall West N", Ground( new Vector3( -hw * 0.46f, hl * 0.18f, 0 ), office ), office, AimboxArenaSurface.StoneBrick );
		Block( root, "Office Wall West S", Ground( new Vector3( -hw * 0.40f, -hl * 0.22f, 0 ), office ), office, AimboxArenaSurface.StoneBrick );
		Block( root, "Office Wall East N", Ground( new Vector3( hw * 0.40f, hl * 0.22f, 0 ), office ), office, AimboxArenaSurface.StoneBrick );
		Block( root, "Office Wall East S", Ground( new Vector3( hw * 0.46f, -hl * 0.18f, 0 ), office ), office, AimboxArenaSurface.StoneBrick );

		var pillar = new Vector3( t * 1.55f, t * 1.55f, cfg.WallHeight * 0.82f );
		Block( root, "Marble Pillar NW", Ground( new Vector3( -hw * 0.24f, hl * 0.25f, 0 ), pillar ), pillar, AimboxArenaSurface.StoneBrick );
		Block( root, "Marble Pillar NE", Ground( new Vector3( hw * 0.24f, hl * 0.25f, 0 ), pillar ), pillar, AimboxArenaSurface.StoneBrick );
		Block( root, "Marble Pillar SW", Ground( new Vector3( -hw * 0.24f, -hl * 0.25f, 0 ), pillar ), pillar, AimboxArenaSurface.StoneBrick );
		Block( root, "Marble Pillar SE", Ground( new Vector3( hw * 0.24f, -hl * 0.25f, 0 ), pillar ), pillar, AimboxArenaSurface.StoneBrick );

		var desk = new Vector3( t * 2.4f, t * 1.65f, waist * 0.82f );
		AimboxMapBuilderCommon.ScatterCover(
			root,
			cfg,
			[
				new Vector2( -hw * 0.58f, hl * 0.04f ),
				new Vector2( hw * 0.58f, -hl * 0.04f ),
				new Vector2( -hw * 0.10f, hl * 0.44f ),
				new Vector2( hw * 0.12f, -hl * 0.44f ),
				new Vector2( -hw * 0.12f, -hl * 0.43f ),
				new Vector2( hw * 0.10f, hl * 0.43f )
			],
			desk,
			AimboxArenaPalette.Wood,
			"Office Desk" );

		AimboxMapBuilderCommon.BuildPerimeterDressing(
			root,
			cfg,
			AimboxArenaSurface.StoneBrick,
			AimboxArenaPalette.Wood,
			AimboxArenaSurface.Brick );

		AimboxMapBuilderCommon.BuildSpawnAlcoves( root, cfg, AimboxArenaSurface.Brick );
		AimboxMapBuilderCommon.BuildSpawnAccents( root, cfg );
	}

	static Vector3 Ground( Vector3 position, Vector3 size ) => AimboxMapBuilderCommon.OnGround( position, size );
	static void Block( GameObject root, string name, Vector3 center, Vector3 size, AimboxArenaSurface surface, Rotation rotation = default ) =>
		AimboxMapBuilderCommon.AddBlock( root, name, center, size, surface, rotation );
}
