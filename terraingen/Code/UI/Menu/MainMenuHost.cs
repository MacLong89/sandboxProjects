namespace Terraingen.UI.Menu;

using Sandbox.UI;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Menu.Panels;

enum MainMenuView
{
	Root,
	Browser,
	Settings,
	Credits
}

/// <summary>Cinematic main menu UI — world-first layout, event-driven refresh.</summary>
[StyleSheet( "MainMenuHost.scss" )]
[Title( "Main Menu Host" )]
[Category( "Thorns/UI" )]
[Icon( "menu" )]
public sealed class MainMenuHost : PanelComponent
{
	public static MainMenuHost Instance { get; private set; }

	[Property] public bool LoadBackdropImage { get; set; } = true;

	Panel _leftRail;
	Panel _rootNav;
	Panel _worldSlot;
	Panel _screens;
	ServerBrowserScreen _browser;
	MainMenuSettingsScreen _settings;
	MainMenuCreditsScreen _credits;
	Panel _topRight;
	NewsPanel _news;
	MainMenuProgressOverlay _progressOverlay;
	MainMenuWorldNamePrompt _worldNamePrompt;
	MainMenuConfirmPrompt _confirmPrompt;

	MainMenuView _view = MainMenuView.Root;
	bool _uiBuilt;
	bool _building;
	bool _buildFailed;

	public bool IsUiBuilt => _uiBuilt;
	public bool IsPanelReady => Panel is not null && Panel.IsValid;

