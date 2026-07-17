namespace FinalOutpost;

public sealed class GameCore : Component
{
	public static GameCore Instance { get; private set; }
	/// <summary>Set when startup fails — surfaced on the boot fallback screen.</summary>
	public static string BootError { get; private set; }

	internal static void SetBootError( string message ) => BootError = message;

	public PlayerProfile Profile { get; private set; }
	public GameModeId ActiveMode { get; private set; } = GameModeId.Survival;
	public SaveData Save { get; private set; }
	public PlayerWallet Wallet { get; private set; }
	public ResourceBank Resources { get; private set; }
	public UpgradeSystem Upgrades { get; private set; }

	public OutpostManager Outpost { get; private set; }
	public CombatSystem Combat { get; private set; }
	public NightController Nights { get; private set; }
	public SeasonController Seasons { get; private set; }
	public OutpostCamera Camera { get; private set; }
	public BuildManager Build { get; private set; }
	public RepairManager Repairs { get; private set; }
	public DefenderManager Defenders { get; private set; }
	public PlotManager Plots { get; private set; }
	public WorkerManager Workers { get; private set; }
	public ExpeditionManager Expeditions { get; private set; }
	public ComboSystem Combo { get; private set; }

	public bool IsSurvival => ActiveMode == GameModeId.Survival;
	public bool IsCure => ActiveMode == GameModeId.RoadToCure;

	public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
	public bool ShopOpen { get; private set; }
	public bool SettingsOpen { get; private set; }
	public bool LeaderboardOpen { get; private set; }
	public bool RecruitOpen { get; private set; }
	public bool WorkersOpen { get; private set; }
	public bool ExpeditionsOpen { get; private set; }
	public bool MilestonesOpen { get; private set; }
	public bool CatalogOpen { get; private set; }
	public bool LegacyOpen { get; private set; }
	public bool CureProgressOpen { get; private set; }
	public bool TechTreeOpen { get; private set; }
	public bool WelcomePending { get; private set; }
	public bool DailyPending { get; private set; }
	public OfflineSummary Welcome { get; private set; }
	public DailyState Daily { get; private set; }
	public string ObjectiveToast { get; private set; }
	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public NightRecap PendingNightRecap { get; private set; }
	public string PendingSeasonRecap { get; private set; }
	public bool TutorialTipsHidden => Save?.HideTutorialTips ?? false;
	public bool IsUiBlocking => ShopOpen || SettingsOpen || LeaderboardOpen || RecruitOpen || WorkersOpen || ExpeditionsOpen
		|| MilestonesOpen || CatalogOpen || LegacyOpen || CureProgressOpen || TechTreeOpen || WelcomePending || DailyPending
		|| Phase == GamePhase.MainMenu || Phase == GamePhase.GameOver || Phase == GamePhase.NightRecap
		|| Phase == GamePhase.SeasonRecap || Phase == GamePhase.Victory;

	/// <summary>Permanent prestige bonus applied to gameplay scrap income.</summary>
	public double LegacyMult => IsSurvival ? 1.0 + Save.LegacyPoints * GameConstants.LegacyScrapBonusPer : 1.0;
	/// <summary>Combined scrap multiplier (upgrades × prestige legacy).</summary>
	public double ScrapMultiplier => Upgrades.ScrapMult * LegacyMult;

	private int PrestigeReach => Math.Max( Save.BestNight, Save.CurrentNight );
	public bool PrestigeAvailable => IsSurvival && PrestigeReach >= GameConstants.PrestigeMinNight;
	public int PrestigeGain => Math.Max( 1, PrestigeReach / GameConstants.PrestigeNightsPerPoint );
	public bool HasRunInProgress => IsSurvival
		? Save?.HasRunInProgress ?? false
		: Save?.HasCureRunInProgress ?? false;

	public bool CanContinueSurvival => Profile?.HasEverStartedSurvival == true && SaveManager.SurvivalSave.HasRunInProgress;
	public bool CanContinueCure => Profile?.HasEverStartedCure == true && SaveManager.CureSave.HasCureRunInProgress;

	public int LastNightReached { get; private set; }
	public int CheckpointYear { get; private set; }
	public int CheckpointSeason { get; private set; }

