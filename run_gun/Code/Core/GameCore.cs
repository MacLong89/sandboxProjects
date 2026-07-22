namespace RunGun;

public enum GamePhase { Start, Running, GameOver }

public sealed class GameCore : Component
{
	public static GameCore Instance { get; private set; }

	public SaveData Save { get; private set; }
	public PlayerWallet Wallet { get; private set; }
	public UpgradeSystem Upgrades { get; private set; }
	public CharacterSystem Characters { get; private set; }
	public AchievementSystem Achievements { get; private set; }
	public DailyChallengeSystem Daily { get; private set; }
	public MissionSystem Missions { get; private set; }
	public PowerPickSystem PowerPicks { get; private set; }
	public RunState Run { get; private set; }

	public TrackManager Track { get; private set; }
	public RunnerPlayer Player { get; private set; }

	public GamePhase Phase { get; private set; } = GamePhase.Start;
	public bool ShopOpen { get; private set; }
	public bool CharacterSelectOpen { get; private set; }
	public string StatusBanner { get; private set; } = "";
	public float StatusBannerTime { get; private set; }
	public UpgradeId? FeaturedUpgrade { get; private set; }
	public string DistrictAnnounce { get; private set; } = "";
	public float DistrictAnnounceTime { get; private set; }
	public int LastDistrictIndex { get; private set; } = -1;

	public bool IsUiBlocking =>
		Phase != GamePhase.Running
		|| ShopOpen
		|| CharacterSelectOpen
		|| (PowerPicks?.IsOpen ?? false);

	public double LastRunReward { get; private set; }
	public float LastRunDistance { get; private set; }
	public double LastRunScore { get; private set; }
	public double LastMissionReward { get; private set; }
	public double LastPayoutBonus { get; private set; }

	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public string TipToast { get; private set; } = "";

	private TimeUntil _nextAutosave;
	private TimeUntil _tipToastHide;
	private bool _bootstrapped;
	private bool _tutorialDistanceGateChecked;

	protected override void OnAwake()
	{
		Instance = this;

		Save = SaveManager.Load();
		Wallet = new PlayerWallet( Save );
		Upgrades = new UpgradeSystem( Save );
		Characters = new CharacterSystem( Save );
		Achievements = new AchievementSystem( Save, Wallet );
		Daily = new DailyChallengeSystem( Save );
		Missions = new MissionSystem();
		PowerPicks = new PowerPickSystem();
		Run = new RunState( Upgrades, Characters, Daily );
	}

	protected override void OnStart()
	{
		var trackGo = new GameObject( true, "Track" );
		Track = trackGo.Components.Create<TrackManager>();

		var playerGo = new GameObject( true, "Player" );
		playerGo.WorldPosition = Vector3.Zero;
		Player = playerGo.Components.Create<RunnerPlayer>();

		var vfxGo = new GameObject( true, "Vfx" );
		vfxGo.Components.Create<VfxManager>();

		var hudGo = new GameObject( true, "HUD" );
		hudGo.Components.Create<ScreenPanel>();
		hudGo.Components.Create<UI.Hud>();

		Phase = GamePhase.Start;
		_nextAutosave = GameConstants.AutosaveInterval;
		_bootstrapped = true;

		if ( !Save.HasCompletedTutorialRun )
			StartRun();
	}

	protected override void OnUpdate()
	{
		if ( !_bootstrapped ) return;

		if ( StatusBannerTime > 0f )
			StatusBannerTime = MathF.Max( 0f, StatusBannerTime - Time.Delta );
		if ( DistrictAnnounceTime > 0f )
			DistrictAnnounceTime = MathF.Max( 0f, DistrictAnnounceTime - Time.Delta );

		RefreshTutorialTips();

		if ( _tipToastHide )
			TipToast = "";

		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			ToggleTutorialTipsHidden();

		if ( Phase == GamePhase.Running && Run.Active )
		{
			TickDistrictAnnounce();
			TickTutorialDistanceGate();

			if ( Run.Squad < 1f )
				EndRun();
		}

		if ( _nextAutosave )
		{
			_nextAutosave = GameConstants.AutosaveInterval;
			SaveManager.Save( Save );
		}
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
		{
			SaveManager.Save( Save );
			Instance = null;
		}
	}

