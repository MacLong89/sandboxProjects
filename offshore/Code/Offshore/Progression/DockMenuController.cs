namespace Offshore;

public enum DockPanel
{
	None,
	Hub,
	Sell,
	Upgrades,
	Boats,
	Travel,
	Journal,
	Contracts,
	Bait,
	Tournament
}

/// <summary>
/// Bait shop / dock menus: upgrades, boats, bait, sell, travel, journal, etc.
/// Open by walking to the shop (left side of the dock) and pressing E.
/// </summary>
public sealed class DockMenuController : Component
{
	public DockPanel Panel { get; private set; } = DockPanel.None;
	public int Cursor { get; private set; }
	public bool IsOpen => Panel != DockPanel.None;
	public string FlashMessage { get; private set; } = "";
	public int UpgradeTab { get; private set; }

	/// <summary>True when the angler is in the bait-shop zone and E can open the shop.</summary>
	public bool ShowShopPrompt { get; private set; }

	private static readonly UpgradeCategory[] UpgradeTabs =
	[
		UpgradeCategory.Rod,
		UpgradeCategory.Reel,
		UpgradeCategory.Line,
		UpgradeCategory.Hook,
		UpgradeCategory.Cooler,
		UpgradeCategory.Boat,
		UpgradeCategory.FishFinder
	];

	public static string UpgradeTabIcon( UpgradeCategory cat ) => cat switch
	{
		UpgradeCategory.Rod => OffshoreSprites.Paths.IconRod,
		UpgradeCategory.Reel => OffshoreSprites.Paths.IconReel,
		UpgradeCategory.Line => OffshoreSprites.Paths.IconLine,
		UpgradeCategory.Hook => OffshoreSprites.Paths.IconHook,
		UpgradeCategory.Cooler => OffshoreSprites.Paths.IconCooler,
		UpgradeCategory.Boat => OffshoreSprites.Paths.IconBoat,
		UpgradeCategory.FishFinder => OffshoreSprites.Paths.IconFinder,
		_ => OffshoreSprites.Paths.IconRod
	};

	public UpgradeCategory CurrentUpgradeTab =>
		UpgradeTabs[Math.Clamp( UpgradeTab, 0, UpgradeTabs.Length - 1 )];

	public IReadOnlyList<UpgradeDefinition> UpgradesInTab()
	{
		var cat = CurrentUpgradeTab;
		var list = new List<UpgradeDefinition>();
		foreach ( var u in UpgradeCatalog.All )
		{
			if ( u.Category == cat )
				list.Add( u );
		}
		return list;
	}

	private FishingSessionState _returnState = FishingSessionState.AimingCast;

