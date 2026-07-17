namespace DeepDive;

/// <summary>
/// Hub for DEEP: balance, progression, run state, economy, upgrades, save, and bootstrap.
/// </summary>
public sealed class DeepDiveGame : Component
{
	public static DeepDiveGame Instance { get; private set; }

	public BalanceConfig Balance { get; private set; }
	public DiveRunState Run { get; private set; }
	public PlayerProgressionData Progression { get; private set; }
	public DeepDiveGameStateMachine State { get; private set; }
	public UpgradeSystem Upgrades { get; private set; }
	public SellSystem Seller { get; private set; }
	public ShopSystem Shop { get; private set; }
	public DiveSummary LastSummary { get; private set; }
	public LoadoutInventory Loadout => Progression?.Loadout;
	public DiveHistoryLog History => Progression?.History;

	public GamePhase Phase => State?.Phase ?? GamePhase.Boot;
	public DiverController Diver { get; private set; }
	public OxygenComponent Oxygen { get; private set; }
	public HealthComponent Health { get; private set; }
	public PressureComponent Pressure { get; private set; }
	public BoostComponent Boost { get; private set; }
	public DiveCamera DiveCamera { get; private set; }
	public DepthZoneVisuals ZoneVisuals { get; private set; }
	public CollectibleSpawnSystem Collectibles { get; private set; }
	public HazardSpawnSystem Hazards { get; private set; }
	public CreatureSpawnSystem Creatures { get; private set; }
	public StorySpawnSystem Stories { get; private set; }
	public CheckpointSystem Checkpoints { get; private set; }
	public VehicleSystem Vehicles { get; private set; }
	public ToolSystem Tools { get; private set; }
	public ObjectiveSystem Objectives { get; private set; }
	public DiveLog DiveLog { get; private set; }
	public DayClock Clock { get; private set; }

	public float SessionDeepestMeters => Progression?.DeepestEverMeters ?? 0f;
	public string StatusMessage { get; private set; } = "";
	public float StatusMessageRemaining { get; private set; }

	public bool IsUiBlocking => State?.IsUiBlocking ?? false;
	public bool CanStartDive =>
		State is not null && State.CanStartDive && Diver is not null && Oxygen is not null;

	private TimeUntil _holdPhaseEnds;
	private DepthZone _lastZone = (DepthZone)(-1);

	protected override void OnAwake()
	{
		Instance = this;
		Balance = BalanceConfig.CreateDefaults();
		Run = new DiveRunState();
		Progression = new PlayerProgressionData();
		State = new DeepDiveGameStateMachine();
		Seller = new SellSystem();
		Shop = new ShopSystem();
		Upgrades = new UpgradeSystem( Progression, Balance );
		Objectives = new ObjectiveSystem();
		DiveLog = new DiveLog();
		Clock = new DayClock();
		State.EnterBoot();
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor )
			return;

		DeepDiveSaveSystem.TryLoad( Progression );
		Upgrades.ApplyAllToBalance();

		BuildWorld();
		BuildDiver();
		BuildCamera();
		BuildHud();

		Collectibles?.RespawnAll( Balance );
		Hazards?.RespawnAll( Balance );