	private TimeUntil _nextAutosave;
	private TimeUntil _toastHide;
	private string _pendingRecapToast;
	private int _nightStartRecruits;
	private long _nightStartKills;

	protected override void OnAwake()
	{
		Instance = this;
		Log.Info( "[FinalOutpost] GameCore OnAwake" );
		Profile = SaveManager.Profile;
		ActiveMode = Profile.LastMode;
		Save = SaveManager.Load( ActiveMode );
		Save.Migrate();
		Profile.ApplyAudioTo( Save );
		AudioSettings.Bind( Save );
		Wallet = new PlayerWallet( Save );
		Resources = new ResourceBank( Save );
		Upgrades = new UpgradeSystem( Save );
		Upgrades.Changed += OnUpgradesChanged;
	}

	protected override void OnStart()
	{
		try
		{
			BootError = null;

			// Camera and HUD first — if world build fails, the player still gets a view and menu.
			if ( !Scene.GetAllComponents<OutpostCamera>().Any() )
			{
				var camGo = new GameObject( true, "Camera" );
				Camera = camGo.Components.Create<OutpostCamera>();
			}
			else
			{
				Camera = Scene.GetAllComponents<OutpostCamera>().FirstOrDefault();
			}

			if ( !Scene.GetAllComponents<HudHost>().Any() )
			{
				var hudGo = new GameObject( true, "HUD" );
				hudGo.Components.Create<HudHost>();
			}

			GameBoot.DedupeHudHosts( Scene );

			var outpostGo = new GameObject( true, "Outpost" );
			Outpost = outpostGo.Components.Create<OutpostManager>();
			Outpost.Build( Upgrades );

			var combatGo = new GameObject( true, "Combat" );
			Combat = combatGo.Components.Create<CombatSystem>();

			var nightGo = new GameObject( true, "Nights" );
			Nights = nightGo.Components.Create<NightController>();

			var seasonGo = new GameObject( true, "Seasons" );
			Seasons = seasonGo.Components.Create<SeasonController>();

			var buildGo = new GameObject( true, "Build" );
			Build = buildGo.Components.Create<BuildManager>();
			Repairs = buildGo.Components.Create<RepairManager>();

			var defenderGo = new GameObject( true, "Defenders" );
			Defenders = defenderGo.Components.Create<DefenderManager>();

			var plotGo = new GameObject( true, "PlotManager" );
			Plots = plotGo.Components.Create<PlotManager>();

			var workerGo = new GameObject( true, "Workers" );
			Workers = workerGo.Components.Create<WorkerManager>();

			var expeditionGo = new GameObject( true, "Expeditions" );
			Expeditions = expeditionGo.Components.Create<ExpeditionManager>();

			var comboGo = new GameObject( true, "Combo" );
			Combo = comboGo.Components.Create<ComboSystem>();

			var economyGo = new GameObject( true, "ColonyEconomy" );
			economyGo.Components.Create<ColonyEconomy>();

			var ordersGo = new GameObject( true, "UnitOrders" );
			ordersGo.Components.Create<UnitOrderController>();

			Build.LoadFromSave( Save );
			TileOccupancy.RebuildAll();
			try
			{
				Defenders.RebuildFromSave();
				Plots.RebuildFromSave();
				Workers.RebuildFromSave();
			}
			catch ( Exception e )
			{
				Log.Error( $"[FinalOutpost] Failed to rebuild units from save: {e.Message}" );
			}

			Outpost.ApplyUpgrades( Upgrades, healToFull: false );
			// AUDIT FIX H1: Build() always spawned full HP. Re-apply damage from save after load.
			Outpost.LoadPersistedHealth( Save );

			if ( IsSurvival )
			{
				Welcome = IdleProgress.Apply( this );
				WelcomePending = Welcome.Any;
				Daily = DailyReward.Evaluate( Save );
				DailyPending = Daily.Available;
			}

			Phase = GamePhase.MainMenu;
			_nextAutosave = GameConstants.AutosaveInterval;
			Log.Info( "[FinalOutpost] GameCore started." );
			BootDiagnostics.LogSceneState( "GameCoreStart", Scene );
		}
		catch ( Exception e )
		{
			SetBootError( e.Message );
			Log.Error( $"[FinalOutpost] GameCore startup failed: {e}" );
		}
	}

