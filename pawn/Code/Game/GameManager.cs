namespace PawnShop;

public enum GameState
{
	MainMenu,
	MorningPrep,
	ShopOpen,
	ShopClosing,
	DailySummary,
	Bankruptcy,
}

/// <summary>One HUD toast notification.</summary>
public sealed class Toast
{
	public string Text;
	public string Icon;
	public RealTimeSince Age;
}

/// <summary>
/// Central hub: owns the save, all plain-C# systems, the game state machine, and the
/// day cycle. Everything UI-facing routes through here.
/// </summary>
public sealed class GameManager : Component
{
	public static GameManager Instance { get; private set; }

	public SaveData Save { get; private set; }
	public EconomySystem Economy { get; private set; }
	public InventorySystem Inventory { get; private set; }
	public PawnSystem Pawns { get; private set; }
	public WorkshopSystem Workshop { get; private set; }
	public ReputationSystem Reputation { get; private set; }
	public RelationshipSystem Relationships { get; private set; }
	public EventSystem Events { get; private set; }
	public NegotiationSystem Negotiation { get; private set; }
	public TutorialSystem Tutorial { get; private set; }
	public GoalSystem Goals { get; private set; }
	public CrateSystem Crate { get; private set; }
	public CollectionSystem Collection { get; private set; }
	public ChoreSystem Chores { get; private set; }

	public ShopBuilder Shop { get; private set; }
	public CustomerManager Customers { get; private set; }

	public GameState State { get; private set; } = GameState.MainMenu;

	// --- Clock ---
	/// <summary>In-game minutes since midnight.</summary>
	public float ClockMinutes { get; private set; } = GameConstants.OpenClockStart - 30f;
	public string ClockLabel => GameConstants.FormatClock( ClockMinutes );
	public bool ShopIsOpen => State is GameState.ShopOpen or GameState.ShopClosing;

	// --- UI flags ---
	public bool ManagementOpen { get; private set; }
	public int ManagementTab { get; private set; }
	public int InspectionItemId { get; private set; } = -1;
	public InspectTool SelectedTool { get; private set; } = InspectTool.Eyes;
	public string InspectionMessage { get; private set; } = "";
	public bool PauseMenuOpen { get; private set; }
	public int SelectedInventoryId { get; private set; } = -1;

	/// <summary>Item the player is physically carrying between backroom and shelves.</summary>
	public int CarriedItemId { get; private set; } = -1;
	public ItemInstance CarriedItem => CarriedItemId >= 0 ? Inventory.Get( CarriedItemId ) : null;

	public ItemInstance InspectionItem => InspectionItemId >= 0
		? Inventory.Get( InspectionItemId ) ?? (Negotiation.Item?.Id == InspectionItemId ? Negotiation.Item : null)
		: null;

	public bool IsUiBlocking =>
		State is GameState.MainMenu or GameState.DailySummary or GameState.Bankruptcy
		|| ManagementOpen || InspectionItemId >= 0 || Negotiation.Active || PauseMenuOpen;

	// --- Summary data for the end-of-day screen ---
	public DayLedger SummaryLedger { get; private set; }
	public List<string> SummaryNotes { get; private set; } = new();

	// --- Toasts ---
	public readonly List<Toast> Toasts = new();

	private TimeUntil _nextAutosave;
	private MusicPlayer _ambience;
	private float _closingGrace;

	protected override void OnAwake()
	{
		Instance = this;
		LoadInto( SaveManager.Load() );
	}

	private void LoadInto( SaveData save )
	{
		Save = save;
		Economy = new EconomySystem( save );
		Inventory = new InventorySystem( save );
		Relationships = new RelationshipSystem( save );
		Pawns = new PawnSystem( save, Inventory, Economy, Relationships );
		Workshop = new WorkshopSystem( save, Economy );
		Reputation = new ReputationSystem( save, Economy );
		Events = new EventSystem( save );
		Negotiation = new NegotiationSystem();
		Tutorial = new TutorialSystem( save );
		Goals = new GoalSystem( save );
		Crate = new CrateSystem( save );
		Collection = new CollectionSystem( save );
		UiState.Bump();
	}

