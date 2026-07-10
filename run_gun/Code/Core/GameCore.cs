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
	public RunState Run { get; private set; }

	public TrackManager Track { get; private set; }
	public RunnerPlayer Player { get; private set; }

	public GamePhase Phase { get; private set; } = GamePhase.Start;
	public bool ShopOpen { get; private set; }
	public bool CharacterSelectOpen { get; private set; }
	public bool IsUiBlocking => Phase != GamePhase.Running;

	public double LastRunReward { get; private set; }
	public float LastRunDistance { get; private set; }
	public double LastRunScore { get; private set; }
	public double LastMissionReward { get; private set; }

	private TimeUntil _nextAutosave;

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
	}

	protected override void OnUpdate()
	{
		if ( Phase == GamePhase.Running && Run.Squad < 1f )
			EndRun();

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
		Daily.BeginRun();
		Missions.BeginRun( Daily.TodaySeed );
		Player?.ResetToStart( 0f );
		Run.Begin( 0f );
		Track?.ResetRun( 0f );
		Phase = GamePhase.Running;
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

		Wallet.Earn( LastRunReward );
		Save.TotalRuns++;
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
		Sfx.Play( Sfx.Death );
		SaveManager.Save( Save );
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
		return true;
	}
}
