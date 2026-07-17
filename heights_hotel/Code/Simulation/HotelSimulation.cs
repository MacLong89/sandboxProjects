using System;
using System.Collections.Generic;
using System.Linq;

namespace HeightsHotel;

/// <summary>
/// Sole authority for hotel state mutations and time advancement.
/// </summary>
public sealed class HotelSimulation
{
	public HotelState State { get; private set; }
	public event Action Changed;

	Random _rng;
	float _tickAccumulator;
	float _recurringLedgerTimer;
	long _pendingWageLedgerCents;
	long _pendingUpkeepLedgerCents;
	float _wageRemainder;
	float _upkeepRemainder;

	public HotelSimulation( HotelState state = null )
	{
		State = state ?? CreateNewGame();
		SyncRng();
		EnsurePostMvpDefaults();
	}

	public static HotelState CreateNewGame( int seed = 1337 )
	{
		var state = new HotelState
		{
			RngSeed = seed,
			CashCents = GameBalance.StartingCashCents,
			LastRealWorldUtc = DateTimeOffset.UtcNow,
			ActiveTutorial = "build_room",
			Weather = WeatherKind.Clear,
			WeatherRemaining = 60f,
			GoalsDay = 1
		};
		state.Cells.Add( new GridCell { X = 0, Y = 0, Type = RoomType.Lobby, Level = 1, Cleanliness = 1f } );
		return state;
	}

	void EnsurePostMvpDefaults()
	{
		State.DailyGoals ??= new List<DailyGoal>();
		State.PeakReputationLevel = Math.Max( State.PeakReputationLevel, State.ReputationLevel );
		if ( State.WeatherRemaining <= 0 )
			State.WeatherRemaining = 45f;
		if ( State.GoalsDay < 1 )
			State.GoalsDay = GameBalance.DayFromSimTime( State.SimTime );
		if ( State.DailyGoals.Count == 0 )
			RefreshDailyGoals( force: true );
	}

	void SyncRng()
	{
		_rng = new Random( State.RngSeed );
		for ( var i = 0; i < State.RngCalls; i++ )
			_rng.Next();
	}

	int NextInt( int max )
	{
		State.RngCalls++;
		return max <= 0 ? 0 : _rng.Next( max );
	}

	float NextFloat()
	{
		return NextInt( 10_000 ) / 10_000f;
	}

	void Notify( string status = null )
	{
		if ( status is not null )
		{
			State.StatusMessage = status;
			State.StatusRevision++;
		}
		State.Revision++;
		Changed?.Invoke();
	}

	public void ShowStatus( string message ) => Notify( message );

	public GridCell GetCell( int x, int y ) => State.Cells.FirstOrDefault( c => c.X == x && c.Y == y );
	public bool HasCell( int x, int y ) => GetCell( x, y ) is not null;
	public int RoomCount => State.Cells.Count;
	public long LifetimeProfit => State.LifetimeIncomeCents - State.LifetimeExpenseCents;
	public float ReputationProgress => GameBalance.ReputationProgress( LifetimeProfit, State.ReputationLevel );
	public WeatherKind CurrentWeather => State.Weather;
	public IReadOnlyList<DailyGoal> DailyGoals => State.DailyGoals;

	public bool IsAdjacentToHotel( int x, int y )
	{
		if ( HasCell( x, y ) )
			return false;
		return HasCell( x - 1, y ) || HasCell( x + 1, y ) || HasCell( x, y - 1 ) || HasCell( x, y + 1 );
	}

	public bool IsUnlocked( RoomType type ) => GameBalance.GetRoom( type ).UnlockReputation <= State.ReputationLevel;
	public bool IsUnlocked( StaffRole role ) => GameBalance.GetStaff( role ).UnlockReputation <= State.ReputationLevel;

	public long CurrentBuildCost( RoomType type ) => GameBalance.BuildCost( type, RoomCount );

	public float AverageSatisfaction
	{
		get
		{
			var staying = State.Guests.Where( g => g.Phase is GuestPhase.Staying or GuestPhase.VisitingAmenity ).ToList();
			if ( staying.Count == 0 )
				return 0.75f;
			return staying.Average( g => g.Satisfaction );
		}
	}

	public float Occupancy
	{
		get
		{
			var lodging = State.Cells.Where( c => GameBalance.GetRoom( c.Type ).Category == RoomCategory.Lodging && !c.UnderConstruction ).ToList();
			if ( lodging.Count == 0 )
				return 0f;
			var capacity = lodging.Sum( c => GameBalance.CapacityAtLevel( GameBalance.GetRoom( c.Type ), c.Level ) );
			if ( capacity <= 0 )
				return 0f;
			var occupied = lodging.Sum( c => c.OccupantGuestIds.Count );
			return (float)occupied / capacity;
		}
	}

	public long EstimateNetIncomePerMinuteCents()
	{
		long income = 0;
		foreach ( var cell in State.Cells )
		{
			if ( cell.UnderConstruction || cell.Broken )
				continue;
			var def = GameBalance.GetRoom( cell.Type );
			if ( def.Category == RoomCategory.Lodging && cell.OccupantGuestIds.Count > 0 )
			{
				// Average stay ~12 sim-minutes equivalent; nights×rate spread across stay.
				income += GameBalance.RateAtLevel( def, cell.Level ) * cell.OccupantGuestIds.Count / 4;
			}
			else if ( def.Category == RoomCategory.Amenity )
			{
				var seats = GameBalance.CapacityAtLevel( def, cell.Level );
				if ( seats <= 0 )
					continue;
				if ( cell.Type == RoomType.Restaurant && !KitchenIsStaffed( cell ) )
					continue;
				var fill = Math.Clamp( (float)cell.OccupantGuestIds.Count / seats, 0.05f, 1f );
				var expectedVisitsPerMin = seats * fill * 0.35f;
				income += (long)(GameBalance.RateAtLevel( def, cell.Level ) * expectedVisitsPerMin);
			}
		}

		var (demand, _) = GameBalance.WeatherModifiers( State.Weather );
		income = (long)(income * demand);
		return income - CurrentWagesPerMinuteCents - CurrentUpkeepPerMinuteCents;
	}

	public long CurrentWagesPerMinuteCents => State.Employees.Sum( e =>
		{
			var w = GameBalance.GetStaff( e.Role ).WagePerMinuteCents;
			if ( HasSupport( RoomType.StaffRoom ) )
				w = (long)(w * (1f - EffectiveSupportBonus( RoomType.StaffRoom, GameBalance.StaffRoomWageDiscount )));
			return w;
		} );

	public long CurrentUpkeepPerMinuteCents => State.Cells.Sum( c => GameBalance.GetRoom( c.Type ).UpkeepPerMinuteCents );

	public long HotelValueCents
	{
		get
		{
			long value = State.CashCents;
			foreach ( var cell in State.Cells )
			{
				var def = GameBalance.GetRoom( cell.Type );
				value += def.BaseBuildCostCents;
				for ( var lvl = 1; lvl < cell.Level; lvl++ )
					value += GameBalance.UpgradeCost( cell.Type, lvl );
			}
			foreach ( var emp in State.Employees )
				value += GameBalance.GetStaff( emp.Role ).HireCostCents / 2;
			return value;
		}
	}