	protected override void OnUpdate()
	{
		if ( _nextAutosave )
		{
			_nextAutosave = GameConstants.AutosaveInterval;
			SaveManagerTouch();
		}

		if ( Phase is GamePhase.Day or GamePhase.Night )
		{
			if ( Phase == GamePhase.Day )
			{
				Build?.TickBarracksHeal( Time.Delta );
				Repairs?.Tick( Time.Delta );
			}

			var newlyDone = IsSurvival
				? Objectives.EvaluateAndReward( this ) ?? Milestones.EvaluateAndReward( this )
				: CureObjectives.EvaluateAndReward( this );
			if ( newlyDone is not null )
			{
				ObjectiveToast = newlyDone;
				_toastHide = 4.5f;
			}
		}

		if ( ObjectiveToast is not null && _toastHide )
			ObjectiveToast = null;

		if ( Phase is GamePhase.Day or GamePhase.Night )
			RefreshTutorialTips();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
		{
			Upgrades.Changed -= OnUpgradesChanged;
			SaveManagerTouch();
			Instance = null;
		}
	}

	public void SaveManagerTouch()
	{
		Build?.SaveTo( Save );
		// AUDIT FIX H1: persist perimeter/core HP alongside buildings (was missing entirely).
		Outpost?.SavePersistedHealth( Save );
		Plots?.SaveClearProgress( Save );
		Profile.PullAudioFrom( Save );
		SaveManager.Save( Save, ActiveMode );
		SaveManager.SaveProfile( Profile );
	}

	public void SwitchMode( GameModeId mode )
	{
		if ( ActiveMode == mode ) return;

		SaveManagerTouch();
		ActiveMode = mode;
		Profile.LastMode = mode;
		Save = SaveManager.Load( mode );
		Save.Migrate();
		Profile.ApplyAudioTo( Save );
		AudioSettings.Bind( Save );
		Upgrades.Changed -= OnUpgradesChanged;
		Wallet = new PlayerWallet( Save );
		Resources = new ResourceBank( Save );
		Upgrades = new UpgradeSystem( Save );
		Upgrades.Changed += OnUpgradesChanged;
	}

	public void PrepareContinue( GameModeId mode )
	{
		SwitchMode( mode );
		ApplyFreshRunWorld();
	}

	public void PrepareNewSurvival()
	{
		SwitchMode( GameModeId.Survival );
		Save.ResetToNew();
		Profile.HasEverStartedSurvival = true;
		ApplyFreshRunWorld();
	}

	public void PrepareNewCure( CureTeamId team )
	{
		SwitchMode( GameModeId.RoadToCure );
		Save.ResetCureRun( team );
		Profile.HasEverStartedCure = true;
		ApplyFreshRunWorld();
		TeamBonuses.ApplyStartingBonuses( this, team );
		SeasonCheckpoint.Save( Save );
		Seasons?.ResetTimers();
	}

	private void OnUpgradesChanged() => Outpost?.ApplyUpgrades( Upgrades, healToFull: false );

	public void StartDay()
	{
		ShopOpen = false;
		SettingsOpen = false;
		LeaderboardOpen = false;
		RecruitOpen = false;
		WorkersOpen = false;
		ExpeditionsOpen = false;
		MilestonesOpen = false;
		CatalogOpen = false;
		LegacyOpen = false;
		CureProgressOpen = false;
		TechTreeOpen = false;
		Combat?.ClearAll();
		Build?.ClearDestroyedRemnants();
		Phase = GamePhase.Day;
		Save.HasStartedRun = true;
		DayNightLighting.Instance?.SetNight( false );
		AmbiencePlayer.Instance?.RefreshVolumes();
		NightCombatMusicPlayer.Instance?.RefreshVolumes();
		RefreshTutorialTips();
		SaveManagerTouch();
		Workers?.TryAutoRepairOnDayStart();

		if ( IsCure )
			Seasons?.ResetTimers();
	}