	protected override void OnStart()
	{
		// Build the world.
		var shopGo = new GameObject( true, "Shop" );
		Shop = shopGo.Components.Create<ShopBuilder>();

		var choreGo = new GameObject( true, "Chores" );
		Chores = choreGo.Components.Create<ChoreSystem>();

		var custGo = new GameObject( true, "Customers" );
		Customers = custGo.Components.Create<CustomerManager>();

		var playerGo = new GameObject( true, "Player" );
		playerGo.WorldPosition = ShopLayout.PlayerSpawn;
		playerGo.Components.Create<ShopPlayer>();

		var hudGo = new GameObject( true, "HUD" );
		hudGo.Components.Create<ScreenPanel>();
		hudGo.Components.Create<PawnShop.UI.HudRoot>();

		StartAmbience();

		State = GameState.MainMenu;
		_nextAutosave = GameConstants.AutosaveInterval;
		Log.Info( $"Brass Buck Pawn booted — day {Save.Day}, cash {GameConstants.FormatCash( Save.Cash )}, {Save.Inventory.Count} items." );
		UiState.Bump();
	}

	protected override void OnUpdate()
	{
		TickClock();
		Negotiation.Tick( Time.Delta );
		Chores?.Tick( Time.Delta );
		TickToasts();

		if ( _nextAutosave && State is not (GameState.MainMenu or GameState.Bankruptcy) )
		{
			_nextAutosave = GameConstants.AutosaveInterval;
			SaveManager.Save( Save );
		}
	}

	protected override void OnDestroy()
	{
		_ambience?.Stop();
		_ambience = null;

		if ( Instance == this )
		{
			if ( State is not (GameState.MainMenu or GameState.Bankruptcy) )
				SaveManager.Save( Save );
			Instance = null;
		}
	}

	private void StartAmbience()
	{
		try
		{
			_ambience = MusicPlayer.Play( FileSystem.Mounted, "sounds/ambience.mp3" );
			if ( _ambience is not null )
			{
				_ambience.Repeat = true;
				_ambience.Volume = 0.18f;
			}
		}
		catch { /* ambience is optional */ }
	}

	public bool MusicOn { get; private set; } = true;

	/// <summary>The old shop radio: flick the ambience on and off.</summary>
	public void ToggleMusic()
	{
		MusicOn = !MusicOn;
		if ( _ambience is not null )
			_ambience.Volume = MusicOn ? 0.18f : 0f;
		Toast( MusicOn ? "The radio crackles back to life." : "You switch the radio off.", "radio" );
		Sfx.Play( Sfx.UiClick, 0.4f );
	}

	/// <summary>Open management on a specific tab (workbench / computer interacts).</summary>
	public void OpenManagement( int tab )
	{
		if ( Negotiation.Active || State is GameState.MainMenu or GameState.DailySummary or GameState.Bankruptcy )
			return;
		ManagementOpen = true;
		ManagementTab = tab;
		InspectionItemId = -1;
		Sfx.Play( Sfx.UiClick, 0.4f );
		UiState.Bump();
	}

	private void TickToasts()
	{
		if ( Toasts.RemoveAll( t => t.Age > 5f ) > 0 )
			UiState.Bump();
	}

	public void Toast( string text, string icon = "info" )
	{
		Toasts.Add( new Toast { Text = text, Icon = icon, Age = 0 } );
		if ( Toasts.Count > 4 ) Toasts.RemoveAt( 0 );
		UiState.Bump();
	}

	// ==================================================================== Clock / day cycle

	private void TickClock()
	{
		if ( State is not (GameState.ShopOpen or GameState.ShopClosing) )
			return;

		ClockMinutes += GameConstants.ClockMinutesPerSecond * Time.Delta;

		if ( State == GameState.ShopOpen && ClockMinutes >= GameConstants.OpenClockEnd )
			BeginClosing( auto: true );

		if ( State == GameState.ShopClosing )
		{
			_closingGrace -= Time.Delta;
			// Day truly ends when the last customer leaves (or the grace timer expires).
			if ( (!Negotiation.Active && Customers.ActiveCount == 0) || _closingGrace <= 0f )
				EndDay();
		}
	}

	// --- Main menu actions ---
	public bool HasSave => SaveManager.SaveExists();

	public void NewGame()
	{
		LoadInto( SaveManager.Wipe() );
		Save.HasSeenIntro = true;
		BeginMorning( freshRoll: true );
		SaveManager.Save( Save );
	}

