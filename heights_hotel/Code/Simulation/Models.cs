using System;
using System.Collections.Generic;

namespace HeightsHotel;

public enum GuestPhase
{
	Arriving,
	CheckingIn,
	Staying,
	VisitingAmenity,
	CheckingOut,
	Leaving
}

public enum EmployeeTask
{
	Idle,
	Walk,
	CheckIn,
	Clean,
	Cook,
	Repair
}

public enum WeatherKind
{
	Clear,
	Cloudy,
	Rain,
	Heatwave,
	Festival
}

public enum DailyGoalMetric
{
	BuildRooms,
	ServeGuests,
	EarnCents,
	ReachOccupancy,
	HireStaff,
	RepairRooms
}

public sealed class GridCell
{
	public int X { get; set; }
	public int Y { get; set; }
	public RoomType Type { get; set; }
	public int Level { get; set; } = 1;
	public float Cleanliness { get; set; } = 1f; // 0 dirty .. 1 clean
	public bool Broken { get; set; }
	public bool UnderConstruction { get; set; }
	public float ConstructionRemaining { get; set; }
	public List<int> OccupantGuestIds { get; set; } = new();
	public int? AssignedEmployeeId { get; set; }
}

public sealed class Guest
{
	public int Id { get; set; }
	public string Name { get; set; }
	public int Variant { get; set; }
	public GuestPhase Phase { get; set; }
	public int? RoomX { get; set; }
	public int? RoomY { get; set; }
	public int? AmenityX { get; set; }
	public int? AmenityY { get; set; }
	public float PosX { get; set; }
	public float PosY { get; set; }
	public float TargetX { get; set; }
	public float TargetY { get; set; }
	public float Satisfaction { get; set; } = 0.8f;
	public float StayRemaining { get; set; }
	public float PhaseTimer { get; set; }
	public long SpentCents { get; set; }
	public int Nights { get; set; } = 1;
	public bool FacingLeft { get; set; }
	public RoomType LastLodgingType { get; set; } = RoomType.StandardRoom;
	public int LastLodgingLevel { get; set; } = 1;
	/// <summary>Preferred lodging tier (1=standard, 2=deluxe, 3=suite).</summary>
	public int PreferredTier { get; set; } = 1;
	public int? ClaimedByEmployeeId { get; set; }
	public bool AmenityArrived { get; set; }
}

public sealed class Employee
{
	public int Id { get; set; }
	public StaffRole Role { get; set; }
	public string Name { get; set; }
	public EmployeeTask Task { get; set; } = EmployeeTask.Idle;
	public EmployeeTask PendingWork { get; set; } = EmployeeTask.Idle;
	public float PosX { get; set; }
	public float PosY { get; set; }
	public float TargetX { get; set; }
	public float TargetY { get; set; }
	public int? TargetRoomX { get; set; }
	public int? TargetRoomY { get; set; }
	public float TaskTimer { get; set; }
	public bool FacingLeft { get; set; }
	public int? ServingGuestId { get; set; }
	public int? AssignedRoomX { get; set; }
	public int? AssignedRoomY { get; set; }
}

public sealed class LedgerEntry
{
	public float SimTime { get; set; }
	public string Label { get; set; }
	public long DeltaCents { get; set; }
}

public sealed class DailyGoal
{
	public string Id { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public DailyGoalMetric Metric { get; set; }
	public int Target { get; set; }
	public long RewardCents { get; set; }
	public bool Claimed { get; set; }
}

public sealed class HotelState
{
	public int SaveVersion { get; set; } = GameBalance.SaveVersion;
	public long CashCents { get; set; } = GameBalance.StartingCashCents;
	public long LifetimeIncomeCents { get; set; }
	public long LifetimeExpenseCents { get; set; }
	public int TotalGuestsServed { get; set; }
	public float SimTime { get; set; }
	public int SpeedIndex { get; set; } = 1; // 0 pause, 1=1x, 2=2x, 3=4x
	public int ReputationLevel { get; set; } = 1;
	public int PeakReputationLevel { get; set; } = 1;
	public int NextGuestId { get; set; } = 1;
	public int NextEmployeeId { get; set; } = 1;
	public int RngSeed { get; set; } = 1337;
	public int RngCalls { get; set; }
	public DateTimeOffset LastRealWorldUtc { get; set; } = DateTimeOffset.UtcNow;
	public List<GridCell> Cells { get; set; } = new();
	public List<Guest> Guests { get; set; } = new();
	public List<Employee> Employees { get; set; } = new();
	public List<LedgerEntry> Ledger { get; set; } = new();
	public List<string> CompletedTutorials { get; set; } = new();
	public string ActiveTutorial { get; set; } = "build_room";
	public string StatusMessage { get; set; }
	public int StatusRevision { get; set; }
	public long OfflineEarningsAppliedCents { get; set; }
	public int Revision { get; set; }

	// Post-MVP systems
	public WeatherKind Weather { get; set; } = WeatherKind.Clear;
	public float WeatherRemaining { get; set; } = 60f;
	public int GoalsDay { get; set; } = 1;
	public List<DailyGoal> DailyGoals { get; set; } = new();
	public long DayIncomeBaseline { get; set; }
	public int DayGuestsBaseline { get; set; }
	public int DayRoomsBaseline { get; set; }
	public int DayStaffBaseline { get; set; }
	public int DayRepairsDone { get; set; }
}

public sealed class SimCommandResult
{
	public bool Ok { get; init; }
	public string Message { get; init; }

	public static SimCommandResult Success( string message = null ) => new() { Ok = true, Message = message };
	public static SimCommandResult Fail( string message ) => new() { Ok = false, Message = message };
}

public static class NameBank
{
	public static readonly string[] First =
	{
		"Ada", "Ben", "Cora", "Drew", "Eve", "Finn", "Gia", "Hank", "Ivy", "Jade",
		"Kai", "Lena", "Milo", "Nina", "Owen", "Pia", "Quinn", "Remy", "Sage", "Tess"
	};

	public static string FromIndex( int index ) => First[Math.Abs( index ) % First.Length];
}
