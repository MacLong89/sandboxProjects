namespace Offshore;

/// <summary>Thin bootstrap + orchestration. Gameplay lives in focused services/controllers.</summary>
public sealed class OffshoreGameController : Component
{
	public static OffshoreGameController Instance { get; private set; }

	public SaveData Save { get; private set; }
	public GameStateService State { get; } = new();
	public InventoryService Inventory { get; } = new();
	public TimeOfDayService Clock { get; } = new();
	public WeatherService Weather { get; } = new();
	public ObjectiveService Objectives { get; } = new();
	public AudioService Audio { get; } = new();
	public BoatController Boat { get; } = new();
	public PlayerController Player { get; } = new();
	public FishingController Fishing { get; } = new();
	public HotspotService Hotspots { get; } = new();
	public WorldEventService Events { get; } = new();
	public ShopService Shop { get; private set; }

	public WorldPresenter World { get; private set; }
	public string Notice { get; private set; } = "";
	public float NoticeAge { get; private set; } = 99f;
	public string RangeWarning { get; private set; }
	public InteractPrompt Prompt { get; private set; }
	public CaughtFish PendingCatch { get; private set; }
	public bool ReplaceMode { get; set; }
	public int ReplaceIndex { get; set; }
	public float DisplayCoins { get; private set; }
	public string DistanceBand => BandFor( Boat.OffshoreDistance );
	public float WaterDepth => DepthAt( Boat.OffshoreDistance );
	public bool HudBuilt { get; private set; }
	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public bool TutorialTipsHidden => Save?.HideTutorialTips ?? false;

	CameraComponent _camera;
	bool _worldReady;
	float _boardTimer;
	float _towTimer;
	int _tutorialTipDismissedGoal = -1;

	protected override void OnAwake()
	{
		Instance = this;
		Catalog.Reload();
		Save = SaveService.LoadOrNew( out var warn );
		Inventory.Bind( Save );
		Clock.Load( Save );
		Objectives.Load( Save );
		Audio.Load( Save );
		Shop = new ShopService( Inventory, Autosave, ShowNotice, NotifyObjective );
		DisplayCoins = Save.Coins;
		if ( !string.IsNullOrEmpty( warn ) )
			ShowNotice( warn );
		Boat.ResetToDock( Inventory.Boat );
		Boat.Fuel = Inventory.Boat?.FuelCapacity ?? 200f;
		Log.Info( $"[OFFSHORE] Boat range ready: {Inventory.Boat?.Name} MaxRange={Inventory.Boat?.MaxRange} FuelCap={Inventory.Boat?.FuelCapacity}" );
	}

	protected override void OnStart()
	{
		_camera = Components.Get<CameraComponent>() ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		ConfigureCamera();
		EnsureHud();
		EnsureWorld();
		Mouse.Visibility = MouseVisibility.Visible;
		Audio.SetAmbient( "waves_loop" );
		State.Force( GamePhase.MainMenu );
		Log.Info( $"[OFFSHORE] Started. cam={_camera.IsValid()} hud={HudBuilt} phase={State.Phase}" );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
		Audio.StopAll();
		World?.Clear();
	}

