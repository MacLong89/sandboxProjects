namespace UnderPressure;

/// <summary>
/// Central runtime hub. Owns the plain-C# game systems, drives the save/daily
/// bootstrap, handles job-completion flow and crew passive income, and exposes the actions the
/// HUD calls. Lives for the whole session as a single component.
/// </summary>
public sealed class GameCore : Component
{
	public static GameCore Instance { get; private set; }

	public SaveData Save { get; private set; }
	public PlayerWallet Wallet { get; private set; }
	public UpgradeSystem Upgrades { get; private set; }
	public PrestigeSystem Prestige { get; private set; }
	public JobSiteManager Jobs { get; private set; }
	public ToolSystem Tools { get; private set; }

	// --- UI-facing state ---
	public bool ShopOpen { get; private set; }

	/// <summary>The van locker (tool swap + depart) is open.</summary>
	public bool VanMenuOpen { get; private set; }

	/// <summary>Set by the Van each frame when the crosshair is on it in reach.</summary>
	public bool VanFocused { get; set; }

	/// <summary>Job is finished and the player just needs to return to the van to leave.</summary>
	public bool AwaitingDeparture { get; private set; }

	/// <summary>Any modal UI that needs the cursor and should pause look/movement input.</summary>
	public bool IsUiBlocking => ShopOpen || VanMenuOpen || ShowDailyPopup || ShowHitmanBriefing
		|| ShowKnockoutPopup || IsKnockedOut || IsDeparting || ShowMissionBriefing || ShowDiscoveryPopup;

	/// <summary>Menus that pause fixer locomotion — not mission briefing or departure fade.</summary>
	public bool IsFixerApproachBlocked => ShopOpen || VanMenuOpen || ShowHitmanBriefing
		|| IsKnockedOut || ShowKnockoutPopup;

	/// <summary>True while any dismissable overlay is up (for Escape-to-close).</summary>
	public bool AnyMenuOpen => ShopOpen || VanMenuOpen || ShowDailyPopup || ShowHitmanBriefing
		|| ShowKnockoutPopup || ShowMissionBriefing || ShowDiscoveryPopup;

	public bool ShowCompletion { get; private set; }
	public string CompletedJobName { get; private set; } = "";
	public double CompletionBonus { get; private set; }

	/// <summary>A spotless-100% perfectionist bonus was just earned (celebration flash).</summary>
	public bool ShowPerfect { get; private set; }
	public double PerfectBonus { get; private set; }
	/// <summary>True when the job hit spotless the same moment it first completed.</summary>
	public bool SpotlessAtCompletion { get; private set; }

	public DailyRewardSystem.Result DailyPopup { get; private set; }
	public bool ShowDailyPopup { get; private set; }

	/// <summary>Level-3 fixer conversation that unlocks the classified gun.</summary>
	public bool ShowHitmanBriefing { get; private set; }
	public int HitmanBriefingLine { get; private set; }

	/// <summary>Short-lived HUD alert when a pest harasses the player.</summary>
	public bool ShowHarassment { get; private set; }
	public string HarassmentAlert { get; private set; } = "";
	public string HarassmentSubtext { get; private set; } = "";
	public int PestHitCount { get; private set; }
	public bool IsKnockedOut { get; private set; }
	public bool ShowKnockoutPopup { get; private set; }
	public string KnockoutContext { get; private set; } = "";
	public double KnockoutPenalty { get; private set; }

	/// <summary>Van departure cinematic — world frozen, screen black.</summary>
	public bool IsDeparting { get; private set; }
	public float DepartBlackout { get; private set; }

	/// <summary>Pests and ambient simulation pause during cinematic overlays and NPC conversations.</summary>
	public bool IsWorldFrozen => IsDeparting || ShowMissionBriefing || ShowHitmanBriefing || ShowDiscoveryPopup;

	public bool ShowMissionBriefing { get; private set; }
	public string MissionBriefingTitle { get; private set; } = "";
	public string MissionBriefingBody { get; private set; } = "";
	public string MissionBriefingTag { get; private set; } = "";
	public string MissionBriefingAct { get; private set; } = "";
	public string MissionBriefingLocation { get; private set; } = "";
	public int MissionBriefingLevel { get; private set; }

	/// <summary>Leo's inner monologue when a hidden clue is uncovered.</summary>
	public bool ShowDiscoveryPopup { get; private set; }
	public string DiscoveryMonologue { get; private set; } = "";

	private readonly Queue<string> _discoveryQueue = new();