	public void ContinueGame()
	{
		if ( Save.Day <= 1 && Save.TotalDeals == 0 && !Save.HasSeenIntro )
		{
			NewGame();
			return;
		}
		BeginMorning( freshRoll: false );
	}

	public void QuitToMenu()
	{
		SaveManager.Save( Save );
		Negotiation.Abort();
		Customers.ClearAll();
		CloseAllUi();
		State = GameState.MainMenu;
		UiState.Bump();
	}

	private void BeginMorning( bool freshRoll )
	{
		CloseAllUi();
		CarriedItemId = -1;
		Customers.ClearAll();
		ClockMinutes = GameConstants.OpenClockStart - 30f;
		if ( freshRoll )
			Events.RollForDay( Save.Day );
		Goals.RollForDay( Save.Day );
		Crate.RollForDay( Save.Day );
		Chores?.RollMorning();
		State = GameState.MorningPrep;
		Shop.RefreshDisplays();
		Shop.SetDoorSign( open: false );
		UiState.Bump();
	}

	/// <summary>Open for business (door interact or UI button).</summary>
	public void OpenShop()
	{
		if ( State != GameState.MorningPrep ) return;

		State = GameState.ShopOpen;
		ClockMinutes = GameConstants.OpenClockStart;
		Economy.ResetLedger( Save.Day );
		Customers.OnShopOpened();
		Shop.SetDoorSign( open: true );
		Sfx.Play( Sfx.DayStart, 0.7f );
		Toast( $"Day {Save.Day} — open for business!", "storefront" );
		Tutorial.Notify( TutorialTrigger.OpenedShop );
		UiState.Bump();
	}

	/// <summary>Close early or at end of hours. Customers already inside finish up.</summary>
	public void BeginClosing( bool auto )
	{
		if ( State != GameState.ShopOpen ) return;

		State = GameState.ShopClosing;
		_closingGrace = 30f;
		Customers.OnShopClosing();
		Shop.SetDoorSign( open: false );
		Toast( auto ? "Closing time! Finishing up with the last customers." : "Closing early. Last customers are finishing up.", "door_front" );
		UiState.Bump();
	}

	private void EndDay()
	{
		// A negotiation still running at hard cutoff is aborted gracefully.
		Negotiation.Abort();
		Customers.ClearAll();
		CloseAllUi();

		SummaryNotes = new List<string>();

		// Pawn expirations.
		var defaulted = Pawns.ProcessDayEnd();
		foreach ( var name in defaulted )
			SummaryNotes.Add( $"Pawn defaulted — {name} is now store property." );

		// Police / stolen goods risk.
		ProcessStolenGoodsRisk();

		// Overnight burglary risk.
		ProcessBurglaryRisk();

		// Rep drift from upgrades.
		if ( Save.OwnsUpgrade( UpgradeId.CleanStore ) )
			Reputation.Add( 0.5f );

		// Expenses + bankruptcy check.
		var solvent = Economy.ProcessDayEnd();
		SummaryLedger = Economy.Ledger;

		Inventory.PruneHistory( Save.Day );

		Sfx.Play( Sfx.DayEnd, 0.7f );

		if ( !solvent )
		{
			State = GameState.Bankruptcy;
			SaveManager.Save( Save );
			UiState.Bump();
			return;
		}

		if ( Save.Debt > 0 )
			SummaryNotes.Add( $"Outstanding debt: {GameConstants.FormatCash( Save.Debt )} (interest accrues daily)." );

		State = GameState.DailySummary;
		Tutorial.Notify( TutorialTrigger.SawSummary );
		SaveManager.Save( Save );
		UiState.Bump();
	}

	/// <summary>Advance from the summary screen into the next morning.</summary>
	public void NextDay()
	{
		if ( State != GameState.DailySummary ) return;

		Save.Day++;
		Economy.ResetLedger( Save.Day );
		Events.RollForDay( Save.Day );
		BeginMorning( freshRoll: false );
		SaveManager.Save( Save );
	}

	public void RestartAfterBankruptcy()
	{
		LoadInto( SaveManager.Wipe() );
		Save.HasSeenIntro = true;
		BeginMorning( freshRoll: true );
		SaveManager.Save( Save );
	}