	protected override void OnUpdate()
	{
		NoticeAge += Time.Delta;
		DisplayCoins = DisplayCoins.LerpTo( Save.Coins, Time.Delta * 6f );
		EnsureHud();
		ConfigureCamera();
		Mouse.Visibility = MouseVisibility.Visible;

		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			ToggleTutorialTipsHidden();

		RefreshTutorialTips();

		if ( State.Phase == GamePhase.MainMenu )
		{
			HandleMenuInput();
			// Keep a living dock backdrop behind the menu so play never opens on black.
			UpdateWorld();
			return;
		}

		if ( State.Phase == GamePhase.EmergencyTow )
		{
			_towTimer -= Time.Delta;
			if ( _towTimer <= 0f )
				FinishTow();
			return;
		}

		if ( State.Phase == GamePhase.Boarding )
		{
			_boardTimer -= Time.Delta;
			if ( _boardTimer <= 0f )
			{
				Player.OnBoat = true;
				Boat.Board( Inventory.Boat );
				State.Force( GamePhase.Sailing );
				NotifyObjective( "board_boat" );
				ShowNotice( "Boarded. A/D to sail (right leaves dock, left returns). E at dock to hop off." );
			}
			UpdateWorld();
			return;
		}

		HandleGlobalInput();

		if ( State.Phase is GamePhase.Pause or GamePhase.Settings or GamePhase.Shop
			or GamePhase.Selling or GamePhase.FishLog or GamePhase.Objectives or GamePhase.CatchResult )
		{
			Clock.Paused = State.Phase is GamePhase.Pause or GamePhase.Settings;
			UpdateWorld();
			return;
		}

		Clock.Paused = false;
		Clock.Tick( Time.Delta );
		Weather.Tick( Time.Delta, Boat.OffshoreDistance, Clock.Phase );

		if ( State.Phase == GamePhase.Dock )
			TickDock();
		else if ( State.Phase == GamePhase.Sailing )
			TickSailing();
		else if ( State.IsFishing )
			TickFishing();

		Hotspots.Tick( Time.Delta, FocusX(), Boat.OffshoreDistance, Weather, Clock.Phase );
		Events.Tick( Time.Delta, FocusX(), Boat.OffshoreDistance );
		TryCollectEvents();
		UpdatePrompt();
		UpdateWorld();
		Audio.SetEngine( Boat.Aboard && Math.Abs( Boat.Velocity ) > 4f, Math.Abs( Boat.Velocity ) / Math.Max( 1f, Inventory.Boat?.TopSpeed ?? 1f ) );
	}

	/// <summary>Mario camera focus — always the subject world X so they stay screen-centered.</summary>
	float FocusX() => Boat.Aboard ? Boat.WorldX : Player.DockX;

	void TickDock()
	{
		var move = 0f;
		if ( Down( "Left" ) || Key( "A" ) ) move -= 1f;
		if ( Down( "Right" ) || Key( "D" ) ) move += 1f;
		Player.TickDock( Time.Delta, move );

		var nearBoat = Inventory.HasBoat && Player.NearBoatBerth;

		if ( Pressed( "Interact" ) || KeyPressed( "E" ) )
		{
			if ( Player.NearShop )
				OpenShop();
			else if ( nearBoat )
				BeginBoard();
			else if ( Inventory.StorageUsed > 0 && Player.DockX > 100 && Player.DockX < 200 )
				OpenSell();
		}

		if ( (Pressed( "Secondary" ) || KeyPressed( "F" )) && nearBoat )
			Refuel();

		// Same cast control as on the boat — LMB from the pier.
		if ( !Player.NearShop && (Pressed( "Cast" ) || Pressed( "Attack1" ) || MousePressed()) )
			BeginCast( fromDock: true );
	}

	void TickSailing()
	{
		var throttle = 0f;
		if ( Down( "Left" ) || Key( "A" ) ) throttle -= 1f;
		if ( Down( "Right" ) || Key( "D" ) ) throttle += 1f;

		Boat.Tick( Time.Delta, throttle, Inventory.Boat, Weather, out var limit );
		Player.Anim = Math.Abs( Boat.Velocity ) > 6f ? "idle" : "idle";
		RangeWarning = limit;
		if ( !string.IsNullOrEmpty( limit ) && NoticeAge > 2f )
			ShowNotice( limit );

		if ( Boat.OffshoreDistance > 40f )
			NotifyObjective( "travel_offshore" );

		if ( Boat.Fuel <= 0f )
		{
			StartTow( "Out of fuel! Emergency tow returning you to dock." );
			return;
		}

		// At the berth facing/docked — E to hop off onto the pier.
		if ( (Pressed( "Interact" ) || KeyPressed( "E" )) && Boat.AtDock )
		{
			DockBoat();
			return;
		}

		if ( Pressed( "Cast" ) || Pressed( "Attack1" ) || MousePressed() )
			BeginCast( fromDock: false );
	}

