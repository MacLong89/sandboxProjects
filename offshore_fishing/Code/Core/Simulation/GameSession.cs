namespace OffshoreFishing.Core;

/// <summary>
/// Portable game facade. Presentation adapters call commands and poll events.
/// </summary>
public sealed class GameSession
{
	private readonly GameContent _content;
	private readonly FishingSimulator _fishing;
	private readonly ShopService _shop;
	private readonly ProgressionService _progression;
	private readonly HiredBoatService _hired;
	private readonly List<IDomainEvent> _events = new();
	private SeededRng _rng;

	public GameState State { get; private set; }
	public GameContent Content => _content;
	public IReadOnlyList<IDomainEvent> PendingEvents => _events;

	public GameSession( GameContent content, GameState state = null )
	{
		_content = content ?? throw new ArgumentNullException( nameof( content ) );
		State = state ?? CreateNewState();
		_rng = new SeededRng( State.Seed );
		_fishing = new FishingSimulator( _content );
		_shop = new ShopService( _content );
		_progression = new ProgressionService( _content );
		_hired = new HiredBoatService( _content );
		_progression.EnsureActiveObjective( State );
	}

	public static GameState CreateNewState( long? seed = null )
	{
		var s = new GameState
		{
			Seed = seed ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			Gold = 25,
			OwnedItemIds =
			{
				"rod_starter", "spool_basic", "hook_basic", "bait_worms", "boat_skiff"
			},
			Inventory =
			{
				["bait_worms"] = 20,
				["rod_starter"] = 1,
				["spool_basic"] = 1,
				["hook_basic"] = 1
			},
			ActiveObjectiveId = "obj_first_catch"
		};
		return s;
	}

	public IReadOnlyList<IDomainEvent> DrainEvents()
	{
		CollectChildEvents();
		var copy = _events.ToList();
		_events.Clear();
		return copy;
	}

	public void Advance( double dt )
	{
		if ( State.Mode == GameMode.Paused ) return;

		var fdt = (float)dt;
		State.PlayedSeconds += dt;
		State.TimeOfDayHours += fdt / 60f; // 1 real sec ~= 1 game minute feel for cozy pace
		if ( State.TimeOfDayHours >= 24f )
		{
			State.TimeOfDayHours -= 24f;
			State.Day++;
		}

		_hired.Tick( State, _rng, dt );
		CollectChildEvents();

		if ( State.Mode is GameMode.Fishing or GameMode.Travel )
			_fishing.Tick( State, _rng, fdt );

		CollectChildEvents();
	}

	public int ApplyOfflineProgress( DateTimeOffset nowUtc )
	{
		var offline = nowUtc - State.LastSavedUtc;
		var gained = _hired.ApplyOffline( State, _rng, offline );
		State.LastSavedUtc = nowUtc;
		CollectChildEvents();
		if ( gained > 0 )
			_events.Add( new NotificationEvent { Text = $"While away, crews earned {gained} gold." } );
		return gained;
	}

	public void SetMode( GameMode mode )
	{
		if ( State.Mode == mode ) return;
		State.Mode = mode;
		_events.Add( new ModeChangedEvent { Mode = mode } );
	}

	public void BoardBoat()
	{
		State.OnBoat = true;
		State.Mode = GameMode.Travel;
		State.BoatDistanceM = Math.Max( State.BoatDistanceM, 20f );
		_events.Add( new ModeChangedEvent { Mode = State.Mode } );
		_events.Add( new TutorialPromptEvent { Text = "Sail out, then cast your line." } );
	}

	public void Disembark()
	{
		State.OnBoat = false;
		State.BoatDistanceM = 0f;
		State.BoatDepthM = 0f;
		State.Mode = GameMode.Dock;
		State.Fishing = new FishingSession();
		_events.Add( new ModeChangedEvent { Mode = State.Mode } );
	}

	public void OpenShop()
	{
		if ( State.OnBoat ) return;
		State.Mode = GameMode.Shop;
		_events.Add( new ModeChangedEvent { Mode = State.Mode } );
	}

	public void CloseShop()
	{
		State.Mode = GameMode.Dock;
		_events.Add( new ModeChangedEvent { Mode = State.Mode } );
	}

	public void MoveOnDock( float deltaX, float dockMin, float dockMax )
	{
		if ( State.Mode != GameMode.Dock || State.OnBoat ) return;
		State.DockPlayerX = Math.Clamp( State.DockPlayerX + deltaX, dockMin, dockMax );
	}

	public void Travel( float direction, float dt )
	{
		if ( !State.OnBoat ) return;
		if ( State.Mode == GameMode.CatchReveal ) return;
		if ( State.Fishing.Phase is FishingPhase.Fighting or FishingPhase.BiteWindow or FishingPhase.Casting )
			return;

		var boat = _content.GetBoat( State.OwnedBoatId );
		State.BoatDistanceM = Math.Clamp( State.BoatDistanceM + direction * boat.Speed * dt * 12f, 0f, boat.MaxRangeM );
		State.FarthestDistanceM = Math.Max( State.FarthestDistanceM, State.BoatDistanceM );
		UpdateZoneFromDistance();
		_progression.OnDistance( State );
		_progression.CheckDistanceUnlocks( State );
		CollectChildEvents();

		if ( State.BoatDistanceM <= 5f && direction < 0f )
		{
			// Near dock marker.
		}

		if ( State.Mode != GameMode.Fishing )
			State.Mode = GameMode.Travel;
	}