	private TimeUntil _completionTimer;
	private TimeUntil _perfectTimer;
	private bool _perfectAwarded;
	private TimeUntil _harassmentTimer;
	private TimeUntil _knockoutBlackoutTimer;
	private string _lastAttacker = "the pests";
	private TimeUntil _nextAutosave;
	private bool _dailyGrantedPending;

	// Looping background ambience for the whole session.
	private MusicPlayer _ambience;
	private SoundHandle _vanDepartSound;
	private float _departElapsed;
	private enum DepartPhase { None, Driving, Briefing, FadingIn }
	private DepartPhase _departPhase;

	protected override void OnAwake()
	{
		Instance = this;

		Save = SaveManager.Load();
		Wallet = new PlayerWallet( Save );
		Upgrades = new UpgradeSystem( Save );
		Prestige = new PrestigeSystem( Save );
		Tools = new ToolSystem( Save );
		Jobs = new JobSiteManager( Save, Wallet, Upgrades, Prestige );
	}

	protected override void OnStart()
	{
		Jobs.LoadJob( Save.JobIndex );

		DailyPopup = DailyRewardSystem.Apply( Save, Wallet, Prestige );
		_dailyGrantedPending = DailyPopup.Granted;
		ShowDailyPopup = false;

		HitmanBriefingNpc.EnsureForJob( Scene, Jobs.Index, Save );

		SpawnPlayer();
		SpawnVan();
		SpawnEnemies();
		SpawnPedestrians();
		SpawnHud();
		StartAmbience();

		_ = LeaderboardService.MigrateLifetimeTotal( Save );

		OpenMissionBriefing();

		_nextAutosave = GameConstants.AutosaveInterval;
	}

	protected override void OnUpdate()
	{
		// Crew passive income only ticks while the game is open.
		var idle = Upgrades.AutoIncomePerSecond * Prestige.Multiplier;
		if ( idle > 0 )
			Wallet.Earn( idle * Time.Delta );

		TickCompletion();
		TickDeparture();

		if ( _nextAutosave )
		{
			_nextAutosave = GameConstants.AutosaveInterval;
			SaveManager.Save( Save );
		}
	}

	protected override void OnDestroy()
	{
		_ambience?.Stop();
		_ambience = null;
		StopVanDepartSound();

		if ( Instance == this )
		{
			SaveManager.Save( Save );
			LeaderboardService.Flush();
			Instance = null;
		}
	}

	/// <summary>Loop quiet outdoor ambience under everything for the whole session.</summary>
	private void StartAmbience()
	{
		try
		{
			_ambience = MusicPlayer.Play( FileSystem.Mounted, "sounds/ambience.mp3" );
			if ( _ambience is not null )
			{
				_ambience.Repeat = true;
				_ambience.Volume = 0.35f;
			}
		}
		catch
		{
			// Missing/unreadable ambience shouldn't break startup.
		}
	}

	private void TickCompletion()
	{
		// Reaching 99% marks the job done: pay the completion bonus and let the player leave
		// from the van whenever they like. We never auto-advance (see DepartJob).
		if ( !AwaitingDeparture && Jobs.IsComplete )
		{
			CompletionBonus = Jobs.AwardCompletionBonus();
			CompletedJobName = Jobs.Current.Name;
			AwaitingDeparture = true;
			SpotlessAtCompletion = Jobs.IsSpotless;

			if ( SpotlessAtCompletion )
			{
				_perfectAwarded = true;
				PerfectBonus = Jobs.AwardPerfectBonus();
				ShowPerfect = true;
				_perfectTimer = 4f;
				Sfx.Play( Sfx.Reward );
			}
			else
			{
				ShowCompletion = true;
				_completionTimer = 3.5f;
				Sfx.Play( Sfx.JobComplete );
			}

			SaveManager.Save( Save );
		}

		// Mopping the last specks up to a spotless 100% after already finishing at 99%.
		if ( AwaitingDeparture && !_perfectAwarded && Jobs.IsSpotless )
		{
			_perfectAwarded = true;
			PerfectBonus = Jobs.AwardPerfectBonus();
			ShowPerfect = true;
			_perfectTimer = 3.5f;
			Sfx.Play( Sfx.Reward );
			SaveManager.Save( Save );
		}

		// Fade the celebration cards.
		if ( ShowCompletion && _completionTimer )
			ShowCompletion = false;

		if ( ShowPerfect && _perfectTimer )
			ShowPerfect = false;

		if ( ShowHarassment && _harassmentTimer )
			ShowHarassment = false;

		if ( IsKnockedOut && _knockoutBlackoutTimer )
			EndKnockoutBlackout();
	}