	public void StartNight()
	{
		if ( !IsSurvival || Phase != GamePhase.Day ) return;

		Phase = GamePhase.Night;
		DayNightLighting.Instance?.SetNight( true );
		AmbiencePlayer.Instance?.RefreshVolumes();
		NightCombatMusicPlayer.Instance?.RefreshVolumes();
		ShopOpen = false;
		SettingsOpen = false;
		RecruitOpen = false;
		WorkersOpen = false;
		ExpeditionsOpen = false;
		MilestonesOpen = false;
		CatalogOpen = false;
		LegacyOpen = false;
		Combo?.ResetCombo();
		Build?.CancelBuildInteraction();
		Build?.Deselect();
		_nightStartRecruits = Save.Recruits.Count;
		_nightStartKills = Save.TotalKills;
		Defenders?.ClearAllOrders();
		Nights?.BeginNight( Save.CurrentNight );

		if ( Save.Recruits.Count > 0 )
			ShowToast( "Recruits auto-defend · use Command Recruits to focus a zombie or area", 4.5f );
	}

	public void TriggerThreat( float threatMult = 1f, string bossLabel = null )
	{
		if ( !IsCure || Phase != GamePhase.Day ) return;

		Phase = GamePhase.Night;
		DayNightLighting.Instance?.SetNight( true );
		AmbiencePlayer.Instance?.RefreshVolumes();
		NightCombatMusicPlayer.Instance?.RefreshVolumes();
		ShopOpen = false;
		SettingsOpen = false;
		RecruitOpen = false;
		WorkersOpen = false;
		ExpeditionsOpen = false;
		MilestonesOpen = false;
		CatalogOpen = false;
		LegacyOpen = false;
		CureProgressOpen = false;
		TechTreeOpen = false;
		Combo?.ResetCombo();
		Build?.CancelBuildInteraction();
		Build?.Deselect();
		Defenders?.ClearAllOrders();

		Save.CureThreatIndex++;
		var count = Math.Max( 6, (int)(CureConstants.ZombiesForThreat( Save ) * Math.Max( 1f, threatMult )) );
		Nights?.BeginThreat( count, Save.CureThreatIndex );

		if ( !string.IsNullOrEmpty( bossLabel ) )
			ShowToast( $"{bossLabel} — defend the outpost!" );
		else if ( Save.Recruits.Count > 0 )
			ShowToast( "Recruits auto-defend · use Command Recruits to focus a zombie or area", 4.5f );
	}

	public void OnThreatSurvived()
	{
		if ( !IsCure ) return;

		Save.ThreatsSurvivedThisSeason++;
		Save.TotalThreatsSurvived++;

		if ( Save.CurrentSeason == (int)CureSeason.Winter )
			Save.EverSurvivedWinterThreat = true;

		Save.ColonySickness = MathF.Max( 0f, Save.ColonySickness - CureConstants.SicknessAfterThreatDrop );

		var bonus = 8.0 + Save.CurrentYear * 2.0;
		Wallet.Earn( bonus * ScrapMultiplier );
		KnowledgeGain.OnThreatSurvived( this );

		Sfx.Play( Sfx.WaveClear );
		Combat?.ClearAll();
		DayNightLighting.Instance?.SetNight( false );
		Phase = GamePhase.Day;

		ShowToast( $"Threat repelled! +{CureConstants.KnowledgeFromThreatSurvived:0} Knowledge" );
		Build?.BarracksHealAfterNight();
		Defenders?.RebuildFromSave();
		Build?.ClearDestroyedRemnants();
		CureObjectives.EvaluateAndReward( this );
		SaveManagerTouch();
		Workers?.TryAutoRepairOnDayStart();
	}

	public void BeginSeasonRecap( string summary )
	{
		PendingSeasonRecap = summary;
		Phase = GamePhase.SeasonRecap;
	}

	public void DismissSeasonRecap()
	{
		if ( Phase != GamePhase.SeasonRecap ) return;

		PendingSeasonRecap = null;
		Phase = GamePhase.Day;
		ShowToast( "New season — keep pushing toward the cure." );
		SaveManagerTouch();
	}

	public void OnCureComplete()
	{
		Save.CureRunComplete = true;
		Phase = GamePhase.Victory;
		Sfx.Play( Sfx.WaveClear );
		SaveManagerTouch();
	}