	private void ProcessStolenGoodsRisk()
	{
		var stolen = Save.Inventory.Where( i =>
			i.LegalStatus == LegalStatus.Stolen
			&& i.Location is ItemLocation.Backroom or ItemLocation.OnDisplay or ItemLocation.PawnStorage ).ToList();
		if ( stolen.Count == 0 ) return;

		var checkChance = Events.PoliceActive ? 0.95f : 0.12f + stolen.Count * 0.04f;
		if ( Game.Random.Float() > checkChance ) return;

		var item = stolen[Game.Random.Int( 0, stolen.Count - 1 )];
		var fine = Math.Max( 50, ItemValue.TrueValue( item, this ) / 2 );

		Inventory.Confiscate( item );
		Economy.Spend( fine );
		Economy.Ledger.Fines += fine;
		Reputation.Add( -8f );
		SummaryNotes.Add( $"Police traced a stolen {item.Name} to your shop. Confiscated + {GameConstants.FormatCash( fine )} fine." );
		Sfx.Play( Sfx.Alarm, 0.6f );
	}

	private void ProcessBurglaryRisk()
	{
		var displayed = Inventory.OnDisplay.ToList();
		if ( displayed.Count == 0 ) return;

		var risk = 0.05f + (100f - Save.Reputation) * 0.0008f;
		if ( Save.OwnsUpgrade( UpgradeId.AlarmSystem ) ) risk *= 0.15f;
		else if ( Save.OwnsUpgrade( UpgradeId.SecurityCamera ) ) risk *= 0.5f;

		if ( Game.Random.Float() > risk ) return;

		var item = displayed[Game.Random.Int( 0, displayed.Count - 1 )];
		var value = ItemValue.TrueValue( item, this );
		Economy.Ledger.TheftLosses += item.TotalInvested;
		Inventory.Remove( item );
		SummaryNotes.Add( $"Overnight burglary! Your {item.Name} was stolen ({GameConstants.FormatCash( item.TotalInvested )} invested, {GameConstants.FormatCash( value )} value)." );
		Sfx.Play( Sfx.Alarm, 0.7f );
	}

	// ==================================================================== UI control

	public void ToggleManagement()
	{
		if ( Negotiation.Active || State is GameState.MainMenu or GameState.DailySummary or GameState.Bankruptcy )
			return;
		ManagementOpen = !ManagementOpen;
		InspectionItemId = -1;
		Sfx.Play( Sfx.UiClick, 0.4f );
		UiState.Bump();
	}

	public void SetManagementTab( int tab )
	{
		ManagementTab = tab;
		Sfx.Play( Sfx.UiClick, 0.3f );
		UiState.Bump();
	}

	public void SelectInventoryItem( int id )
	{
		SelectedInventoryId = SelectedInventoryId == id ? -1 : id;
		UiState.Bump();
	}

	/// <summary>Force-select an inventory row (workbench / carry handoff).</summary>
	public void FocusInventoryItem( int id )
	{
		SelectedInventoryId = id;
		UiState.Bump();
	}

	public void OpenPauseMenu()
	{
		PauseMenuOpen = true;
		UiState.Bump();
	}

	public void ClosePauseMenu()
	{
		PauseMenuOpen = false;
		UiState.Bump();
	}

	/// <summary>Escape backs out one layer at a time. Returns true if something closed.</summary>
	public bool CloseTopmost()
	{
		if ( PauseMenuOpen ) { ClosePauseMenu(); return true; }
		if ( InspectionItemId >= 0 ) { CloseInspection(); return true; }
		if ( ManagementOpen ) { ManagementOpen = false; UiState.Bump(); return true; }
		if ( State is GameState.MorningPrep or GameState.ShopOpen or GameState.ShopClosing && !Negotiation.Active )
		{
			OpenPauseMenu();
			return true;
		}
		return false;
	}

	private void CloseAllUi()
	{
		ManagementOpen = false;
		InspectionItemId = -1;
		PauseMenuOpen = false;
		SelectedInventoryId = -1;
		UiState.Bump();
	}

	// ==================================================================== Carry / place (world)