	// --- HUD actions ---
	public void SetShopOpen( bool open ) => ShopOpen = open;
	public void ToggleShop() => ShopOpen = !ShopOpen;

	/// <summary>
	/// Close the topmost open overlay, one press at a time (daily → shop).
	/// Returns true if something was closed. Used by the Escape key so the player can
	/// always back out of any menu.
	/// </summary>
	public bool CloseTopmostMenu()
	{
		if ( ShowDailyPopup ) { DismissDailyPopup(); return true; }
		if ( ShowKnockoutPopup ) { DismissKnockoutPopup(); return true; }
		if ( ShowDiscoveryPopup ) { DismissDiscoveryPopup(); return true; }
		if ( ShowMissionBriefing ) { DismissMissionBriefing(); return true; }
		if ( ShowHitmanBriefing ) { DismissHitmanBriefing(); return true; }
		if ( VanMenuOpen ) { CloseVanMenu(); return true; }
		if ( ShopOpen ) { SetShopOpen( false ); return true; }
		return false;
	}

	// --- Van locker (tool swap + departure) ---
	public void OpenVanMenu()
	{
		ShopOpen = false;
		VanMenuOpen = true;
	}

	public void CloseVanMenu() => VanMenuOpen = false;

	public void EquipTool( ToolType type )
	{
		if ( !Tools.IsOwned( type ) ) return;
		Tools.Equip( type );
		Sfx.Play( Sfx.Purchase );
		SaveManager.Save( Save );
	}

	/// <summary>Buy a new tool from the van shop, then auto-equip it.</summary>
	public bool BuyTool( ToolType type )
	{
		if ( !Tools.TryBuy( type, Wallet ) ) return false;

		Tools.Equip( type );
		Sfx.Play( Sfx.Purchase );
		SaveManager.Save( Save );
		return true;
	}

	/// <summary>Leave a finished job — fade out, drive van, then show the next briefing.</summary>
	public void DepartJob()
	{
		if ( !AwaitingDeparture || IsDeparting )
			return;

		SpotlessAtCompletion = false;
		StartDeparture();
	}

	public void DismissMissionBriefing()
	{
		if ( !ShowMissionBriefing )
			return;

		ShowMissionBriefing = false;

		if ( _departPhase == DepartPhase.Briefing )
		{
			_departPhase = DepartPhase.FadingIn;
			_departElapsed = 0f;
		}
		else if ( _dailyGrantedPending )
		{
			ShowDailyPopup = true;
			_dailyGrantedPending = false;
		}
	}

	private void OpenMissionBriefing()
	{
		var job = Jobs.Current;
		MissionBriefingLevel = Jobs.Index + 1;
		MissionBriefingTitle = job.Name;
		MissionBriefingTag = job.BriefingTag ?? "";
		MissionBriefingAct = job.ActTitle ?? "";
		MissionBriefingLocation = job.Location ?? "";
		MissionBriefingBody = string.IsNullOrWhiteSpace( job.Briefing ) ? job.Blurb : job.Briefing;
		ShowMissionBriefing = true;
		ShopOpen = false;
		VanMenuOpen = false;
	}

	/// <summary>Called when the player washes away enough grime to expose a story clue.</summary>
	public void NotifyDiscovery( string id, string monologue )
	{
		if ( string.IsNullOrWhiteSpace( id ) || string.IsNullOrWhiteSpace( monologue ) )
			return;

		if ( Save.HasDiscovery( id ) )
			return;

		Save.MarkDiscovery( id );
		_discoveryQueue.Enqueue( monologue );
		SaveManager.Save( Save );
		TryShowNextDiscovery();
	}

	public void DismissDiscoveryPopup()
	{
		if ( !ShowDiscoveryPopup )
			return;

		ShowDiscoveryPopup = false;
		DiscoveryMonologue = "";
		TryShowNextDiscovery();
	}

	private void TryShowNextDiscovery()
	{
		if ( ShowDiscoveryPopup || ShowMissionBriefing || IsDeparting )
			return;

		if ( _discoveryQueue.Count == 0 )
			return;

		DiscoveryMonologue = _discoveryQueue.Dequeue();
		ShowDiscoveryPopup = true;
	}