	protected override void OnAwake()
	{
		Instance = this;
		ThornsUiCursor.EnsureMainMenuVisible();
		ThornsUiManager.Reset( ThornsUiManager.UiContext.MainMenu );
		UiRevisionBus.ResetMenuListeners();
		Log.Info( "[Thorns Menu] MainMenuHost awake." );
	}

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		ThornsGameplayUiStyles.LoadMainMenuRoot( Panel );
		EnsureBuilt();
	}

	/// <summary>Rebuild shell after UI skin change (Settings → UI Skin).</summary>
	public void RequestSkinRebuild()
	{
		if ( !_uiBuilt )
			return;

		var view = _view;
		_uiBuilt = false;
		ThornsUiSkin.ApplyRoot( Panel );
		Panel.DeleteChildren( true );
		EnsureBuilt();
		if ( _uiBuilt )
			ShowView( view );
	}

	protected override void OnStart()
	{
		Log.Info( "[Thorns Menu] MainMenuHost OnStart." );
		_ = StartAsync();
	}

	async System.Threading.Tasks.Task StartAsync()
	{
		try
		{
			await System.Threading.Tasks.Task.Yield();

			var screenPanel = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
			if ( screenPanel is null || !screenPanel.IsValid )
				Log.Warning( "[Thorns Menu] MainMenuHost started without a ScreenPanel." );

			await System.Threading.Tasks.Task.Yield();

			ThornsLocalSettings.Load();
			ThornsMenuServerPrefs.Load();
			ThornsHostedServerCatalog.Load();
			_ = Components.Get<ThornsMainMenuAtmosphere>() ?? Components.Create<ThornsMainMenuAtmosphere>();

			ThornsMainMenuBackdrop.EnableImageBackdrop = LoadBackdropImage;

			EnsureBuilt();
			if ( _uiBuilt )
				ShowView( MainMenuView.Root );

			ThornsMenuJoinFlow.StageChanged += OnJoinStage;
		}
		catch ( Exception e )
		{
			Log.Error( e, "[Thorns Menu] MainMenuHost start failed." );
		}
	}

	protected override void OnUpdate()
	{
		ThornsKeybindService.TickCapture();

		if ( ThornsMenuSceneLoader.IsInGameplayScene() )
		{
			GameObject.Destroy();
			return;
		}

		ThornsUiCursor.EnsureMainMenuVisible();
		ThornsUiManager.SetContext( ThornsUiManager.UiContext.MainMenu );

		if ( Input.Pressed( "Menu" ) || Input.Pressed( "Cancel" ) )
			ThornsUiManager.TryHandleCancel( ThornsUiManager.UiContext.MainMenu );

		if ( !_uiBuilt && !_buildFailed )
			EnsureBuilt();

		_progressOverlay?.Tick();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;

		ThornsMenuJoinFlow.StageChanged -= OnJoinStage;

		if ( _browser is not null && _browser.IsValid )
			_browser.MenuBackPressed -= OnBrowserBack;

		if ( _settings is not null && _settings.IsValid )
			_settings.MenuBackPressed -= OnBrowserBack;

		if ( _credits is not null && _credits.IsValid )
			_credits.MenuBackPressed -= OnBrowserBack;
	}

	void OnJoinStage( ThornsMenuJoinStage stage )
	{
		_ = stage;
		_progressOverlay?.Refresh();
	}

	public void EnsureBuilt()
	{
		if ( _uiBuilt || _building || _buildFailed )
			return;

		if ( Panel is null || !Panel.IsValid )
			return;

		_building = true;
		try
		{
			ThornsMenuNews.Reload();
			BuildShell();
			_uiBuilt = true;
			Log.Info( "[Thorns Menu] Main menu UI built." );
		}
		catch ( Exception e )
		{
			_buildFailed = true;
			Log.Error( e, $"[Thorns Menu] Main menu UI build failed: {e.Message}" );
		}
		finally
		{
			_building = false;
		}
	}

	public void ShowRootView()
	{
		if ( !_uiBuilt )
			return;

		ShowView( MainMenuView.Root );
	}

	void BuildShell()
	{
		Log.Info( "[Thorns Menu] Building main menu UI." );

		_browser = null;
		_settings = null;
		_credits = null;

		Panel.DeleteChildren();
		Panel.AddClass( "mainmenu-root" );
		ThornsUiSkin.ApplyRoot( Panel );

		var shell = ThornsUiFactory.AddPanel( Panel, "mainmenu-shell" );
		shell.Style.Position = PositionMode.Relative;

		_worldSlot = ThornsUiFactory.AddPanel( shell, "mainmenu-world-slot" );
		ThornsMainMenuBackdrop.ApplyToPanel( _worldSlot, ThornsMainMenuBackdrop.DefaultPath );

		_leftRail = ThornsUiFactory.AddPanel( shell, "mainmenu-left-rail mainmenu-left-rail-overlay" );
		ThornsUiFactory.AddLabel( _leftRail, "THORNS", "mainmenu-brand" );
		ThornsUiFactory.AddLabel( _leftRail, "SURVIVE TOGETHER", "mainmenu-tagline" );

		_rootNav = ThornsUiFactory.AddPanel( _leftRail, "mainmenu-nav" );
		AddDisabledNavItem( "HOST A WORLD TO PLAY" );
		AddNavButton( "HOST / JOIN", false, OnServersClicked );
		AddNavButton( "SETTINGS", false, OnSettingsClicked );
		AddNavButton( "CREDITS", false, OnCreditsClicked );
		AddNavButton( "QUIT", false, OnQuitClicked );

		var overlay = ThornsUiFactory.AddPanel( shell, "mainmenu-overlay-layer" );

		_topRight = ThornsUiFactory.AddPanel( overlay, "mainmenu-top-right" );
		_news = new NewsPanel( _topRight );

		_screens = ThornsUiFactory.AddPanel( overlay, "mainmenu-screens" );

		_progressOverlay = new MainMenuProgressOverlay( Panel );
		_progressOverlay.Refresh();

		_worldNamePrompt = new MainMenuWorldNamePrompt( Panel );
		_confirmPrompt = new MainMenuConfirmPrompt( Panel );
	}

	void EnsureBrowserScreen()
	{
		if ( TryResolveBrowserScreen( out _browser ) )
			return;

		PurgeOrphanBrowserScreens( null );

		_browser = new ServerBrowserScreen( _screens, _worldNamePrompt, _confirmPrompt );
		_browser.SetClass( "mainmenu-hidden", true );
		_browser.MenuBackPressed -= OnBrowserBack;
		_browser.MenuBackPressed += OnBrowserBack;
	}

	bool TryResolveBrowserScreen( out ServerBrowserScreen browser )
	{
		browser = null;

		if ( _browser is not null && _browser.IsValid )
		{
			browser = _browser;
			return true;
		}

		if ( _screens is null || !_screens.IsValid )
			return false;

		foreach ( var child in _screens.Children )
		{
			if ( child is not ServerBrowserScreen existing || !existing.IsValid )
				continue;

			browser = existing;
			_browser = existing;
			_browser.MenuBackPressed -= OnBrowserBack;
			_browser.MenuBackPressed += OnBrowserBack;
			PurgeOrphanBrowserScreens( existing );
			return true;
		}

		return false;
	}

	void PurgeOrphanBrowserScreens( ServerBrowserScreen keep )
	{
		if ( _screens is null || !_screens.IsValid )
			return;

		foreach ( var child in _screens.Children.ToArray() )
		{
			if ( child is ServerBrowserScreen browser && browser != keep && browser.IsValid )
				browser.Delete();
		}
	}

	void EnsureSettingsScreen()
	{
		if ( _settings is not null && _settings.IsValid )
			return;

		_settings = new MainMenuSettingsScreen( _screens );
		_settings.SetClass( "mainmenu-hidden", true );
		_settings.MenuBackPressed += OnBrowserBack;
	}

	void EnsureCreditsScreen()
	{
		if ( _credits is not null && _credits.IsValid )
			return;

		_credits = new MainMenuCreditsScreen( _screens );
		_credits.SetClass( "mainmenu-hidden", true );
		_credits.MenuBackPressed += OnBrowserBack;
	}

	void AddNavButton( string text, bool primary, Action onClick )
	{
		ThornsUiFactory.AddClickable( _rootNav, primary ? "mainmenu-nav-btn primary" : "mainmenu-nav-btn", text, onClick );
	}

	void AddDisabledNavItem( string text )
	{
		var item = ThornsUiFactory.AddLabel( _rootNav, text, "mainmenu-nav-btn primary disabled" );
		item.Style.PointerEvents = PointerEvents.None;
	}

	void OnServersClicked()
	{
		if ( _view == MainMenuView.Browser && TryResolveBrowserScreen( out var browser ) )
		{
			browser.SetClass( "mainmenu-hidden", false );
			_ = browser.RefreshAsyncSafe();
			return;
		}

		ShowView( MainMenuView.Browser );
	}
	void OnSettingsClicked() => ShowView( MainMenuView.Settings );
	void OnCreditsClicked() => ShowView( MainMenuView.Credits );
	void OnQuitClicked() => QuitGame();
	void OnBrowserBack()
	{
		_worldNamePrompt?.Hide();
		_confirmPrompt?.Hide();
		ShowView( MainMenuView.Root );
	}

	void ShowView( MainMenuView view )
	{
		_view = view;

		var onRoot = view == MainMenuView.Root;
		_leftRail?.SetClass( "mainmenu-hidden", !onRoot );
		_leftRail?.SetClass( "mainmenu-rail-minimal", false );

		if ( view == MainMenuView.Browser )
		{
			EnsureBrowserScreen();
			_browser?.SetClass( "mainmenu-hidden", false );
		}
		else
			_browser?.SetClass( "mainmenu-hidden", true );

		if ( view == MainMenuView.Settings )
		{
			EnsureSettingsScreen();
			_settings?.SetClass( "mainmenu-hidden", false );
		}
		else
			_settings?.SetClass( "mainmenu-hidden", true );

		if ( view == MainMenuView.Credits )
		{
			EnsureCreditsScreen();
			_credits?.SetClass( "mainmenu-hidden", false );
		}
		else
			_credits?.SetClass( "mainmenu-hidden", true );

		var hideNews = view != MainMenuView.Root;
		if ( _topRight is not null && _topRight.IsValid )
			_topRight.SetClass( "mainmenu-hidden", hideNews );
		if ( _news is not null && _news.IsValid )
			_news.SetClass( "mainmenu-hidden", hideNews );

		if ( view == MainMenuView.Browser && _browser is not null )
			_ = _browser.RefreshAsyncSafe();
	}

	static void QuitGame()
	{
		Log.Info( "[Thorns Menu] Quit requested." );
		ThornsWorldPersistence.FlushBeforeExit();
		Game.Close();
	}

	public static async System.Threading.Tasks.Task LoadGameplaySceneAsync() => await ThornsMenuSceneLoader.LoadGameplayAsync();
}