	public void OnNightSurvived()
	{
		if ( IsCure ) return;
		var progressBefore = NightUnlocks.ProgressNight( Save );
		var survivedNight = Save.CurrentNight;

		var bonus = GameConstants.NightClearBonusBase + survivedNight * GameConstants.NightClearBonusPerNight;
		Wallet.Earn( bonus * ScrapMultiplier );

		var firstNightBonus = 0.0;
		if ( survivedNight == 1 && !Save.FirstNightBonusClaimed )
		{
			Save.FirstNightBonusClaimed = true;
			firstNightBonus = GameConstants.FirstNightSurvivalBonus * ScrapMultiplier;
			Wallet.Earn( firstNightBonus );
		}

		var plotExpansionBonus = 0.0;
		if ( survivedNight == 5 && !Save.FifthNightPlotBonusClaimed )
		{
			Save.FifthNightPlotBonusClaimed = true;
			plotExpansionBonus = GameConstants.FifthNightPlotBonus * ScrapMultiplier;
			Wallet.Earn( plotExpansionBonus );
		}

		Save.CurrentNight++;

		if ( Save.CurrentNight > Save.BestNight )
			Save.BestNight = Save.CurrentNight;

		var unlockMsg = NightUnlocks.DescribeNewUnlocks( Save, progressBefore, NightUnlocks.ProgressNight( Save ) );
		_pendingRecapToast = null;
		if ( firstNightBonus > 0 )
			_pendingRecapToast = $"First night survived! +{GameConstants.FormatScrap( GameConstants.FirstNightSurvivalBonus ).Replace( " scrap", "" )} scrap";
		if ( plotExpansionBonus > 0 )
		{
			var plotMsg = $"Night 5 plot fund! +{GameConstants.FormatScrap( GameConstants.FifthNightPlotBonus ).Replace( " scrap", "" )} scrap — claim adjacent land";
			_pendingRecapToast = _pendingRecapToast is null ? plotMsg : $"{_pendingRecapToast} · {plotMsg}";
		}
		if ( unlockMsg is not null )
			_pendingRecapToast = _pendingRecapToast is null ? $"Unlocked: {unlockMsg}" : $"{_pendingRecapToast} · Unlocked: {unlockMsg}";

		Sfx.Play( Sfx.WaveClear );
		Combat?.ClearAll();
		DayNightLighting.Instance?.SetNight( false );

		PendingNightRecap = NightRecap.Build(
			this,
			survivedNight,
			bonus * ScrapMultiplier,
			firstNightBonus,
			plotExpansionBonus,
			unlockMsg,
			_nightStartRecruits,
			_nightStartKills );

		Phase = GamePhase.NightRecap;
		SaveManagerTouch();
	}

	public void DismissNightRecap()
	{
		if ( Phase != GamePhase.NightRecap ) return;

		Build?.BarracksHealAfterNight();
		Defenders?.RebuildFromSave();
		Build?.ClearDestroyedRemnants();
		Phase = GamePhase.Day;
		PendingNightRecap = null;

		if ( _pendingRecapToast is not null )
		{
			ObjectiveToast = _pendingRecapToast;
			_toastHide = 5f;
			_pendingRecapToast = null;
		}

		RefreshTutorialTips();
		SaveManagerTouch();
		Workers?.TryAutoRepairOnDayStart();
	}

	public void OnCoreDestroyed()
	{
		if ( Phase == GamePhase.GameOver ) return;

		if ( IsCure )
		{
			CheckpointYear = Save.CheckpointYear;
			CheckpointSeason = Save.CheckpointSeason;
			SeasonCheckpoint.Restore( Save );
			ApplyFreshRunWorld();
			Phase = GamePhase.GameOver;
			Combat?.ClearAll();
			DayNightLighting.Instance?.SetNight( false );
			Sfx.Play( Sfx.GameOver );
			SaveManagerTouch();
			return;
		}

		LastNightReached = Save.CurrentNight;
		if ( LastNightReached > Save.BestNight )
			Save.BestNight = LastNightReached;

		LeaderboardService.RecordAndSubmitRun( Save, LastNightReached );
		Save.WipeAfterDefeat();
		ApplyFreshRunWorld();

		Phase = GamePhase.GameOver;
		Combat?.ClearAll();
		DayNightLighting.Instance?.SetNight( false );
		Sfx.Play( Sfx.GameOver );
		SaveManagerTouch();
	}

