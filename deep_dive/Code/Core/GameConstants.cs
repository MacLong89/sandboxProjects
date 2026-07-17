namespace DeepDive;

/// <summary>
/// Compatibility façade over <see cref="BalanceConfig.Defaults"/>.
/// Prefer reading <see cref="DeepDiveGame.Balance"/> at runtime so upgrades can mutate values later.
/// </summary>
public static class GameConstants
{
	private static BalanceConfig B => DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;

	public static float SurfaceZ => B.SurfaceZ;
	public static float SurfaceEpsilon => B.SurfaceEpsilon;
	public static float UnitsPerMeter => B.UnitsPerMeter;
	public static float MaxOceanDepthMeters => B.MaxOceanDepthMeters;

	public static float ZoneSunlitEnd => B.ZoneSunlitEnd;
	public static float ZoneBlueEnd => B.ZoneBlueEnd;
	public static float ZoneTwilightEnd => B.ZoneTwilightEnd;

	public static float SurfaceSpawnX => B.SurfaceSpawnX;
	public static float SurfaceSpawnZ => B.SurfaceSpawnZ;
	public static float DiveStartZ => B.DiveStartZ;

	public static float MaxOxygenSeconds => B.MaxOxygenSeconds;
	public static float LowOxygenFraction => B.LowOxygenFraction;

	public static float SwimSpeed => B.SwimSpeed;
	public static float AscentSpeed => B.AscentSpeed;
	public static float DescentSpeed => B.DescentSpeed;
	public static float MoveAcceleration => B.MoveAcceleration;
	public static float MoveDeceleration => B.MoveDeceleration;

	public static float HorizontalHalfWidth => B.HorizontalHalfWidth;
	public static float MinWorldZ => B.MinWorldZ;

	public static float CamDistance => B.CamDistance;
	public static float CamFollowSpeed => B.CamFollowSpeed;
	public static float CamLookAheadScale => B.CamLookAheadScale;
	public static float CamMaxLookAhead => B.CamMaxLookAhead;
	public static float CamFov => B.CamFov;
	public const float CamZNear = 1f;
	public const float CamZFar = 500f;

	public static float DiveSuccessHoldSeconds => B.DiveSuccessHoldSeconds;
	public static float DiveFailedHoldSeconds => B.DiveFailedHoldSeconds;
	public static float StatusMessageSeconds => B.StatusMessageSeconds;

	public static float DiverSpriteWorldHeight => B.DiverSpriteWorldHeight;
	public static float BoatSpriteWorldWidth => B.BoatSpriteWorldWidth;
	public static float PropSpriteWorldHeight => B.PropSpriteWorldHeight;
	public static float BackdropY => B.BackdropY;
	public static float OceanBackdropY => B.OceanBackdropY;

	public static int BaseHaulCapacity => B.BaseHaulCapacity;
	public static float MaxHealth => B.MaxHealth;
	public static float SafeDepthMeters => B.SafeDepthMeters;

	public static Color WaterSunlit => new( 0.36f, 0.78f, 0.82f );
	public static Color WaterBlue => new( 0.12f, 0.28f, 0.55f );
	public static Color WaterTwilight => new( 0.04f, 0.07f, 0.16f );
	public static Color AccentYellow => new( 0.96f, 0.77f, 0.26f );
	public static Color AccentLime => new( 0.60f, 0.76f, 0.24f );
	public static Color AccentOrange => new( 0.90f, 0.49f, 0.13f );
	public static Color AccentRed => new( 0.91f, 0.30f, 0.24f );

	public static float DepthFromWorldZ( float worldZ ) => B.DepthFromWorldZ( worldZ );
	public static float WorldZFromDepth( float depthMeters ) => B.WorldZFromDepth( depthMeters );
	public static DepthZone ZoneAtDepth( float depthMeters ) => B.ZoneAtDepth( depthMeters );
}

public enum DepthZone
{
	Sunlit,
	BlueDepths,
	Twilight,
	Midnight,
	Abyssal,
	Hadal
}