	bool HasSupport( RoomType type ) => State.Cells.Any( c => c.Type == type && !c.UnderConstruction && !c.Broken );

	float EffectiveSupportBonus( RoomType type, float baseBonus )
	{
		var level = State.Cells
			.Where( c => c.Type == type && !c.UnderConstruction && !c.Broken )
			.Select( c => c.Level )
			.DefaultIfEmpty( 0 )
			.Max();
		if ( level <= 0 )
			return 0f;
		return baseBonus * (1f + GameBalance.UpgradeRateBonusPerLevel * (level - 1));
	}

	bool KitchenIsStaffed( GridCell kitchen )
	{
		if ( kitchen.Type == RoomType.Cafe )
			return true; // starter café self-serves
		return State.Employees.Any( e =>
			e.Role == StaffRole.Cook
			&& !kitchen.Broken
			&& (e.AssignedRoomX == kitchen.X && e.AssignedRoomY == kitchen.Y
				|| (e.Task == EmployeeTask.Cook && e.TargetRoomX == kitchen.X && e.TargetRoomY == kitchen.Y)
				|| (e.Task == EmployeeTask.Idle && MathF.Abs( e.PosX - kitchen.X ) < 0.4f && MathF.Abs( e.PosY - kitchen.Y ) < 0.4f)) );
	}

	public void SetSpeed( int index )
	{
		State.SpeedIndex = Math.Clamp( index, 0, GameBalance.SpeedMultipliers.Length - 1 );
		Notify();
	}

	public void Advance( float realDeltaSeconds )
	{
		var speed = GameBalance.SpeedMultipliers[State.SpeedIndex];
		if ( speed <= 0f )
			return;

		var simDelta = Math.Min( realDeltaSeconds * speed, 2f );
		_tickAccumulator += simDelta;
		var dirty = false;
		while ( _tickAccumulator >= GameBalance.TickSeconds )
		{
			_tickAccumulator -= GameBalance.TickSeconds;
			Tick();
			dirty = true;
		}

		if ( dirty )
		{
			State.LastRealWorldUtc = DateTimeOffset.UtcNow;
			Notify();
		}
	}

	void Tick()
	{
		State.SimTime += GameBalance.TickSeconds;
		ProcessConstruction();
		ProcessWagesAndUpkeep();
		UpdateWeather();
		MaybeBreakAmenities();
		RefreshDailyGoals( force: false );
		SpawnGuests();
		UpdateGuests();
		AssignEmployeeTasks();
		UpdateEmployees();
		UpdateReputation();
	}

	void ProcessConstruction()
	{
		foreach ( var cell in State.Cells.Where( c => c.UnderConstruction ) )
		{
			cell.ConstructionRemaining -= GameBalance.TickSeconds;
			if ( cell.ConstructionRemaining <= 0 )
			{
				cell.UnderConstruction = false;
				cell.ConstructionRemaining = 0;
				CompleteTutorial( "build_room", "hire_staff" );
			}
		}
	}

	void ProcessWagesAndUpkeep()
	{
		float wages = 0;
		foreach ( var emp in State.Employees )
		{
			var w = (float)GameBalance.GetStaff( emp.Role ).WagePerMinuteCents;
			if ( HasSupport( RoomType.StaffRoom ) )
				w *= 1f - EffectiveSupportBonus( RoomType.StaffRoom, GameBalance.StaffRoomWageDiscount );
			wages += w;
		}

		var upkeep = (float)State.Cells.Sum( c => GameBalance.GetRoom( c.Type ).UpkeepPerMinuteCents );
		_wageRemainder += wages / GameBalance.WageTickDivisor;
		_upkeepRemainder += upkeep / GameBalance.WageTickDivisor;

		var wageTick = (long)_wageRemainder;
		var upkeepTick = (long)_upkeepRemainder;
		_wageRemainder -= wageTick;
		_upkeepRemainder -= upkeepTick;

		if ( wageTick > 0 )
		{
			ApplyExpense( wageTick, "Wages", false );
			_pendingWageLedgerCents += wageTick;
		}
		if ( upkeepTick > 0 )
		{
			ApplyExpense( upkeepTick, "Upkeep", false );
			_pendingUpkeepLedgerCents += upkeepTick;
		}

		_recurringLedgerTimer += GameBalance.TickSeconds;
		if ( _recurringLedgerTimer >= 10f )
		{
			_recurringLedgerTimer = 0f;
			if ( _pendingWageLedgerCents > 0 )
				PushLedger( "Wages", -_pendingWageLedgerCents );
			if ( _pendingUpkeepLedgerCents > 0 )
				PushLedger( "Upkeep", -_pendingUpkeepLedgerCents );
			_pendingWageLedgerCents = 0;
			_pendingUpkeepLedgerCents = 0;
		}
	}

	void MaybeBreakAmenities()
	{
		foreach ( var amenity in State.Cells.Where( c =>
			GameBalance.GetRoom( c.Type ).Category == RoomCategory.Amenity
			&& !c.Broken && !c.UnderConstruction ) )
		{
			if ( NextFloat() < GameBalance.AmenityBreakChancePerTick )
				amenity.Broken = true;
		}
	}

	void UpdateWeather()
	{
		State.WeatherRemaining -= GameBalance.TickSeconds;
		if ( State.WeatherRemaining > 0 )
			return;

		var options = new[] { WeatherKind.Clear, WeatherKind.Cloudy, WeatherKind.Rain, WeatherKind.Heatwave, WeatherKind.Festival };
		WeatherKind next;
		do
		{
			next = options[NextInt( options.Length )];
		} while ( next == State.Weather && options.Length > 1 );

		State.Weather = next;
		State.WeatherRemaining = GameBalance.MinWeatherDuration
			+ NextFloat() * (GameBalance.MaxWeatherDuration - GameBalance.MinWeatherDuration);
		Notify( $"Weather shifted: {GameBalance.WeatherLabel( next )}" );
	}

	void SpawnGuests()
	{
		var lodgingSlots = FreeLodgingSlots();
		var queued = State.Guests.Count( g => g.Phase is GuestPhase.Arriving or GuestPhase.CheckingIn );
		if ( lodgingSlots - queued <= 0 )
			return;

		var (weatherDemand, _) = GameBalance.WeatherModifiers( State.Weather );
		var demand = GameBalance.BaseGuestSpawnChancePerTick
			* (0.6f + State.ReputationLevel * 0.15f)
			* GameBalance.SoftPrestigeDemandBonus( State.ReputationLevel )
			* (0.7f + AverageSatisfaction * GameBalance.SatisfactionDemandWeight)
			* MathF.Min( 1.5f, lodgingSlots * 0.35f )
			* weatherDemand;

		if ( NextFloat() > demand )
			return;

		var maxTier = Math.Clamp( State.ReputationLevel, 1, 3 );
		var guest = new Guest
		{
			Id = State.NextGuestId++,
			Name = NameBank.FromIndex( NextInt( NameBank.First.Length ) ),
			Variant = NextInt( 3 ),
			Phase = GuestPhase.Arriving,
			PosX = -1.2f,
			PosY = 0f,
			TargetX = 0f,
			TargetY = 0f,
			StayRemaining = GameBalance.GuestMinStaySeconds
				+ NextFloat() * (GameBalance.GuestMaxStaySeconds - GameBalance.GuestMinStaySeconds),
			Nights = 1 + NextInt( 3 ),
			Satisfaction = Math.Clamp( 0.72f + NextFloat() * 0.18f - State.ReputationLevel * GameBalance.GuestExpectationPenaltyPerRep * 0.25f, 0.45f, 0.95f ),
			PreferredTier = 1 + NextInt( maxTier )
		};
		State.Guests.Add( guest );
	}

