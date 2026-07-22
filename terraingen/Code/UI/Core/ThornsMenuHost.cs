namespace Terraingen.UI.Core;

using Sandbox.UI;
using Terraingen;
using Terraingen.Core;
using Terraingen.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI.Hud;
using Terraingen.UI.Components;
using Terraingen.UI.Screens;
using Terraingen.UI.Menu;
using Terraingen.Buildings;
using Terraingen.Clutter;
using Terraingen.Economy;
using Terraingen.Multiplayer;
using Terraingen.UI.Menu.Panels;

/// <summary>Single gameplay screen UI: HUD layer + Tab menu overlay on one panel tree.</summary>
[StyleSheet( "ThornsMenuHost.scss" )]
[Title( "Thorns Menu Host" )]
public sealed class ThornsMenuHost : PanelComponent
{
	public static ThornsMenuHost Instance { get; private set; }
	public static bool IsOpen { get; private set; }

	public static bool IsWorldContainerOpen =>
		ThornsUiClientState.Snapshot.ExternalContainer?.IsOpen == true;

	public static bool IsRadioShopOpen =>
		ThornsUiClientState.Snapshot.RadioShop?.IsOpen == true;

	public static bool IsResearchOpen =>
		ThornsUiClientState.Snapshot.Research?.IsOpen == true;

	public static bool IsCampfireOpen =>
		ThornsUiClientState.Snapshot.Campfire?.IsOpen == true;

	public static bool IsWorkbenchOpen =>
		ThornsUiClientState.Snapshot.Workbench?.IsOpen == true;

	public static bool IsVictoryIntroOpen =>
		Instance?._victoryIntro?.IsOpen == true;

	public static bool IsFirstSessionTutorialOpen =>
		Instance?._firstSessionTutorial?.IsOpen == true;

	public static bool IsJournalTabOpen =>
		IsOpen
		&& string.Equals( Instance?._activeTab, "Journal", StringComparison.OrdinalIgnoreCase );

	static readonly string[] TabOrder =
	{
		"Inventory", "Journal", "Tames", "Skills", "Map", "Guild", "Settings"
	};

	Panel _hudLayer;
	Panel _overlay;
	Panel _body;
	ThornsTabBar _tabBar;
	ThornsScreenBase _activeScreen;

	ThornsVitalsHud _vitals;
	ThornsInteractionHud _interaction;
	ThornsLeftHudColumn _leftHud;
	ThornsRightHudColumn _rightHud;
	ThornsHotbarHud _hotbar;
	ThornsBuildMenuHud _buildMenu;
	ThornsWorldContainerHud _worldContainer;
	ThornsRadioShopHud _radioShop;
	ThornsResearchStationHud _researchStation;
	ThornsCampfireHud _campfireHud;
	ThornsWorkbenchHud _workbenchHud;
	ThornsNotificationHud _notifications;
	ThornsWorldEventHud _worldEvents;
	ThornsJoinAnnouncementHud _joinAnnouncements;
	ThornsLootFeedHud _lootFeed;
	ThornsCrosshairHud _crosshair;
	ThornsSniperScopeHud _sniperScope;
	ThornsDamageFlashHud _damageFlash;
	ThornsUnderwaterOverlayHud _underwaterOverlay;
	ThornsVictoryPathIntroHud _victoryIntro;
	ThornsVitalsCriticalHud _vitalsCritical;
	ThornsLevelUpMomentHud _levelUpMoment;
	ThornsSessionRecapHud _sessionRecap;
	ThornsFirstSessionTutorialHud _firstSessionTutorial;
	MainMenuProgressOverlay _joinProgressOverlay;

	string _activeTab = "Inventory";
	int _tabIndex;
	bool _uiBuilt;
	bool _panelTreeReady;
	bool _hadSnapshot;
	bool _loggedBuild;
	TimeUntil _buildRetryDelay;
	static TimeSince _lastMenuUpdateErrorLog;
	string _deferredOpenTab;
	readonly Dictionary<string, ThornsScreenBase> _tabScreens = new( StringComparer.OrdinalIgnoreCase );

	public bool IsUiBuilt => _uiBuilt;

