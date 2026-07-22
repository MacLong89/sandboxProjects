namespace CatchACritter;

public enum MenuTab { Shop, Sanctuary, Codex, Daily, Ascend }

/// <summary>
/// Lobby lifecycle + local UI state. Progression is per-client; the host only
/// owns the shared critter population.
/// </summary>
public sealed class CritterGame : Component, Component.INetworkListener
{
	public static CritterGame Instance { get; private set; }

	[Property] public bool StartServer { get; set; } = true;

	// ---- Local UI state ----
	public bool MenuOpen { get; private set; }
	public MenuTab ActiveTab { get; private set; } = MenuTab.Shop;
	public bool WelcomeOpen { get; set; }
	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public string TipToast { get; private set; } = "";
	public bool IsUiOpen => MenuOpen || WelcomeOpen || ActiveTutorialTip is not null;

	bool _worldBuilt;
	TimeUntil _tipCooldown;
	TimeUntil _tipToastHide;

	protected override void OnAwake()
	{
		Instance = this;
		EnsureWorld();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor ) return;
		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Opening Critter Isle";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() { MaxPlayers = Balance.MaxLobbyPlayers } );
		}
	}

	protected override void OnStart()
	{
		Instance = this;
		EnsureWorld();

		var progress = PlayerProgress.Local;
		if ( progress is not null && (progress.Data.SeenWelcome && (progress.OfflineCoinsEarned > 1 || progress.StreakGemsGranted > 0)) )
			WelcomeOpen = true;
		else if ( progress is not null && !progress.Data.SeenWelcome )
			RefreshTutorialTips();
	}

	protected override void OnUpdate()
	{
		// Menu hotkey (Tab)
		if ( Input.Pressed( "score" ) )
		{
			if ( MenuOpen ) CloseMenu();
			else OpenMenu( ActiveTab );
		}

		if ( MenuOpen && Input.EscapePressed )
		{
			Input.EscapePressed = false;
			CloseMenu();
		}

		if ( _tipToastHide )
			TipToast = "";

		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			ToggleTutorialTipsHidden();

		RefreshTutorialTips();

		Mouse.Visibility = IsUiOpen ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}

	void EnsureWorld()
	{
		if ( _worldBuilt || Scene.IsEditor ) return;
		if ( Scene.Directory.FindByName( "Island" ).FirstOrDefault().IsValid() ) { _worldBuilt = true; return; }
		WorldBuilder.Build( Scene );
		_worldBuilt = true;
	}

	public void OpenMenu( MenuTab tab )
	{
		ActiveTab = tab;
		MenuOpen = true;
	}

	public void CloseMenu() => MenuOpen = false;

	public void DismissWelcome()
	{
		WelcomeOpen = false;
		var progress = PlayerProgress.Local;
		if ( progress is not null )
		{
			progress.Data.SeenWelcome = true;
			progress.RequestSave();
		}
	}

	public void RefreshTutorialTips()
	{
		var progress = PlayerProgress.Local;
		if ( progress is null || WelcomeOpen || MenuOpen || progress.Data.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			return;
		}

		if ( ActiveTutorialTip is not null )
			return;

		if ( !_tipCooldown )
			return;

		ActiveTutorialTip = TutorialTips.PickNext( progress.Data );
	}

	public void DismissTutorialTip( bool hideAll = false )
	{
		var progress = PlayerProgress.Local;
		if ( progress is null )
			return;

		if ( ActiveTutorialTip is not null )
		{
			TutorialTips.MarkShown( progress.Data, ActiveTutorialTip.Id );
			ActiveTutorialTip = null;
		}

		progress.Data.SeenWelcome = true;

		if ( hideAll )
		{
			progress.Data.HideTutorialTips = true;
			TipToast = "Tips hidden — press H to show again";
			_tipToastHide = 3f;
			_tipCooldown = 0f;
		}
		else
		{
			// Wait for the next progress gate — don't chain the following tip immediately.
			_tipCooldown = 1.2f;
		}

		progress.RequestSave();
	}

	public void ToggleTutorialTipsHidden()
	{
		var progress = PlayerProgress.Local;
		if ( progress is null )
			return;

		progress.Data.HideTutorialTips = !progress.Data.HideTutorialTips;
		if ( progress.Data.HideTutorialTips )
		{
			ActiveTutorialTip = null;
			TipToast = "Tips hidden — press H to show again";
		}
		else
		{
			TipToast = "Tips enabled";
		}

		_tipToastHide = 3f;
		progress.RequestSave();
		RefreshTutorialTips();
	}

	// ---- Networking ----

	public void OnActive( Connection channel )
	{
		if ( !Networking.IsHost ) return;

		var angle = Game.Random.Float( 0f, MathF.Tau );
		var spawn = new Vector3( MathF.Cos( angle ) * 180f, MathF.Sin( angle ) * 180f, 10f );

		var go = new GameObject( true, $"Keeper - {channel.DisplayName}" );
		go.WorldPosition = spawn;

		go.Components.Create<CritterPlayer>();

		go.NetworkSpawn( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost ) return;
		var player = Scene.GetAllComponents<CritterPlayer>()
			.FirstOrDefault( p => p.Network.Owner == channel );
		if ( player.IsValid() )
			player.GameObject.Destroy();
	}

	/// <summary>Session leaderboard rows, best first.</summary>
	public IEnumerable<CritterPlayer> Leaderboard =>
		Scene.GetAllComponents<CritterPlayer>().OrderByDescending( p => p.SessionCatches );
}