	void TickFishing()
	{
		if ( State.Phase == GamePhase.Casting )
		{
			Player.Anim = "cast";
			if ( Fishing.Swinging )
			{
				if ( Fishing.TickCastSwing( Time.Delta ) )
					State.Force( GamePhase.WaitingBite );
			}
			else if ( Down( "Cast" ) || Down( "Attack1" ) || MouseDown() )
			{
				Fishing.UpdateCharge( Time.Delta );
			}
			else if ( Fishing.Charging )
			{
				Fishing.ReleaseCast( Inventory, Inventory.Boat, Weather, !Boat.Aboard );
				Audio.PlayEffect( "cast" );
				Audio.PlayEffect( "splash" );
				NotifyObjective( "cast_line" );
				if ( Fishing.Status == "No bait equipped." )
				{
					ShowNotice( Fishing.Status );
					State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
					Fishing.Cancel();
					return;
				}
				// Stay in Casting briefly so the rod swings forward, then WaitingBite.
			}

			if ( Pressed( "Secondary" ) || KeyPressed( "F" ) )
			{
				Fishing.Cancel();
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
			}
			return;
		}

		if ( State.Phase == GamePhase.WaitingBite )
		{
			Player.Anim = "fish";
			if ( Pressed( "Secondary" ) || KeyPressed( "F" ) )
			{
				Fishing.Cancel();
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
				ShowNotice( "Line retrieved." );
				return;
			}

			var depthDelta = 0f;
			if ( Key( "W" ) ) depthDelta -= 25f;
			if ( Key( "S" ) ) depthDelta += 25f;
			Fishing.AdjustDepth( depthDelta * Time.Delta );

			var ended = Fishing.TickWaiting( Time.Delta, BuildEcology(), Inventory, out var msg );
			if ( msg == "bite" )
			{
				Audio.PlayEffect( "bite" );
				State.Force( GamePhase.Hooking );
			}
			else if ( ended )
			{
				ShowNotice( msg ?? "Bait lost." );
				Audio.PlayEffect( "escape" );
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
				Fishing.Cancel();
			}
			return;
		}

		if ( State.Phase == GamePhase.Hooking )
		{
			Player.Anim = "hook";
			if ( Fishing.TickHookWindow( Time.Delta ) )
			{
				Inventory.ConsumeBait();
				ShowNotice( "Too slow — the fish left." );
				Audio.PlayEffect( "escape" );
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
				Fishing.Cancel();
				return;
			}

			if ( Pressed( "Interact" ) || KeyPressed( "E" ) || Pressed( "Cast" ) || Pressed( "Attack1" ) || MousePressed() )
			{
				if ( Fishing.TrySetHook( Inventory, out var fail ) )
				{
					Audio.PlayEffect( "hook" );
					State.Force( GamePhase.Reeling );
				}
				else
				{
					ShowNotice( fail );
					Audio.PlayEffect( "escape" );
					State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
					Fishing.Cancel();
				}
			}
			return;
		}

		if ( State.Phase == GamePhase.Reeling )
		{
			Player.Anim = "reel";
			// Stardew: same action button raises the green bar (hold) / gravity (release).
			var reeling = Down( "Cast" ) || Down( "Attack1" ) || MouseDown()
				|| Down( "Reel" ) || Key( "R" ) || Input.Down( "Attack2" )
				|| Down( "Interact" ) || Key( "E" ) || Key( "Space" );
			var result = Fishing.TickReel( Time.Delta, reeling, Inventory, Inventory.Boat, Weather );
			if ( result == FishingController.FightResult.Caught )
			{
				Audio.PlayEffect( "catch" );
				PendingCatch = Fishing.LastCatch;
				Inventory.RecordCatch( PendingCatch );
				NotifyObjective( "catch_fish" );
				State.Force( GamePhase.CatchResult );
			}
			else if ( result == FishingController.FightResult.Escaped )
			{
				ShowNotice( "The fish escaped." );
				Audio.PlayEffect( "escape" );
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
				Fishing.Cancel();
			}
			else if ( result == FishingController.FightResult.LineBroke )
			{
				ShowNotice( "Line snapped!" );
				Audio.PlayEffect( "line_break" );
				State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
				Fishing.Cancel();
			}
		}
	}