	private void StartDeparture()
	{
		IsDeparting = true;
		ShowMissionBriefing = false;
		VanMenuOpen = false;
		ShowCompletion = false;
		ShowPerfect = false;
		_departPhase = DepartPhase.Driving;
		_departElapsed = 0f;
		DepartBlackout = 0f;

		if ( _ambience is not null )
			_ambience.Volume = 0.08f;

		_vanDepartSound = Sfx.PlayHandle( Sfx.VanDepart );
		if ( _vanDepartSound is { IsValid: true } )
			_vanDepartSound.Volume = 1f;
	}

	private void TickDeparture()
	{
		if ( _departPhase == DepartPhase.None )
			return;

		_departElapsed += Time.Delta;

		switch ( _departPhase )
		{
			case DepartPhase.Driving:
				TickDepartureDrive();
				break;
			case DepartPhase.Briefing:
				DepartBlackout = 1f;
				break;
			case DepartPhase.FadingIn:
				TickDepartureFadeIn();
				break;
		}
	}

	private void TickDepartureDrive()
	{
		var fadeIn = GameConstants.DepartFadeToBlack;
		var driveLength = GameConstants.DepartVanSoundLength;
		var vanFade = GameConstants.DepartVanSoundFadeOut;

		DepartBlackout = Math.Clamp( _departElapsed / fadeIn, 0f, 1f );

		var vanFadeStart = Math.Max( fadeIn, driveLength - vanFade );
		if ( _vanDepartSound is { IsValid: true } )
		{
			if ( _departElapsed >= vanFadeStart )
			{
				var t = Math.Clamp( (_departElapsed - vanFadeStart) / vanFade, 0f, 1f );
				_vanDepartSound.Volume = 1f - t;
			}
		}

		if ( _departElapsed < driveLength )
			return;

		StopVanDepartSound();
		FinishDepartureLoad();
	}

	private void FinishDepartureLoad()
	{
		AwaitingDeparture = false;
		_perfectAwarded = false;
		PestHitCount = 0;

		Jobs.AdvanceToNext();
		HitmanBriefingNpc.EnsureForJob( Scene, Jobs.Index, Save );
		PressureWasher.Instance?.ResetTank();
		SaveManager.Save( Save );

		OpenMissionBriefing();
		_departPhase = DepartPhase.Briefing;
		DepartBlackout = 1f;
	}

	private void TickDepartureFadeIn()
	{
		DepartBlackout = Math.Clamp( 1f - _departElapsed / GameConstants.DepartFadeFromBlack, 0f, 1f );

		if ( _ambience is not null )
			_ambience.Volume = 0.08f + 0.27f * (1f - DepartBlackout);

		if ( DepartBlackout > 0f )
			return;

		IsDeparting = false;
		_departPhase = DepartPhase.None;
		_departElapsed = 0f;

		if ( _ambience is not null )
			_ambience.Volume = 0.35f;
	}

	private void StopVanDepartSound()
	{
		if ( _vanDepartSound is { IsValid: true } )
			_vanDepartSound.Stop( 0.05f );
		_vanDepartSound = null;
	}

	public bool BuyUpgrade( UpgradeId id )
	{
		var ok = Upgrades.TryPurchase( id, Wallet );
		if ( ok )
		{
			Sfx.Play( Sfx.Purchase );
			SaveManager.Save( Save );
		}
		return ok;
	}

	public bool DoPrestige()
	{
		var ok = Prestige.TryPrestige( Wallet, Jobs );
		if ( ok )
		{
			Sfx.Play( Sfx.Prestige );
			PressureWasher.Instance?.ResetTank();
			SaveManager.Save( Save );
		}

		return ok;
	}

	public void DismissDailyPopup()
	{
		if ( ShowDailyPopup ) Sfx.Play( Sfx.Reward );
		ShowDailyPopup = false;
	}

	public void StartHitmanBriefing()
	{
		if ( Save.HitmanBriefingSeen || ShowHitmanBriefing )
			return;

		HitmanBriefingLine = 0;
		ShowHitmanBriefing = true;
		ShopOpen = false;
		VanMenuOpen = false;
	}

	public void AdvanceHitmanBriefing()
	{
		if ( !ShowHitmanBriefing )
			return;

		HitmanBriefingLine++;
		if ( HitmanBriefingLine >= 4 )
			DismissHitmanBriefing();
	}