	public bool TryPickupItem( int itemId )
	{
		if ( Chores?.CarryingTrash == true )
		{
			Toast( "Drop the trash at the dumpster first.", "delete" );
			Sfx.Play( Sfx.UiError, 0.4f );
			return false;
		}
		if ( CarriedItemId >= 0 )
		{
			Toast( "You're already carrying something. Press Q to put it down.", "luggage" );
			Sfx.Play( Sfx.UiError, 0.4f );
			return false;
		}

		var item = Inventory.Get( itemId );
		if ( item is null ) return false;
		if ( item.Location is not (ItemLocation.Backroom or ItemLocation.OnDisplay) ) return false;

		if ( item.Location == ItemLocation.OnDisplay )
			Inventory.Stow( item );

		CarriedItemId = item.Id;
		Sfx.Play( Sfx.ItemPlaced, 0.55f );
		Toast( $"Picked up {item.Name}. Carry it to a shelf or the workbench.", "luggage" );
		Shop.RefreshDisplays();
		UiState.Bump();
		return true;
	}

	public bool TryPlaceCarriedOnSlot( int slot )
	{
		var item = CarriedItem;
		if ( item is null ) return false;
		if ( !Inventory.SlotUnlocked( slot ) || Inventory.SlotOccupied( slot ) )
		{
			Toast( "That shelf isn't free.", "shelves" );
			Sfx.Play( Sfx.UiError, 0.4f );
			return false;
		}

		if ( Inventory.Display( item, slot ) )
		{
			CarriedItemId = -1;
			Sfx.Play( Sfx.ItemPlaced, 0.7f );
			Toast( $"{item.Name} is on the shelf for {GameConstants.FormatCash( item.SalePrice )}.", "storefront" );
			Tutorial.Notify( TutorialTrigger.DisplayedItem );
			UiState.Bump();
			return true;
		}
		return false;
	}

	/// <summary>Drop whatever is in hand — merchandise or trash bag.</summary>
	public bool DropHeld()
	{
		if ( Chores?.CarryingTrash == true )
			return Chores.DropTrash( ShopPlayer.Instance?.WorldPosition );

		return DropCarried();
	}

	public bool DropCarried()
	{
		if ( CarriedItemId < 0 ) return false;
		var name = CarriedItem?.Name ?? "item";
		CarriedItemId = -1;
		Sfx.Play( Sfx.ItemPlaced, 0.45f );
		Toast( $"Put {name} back in the backroom.", "inventory" );
		Shop.RefreshDisplays();
		UiState.Bump();
		return true;
	}

	// ==================================================================== Inspection

	public void OpenInspection( ItemInstance item )
	{
		if ( item is null ) return;
		InspectionItemId = item.Id;
		SelectedTool = InspectTool.Eyes;
		InspectionMessage = "Pick a tool and click an inspection point.";
		Tutorial.Notify( TutorialTrigger.OpenedInspection );
		Sfx.Play( Sfx.UiClick, 0.4f );
		UiState.Bump();
	}

	public void CloseInspection()
	{
		InspectionItemId = -1;
		UiState.Bump();
	}

	public void SelectTool( InspectTool tool )
	{
		if ( !Save.OwnsTool( tool ) ) return;
		SelectedTool = tool;
		Sfx.Play( Sfx.UiClick, 0.3f );
		UiState.Bump();
	}

	/// <summary>Examine an inspection spot with the currently selected tool.</summary>
	public void CheckSpot( int spotIndex )
	{
		var item = InspectionItem;
		if ( item is null ) return;

		var spots = InspectionModel.SpotsFor( item );
		if ( spotIndex < 0 || spotIndex >= spots.Count ) return;
		if ( item.CheckedSpots.Contains( spotIndex ) ) return;

		var spot = spots[spotIndex];
		var toolDef = ToolCatalog.Get( SelectedTool );

		// Inspecting during a live negotiation costs customer patience.
		if ( Negotiation.Active && Negotiation.Customer is not null )
			Negotiation.Customer.Patience -= toolDef.UseCost;

		if ( SelectedTool != spot.Tool )
		{
			InspectionMessage = SelectedTool == InspectTool.Eyes
				? $"{spot.Label}: can't tell much with the naked eye — this spot needs the {ToolCatalog.Get( spot.Tool ).Name}."
				: $"{spot.Label}: the {toolDef.Name} shows nothing conclusive here.";
			Sfx.Play( Sfx.UiClick, 0.3f );
			UiState.Bump();
			return;
		}

		item.CheckedSpots.Add( spotIndex );

		if ( spot.DefectId is not null )
		{
			item.DiscoverDefect( spot.DefectId );
			var defect = DefectCatalog.Get( spot.DefectId );
			InspectionMessage = $"{spot.Label}: {defect.Name} — {defect.Description}";

			if ( defect.CounterfeitSign )
			{
				Sfx.Play( Sfx.Alarm, 0.45f );
				Toast( $"Counterfeit sign found: {defect.Name}", "gpp_bad" );
			}
			else if ( defect.StolenSign )
			{
				Sfx.Play( Sfx.Alarm, 0.45f );
				Toast( $"Suspicious: {defect.Name}", "policy" );
			}
			else if ( defect.IsPositive )
			{
				Sfx.Play( Sfx.BigFind, 0.6f );
				Toast( $"Hidden value found: {defect.Name}!", "star" );
			}
			else
			{
				Sfx.Play( Sfx.UiClick, 0.5f );
			}

			Tutorial.Notify( TutorialTrigger.FoundDefect );
		}
		else
		{
			InspectionMessage = $"{spot.Label}: nothing wrong here.";
			Sfx.Play( Sfx.UiClick, 0.4f );
			Tutorial.Notify( TutorialTrigger.FoundDefect );
		}

		UiState.Bump();
	}

