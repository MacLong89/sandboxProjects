using System;
using System.Linq;

namespace HeightsHotel;

/// <summary>
/// Central tunable values. Money is integer cents.
/// </summary>
public static class GameBalance
{
	public const int SaveVersion = 2;
	public const float TickSeconds = 0.25f;
	public const int CellWidth = 96;
	public const int CellHeight = 64;
	public const float SecondsPerDay = 120f;

	public const long StartingCashCents = 2_500_00; // $2,500.00
	public const float CostEscalationPerRoom = 0.08f;
	public const float DemolishRefundFraction = 0.40f;

	public const float OfflineEfficiency = 0.60f;
	public const float MaxOfflineHours = 8f;

	public const int MaxRoomLevel = 5;
	public const float UpgradeRateBonusPerLevel = 0.18f;
	public const float UpgradeCostMultiplierPerLevel = 1.55f;

	public const long WageTickDivisor = 240; // wages listed per minute; tick = 0.25s → 240 ticks/min

	public const float BaseGuestSpawnChancePerTick = 0.035f;
	public const float SatisfactionDemandWeight = 0.4f;
	public const float DirtPerOccupiedTick = 0.004f;
	public const float BreakChancePerOccupiedTick = 0.00015f;
	public const float AmenityBreakChancePerTick = 0.00005f;
	public const float AmenityVisitChancePerTick = 0.02f;
	public const float GuestCheckInPatience = 18f;
	public const float GuestMinStaySeconds = 14f;
	public const float GuestMaxStaySeconds = 38f;
	public const float GuestExpectationPenaltyPerRep = 0.015f;

	public const float LaundryCleanSpeedBonus = 0.35f;
	public const float WorkshopRepairSpeedBonus = 0.35f;
	public const float StaffRoomWageDiscount = 0.08f;

	public const long TipBaseCents = 50;
	public const float TipSatisfactionThreshold = 0.75f;

	public const float MinWeatherDuration = 40f;
	public const float MaxWeatherDuration = 90f;

	public static readonly float[] SpeedMultipliers = { 0f, 1f, 2f, 4f };

	public static readonly RoomDef[] Rooms =
	{
		new( RoomType.Lobby, "Lobby", RoomCategory.Structure, 0, 0, 0, 0, 0, 1 ),
		new( RoomType.StandardRoom, "Standard Room", RoomCategory.Lodging, 450_00, 2, 85_00, 8_00, 1, 1 ),
		new( RoomType.DeluxeRoom, "Deluxe Room", RoomCategory.Lodging, 900_00, 2, 140_00, 14_00, 2, 2 ),
		new( RoomType.Suite, "Suite", RoomCategory.Lodging, 2_200_00, 3, 260_00, 28_00, 5, 3 ),
		new( RoomType.Cafe, "Café", RoomCategory.Amenity, 600_00, 4, 35_00, 12_00, 1, 0 ),
		new( RoomType.Restaurant, "Restaurant", RoomCategory.Amenity, 1_400_00, 6, 70_00, 22_00, 3, 0 ),
		new( RoomType.Spa, "Spa", RoomCategory.Amenity, 1_800_00, 3, 110_00, 24_00, 4, 0 ),
		new( RoomType.GiftShop, "Gift Shop", RoomCategory.Amenity, 800_00, 4, 45_00, 12_00, 2, 0 ),
		new( RoomType.Laundry, "Laundry", RoomCategory.Support, 700_00, 0, 0, 10_00, 2, 0 ),
		new( RoomType.MaintenanceWorkshop, "Workshop", RoomCategory.Support, 850_00, 0, 0, 12_00, 4, 0 ),
		new( RoomType.StaffRoom, "Staff Room", RoomCategory.Support, 650_00, 0, 0, 8_00, 3, 0 ),
	};

	public static readonly StaffDef[] Staff =
	{
		new( StaffRole.Receptionist, "Receptionist", 200_00, 18_00, 1 ),
		new( StaffRole.Housekeeper, "Housekeeper", 180_00, 16_00, 1 ),
		new( StaffRole.Cook, "Cook", 320_00, 28_00, 1 ),
		new( StaffRole.MaintenanceWorker, "Maintenance", 300_00, 26_00, 4 ),
	};

	/// <summary>Lifetime profit cents required to reach each reputation level (index = level).</summary>
	public static readonly long[] ReputationProfitThresholds =
	{
		0,
		0,           // 1
		1_500_00,    // 2
		5_000_00,    // 3
		12_000_00,   // 4
		30_000_00,   // 5
		75_000_00,   // 6
	};

	/// <summary>Extra profit per soft prestige level beyond 6.</summary>
	public const long SoftPrestigeProfitStep = 50_000_00;

	public static RoomDef GetRoom( RoomType type ) => Rooms.First( r => r.Type == type );
	public static StaffDef GetStaff( StaffRole role ) => Staff.First( s => s.Role == role );

	public static bool CanAssignStaff( StaffRole role, RoomType room ) => role switch
	{
		StaffRole.Receptionist => room is RoomType.Lobby or RoomType.GiftShop or RoomType.Spa,
		StaffRole.Housekeeper => room is RoomType.StandardRoom or RoomType.DeluxeRoom or RoomType.Suite
			or RoomType.Laundry or RoomType.StaffRoom or RoomType.Spa,
		StaffRole.Cook => room is RoomType.Cafe or RoomType.Restaurant,
		StaffRole.MaintenanceWorker => room != RoomType.Lobby,
		_ => false
	};

