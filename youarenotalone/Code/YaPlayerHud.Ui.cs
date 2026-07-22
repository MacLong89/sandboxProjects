using Sandbox.UI;

namespace Sandbox;

public sealed partial class YaPlayerHud
{
	YaUiManager _uiManager;
	YaUiFrameContext _uiFrame = new();
	bool _uiReady;
	YaPlayerRole _chromeRole = YaPlayerRole.Unassigned;

	Panel _modalScrimRoot;
	Panel _topObjectiveRoot;
	Panel _topLeftHintsRoot;
	Panel _inputHintsPanel;
	Panel _healthCardPanel;
	Panel _combatHudRoot;
	Panel _paranoiaOverlayRoot;
	Panel _damageFeedbackRoot;
	Panel _floatingMessageStackRoot;
	Panel _floatingPersonalRoot;
	Panel _floatingCombatRoot;
	Label _floatingPersonalLabel;
	Label _floatingCombatLabel;

	internal Panel ModalScrimRoot => _modalScrimRoot;
	internal YaDeathOverlayPanel DeathOverlayPanel => _deathOverlay;
	internal Panel RoundVictoryRoot => _roundVictoryRoot;
	internal Panel PracticeChoiceRoot => _practiceChoiceRoot;
	internal YaControlsTutorialPanel ControlsTutorialPanel => _controlsTutorial;
	internal Panel ScoreboardRoot => _scoreboardRoot;
	internal Panel TopObjectiveRoot => _topObjectiveRoot;
	internal Panel TopLeftHintsRoot => _topLeftHintsRoot;
	internal Panel CombatHudRoot => _combatHudRoot;
	internal Panel CrosshairRoot => _crosshairRoot;
	internal Panel ParanoiaOverlayRoot => _paranoiaOverlayRoot;
	internal Panel DamageFeedbackRoot => _damageFeedbackRoot;
	internal YaKillFeedPanel KillFeedPanel => _killFeed;
	internal Panel RoundStartAnnouncementRoot => _roundStartAnnouncementRoot;
	internal Panel LobbySoloHintWrap => _lobbySoloHintWrap;
	internal Panel FloatingMessageRoot => _floatingMessageStackRoot;

	void InitializeUiManager()
	{
		if ( _uiManager is not null )
			return;

		_uiManager = new YaUiManager();
		YaUiManager.SetLocal( _uiManager );
		YaHudUiRegistry.RegisterAll( this, _uiManager );
		_uiReady = true;
	}

	void BuildUiFrameContext(
		bool isDead,
		bool inRoundClient,
		bool spectatingInRound,
		bool showPracticeChoice,
		bool controlsTutorialVisible,
		bool scoreboardVisible,
		bool showDeathOverlay,
		bool showCombatHudForAssignedRole,
		bool showRoundVictory,
		bool paranoiaHunterOn,
		bool showRoundStartAnnouncement,
		bool showLobbySoloHint,
		bool showFloatingMessages,
		bool showDamageFeedback,
		bool showOnboardingTip )
	{
		var hideCombatHud = spectatingInRound;

		_uiFrame.IsLocalPlayer = true;
		_uiFrame.InRound = inRoundClient;
		_uiFrame.InCombat = inRoundClient && !spectatingInRound && !isDead;
		_uiFrame.IsDead = isDead;
		_uiFrame.IsSpectating = spectatingInRound;
		_uiFrame.ShowPracticeChoice = showPracticeChoice;
		_uiFrame.ShowControlsTutorial = controlsTutorialVisible;
		_uiFrame.ShowScoreboard = scoreboardVisible;
		_uiFrame.ShowDeathOverlay = showDeathOverlay;
		_uiFrame.ShowRoundVictory = showRoundVictory;
		_uiFrame.ShowHudCombat = !hideCombatHud && showCombatHudForAssignedRole;
		_uiFrame.ShowHudTopObjective = !hideCombatHud || inRoundClient;
		_uiFrame.ShowHudTopLeftHints = !hideCombatHud && showCombatHudForAssignedRole;
		_uiFrame.ShowCrosshair = !hideCombatHud && showCombatHudForAssignedRole;
		_uiFrame.ShowParanoiaOverlays = paranoiaHunterOn;
		_uiFrame.ShowDamageFeedback = showDamageFeedback && !hideCombatHud;
		_uiFrame.ShowEventFeed = true;
		_uiFrame.ShowRoundStartAnnouncement = showRoundStartAnnouncement;
		_uiFrame.ShowLobbySoloHint = showLobbySoloHint;
		_uiFrame.ShowFloatingMessages = showFloatingMessages;
		_uiFrame.RequiresMouse = showPracticeChoice || controlsTutorialVisible || scoreboardVisible
		                         || hideCombatHud || showDeathOverlay || showOnboardingTip;
	}

	void TickUiManager( float dt )
	{
		if ( !_uiReady || _uiManager is null )
			return;

		_uiManager.Tick( _uiFrame, dt );
		ApplyUiManagerResults();
	}

	void ApplyUiManagerResults()
	{
		Mouse.Visibility = _uiFrame.RequiresMouse || _uiManager.AnyModalActive
			? MouseVisibility.Visible
			: MouseVisibility.Hidden;

		if ( _scoreboardRoot.IsValid() && _uiManager.IsVisible( YaUiSurfaceId.FullscreenScoreboard ) )
		{
			if ( Time.Now >= _nextScoreboardRefresh )
			{
				_nextScoreboardRefresh = Time.Now + 0.2;
				RefreshScoreboardRows( GameObject.Scene );
			}
		}

		RefreshFloatingMessageStack();
	}