	public void DismissHitmanBriefing()
	{
		if ( !ShowHitmanBriefing )
			return;

		ShowHitmanBriefing = false;
		Save.HitmanBriefingSeen = true;
		Save.HitmanContractUnlocked = true;
		Sfx.Play( Sfx.Purchase );
		PulseHarassment( "Classified hardware unlocked in your van." );
		SaveManager.Save( Save );

		foreach ( var npc in Scene.GetAllComponents<HitmanBriefingNpc>() )
			npc.GameObject.Destroy();
	}

	/// <summary>Flash a warning when pests sting, rob, or hose-fight the player.</summary>
	public void PulseHarassment( string message, string subtext = "" )
	{
		HarassmentAlert = message;
		HarassmentSubtext = subtext;
		ShowHarassment = true;
		_harassmentTimer = GameConstants.HarassmentAlertDuration;
	}

	/// <summary>Record a pest hit, show feedback, and knock the player out after too many.</summary>
	public void RegisterPestHit( string message, string attackerName )
	{
		if ( IsKnockedOut || ShowKnockoutPopup )
			return;

		_lastAttacker = attackerName;
		PestHitCount++;

		var max = GameConstants.PestHitsUntilKnockout;
		var hitsLeft = max - PestHitCount;
		var subtext = hitsLeft > 0
			? $"{hitsLeft} hit{(hitsLeft == 1 ? "" : "s")} until you pass out"
			: "";
		PulseHarassment( message, subtext );

		if ( PestHitCount >= max )
			BeginKnockout();
	}

	public void DismissKnockoutPopup() => ShowKnockoutPopup = false;

	/// <summary>Dev/cheat: wipe all progress and restart from level 1.</summary>
	public void ResetAllProgress()
	{
		StopVanDepartSound();

		Save = SaveManager.Wipe();
		Wallet = new PlayerWallet( Save );
		Upgrades = new UpgradeSystem( Save );
		Prestige = new PrestigeSystem( Save );
		Tools = new ToolSystem( Save );
		Jobs = new JobSiteManager( Save, Wallet, Upgrades, Prestige );

		ShopOpen = false;
		VanMenuOpen = false;
		AwaitingDeparture = false;
		ShowCompletion = false;
		ShowPerfect = false;
		SpotlessAtCompletion = false;
		ShowDailyPopup = false;
		ShowHitmanBriefing = false;
		HitmanBriefingLine = 0;
		ShowHarassment = false;
		PestHitCount = 0;
		IsKnockedOut = false;
		ShowKnockoutPopup = false;
		IsDeparting = false;
		DepartBlackout = 0f;
		ShowMissionBriefing = false;
		_departPhase = DepartPhase.None;
		_departElapsed = 0f;
		_perfectAwarded = false;

		Jobs.LoadJob( 0 );

		foreach ( var npc in Scene.GetAllComponents<HitmanBriefingNpc>() )
			npc.GameObject.Destroy();
		HitmanBriefingNpc.EnsureForJob( Scene, Jobs.Index, Save );

		PressurePlayer.Instance?.RecoverAtSpawn();
		PressureWasher.Instance?.ResetTank();
		Tools.Equip( ToolType.PressureWasher );

		SaveManager.Save( Save );
		_dailyGrantedPending = false;
		OpenMissionBriefing();

		Log.Info( "[UnderPressure] Progress reset — back to level 1." );
	}

	/// <summary>Dev/cheat: mark the current job spotless.</summary>
	public void CheatInstantComplete()
	{
		if ( Jobs is null || Jobs.TotalCells <= 0 )
		{
			Log.Warning( "[up_complete] No active job to complete." );
			return;
		}

		Jobs.InstantComplete();
		Log.Info( $"[up_complete] {Jobs.Current.Name} is now spotless ({Jobs.CleanedCells}/{Jobs.TotalCells})." );
	}