	public void StartRun()
	{
		ShopOpen = false;
		CharacterSelectOpen = false;
		FeaturedUpgrade = null;
		LastPayoutBonus = 0;
		LastDistrictIndex = -1;
		DistrictAnnounce = "";
		DistrictAnnounceTime = 0f;

		Daily.BeginRun();
		Missions.BeginRun( Daily.TodaySeed );
		PowerPicks.Reset();
		Player?.ResetToStart( 0f );
		Run.Begin( 0f );
		Track?.ResetRun( 0f );
		Phase = GamePhase.Running;
		_tutorialDistanceGateChecked = false;
		RefreshTutorialTips();
	}

	private void EndRun()
	{
		Run.Active = false;
		Phase = GamePhase.GameOver;

		Missions.Update( Run );
		LastMissionReward = Missions.PendingReward;
		LastRunReward = Run.Coins + Missions.PendingReward;
		LastRunDistance = Run.DistanceMeters;
		LastRunScore = Run.Score;

		// First wreck only: tiny floor so a brand-new player can buy one upgrade.
		if ( Save.TotalRuns == 0 && LastRunReward < GameConstants.FirstRunBonusPayout )
		{
			LastPayoutBonus = GameConstants.FirstRunBonusPayout - LastRunReward;
			LastRunReward = GameConstants.FirstRunBonusPayout;
		}

		Wallet.Earn( LastRunReward );
		Save.TotalRuns++;
		Save.HasCompletedTutorialRun = true;

		if ( Run.DistanceMeters > Save.BestDistance )
			Save.BestDistance = Run.DistanceMeters;
		if ( Run.Score > Save.BestScore )
			Save.BestScore = Run.Score;

		if ( Daily.TryComplete( Run.DistanceMeters ) )
		{
			var reward = Daily.CompletionReward;
			Wallet.Earn( reward );
			LastRunReward += reward;
		}

		Achievements.CheckRunEnd( Run );
		FeaturedUpgrade = PickFeaturedUpgrade();
		Sfx.Play( Sfx.Death );
		SaveManager.Save( Save );
	}

	public UpgradeId? PickFeaturedUpgrade()
	{
		UpgradeId? best = null;
		var bestCost = double.PositiveInfinity;

		foreach ( var def in UpgradeSystem.All )
		{
			if ( Upgrades.IsMaxed( def.Id ) ) continue;
			var cost = Upgrades.NextCost( def.Id );
			if ( cost > Wallet.Cash ) continue;
			if ( cost >= bestCost ) continue;
			bestCost = cost;
			best = def.Id;
		}

		if ( best is not null ) return best;

		foreach ( var def in UpgradeSystem.All )
		{
			if ( Upgrades.IsMaxed( def.Id ) ) continue;
			var cost = Upgrades.NextCost( def.Id );
			if ( cost >= bestCost ) continue;
			bestCost = cost;
			best = def.Id;
		}

		return best;
	}

	public void BuyFeaturedUpgrade()
	{
		if ( FeaturedUpgrade is not { } id ) return;
		if ( BuyUpgrade( id ) )
			FeaturedUpgrade = PickFeaturedUpgrade();
	}

	public void OpenShop() { ShopOpen = true; CharacterSelectOpen = false; }
	public void CloseShop() => ShopOpen = false;
	public void ToggleShop() => ShopOpen = !ShopOpen;

	public void OpenCharacters() { CharacterSelectOpen = true; ShopOpen = false; }
	public void CloseCharacters() => CharacterSelectOpen = false;

	public bool BuyUpgrade( UpgradeId id )
	{
		var ok = Upgrades.TryPurchase( id, Wallet );
		if ( ok )
		{
			Sfx.Play( Sfx.Purchase );
			SaveManager.Save( Save );
			ShowBanner( $"{UpgradeSystem.Def( id ).Name} upgraded!", 1.6f );
		}
		return ok;
	}