	public void BeginCastAim()
	{
		if ( !State.OnBoat && State.Mode != GameMode.Dock ) return;
		// Allow dock casting for tutorial.
		if ( !State.OnBoat )
		{
			State.Mode = GameMode.Fishing;
			State.CurrentZoneId = "harbor";
		}
		_fishing.BeginAim( State );
		CollectChildEvents();
	}

	public void SetAim( float aimAngle ) => _fishing.SetAim( State, aimAngle );
	public void ChargeCast( float dt ) => _fishing.ChargeCast( State, dt );

	public void ReleaseCast()
	{
		_fishing.ReleaseCast( State, _rng );
		CollectChildEvents();
	}

	public void TryHook()
	{
		_fishing.TryHook( State, _rng );
		CollectChildEvents();
		// FishCaught handled after fight lands; progression wired in NotifyCatch from adapter polling.
	}

	public void SetReelHeld( bool held ) => _fishing.SetReelHeld( State, held );

	public void CloseCatchReveal()
	{
		var pending = State.Fishing.PendingCatch;
		var fishId = pending?.FishId;
		var wasNew = fishId != null && State.FishLog.TryGetValue( fishId, out var e ) && e.TimesCaught <= 1;
		_fishing.CloseCatchReveal( State );
		if ( pending != null )
		{
			_progression.OnFishCaught( State, pending, wasNew );
			_progression.EnsureActiveObjective( State );
		}
		CollectChildEvents();
	}

	public bool BuyItem( string id )
	{
		var ok = _shop.TryBuyItem( State, id );
		if ( ok )
		{
			_progression.OnItemBought( State, id );
			CollectChildEvents();
		}
		return ok;
	}

	public bool BuyBoat( string id )
	{
		var ok = _shop.TryBuyBoat( State, id );
		if ( ok )
		{
			_progression.OnItemBought( State, id );
			CollectChildEvents();
		}
		return ok;
	}

	public bool HireBoat( string id )
	{
		var ok = _shop.TryHireBoat( State, id );
		if ( ok )
		{
			_progression.OnHired( State, id );
			CollectChildEvents();
		}
		return ok;
	}

	public int SellAll()
	{
		var gained = _shop.SellAll( State );
		if ( gained > 0 )
		{
			_progression.OnGoldEarned( State, gained );
			CollectChildEvents();
		}
		return gained;
	}

	public void Equip( string itemId )
	{
		_shop.Equip( State, itemId );
	}

	public void TogglePause()
	{
		if ( State.Mode == GameMode.Paused )
			State.Mode = State.OnBoat ? GameMode.Travel : GameMode.Dock;
		else
			State.Mode = GameMode.Paused;
		_events.Add( new ModeChangedEvent { Mode = State.Mode } );
	}

	public SaveGameDto ToSaveDto()
	{
		State.LastSavedUtc = DateTimeOffset.UtcNow;
		State.Seed = _rng.State;
		return new SaveGameDto
		{
			SchemaVersion = SaveSchema.CurrentVersion,
			State = State,
			Checksum = $"v{SaveSchema.CurrentVersion}:{State.Gold}:{State.TotalCatches}:{State.PlayedSeconds:F0}"
		};
	}

	public static GameSession FromSave( GameContent content, SaveGameDto dto )
	{
		var state = SaveMigrator.Migrate( dto );
		return new GameSession( content, state );
	}

	private void UpdateZoneFromDistance()
	{
		ZoneDef best = null;
		foreach ( var zone in _content.Zones.OrderBy( z => z.MinDistanceM ) )
		{
			if ( !State.UnlockedZoneIds.Contains( zone.Id ) ) continue;
			if ( State.BoatDistanceM >= zone.MinDistanceM )
				best = zone;
		}
		if ( best != null )
			State.CurrentZoneId = best.Id;
	}

	private void CollectChildEvents()
	{
		foreach ( var e in _fishing.DrainEvents() )
		{
			_events.Add( e );
			if ( e is FishCaughtEvent catchEvt )
			{
				// Progression applied on reveal close to keep UI timing nice; still track here for listeners.
			}
		}
		foreach ( var e in _shop.DrainEvents() ) _events.Add( e );
		foreach ( var e in _progression.DrainEvents() ) _events.Add( e );
		foreach ( var e in _hired.DrainEvents() )
		{
			_events.Add( e );
			if ( e is HiredBoatReturnedEvent h )
				_progression.OnGoldEarned( State, h.GoldGained );
		}
		foreach ( var e in _progression.DrainEvents() ) _events.Add( e );
	}
}