	internal void SetCombatDockVisible( bool visible )
	{
		if ( !_weaponDock.IsValid() )
			return;

		_weaponDock.Style.Display = visible ? DisplayMode.Flex : DisplayMode.None;
	}

	void ApplyRoleChromeTheme( YaPlayerRole role )
	{
		_chromeRole = role;
		var p = YaHudRoleTheme.For( role );
		var isAlone = role == YaPlayerRole.Alone;

		Panel.RemoveClass( "ya-player-hud--alone" );
		Panel.RemoveClass( "ya-player-hud--hunter" );
		if ( isAlone )
			Panel.AddClass( "ya-player-hud--alone" );
		else if ( role == YaPlayerRole.NotAlone )
			Panel.AddClass( "ya-player-hud--hunter" );

		_killFeed?.SetLocalRole( role );

		if ( _weaponRail.IsValid() )
		{
			_weaponRail.Style.BorderTopColor = p.AccentStrong;
			_weaponRail.Style.BorderBottomColor = p.Border;
		}

		if ( _inputHintsPanel.IsValid() )
		{
			_inputHintsPanel.Style.BorderTopColor = p.AccentStrong;
			_inputHintsPanel.Style.BorderColor = p.Border;
			ApplyLabelAccentInTree( _inputHintsPanel, "ya-hud-input-hints-panel__title", p.Accent );
			ApplyLabelAccentInTree( _inputHintsPanel, "ya-hud-input-hints-panel__key-text", p.Accent );
			ApplyLabelAccentInTree( _inputHintsPanel, "ya-hud-input-hints-panel__arrow", p.AccentDim );
			ApplyPanelChromeInTree( _inputHintsPanel, "ya-hud-input-hints-panel__key", panel =>
			{
				panel.Style.BorderColor = p.BorderStrong;
			} );
		}

		if ( _healthCardPanel.IsValid() )
		{
			_healthCardPanel.Style.BorderTopWidth = 2;
			_healthCardPanel.Style.BorderTopColor = p.AccentStrong;
			_healthCardPanel.Style.BorderColor = p.Border;
		}

		if ( _intermissionReadyLabel.IsValid() )
			_intermissionReadyLabel.Style.FontColor = p.Accent;

		if ( _roundStartAnnouncementLabel.IsValid() )
			_roundStartAnnouncementLabel.Style.FontColor = p.Accent;

		if ( _mimicProgressLabel.IsValid() )
			_mimicProgressLabel.Style.FontColor = p.Accent;
		if ( _mimicProgressTrack.IsValid() )
			_mimicProgressTrack.Style.BorderColor = p.AccentStrong;
		if ( _mimicProgressFill.IsValid() && isAlone )
			_mimicProgressFill.Style.BackgroundColor = p.MeterPrimary;

		if ( _hotbarHint.IsValid() )
		{
			_hotbarHint.RemoveClass( "ya-hud-hint--alone" );
			if ( isAlone )
				_hotbarHint.AddClass( "ya-hud-hint--alone" );
			_hotbarHint.Style.FontColor = isAlone ? YaHudTheme.TextSecondary : YaHudTheme.TextMuted;
		}

		_alivePanel?.SetAccent( p.Accent );
	}

	static void ApplyPanelChromeInTree( Panel root, string className, Action<Panel> apply )
	{
		if ( !root.IsValid() )
			return;

		if ( root.HasClass( className ) )
			apply( root );

		foreach ( var child in root.Children )
			ApplyPanelChromeInTree( child, className, apply );
	}

	static void ApplyLabelAccentInTree( Panel root, string className, Color color )
	{
		if ( !root.IsValid() )
			return;

		if ( root is Label lbl && lbl.HasClass( className ) )
			lbl.Style.FontColor = color;

		foreach ( var child in root.Children )
			ApplyLabelAccentInTree( child, className, color );
	}

	void RefreshFloatingMessageStack()
	{
		if ( !_floatingMessageStackRoot.IsValid() )
			return;

		RefreshFloatingZone( _floatingPersonalRoot, YaUiPopupZone.BottomLeftPersonal, ref _floatingPersonalLabel );
		RefreshFloatingZone( _floatingCombatRoot, YaUiPopupZone.TopRightCombat, ref _floatingCombatLabel );
	}

	void RefreshFloatingZone( Panel root, YaUiPopupZone zone, ref Label labelRef )
	{
		if ( !root.IsValid() )
			return;

		if ( !labelRef.IsValid() )
		{
			labelRef = root.AddChild( new Label( "", "ya-hud-floating-message" ) );
			labelRef.AddClass( zone == YaUiPopupZone.BottomLeftPersonal
				? "ya-hud-floating-message--personal"
				: "ya-hud-floating-message--combat" );
			labelRef.Style.FontSize = zone == YaUiPopupZone.BottomLeftPersonal ? 18 : 20;
			labelRef.Style.FontWeight = 900;
			labelRef.Style.FontColor = YaHudRoleTheme.For( _chromeRole ).Accent;
		}

		var line = _uiManager?.Popups.GetVisibleLine( zone );
		if ( line is null )
		{
			labelRef.Style.Display = DisplayMode.None;
			return;
		}

		labelRef.Style.Display = DisplayMode.Flex;
		labelRef.Text = line.Value.Text;
		labelRef.Style.FontColor = line.Value.Color;
	}

	void PushFloatingMessageToQueue( string text, Color? color = null )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return;

		if ( !_uiReady || _uiManager is null )
			return;

		_uiManager.Popups.Push( text.Trim(), color ?? YaHudRoleTheme.For( _chromeRole ).Accent, YaUiPopupQueue.ResolveZone( text ) );
	}
}