	int FreeLodgingSlots()
	{
		var free = 0;
		foreach ( var cell in State.Cells )
		{
			var def = GameBalance.GetRoom( cell.Type );
			if ( def.Category != RoomCategory.Lodging || cell.UnderConstruction || cell.Broken )
				continue;
			var cap = GameBalance.CapacityAtLevel( def, cell.Level );
			free += Math.Max( 0, cap - cell.OccupantGuestIds.Count );
		}
		return free;
	}

	void UpdateGuests()
	{
		var (_, weatherSat) = GameBalance.WeatherModifiers( State.Weather );
		foreach ( var guest in State.Guests.ToList() )
		{
			var gx = guest.PosX;
			var gy = guest.PosY;
			var face = guest.FacingLeft;
			MoveToward( ref gx, ref gy, guest.TargetX, guest.TargetY, 1.8f, out var arrived, ref face );
			guest.PosX = gx;
			guest.PosY = gy;
			guest.FacingLeft = face;

			switch ( guest.Phase )
			{
				case GuestPhase.Arriving:
					if ( arrived )
					{
						guest.Phase = GuestPhase.CheckingIn;
						guest.PhaseTimer = GameBalance.GuestCheckInPatience;
					}
					break;

				case GuestPhase.CheckingIn:
					guest.PhaseTimer -= GameBalance.TickSeconds;
					TrySelfServiceCheckIn( guest );
					if ( guest.PhaseTimer <= 0 && guest.Phase == GuestPhase.CheckingIn )
					{
						guest.Phase = GuestPhase.Leaving;
						guest.TargetX = -1.5f;
						guest.TargetY = 0;
						guest.Satisfaction *= 0.5f;
						guest.ClaimedByEmployeeId = null;
						ReleaseReceptionClaims( guest.Id );
					}
					break;

				case GuestPhase.Staying:
					UpdateStayingGuest( guest, weatherSat );
					break;

				case GuestPhase.VisitingAmenity:
					if ( !guest.AmenityArrived )
					{
						if ( arrived )
						{
							guest.AmenityArrived = true;
							var amenityType = GetCell( guest.AmenityX ?? 0, guest.AmenityY ?? 0 )?.Type ?? RoomType.Cafe;
							guest.PhaseTimer = GameBalance.AmenityVisitDuration( amenityType ) + NextFloat();
						}
						break;
					}
					guest.PhaseTimer -= GameBalance.TickSeconds;
					if ( guest.PhaseTimer <= 0 )
						FinishAmenityVisit( guest );
					break;

				case GuestPhase.CheckingOut:
					guest.PhaseTimer -= GameBalance.TickSeconds;
					if ( guest.PhaseTimer <= 0 )
						FinishCheckout( guest );
					break;

				case GuestPhase.Leaving:
					if ( arrived )
						State.Guests.Remove( guest );
					break;
			}
		}
	}

	void TrySelfServiceCheckIn( Guest guest )
	{
		// Without a claimed receptionist, allow a slow self check-in after waiting.
		if ( guest.ClaimedByEmployeeId is not null )
			return;
		if ( HasStaffRole( StaffRole.Receptionist ) )
			return;
		if ( guest.PhaseTimer > GameBalance.GuestCheckInPatience - 4f )
			return;
		if ( TryAssignLodging( guest ) )
		{
			guest.Satisfaction = Math.Clamp( guest.Satisfaction - 0.12f, 0.05f, 1f );
			guest.Phase = GuestPhase.Staying;
			CompleteTutorial( "first_guest", null );
		}
	}

	void UpdateStayingGuest( Guest guest, float weatherSat )
	{
		if ( guest.RoomX is not int gx || guest.RoomY is not int gy )
			return;
		var room = GetCell( gx, gy );
		if ( room is null )
			return;

		room.Cleanliness = Math.Clamp( room.Cleanliness - GameBalance.DirtPerOccupiedTick, 0f, 1f );
		var expectation = State.ReputationLevel * GameBalance.GuestExpectationPenaltyPerRep;
		guest.Satisfaction = Math.Clamp(
			guest.Satisfaction + (room.Cleanliness - 0.5f) * 0.01f - expectation * 0.002f + weatherSat * 0.002f,
			0.05f, 1f );

		if ( room.Broken )
			guest.Satisfaction = Math.Clamp( guest.Satisfaction - 0.02f, 0.05f, 1f );
		else if ( NextFloat() < GameBalance.BreakChancePerOccupiedTick )
			room.Broken = true;

		if ( !room.Broken && NextFloat() < GameBalance.AmenityVisitChancePerTick )
			TryStartAmenityVisit( guest );

		guest.StayRemaining -= GameBalance.TickSeconds;
		if ( guest.StayRemaining <= 0 )
		{
			guest.Phase = GuestPhase.CheckingOut;
			guest.PhaseTimer = HasStaffRole( StaffRole.Receptionist ) ? 1.2f : 2.0f;
			guest.TargetX = 0;
			guest.TargetY = 0;
			room.OccupantGuestIds.Remove( guest.Id );
			room.Cleanliness = Math.Clamp( room.Cleanliness - 0.35f, 0f, 1f );
			guest.RoomX = guest.RoomY = null;
		}
	}

	void TryStartAmenityVisit( Guest guest )
	{
		var amenities = State.Cells
			.Where( c =>
			{
				var d = GameBalance.GetRoom( c.Type );
				if ( d.Category != RoomCategory.Amenity || c.UnderConstruction || c.Broken )
					return false;
				if ( c.Type == RoomType.Restaurant && !KitchenIsStaffed( c ) )
					return false;
				var cap = GameBalance.CapacityAtLevel( d, c.Level );
				return c.OccupantGuestIds.Count < cap;
			} )
			.ToList();
		if ( amenities.Count == 0 )
			return;

		// Prefer spa/restaurant when preferred tier is high.
		amenities = amenities
			.OrderByDescending( c => guest.PreferredTier >= 2 && c.Type is RoomType.Spa or RoomType.Restaurant )
			.ThenBy( _ => NextInt( 100 ) )
			.ToList();

		var pick = amenities[0];
		guest.Phase = GuestPhase.VisitingAmenity;
		guest.AmenityX = pick.X;
		guest.AmenityY = pick.Y;
		guest.TargetX = pick.X;
		guest.TargetY = pick.Y;
		guest.AmenityArrived = false;
		guest.PhaseTimer = 99f;
		pick.OccupantGuestIds.Add( guest.Id );
	}