	public void RetrySeason()
	{
		if ( !IsCure ) return;
		SeasonCheckpoint.Restore( Save );
		ApplyFreshRunWorld();
		Phase = GamePhase.Day;
		Seasons?.ResetTimers();
		SaveManagerTouch();
		StartDay();
	}

	public void ReturnToMenu()
	{
		if ( Phase == GamePhase.Victory && IsCure )
		{
			Save.HasStartedRun = false;
			Save.CureRunComplete = true;
		}

		ShopOpen = false;
		SettingsOpen = false;
		LeaderboardOpen = false;
		RecruitOpen = false;
		WorkersOpen = false;
		ExpeditionsOpen = false;
		MilestonesOpen = false;
		CatalogOpen = false;
		LegacyOpen = false;
		CureProgressOpen = false;
		TechTreeOpen = false;
		Combat?.ClearAll();
		Build?.CancelBuildInteraction();
		Build?.Deselect();
		DayNightLighting.Instance?.SetNight( false );
		Phase = GamePhase.MainMenu;
	}

	public void ResetProgress()
	{
		Save.ResetToNew();
		ApplyFreshRunWorld();
	}

	/// <summary>Rebuilds the live world from the current save after a wipe.</summary>
	private void ApplyFreshRunWorld()
	{
		Combat?.ClearAll();
		DestructionFx.Clear();
		Build?.ClearAll();
		Repairs?.Clear();
		Defenders?.RebuildFromSave();
		Plots?.RebuildFromSave();
		if ( IsCure )
			RivalCivManager.EnsureSeeded( Save );
		Workers?.RebuildFromSave();
		Outpost?.Build( Upgrades );
		Outpost?.ApplyUpgrades( Upgrades, healToFull: true );
		// AUDIT FIX H1: wipes/prestiges clear SavedCoreHealth; continue/retry restore damage here.
		Outpost?.LoadPersistedHealth( Save );
		Build?.LoadFromSave( Save );
		TileOccupancy.RebuildAll();

		ShopOpen = false;
		SettingsOpen = false;
		LeaderboardOpen = false;
		RecruitOpen = false;
		WorkersOpen = false;
		ExpeditionsOpen = false;
		MilestonesOpen = false;
		CatalogOpen = false;
		LegacyOpen = false;
		WelcomePending = false;
		DailyPending = false;
		ObjectiveToast = null;
		ActiveTutorialTip = null;
		DayNightLighting.Instance?.SetNight( false );
		SaveManagerTouch();
	}

	/// <summary>Wipes the save and immediately begins a fresh run.</summary>
	public void ResetAndStart()
	{
		ResetProgress();
		StartDay();
	}

	public void OpenShop() => ShopOpen = true;
	public void CloseShop() => ShopOpen = false;
	public void ToggleShop() => ShopOpen = !ShopOpen;

	public void OpenSettings() => SettingsOpen = true;
	public void CloseSettings() => SettingsOpen = false;
	public void ToggleSettings() => SettingsOpen = !SettingsOpen;

	public void OpenLeaderboard() => LeaderboardOpen = true;
	public void CloseLeaderboard() => LeaderboardOpen = false;
	public void ToggleLeaderboard() => LeaderboardOpen = !LeaderboardOpen;

	public void OpenRecruit() => RecruitOpen = true;
	public void CloseRecruit() => RecruitOpen = false;
	public void ToggleRecruit() => RecruitOpen = !RecruitOpen;

	public void OpenWorkers() => WorkersOpen = true;
	public void CloseWorkers() => WorkersOpen = false;
	public void ToggleWorkers() => WorkersOpen = !WorkersOpen;

	public void OpenExpeditions() => ExpeditionsOpen = true;
	public void CloseExpeditions() => ExpeditionsOpen = false;
	public void ToggleExpeditions() => ExpeditionsOpen = !ExpeditionsOpen;

	public void OpenMilestones() => MilestonesOpen = true;
	public void CloseMilestones() => MilestonesOpen = false;

	public void OpenCatalog() => CatalogOpen = true;
	public void CloseCatalog() => CatalogOpen = false;
	public void ToggleCatalog() => CatalogOpen = !CatalogOpen;