	protected override void OnUpdate()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null || game.State == FishingSessionState.Paused )
			return;

		ShowShopPrompt = !IsOpen && IsNearShop( game ) && CanBrowseShop( game );

		if ( IsOpen )
		{
			// Mouse UI drives the shop; RMB closes (Esc handled by game controller).
			if ( Input.Pressed( "Attack2" ) )
				Close( game );
			return;
		}

		if ( !CanBrowseShop( game ) || !IsNearShop( game ) )
			return;

		// Pier-tip boat zone owns E when a boat is equipped (same band as the board prompt).
		if ( BoatBoardController.IsNearMooredBoat( game ) && BoatSystem.Equipped( game.Progression ) is not null )
			return;

		if ( Input.Pressed( "Use" ) || Input.Pressed( "Score" ) )
			OpenShop( game );
	}

	public bool TryClose( OffshoreGameController game )
	{
		if ( !IsOpen )
			return false;

		Close( game );
		return true;
	}

	public void OpenHub( OffshoreGameController game ) => OpenShop( game );

	public void OpenShop( OffshoreGameController game )
	{
		if ( game is null || !IsNearShop( game ) )
			return;
		Open( game, DockPanel.Sell );
	}

	public void OpenSellFromCoolerFull( OffshoreGameController game )
	{
		if ( game is null )
			return;
		Open( game, DockPanel.Sell );
	}

	// --- Mouse UI API ---

	public void UiOpenPanel( DockPanel panel )
	{
		var game = OffshoreGameController.Instance;
		if ( game is null || panel == DockPanel.None )
			return;
		Open( game, panel );
	}

	public void UiSetUpgradeTab( int tabIndex )
	{
		UpgradeTab = Math.Clamp( tabIndex, 0, UpgradeTabs.Length - 1 );
		Cursor = 0;
		FlashMessage = "";
	}

	public void UiSelectIndex( int index )
	{
		Cursor = Math.Max( 0, index );
		FlashMessage = "";
	}

	public void UiBuySelected()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null || !IsOpen )
			return;
		Activate( game );
	}

	public void UiSellAll()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null )
			return;
		if ( SellSystem.TrySellAll( game ) )
			FlashMessage = $"Sold for ${SellSystem.LastSummary?.TotalEarned:N0}  -  {SellSystem.LastSummary?.NextHint}";
		else
			FlashMessage = string.IsNullOrEmpty( game.StatusMessage ) ? "Nothing to sell" : game.StatusMessage;
	}

	public void UiClose()
	{
		var game = OffshoreGameController.Instance;
		if ( game is not null )
			Close( game );
	}

	public static bool IsNearShop( OffshoreGameController game )
	{
		var player = game?.Player;
		if ( player is null || !player.IsValid() )
			return false;

		// Left ~65% of the dock hub (+ pad). Center is DockHubWorldX.
		var halfW = OffshoreConstants.DockHubWidth * 0.5f;
		var left = OffshoreConstants.DockHubWorldX - halfW - 4f;
		var right = OffshoreConstants.DockHubWorldX + halfW * 0.55f;
		var x = player.WorldPosition.x;
		return x >= left && x <= right;
	}

	/// <summary>States where the angler can open or see the shop prompt.</summary>
	private static bool CanBrowseShop( OffshoreGameController game )
	{
		if ( game.Player?.Mode == AnglerController.LocomotionMode.InBoat )
			return false;

		return game.State is FishingSessionState.DockIdle
			or FishingSessionState.AimingCast
			or FishingSessionState.CoolerFull
			or FishingSessionState.FishEscaped;
	}

	private static bool CanOpenMenus( OffshoreGameController game ) => CanBrowseShop( game );

	private void Open( OffshoreGameController game, DockPanel panel )
	{
		if ( panel == DockPanel.None )
			return;

		// Already browsing the shop â€” switch tabs without re-entering state (mouse UI).
		if ( IsOpen && IsShopBrowsePanel( Panel ) && IsShopBrowsePanel( panel ) )
		{
			var prev = Panel;
			Panel = panel;
			Cursor = 0;
			if ( panel == DockPanel.Upgrades && prev != DockPanel.Upgrades )
				UpgradeTab = 0;
			FlashMessage = "";
			game.SetStatus( PanelTitle( panel ) );
			return;
		}

		_returnState = game.State is FishingSessionState.DockIdle
			? FishingSessionState.DockIdle
			: FishingSessionState.AimingCast;

		if ( game.Fishing?.Pending is not null )
			_returnState = FishingSessionState.CatchSuccess;
		else if ( game.State is FishingSessionState.CoolerFull or FishingSessionState.FishEscaped )
			_returnState = FishingSessionState.AimingCast;

		// One menu state for the whole bait shop so tabs can switch freely.
		var targetState = IsShopBrowsePanel( panel )
			? FishingSessionState.UpgradeMenu
			: panel switch
			{
				DockPanel.Journal or DockPanel.Contracts => FishingSessionState.JournalMenu,
				DockPanel.Tournament => FishingSessionState.Tournament,
				_ => FishingSessionState.UpgradeMenu
			};

		if ( !game.TrySetState( targetState ) && !ForceMenuState( game, targetState ) )
			return;

		Panel = panel;
		Cursor = 0;
		if ( panel == DockPanel.Upgrades )
			UpgradeTab = 0;
		FlashMessage = "";
		ContractSystem.EnsureSlots( game.Progression );
		game.SetStatus( PanelTitle( panel ) );
	}

	private static bool IsShopBrowsePanel( DockPanel panel ) =>
		panel is DockPanel.Hub or DockPanel.Sell or DockPanel.Upgrades
			or DockPanel.Boats or DockPanel.Bait or DockPanel.Travel;

	private static bool ForceMenuState( OffshoreGameController game, FishingSessionState state )
	{
		// CoolerFull / outcomes may need an interim hop.
		if ( game.State is FishingSessionState.CoolerFull
			or FishingSessionState.CatchSuccess
			or FishingSessionState.FishEscaped )
		{
			game.Fishing?.ForceResetVisuals();
			if ( game.StateMachine.ForceSet( state ) )
				return true;
		}

		return false;
	}

	private void Close( OffshoreGameController game )
	{
		Panel = DockPanel.None;
		Cursor = 0;
		FlashMessage = "";
		SellSystem.ClearLatch();
		game.Upgrades.ClearPurchaseLatch();
		BoatSystem.ClearLatch();

		if ( game.StateMachine.IsMenuOpen || game.State == FishingSessionState.Tournament )
			game.StateMachine.ForceSet( _returnState );

		game.SetStatus( "Hold Cast to charge  -  walk to the bait shop for upgrades" );
	}

	private void Activate( OffshoreGameController game )
	{
		switch ( Panel )
		{
			case DockPanel.Sell:
				UiSellAll();
				break;
			case DockPanel.Upgrades:
				ActivateUpgrade( game );
				break;
			case DockPanel.Boats:
				ActivateBoat( game );
				break;
			case DockPanel.Travel:
				ActivateTravel( game );
				break;
			case DockPanel.Bait:
				ActivateBait( game );
				break;
			case DockPanel.Journal:
			case DockPanel.Contracts:
			case DockPanel.Tournament:
			case DockPanel.Hub:
				break;
		}
	}

	private void ActivateUpgrade( OffshoreGameController game )
	{
		var inTab = UpgradesInTab();
		if ( Cursor < 0 || Cursor >= inTab.Count )
			return;

		var def = inTab[Cursor];
		if ( game.Upgrades.TryPurchase( game, def.Id ) )
			FlashMessage = game.StatusMessage;
		else
			FlashMessage = string.IsNullOrEmpty( game.StatusMessage ) ? "Can't buy" : game.StatusMessage;
	}

	private void ActivateBoat( OffshoreGameController game )
	{
		if ( Cursor < 0 || Cursor >= BoatCatalog.All.Count )
			return;

		var boat = BoatCatalog.All[Cursor];
		if ( BoatSystem.TryBuy( game, boat.Id ) )
			FlashMessage = game.StatusMessage;
		else
			FlashMessage = game.StatusMessage;
	}

	private void ActivateTravel( OffshoreGameController game )
	{
		if ( Cursor < 0 || Cursor >= LocationCatalog.All.Count )
			return;

		var loc = LocationCatalog.All[Cursor];
		if ( LocationManager.TryTravel( game, loc.Id ) )
		{
			FlashMessage = game.StatusMessage;
			Close( game );
		}
		else
			FlashMessage = game.StatusMessage;
	}

	private void ActivateBait( OffshoreGameController game )
	{
		if ( Cursor < 0 || Cursor >= BaitSystem.Baits.Length )
			return;

		var bait = BaitSystem.Baits[Cursor];
		BaitSystem.EnsureDefaults( game.Progression );
		if ( BaitSystem.Owns( game.Progression, bait ) )
		{
			if ( BaitSystem.TrySelect( game.Progression, bait ) )
			{
				OffshoreSaveSystem.Save( game.Progression );
				FlashMessage = $"Equipped {BaitSystem.DisplayName( bait )}";
				game.SetStatus( FlashMessage );
			}
			return;
		}

		if ( BaitSystem.TryBuy( game, bait ) )
			FlashMessage = game.StatusMessage;
		else
			FlashMessage = game.StatusMessage;
	}

	public int ItemCount( OffshoreGameController game ) => Panel switch
	{
		DockPanel.Sell => 1,
		DockPanel.Upgrades => UpgradesInTab().Count,
		DockPanel.Boats => BoatCatalog.All.Count,
		DockPanel.Travel => LocationCatalog.All.Count,
		DockPanel.Bait => BaitSystem.Baits.Length,
		_ => 0
	};

	public IReadOnlyList<string> Lines( OffshoreGameController game )
	{
		var list = new List<string>();
		switch ( Panel )
		{
			case DockPanel.Sell:
				var cooler = game.Progression.Cooler;
				if ( cooler.Count == 0 )
					list.Add( "Cooler empty" );
				else
					list.Add( $"{cooler.Count} fish  -  ~${game.Progression.CoolerEstimatedValue:N0}" );
				break;
			case DockPanel.Upgrades:
				foreach ( var u in UpgradesInTab() )
				{
					var lv = game.Upgrades.GetLevel( game.Progression, u.Id );
					var cost = lv >= u.MaxLevel ? "MAX" : $"${game.Upgrades.GetCost( u, lv ):N0}";
					list.Add( $"{u.DisplayName}  Lv {lv} / {u.MaxLevel}  {cost}" );
				}
				break;
			case DockPanel.Boats:
				foreach ( var b in BoatCatalog.All )
				{
					var owned = game.Progression.OwnedBoatIds.Contains( b.Id );
					var equipped = string.Equals( game.Progression.EquippedBoatId, b.Id, StringComparison.OrdinalIgnoreCase );
					var tag = equipped ? "EQUIPPED" : owned ? "OWNED" : $"${b.Price:N0}";
					list.Add(
						$"{b.DisplayName}  -  {tag}  -  hold +{b.CapacityBonus:0}  -  spd {b.MoveSpeed:0}  -  " +
						$"range {b.TripRange:0}  -  depth {b.MaxDepth:0}  -  max size {b.MaxFishSize:0.0}" );
				}
				break;
			case DockPanel.Travel:
				foreach ( var loc in LocationCatalog.All )
				{
					var unlocked = game.Progression.UnlockedLocationIds.Contains( loc.Id );
					var here = string.Equals( game.Progression.CurrentLocationId, loc.Id, StringComparison.OrdinalIgnoreCase );
					var tag = here ? "HERE" : unlocked ? "OPEN" : $"${loc.UnlockCost:N0}";
					list.Add( $"{loc.DisplayName}  -  {tag}" );
				}
				break;
			case DockPanel.Bait:
				BaitSystem.EnsureDefaults( game.Progression );
				foreach ( var bait in BaitSystem.Baits )
				{
					var owned = BaitSystem.Owns( game.Progression, bait );
					var selected = string.Equals( game.Progression.SelectedBaitId, bait, StringComparison.OrdinalIgnoreCase );
					if ( !owned )
						list.Add( $"{BaitSystem.DisplayName( bait )}  -  ${BaitSystem.Price( bait ):N0}" );
					else if ( selected )
						list.Add( $"{BaitSystem.DisplayName( bait )}  -  EQUIPPED" );
					else
						list.Add( $"{BaitSystem.DisplayName( bait )}  -  OWNED" );
				}
				break;
		}

		return list;
	}

	public static string PanelTitle( DockPanel panel ) => panel switch
	{
		DockPanel.Hub => "Bait Shop",
		DockPanel.Sell => "Sell Catch",
		DockPanel.Upgrades => "Upgrade Gear",
		DockPanel.Boats => "Boats",
		DockPanel.Travel => "Travel",
		DockPanel.Journal => "Fish Journal",
		DockPanel.Contracts => "Contracts",
		DockPanel.Bait => "Bait",
		DockPanel.Tournament => "Tournament",
		_ => ""
	};
}