	/// <summary>Dev/cheat: jump to a specific level (1-based).</summary>
	public void CheatJumpToLevel( int level )
	{
		var maxLevel = JobCatalog.Jobs.Count;
		if ( level < 1 || level > maxLevel )
		{
			Log.Warning( $"[up_level] Use level 1–{maxLevel}." );
			return;
		}

		StopVanDepartSound();

		ShopOpen = false;
		VanMenuOpen = false;
		AwaitingDeparture = false;
		ShowCompletion = false;
		ShowPerfect = false;
		SpotlessAtCompletion = false;
		ShowDailyPopup = false;
		ShowHitmanBriefing = false;
		HitmanBriefingLine = 0;
		ShowHarassment = false;
		PestHitCount = 0;
		IsKnockedOut = false;
		ShowKnockoutPopup = false;
		IsDeparting = false;
		DepartBlackout = 0f;
		ShowMissionBriefing = false;
		_departPhase = DepartPhase.None;
		_departElapsed = 0f;
		_perfectAwarded = false;

		// Level 8 needs the classified contract unlocked for target/bodyguard spawns.
		if ( level >= GameConstants.HitmanContractJobIndex + 1 )
		{
			Save.HitmanBriefingSeen = true;
			Save.HitmanContractUnlocked = true;
		}
		else if ( level == GameConstants.HitmanBriefingJobIndex + 1 )
		{
			Save.HitmanBriefingSeen = false;
			Save.HitmanContractUnlocked = false;
		}

		Jobs.LoadJob( level - 1 );

		foreach ( var npc in Scene.GetAllComponents<HitmanBriefingNpc>() )
			npc.GameObject.Destroy();
		HitmanBriefingNpc.EnsureForJob( Scene, Jobs.Index, Save );

		PressurePlayer.Instance?.RecoverAtSpawn();
		PressureWasher.Instance?.ResetTank();

		SaveManager.Save( Save );
		OpenMissionBriefing();

		Log.Info( $"[up_level] Jumped to level {level}: {Jobs.Current.Name}" );
	}

	/// <summary>Dev/cheat: replay the level-3 fixer encounter on the current job.</summary>
	public void CheatResetFixerBriefing()
	{
		if ( Jobs.Index != GameConstants.HitmanBriefingJobIndex )
		{
			Log.Warning( $"[up_fixer] Must be on level {GameConstants.HitmanBriefingJobIndex + 1} (use up_level {GameConstants.HitmanBriefingJobIndex + 1})." );
			return;
		}

		ShowHitmanBriefing = false;
		HitmanBriefingLine = 0;
		Save.HitmanBriefingSeen = false;
		Save.HitmanContractUnlocked = false;

		foreach ( var npc in Scene.GetAllComponents<HitmanBriefingNpc>() )
			npc.GameObject.Destroy();
		HitmanBriefingNpc.EnsureForJob( Scene, Jobs.Index, Save );

		SaveManager.Save( Save );
		Log.Info( "[up_fixer] Fixer reset — he should approach again." );
	}

	private void BeginKnockout()
	{
		ShowHarassment = false;
		PestHitCount = 0;
		IsKnockedOut = true;
		_knockoutBlackoutTimer = GameConstants.KnockoutBlackoutDuration;

		KnockoutPenalty = ComputeKnockoutPenalty();
		if ( KnockoutPenalty > 0 )
			Wallet.TrySpend( KnockoutPenalty );

		KnockoutContext = $"You took too many hits from {_lastAttacker} and the rest of the crew. "
			+ "You woke up slumped against the van while they finished laughing.";

		Sfx.Play( Sfx.Footstep, 0.35f );
		PressurePlayer.Instance?.RecoverAtSpawn();
		PressureWasher.Instance?.DrainWater( 9999f );
		PressureWasher.Instance?.DrainStamina( 9999f );
	}

	private void EndKnockoutBlackout()
	{
		IsKnockedOut = false;
		ShowKnockoutPopup = true;
		PressureWasher.Instance?.ResetTank();
		Sfx.Play( Sfx.Reward, 0.45f );
	}

	private double ComputeKnockoutPenalty()
	{
		var cash = Wallet.Cash;
		if ( cash <= 0 )
			return 0;

		var raw = Math.Clamp(
			cash * GameConstants.KnockoutPenaltyPercent,
			GameConstants.KnockoutPenaltyMin,
			GameConstants.KnockoutPenaltyMax );
		return Math.Min( cash, raw );
	}

	private void SpawnPlayer()
	{
		var go = new GameObject( true, "Player" );
		go.WorldPosition = Jobs.SpawnPosition + Vector3.Up * 8f;
		go.Components.Create<PressurePlayer>();
	}

	private void SpawnVan()
	{
		var go = new GameObject( true, "Van" );
		go.Components.Create<Van>();
	}

	private void SpawnEnemies()
	{
		var go = new GameObject( true, "EnemyManager" );
		go.Components.Create<EnemyManager>();
	}

	private void SpawnPedestrians()
	{
		var go = new GameObject( true, "AmbientPedestrianManager" );
		go.Components.Create<AmbientPedestrianManager>();
	}

	private void SpawnHud()
	{
		var go = new GameObject( true, "HUD" );
		go.Components.Create<ScreenPanel>();
		go.Components.Create<UnderPressure.UI.Hud>();
	}
}