		State.EnterSurfaceIdle();
		Objectives.BeginSession( Progression );
		ShowMessage( "Start Dive or open Diver Hub", 2.5f );
		ZoneVisuals?.Apply( 0f );
		Progression.DiscoverZone( DepthZone.Sunlit );
	}

	protected override void OnUpdate()
	{
		if ( State.IsDivingActive )
		{
			Run.Tick( Time.Delta );
			Clock?.AdvanceDuringDive( Time.Delta );
			TrackZoneDiscovery();
			Objectives?.Tick( this );
		}

		TickStatusMessage();
		TickHoldPhases();
		HandleGlobalInput();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void StartDive()
	{
		if ( !CanStartDive )
			return;

		if ( !State.TryPrepareDive() )
			return;

		Upgrades.ApplyAllToBalance();
		Seller.ResetSaleLatch();
		LastSummary = null;

		Run.Begin( Balance.BaseHaulCapacity );
		Oxygen.ResetToFull();
		Health.ResetToFull( Balance.MaxHealth );
		Boost?.ResetToFull( Balance.BoostMaxEnergy );
		Diver.ApplyMovementFromBalance( Balance );
		Diver.BeginDive();
		Tools?.ClearDiveEffects();
		Loadout?.ApplyToHotbar( Tools?.Hotbar );
		DiveLog?.Clear();
		Objectives?.BeginDive( Progression );

		Collectibles?.RespawnAll( Balance );
		Hazards?.RespawnAll( Balance );
		Creatures?.RespawnAll( Balance );
		Stories?.RespawnAll( Balance );
		Checkpoints?.RespawnAll( Balance );
		Vehicles?.RespawnForDive( this );

		if ( !State.TryBeginDiving() )
		{
			State.EnterSurfaceIdle();
			Run.Clear();
			Vehicles?.Clear();
			return;
		}

		ShowMessage( "Diving..." );
	}

	public void CompleteDiveSuccess()
	{
		if ( !State.TryCompleteSuccess() )
			return;

		var maxDepth = Diver?.DiveMaxDepthMeters ?? Run.MaxDepthMeters;
		Run.SetDepth( maxDepth );
		if ( Progression.ApplyDiveMaxDepth( maxDepth ) )
			Run.MarkRecordBroken();

		var o2Left = Oxygen?.Fraction ?? 0f;
		Run.CompleteSuccess();
		Objectives?.NotifyDiveSuccess();
		Clock?.AdvanceAfterDive( Run.DiveDurationSeconds, success: true );
		Progression.RegisterSuccessfulDive();
		LastSummary = Seller.BuildSuccessfulPreview( Run, Run.Haul, Progression, Balance, o2Left );
		RecordHistory( LastSummary, success: true );
		Progression.AddShells( MathF.Max( 5f, (int)(Run.MaxDepthMeters / 20f) + Run.Haul.ItemCount * 2f ) );

		Diver?.ReturnToSurface( snap: false );
		Diver?.SetInVehicle( false );
		Loadout?.CaptureFromHotbar( Tools?.Hotbar );
		Tools?.ClearEffects();
		Vehicles?.Clear();
		// Wait for recap buttons — no short auto-dismiss.
		_holdPhaseEnds = 120f;
	}

	public void FailDive( DiveFailureReason reason = DiveFailureReason.OxygenDepleted )
	{
		if ( Diver is not null && !Diver.IsUnderwater && State.IsDivingActive )
		{
			CompleteDiveSuccess();
			return;
		}

		if ( !State.TryFail() )
			return;

		var maxDepth = Diver?.DiveMaxDepthMeters ?? Run.MaxDepthMeters;
		Run.SetDepth( maxDepth );
		Progression.ApplyDiveMaxDepth( maxDepth );
		Run.CompleteFailure( reason );
		Clock?.AdvanceAfterDive( Run.DiveDurationSeconds, success: false );
		Progression.RegisterFailedDive();
		LastSummary = Seller.SettleFailedDive( Run, Run.Haul, Progression, Balance );
		RecordHistory( LastSummary, success: false );
		DeepDiveSaveSystem.Save( Progression );

		Diver?.ReturnToSurface( snap: true );
		Diver?.SetInVehicle( false );
		Loadout?.CaptureFromHotbar( Tools?.Hotbar );
		Tools?.ClearEffects();
		Vehicles?.Clear();
		_holdPhaseEnds = Balance.DiveFailedHoldSeconds;
		ShowMessage( FailureMessage( reason ) );
	}

	/// <summary>Primary CTA — pay out haul bonuses and return to surface hub.</summary>
	public void SellAndContinue()
	{
		if ( Phase != GamePhase.DiveSuccess || LastSummary is null )
			return;

		LastSummary = Seller.CommitSuccessfulSale( LastSummary, Run.Haul, Progression );
		DeepDiveSaveSystem.Save( Progression );
		ReturnToSurfacePhase();
		ShowMessage( LastSummary?.Headline ?? "Sold", 2f );
	}

	/// <summary>Secondary CTA — discard unpaid haul and return (no payout).</summary>
	public void ReturnWithoutSelling()
	{
		if ( Phase != GamePhase.DiveSuccess )
			return;

		Seller.AbandonPendingHaul( Run.Haul );
		if ( LastSummary is not null )
			LastSummary.SaleCommitted = true;
		DeepDiveSaveSystem.Save( Progression );
		ReturnToSurfacePhase();
		ShowMessage( "Returned without selling", 2f );
	}

	public void ContinueAfterFail()
	{
		if ( Phase != GamePhase.DiveFailed )
			return;

		ReturnToSurfacePhase();
	}

	public void NotifyDepth( float depthMeters )
	{
		if ( !State.IsDivingActive )
			return;

		Run.SetDepth( depthMeters );

		var previousBest = Progression.DeepestEverMeters;
		if ( depthMeters > previousBest )
		{
			Progression.ApplyDiveMaxDepth( depthMeters );
			if ( !Run.BrokeDepthRecord )
			{
				Run.MarkRecordBroken();
				ShowMessage( $"NEW RECORD — {(int)depthMeters}m!", 1.6f );
			}
		}

		ZoneVisuals?.Apply( depthMeters );
	}

	public bool TryBuyUpgrade( string upgradeId )
	{
		if ( Phase is not (GamePhase.UpgradeMenu or GamePhase.DiverHub) )
			return false;

		if ( !Upgrades.TryBuy( upgradeId ) )
		{
			ShowMessage( "Can't buy", 1.2f );
			return false;
		}

		Oxygen?.ResetToFull();
		Health?.ResetToFull( Balance.MaxHealth );
		Diver?.ApplyMovementFromBalance( Balance );
		DeepDiveSaveSystem.Save( Progression );
		ShowMessage( "Upgrade purchased!", 1.4f );
		return true;
	}

	public bool TryBuyShopItem( string itemId )
	{
		if ( Phase != GamePhase.DiverHub )
			return false;

		var item = ShopCatalog.Get( itemId );
		if ( item is null || !Shop.TryBuy( item, Progression, Loadout ) )
		{
			ShowMessage( "Can't buy", 1.2f );
			return false;
		}

		DeepDiveSaveSystem.Save( Progression );
		ShowMessage( $"Bought {item.DisplayName}", 1.3f );
		return true;
	}

	public void OpenDiverHub()
	{
		if ( State.TryOpenDiverHub() )
			ShowMessage( "", 0.01f );
	}

	public void OpenJournal()
	{
		if ( State.TryOpenJournal() )
			ShowMessage( "", 0.01f );
	}

	private void RecordHistory( DiveSummary summary, bool success )
	{
		if ( summary is null || History is null ) return;

		var icons = new List<string>();
		var labels = new List<string>();
		foreach ( var item in summary.TopItems.Take( 4 ) )
		{
			labels.Add( item.Name );
			icons.Add( "/ui/icons/map_loot.png" );
		}

		History.Add( new DiveHistoryEntry
		{
			DiveNumber = summary.DiveNumber,
			DayNumber = Clock?.DayNumber ?? 1,
			TimeOfDay = Clock?.TimeFormatted ?? "08:00 AM",
			MaxDepthMeters = summary.MaxDepthMeters,
			DurationSeconds = summary.DurationSeconds,
			MoneyEarned = success ? summary.MoneyEarned : 0f,
			Success = success,
			LootIcons = icons,
			LootLabels = labels
		} );
	}

	public void ShowMessage( string text, float seconds = -1f )
	{
		StatusMessage = text ?? "";
		StatusMessageRemaining = seconds < 0f ? Balance.StatusMessageSeconds : seconds;
	}

	private void TrackZoneDiscovery()
	{
		var zone = Balance.ZoneAtDepth( Diver?.CurrentDepthMeters ?? 0f );
		if ( zone == _lastZone )
			return;

		_lastZone = zone;
		Progression.DiscoverZone( zone );
	}

	private static string FailureMessage( DiveFailureReason reason ) => reason switch
	{
		DiveFailureReason.OxygenDepleted => "Out of oxygen!",
		DiveFailureReason.HealthDepleted => "Suit ruptured!",
		DiveFailureReason.PressureDamage => "Crushed by pressure!",
		DiveFailureReason.LeftWorldBounds => "Lost in the dark!",
		DiveFailureReason.Abandoned => "Dive abandoned.",
		_ => "Dive failed!"
	};

	private void TickStatusMessage()
	{
		if ( StatusMessageRemaining <= 0f )
			return;

		StatusMessageRemaining -= Time.Delta;
		if ( StatusMessageRemaining <= 0f )
			StatusMessage = "";
	}

	private void TickHoldPhases()
	{
		if ( Phase is not (GamePhase.DiveSuccess or GamePhase.DiveFailed) )
			return;

		if ( _holdPhaseEnds )
			ReturnToSurfacePhase();
	}

	private void ReturnToSurfacePhase()
	{
		State.TryReturnToSurfaceIdle();
		Oxygen?.ResetToFull();
		Health?.ResetToFull( Balance.MaxHealth );
		ZoneVisuals?.Apply( 0f );
		Objectives?.BeginSession( Progression );
		ShowMessage( "Start Dive or open Diver Hub", 2.2f );
	}

	private void HandleGlobalInput()
	{
		if ( Input.EscapePressed )
		{
			Input.EscapePressed = false;

			if ( Phase is GamePhase.UpgradeMenu or GamePhase.JournalMenu or GamePhase.DiverHub )
			{
				State.TryCloseMenuToSurface();
				ShowMessage( "Start Dive or open Diver Hub", 1.5f );
				return;
			}

			TogglePause();
			return;
		}

		if ( Phase == GamePhase.Paused )
			return;

		if ( Phase == GamePhase.SurfaceIdle )
		{
			if ( Input.Pressed( "Menu" ) || Input.Pressed( "Score" ) )
			{
				OpenDiverHub();
				return;
			}
		}

		if ( Input.Pressed( "Use" ) || Input.Pressed( "Jump" ) )
		{
			if ( Phase == GamePhase.DiveSuccess )
			{
				SellAndContinue();
				return;
			}

			if ( Phase == GamePhase.DiveFailed )
			{
				ReturnToSurfacePhase();
				return;
			}

			if ( CanStartDive )
				StartDive();
		}
	}

	private void TogglePause()
	{
		if ( Phase == GamePhase.Paused )
		{
			if ( State.TryResume() )
				ShowMessage( "Resumed", 1f );
			return;
		}

		if ( !State.TryPause() )
			return;

		ShowMessage( "Paused — Esc to resume", 99f );
	}

	private void BuildWorld()
	{
		// Seabed first — props/loot/boat clamp against it.
		var seabedGo = new GameObject( true, "SeabedTerrain" );
		seabedGo.Components.Create<SeabedTerrain>();

		var surfaceGo = new GameObject( true, "SurfacePlatform" );
		surfaceGo.Components.Create<SurfacePlatform>();

		var oceanGo = new GameObject( true, "OceanBackdrop" );
		oceanGo.Components.Create<OceanBackdrop>();

		var zonesGo = new GameObject( true, "DepthZones" );
		ZoneVisuals = zonesGo.Components.Create<DepthZoneVisuals>();

		var markersGo = new GameObject( true, "DepthMarkers" );
		markersGo.Components.Create<DepthMarkers>();

		var backdropsGo = new GameObject( true, "ZoneBackdrops" );
		backdropsGo.Components.Create<ZoneBackdrops>();

		var boundsGo = new GameObject( true, "OceanBounds" );
		boundsGo.Components.Create<OceanBounds>();

		var lootGo = new GameObject( true, "Collectibles" );
		Collectibles = lootGo.Components.Create<CollectibleSpawnSystem>();

		var hazardGo = new GameObject( true, "Hazards" );
		Hazards = hazardGo.Components.Create<HazardSpawnSystem>();

		var creatureGo = new GameObject( true, "Creatures" );
		Creatures = creatureGo.Components.Create<CreatureSpawnSystem>();

		var storyGo = new GameObject( true, "Stories" );
		Stories = storyGo.Components.Create<StorySpawnSystem>();

		var checkpointGo = new GameObject( true, "Checkpoints" );
		Checkpoints = checkpointGo.Components.Create<CheckpointSystem>();

		var vehicleGo = new GameObject( true, "Vehicles" );
		Vehicles = vehicleGo.Components.Create<VehicleSystem>();

		var interactGo = new GameObject( true, "DiveInteract" );
		interactGo.Components.Create<DiveInteractSystem>();
	}

	private void BuildDiver()
	{
		var diverGo = new GameObject( true, "Diver" );
		diverGo.WorldPosition = new Vector3( Balance.SurfaceSpawnX, 0f, Balance.SurfaceSpawnZ );
		Diver = diverGo.Components.Create<DiverController>();
		Oxygen = diverGo.Components.Create<OxygenComponent>();
		Health = diverGo.Components.Create<HealthComponent>();
		Pressure = diverGo.Components.Create<PressureComponent>();
		Boost = diverGo.Components.Create<BoostComponent>();
		Tools = diverGo.Components.Create<ToolSystem>();
		Tools.ClearDiveEffects();
		Loadout?.ApplyToHotbar( Tools.Hotbar );
		Diver.ApplyMovementFromBalance( Balance );
		Health.ResetToFull( Balance.MaxHealth );
		Oxygen.ResetToFull();
		Boost.ResetToFull( Balance.BoostMaxEnergy );
	}

	private void BuildCamera()
	{
		var camGo = new GameObject( true, "DiveCamera" );
		DiveCamera = camGo.Components.Create<DiveCamera>();
	}

	private void BuildHud()
	{
		var hudGo = new GameObject( true, "HUD" );
		hudGo.Components.Create<ScreenPanel>();
		hudGo.Components.Create<UI.Hud>();
	}
}