	EcologyContext BuildEcology()
	{
		var (hs, bias) = Hotspots.Sample( Boat.Aboard ? Boat.WorldX + Fishing.LureDistance * 0.3f : Player.DockX );
		return new EcologyContext
		{
			Distance = Boat.Aboard ? Boat.OffshoreDistance + Fishing.LureDistance * 0.2f : 10f,
			WaterDepth = WaterDepth,
			LureDepth = Fishing.LureDepth,
			BaitId = Save.EquippedBait,
			Time = Clock.Phase,
			Weather = Weather.Current,
			Temperature = Weather.Temperature,
			Clarity = Weather.WaterClarity,
			Seabed = WaterDepth < 35 ? "sand" : WaterDepth < 90 ? "rock" : "deep",
			Structure = Boat.OffshoreDistance < 40 ? "dock" : WaterDepth > 70 ? "wreck" : "open",
			HotspotStrength = hs,
			HotspotSpeciesBias = bias,
			LineVisibility = Inventory.Line.Visibility,
			Noise = Player.OnBoat ? Math.Abs( Boat.Velocity ) / 80f : 0.1f,
			BoatSpeed = Math.Abs( Boat.Velocity ),
			Moonlight = Clock.StarVisibility,
			Wind = Weather.Wind,
			Rain = Weather.Rain,
			Hook = Inventory.Hook,
			CastAccuracy = Inventory.Rod.CastAccuracy * (Inventory.Boat?.CastAccuracy ?? 0.85f)
		};
	}

	void BeginCast( bool fromDock )
	{
		if ( Inventory.BaitCount <= 0 )
		{
			ShowNotice( "No bait left. Visit the shop." );
			return;
		}
		if ( !State.TryEnter( GamePhase.Casting ) )
			State.Force( GamePhase.Casting );
		Boat.CutThrottle();
		Fishing.BeginCharge();
		Player.Anim = "cast";
		Audio.PlayUi( "ui_click" );
	}

	void BeginBoard()
	{
		if ( !Inventory.HasBoat )
		{
			ShowNotice( "Buy a boat at the shop first." );
			return;
		}
		if ( !State.TryEnter( GamePhase.Boarding ) )
			State.Force( GamePhase.Boarding );
		_boardTimer = 0.2f;
		Player.Anim = "walk";
		ShowNotice( "Entering boat…" );
		Audio.PlayEffect( "wood_creak" );
	}

	void DockBoat()
	{
		Boat.Disembark();
		Player.OnBoat = false;
		Player.DockX = BoatController.DockX - 20f;
		State.Force( GamePhase.Dock );
		NotifyObjective( "return_dock" );
		Boat.Fuel = Inventory.Boat?.FuelCapacity ?? Boat.Fuel;
		Autosave();
		ShowNotice( "Docked. Fuel topped off." );
		Audio.PlayEffect( "wood_creak" );
	}

	void Refuel()
	{
		if ( Inventory.Boat is null ) return;
		Boat.Fuel = Inventory.Boat.FuelCapacity;
		Autosave();
		ShowNotice( "Fuel refilled." );
	}

	void OpenShop()
	{
		if ( Boat.Aboard ) return;
		Shop.Tab = ShopTab.Bait;
		Shop.SelectedId = "worm";
		State.Force( GamePhase.Shop );
		NotifyObjective( "enter_shop" );
		Audio.PlayUi( "ui_click" );
	}

	public void OpenSell()
	{
		if ( Boat.Aboard ) return;
		State.Force( GamePhase.Selling );
		Audio.PlayUi( "ui_click" );
	}

	public void KeepCatch()
	{
		if ( PendingCatch is null ) return;
		if ( Inventory.TryAddCatch( PendingCatch, out var err ) )
		{
			ShowNotice( $"Kept {PendingCatch.SpeciesName}." );
			Autosave();
		}
		else
		{
			ReplaceMode = true;
			ShowNotice( err );
			return;
		}
		PendingCatch = null;
		ReplaceMode = false;
		State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
		Fishing.Cancel();
		// First kept fish during the haul objective — offer the sell-at-shop tip.
		_tutorialTipDismissedGoal = -1;
		RefreshTutorialTips();
	}

	public void ReplaceAndKeep()
	{
		if ( PendingCatch is null ) return;
		Inventory.ReplaceCatch( ReplaceIndex, PendingCatch );
		ShowNotice( $"Replaced catch with {PendingCatch.SpeciesName}." );
		PendingCatch = null;
		ReplaceMode = false;
		Autosave();
		State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
		Fishing.Cancel();
		_tutorialTipDismissedGoal = -1;
		RefreshTutorialTips();
	}

	public void ReleaseCatch()
	{
		ShowNotice( PendingCatch is null ? "Released." : $"Released {PendingCatch.SpeciesName}." );
		PendingCatch = null;
		ReplaceMode = false;
		State.Force( Boat.Aboard ? GamePhase.Sailing : GamePhase.Dock );
		Fishing.Cancel();
	}