	public void OpenCureProgress() => CureProgressOpen = true;
	public void CloseCureProgress() => CureProgressOpen = false;
	public void ToggleCureProgress() => CureProgressOpen = !CureProgressOpen;

	public void OpenTechTree() => TechTreeOpen = true;
	public void CloseTechTree() => TechTreeOpen = false;
	public void ToggleTechTree() => TechTreeOpen = !TechTreeOpen;

	public void OpenLegacy() => LegacyOpen = true;
	public void CloseLegacy() => LegacyOpen = false;

	public void DismissWelcome()
	{
		WelcomePending = false;
		RefreshTutorialTips();
	}

	public void ShowToast( string message, float seconds = 4.5f )
	{
		ObjectiveToast = message;
		_toastHide = seconds;
	}

	public void RefreshTutorialTips()
	{
		if ( Save.HideTutorialTips || WelcomePending || DailyPending
			|| Phase == GamePhase.NightRecap || Phase == GamePhase.SeasonRecap
			|| ShopOpen || SettingsOpen || LeaderboardOpen || RecruitOpen || WorkersOpen
			|| ExpeditionsOpen || MilestonesOpen || CatalogOpen || LegacyOpen
			|| CureProgressOpen || TechTreeOpen )
		{
			ActiveTutorialTip = null;
			return;
		}

		if ( ActiveTutorialTip is not null )
			return;

		if ( Phase is not GamePhase.Day and not GamePhase.Night )
			return;

		if ( Phase != GamePhase.Day )
		{
			ActiveTutorialTip = null;
			return;
		}

		if ( IsSurvival )
		{
			if ( NightUnlocks.ProgressNight( Save ) > TutorialTips.MaxNight )
			{
				ActiveTutorialTip = null;
				return;
			}

			ActiveTutorialTip = TutorialTips.PickNext( this );
			return;
		}

		if ( IsCure )
		{
			if ( CureConstants.ProgressSeason( Save ) > CureTutorialTips.MaxSeason )
			{
				ActiveTutorialTip = null;
				return;
			}

			ActiveTutorialTip = CureTutorialTips.PickNext( this );
		}
	}

	public void DismissTutorialTip( bool hideAll = false )
	{
		if ( ActiveTutorialTip is not null )
		{
			TutorialTips.MarkShown( Save, ActiveTutorialTip.Id );
			ActiveTutorialTip = null;
		}

		if ( hideAll )
		{
			Save.HideTutorialTips = true;
			ObjectiveToast = "Tips hidden — press H to show again";
			_toastHide = 3f;
		}

		SaveManagerTouch();
		RefreshTutorialTips();
	}

	public void ToggleTutorialTipsHidden()
	{
		Save.HideTutorialTips = !Save.HideTutorialTips;

		if ( Save.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			ObjectiveToast = "Tips hidden — press H to show again";
		}
		else
		{
			ObjectiveToast = "Tips enabled";
		}

		_toastHide = 3f;
		SaveManagerTouch();
		RefreshTutorialTips();
	}

	public void ClaimDaily()
	{
		if ( !IsSurvival || !DailyPending ) return;
		DailyReward.Claim( this );
		Daily = DailyReward.Evaluate( Save );
		DailyPending = false;
		RefreshTutorialTips();
	}

	/// <summary>Evacuate &amp; rebuild: soft-reset the run for permanent legacy points.</summary>
	public bool DoPrestige()
	{
		if ( !IsSurvival || !PrestigeAvailable ) return false;

		LeaderboardService.RecordAndSubmitRun( Save, PrestigeReach );
		Save.Prestige( PrestigeGain );
		ApplyFreshRunWorld();

		LegacyOpen = false;
		Phase = GamePhase.MainMenu;
		Sfx.Play( Sfx.WaveClear );
		SaveManagerTouch();
		return true;
	}

	public bool BuyUpgrade( UpgradeId id )
	{
		// AUDIT FIX H5: shop upgrades raised wall/core max HP mid-night. Day-only like place/repair.
		if ( Phase != GamePhase.Day ) return false;

		var ok = Upgrades.TryPurchase( id, Wallet );
		if ( ok )
		{
			Sfx.Play( Sfx.Purchase );
			SaveManagerTouch();
		}
		return ok;
	}
}
