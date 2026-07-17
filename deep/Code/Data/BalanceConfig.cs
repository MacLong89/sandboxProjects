namespace Deep;

/// <summary>Centralized editable balance for DEEP.</summary>
public sealed class BalanceConfig
{
	public static BalanceConfig Defaults { get; } = CreateDefaults();

	public float SurfaceZ { get; set; } = 0f;
	public float SurfaceEpsilon { get; set; } = 0.35f;
	public float UnitsPerMeter { get; set; } = 1f;
	public float MaxOceanDepthMeters { get; set; } = 400f;
	public float ZoneSunlitEnd { get; set; } = 50f;
	public float ZoneBlueEnd { get; set; } = 120f;
	public float ZoneTwilightEnd { get; set; } = 200f;
	public float ZoneMidnightEnd { get; set; } = 280f;
	public float ZoneAbyssalEnd { get; set; } = 340f;

	public float SurfaceSpawnX { get; set; } = 0f;
	public float SurfaceSpawnZ { get; set; } = 1.5f;
	public float DiveStartZ { get; set; } = -2.5f;
	public float BoatReturnRadius { get; set; } = 14f;

	public float SeabedLeftX { get; set; } = -48f;
	public float SeabedRightX { get; set; } = 48f;
	public float SeabedBaseFloorZ { get; set; } = -380f;
	public float SeabedHillAmplitude { get; set; } = 22f;
	public float SeabedSwimClearance { get; set; } = 2.4f;

	public float MaxOxygenSeconds { get; set; } = 28f;
	public float LowOxygenFraction { get; set; } = 0.25f;
	public float CriticalOxygenFraction { get; set; } = 0.1f;
	public float OxygenDepthUsePer100m { get; set; } = 0.22f;

	public float SwimSpeed { get; set; } = 9f;
	public float AscentSpeed { get; set; } = 10f;
	public float DescentSpeed { get; set; } = 11f;
	public float MoveAcceleration { get; set; } = 32f;
	public float MoveDeceleration { get; set; } = 26f;
	public float BoostMaxEnergy { get; set; } = 40f;
	public float BoostDrainPerSecond { get; set; } = 34f;
	public float BoostRegenPerSecond { get; set; } = 9f;

	public float HorizontalHalfWidth { get; set; } = 48f;

	public float CamDistance { get; set; } = 55f;
	public float CamFollowSpeed { get; set; } = 8f;
	public float CamLookAheadScale { get; set; } = 0.35f;
	public float CamMaxLookAhead { get; set; } = 12f;
	public float CamFov { get; set; } = 55f;

	public float DiveSuccessHoldSeconds { get; set; } = 2.4f;
	public float DiveFailedHoldSeconds { get; set; } = 2.2f;
	public float StatusMessageSeconds { get; set; } = 2.2f;

	public float DiverSpriteWorldHeight { get; set; } = 3.4f;
	public float BoatSpriteWorldWidth { get; set; } = 18f;
	public float PropSpriteWorldHeight { get; set; } = 10f;
	public float BackdropY { get; set; } = -8f;
	public float OceanBackdropY { get; set; } = -28f;
	public float OceanBackdropWidth { get; set; } = 120f;
	public float OceanBackdropHeight { get; set; } = 80f;

	public int BaseHaulCapacity { get; set; } = 4;
	public float CollectPickupRadius { get; set; } = 2.4f;
	public int CollectibleSpawnCount { get; set; } = 52;

	public float MaxHealth { get; set; } = 55f;
	public float SafeDepthMeters { get; set; } = 18f;
	public float PressureWarnMarginMeters { get; set; } = 8f;
	public float PressureDamagePerSecond { get; set; } = 10f;
	public float HazardDamage { get; set; } = 22f;
	public float HazardCooldownSeconds { get; set; } = 0.85f;
	public int HazardSpawnCount { get; set; } = 18;

	public float VisibilityBonus { get; set; }
	public float ScannerRangeBonus { get; set; }

	public float MinWorldZ => -MaxOceanDepthMeters;

	public float DepthFromWorldZ( float worldZ ) =>
		MathF.Max( 0f, -worldZ / MathF.Max( UnitsPerMeter, 0.0001f ) );

	public float WorldZFromDepth( float depthMeters ) =>
		-depthMeters * UnitsPerMeter;

	public DepthZone ZoneAtDepth( float depthMeters )
	{
		if ( depthMeters < ZoneSunlitEnd ) return DepthZone.Sunlit;
		if ( depthMeters < ZoneBlueEnd ) return DepthZone.BlueDepths;
		if ( depthMeters < ZoneTwilightEnd ) return DepthZone.Twilight;
		if ( depthMeters < ZoneMidnightEnd ) return DepthZone.Midnight;
		if ( depthMeters < ZoneAbyssalEnd ) return DepthZone.Abyssal;
		return DepthZone.Hadal;
	}

	public float OxygenUseMultiplierAtDepth( float depthMeters ) =>
		1f + (depthMeters / 100f) * OxygenDepthUsePer100m;

	public static BalanceConfig CreateDefaults() => new();
}