	public void SellAll()
	{
		var n = Inventory.StorageUsed;
		var gained = Inventory.SellAll();
		if ( n > 0 )
			NotifyObjective( "sell_fish" );
		Audio.PlayEffect( "sell" );
		Autosave();
		ShowNotice( $"Sold catch for {gained} coins." );
		State.Force( GamePhase.Dock );
	}

	public void SellSelected( List<int> indices )
	{
		var gained = Inventory.SellSelected( indices );
		if ( gained > 0 )
			NotifyObjective( "sell_fish" );
		Audio.PlayEffect( "sell" );
		Autosave();
		ShowNotice( $"Sold for {gained} coins." );
	}

	void StartTow( string reason )
	{
		State.Force( GamePhase.EmergencyTow );
		_towTimer = 2.2f;
		ShowNotice( reason );
		var penalty = (int)(Inventory.Boat?.TowPenalty ?? 40);
		Save.Coins = Math.Max( 0, Save.Coins - penalty );
		if ( Save.Storage.Count > 0 && Random.Shared.NextDouble() < 0.35 )
			Save.Storage.RemoveAt( Save.Storage.Count - 1 );
		Audio.PlayEffect( "thunder" );
	}

	void FinishTow()
	{
		Boat.ResetToDock( Inventory.Boat );
		Boat.Fuel = (Inventory.Boat?.FuelCapacity ?? 200f) * 0.5f;
		Player.OnBoat = false;
		Player.DockX = BoatController.DockX - 20f;
		Fishing.Cancel();
		Autosave();
		State.Force( GamePhase.Dock );
		ShowNotice( "Towed back to dock." );
	}

	void TryCollectEvents()
	{
		if ( !Boat.Aboard ) return;
		var ev = Events.TryCollect( Boat.WorldX );
		if ( ev is null ) return;
		if ( ev.CoinReward > 0 )
		{
			Save.Coins += ev.CoinReward;
			ShowNotice( $"+{ev.CoinReward} coins from {ev.Label}" );
		}
		if ( !string.IsNullOrEmpty( ev.BaitReward ) )
		{
			Save.BaitCounts[ev.BaitReward] = Inventory.GetBait( ev.BaitReward ) + ev.BaitAmount;
			ShowNotice( $"Found {ev.BaitAmount}x bait." );
		}
		Audio.PlayEffect( "purchase" );
		Autosave();
	}

	void HandleGlobalInput()
	{
		if ( Pressed( "Pause" ) || KeyPressed( "ESCAPE" ) )
		{
			if ( State.IsMenu && State.Phase is not GamePhase.Pause and not GamePhase.MainMenu and not GamePhase.Settings )
			{
				State.CloseOverlay();
				Audio.PlayUi( "ui_click" );
				return;
			}
			if ( State.Phase == GamePhase.Pause )
				State.CloseOverlay();
			else if ( State.Phase != GamePhase.MainMenu && State.Phase != GamePhase.CatchResult )
				State.TryEnter( GamePhase.Pause );
			Audio.PlayUi( "ui_click" );
		}

		if ( Pressed( "FishLog" ) || KeyPressed( "TAB" ) )
		{
			if ( State.Phase is GamePhase.Shop or GamePhase.FishLog )
				State.CloseOverlay();
			else if ( !State.IsFishing && State.Phase is GamePhase.Dock or GamePhase.Sailing )
				State.TryEnter( GamePhase.FishLog );
			Audio.PlayUi( "ui_click" );
		}

		if ( Pressed( "Objectives" ) || KeyPressed( "M" ) )
		{
			if ( State.Phase == GamePhase.Objectives ) State.CloseOverlay();
			else if ( !State.IsFishing && State.Phase is GamePhase.Dock or GamePhase.Sailing or GamePhase.Shop )
				State.Force( GamePhase.Objectives );
			Audio.PlayUi( "ui_click" );
		}

		TickHotbar();
	}

