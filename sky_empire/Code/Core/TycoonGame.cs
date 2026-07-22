namespace SkyEmpire;

public enum MenuTab { Boosts, Rebirth, Daily, Help }

/// <summary>
/// Lobby lifecycle + local UI state + plot assignment. Progression is
/// per-client; the host only hands out plot indices.
/// </summary>
public sealed class TycoonGame : Component, Component.INetworkListener
{
	public static TycoonGame Instance { get; private set; }

	[Property] public bool StartServer { get; set; } = true;

	// ---- Local UI state ----
	public bool MenuOpen { get; private set; }
	public MenuTab ActiveTab { get; private set; } = MenuTab.Boosts;
	public bool WelcomeOpen { get; set; }
	public TutorialTipDef ActiveTutorialTip { get; private set; }
	public string TipToast { get; private set; } = "";
	public bool IsUiOpen => MenuOpen || WelcomeOpen || ActiveTutorialTip is not null;

	bool _worldBuilt;
	TimeUntil _tipToastHide;
	int _tutorialTipDismissedGoal = -1;

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
			LoadingScreen.Title = "Ascending to the Sky Ring";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() { MaxPlayers = Balance.MaxLobbyPlayers } );
		}
	}

	protected override void OnStart()
	{
		Instance = this;
		EnsureWorld();

		var progress = PlayerProgress.Local;
		if ( progress is not null && (progress.Data.SeenWelcome && (progress.OfflineCashEarned > 1 || progress.StreakGemsGranted > 0)) )
			WelcomeOpen = true;
		else if ( progress is not null && !progress.Data.SeenWelcome )
			RefreshTutorialTips();
	}

	protected override void OnUpdate()
	{
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

		UpdateFriendBoost();
	}

	void EnsureWorld()
	{
		if ( _worldBuilt || Scene.IsEditor ) return;
		if ( Scene.Directory.FindByName( "SkyWorld" ).FirstOrDefault().IsValid() ) { _worldBuilt = true; return; }
		WorldBuilder.Build( Scene );
		_worldBuilt = true;
	}

	/// <summary>Both sides earn while someone stands on someone else's island.</summary>
	void UpdateFriendBoost()
	{
		var progress = PlayerProgress.Local;
		var me = TycoonPlayer.Local;
		if ( progress is null || !me.IsValid() ) return;

		var boosted = false;
		var myPlot = WorldBuilder.PlotCenter( me.PlotIndex );
		foreach ( var other in Scene.GetAllComponents<TycoonPlayer>() )
		{
			if ( other == me ) continue;
			// A visitor on my island...
			if ( (other.WorldPosition - myPlot).WithZ( 0 ).Length < Balance.PlotRadius ) { boosted = true; break; }
			// ...or me visiting theirs.
			var theirPlot = WorldBuilder.PlotCenter( other.PlotIndex );
			if ( (me.WorldPosition - theirPlot).WithZ( 0 ).Length < Balance.PlotRadius ) { boosted = true; break; }
		}

		if ( boosted && !progress.FriendBoostActive )
			progress.AddToast( "🤝 Friend boost! You're both earning +25%!" );
		progress.FriendBoostActive = boosted;
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

		var tip = TutorialTips.PickNext( progress.Data, _tutorialTipDismissedGoal );
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
		var progress = PlayerProgress.Local;
		if ( progress is null )
			return;

		progress.Data.SeenWelcome = true;

		if ( hideAll )
		{
			TutorialTips.HideAllTips( progress.Data );
			ActiveTutorialTip = null;
			_tutorialTipDismissedGoal = -1;
			TipToast = "Tips hidden — press H to show again";
			_tipToastHide = 3f;
		}
		else
		{
			// Soft dismiss — stay hidden for this milestone until it completes.
			_tutorialTipDismissedGoal = ActiveTutorialTip?.GoalIndex
				?? progress.Data.MilestoneIndex;
			ActiveTutorialTip = null;
		}

		progress.RequestSave();
	}

	/// <summary>Milestone completed — force the next goal's coach card.</summary>
	public void AdvanceTutorialTipAfterMilestone( int completedGoalIndex )
	{
		var progress = PlayerProgress.Local;
		if ( progress is null )
			return;

		TutorialTips.MarkShown( progress.Data, completedGoalIndex );
		ActiveTutorialTip = null;
		_tutorialTipDismissedGoal = -1;
		RefreshTutorialTips();
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
			_tutorialTipDismissedGoal = -1;
			TipToast = "Tips hidden — press H to show again";
		}
		else
		{
			_tutorialTipDismissedGoal = -1;
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

		// First plot index not already claimed by a live player.
		var used = Scene.GetAllComponents<TycoonPlayer>().Select( p => p.PlotIndex ).ToHashSet();
		var plot = 0;
		while ( used.Contains( plot ) && plot < Balance.PlotCount - 1 ) plot++;

		var go = new GameObject( true, $"Baron - {channel.DisplayName}" );
		go.WorldPosition = WorldBuilder.PlotSpawn( plot );

		var player = go.Components.Create<TycoonPlayer>();
		player.PlotIndex = plot;
		player.OutfitColor = OutfitFor( channel.DisplayName ?? "baron" );

		go.NetworkSpawn( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		if ( !Networking.IsHost ) return;
		var player = Scene.GetAllComponents<TycoonPlayer>()
			.FirstOrDefault( p => p.Network.Owner == channel );
		if ( player.IsValid() )
			player.GameObject.Destroy();
	}

	static Color OutfitFor( string name )
	{
		var hash = 17;
		foreach ( var c in name ) hash = hash * 31 + c;
		return new ColorHsv( Math.Abs( hash ) % 360, 0.55f, 0.9f );
	}

	/// <summary>Session leaderboard rows, best first.</summary>
	public IEnumerable<TycoonPlayer> Leaderboard =>
		Scene.GetAllComponents<TycoonPlayer>()
			.OrderByDescending( p => p.Rebirths )
			.ThenByDescending( p => p.SessionEarned );
}