	void FinishAmenityVisit( Guest guest )
	{
		var amenity = GetCell( guest.AmenityX ?? 0, guest.AmenityY ?? 0 );
		if ( amenity is not null )
		{
			amenity.OccupantGuestIds.Remove( guest.Id );
			var def = GameBalance.GetRoom( amenity.Type );
			var pay = GameBalance.RateAtLevel( def, amenity.Level );
			ApplyIncome( pay, $"{def.DisplayName} visit" );
			guest.SpentCents += pay;
			guest.Satisfaction = Math.Clamp( guest.Satisfaction + GameBalance.AmenitySatisfactionBonus( amenity.Type ), 0f, 1f );
		}
		guest.AmenityX = guest.AmenityY = null;
		guest.AmenityArrived = false;
		guest.Phase = GuestPhase.Staying;
		if ( guest.RoomX is int rx && guest.RoomY is int ry )
		{
			guest.TargetX = rx;
			guest.TargetY = ry;
		}
	}

	bool TryAssignLodging( Guest guest )
	{
		var rooms = State.Cells
			.Where( c =>
			{
				var d = GameBalance.GetRoom( c.Type );
				return d.Category == RoomCategory.Lodging && !c.UnderConstruction && !c.Broken
					&& c.OccupantGuestIds.Count < GameBalance.CapacityAtLevel( d, c.Level );
			} )
			.OrderByDescending( c => GameBalance.GetRoom( c.Type ).LodgingTier >= guest.PreferredTier )
			.ThenByDescending( c => GameBalance.GetRoom( c.Type ).LodgingTier )
			.ThenByDescending( c => c.Cleanliness )
			.ToList();

		if ( rooms.Count == 0 )
			return false;

		var room = rooms[0];
		var tier = GameBalance.GetRoom( room.Type ).LodgingTier;
		room.OccupantGuestIds.Add( guest.Id );
		guest.RoomX = room.X;
		guest.RoomY = room.Y;
		guest.TargetX = room.X;
		guest.TargetY = room.Y;
		guest.LastLodgingType = room.Type;
		guest.LastLodgingLevel = room.Level;
		guest.ClaimedByEmployeeId = null;

		if ( tier < guest.PreferredTier )
			guest.Satisfaction = Math.Clamp( guest.Satisfaction - 0.08f * (guest.PreferredTier - tier), 0.05f, 1f );
		else if ( tier > guest.PreferredTier )
			guest.Satisfaction = Math.Clamp( guest.Satisfaction + 0.04f, 0.05f, 1f );

		return true;
	}

	void FinishCheckout( Guest guest )
	{
		var def = GameBalance.GetRoom( guest.LastLodgingType );
		var rate = GameBalance.RateAtLevel( def, guest.LastLodgingLevel );
		var pay = (long)(rate * guest.Nights * (0.6f + guest.Satisfaction * 0.6f));
		ApplyIncome( pay, $"{guest.Name} checkout" );
		guest.SpentCents += pay;
		if ( guest.Satisfaction >= GameBalance.TipSatisfactionThreshold )
		{
			var tip = GameBalance.TipBaseCents + (long)(guest.Satisfaction * 100);
			ApplyIncome( tip, "Tip" );
			guest.SpentCents += tip;
		}
		guest.Phase = GuestPhase.Leaving;
		guest.TargetX = -1.5f;
		guest.TargetY = 0;
		State.TotalGuestsServed++;
		CompleteTutorial( "earn_money", "upgrade_room" );
	}

	bool HasStaffRole( StaffRole role ) => State.Employees.Any( e => e.Role == role );

	void ReleaseReceptionClaims( int guestId )
	{
		foreach ( var emp in State.Employees.Where( e => e.ServingGuestId == guestId ) )
		{
			emp.ServingGuestId = null;
			if ( emp.Task == EmployeeTask.CheckIn )
				emp.Task = EmployeeTask.Idle;
		}
	}

	bool IsRoomClaimed( int x, int y, int exceptEmployeeId ) =>
		State.Employees.Any( e =>
			e.Id != exceptEmployeeId
			&& e.TargetRoomX == x && e.TargetRoomY == y
			&& e.Task is EmployeeTask.Walk or EmployeeTask.Clean or EmployeeTask.Repair or EmployeeTask.Cook );

	void AssignEmployeeTasks()
	{
		foreach ( var emp in State.Employees.Where( e => e.Task == EmployeeTask.Idle ) )
		{
			switch ( emp.Role )
			{
				case StaffRole.Receptionist:
					var waiting = State.Guests.FirstOrDefault( g =>
						g.Phase == GuestPhase.CheckingIn
						&& (g.ClaimedByEmployeeId is null || g.ClaimedByEmployeeId == emp.Id) );
					if ( waiting is not null )
					{
						waiting.ClaimedByEmployeeId = emp.Id;
						emp.Task = EmployeeTask.Walk;
						emp.PendingWork = EmployeeTask.CheckIn;
						emp.ServingGuestId = waiting.Id;
						emp.TargetX = 0;
						emp.TargetY = 0;
						emp.TargetRoomX = 0;
						emp.TargetRoomY = 0;
						emp.TaskTimer = 1.4f;
					}
					else
						SendIdleStaffHome( emp );
					break;

				case StaffRole.Housekeeper:
					var dirty = FindDirtyRoom( emp );
					if ( dirty is not null )
						StartWalkTask( emp, dirty, EmployeeTask.Clean, 2.5f );
					else
						SendIdleStaffHome( emp );
					break;

				case StaffRole.Cook:
					var kitchen = FindKitchen( emp );
					if ( kitchen is not null )
						StartWalkTask( emp, kitchen, EmployeeTask.Cook, 999f );
					else
						SendIdleStaffHome( emp );
					break;

				case StaffRole.MaintenanceWorker:
					var broken = FindBrokenRoom( emp );
					if ( broken is not null )
						StartWalkTask( emp, broken, EmployeeTask.Repair, 3.5f );
					else
						SendIdleStaffHome( emp );
					break;
			}
		}
	}

	GridCell FindDirtyRoom( Employee emp )
	{
		var assigned = GetAssignedRoom( emp );
		if ( assigned is not null
			&& GameBalance.GetRoom( assigned.Type ).Category == RoomCategory.Lodging
			&& assigned.Cleanliness < 0.9f
			&& !IsRoomClaimed( assigned.X, assigned.Y, emp.Id ) )
			return assigned;

		return State.Cells
			.Where( c => GameBalance.GetRoom( c.Type ).Category == RoomCategory.Lodging
				&& c.Cleanliness < 0.65f && !c.UnderConstruction
				&& !IsRoomClaimed( c.X, c.Y, emp.Id ) )
			.OrderBy( c => c.Cleanliness )
			.FirstOrDefault();
	}

	GridCell FindKitchen( Employee emp )
	{
		var assigned = GetAssignedRoom( emp );
		if ( assigned is not null
			&& assigned.Type is RoomType.Cafe or RoomType.Restaurant
			&& !assigned.Broken && !assigned.UnderConstruction )
			return assigned;

		return State.Cells.FirstOrDefault( c =>
			(c.Type == RoomType.Restaurant || c.Type == RoomType.Cafe)
			&& !c.UnderConstruction && !c.Broken
			&& !IsRoomClaimed( c.X, c.Y, emp.Id ) );
	}