	public bool UnlockCharacter( string id )
	{
		var ok = Characters.TryUnlock( id, Wallet );
		if ( ok )
		{
			Sfx.Play( Sfx.Purchase );
			SaveManager.Save( Save );
		}
		return ok;
	}

	public bool SelectCharacter( string id )
	{
		var ok = Characters.Select( id );
		if ( ok ) SaveManager.Save( Save );
		return ok;
	}

	public bool TryPrestige()
	{
		if ( Save.PrestigeLevel >= 10 ) return false;
		var totalLevels = UpgradeSystem.All.Sum( u => Upgrades.Level( u.Id ) );
		if ( totalLevels < 25 ) return false;

		Save.PrestigeLevel++;
		Save.Cash = 0;
		Upgrades.ResetForPrestige();

		Achievements.CheckPrestige();
		SaveManager.Save( Save );
		ShowBanner( $"RIOT LEVEL {Save.PrestigeLevel} — permanent coin bonus", 3f );
		return true;
	}

	public void OnBossKilled()
	{
		if ( Phase != GamePhase.Running || !Run.Active ) return;
		PowerPicks.OfferBossReward();
		if ( PowerPicks.IsOpen )
			ShowBanner( "BOSS DOWN — grab a power", 2f );
	}

	public void ChoosePowerPick( PowerPickId id )
	{
		if ( PowerPicks is null || !PowerPicks.IsOpen ) return;
		PowerPicks.Choose( id, Run );
		ShowBanner( PowerPickSystem.Catalog.First( p => p.Id == id ).Name, 1.8f );
	}

	public void ShowBanner( string text, float seconds )
	{
		StatusBanner = text ?? "";
		StatusBannerTime = seconds;
	}

	public void RefreshTutorialTips()
	{
		if ( Save.HideTutorialTips || Phase != GamePhase.Running || ShopOpen || CharacterSelectOpen
		     || (PowerPicks?.IsOpen ?? false) )
		{
			ActiveTutorialTip = null;
			return;
		}

		if ( ActiveTutorialTip is not null )
			return;

		ActiveTutorialTip = TutorialTips.PickNext( this );
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
			TipToast = "Tips hidden — press H to show again";
			_tipToastHide = 3f;
		}

		SaveManager.Save( Save );
	}

	/// <summary>Gate crossed during the first tutorial run — unlocks later tip gates.</summary>
	public void NotifyTutorialGateCrossed( BuildStat stat, GateOp op, float value )
	{
		if ( Save.HasCompletedTutorialRun || Phase != GamePhase.Running )
			return;

		var isTrap = stat == BuildStat.Squad && op == GateOp.Add && value < 0f;

		if ( !isTrap && stat == BuildStat.Squad )
			Save.TutorialGreenGatePassed = true;

		if ( isTrap && Run.Squad >= 1f )
			Save.TutorialRedSurvived = true;

		RefreshTutorialTips();
	}

	public void ToggleTutorialTipsHidden()
	{
		Save.HideTutorialTips = !Save.HideTutorialTips;

		if ( Save.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			TipToast = "Tips hidden — press H to show again";
		}
		else
		{
			TipToast = "Tips enabled";
		}

		_tipToastHide = 3f;
		SaveManager.Save( Save );
		RefreshTutorialTips();
	}

	private void TickDistrictAnnounce()
	{
		var track = Track;
		if ( track is null ) return;
		var idx = track.CurrentBiomeIndex;
		if ( idx == LastDistrictIndex ) return;
		LastDistrictIndex = idx;
		DistrictAnnounce = DistrictTheme.Name( idx );
		DistrictAnnounceTime = 2.8f;
		ShowBanner( $"{DistrictTheme.Name( idx )} — {DistrictTheme.Tagline( idx )}", 2.6f );
	}

	private void TickTutorialDistanceGate()
	{
		if ( Save.HasCompletedTutorialRun || _tutorialDistanceGateChecked )
			return;

		if ( Run.DistanceMeters < TutorialTips.RedTipDistanceMeters )
			return;

		_tutorialDistanceGateChecked = true;
		RefreshTutorialTips();
	}
}