	void TickHotbar()
	{
		if ( State.Phase is GamePhase.MainMenu or GamePhase.Pause or GamePhase.Settings
			or GamePhase.Shop or GamePhase.Selling or GamePhase.FishLog or GamePhase.Objectives
			or GamePhase.CatchResult or GamePhase.EmergencyTow or GamePhase.Boarding )
			return;

		for ( var i = 0; i < 8; i++ )
		{
			if ( !KeyPressed( (i + 1).ToString() ) )
				continue;
			var msg = Inventory.TryEquipHotbar( i );
			if ( msg is not null )
			{
				ShowNotice( msg );
				Audio.PlayUi( "ui_click" );
				NotifyObjective( "equip_bait" );
				Autosave();
			}
			else
			{
				// Already that item — still play a light click so selection feedback is clear.
				Audio.PlayUi( "ui_hover" );
			}
			break;
		}
	}

	static bool Down( string action )
	{
		try { return Input.Down( action ); }
		catch { return false; }
	}

	static bool Pressed( string action )
	{
		try { return Input.Pressed( action ); }
		catch { return false; }
	}

	static bool Key( string key ) => Input.Keyboard.Down( key );
	static bool KeyPressed( string key ) => Input.Keyboard.Pressed( key );
	static bool MouseDown() => Input.Down( "Attack1" );
	static bool MousePressed() => Input.Pressed( "Attack1" );

	public void ReturnToDockFromPause()
	{
		if ( Boat.Aboard )
		{
			Boat.Disembark();
			Player.OnBoat = false;
			Player.DockX = BoatController.DockX - 20f;
			Fishing.Cancel();
			Boat.Fuel = Inventory.Boat?.FuelCapacity ?? Boat.Fuel;
		}
		State.Force( GamePhase.Dock );
		Autosave();
		ShowNotice( "Returned to dock." );
	}

	void HandleMenuInput() { }

	public void NewGame()
	{
		SaveService.Delete();
		Save = SaveData.NewGame();
		Inventory.Bind( Save );
		Clock.Load( Save );
		Objectives.Load( Save );
		Audio.Load( Save );
		DisplayCoins = Save.Coins;
		Boat.ResetToDock( Inventory.Boat );
		Boat.Fuel = Inventory.Boat?.FuelCapacity ?? 200f;
		Player.OnBoat = false;
		Player.DockX = 80f;
		Fishing.Cancel();
		EnsureWorld();
		State.Force( GamePhase.Dock );
		Audio.PlayUi( "ui_click" );
		RefreshTutorialTips();
	}

	public void ContinueGame()
	{
		EnsureWorld();
		Boat.ResetToDock( Inventory.Boat );
		Boat.Fuel = Inventory.Boat?.FuelCapacity ?? 200f;
		Player.OnBoat = false;
		State.Force( GamePhase.Dock );
		Audio.PlayUi( "ui_click" );
		RefreshTutorialTips();
	}

	public void RefreshTutorialTips()
	{
		if ( Save is null || Save.HideTutorialTips
			|| State.Phase is GamePhase.MainMenu or GamePhase.Shop or GamePhase.Selling
			or GamePhase.FishLog or GamePhase.Objectives or GamePhase.Pause or GamePhase.Settings
			or GamePhase.CatchResult or GamePhase.Casting or GamePhase.Reeling or GamePhase.WaitingBite
			or GamePhase.Hooking or GamePhase.EmergencyTow or GamePhase.Boarding )
		{
			ActiveTutorialTip = null;
			return;
		}

		var tip = TutorialTips.PickNext( Save, _tutorialTipDismissedGoal );
		if ( tip is null )
		{
			ActiveTutorialTip = null;
			return;
		}

		var sameText = ActiveTutorialTip is not null
			&& ActiveTutorialTip.Id == tip.Id
			&& ActiveTutorialTip.Title == tip.Title
			&& ActiveTutorialTip.Body == tip.Body;
		if ( sameText )
			return;

		ActiveTutorialTip = tip;
	}

	public void DismissTutorialTip( bool hideAll = false )
	{
		if ( hideAll )
		{
			TutorialTips.HideAllTips( Save );
			ActiveTutorialTip = null;
			_tutorialTipDismissedGoal = -1;
			ShowNotice( "Tips hidden — press H to show again" );
		}
		else
		{
			// Mark this tip shown, then immediately surface any follow-up (cast → reel, sell → dinghy).
			var followUpId = ActiveTutorialTip?.FollowUpId;
			TutorialTips.MarkShown( Save, ActiveTutorialTip );
			ActiveTutorialTip = null;

			TutorialTipDef followUp = null;
			if ( !string.IsNullOrEmpty( followUpId ) )
			{
				var chained = TutorialTips.TipById( followUpId );
				if ( chained is not null && !TutorialTips.IsShown( Save, chained.Id ) )
					followUp = chained;
			}

			followUp ??= TutorialTips.PickNext( Save, dismissedGoalIndex: -1 );
			if ( followUp is not null )
			{
				ActiveTutorialTip = followUp;
			}
			else
			{
				// Soft dismiss — stay hidden for this objective until it completes.
				_tutorialTipDismissedGoal = TutorialTips.NormalizedGoalIndex( Save );
			}
		}

		Autosave();
	}