	GridCell FindBrokenRoom( Employee emp )
	{
		var assigned = GetAssignedRoom( emp );
		if ( assigned is not null && assigned.Broken && !IsRoomClaimed( assigned.X, assigned.Y, emp.Id ) )
			return assigned;

		return State.Cells.FirstOrDefault( c => c.Broken && !c.UnderConstruction && !IsRoomClaimed( c.X, c.Y, emp.Id ) );
	}

	void SendIdleStaffHome( Employee emp )
	{
		var home = emp.AssignedRoomX is int ax && emp.AssignedRoomY is int ay
			? GetCell( ax, ay )
			: State.Cells.FirstOrDefault( c => c.Type == RoomType.StaffRoom && !c.UnderConstruction )
				?? GetCell( 0, 0 );
		if ( home is null )
			return;
		if ( MathF.Abs( emp.PosX - home.X ) < 0.2f && MathF.Abs( emp.PosY - home.Y ) < 0.2f )
			return;
		emp.Task = EmployeeTask.Walk;
		emp.PendingWork = EmployeeTask.Idle;
		emp.TargetX = home.X;
		emp.TargetY = home.Y;
		emp.TargetRoomX = home.X;
		emp.TargetRoomY = home.Y;
		emp.TaskTimer = 0f;
	}

	GridCell GetAssignedRoom( Employee employee )
	{
		return employee.AssignedRoomX is int x && employee.AssignedRoomY is int y
			? GetCell( x, y )
			: null;
	}

	void StartWalkTask( Employee emp, GridCell cell, EmployeeTask work, float workTime )
	{
		emp.Task = EmployeeTask.Walk;
		emp.PendingWork = work;
		emp.TargetRoomX = cell.X;
		emp.TargetRoomY = cell.Y;
		emp.TargetX = cell.X;
		emp.TargetY = cell.Y;
		emp.TaskTimer = workTime;
		emp.ServingGuestId = null;
	}

	void UpdateEmployees()
	{
		foreach ( var emp in State.Employees )
		{
			var ex = emp.PosX;
			var ey = emp.PosY;
			var face = emp.FacingLeft;
			MoveToward( ref ex, ref ey, emp.TargetX, emp.TargetY, 2.2f, out var arrived, ref face );
			emp.PosX = ex;
			emp.PosY = ey;
			emp.FacingLeft = face;

			if ( emp.Task == EmployeeTask.Walk && arrived )
			{
				emp.Task = emp.PendingWork;
				emp.PendingWork = EmployeeTask.Idle;
				if ( emp.Task == EmployeeTask.Idle )
				{
					emp.TargetRoomX = emp.TargetRoomY = null;
					continue;
				}
			}

			if ( emp.Task == EmployeeTask.CheckIn && arrived )
			{
				emp.TaskTimer -= GameBalance.TickSeconds;
				if ( emp.TaskTimer <= 0 )
				{
					var guest = State.Guests.FirstOrDefault( g => g.Id == emp.ServingGuestId );
					if ( guest is not null && guest.Phase == GuestPhase.CheckingIn && TryAssignLodging( guest ) )
					{
						guest.Phase = GuestPhase.Staying;
						guest.Satisfaction = Math.Clamp( guest.Satisfaction + 0.05f, 0.05f, 1f );
						CompleteTutorial( "first_guest", null );
					}
					emp.ServingGuestId = null;
					emp.Task = EmployeeTask.Idle;
					emp.TargetRoomX = emp.TargetRoomY = null;
				}
			}
			else if ( emp.Task == EmployeeTask.Clean && arrived )
			{
				var cell = emp.TargetRoomX is int x && emp.TargetRoomY is int y ? GetCell( x, y ) : null;
				if ( cell is not null )
				{
					var speed = 0.08f * (1f + EffectiveSupportBonus( RoomType.Laundry, GameBalance.LaundryCleanSpeedBonus ));
					cell.Cleanliness = Math.Clamp( cell.Cleanliness + speed, 0f, 1f );
					emp.TaskTimer -= GameBalance.TickSeconds;
					if ( cell.Cleanliness >= 1f || emp.TaskTimer <= 0 )
					{
						cell.Cleanliness = Math.Max( cell.Cleanliness, 0.95f );
						emp.Task = EmployeeTask.Idle;
						emp.TargetRoomX = emp.TargetRoomY = null;
						CompleteTutorial( "hire_staff", "first_guest" );
					}
				}
				else
					emp.Task = EmployeeTask.Idle;
			}
			else if ( emp.Task == EmployeeTask.Repair && arrived )
			{
				var cell = emp.TargetRoomX is int x && emp.TargetRoomY is int y ? GetCell( x, y ) : null;
				if ( cell is not null && cell.Broken )
				{
					emp.TaskTimer -= GameBalance.TickSeconds
						* (1f + EffectiveSupportBonus( RoomType.MaintenanceWorkshop, GameBalance.WorkshopRepairSpeedBonus ));
					if ( emp.TaskTimer <= 0 )
					{
						cell.Broken = false;
						State.DayRepairsDone++;
						emp.Task = EmployeeTask.Idle;
						emp.TargetRoomX = emp.TargetRoomY = null;
					}
				}
				else
					emp.Task = EmployeeTask.Idle;
			}
			else if ( emp.Task == EmployeeTask.Cook )
			{
				if ( emp.TargetRoomX is int cx && emp.TargetRoomY is int cy )
				{
					var cell = GetCell( cx, cy );
					if ( cell is null || cell.Broken || cell.UnderConstruction )
					{
						emp.Task = EmployeeTask.Idle;
						emp.TargetRoomX = emp.TargetRoomY = null;
					}
				}
			}
		}
	}

	void MoveToward( ref float x, ref float y, float tx, float ty, float speed, out bool arrived, ref bool facingLeft )
	{
		var finalDy = ty - y;
		var elevatorX = State.Cells.Count > 0 ? State.Cells.Min( c => c.X ) - 0.72f : 0f;
		var waypointX = tx;
		var waypointY = ty;

		if ( MathF.Abs( finalDy ) > 0.01f )
		{
			if ( MathF.Abs( x - elevatorX ) > 0.01f )
			{
				waypointX = elevatorX;
				waypointY = y;
			}
			else
			{
				waypointX = elevatorX;
				waypointY = ty;
			}
		}

		var dx = waypointX - x;
		var dy = waypointY - y;
		var dist = MathF.Sqrt( dx * dx + dy * dy );
		var step = speed * GameBalance.TickSeconds;
		if ( dist <= step || dist < 0.001f )
		{
			x = waypointX;
			y = waypointY;
			arrived = MathF.Abs( tx - x ) < 0.01f && MathF.Abs( ty - y ) < 0.01f;
			return;
		}

		if ( MathF.Abs( dx ) > 0.01f )
			facingLeft = dx < 0;
		x += dx / dist * step;
		y += dy / dist * step;
		arrived = false;
	}

