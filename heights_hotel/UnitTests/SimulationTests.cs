using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HeightsHotel;

[TestClass]
public class SimulationTests
{
	[TestMethod]
	public void NewGame_HasLobbyAndStartingCash()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 42 ) );
		Assert.AreEqual( 1, sim.RoomCount );
		Assert.IsNotNull( sim.GetCell( 0, 0 ) );
		Assert.AreEqual( RoomType.Lobby, sim.GetCell( 0, 0 ).Type );
		Assert.AreEqual( GameBalance.StartingCashCents, sim.State.CashCents );
	}

	[TestMethod]
	public void Build_RequiresAdjacency()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 1 ) );
		var far = sim.TryBuild( RoomType.StandardRoom, 5, 5 );
		Assert.IsFalse( far.Ok );

		var ok = sim.TryBuild( RoomType.StandardRoom, 1, 0 );
		Assert.IsTrue( ok.Ok, ok.Message );
		Assert.IsNotNull( sim.GetCell( 1, 0 ) );
	}

	[TestMethod]
	public void Build_BlocksWhenUnaffordable()
	{
		var state = HotelSimulation.CreateNewGame( 2 );
		state.CashCents = 10;
		var sim = new HotelSimulation( state );
		var r = sim.TryBuild( RoomType.StandardRoom, 1, 0 );
		Assert.IsFalse( r.Ok );
		Assert.IsNull( sim.GetCell( 1, 0 ) );
	}

	[TestMethod]
	public void Build_CannotOverlap()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 3 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var again = sim.TryBuild( RoomType.Cafe, 1, 0 );
		Assert.IsFalse( again.Ok );
	}

	[TestMethod]
	public void HireAndUpgrade_SpendCash()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 4 ) );
		var before = sim.State.CashCents;
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );
		Assert.IsTrue( sim.State.CashCents < before );
		Assert.AreEqual( 1, sim.State.Employees.Count );

		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var cell = sim.GetCell( 1, 0 );
		cell.UnderConstruction = false;
		var cash = sim.State.CashCents;
		Assert.IsTrue( sim.TryUpgrade( 1, 0 ).Ok );
		Assert.AreEqual( 2, cell.Level );
		Assert.IsTrue( sim.State.CashCents < cash );
	}

	[TestMethod]
	public void LockedRooms_RequireReputation()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 5 ) );
		Assert.IsFalse( sim.IsUnlocked( RoomType.Suite ) );
		var r = sim.TryBuild( RoomType.Suite, 1, 0 );
		Assert.IsFalse( r.Ok );
	}

	[TestMethod]
	public void StaffCompatibility_CoversEveryRoomAndRoleAtUnlock()
	{
		foreach ( var room in GameBalance.Rooms )
		{
			var preferredRole = GameBalance.PreferredStaffRole( room.Type );
			Assert.IsTrue(
				GameBalance.CanAssignStaff( preferredRole, room.Type ),
				$"{preferredRole} should be compatible with {room.Type}." );
			// Lobby (structure) exists from day one; preferred staff still unlocks at reputation 1.
			if ( room.Category == RoomCategory.Structure )
				continue;
			Assert.IsTrue(
				GameBalance.GetStaff( preferredRole ).UnlockReputation <= room.UnlockReputation,
				$"{room.Type} unlocks before its preferred {preferredRole}." );
		}

		foreach ( var staff in GameBalance.Staff )
		{
			Assert.IsTrue(
				GameBalance.Rooms.Any( room => GameBalance.CanAssignStaff( staff.Role, room.Type ) ),
				$"{staff.Role} has no compatible room." );
		}
	}

	[TestMethod]
	public void Tick_IsDeterministicForSameSeed()
	{
		var a = new HotelSimulation( HotelSimulation.CreateNewGame( 99 ) );
		var b = new HotelSimulation( HotelSimulation.CreateNewGame( 99 ) );
		a.TryBuild( RoomType.StandardRoom, 1, 0 );
		b.TryBuild( RoomType.StandardRoom, 1, 0 );
		a.GetCell( 1, 0 ).UnderConstruction = false;
		b.GetCell( 1, 0 ).UnderConstruction = false;
		a.TryHire( StaffRole.Receptionist );
		b.TryHire( StaffRole.Receptionist );

		for ( var i = 0; i < 200; i++ )
		{
			a.Advance( GameBalance.TickSeconds );
			b.Advance( GameBalance.TickSeconds );
		}

		Assert.AreEqual( a.State.CashCents, b.State.CashCents );
		Assert.AreEqual( a.State.Guests.Count, b.State.Guests.Count );
		Assert.AreEqual( a.State.RngCalls, b.State.RngCalls );
	}

	[TestMethod]
	public void Offline_CapsAtEightHoursEfficiency()
	{
		var state = HotelSimulation.CreateNewGame( 7 );
		state.CashCents = 50_000_00;
		state.Cells.Add( new GridCell { X = 1, Y = 0, Type = RoomType.StandardRoom, Level = 3, Cleanliness = 1f } );
		state.Cells.Add( new GridCell { X = 2, Y = 0, Type = RoomType.Cafe, Level = 2, Cleanliness = 1f } );
		state.Employees.Add( new Employee { Id = 1, Role = StaffRole.Receptionist, Name = "A" } );
		state.LastRealWorldUtc = DateTimeOffset.UtcNow.AddHours( -100 );
		var sim = new HotelSimulation( state );
		var before = sim.State.CashCents;
		var earned = sim.ApplyOfflineProgress();
		Assert.AreEqual( earned, sim.State.OfflineEarningsAppliedCents );
		Assert.AreEqual( before + earned, sim.State.CashCents );
		// Even with huge gap, offline apply should be finite
		var maxSensible = 50_000_00L;
		Assert.IsTrue( Math.Abs( earned ) < maxSensible );
		Assert.AreEqual( 0, sim.ApplyOfflineProgress(), "Offline progress should apply only once per load interval." );
	}

	[TestMethod]
	public void Wages_AreChargedDuringTicks()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 15 ) );
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );
		var before = sim.State.CashCents;
		sim.Advance( 1f );
		Assert.IsTrue( sim.State.CashCents < before );
		Assert.IsTrue( sim.State.LifetimeExpenseCents > GameBalance.GetStaff( StaffRole.Receptionist ).HireCostCents );
	}

	[TestMethod]
	public void RecurringExpenses_AreBatchedInLedger()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 115 ) );
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );
		sim.State.Ledger.Clear();

		for ( var i = 0; i < 6; i++ )
			sim.Advance( 2f );

		Assert.AreEqual( 1, sim.State.Ledger.Count( e => e.Label == "Wages" ) );
		Assert.IsTrue( sim.State.Ledger.Single( e => e.Label == "Wages" ).DeltaCents < 0 );
	}

	[TestMethod]
	public void StarterCafe_CanServeWithoutCook()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 116 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.Cafe, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, -1, 0 ).Ok );
		var lodging = sim.GetCell( -1, 0 );
		lodging.UnderConstruction = false;
		for ( var i = 0; i < 100; i++ )
		{
			lodging.OccupantGuestIds.Add( i + 1 );
			sim.State.Guests.Add( new Guest
			{
				Id = i + 1,
				Phase = GuestPhase.Staying,
				RoomX = -1,
				RoomY = 0,
				StayRemaining = 60f,
				PosX = -1,
				PosY = 0,
				LastLodgingType = RoomType.StandardRoom,
				Satisfaction = 0.7f
			} );
		}

		sim.Advance( GameBalance.TickSeconds );

		Assert.IsTrue( sim.State.Guests.Any( g => g.Phase == GuestPhase.VisitingAmenity ) );
		Assert.AreEqual( 0, sim.State.Employees.Count );
	}

	[TestMethod]
	public void AssignedHousekeeper_CleansTargetRoom()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 16 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		room.Cleanliness = 0.2f;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		var employee = sim.State.Employees[0];
		Assert.IsTrue( sim.TryAssignEmployee( employee.Id, 1, 0 ).Ok );

		for ( var i = 0; i < 30; i++ )
			sim.Advance( GameBalance.TickSeconds );

		Assert.IsTrue( room.Cleanliness >= 0.9f );
	}

	[TestMethod]
	public void ProfitThreshold_UnlocksNextReputationLevel()
	{
		var state = HotelSimulation.CreateNewGame( 17 );
		state.LifetimeIncomeCents = GameBalance.ReputationProfitThresholds[2];
		var sim = new HotelSimulation( state );
		sim.Advance( GameBalance.TickSeconds );
		Assert.AreEqual( 2, sim.State.ReputationLevel );
		Assert.IsTrue( sim.IsUnlocked( RoomType.DeluxeRoom ) );
	}

	[TestMethod]
	public void SaveRoundTrip_PreservesLayout()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 8 ) );
		sim.TryBuild( RoomType.StandardRoom, 1, 0 );
		sim.TryHire( StaffRole.Housekeeper );
		var json = System.Text.Json.JsonSerializer.Serialize( sim.State );
		var loaded = System.Text.Json.JsonSerializer.Deserialize<HotelState>( json );
		Assert.IsNotNull( loaded );
		Assert.AreEqual( 2, loaded.Cells.Count );
		Assert.AreEqual( 1, loaded.Employees.Count );
		Assert.AreEqual( GameBalance.SaveVersion, loaded.SaveVersion );
	}

	[TestMethod]
	public void FireStaff_RemovesEmployee()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 9 ) );
		sim.TryHire( StaffRole.Housekeeper );
		var id = sim.State.Employees[0].Id;
		Assert.IsTrue( sim.TryFire( id ).Ok );
		Assert.AreEqual( 0, sim.State.Employees.Count );
	}

	[TestMethod]
	public void AssignStaff_ValidatesRoleAndPersistsRoomLink()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 13 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		var employee = sim.State.Employees[0];

		var assigned = sim.TryAssignEmployee( employee.Id, 1, 0 );
		Assert.IsTrue( assigned.Ok, assigned.Message );
		Assert.AreEqual( employee.Id, room.AssignedEmployeeId );
		Assert.AreEqual( 1, employee.AssignedRoomX );
		Assert.AreEqual( 0, employee.AssignedRoomY );

		var invalid = sim.TryAssignEmployee( employee.Id, 0, 0 );
		Assert.IsFalse( invalid.Ok );
	}

	[TestMethod]
	public void AutoAssignStaff_UsesAvailableEmployeeAndMovesAssignment()
	{
		var state = HotelSimulation.CreateNewGame( 130 );
		state.CashCents = 1_000_000;
		var sim = new HotelSimulation( state );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 2, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		sim.GetCell( 2, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		var employee = sim.State.Employees.Single();
		Assert.IsTrue( sim.TryAssignEmployee( employee.Id, 1, 0 ).Ok );
		employee.Task = EmployeeTask.Walk;
		employee.PendingWork = EmployeeTask.Idle;

		var result = sim.TryAutoAssignEmployee( 2, 0, out var noAvailableStaff );

		Assert.IsTrue( result.Ok, result.Message );
		Assert.IsFalse( noAvailableStaff );
		Assert.IsNull( sim.GetCell( 1, 0 ).AssignedEmployeeId );
		Assert.AreEqual( employee.Id, sim.GetCell( 2, 0 ).AssignedEmployeeId );
	}

	[TestMethod]
	public void AutoAssignStaff_ReportsWhenCompatibleEmployeeIsBusy()
	{
		var state = HotelSimulation.CreateNewGame( 131 );
		state.CashCents = 1_000_000;
		var sim = new HotelSimulation( state );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		sim.State.Employees.Single().Task = EmployeeTask.Clean;

		var result = sim.TryAutoAssignEmployee( 1, 0, out var noAvailableStaff );

		Assert.IsFalse( result.Ok );
		Assert.IsTrue( noAvailableStaff );
		Assert.IsNull( sim.GetCell( 1, 0 ).AssignedEmployeeId );
	}

	[TestMethod]
	public void AutoAssignStaff_CanRequireSpecificCompatibleRole()
	{
		var state = HotelSimulation.CreateNewGame( 132 );
		state.CashCents = 1_000_000;
		state.ReputationLevel = 4;
		var sim = new HotelSimulation( state );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		Assert.IsTrue( sim.TryHire( StaffRole.MaintenanceWorker ).Ok );

		var result = sim.TryAutoAssignEmployee(
			1,
			0,
			out var noAvailableStaff,
			StaffRole.MaintenanceWorker );

		Assert.IsTrue( result.Ok, result.Message );
		Assert.IsFalse( noAvailableStaff );
		var assignedId = sim.GetCell( 1, 0 ).AssignedEmployeeId;
		Assert.AreEqual(
			StaffRole.MaintenanceWorker,
			sim.State.Employees.Single( employee => employee.Id == assignedId ).Role );
	}

	[TestMethod]
	public void FireStaff_ClearsRoomAssignment()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 14 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.Cafe, 1, 0 ).Ok );
		var cafe = sim.GetCell( 1, 0 );
		cafe.UnderConstruction = false;
		sim.State.ReputationLevel = 3;
		Assert.IsTrue( sim.TryHire( StaffRole.Cook ).Ok );
		var employee = sim.State.Employees[0];
		Assert.IsTrue( sim.TryAssignEmployee( employee.Id, 1, 0 ).Ok );

		Assert.IsTrue( sim.TryFire( employee.Id ).Ok );
		Assert.IsNull( cafe.AssignedEmployeeId );
	}

	[TestMethod]
	public void EnumerateBuildCandidates_TouchesHotel()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 10 ) );
		var spots = sim.EnumerateBuildCandidates().ToList();
		Assert.IsTrue( spots.Contains( (1, 0) ) );
		Assert.IsTrue( spots.Contains( (-1, 0) ) );
		Assert.IsTrue( spots.Contains( (0, 1) ) );
		Assert.IsFalse( spots.Contains( (0, -1) ) ); // below ground blocked
	}

	[TestMethod]
	public void Demolish_RefundsAndKeepsConnectivity()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 201 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryBuild( RoomType.Cafe, 2, 0 ).Ok );
		sim.GetCell( 2, 0 ).UnderConstruction = false;

		var before = sim.State.CashCents;
		Assert.IsTrue( sim.TryDemolish( 2, 0 ).Ok );
		Assert.IsNull( sim.GetCell( 2, 0 ) );
		Assert.IsTrue( sim.State.CashCents > before );

		// Demolishing the only link would disconnect if we had a floating room — middle room stays.
		Assert.IsFalse( sim.TryDemolish( 0, 0 ).Ok, "Lobby protected" );
	}

	[TestMethod]
	public void Demolish_BlocksWhenItWouldOrphanRooms()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 202 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryBuild( RoomType.Cafe, 2, 0 ).Ok );
		sim.GetCell( 2, 0 ).UnderConstruction = false;

		var blocked = sim.TryDemolish( 1, 0 );
		Assert.IsFalse( blocked.Ok );
		Assert.IsNotNull( sim.GetCell( 1, 0 ) );
		Assert.IsNotNull( sim.GetCell( 2, 0 ) );
	}

	[TestMethod]
	public void DailyGoals_CanBeClaimedWhenComplete()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 203 ) );
		var hireGoal = sim.State.DailyGoals.FirstOrDefault( g => g.Metric == DailyGoalMetric.HireStaff )
			?? sim.State.DailyGoals[0];
		hireGoal.Metric = DailyGoalMetric.HireStaff;
		hireGoal.Target = 1;
		hireGoal.Claimed = false;
		hireGoal.RewardCents = 50_00;
		sim.State.DayStaffBaseline = sim.State.Employees.Count;

		Assert.IsFalse( sim.IsGoalComplete( hireGoal ) );
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );
		Assert.IsTrue( sim.IsGoalComplete( hireGoal ) );

		var before = sim.State.CashCents;
		Assert.IsTrue( sim.TryClaimGoal( hireGoal.Id ).Ok );
		Assert.IsTrue( hireGoal.Claimed );
		Assert.IsTrue( sim.State.CashCents > before );
	}

	[TestMethod]
	public void SoftPrestige_RaisesReputationBeyondSix()
	{
		var state = HotelSimulation.CreateNewGame( 204 );
		state.LifetimeIncomeCents = GameBalance.ReputationProfitThresholds[^1] + GameBalance.SoftPrestigeProfitStep * 2;
		state.LifetimeExpenseCents = 0;
		var sim = new HotelSimulation( state );
		sim.Advance( GameBalance.TickSeconds );
		Assert.IsTrue( sim.State.ReputationLevel >= 7 );
		Assert.IsTrue( GameBalance.SoftPrestigeDemandBonus( sim.State.ReputationLevel ) > 1f );
	}

	[TestMethod]
	public void Staff_DoNotDoubleClaimSameDirtyRoom()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 205 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		room.Cleanliness = 0.2f;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );

		sim.Advance( GameBalance.TickSeconds );
		var targeting = sim.State.Employees.Count( e => e.TargetRoomX == 1 && e.TargetRoomY == 0 );
		Assert.AreEqual( 1, targeting );
	}

	[TestMethod]
	public void DispatchService_SendsAvailableHousekeeperToSelectedRoom()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 207 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		room.Cleanliness = 0.2f;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );

		var result = sim.TryDispatchService( StaffRole.Housekeeper, 1, 0 );

		Assert.IsTrue( result.Ok, result.Message );
		var employee = sim.State.Employees.Single();
		Assert.AreEqual( EmployeeTask.Walk, employee.Task );
		Assert.AreEqual( EmployeeTask.Clean, employee.PendingWork );
		Assert.AreEqual( 1, employee.TargetRoomX );
		Assert.AreEqual( 0, employee.TargetRoomY );
	}

	[TestMethod]
	public void Receptionist_MustProcessCheckIn()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 206 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		sim.GetCell( 1, 0 ).UnderConstruction = false;
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );

		sim.State.Guests.Add( new Guest
		{
			Id = 99,
			Name = "Test",
			Phase = GuestPhase.CheckingIn,
			PhaseTimer = 10f,
			StayRemaining = 40f,
			PosX = 0,
			PosY = 0,
			TargetX = 0,
			TargetY = 0,
			PreferredTier = 1,
			Satisfaction = 0.8f
		} );

		for ( var i = 0; i < 40; i++ )
			sim.Advance( GameBalance.TickSeconds );

		var guest = sim.State.Guests.FirstOrDefault( g => g.Id == 99 );
		Assert.IsNotNull( guest );
		Assert.AreEqual( GuestPhase.Staying, guest.Phase );
		Assert.IsTrue( guest.RoomX.HasValue );
	}

	[TestMethod]
	public void Restaurant_OpensWhenCookHired_NoAssignmentRequired()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 208 ) );
		sim.State.ReputationLevel = 3;
		Assert.IsTrue( sim.TryBuild( RoomType.Restaurant, 1, 0 ).Ok );
		var restaurant = sim.GetCell( 1, 0 );
		restaurant.UnderConstruction = false;

		Assert.IsFalse( sim.IsAmenityOpen( restaurant ) );
		Assert.IsTrue( sim.AmenityClosedReason( restaurant )!.Contains( "hire", StringComparison.OrdinalIgnoreCase ) );

		Assert.IsTrue( sim.TryHire( StaffRole.Cook ).Ok );
		Assert.IsTrue( sim.IsAmenityOpen( restaurant ) );
	}

	[TestMethod]
	public void GiftShop_OpensWhenReceptionistHired_NoAssignmentRequired()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 209 ) );
		sim.State.ReputationLevel = 2;
		Assert.IsTrue( sim.TryBuild( RoomType.GiftShop, 1, 0 ).Ok );
		var shop = sim.GetCell( 1, 0 );
		shop.UnderConstruction = false;
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, -1, 0 ).Ok );
		var lodging = sim.GetCell( -1, 0 );
		lodging.UnderConstruction = false;

		Assert.IsFalse( sim.IsAmenityOpen( shop ) );

		for ( var i = 0; i < 100; i++ )
		{
			lodging.OccupantGuestIds.Add( i + 1 );
			sim.State.Guests.Add( new Guest
			{
				Id = i + 1,
				Phase = GuestPhase.Staying,
				RoomX = -1,
				RoomY = 0,
				StayRemaining = 60f,
				PosX = -1,
				PosY = 0,
				LastLodgingType = RoomType.StandardRoom,
				Satisfaction = 0.7f,
				PreferredTier = 1
			} );
		}

		sim.Advance( GameBalance.TickSeconds );
		Assert.IsFalse( sim.State.Guests.Any( g => g.Phase == GuestPhase.VisitingAmenity && g.AmenityX == 1 ) );

		var hire = sim.TryHire( StaffRole.Receptionist );
		Assert.IsTrue( hire.Ok, hire.Message );
		Assert.IsTrue( sim.IsAmenityOpen( shop ), sim.AmenityClosedReason( shop ) );

		var visited = false;
		for ( var i = 0; i < 40 && !visited; i++ )
		{
			sim.Advance( GameBalance.TickSeconds );
			visited = sim.State.Guests.Any( g => g.Phase == GuestPhase.VisitingAmenity && g.AmenityX == 1 );
		}
		Assert.IsTrue( visited, "Staffed gift shop should accept amenity visits." );
	}

	[TestMethod]
	public void StaffingNeeds_ReportsHireWhenDirtyRoomsHaveNoHousekeeper()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 210 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		room.Cleanliness = 0.4f;

		var needs = sim.GetStaffingNeeds();
		Assert.IsTrue( needs.Any( n => n.Role == StaffRole.Housekeeper ), "Should need a housekeeper." );
		Assert.IsTrue( sim.IsUnderstaffed );

		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );
		Assert.IsTrue( sim.TryHire( StaffRole.Receptionist ).Ok );
		Assert.IsFalse( sim.GetStaffingNeeds().Any( n => n.Role == StaffRole.Housekeeper ) );
	}

	[TestMethod]
	public void UnassignedHousekeeper_AutoCleansDirtyRooms()
	{
		var sim = new HotelSimulation( HotelSimulation.CreateNewGame( 211 ) );
		Assert.IsTrue( sim.TryBuild( RoomType.StandardRoom, 1, 0 ).Ok );
		var room = sim.GetCell( 1, 0 );
		room.UnderConstruction = false;
		room.Cleanliness = 0.2f;
		Assert.IsTrue( sim.TryHire( StaffRole.Housekeeper ).Ok );

		var cleaned = false;
		for ( var i = 0; i < 80 && !cleaned; i++ )
		{
			sim.Advance( GameBalance.TickSeconds );
			cleaned = room.Cleanliness >= 0.9f;
		}
		Assert.IsTrue( cleaned, "Housekeeper should auto-clean without room assignment." );
	}
}