	protected override void OnAwake()
	{
		ThornsUiClientState.ResetForGameplaySession();
		Instance = this;
		ThornsMenuJoinFlow.StageChanged += OnJoinProgressChanged;
		ForceGameplayState();
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;

		IsOpen = false;
		_deferredOpenTab = null;
		ThornsMenuJoinFlow.StageChanged -= OnJoinProgressChanged;
		ThornsMenuPerformance.SetTabMenuOpen( false );
		ThornsUiManager.Reset( ThornsUiManager.UiContext.Gameplay );
		UiRevisionBus.MenuRevisionChanged -= OnHudRevision;

		if ( Panel is not null && Panel.IsValid )
			ThornsGameplayUiStyles.ForgetPanel( Panel );

		DisposeHudSubscriptions();

		try
		{
			SetPlayerInputBlocked( false );
		}
		catch
		{
			// Scene teardown — player controller may already be destroyed.
		}
	}

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		_panelTreeReady = true;
		ThornsGameplayUiStyles.LoadGameplayRoot( Panel );
		TryBuildUi();
	}

	/// <summary>Rebuild HUD + menu tree after UI skin change (Settings → UI Skin).</summary>
	public void RequestSkinRebuild()
	{
		if ( !_uiBuilt )
			return;

		var wasOpen = IsOpen;
		var tab = _activeTab;

		if ( wasOpen )
			SetOpen( false );

		InvalidateUi();
		ThornsUiSkin.ApplyRoot( Panel );
		TryBuildUi();

		if ( _uiBuilt && wasOpen )
			SetOpen( true, tab );
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor && !Game.IsPlaying )
			return;

		Instance = this;
		ThornsLocalSettings.Load();
		ThornsLocalSettings.Current.UiSkin = "Classic";
		ThornsMenuTabUnlock.ResetSession();
		ThornsCrosshairSettings.Apply( ThornsLocalSettings.Current );
		ThornsUiSkin.ApplyRoot( Panel );
		_activeTab = "Inventory";
		_tabIndex = Math.Max( 0, Array.IndexOf( TabOrder, _activeTab ) );
		TryBuildUi();
		SetOpen( false );
		RefreshHud();
		ThornsGameplayUiDiagnostics.Event( DescribeUiState() );
	}

	public void EnsureUiReady() => TryBuildUi();

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		try
		{
			OnUpdateMenuAndHud();
		}
		catch ( Exception e )
		{
			if ( _lastMenuUpdateErrorLog > 5f )
			{
				_lastMenuUpdateErrorLog = 0f;
				Log.Error( e, "[Thorns UI] MenuHost Update failed — UI will attempt rebuild; restart session if HUD stays broken." );
				InvalidateUi();
			}
		}
	}

	TimeUntil _nextInteractionPoll;
	TimeUntil _nextMenuMapBlipPoll;
	TimeUntil _nextOverlayHudPoll;
	bool _containerHudOpen;
	bool _radioHudOpen;
	bool _researchHudOpen;
	bool _campfireHudOpen;
	bool _workbenchHudOpen;

	void OnUpdateMenuAndHud()
	{
		TryBuildUi();
		TickDeferredOpenTab();
		_joinProgressOverlay?.Tick();
		ApplyJoinLoadingPresentation();

		if ( ThornsUiClientState.HasSnapshot && !_hadSnapshot )
		{
			_hadSnapshot = true;
			RefreshHud();
			RefreshObjectivesHud();
			if ( IsOpen )
				_activeScreen?.Rebuild();
		}

		TickInventoryDragUi();

		if ( !IsOpen )
			_victoryIntro?.Tick();
		_vitals?.TickLowWarningPulse();

		var containerOpen = IsWorldContainerOpen || ThornsPlayerGameplay.Local?.IsAwaitingWorldContainerUi == true;
		var radioOpen = IsRadioShopOpen;
		var researchOpen = IsResearchOpen;
		var campfireOpen = IsCampfireOpen;
		var workbenchOpen = IsWorkbenchOpen;
		if ( containerOpen )
		{
			if ( !_containerHudOpen )
				_worldContainer?.Refresh();

			_containerHudOpen = true;
			SyncOverlayRegistration( "world-container", containerOpen && !IsOpen && _worldContainer?.IsOpen == true,
				ThornsUiPriority.InventoryBuild,
				_worldContainer?.Backdrop,
				() => ThornsPlayerGameplay.Local?.RequestCloseWorldContainer(),
				ThornsUiWindowKind.WorldContainer );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyContainerCursor( true );
			}
		}
		else
		{
			_containerHudOpen = false;
			SyncOverlayRegistration( "world-container", false, ThornsUiPriority.InventoryBuild, null, null, ThornsUiWindowKind.WorldContainer );
		}

		if ( radioOpen )
		{
			if ( !_radioHudOpen || _nextOverlayHudPoll )
			{
				_nextOverlayHudPoll = ThornsHudTickRates.MenuOverlayPollSeconds;
				_radioShop?.Refresh();
			}

			_radioHudOpen = true;
			TickRadioShopProximity();
			SyncOverlayRegistration( "radio-shop", radioOpen && !IsOpen && _radioShop?.IsOpen == true,
				ThornsUiPriority.NpcDialog,
				_radioShop?.Backdrop,
				() => ThornsPlayerGameplay.Local?.RequestCloseRadioShop(),
				ThornsUiWindowKind.RadioShop );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyContainerCursor( true );
			}
		}
		else
		{
			_radioHudOpen = false;
			SyncOverlayRegistration( "radio-shop", false, ThornsUiPriority.NpcDialog, null, null, ThornsUiWindowKind.RadioShop );
		}

		if ( researchOpen )
		{
			if ( !_researchHudOpen || _nextOverlayHudPoll )
			{
				_nextOverlayHudPoll = ThornsHudTickRates.MenuOverlayPollSeconds;
				_researchStation?.Refresh();
			}

			_researchHudOpen = true;
			SyncOverlayRegistration( "research-station", researchOpen && !IsOpen && _researchStation?.IsOpen == true,
				ThornsUiPriority.InventoryBuild,
				_researchStation?.Backdrop,
				() => ThornsPlayerGameplay.Local?.RequestCloseResearchStation(),
				ThornsUiWindowKind.ResearchStation );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyContainerCursor( true );
			}
		}
		else
		{
			_researchHudOpen = false;
			SyncOverlayRegistration( "research-station", false, ThornsUiPriority.InventoryBuild, null, null, ThornsUiWindowKind.ResearchStation );
		}

		if ( campfireOpen )
		{
			if ( !_campfireHudOpen || _nextOverlayHudPoll )
			{
				_nextOverlayHudPoll = ThornsHudTickRates.MenuOverlayPollSeconds;
				_campfireHud?.Refresh();
			}

			_campfireHudOpen = true;
			SyncOverlayRegistration( "campfire", campfireOpen && !IsOpen && _campfireHud?.IsOpen == true,
				ThornsUiPriority.InventoryBuild,
				_campfireHud?.Backdrop,
				() => ThornsPlayerGameplay.Local?.RequestCloseCampfire(),
				ThornsUiWindowKind.Campfire );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyContainerCursor( true );
			}
		}
		else
		{
			_campfireHudOpen = false;
			SyncOverlayRegistration( "campfire", false, ThornsUiPriority.InventoryBuild, null, null, ThornsUiWindowKind.Campfire );
		}

		if ( workbenchOpen )
		{
			if ( !_workbenchHudOpen || _nextOverlayHudPoll )
			{
				_nextOverlayHudPoll = ThornsHudTickRates.MenuOverlayPollSeconds;
				_workbenchHud?.Refresh();
			}

			_workbenchHudOpen = true;
			SyncOverlayRegistration( "workbench", workbenchOpen && !IsOpen && _workbenchHud?.IsOpen == true,
				ThornsUiPriority.InventoryBuild,
				_workbenchHud?.Backdrop,
				() => ThornsPlayerGameplay.Local?.RequestCloseWorkbench(),
				ThornsUiWindowKind.Workbench );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyContainerCursor( true );
			}
		}
		else
		{
			_workbenchHudOpen = false;
			SyncOverlayRegistration( "workbench", false, ThornsUiPriority.InventoryBuild, null, null, ThornsUiWindowKind.Workbench );
		}

		var victoryIntroOpen = _victoryIntro?.IsOpen == true;
		if ( victoryIntroOpen )
		{
			SyncOverlayRegistration( "victory-intro", victoryIntroOpen && !IsOpen && _victoryIntro?.IsOpen == true,
				ThornsUiPriority.CriticalPopup,
				_victoryIntro?.Backdrop,
				() => _victoryIntro?.Dismiss(),
				ThornsUiWindowKind.VictoryIntro );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyMenuCursor( true );
				ThornsMenuPerformance.SetVictoryIntroOverlayOpen( true );
			}
		}
		else
		{
			SyncOverlayRegistration( "victory-intro", false, ThornsUiPriority.CriticalPopup, null, null, ThornsUiWindowKind.VictoryIntro );
			if ( !IsOpen )
				ThornsMenuPerformance.SetVictoryIntroOverlayOpen( false );
		}

		var tutorialOpen = _firstSessionTutorial?.IsOpen == true;
		if ( tutorialOpen )
		{
			SyncOverlayRegistration( "first-session-tutorial", tutorialOpen && !IsOpen && _firstSessionTutorial?.IsOpen == true,
				ThornsUiPriority.CriticalPopup,
				_firstSessionTutorial?.Backdrop,
				() => _firstSessionTutorial?.DismissStep(),
				ThornsUiWindowKind.FirstSessionTutorial );
			if ( !IsOpen )
			{
				SetPlayerInputBlocked( true );
				ApplyMenuCursor( true );
			}
		}
		else
		{
			SyncOverlayRegistration( "first-session-tutorial", false, ThornsUiPriority.CriticalPopup, null, null, ThornsUiWindowKind.FirstSessionTutorial );
		}

		ThornsUiManager.ApplyFocusDimming( _hudLayer );
		ThornsTooltip.Tick();

		if ( !containerOpen && !radioOpen && !researchOpen && !campfireOpen && !workbenchOpen && !victoryIntroOpen && !tutorialOpen && !IsOpen && ThornsPlayerGameplay.Local?.IsAwaitingWorldContainerUi != true )
		{
			SetPlayerInputBlocked( false );
		}

		ThornsUiCursor.SyncForActiveContext();

		_crosshair?.SetVisible( ShouldShowGameplayCrosshair( containerOpen, radioOpen, researchOpen, campfireOpen, workbenchOpen, victoryIntroOpen, tutorialOpen ) );
		_sniperScope?.Refresh();

		if ( (victoryIntroOpen || tutorialOpen) && !IsOpen )
		{
			SetPlayerInputBlocked( true );
			ApplyMenuCursor( true );
		}

		if ( !IsOpen )
		{
			if ( ThornsUiInputGate.AllowsHudTick )
			{
				SyncBuildMenuRegistration();

				if ( ThornsPlayerBuildingController.Local?.BuildMenuOpen == true )
					_buildMenu?.Refresh();

				ThornsJournalPinAlert.Tick( Time.Delta );
				UpdateObjectivesPinAlert();
				ThornsDamageFlashState.Tick();
				_damageFlash?.Refresh();
				ThornsUnderwaterViewState.Tick( Scene, Time.Delta );
				_underwaterOverlay?.Refresh();
				_rightHud?.RefreshMinimapBlip();
				_leftHud?.UpdateDayTime();
			}
		}
		else if ( _activeTab == "Map" && _activeScreen is ThornsMapScreen mapScreen && _nextMenuMapBlipPoll )
		{
			_nextMenuMapBlipPoll = ThornsHudTickRates.MenuMapBlipSeconds;
			mapScreen.RefreshBlip();
		}

		ThornsTameFeedNoticeBus.Tick( Time.Delta );

		// Modal session recap must tick even when input context is Modal (auto-dismiss / Continue).
		if ( !IsOpen )
			_sessionRecap?.Tick();

		if ( !IsOpen && ThornsUiInputGate.AllowsHudTick )
		{
			_firstSessionTutorial?.TryHandleDismissInput();
			_firstSessionTutorial?.Tick( Time.Delta );
			ThornsNotificationBus.Tick( Time.Delta );
			ThornsJoinAnnouncementBus.Tick( Time.Delta );
			ThornsWorldEventHudBus.Tick( Time.Delta );
			ThornsLootFeedBus.Tick( Time.Delta );
			_levelUpMoment?.Tick();
			_vitalsCritical?.Refresh();
			var pendingLevel = ThornsLevelUpMomentBus.ConsumePendingLevel();
			if ( pendingLevel > 0 )
				_levelUpMoment?.Show( pendingLevel );
			if ( _nextInteractionPoll )
			{
				_nextInteractionPoll = ThornsHudTickRates.InteractionPromptSeconds;
				_interaction?.Refresh();
			}
			_crosshair?.ApplyScale();
			ThornsHitmarkerState.Tick();
			_crosshair?.RefreshHitFlash();
			_sniperScope?.Refresh();

		}
	}

	bool ShouldShowGameplayCrosshair( bool containerOpen, bool radioOpen, bool researchOpen, bool campfireOpen, bool workbenchOpen, bool victoryIntroOpen, bool tutorialOpen ) =>
		!IsOpen
		&& !containerOpen
		&& !radioOpen
		&& !researchOpen
		&& !campfireOpen
		&& !workbenchOpen
		&& !victoryIntroOpen
		&& !tutorialOpen
		&& !ThornsSniperScopeHudState.HideStandardCrosshair
		&& !ThornsSniperScopeHudState.ShowClassicScope;

	void TickInventoryDragUi()
	{
		if ( !ThornsDragState.IsDragging )
		{
			ThornsInventoryDragGhost.Hide();
			return;
		}

		var (basis, coord) = ResolveDragGhostPanels();
		ThornsInventoryDragGhost.EnsureHost( basis, coord );

		ThornsDragState.UpdatePointer();
		ThornsInventoryDragGhost.Tick();

		if ( !IsOpen && !IsWorldContainerOpen && !IsCampfireOpen && !IsWorkbenchOpen && !IsRadioShopOpen && !IsResearchOpen && !IsVictoryIntroOpen && !IsFirstSessionTutorialOpen )
			return;

		ThornsItemSlot.RefreshDropTarget();
		ThornsAttachmentInspectSlot.RefreshDropTarget();

		if ( Input.Pressed( "Attack1" ) || Input.Pressed( "attack1" ) )
		{
			ThornsAttachmentInspectSlot.RefreshDropTarget();
			if ( !ThornsAttachmentInspectSlot.TryCompleteHoveredDrop() )
			{
				ThornsItemSlot.RefreshDropTarget();
				if ( !ThornsItemSlot.TryCompleteHoveredDrop() )
					ThornsDragState.Clear();
			}

			ThornsItemSlot.ClearDropTarget();
			ThornsAttachmentInspectSlot.RefreshDropTarget();
			return;
		}
	}

	/// <summary>Called from <see cref="ThornsGameplayUiHost"/> — PanelComponent does not receive game Input reliably.</summary>
	public void HandleGameplayInput()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !_uiBuilt )
		{
			if ( Input.Pressed( "Tab" ) || Input.Pressed( "InventoryMenu" ) )
				ThornsGameplayUiDiagnostics.Warn( "Tab ignored — gameplay UI not built yet (Panel null or BuildUi pending)." );

			return;
		}

		ThornsKeybindService.TickCapture();

		if ( !ThornsKeybindService.IsListening && ThornsKeybindService.Pressed( "ToggleHud" ) )
		{
			ThornsUiManager.ManualHudHidden = !ThornsUiManager.ManualHudHidden;
			ThornsUiManager.ApplyFocusDimming( _hudLayer );
			return;
		}

		// Escape stack — topmost window closes first.
		if ( Input.Pressed( "Menu" ) || Input.Pressed( "Cancel" ) )
		{
			if ( ThornsUiManager.TryHandleCancel( ThornsUiManager.UiContext.Gameplay ) )
				return;
		}

		if ( ThornsKeybindService.Pressed( "Tab" ) )
		{
			if ( !ThornsUiGameplayState.AllowsInventoryAccess )
				return;

			if ( IsWorldContainerOpen )
			{
				ThornsPlayerGameplay.Local?.RequestCloseWorldContainer();
				return;
			}

			if ( IsRadioShopOpen )
			{
				ThornsPlayerGameplay.Local?.RequestCloseRadioShop();
				return;
			}

			if ( IsResearchOpen )
			{
				ThornsPlayerGameplay.Local?.RequestCloseResearchStation();
				return;
			}

			if ( IsCampfireOpen )
			{
				ThornsPlayerGameplay.Local?.RequestCloseCampfire();
				return;
			}

			if ( IsWorkbenchOpen )
			{
				ThornsPlayerGameplay.Local?.RequestCloseWorkbench();
				return;
			}

			ThornsGameplayUiDiagnostics.OnTabInput( this, _uiBuilt );
			try
			{
				SetOpen( !IsOpen );
			}
			catch ( Exception e )
			{
				Log.Error( e, "[Thorns UI] Tab toggle failed — menu closed to recover." );
				ForceCloseMenuSafe();
			}
			return;
		}

		if ( ThornsUiGameplayState.AllowsInventoryAccess )
		{
			if ( ThornsKeybindService.Pressed( "InventoryMenu" ) )
			{
				ToggleMenuTab( "Inventory", "inventory" );
				return;
			}

			if ( ThornsKeybindService.Pressed( "JournalMenu" ) )
			{
				ToggleMenuTab( "Journal", "journal" );
				return;
			}

			if ( ThornsKeybindService.Pressed( "MapMenu" ) )
			{
				ToggleMenuTab( "Map", "map" );
				return;
			}

			if ( ThornsKeybindService.Pressed( "SkillsMenu" ) )
			{
				ToggleMenuTab( "Skills", "skills" );
				return;
			}

			if ( ThornsKeybindService.Pressed( "GuildMenu" ) )
			{
				ToggleMenuTab( "Guild", null );
				return;
			}
		}

		if ( Input.Pressed( "Menu" ) && IsOpen )
			SetOpen( false );

		if ( IsWorldContainerOpen || IsRadioShopOpen || IsResearchOpen || IsVictoryIntroOpen )
			return;

		if ( !IsOpen )
			return;

		if ( Input.Pressed( "SlotPrev" ) )
			NavigateTab( -1 );
		if ( Input.Pressed( "SlotNext" ) )
			NavigateTab( 1 );
	}

	public void ToggleOpen() => SetOpen( !IsOpen );

	public void SetOpen( bool open, string openingTabId = null )
	{
		try
		{
			SetOpenCore( open, openingTabId );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns UI] SetOpen({open}) failed — forcing menu closed." );
			ForceCloseMenuSafe();
		}
	}

	void SetOpenCore( bool open, string openingTabId = null )
	{
		IsOpen = open;
		ThornsMenuPerformance.SetTabMenuOpen( open );

		if ( open )
		{
			if ( !ThornsUiGameplayState.AllowsInventoryAccess )
				return;

			if ( !ThornsUiManager.IsOpen( "tab-menu" ) )
				SyncOverlayRegistration( "tab-menu", true, ThornsUiPriority.FullscreenMenu, _overlay,
					() => SetOpen( false ), ThornsUiWindowKind.TabMenu, isModal: true );
		}
		else
		{
			_deferredOpenTab = null;
			SyncOverlayRegistration( "tab-menu", false, ThornsUiPriority.FullscreenMenu, null, null, ThornsUiWindowKind.TabMenu );
			ThornsLocalSettings.Save();
		}

		var overlayOk = _overlay is not null && _overlay.IsValid;
		if ( !_uiBuilt || !overlayOk )
		{
			if ( open )
				IsOpen = false;

			ThornsGameplayUiDiagnostics.OnSetOpen( open, _uiBuilt, overlayOk );
			if ( !open )
				SetPlayerInputBlocked( false );

			return;
		}

		ApplyOverlayVisibility( open );
		ThornsGameplayUiDiagnostics.OnSetOpen( open, _uiBuilt, overlayOk );

		_hotbar?.Refresh();

		if ( open )
		{
			ThornsPlayerGameplay.Local?.RequestCloseWorldContainer();
			ThornsPlayerGameplay.Local?.RequestCloseRadioShop();
			ThornsPlayerGameplay.Local?.RequestCloseResearchStation();
			ThornsPlayerGameplay.Local?.RequestCloseCampfire();
			ThornsPlayerGameplay.Local?.RequestCloseWorkbench();
			SetPlayerInputBlocked( true );
			var tabId = string.IsNullOrWhiteSpace( openingTabId )
				? ResolveInitialTab( _activeTab )
				: openingTabId;

			if ( _body is not null && _body.IsValid )
				ShowTab( tabId );
			else
				_deferredOpenTab = tabId;

			ThornsPlayerGameplay.Local?.RefreshMenuSnapshot();
		}
		else
		{
			SetPlayerInputBlocked( false );
		}

		ApplyMenuCursor( open );
		_crosshair?.SetVisible( !open );
		if ( !open )
			_crosshair?.RefreshHitFlash();
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	void TickDeferredOpenTab()
	{
		if ( string.IsNullOrWhiteSpace( _deferredOpenTab ) || !IsOpen || !_uiBuilt )
			return;

		if ( _body is null || !_body.IsValid )
			return;

		var tabId = _deferredOpenTab;
		_deferredOpenTab = null;

		try
		{
			ShowTab( tabId );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns UI] Deferred ShowTab failed for '{tabId}'." );
			_activeScreen?.DisposeSubscriptions();
			_activeScreen = null;
		}
	}

	void ForceCloseMenuSafe()
	{
		_deferredOpenTab = null;
		IsOpen = false;
		ThornsMenuPerformance.SetTabMenuOpen( false );
		try
		{
			SyncOverlayRegistration( "tab-menu", false, ThornsUiPriority.FullscreenMenu, null, null, ThornsUiWindowKind.TabMenu );
			ApplyOverlayVisibility( false );
			SetPlayerInputBlocked( false );
			ApplyMenuCursor( false );
			_crosshair?.SetVisible( true );
		}
		catch
		{
			// Best-effort recovery during teardown.
		}
	}

	static void ReclaimGameplayCameraAfterMenu()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
		if ( !player.IsValid() )
			return;

		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( scene, player );
	}

	static void ApplyMenuCursor( bool menuOpen ) => ThornsUiCursor.ApplyGameplayMenuOpen( menuOpen );

	static void ApplyContainerCursor( bool containerOpen )
	{
		if ( IsOpen )
			return;

		ThornsUiCursor.ApplyGameplayMenuOpen( containerOpen );
	}

	void TickRadioShopProximity()
	{
		if ( !IsRadioShopOpen )
			return;

		var gameplay = ThornsPlayerGameplay.Local;
		if ( !gameplay.IsValid() )
			return;

		if ( Networking.IsActive && Networking.IsHost )
			gameplay.HostTickRadioShopProximity();

		var snap = ThornsUiClientState.Snapshot.RadioShop;
		if ( !Guid.TryParse( snap?.StationId, out var stationId ) || stationId == Guid.Empty )
		{
			gameplay.RequestCloseRadioShop();
			return;
		}

		if ( !ThornsRadioStation.ActiveById.TryGetValue( stationId, out var station )
		     || !station.IsValid()
		     || !station.HostIsInRange( gameplay.GameObject.WorldPosition ) )
			gameplay.RequestCloseRadioShop();
	}

	public static void ForceGameplayState()
	{
		if ( Instance is not null && Instance.IsValid() )
		{
			try
			{
				Instance.SetOpen( false );
			}
			catch ( Exception e )
			{
				Log.Warning( e, "[Thorns UI] ForceGameplayState SetOpen failed." );
				IsOpen = false;
				ThornsMenuPerformance.SetTabMenuOpen( false );
				ApplyMenuCursor( false );
			}
		}
		else
		{
			IsOpen = false;
			ThornsMenuPerformance.SetTabMenuOpen( false );
			ThornsUiCursor.ApplyGameplayMenuOpen( false );
		}
	}

	void ApplyOverlayVisibility( bool open )
	{
		if ( Panel.IsValid )
			Panel.SetClass( "menu-open", open );

		if ( _overlay is null || !_overlay.IsValid )
			return;

		_overlay.SetClass( "thorns-hidden", !open );
		_overlay.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		_overlay.Style.PointerEvents = open ? PointerEvents.All : PointerEvents.None;
		_overlay.Style.Opacity = open ? 1f : 0f;
	}

	void SetPlayerInputBlocked( bool blocked )
	{
		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() )
			return;

		ThornsPlayerLocomotion.SetOverlayInputBlocked( player.GameObject, blocked );
	}

	void NavigateTab( int delta )
	{
		var unlocked = GetVisibleTabOrder();
		if ( unlocked.Count == 0 )
			return;

		var current = unlocked.FindIndex( t => string.Equals( t, _activeTab, StringComparison.OrdinalIgnoreCase ) );
		if ( current < 0 )
			current = 0;

		current = (current + delta + unlocked.Count) % unlocked.Count;
		ShowTab( unlocked[current] );
	}

	static string ResolveInitialTab( string preferred )
	{
		var unlocked = GetVisibleTabOrder();
		if ( unlocked.Count == 0 )
			return "Inventory";

		if ( unlocked.Any( t => string.Equals( t, preferred, StringComparison.OrdinalIgnoreCase ) ) )
			return preferred;

		return unlocked[0];
	}

	static List<string> GetVisibleTabOrder()
	{
		var list = new List<string>();
		foreach ( var tab in TabOrder )
		{
			if ( ThornsMenuTabUnlock.IsTabUnlocked( tab ) )
				list.Add( tab );
		}

		return list;
	}

	public void RefreshTabBar()
	{
		var tabs = GetVisibleTabOrder();
		_tabBar?.BuildTabs( tabs );
		_tabBar?.SetActive( ResolveInitialTab( _activeTab ) );
	}

	public void ShowSessionRecap( string nextGoalTitle ) =>
		_sessionRecap?.Show( nextGoalTitle );

	void OpenMenuTab( string tabId, string controlTaskId )
	{
		SetOpen( true, tabId );
		if ( !string.IsNullOrWhiteSpace( controlTaskId ) )
			ThornsPlayerGameplay.Local?.RequestCompleteJournalTask( "goal_explore_controls", controlTaskId );
	}

	void ToggleMenuTab( string tabId, string controlTaskId )
	{
		if ( IsOpen && string.Equals( _activeTab, tabId, StringComparison.OrdinalIgnoreCase ) )
		{
			SetOpen( false );
			return;
		}

		OpenMenuTab( tabId, controlTaskId );
	}

	static void NotifyControlGoalTabOpened( string tabId )
	{
		var taskId = tabId switch
		{
			"Inventory" => "inventory",
			"Journal" => "journal",
			"Map" => "map",
			"Skills" => "skills",
			"Settings" => "settings",
			_ => null
		};

		if ( string.IsNullOrWhiteSpace( taskId ) )
			return;

		ThornsPlayerGameplay.Local?.RequestCompleteJournalTask( "goal_explore_controls", taskId );
	}

	public void ShowTab( string tabId )
	{
		if ( _body is null || !_body.IsValid )
		{
			Log.Warning( "[Thorns UI] ShowTab skipped — menu body not ready." );
			return;
		}

		try
		{
			ShowTabCore( tabId );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns UI] ShowTab failed for '{tabId}'." );
			_activeScreen?.DisposeSubscriptions();
			_activeScreen = null;
		}
	}

	void ShowTabCore( string tabId )
	{
		if ( !ThornsMenuTabUnlock.IsTabUnlocked( tabId ) )
		{
			tabId = ResolveInitialTab( "Inventory" );
			ThornsNotificationBus.Push( "That menu tab is not unlocked yet.", "info", 2.5f );
		}

		NotifyControlGoalTabOpened( tabId );

		_activeTab = tabId;
		var unlocked = GetVisibleTabOrder();
		_tabIndex = Math.Max( 0, unlocked.FindIndex( t => string.Equals( t, tabId, StringComparison.OrdinalIgnoreCase ) ) );
		ThornsLocalSettings.Current.LastMenuTab = tabId;
		RefreshTabBar();

		HideAllTabScreens();

		try
		{
			if ( string.Equals( tabId, "Guild", StringComparison.OrdinalIgnoreCase ) )
				ThornsPlayerGameplay.Local?.EnsurePersonalGuildForMenu();

			var isNew = !_tabScreens.TryGetValue( tabId, out _activeScreen ) || _activeScreen is null || !_activeScreen.IsValid;
			if ( isNew )
			{
				_activeScreen = CreateScreenForTab( tabId, this, _body );
				_tabScreens[tabId] = _activeScreen;
			}
			else
			{
				_activeScreen.SetTabVisible( true );
			}

			if ( ThornsUiClientState.HasSnapshot )
				_activeScreen?.OnShown( isNew );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"[Thorns UI] Tab screen open failed for '{tabId}'." );
			if ( _tabScreens.TryGetValue( tabId, out var failed ) && failed == _activeScreen )
				_tabScreens.Remove( tabId );
			_activeScreen?.DisposeSubscriptions();
			_activeScreen = null;
		}
	}

	static ThornsScreenBase CreateScreenForTab( string tabId, ThornsMenuHost host, Panel body )
	{
		if ( host is null || body is null || !body.IsValid )
			return null;

		return tabId switch
		{
			"Inventory" => new ThornsInventoryScreen( host, body ),
			"Journal" => new ThornsJournalScreen( host, body ),
			"Tames" => new ThornsTamesScreen( host, body ),
			"Skills" => new ThornsSkillsScreen( host, body ),
			"Map" => new ThornsMapScreen( host, body ),
			"Guild" => new ThornsGuildScreen( host, body ),
			"Settings" => new ThornsSettingsScreen( host, body ),
			_ => null
		};
	}

	void HideAllTabScreens()
	{
		foreach ( var screen in _tabScreens.Values )
			screen?.SetTabVisible( false );
	}

	void DisposeTabScreens()
	{
		foreach ( var screen in _tabScreens.Values )
			screen?.DisposeSubscriptions();

		_tabScreens.Clear();
	}

	public void ApplyUiScale()
	{
		if ( Panel is null || !Panel.IsValid )
			return;

		var scale = ThornsLocalSettings.Current.UiScale;
		Panel.Style.FontSize = Length.Pixels( 14f * scale );
	}

	public void RefreshHud()
	{
		if ( ThornsUiClientState.HasSnapshot )
			ThornsUiClientState.EnsureSnapshotCoherentForRefresh();

		// Each widget is refreshed in isolation: on the first frame after world
		// load some snapshots/fields can still be null, and we don't want a single
		// widget's NRE to abort the whole HUD refresh. The named log pinpoints the
		// offender without taking the rest of the HUD down with it.
		SafeRefresh( "vitals", () => _vitals?.Refresh() );
		SafeRefresh( "hotbar", () => _hotbar?.Refresh() );
		SafeRefresh( "buildMenu", () => _buildMenu?.Refresh() );
		SafeRefresh( "worldContainer", () => _worldContainer?.Refresh() );
		SafeRefresh( "radioShop", () => _radioShop?.Refresh() );
		SafeRefresh( "researchStation", () => _researchStation?.Refresh() );
		SafeRefresh( "notifications", () => _notifications?.Refresh() );
		SafeRefresh( "joinAnnouncements", () => _joinAnnouncements?.Refresh() );
		SafeRefresh( "lootFeed", () => _lootFeed?.Refresh() );
		SafeRefresh( "vitalsCritical", () => _vitalsCritical?.Refresh() );
		SafeRefresh( "minimap", () => _rightHud?.RefreshMinimap( force: true ) );
		SafeRefresh( "dayTime", () => _leftHud?.UpdateDayTime() );
		SafeRefresh( "objectives", RefreshObjectivesHud );
		SafeRefresh( "worldEvents", () => _worldEvents?.Refresh() );
		SafeRefresh( "interaction", () => _interaction?.Refresh() );
	}

	static void SafeRefresh( string widget, System.Action refresh )
	{
		try
		{
			refresh();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns UI] HUD widget '{widget}' refresh skipped ({ex.GetType().Name}: {ex.Message})." );
		}
	}

	void RefreshObjectivesHud()
	{
		_leftHud?.RefreshObjectives();
		_rightHud?.RefreshObjectives();
	}

	void UpdateObjectivesPinAlert()
	{
		_leftHud?.UpdatePinAlert();
		_rightHud?.Objectives?.UpdatePinAlert();
	}

	void OnHudRevision( UiRevisionChannel channel, int revision )
	{
		_ = revision;
		if ( channel is UiRevisionChannel.Notifications )
			_notifications?.Refresh();

		if ( channel is UiRevisionChannel.Vitals or UiRevisionChannel.Inventory or UiRevisionChannel.Hotbar )
		{
			_vitals?.Refresh();
			_hotbar?.Refresh();
		}

		if ( channel is UiRevisionChannel.Inventory or UiRevisionChannel.Hotbar or UiRevisionChannel.BuildMenu )
			_buildMenu?.Refresh();

		if ( channel == UiRevisionChannel.Interaction )
			_interaction?.Refresh();

		if ( channel == UiRevisionChannel.LootFeed )
			_lootFeed?.Refresh();

		if ( channel is UiRevisionChannel.Journal )
		{
			RefreshObjectivesHud();
			RefreshTabBar();
		}

		if ( channel is UiRevisionChannel.Victory or UiRevisionChannel.Guild )
			RefreshObjectivesHud();

		if ( channel is UiRevisionChannel.Map )
			_rightHud?.RefreshMinimap( force: true );

		if ( channel is UiRevisionChannel.WorldContainer )
			_worldContainer?.Refresh();

		if ( channel is UiRevisionChannel.RadioShop )
			_radioShop?.Refresh();

		if ( channel is UiRevisionChannel.Research )
			_researchStation?.Refresh();

		if ( channel is UiRevisionChannel.Campfire )
			_campfireHud?.Refresh();

		if ( channel is UiRevisionChannel.Workbench )
			_workbenchHud?.Refresh();

		if ( channel is UiRevisionChannel.Milestones or UiRevisionChannel.WorldEvents )
			_worldEvents?.Refresh();

		if ( channel is UiRevisionChannel.Skills )
			_hotbar?.Refresh();

		if ( IsOpen && _activeScreen is not null )
		{
			var rebuild = channel switch
			{
				UiRevisionChannel.Inventory or UiRevisionChannel.Craft => _activeTab == "Inventory",
				UiRevisionChannel.Journal => _activeTab == "Journal",
				UiRevisionChannel.Tames => _activeTab == "Tames",
				UiRevisionChannel.Skills => _activeTab == "Skills",
				UiRevisionChannel.Map => _activeTab == "Map",
				UiRevisionChannel.Guild or UiRevisionChannel.Victory => _activeTab == "Guild",
				_ => false
			};

			if ( rebuild )
				_activeScreen.Rebuild();
		}
	}

	public void SetCraftPanelExpanded( bool expanded ) =>
		ThornsPlayerGameplay.Local?.SetCraftUiState( expanded, null, null );

	public void OpenCraftStation( ThornsCraftStationKind station )
	{
		var gameplay = ThornsPlayerGameplay.Local;
		if ( gameplay is null || !gameplay.IsValid() )
			return;

		gameplay.SetNearestStation( station );
		gameplay.SetCraftUiState(
			true,
			ThornsCraftCatalog.AllCraftCategoryId,
			null );
		SetOpen( true, "Inventory" );
	}

	public void SetJournalSection( ThornsJournalSection section ) =>
		ThornsPlayerGameplay.Local?.SetJournalUiState( section, null );

	public void SetVictoryPath( string pathId ) =>
		ThornsPlayerGameplay.Local?.SetVictoryUiState( pathId );

	public void SetSelectedGoal( string goalId ) =>
		ThornsPlayerGameplay.Local?.SetJournalUiState( null, goalId );

	public void PinGoalToHud( string goalId ) =>
		ThornsPlayerGameplay.Local?.PinGoalToHud( goalId );

	public void SetSelectedDiscovery( string discoveryId ) =>
		ThornsPlayerGameplay.Local?.SetJournalUiState( null, null, discoveryId );

	public void SetSelectedTame( Guid entityId )
	{
		ThornsUiClientState.Snapshot.Tames.SelectedEntityId = entityId;
		UiRevisionBus.Publish( UiRevisionChannel.Tames );
	}

	public void SetSkillCategory( ThornsSkillCategory category ) =>
		ThornsPlayerGameplay.Local?.SetSkillsUiState( category, null );

	public void SetSelectedSkill( string skillId ) =>
		ThornsPlayerGameplay.Local?.SetSkillsUiState( null, skillId );

	bool IsUiHealthy() =>
		_uiBuilt
		&& Panel is not null && Panel.IsValid
		&& _hudLayer is not null && _hudLayer.IsValid
		&& _hotbar is not null;

	void InvalidateUi( bool force = false )
	{
		if ( !_uiBuilt && !force )
			return;

		UiRevisionBus.MenuRevisionChanged -= OnHudRevision;
		DisposeTabScreens();
		_activeScreen?.DisposeSubscriptions();
		DisposeHudSubscriptions();
		_deferredOpenTab = null;
		_uiBuilt = false;
		_hudLayer = null;
		_overlay = null;
		_body = null;
		_tabBar = null;
		_activeScreen = null;
		_vitals = null;
		_interaction = null;
		_leftHud = null;
		_rightHud = null;
		_hotbar = null;
		_buildMenu = null;
		_worldContainer = null;
		_radioShop = null;
		_researchStation = null;
		_campfireHud = null;
		_workbenchHud = null;
		_notifications = null;
		_worldEvents = null;
		_joinAnnouncements = null;
		_lootFeed = null;
		_crosshair = null;
		_sniperScope = null;
		ThornsJournalPinAlert.Cancel();
	}

	void DisposeHudSubscriptions()
	{
		_rightHud?.Dispose();
		_leftHud = null;
		_worldContainer?.Dispose();
		_radioShop?.Dispose();
		_researchStation?.Dispose();
		_campfireHud?.Dispose();
		_workbenchHud?.Dispose();
		_notifications?.Dispose();
		_worldEvents?.Dispose();
		_joinAnnouncements?.Dispose();
		_lootFeed?.Dispose();
	}

	void TryBuildUi()
	{
		if ( !_panelTreeReady )
		{
			ThornsJoinFlowDebug.JoinInfo( "TryBuildUi waiting — panel tree not ready." );
			return;
		}

		if ( !ThornsGameplayUiStyles.IsGameplayRootReady( Panel ) )
		{
			ThornsJoinFlowDebug.JoinInfo( "TryBuildUi loading gameplay root stylesheet." );
			ThornsGameplayUiStyles.LoadGameplayRoot( Panel );
			return;
		}

		if ( _uiBuilt && !IsUiHealthy() )
			InvalidateUi();

		if ( _uiBuilt )
			return;

		if ( _buildRetryDelay > 0f )
			return;

		if ( Panel is null || !Panel.IsValid )
		{
			Log.Warning( "[Thorns UI] Gameplay UI waiting for ScreenPanel…" );
			return;
		}

		try
		{
			BuildUi();
			_uiBuilt = true;
			ThornsJoinFlowDebug.LogMilestone( $"TryBuildUi complete — {ThornsJoinFlowDebug.DescribeHud()}" );
			_buildRetryDelay = 0f;
			if ( Instance is null || !Instance.IsValid() )
				Instance = this;

			try
			{
				RefreshHud();
			}
			catch ( Exception refreshEx )
			{
				Log.Error( refreshEx, $"[Thorns UI] RefreshHud failed ({refreshEx.GetType().Name}: {refreshEx.Message}) — menu remains usable." );
			}

			_ = DeferredWarmGameplayIconsAsync();
			ThornsIconCache.WarmInventoryUiIcons();
			ThornsMenuChrome.WarmClassicTextures();
			ThornsMainMenuBackdrop.WarmTabMenuBackdrop();

			if ( !_loggedBuild )
			{
				_loggedBuild = true;
				ThornsGameplayUiDiagnostics.Event( $"BuildUi complete {DescribeUiState()}" );
				if ( Panel.IsValid )
					ThornsGameplayUiDiagnostics.Event( ThornsGameplayUiDiagnostics.DescribePanel( Panel, "root" ) );
			}
		}
		catch ( Exception e )
		{
			_uiBuilt = false;
			_buildRetryDelay = 0.5f;
			InvalidateUi( force: true );
			if ( Panel is { IsValid: true } )
				Panel.DeleteChildren( true );

			Log.Error( e, $"[Thorns UI] TryBuildUi failed ({e.GetType().Name}: {e.Message}) — HUD will retry on next EnsureUiReady." );
			ThornsJoinFlowDebug.JoinWarn( $"TryBuildUi failed — {e.GetType().Name}: {e.Message}" );
		}
	}

	static async System.Threading.Tasks.Task DeferredWarmGameplayIconsAsync()
	{
		for ( var attempt = 0; attempt < 60; attempt++ )
		{
			await System.Threading.Tasks.Task.Yield();
			if ( attempt > 0 )
				await System.Threading.Tasks.Task.Yield();

			try
			{
				ThornsIconCache.WarmGameplayIcons();
				if ( ThornsIconCache.IsGameplayIconsWarmed )
					return;
			}
			catch ( Exception e )
			{
				Log.Warning( e, "[Thorns UI] Deferred icon warm failed." );
				return;
			}
		}

		Log.Warning( "[Thorns UI] Icon cache warm gave up — mount still has no ui/iconsv8 PNGs." );
	}

	public string DescribeUiState() =>
		$"uiBuilt={_uiBuilt} hudLayer={_hudLayer is not null && _hudLayer.IsValid} overlay={_overlay is not null && _overlay.IsValid} " +
		$"vitals={_vitals is not null} hotbar={_hotbar is not null} rightHud={_rightHud is not null} " +
		$"menuOpen={IsOpen} snapshot={ThornsUiClientState.HasSnapshot} go={GameObject.Name}";

	void OnJoinProgressChanged( ThornsMenuJoinStage stage )
	{
		_ = stage;
		_joinProgressOverlay?.Refresh();
		ApplyJoinLoadingPresentation();
	}

	void ApplyJoinLoadingPresentation()
	{
		var loading = ThornsMenuJoinFlow.IsProgressVisible || ThornsNearbyCosmeticsReadiness.IsWaiting;
		if ( _hudLayer is not null && _hudLayer.IsValid )
			_hudLayer.SetClass( "mainmenu-hidden", loading );

		if ( _overlay is not null && _overlay.IsValid && loading )
			_overlay.SetClass( "mainmenu-hidden", true );

		_joinProgressOverlay?.Refresh();
	}

	void BuildUi()
	{
		Panel.DeleteChildren( true );
		Panel.AddClass( "ThornsGameplayUi" );
		Panel.AddClass( "ThornsMenuHost" );
		ThornsUiSkin.ApplyRoot( Panel );
		ApplyRootPanelLayout();
		ApplyUiScale();

		_hudLayer = ThornsUiFactory.AddPanel( Panel, "ThornsHudRoot hud-pass-through" );
		_hudLayer.Style.Position = PositionMode.Absolute;
		_hudLayer.Style.Left = Length.Pixels( 0 );
		_hudLayer.Style.Top = Length.Pixels( 0 );
		_hudLayer.Style.Width = Length.Percent( 100 );
		_hudLayer.Style.Height = Length.Percent( 100 );
		_hudLayer.Style.Display = DisplayMode.Flex;
		_hudLayer.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.ApplyPassive( _hudLayer, ThornsUiPriority.Hud );
		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
			_hudLayer.AddClass( "hud-classic-layout" );

		if ( ThornsGameplayUiDiagnostics.ShowVisibleBanner )
		{
			var banner = ThornsUiFactory.AddLabel( _hudLayer, "THORNS UI LIVE", "thorns-debug-banner" );
			banner.Style.Position = PositionMode.Absolute;
			banner.Style.Top = Length.Pixels( 8 );
			banner.Style.Right = Length.Pixels( 12 );
			banner.Style.FontSize = Length.Pixels( 11 );
			banner.Style.FontColor = new Color( 1f, 0.35f, 0.35f );
			banner.Style.BackgroundColor = new Color( 0.1f, 0f, 0f, 0.75f );
			banner.Style.Padding = Length.Pixels( 6 );
		}

		_vitals = new ThornsVitalsHud( _hudLayer );
		_interaction = new ThornsInteractionHud( _hudLayer );
		if ( ThornsUiSkin.Active == ThornsUiSkinKind.Classic )
			_leftHud = new ThornsLeftHudColumn( _hudLayer );
		_rightHud = new ThornsRightHudColumn( _hudLayer, includeObjectives: ThornsUiSkin.Active != ThornsUiSkinKind.Classic );
		_hotbar = new ThornsHotbarHud( _hudLayer );
		_buildMenu = new ThornsBuildMenuHud( _hudLayer );
		_worldContainer = new ThornsWorldContainerHud( _hudLayer );
		_radioShop = new ThornsRadioShopHud( _hudLayer );
		_researchStation = new ThornsResearchStationHud( _hudLayer );
		_campfireHud = new ThornsCampfireHud( _hudLayer );
		_workbenchHud = new ThornsWorkbenchHud( _hudLayer );
		_notifications = new ThornsNotificationHud( _hudLayer );
		_worldEvents = new ThornsWorldEventHud( _hudLayer );
		_joinAnnouncements = new ThornsJoinAnnouncementHud( _hudLayer );
		_lootFeed = new ThornsLootFeedHud( _hudLayer );
		ThornsHitmarkerState.Reset();
		ThornsDamageFlashState.Reset();
		ThornsUnderwaterViewState.Reset();
		_crosshair = new ThornsCrosshairHud( _hudLayer );
		_sniperScope = new ThornsSniperScopeHud( _hudLayer );
		_damageFlash = new ThornsDamageFlashHud( _hudLayer );
		_underwaterOverlay = new ThornsUnderwaterOverlayHud( _hudLayer );
		_victoryIntro = new ThornsVictoryPathIntroHud( Panel );
		_vitalsCritical = new ThornsVitalsCriticalHud( _hudLayer );
		_levelUpMoment = new ThornsLevelUpMomentHud( _hudLayer );
		_sessionRecap = new ThornsSessionRecapHud( _hudLayer );
		if ( ThornsFirstSessionTutorialHud.Enabled )
			_firstSessionTutorial = new ThornsFirstSessionTutorialHud( Panel );

		_joinProgressOverlay = new MainMenuProgressOverlay( Panel );
		_joinProgressOverlay.Refresh();

		ThornsTooltip.EnsureHost( Panel );
		ThornsUiManager.SetContext( ThornsUiManager.UiContext.Gameplay );

		_overlay = ThornsUiFactory.AddPanel( Panel, "thorns-menu-overlay" );
		ApplyOverlayPanelLayout( _overlay );
		_overlay.Style.Display = DisplayMode.None;
		ThornsMenuChrome.ApplyMenuOverlay( _overlay );

		var sideInset = ThornsUiMetrics.MenuScreenSideInset;
		var edgeInset = ThornsUiMetrics.MenuScreenEdgeInset;

		var top = ThornsUiFactory.AddPanel( _overlay, "thorns-menu-topbar thorns-menu-topbar-fantasy" );
		top.Style.FlexShrink = 0;
		top.Style.Height = Length.Pixels( ThornsUiMetrics.MenuTopbarHeight );
		top.Style.MinHeight = Length.Pixels( ThornsUiMetrics.MenuTopbarHeight );
		top.Style.FlexDirection = FlexDirection.Row;
		top.Style.AlignItems = Align.Center;
		top.Style.MarginLeft = Length.Pixels( sideInset );
		top.Style.MarginRight = Length.Pixels( sideInset );
		top.Style.MarginTop = Length.Pixels( edgeInset );

		ThornsMenuChrome.ApplyMenuTopBar( top );
		var brand = ThornsUiFactory.AddPanel( top, "thorns-menu-brand" );
		brand.Style.FlexDirection = FlexDirection.Row;
		brand.Style.AlignItems = Align.Center;
		brand.Style.FlexShrink = 0;
		brand.Style.MaxWidth = Length.Pixels( 160 );
		brand.Style.Overflow = OverflowMode.Hidden;
		var brandIcon = ThornsUiFactory.AddPanel( brand, "thorns-menu-brand-icon slot-icon" );
		brandIcon.Style.Width = Length.Pixels( ThornsUiMetrics.BrandIcon );
		brandIcon.Style.Height = Length.Pixels( ThornsUiMetrics.BrandIcon );
		brandIcon.Style.MinWidth = Length.Pixels( ThornsUiMetrics.BrandIcon );
		brandIcon.Style.MinHeight = Length.Pixels( ThornsUiMetrics.BrandIcon );
		brandIcon.Style.FlexShrink = 0;
		ThornsIconCache.ApplyToPanel( brandIcon, ThornsIconRegistry.GuildEmblem() );
		ThornsUiFactory.AddLabel( brand, "THORNS", "thorns-menu-brand-label" );

		_tabBar = new ThornsTabBar( top, ShowTab );
		RefreshTabBar();

		var close = ThornsUiFactory.AddClickable( top, "close exit-to-main-menu", OnExitToMainMenuClicked );
		var closeIcon = ThornsUiFactory.AddPanel( close, "close-icon slot-icon" );
		closeIcon.Style.Width = Length.Pixels( ThornsUiMetrics.CloseIcon );
		closeIcon.Style.Height = Length.Pixels( ThornsUiMetrics.CloseIcon );
		closeIcon.Style.MinWidth = Length.Pixels( ThornsUiMetrics.CloseIcon );
		closeIcon.Style.MinHeight = Length.Pixels( ThornsUiMetrics.CloseIcon );
		ThornsIconCache.ApplyToPanel( closeIcon, ThornsIconRegistry.MenuTab( "exit" ) );

		_body = ThornsUiFactory.AddPanel( _overlay, "thorns-menu-body thorns-menu-body-parchment" );
		_body.Style.FlexGrow = 1;
		_body.Style.FlexShrink = 1;
		_body.Style.MinHeight = Length.Pixels( 0 );
		_body.Style.Height = Length.Percent( 100 );
		_body.Style.MarginLeft = Length.Pixels( sideInset );
		_body.Style.MarginRight = Length.Pixels( sideInset );
		_body.Style.MarginBottom = Length.Pixels( edgeInset );
		ThornsMenuChrome.ApplyMenuBodyParchment( _body );
		_body.AddEventListener( "onmouseup", OnMenuBodyMouseUp );

		UiRevisionBus.MenuRevisionChanged -= OnHudRevision;
		UiRevisionBus.MenuRevisionChanged += OnHudRevision;
	}

	/// <summary>
	/// Drag ghost must share the menu overlay's coordinate space when Tab is open (root has pass-through pointer events).
	/// Matches thorns <c>ShellInventoryDragGhostHostParent</c> / <c>ShellTabInventoryGhostScreenPoint</c>.
	/// </summary>
	(Panel basis, Panel coord) ResolveDragGhostPanels()
	{
		if ( IsOpen && _overlay is { IsValid: true } )
			return (_overlay, _overlay);

		if ( IsWorldContainerOpen && Panel is { IsValid: true } )
			return (Panel, Panel);

		if ( ( IsCampfireOpen || IsWorkbenchOpen ) && Panel is { IsValid: true } )
			return (Panel, Panel);

		return (Panel, Panel);
	}

	void ApplyRootPanelLayout()
	{
		if ( Panel is null || !Panel.IsValid )
			return;

		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Left = Length.Pixels( 0 );
		Panel.Style.Top = Length.Pixels( 0 );
		Panel.Style.Width = Length.Percent( 100 );
		Panel.Style.Height = Length.Percent( 100 );
		Panel.Style.Display = DisplayMode.Flex;
		Panel.Style.FlexDirection = FlexDirection.Column;
		Panel.Style.PointerEvents = PointerEvents.None;
		Panel.Style.Opacity = 1f;
		Panel.Style.FontColor = ThornsTheme.TextPrimary;
	}

	static void ApplyOverlayPanelLayout( Panel overlay )
	{
		overlay.Style.Position = PositionMode.Absolute;
		overlay.Style.Left = Length.Pixels( 0 );
		overlay.Style.Top = Length.Pixels( 0 );
		overlay.Style.Width = Length.Percent( 100 );
		overlay.Style.Height = Length.Percent( 100 );
		overlay.Style.FlexDirection = FlexDirection.Column;
		overlay.Style.BackgroundColor = Color.Transparent;
		ThornsUiLayer.ApplyModalSurface( overlay, ThornsUiPriority.FullscreenMenu );
	}

	void SyncOverlayRegistration(
		string id,
		bool open,
		ThornsUiPriority priority,
		Panel panel,
		Action onEscape,
		ThornsUiWindowKind kind,
		bool isModal = true )
	{
		if ( !open || panel is null || !panel.IsValid )
		{
			if ( ThornsUiManager.IsOpen( id ) )
				ThornsUiManager.Unregister( id );
			return;
		}

		ThornsUiManager.Register(
			id,
			priority,
			panel,
			capturesInput: true,
			blocksGameplay: true,
			isModal: isModal,
			onEscape: onEscape,
			onConflictClose: onEscape,
			kind: kind );
	}

	void SyncBuildMenuRegistration()
	{
		var buildOpen = ThornsPlayerBuildingController.Local?.BuildMenuOpen == true;
		if ( !buildOpen )
		{
			if ( ThornsUiManager.IsOpen( "build-menu" ) )
				ThornsUiManager.Unregister( "build-menu" );
			return;
		}

		var panel = _buildMenu?.Root;
		if ( panel is null || !panel.IsValid )
			return;

		ThornsUiManager.Register(
			"build-menu",
			ThornsUiPriority.Hotbar,
			panel,
			capturesInput: true,
			blocksGameplay: false,
			isModal: false,
			onEscape: () => ThornsPlayerBuildingController.Local?.ForceCloseBuildMode(),
			onConflictClose: () => ThornsPlayerBuildingController.Local?.ForceCloseBuildMode(),
			kind: ThornsUiWindowKind.BuildMenu );
	}

	async void OnExitToMainMenuClicked()
	{
		SetOpen( false );
		ThornsUiCursor.EnsureMainMenuVisible();
		await ThornsMenuSceneLoader.LoadMainMenuAsync();
	}

	static void OnMenuBodyMouseUp( PanelEvent e )
	{
		if ( !ThornsDragState.IsDragging )
			return;

		ThornsAttachmentInspectSlot.RefreshDropTarget();
		if ( ThornsAttachmentInspectSlot.TryCompleteHoveredDrop() )
			return;

		if ( ThornsDragState.PointerMoved && ThornsItemSlot.TryCompleteHoveredDrop() )
			return;

		if ( !ThornsDragState.PointerMoved )
			ThornsAttachmentDragDebug.LogReject( "OnMenuBodyMouseUp", "pointer did not move enough (<4px) — drag cancelled" );

		ThornsDragState.Clear();
		ThornsItemSlot.ClearDropTarget();
		ThornsAttachmentInspectSlot.RefreshDropTarget();
	}

	static bool IsItemSlotPanel( Panel panel )
	{
		for ( var p = panel; p is not null && p.IsValid; p = p.Parent )
		{
			if ( p is Terraingen.UI.Components.ThornsItemSlot || p.HasClass( "thorns-item-slot" ) )
				return true;
		}

		return false;
	}
}