	void UpdateReputation()
	{
		var profit = LifetimeProfit;
		var level = 1;
		for ( var i = GameBalance.ReputationProfitThresholds.Length - 1; i >= 1; i-- )
		{
			if ( profit >= GameBalance.ReputationProfitThresholds[i] )
			{
				level = i;
				break;
			}
		}

		// Soft prestige beyond level 6.
		if ( level >= 6 )
		{
			var extra = profit - GameBalance.ReputationProfitThresholds[^1];
			if ( extra > 0 )
				level = 6 + (int)(extra / GameBalance.SoftPrestigeProfitStep);
		}

		if ( AverageSatisfaction < 0.4f && level > 1 )
			level = Math.Max( 1, level - 1 );

		// Never drop below the best unlock tier the player has earned permanently.
		level = Math.Max( level, Math.Min( State.PeakReputationLevel, 6 ) );

		if ( level != State.ReputationLevel )
		{
			var up = level > State.ReputationLevel;
			State.ReputationLevel = level;
			State.PeakReputationLevel = Math.Max( State.PeakReputationLevel, level );
			if ( up )
				State.StatusMessage = level > 6
					? $"Soft prestige {GameBalance.SoftPrestigeLevel( level )}! Demand is booming."
					: $"Reputation reached level {level}!";
		}
	}

	void RefreshDailyGoals( bool force )
	{
		var day = GameBalance.DayFromSimTime( State.SimTime );
		if ( !force && day == State.GoalsDay && State.DailyGoals.Count > 0 )
			return;

		State.GoalsDay = day;
		State.DayIncomeBaseline = State.LifetimeIncomeCents;
		State.DayGuestsBaseline = State.TotalGuestsServed;
		State.DayRoomsBaseline = RoomCount;
		State.DayStaffBaseline = State.Employees.Count;
		State.DayRepairsDone = 0;
		State.DailyGoals = GenerateDailyGoals();
		if ( !force )
			Notify( "New daily goals are ready." );
	}

	List<DailyGoal> GenerateDailyGoals()
	{
		var pool = new List<DailyGoal>
		{
			new()
			{
				Id = "build",
				Title = "Expand the skyline",
				Description = "Build more rooms today",
				Metric = DailyGoalMetric.BuildRooms,
				Target = Math.Max( 1, 2 + State.ReputationLevel / 2 ),
				RewardCents = 150_00 + State.ReputationLevel * 25_00
			},
			new()
			{
				Id = "guests",
				Title = "Warm welcome",
				Description = "Check guests out successfully",
				Metric = DailyGoalMetric.ServeGuests,
				Target = Math.Max( 3, 5 + State.ReputationLevel * 2 ),
				RewardCents = 120_00 + State.ReputationLevel * 20_00
			},
			new()
			{
				Id = "earn",
				Title = "Busy front desk",
				Description = "Earn cash today",
				Metric = DailyGoalMetric.EarnCents,
				Target = (int)(800_00 + State.ReputationLevel * 400_00),
				RewardCents = 200_00 + State.ReputationLevel * 30_00
			},
			new()
			{
				Id = "occupancy",
				Title = "Packed floors",
				Description = "Reach high occupancy",
				Metric = DailyGoalMetric.ReachOccupancy,
				Target = Math.Clamp( 50 + State.ReputationLevel * 5, 50, 85 ),
				RewardCents = 175_00
			},
			new()
			{
				Id = "hire",
				Title = "Grow the team",
				Description = "Hire additional staff",
				Metric = DailyGoalMetric.HireStaff,
				Target = 1,
				RewardCents = 100_00
			},
			new()
			{
				Id = "repair",
				Title = "Keep it running",
				Description = "Finish maintenance jobs",
				Metric = DailyGoalMetric.RepairRooms,
				Target = Math.Max( 1, State.ReputationLevel / 2 ),
				RewardCents = 130_00
			}
		};

		var picks = new List<DailyGoal>();
		while ( picks.Count < 3 && pool.Count > 0 )
		{
			var idx = NextInt( pool.Count );
			picks.Add( pool[idx] );
			pool.RemoveAt( idx );
		}
		return picks;
	}

	public int GoalProgress( DailyGoal goal )
	{
		if ( goal is null )
			return 0;
		return goal.Metric switch
		{
			DailyGoalMetric.BuildRooms => Math.Max( 0, RoomCount - State.DayRoomsBaseline ),
			DailyGoalMetric.ServeGuests => Math.Max( 0, State.TotalGuestsServed - State.DayGuestsBaseline ),
			DailyGoalMetric.EarnCents => (int)Math.Max( 0, State.LifetimeIncomeCents - State.DayIncomeBaseline ),
			DailyGoalMetric.ReachOccupancy => (int)(Occupancy * 100),
			DailyGoalMetric.HireStaff => Math.Max( 0, State.Employees.Count - State.DayStaffBaseline ),
			DailyGoalMetric.RepairRooms => State.DayRepairsDone,
			_ => 0
		};
	}

	public bool IsGoalComplete( DailyGoal goal ) => goal is not null && GoalProgress( goal ) >= goal.Target;

	public SimCommandResult TryClaimGoal( string goalId )
	{
		var goal = State.DailyGoals.FirstOrDefault( g => g.Id == goalId );
		if ( goal is null )
			return SimCommandResult.Fail( "Goal not found." );
		if ( goal.Claimed )
			return SimCommandResult.Fail( "Already claimed." );
		if ( !IsGoalComplete( goal ) )
			return SimCommandResult.Fail( "Goal is not finished yet." );

		goal.Claimed = true;
		ApplyIncome( goal.RewardCents, $"Goal: {goal.Title}" );
		Notify( $"Claimed {FormatMoney( goal.RewardCents )} for {goal.Title}." );
		return SimCommandResult.Success();
	}

	void ApplyIncome( long cents, string label )
	{
		if ( cents <= 0 )
			return;
		State.CashCents += cents;
		State.LifetimeIncomeCents += cents;
		PushLedger( label, cents );
	}

	void ApplyExpense( long cents, string label, bool writeLedger = true )
	{
		if ( cents <= 0 )
			return;
		State.CashCents -= cents;
		State.LifetimeExpenseCents += cents;
		if ( writeLedger )
			PushLedger( label, -cents );
	}

	void PushLedger( string label, long delta )
	{
		State.Ledger.Add( new LedgerEntry { SimTime = State.SimTime, Label = label, DeltaCents = delta } );
		if ( State.Ledger.Count > 40 )
			State.Ledger.RemoveAt( 0 );
	}