	public static StaffRole PreferredStaffRole( RoomType room ) => room switch
	{
		RoomType.Lobby => StaffRole.Receptionist,
		RoomType.StandardRoom or RoomType.DeluxeRoom or RoomType.Suite => StaffRole.Housekeeper,
		RoomType.Cafe or RoomType.Restaurant => StaffRole.Cook,
		RoomType.GiftShop => StaffRole.Receptionist,
		RoomType.Laundry or RoomType.StaffRoom => StaffRole.Housekeeper,
		RoomType.Spa => StaffRole.Receptionist,
		RoomType.MaintenanceWorkshop => StaffRole.MaintenanceWorker,
		_ => StaffRole.MaintenanceWorker
	};

	public static long BuildCost( RoomType type, int existingRoomCount )
	{
		var def = GetRoom( type );
		if ( def.Category == RoomCategory.Structure )
			return 0;
		var mult = 1f + CostEscalationPerRoom * existingRoomCount;
		return (long)(def.BaseBuildCostCents * mult);
	}

	public static long DemolishRefund( RoomType type, int level, int existingRoomCount )
	{
		var build = BuildCost( type, Math.Max( 0, existingRoomCount - 1 ) );
		var levelBonus = 1f + 0.15f * Math.Max( 0, level - 1 );
		return (long)(build * DemolishRefundFraction * levelBonus);
	}

	public static long UpgradeCost( RoomType type, int currentLevel )
	{
		if ( currentLevel >= MaxRoomLevel )
			return 0;
		var def = GetRoom( type );
		var baseCost = def.BaseBuildCostCents * 0.45f;
		return (long)(baseCost * MathF.Pow( UpgradeCostMultiplierPerLevel, currentLevel - 1 ));
	}

	public static int CapacityAtLevel( RoomDef def, int level )
	{
		if ( def.BaseCapacity <= 0 )
			return 0;
		return def.BaseCapacity + (level - 1) / 2;
	}

	public static long RateAtLevel( RoomDef def, int level )
	{
		if ( def.BaseRateCents <= 0 )
			return 0;
		return (long)(def.BaseRateCents * (1f + UpgradeRateBonusPerLevel * (level - 1)));
	}

	public static float AmenityVisitDuration( RoomType type ) => type switch
	{
		RoomType.Cafe => 2.5f,
		RoomType.Restaurant => 4.5f,
		RoomType.Spa => 5.5f,
		RoomType.GiftShop => 2.0f,
		_ => 3f
	};

	public static float AmenitySatisfactionBonus( RoomType type ) => type switch
	{
		RoomType.Cafe => 0.04f,
		RoomType.Restaurant => 0.07f,
		RoomType.Spa => 0.10f,
		RoomType.GiftShop => 0.03f,
		_ => 0.05f
	};

	public static (float Demand, float Satisfaction) WeatherModifiers( WeatherKind weather ) => weather switch
	{
		WeatherKind.Cloudy => (0.92f, -0.005f),
		WeatherKind.Rain => (1.18f, 0.01f),      // more indoor demand
		WeatherKind.Heatwave => (0.85f, -0.02f),
		WeatherKind.Festival => (1.35f, 0.02f),
		_ => (1f, 0f)
	};

	public static string WeatherLabel( WeatherKind weather ) => weather switch
	{
		WeatherKind.Cloudy => "Cloudy",
		WeatherKind.Rain => "Rainy",
		WeatherKind.Heatwave => "Heatwave",
		WeatherKind.Festival => "Festival",
		_ => "Clear"
	};

	public static int SoftPrestigeLevel( int reputationLevel ) => Math.Max( 0, reputationLevel - 6 );

	public static float SoftPrestigeDemandBonus( int reputationLevel ) =>
		1f + SoftPrestigeLevel( reputationLevel ) * 0.08f;

	public static long NextReputationThreshold( int currentLevel )
	{
		if ( currentLevel < ReputationProfitThresholds.Length - 1 )
			return ReputationProfitThresholds[currentLevel + 1];
		var soft = SoftPrestigeLevel( currentLevel );
		return ReputationProfitThresholds[^1] + SoftPrestigeProfitStep * (soft + 1);
	}

	public static long CurrentReputationThreshold( int currentLevel )
	{
		if ( currentLevel <= 1 )
			return 0;
		if ( currentLevel < ReputationProfitThresholds.Length )
			return ReputationProfitThresholds[currentLevel];
		var soft = SoftPrestigeLevel( currentLevel );
		return ReputationProfitThresholds[^1] + SoftPrestigeProfitStep * soft;
	}

	public static float ReputationProgress( long lifetimeProfit, int reputationLevel )
	{
		var current = CurrentReputationThreshold( reputationLevel );
		var next = NextReputationThreshold( reputationLevel );
		if ( next <= current )
			return 1f;
		return Math.Clamp( (float)(lifetimeProfit - current) / (next - current), 0f, 1f );
	}

	public static int DayFromSimTime( float simTime ) => 1 + (int)(simTime / SecondsPerDay);
}

public enum RoomCategory
{
	Structure,
	Lodging,
	Amenity,
	Support
}

public enum RoomType
{
	Lobby,
	StandardRoom,
	DeluxeRoom,
	Suite,
	Cafe,
	Restaurant,
	Spa,
	GiftShop,
	Laundry,
	MaintenanceWorkshop,
	StaffRoom
}

public enum StaffRole
{
	Receptionist,
	Housekeeper,
	Cook,
	MaintenanceWorker
}

public readonly record struct RoomDef(
	RoomType Type,
	string DisplayName,
	RoomCategory Category,
	long BaseBuildCostCents,
	int BaseCapacity,
	long BaseRateCents,
	long UpkeepPerMinuteCents,
	int UnlockReputation,
	int LodgingTier
);

public readonly record struct StaffDef(
	StaffRole Role,
	string DisplayName,
	long HireCostCents,
	long WagePerMinuteCents,
	int UnlockReputation
);