	/// <summary>Objective completed — force the next goal's coach card.</summary>
	public void AdvanceTutorialTipAfterObjective( int completedGoalIndex )
	{
		if ( Save is null )
			return;

		TutorialTips.MarkShown( Save, completedGoalIndex );
		ActiveTutorialTip = null;
		_tutorialTipDismissedGoal = -1;
		RefreshTutorialTips();
	}

	public void ToggleTutorialTipsHidden()
	{
		if ( Save is null || State.Phase == GamePhase.MainMenu )
			return;

		Save.HideTutorialTips = !Save.HideTutorialTips;
		if ( Save.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			_tutorialTipDismissedGoal = -1;
			ShowNotice( "Tips hidden — press H to show again" );
		}
		else
		{
			_tutorialTipDismissedGoal = -1;
			ShowNotice( "Tips enabled" );
		}

		Autosave();
		RefreshTutorialTips();
	}

	public void QuitToMenu()
	{
		Clock.SyncTo( Save );
		Objectives.SyncTo( Save );
		Audio.SyncTo( Save );
		Autosave();
		Fishing.Cancel();
		Boat.Disembark();
		Player.OnBoat = false;
		State.Force( GamePhase.MainMenu );
	}

	public void Autosave()
	{
		Clock.SyncTo( Save );
		Objectives.SyncTo( Save );
		Audio.SyncTo( Save );
		SaveService.Write( Save );
	}

	bool NotifyObjective( string key )
	{
		var done = Objectives.Notify( key );
		Objectives.SyncTo( Save );
		if ( done )
		{
			var completedIndex = Objectives.Index - 1;
			AdvanceTutorialTipAfterObjective( completedIndex );
			ShowNotice( $"Objective complete! {Objectives.HudText}" );
			Autosave();
		}
		return done;
	}

	void UpdatePrompt()
	{
		var nearBoat = Inventory.HasBoat && Player.NearBoatBerth;

		if ( State.Phase == GamePhase.Dock )
		{
			if ( Player.NearShop )
				Prompt = InteractPrompt.EnterShop;
			else if ( nearBoat )
				Prompt = InteractPrompt.BoardBoat;
			else if ( Inventory.StorageUsed > 0 && Player.DockX > 100 && Player.DockX < 200 )
				Prompt = InteractPrompt.SellCatch;
			else
				Prompt = InteractPrompt.Cast;
			return;
		}

		Prompt = Player.Prompt( State.Phase, Inventory.StorageUsed > 0, nearBoat, Player.NearShop, Player.DockX > 100 );
		if ( State.Phase == GamePhase.Sailing && Boat.AtDock )
			Prompt = InteractPrompt.DockBoat;
		if ( State.Phase == GamePhase.Sailing && !Boat.AtDock )
			Prompt = InteractPrompt.Cast;
		if ( State.Phase == GamePhase.WaitingBite )
			Prompt = InteractPrompt.None;
		if ( State.Phase == GamePhase.Hooking )
			Prompt = InteractPrompt.Hook;
		if ( State.Phase == GamePhase.Reeling )
			Prompt = InteractPrompt.Reel;
		if ( State.Phase == GamePhase.Boarding )
			Prompt = InteractPrompt.None;
		if ( State.Phase == GamePhase.Shop )
			Prompt = InteractPrompt.None;
	}

	void UpdateWorld()
	{
		EnsureWorld();
		World?.Update( Time.Delta, FocusX(), Player.DockX, Boat.Aboard, Boat, Inventory.Boat, Player, Clock, Weather, WaterDepth, Fishing, State.Phase, Hotspots, Events );
		if ( _camera is not null )
			_camera.BackgroundColor = Color.Lerp( Clock.SkyTop, Clock.SkyHorizon, 0.4f ) * (0.7f + Weather.Visibility * 0.3f);
	}