	public SimCommandResult TryBuild( RoomType type, int x, int y )
	{
		var def = GameBalance.GetRoom( type );
		if ( def.Category == RoomCategory.Structure )
			return SimCommandResult.Fail( "Cannot build another lobby." );
		if ( !IsUnlocked( type ) )
			return SimCommandResult.Fail( $"{def.DisplayName} locked (rep {def.UnlockReputation})." );
		if ( HasCell( x, y ) )
			return SimCommandResult.Fail( "Cell occupied." );
		if ( !IsAdjacentToHotel( x, y ) )
			return SimCommandResult.Fail( "Must touch the hotel." );
		if ( y < 0 )
			return SimCommandResult.Fail( "Cannot build below ground." );

		var cost = CurrentBuildCost( type );
		if ( State.CashCents < cost )
			return SimCommandResult.Fail( "Not enough cash." );

		ApplyExpense( cost, $"Build {def.DisplayName}" );
		State.Cells.Add( new GridCell
		{
			X = x,
			Y = y,
			Type = type,
			Level = 1,
			Cleanliness = 1f,
			UnderConstruction = true,
			ConstructionRemaining = 2.5f
		} );
		Notify( $"Building {def.DisplayName}" );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryDemolish( int x, int y )
	{
		var cell = GetCell( x, y );
		if ( cell is null )
			return SimCommandResult.Fail( "No room." );
		if ( cell.Type == RoomType.Lobby )
			return SimCommandResult.Fail( "Lobby cannot be demolished." );
		if ( cell.OccupantGuestIds.Count > 0 )
			return SimCommandResult.Fail( "Room still has guests." );
		if ( State.Guests.Any( g => g.AmenityX == x && g.AmenityY == y ) )
			return SimCommandResult.Fail( "Guests are using this room." );
		if ( State.Employees.Any( e => e.TargetRoomX == x && e.TargetRoomY == y && e.Task != EmployeeTask.Idle ) )
			return SimCommandResult.Fail( "Staff are working here." );

		// Connectivity: remaining rooms must all reach the lobby.
		var remaining = State.Cells.Where( c => !(c.X == x && c.Y == y) ).ToList();
		if ( !IsConnectedToLobby( remaining ) )
			return SimCommandResult.Fail( "That would disconnect the hotel." );

		var refund = GameBalance.DemolishRefund( cell.Type, cell.Level, RoomCount );
		var name = GameBalance.GetRoom( cell.Type ).DisplayName;

		foreach ( var emp in State.Employees.Where( e => e.AssignedRoomX == x && e.AssignedRoomY == y ) )
		{
			emp.AssignedRoomX = emp.AssignedRoomY = null;
			emp.Task = EmployeeTask.Idle;
		}

		State.Cells.Remove( cell );
		if ( refund > 0 )
			ApplyIncome( refund, $"Demolish {name}" );
		Notify( $"Demolished {name} · refunded {FormatMoney( refund )}" );
		return SimCommandResult.Success();
	}

	static bool IsConnectedToLobby( List<GridCell> cells )
	{
		var lobby = cells.FirstOrDefault( c => c.Type == RoomType.Lobby );
		if ( lobby is null )
			return false;
		var set = cells.Select( c => (c.X, c.Y) ).ToHashSet();
		var seen = new HashSet<(int, int)>();
		var queue = new Queue<(int, int)>();
		queue.Enqueue( (lobby.X, lobby.Y) );
		seen.Add( (lobby.X, lobby.Y) );
		while ( queue.Count > 0 )
		{
			var (cx, cy) = queue.Dequeue();
			foreach ( var (nx, ny) in new[] { (cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1) } )
			{
				if ( !set.Contains( (nx, ny) ) || !seen.Add( (nx, ny) ) )
					continue;
				queue.Enqueue( (nx, ny) );
			}
		}
		return seen.Count == cells.Count;
	}

	public SimCommandResult TryUpgrade( int x, int y )
	{
		var cell = GetCell( x, y );
		if ( cell is null )
			return SimCommandResult.Fail( "No room." );
		if ( cell.Type == RoomType.Lobby )
			return SimCommandResult.Fail( "Lobby cannot be upgraded." );
		if ( cell.UnderConstruction )
			return SimCommandResult.Fail( "Still building." );
		if ( cell.Level >= GameBalance.MaxRoomLevel )
			return SimCommandResult.Fail( "Max level." );

		var cost = GameBalance.UpgradeCost( cell.Type, cell.Level );
		if ( State.CashCents < cost )
			return SimCommandResult.Fail( "Not enough cash." );

		ApplyExpense( cost, $"Upgrade {GameBalance.GetRoom( cell.Type ).DisplayName}" );
		cell.Level++;
		CompleteTutorial( "upgrade_room", null );
		Notify( $"Upgraded to level {cell.Level}" );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryHire( StaffRole role )
	{
		if ( !IsUnlocked( role ) )
			return SimCommandResult.Fail( "Role locked." );
		var def = GameBalance.GetStaff( role );
		if ( State.CashCents < def.HireCostCents )
			return SimCommandResult.Fail( "Not enough cash." );

		ApplyExpense( def.HireCostCents, $"Hire {def.DisplayName}" );
		var emp = new Employee
		{
			Id = State.NextEmployeeId++,
			Role = role,
			Name = NameBank.FromIndex( NextInt( NameBank.First.Length ) ),
			PosX = 0,
			PosY = 0,
			TargetX = 0,
			TargetY = 0
		};
		State.Employees.Add( emp );
		CompleteTutorial( "hire_staff", "first_guest" );
		Notify( $"Hired {emp.Name} ({def.DisplayName})" );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryFire( int employeeId )
	{
		var emp = State.Employees.FirstOrDefault( e => e.Id == employeeId );
		if ( emp is null )
			return SimCommandResult.Fail( "Staff not found." );
		foreach ( var cell in State.Cells.Where( c => c.AssignedEmployeeId == employeeId ) )
			cell.AssignedEmployeeId = null;
		foreach ( var guest in State.Guests.Where( g => g.ClaimedByEmployeeId == employeeId ) )
			guest.ClaimedByEmployeeId = null;
		State.Employees.Remove( emp );
		Notify( $"Let go {emp.Name}" );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryAssignEmployee( int employeeId, int x, int y )
	{
		var employee = State.Employees.FirstOrDefault( e => e.Id == employeeId );
		if ( employee is null )
			return SimCommandResult.Fail( "Staff member not found." );

		var room = GetCell( x, y );
		if ( room is null || room.UnderConstruction )
			return SimCommandResult.Fail( "Choose a completed room." );

		if ( !GameBalance.CanAssignStaff( employee.Role, room.Type ) )
			return SimCommandResult.Fail( $"{employee.Role} cannot be assigned there." );

		if ( room.AssignedEmployeeId is int current && current != employeeId )
			return SimCommandResult.Fail( "That room already has assigned staff." );

		foreach ( var cell in State.Cells.Where( c => c.AssignedEmployeeId == employeeId ) )
			cell.AssignedEmployeeId = null;
		room.AssignedEmployeeId = employeeId;
		employee.AssignedRoomX = x;
		employee.AssignedRoomY = y;
		employee.Task = EmployeeTask.Idle;
		employee.PendingWork = EmployeeTask.Idle;
		employee.TargetRoomX = employee.TargetRoomY = null;
		Notify( $"Assigned {employee.Name} to {GameBalance.GetRoom( room.Type ).DisplayName}." );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryAutoAssignEmployee(
		int x,
		int y,
		out bool noAvailableStaff,
		StaffRole? requiredRole = null )
	{
		noAvailableStaff = false;
		var room = GetCell( x, y );
		if ( room is null || room.UnderConstruction )
			return SimCommandResult.Fail( "Choose a completed room." );
		if ( requiredRole is StaffRole role && !GameBalance.CanAssignStaff( role, room.Type ) )
			return SimCommandResult.Fail( $"{GameBalance.GetStaff( role ).DisplayName} cannot be assigned there." );

		if ( room.AssignedEmployeeId is int assignedId )
		{
			var assigned = State.Employees.FirstOrDefault( e => e.Id == assignedId );
			if ( assigned is not null )
				return SimCommandResult.Success( $"{assigned.Name} is already assigned here." );
			room.AssignedEmployeeId = null;
		}

		var employee = State.Employees
			.Where( IsAvailableForAssignment )
			.Where( e => requiredRole is null || e.Role == requiredRole )
			.Where( e => GameBalance.CanAssignStaff( e.Role, room.Type ) )
			.OrderBy( e => e.Role == GameBalance.PreferredStaffRole( room.Type ) ? 0 : 1 )
			.ThenBy( e => e.AssignedRoomX.HasValue ? 1 : 0 )
			.ThenBy( e => MathF.Abs( e.PosX - x ) + MathF.Abs( e.PosY - y ) )
			.FirstOrDefault();

		if ( employee is null )
		{
			noAvailableStaff = true;
			var roleName = requiredRole is StaffRole missingRole
				? GameBalance.GetStaff( missingRole ).DisplayName
				: "suitable staff";
			return SimCommandResult.Fail( $"No available {roleName}. Hire someone or wait until they finish." );
		}

		return TryAssignEmployee( employee.Id, x, y );
	}

	static bool IsAvailableForAssignment( Employee employee ) =>
		employee.Task == EmployeeTask.Idle
		|| (employee.Task == EmployeeTask.Walk && employee.PendingWork == EmployeeTask.Idle);

	public SimCommandResult TryUnassignEmployee( int employeeId )
	{
		var employee = State.Employees.FirstOrDefault( e => e.Id == employeeId );
		if ( employee is null )
			return SimCommandResult.Fail( "Staff member not found." );
		foreach ( var cell in State.Cells.Where( c => c.AssignedEmployeeId == employeeId ) )
			cell.AssignedEmployeeId = null;
		employee.AssignedRoomX = employee.AssignedRoomY = null;
		employee.Task = EmployeeTask.Idle;
		Notify( $"{employee.Name} is now on general duty." );
		return SimCommandResult.Success();
	}

	public SimCommandResult TryDispatchService( StaffRole role, int x, int y )
	{
		var room = GetCell( x, y );
		if ( room is null || room.UnderConstruction )
			return SimCommandResult.Fail( "Choose a completed room." );

		var task = role switch
		{
			StaffRole.Housekeeper => EmployeeTask.Clean,
			StaffRole.MaintenanceWorker => EmployeeTask.Repair,
			StaffRole.Cook => EmployeeTask.Cook,
			_ => EmployeeTask.Idle
		};

		if ( task == EmployeeTask.Clean
			&& GameBalance.GetRoom( room.Type ).Category != RoomCategory.Lodging )
			return SimCommandResult.Fail( "Only lodging rooms need housekeeping." );
		if ( task == EmployeeTask.Clean && room.Cleanliness >= 0.95f )
			return SimCommandResult.Fail( "This room is already clean." );
		if ( task == EmployeeTask.Repair && !room.Broken )
			return SimCommandResult.Fail( "This room does not need repairs." );
		if ( task == EmployeeTask.Cook && room.Type is not (RoomType.Cafe or RoomType.Restaurant) )
			return SimCommandResult.Fail( "Cooks can only staff food rooms." );
		if ( task == EmployeeTask.Idle )
			return SimCommandResult.Fail( "That staff role cannot service this room." );

		var employee = State.Employees
			.Where( e => e.Role == role && e.Task == EmployeeTask.Idle )
			.OrderBy( e => MathF.Abs( e.PosX - x ) + MathF.Abs( e.PosY - y ) )
			.FirstOrDefault();
		if ( employee is null )
			return SimCommandResult.Fail( $"No available {GameBalance.GetStaff( role ).DisplayName}. Hire one or wait until they finish." );

		var workTime = task switch
		{
			EmployeeTask.Clean => 2.5f,
			EmployeeTask.Repair => 3.5f,
			EmployeeTask.Cook => 999f,
			_ => 1f
		};
		StartWalkTask( employee, room, task, workTime );
		Notify( $"Sent {employee.Name} to {GameBalance.GetRoom( room.Type ).DisplayName}." );
		return SimCommandResult.Success();
	}

	void CompleteTutorial( string id, string next )
	{
		if ( !State.CompletedTutorials.Contains( id ) )
			State.CompletedTutorials.Add( id );
		if ( State.ActiveTutorial == id )
			State.ActiveTutorial = next;
	}

	public void DismissTutorial()
	{
		if ( State.ActiveTutorial is not null )
		{
			if ( !State.CompletedTutorials.Contains( State.ActiveTutorial ) )
				State.CompletedTutorials.Add( State.ActiveTutorial );
			State.ActiveTutorial = null;
			Notify();
		}
	}

	public long ApplyOfflineProgress()
	{
		var elapsed = DateTimeOffset.UtcNow - State.LastRealWorldUtc;
		var hours = Math.Clamp( (float)elapsed.TotalHours, 0f, GameBalance.MaxOfflineHours );
		if ( hours < 1f / 60f )
			return 0;

		var perMin = EstimateNetIncomePerMinuteCents();
		var minutes = hours * 60f * GameBalance.OfflineEfficiency;
		var delta = (long)(perMin * minutes);
		State.LastRealWorldUtc = DateTimeOffset.UtcNow;

		if ( delta > 0 )
		{
			ApplyIncome( delta, "Offline earnings" );
			State.OfflineEarningsAppliedCents = delta;
			Notify( $"Welcome back! Earned {FormatMoney( delta )} offline." );
		}
		else if ( delta < 0 )
		{
			ApplyExpense( -delta, "Offline expenses" );
			State.OfflineEarningsAppliedCents = delta;
			Notify( $"While you were away, expenses ran {FormatMoney( -delta )}." );
		}
		return delta;
	}

	public void LoadState( HotelState state )
	{
		State = state ?? CreateNewGame();
		EnsurePostMvpDefaults();
		SyncRng();
		Notify();
	}

	public static string FormatMoney( long cents )
	{
		var sign = cents < 0 ? "-" : "";
		var abs = Math.Abs( cents );
		return $"{sign}${abs / 100:N0}.{abs % 100:00}";
	}

	public IEnumerable<(int X, int Y)> EnumerateBuildCandidates()
	{
		var seen = new HashSet<(int, int)>();
		foreach ( var cell in State.Cells )
		{
			foreach ( var (dx, dy) in new[] { (-1, 0), (1, 0), (0, 1), (0, -1) } )
			{
				var nx = cell.X + dx;
				var ny = cell.Y + dy;
				if ( ny < 0 || HasCell( nx, ny ) )
					continue;
				if ( seen.Add( (nx, ny) ) )
					yield return (nx, ny);
			}
		}
	}
}
