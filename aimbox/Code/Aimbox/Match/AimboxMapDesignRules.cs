namespace Sandbox;

/// <summary>Shared layout, pacing, and palette for AIMBOX tactical maps.</summary>
public static class AimboxMapDesignRules
{
	public const int SpawnsPerTeam = 8;
	public const float UnitsPerMeter = 50f;
	public const float PlatformElevationMeters = 1.35f;
	public const float RespawnDelaySeconds = 1f;
	public const float FreezeTimeSeconds = 1f;
	public const float FloorSlabThickness = 8f;

	public static float FloorWalkZ => FloorSlabThickness;

	public static AimboxMapLayout CreateLayout( float widthMeters, float depthMeters )
	{
		var u = UnitsPerMeter;
		var halfWidth = widthMeters * 0.5f * u;
		var halfLength = depthMeters * 0.5f * u;
		return new AimboxMapLayout
		{
			ArenaHalfWidth = halfWidth,
			ArenaHalfLength = halfLength,
			WallHeight = 3.6f * u,
			WallThickness = 0.45f * u,
			LaneDividerHeight = 2.1f * u,
			WaistCoverHeight = 1.2f * u,
			TallCoverHeight = 1.95f * u,
			PlatformElevation = PlatformElevationMeters * u,
			SpawnInset = 1.6f * u,
			SpawnSpreadY = halfLength * 0.68f
		};
	}

	public static float CombatScale( AimboxMapLayout layout ) =>
		layout.ArenaHalfWidth / (520f * AimboxArenaConfig.MapScale);

	public static IReadOnlyList<Vector2> CreateFfaSpawnPositions( AimboxMapLayout layout )
	{
		var laneY = layout.ArenaHalfLength * 0.33f;
		var laneOffsetX = layout.ArenaHalfLength * 0.09f;

		return
		[
			new Vector2( laneOffsetX, layout.ArenaHalfLength * 0.72f ),
			new Vector2( -laneOffsetX, -layout.ArenaHalfLength * 0.72f ),
			new Vector2( layout.ArenaHalfWidth * 0.42f, laneY * 0.42f ),
			new Vector2( -layout.ArenaHalfWidth * 0.42f, -laneY * 0.42f ),
			new Vector2( layout.BlueSpawnX * 0.55f, laneY ),
			new Vector2( layout.RedSpawnX * 0.55f, laneY ),
			new Vector2( layout.BlueSpawnX * 0.55f, -laneY ),
			new Vector2( layout.RedSpawnX * 0.55f, -laneY )
		];
	}
}

public readonly struct AimboxMapLayout
{
	public float ArenaHalfWidth { get; init; }
	public float ArenaHalfLength { get; init; }
	public float WallHeight { get; init; }
	public float WallThickness { get; init; }
	public float LaneDividerHeight { get; init; }
	public float WaistCoverHeight { get; init; }
	public float TallCoverHeight { get; init; }
	public float PlatformElevation { get; init; }
	public float SpawnInset { get; init; }
	public float SpawnSpreadY { get; init; }

	public float RedSpawnX => -(ArenaHalfWidth - SpawnInset);
	public float BlueSpawnX => ArenaHalfWidth - SpawnInset;
}

public static class AimboxArenaPalette
{
	public const AimboxArenaSurface Floor = AimboxArenaSurface.Concrete;
	public const AimboxArenaSurface Wall = AimboxArenaSurface.Concrete;
	public const AimboxArenaSurface Metal = AimboxArenaSurface.SheetMetal;
	public const AimboxArenaSurface MidCover = AimboxArenaSurface.Wood;
	public const AimboxArenaSurface Building = AimboxArenaSurface.Brick;
	public const AimboxArenaSurface BuildingAlt = AimboxArenaSurface.StoneBrick;
	public const AimboxArenaSurface BuildingTrim = AimboxArenaSurface.BarnWood;
	public const AimboxArenaSurface BarnWood = AimboxArenaSurface.BarnWood;
	public const AimboxArenaSurface Road = AimboxArenaSurface.Asphalt;
	public const AimboxArenaSurface Barrier = AimboxArenaSurface.CorrugatedMetal;
	public const AimboxArenaSurface Crate = AimboxArenaSurface.Wood;
	public const AimboxArenaSurface Gravel = AimboxArenaSurface.Gravel;
	public const AimboxArenaSurface Tile = AimboxArenaSurface.Tile;
	public const AimboxArenaSurface Wood = AimboxArenaSurface.Wood;
	public const AimboxArenaSurface Asphalt = AimboxArenaSurface.Asphalt;
	public const AimboxArenaSurface Concrete = AimboxArenaSurface.Concrete;

	public static readonly Color RedAccent = new( 0.86f, 0.16f, 0.14f );
	public static readonly Color BlueAccent = new( 0.16f, 0.38f, 0.88f );
	public static readonly Color LedStrip = new( 0.72f, 0.86f, 1f );
	public static readonly Color ContainerRed = new( 0.62f, 0.18f, 0.14f );

	public static Color FallbackTint( AimboxArenaSurface surface ) => surface switch
	{
		AimboxArenaSurface.Concrete => new Color( 0.62f, 0.62f, 0.64f ),
		AimboxArenaSurface.ConcreteDark => new Color( 0.34f, 0.34f, 0.36f ),
		AimboxArenaSurface.Asphalt => new Color( 0.28f, 0.28f, 0.3f ),
		AimboxArenaSurface.Gravel => new Color( 0.52f, 0.5f, 0.46f ),
		AimboxArenaSurface.Tile => new Color( 0.72f, 0.7f, 0.66f ),
		AimboxArenaSurface.CorrugatedMetal => new Color( 0.58f, 0.6f, 0.64f ),
		AimboxArenaSurface.Wood => new Color( 0.48f, 0.34f, 0.2f ),
		AimboxArenaSurface.BarnWood => new Color( 0.72f, 0.58f, 0.46f ),
		AimboxArenaSurface.Brick => new Color( 0.58f, 0.36f, 0.28f ),
		AimboxArenaSurface.StoneBrick => new Color( 0.58f, 0.64f, 0.72f ),
		AimboxArenaSurface.Metal => new Color( 0.56f, 0.58f, 0.62f ),
		AimboxArenaSurface.SheetMetal => new Color( 0.62f, 0.64f, 0.68f ),
		_ => new Color( 0.55f, 0.55f, 0.58f )
	};
}