	void EnsureWorld()
	{
		if ( _worldReady && World is not null ) return;
		World = new WorldPresenter( Scene );
		World.Build();
		_worldReady = true;
	}

	void ConfigureCamera()
	{
		if ( _camera is null || !_camera.IsValid() )
		{
			_camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if ( _camera is null || !_camera.IsValid() )
			{
				var camGo = new GameObject( true, "OffshoreCamera" );
				_camera = camGo.Components.Create<CameraComponent>();
			}
		}

		_camera.Orthographic = true;
		_camera.OrthographicHeight = 560;
		_camera.IsMainCamera = true;
		_camera.ZNear = 1f;
		_camera.ZFar = 10000f;
		_camera.WorldPosition = new Vector3( 0, -1000, WorldPresenter.CameraZ );
		// Match plunge scene quaternion: look along +Y toward the XY billboard plane.
		_camera.WorldRotation = new Rotation( 0, 0, 0.7071068f, 0.7071068f );
		_camera.BackgroundColor = State.Phase == GamePhase.MainMenu
			? new Color( 0.95f, 0.45f, 0.28f )
			: Color.Lerp( Clock.SkyTop, Clock.SkyHorizon, 0.4f ) * (0.7f + Weather.Visibility * 0.3f );

		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
		{
			if ( !screen.IsValid() ) continue;
			screen.Enabled = true;
			screen.Opacity = 1f;
			screen.ZIndex = Math.Max( screen.ZIndex, 100 );
			if ( screen.TargetCamera != _camera )
				screen.TargetCamera = _camera;
		}
	}

	void EnsureHud()
	{
		try
		{
			var existing = Scene.GetAllComponents<OffshoreHud>().FirstOrDefault();
			if ( existing is not null && existing.IsValid() )
			{
				var host = existing.GameObject;
				var screen = host.Components.Get<ScreenPanel>() ?? host.Components.Create<ScreenPanel>();
				screen.AutoScreenScale = true;
				screen.Scale = 1f;
				screen.Opacity = 1f;
				screen.ZIndex = 100;
				screen.Enabled = true;
				if ( _camera.IsValid() )
					screen.TargetCamera = _camera;
				HudBuilt = true;
				return;
			}

			if ( HudBuilt ) return;

			var hudGo = new GameObject( true, "OffshoreHUD" );
			var panel = hudGo.Components.Create<ScreenPanel>();
			panel.AutoScreenScale = true;
			panel.Scale = 1f;
			panel.Opacity = 1f;
			panel.ZIndex = 100;
			panel.Enabled = true;
			if ( _camera.IsValid() )
				panel.TargetCamera = _camera;
			hudGo.Components.Create<OffshoreHud>();
			HudBuilt = true;
			Log.Info( $"[OFFSHORE] HUD created. camBound={panel.TargetCamera.IsValid()}" );
		}
		catch ( Exception e )
		{
			Log.Error( $"[OFFSHORE] HUD create failed: {e}" );
		}
	}

	public void ShowNotice( string msg )
	{
		Notice = msg;
		NoticeAge = 0f;
	}

	public static float DepthAt( float distance )
	{
		// Continuous slope — no biome walls
		var d = Math.Max( 0f, distance );
		return 12f + d * 0.085f + MathF.Pow( d / 200f, 1.35f ) * 18f;
	}

	public static string BandFor( float distance ) => distance switch
	{
		< 60 => "Near Shore",
		< 200 => "Offshore",
		< 450 => "Far Offshore",
		< 800 => "Deep Water",
		_ => "Abyssal Reach"
	};

	public string PromptText => Prompt switch
	{
		InteractPrompt.EnterShop => "E to Shop",
		InteractPrompt.BoardBoat => "E to Enter Boat",
		InteractPrompt.DockBoat => "E to Exit Boat",
		InteractPrompt.SellCatch => "E Sell Catch",
		InteractPrompt.Refuel => "F Refuel",
		InteractPrompt.Cast => "LMB Cast",
		InteractPrompt.Hook => "! Hook!",
		InteractPrompt.Reel => "Hold LMB Reel",
		InteractPrompt.StopCast => "F Retrieve Line",
		_ => ""
	};
}