	// ==================================================================== Shopping (tools / upgrades)

	public bool BuyTool( InspectTool tool )
	{
		var def = ToolCatalog.Get( tool );
		if ( Save.OwnsTool( tool ) || !Economy.CanAfford( def.Cost ) )
		{
			Sfx.Play( Sfx.UiError, 0.5f );
			return false;
		}

		Economy.Spend( def.Cost );
		Save.Tools.Add( tool.ToString() );
		Sfx.Play( Sfx.CashRegister, 0.6f );
		Toast( $"Bought {def.Name}.", def.Icon );
		SaveManager.Save( Save );
		UiState.Bump();
		return true;
	}

	public bool BuyUpgrade( UpgradeId id )
	{
		var def = UpgradeCatalog.Get( id );
		if ( Save.OwnsUpgrade( id ) || !Economy.CanAfford( def.Cost ) )
		{
			Sfx.Play( Sfx.UiError, 0.5f );
			return false;
		}
		if ( def.Requires is { } req && !Save.OwnsUpgrade( req ) )
		{
			Sfx.Play( Sfx.UiError, 0.5f );
			return false;
		}

		Economy.Spend( def.Cost );
		Save.Upgrades.Add( id.ToString() );
		Sfx.Play( Sfx.Reward, 0.7f );
		Toast( $"Upgrade installed: {def.Name}!", def.Icon );
		Shop.RebuildUpgradeGeometry();
		SaveManager.Save( Save );
		UiState.Bump();
		return true;
	}

	// ==================================================================== Inventory actions (UI)

	public void SetSalePrice( ItemInstance item, int price )
	{
		if ( item is null ) return;
		item.SalePrice = Math.Clamp( price, 1, 999999 );
		Tutorial.Notify( TutorialTrigger.PricedItem );
		Shop.RefreshDisplays();
		UiState.Bump();
	}

	public void DisplayItem( ItemInstance item )
	{
		if ( item is null ) return;
		if ( Inventory.DisplayFull && item.Location != ItemLocation.OnDisplay )
		{
			Toast( "All display slots are full. Stow something first.", "shelves" );
			Sfx.Play( Sfx.UiError, 0.5f );
			return;
		}

		if ( Inventory.Display( item ) )
		{
			if ( CarriedItemId == item.Id ) CarriedItemId = -1;
			Sfx.Play( Sfx.ItemPlaced, 0.6f );
			Toast( $"{item.Name} is now on display for {GameConstants.FormatCash( item.SalePrice )}.", "storefront" );
			Tutorial.Notify( TutorialTrigger.DisplayedItem );
		}
		UiState.Bump();
	}

	public void StowItem( ItemInstance item )
	{
		Inventory.Stow( item );
		if ( CarriedItemId == item?.Id ) CarriedItemId = -1;
		Sfx.Play( Sfx.ItemPlaced, 0.5f );
		UiState.Bump();
	}

	public void CleanItem( ItemInstance item )
	{
		if ( Workshop.Clean( item ) )
		{
			Toast( $"Cleaned {item.Name}.", "cleaning_services" );
			Tutorial.Notify( TutorialTrigger.CleanedItem );
			Goals.Notify( GoalMetric.ItemsCleaned );
			Shop.RefreshDisplays();
		}
		else
		{
			Sfx.Play( Sfx.UiError, 0.4f );
		}
		UiState.Bump();
	}

	public void RepairItem( ItemInstance item )
	{
		var (ok, message) = Workshop.Repair( item, this );
		Toast( message, ok ? "handyman" : "error" );
		if ( ok ) Goals.Notify( GoalMetric.ItemsRepaired );
		Shop.RefreshDisplays();
		UiState.Bump();
	}

	public void ResearchItem( ItemInstance item )
	{
		var (ok, message) = Workshop.Research( item, this );
		Toast( message, ok ? "science" : "error" );
		if ( ok ) Goals.Notify( GoalMetric.ItemsResearched );
		UiState.Bump();
	}

	public void ScrapItem( ItemInstance item )
	{
		var value = Workshop.ScrapValue( item, this );
		if ( Workshop.Scrap( item, this ) )
			Toast( $"Scrapped {item.Name} for {GameConstants.FormatCash( value )}.", "recycling" );
		UiState.Bump();
	}

	/// <summary>A browser buys a displayed item at the sticker price (no haggling needed).</summary>
	public void CompleteStickerSale( CustomerProfile buyer, ItemInstance item )
	{
		if ( item is null || item.Location != ItemLocation.OnDisplay ) return;

		var price = Math.Max( 1, item.SalePrice );
		var profit = price - item.TotalInvested;

		Economy.Earn( price );
		Economy.Ledger.Sales++;
		Economy.Ledger.SaleRevenue += price;
		Economy.RecordDeal( item.Name, profit );
		Inventory.MarkSold( item, price );
		Save.ItemsSold++;
		Save.TotalDeals++;
		Save.BestFlip = Math.Max( Save.BestFlip, profit );
		Goals.Notify( GoalMetric.ItemsSold );
		Goals.Notify( GoalMetric.SaleRevenue, price );
		Goals.Notify( GoalMetric.DealsClosed );
		Collection.RecordFlip( item, this );

		if ( item.TrueAuthenticity != Authenticity.Genuine )
		{
			Reputation.Add( -6f );
			Toast( "Word spreads that you sold a fake. Reputation takes a hit.", "gpp_bad" );
		}
		else if ( profit > 0 )
		{
			Reputation.Add( 1f );
		}

		if ( buyer?.IsNamed == true )
			Relationships.RecordDeal( buyer.Id, buyer.Name, price, fair: true, lowball: false );

		Sfx.Play( Sfx.CashRegister, 0.8f );
		Toast( $"{buyer?.Name ?? "A customer"} bought {item.Name} for {GameConstants.FormatCash( price )} ({(profit >= 0 ? "+" : "")}{GameConstants.FormatCash( profit )}).", "sell" );
		Tutorial.Notify( TutorialTrigger.SoldItem );
		UiState.Bump();
	}

	// ==================================================================== Theft (open hours)

	/// <summary>Called by CustomerManager when a browser decides to pocket something.</summary>
	public void AttemptTheft( CustomerProfile thief )
	{
		var displayed = Inventory.OnDisplay.ToList();
		if ( displayed.Count == 0 ) return;

		var baseChance = 0.5f * Events.TheftMult;
		if ( Save.OwnsUpgrade( UpgradeId.AlarmSystem ) ) baseChance *= 0.1f;
		else if ( Save.OwnsUpgrade( UpgradeId.SecurityCamera ) ) baseChance *= 0.4f;

		var item = displayed.OrderBy( _ => Game.Random.Float() ).First();

		if ( Game.Random.Float() < baseChance )
		{
			Economy.Ledger.TheftLosses += item.TotalInvested;
			Inventory.Remove( item );
			Toast( $"THEFT! Someone pocketed your {item.Name} and slipped out!", "gpp_bad" );
			Sfx.Play( Sfx.Alarm, 0.7f );
			Reputation.Add( -1f );
		}
		else
		{
			Toast( Save.OwnsUpgrade( UpgradeId.SecurityCamera )
				? $"Camera caught {thief.Name} reaching for the {item.Name} — they bolted empty-handed."
				: $"{thief.Name} was acting shifty near the {item.Name}, then hurried out.", "videocam" );
			Sfx.Play( Sfx.Alarm, 0.4f );
		}
		UiState.Bump();
	}
}
