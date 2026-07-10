using System;
using System.Collections.Generic;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Local-only HUD: health bar, role-sized weapon hotbar, ammo readout, crosshair (Thorns-style layout, YA scope).</summary>
[Title( "YouAreNotAlone — Player HUD" )]
[Category( "YouAreNotAlone" )]
[Icon( "web" )]
[Order( 30 )]
public sealed partial class YaPlayerHud : PanelComponent, Component.INetworkSpawn
{
	const float HealthBarWidth = 280f;
	const float SlotPx = 56f;

	/// <summary>Paranoia time→opacity curve: &lt;1 keeps blur/dark stronger late in the debuff (slower return to clear).</summary>
	const float ParanoiaFadeCurveExponent = 0.38f;

	const float ParanoiaHunterFullscreenBlurPixels = 14f;

	const float DamageFlashDurationSeconds = 0.38f;

	const float HitmarkerDurationSeconds = 0.26f;

	const float RoundStartAnnouncementSeconds = 4f;

	const string RoundVictorySoundResource = "sounds/paranoia_sound.sound";

	static readonly Color HitmarkerBodyColor = new( 0.98f, 0.22f, 0.18f, 1f );
	static readonly Color HitmarkerHeadshotColor = new( 1f, 0.86f, 0.28f, 1f );

	/// <summary>Bottom hint under Alone ability tiles (circle bullets between segments).</summary>
	const string AloneAbilitiesHintLine = "LMB - Fast Attack ● Hold RMB - Power Attack ● Q - Dash ● E - Paranoia ● F - Mimic";

	bool _treeReady;
	Panel _healthFill;
	Panel _staminaTrack;
	Panel _staminaFill;
	Label _hpReadout;
	Label _ammoReadout;
	Label _weaponNameLbl;
	Panel _crosshairRoot;
	Panel _hitmarkerRoot;
	readonly Panel[] _hitmarkerXLegs = new Panel[2];

	double _hitmarkerEndRealtime;
	bool _hasPendingHitmarker;
	bool _pendingHitmarkerHeadshot;
	Panel _weaponDock;
	Panel _weaponRail;
	/// <summary>Name + ammo row — hidden for <see cref="YaPlayerRole.Alone"/> (abilities row replaces that copy).</summary>
	Panel _weaponTitleRow;
	Label _hotbarHint;
	Panel _defaultSlotsRow;
	Panel _aloneAbilitiesRow;
	readonly AloneAbilityCellPanel[] _aloneAbilityCells = new AloneAbilityCellPanel[5];
	Panel _dashCooldownTrack;
	Panel _dashCooldownFill;
	Panel _mimicProgressRoot;
	Label _mimicProgressLabel;
	Panel _mimicProgressTrack;
	Panel _mimicProgressFill;
	Panel _paranoiaHunterDebuffTrack;
	Panel _paranoiaHunterDebuffFill;
	Panel _m2ChargeTrack;
	Panel _m2ChargeFill;
	readonly HotbarSlotPanel[] _slotCells = new HotbarSlotPanel[3];
	int? _hoverSlot;

	YaRolePanel _rolePanel;
	YaTimerPanel _timerPanel;
	Panel _lobbySoloHintWrap;
	Label _lobbySoloHint;
	YaObjectivePanel _objectivePanel;
	YaAliveCounterPanel _alivePanel;
	YaAloneStatusPanel _aloneStatusPanel;
	YaKillFeedPanel _killFeed;
	YaDeathOverlayPanel _deathOverlay;
	Panel _roundVictoryRoot;
	Label _roundVictoryHeadline;
	Label _roundVictorySummary;
	Label _roundVictoryCountdown;
	Panel _roundStartAnnouncementRoot;
	Label _roundStartAnnouncementLabel;
	Panel _scoreboardRoot;
	Panel _scoreboardRows;
	YaControlsTutorialPanel _controlsTutorial;
	Panel _practiceChoiceRoot;
	Panel _paranoiaBrightnessOverlay;
	Panel _paranoiaBrightnessOverlayExtra;
	Panel _paranoiaHunterFullscreenBlur;

	/// <summary>Full-screen edge tint when the local player takes damage (owner RPC from <see cref="YaPlayerHealth"/>).</summary>
	Panel _damageVignetteRoot;
	Panel _damageDirectionRoot;
	Panel _headshotFlashRoot;
	Label _mutatorLabel;
	Label _intermissionReadyLabel;
	double _damageDirectionEndAt;
	double _headshotFlashEndAt;
	float _pendingDamageYawDelta;

	double _damageFlashEndRealtime;
	float _damageFlashPeakOpacity = 0.72f;

	/// <summary>&gt;= 0 — damage arrived before vignette panel existed (flush after <see cref="BuildTree"/>).</summary>
	float _pendingDamageFlash = -1f;

	double _nextMatchUiTick;
	double _nextScoreboardRefresh;
	double _nextPracticeRoleRequestAt;
	double _nextHudMatchDebugLog;
	bool _practiceChoiceDismissedLocal;
	Guid _lastAloneConnectionId;
	double _nextFeedThrottle;
	double _roundStartAnnouncementEndAt;
	double _onboardingAutoHideAt;
	bool _onboardingAutoShown;
	bool _onboardingDismissedByInput;
	int _localKillStreak;
	int _lastHeartbeatSecond = -1;
	double _lastRoundVictorySoundAt = -999;
	YaGameState _lastMatchState = YaGameState.Lobby;
	bool _roundStartAnnouncementShownThisRound;
	string _lastVictoryHeadlineShown = "";

	/// <summary>Prior-frame replicated paranoia timer — detect rising edge for hunter debuff sting.</summary>
	float _prevParanoiaDebuffSecondsRemaining;

	sealed class HotbarSlotPanel : Panel
	{
		public int SlotIndex { get; set; }
		public YaPlayerHud Hud { get; set; }
		public Label NameLabel { get; set; }

		public override bool WantsMouseInput() => true;

		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );
			if ( e.MouseButton != MouseButtons.Left || Hud is null )
				return;
			var hb = Hud.Components.Get<YaHotbarEquipment>();
			hb?.RequestSelectHotbarSlot( SlotIndex );
		}

		protected override void OnMouseOver( MousePanelEvent e )
		{
			base.OnMouseOver( e );
			Hud?.SetSlotHover( SlotIndex, true );
		}

		protected override void OnMouseOut( MousePanelEvent e )
		{
			base.OnMouseOut( e );
			Hud?.SetSlotHover( SlotIndex, false );
		}
	}

	/// <summary>Alone hotbar key with bottom-up role fill (cooldown / M2 charge).</summary>
	sealed class AloneAbilityCellPanel : Panel
	{
		public Panel Fill;
		public Label KeyLabel;

		public AloneAbilityCellPanel()
		{
			AddClass( "ya-hud-alone-ability" );
			Style.Width = 48f;
			Style.Height = 52f;
			Style.FlexDirection = FlexDirection.Column;
			Style.JustifyContent = Justify.Center;
			Style.AlignItems = Align.Center;
			Style.Padding = 0;
			Style.BackgroundColor = YaHudTheme.PanelDeep;
			Style.BorderWidth = 1;
			Style.BorderColor = YaHudTheme.Border;
			Style.Overflow = OverflowMode.Hidden;
			Style.Position = PositionMode.Relative;

			Fill = AddChild<Panel>( "ya-hud-alone-ability__fill" );
			Fill.AddClass( "ya-hud-alone-ability__fill" );
			Fill.Style.Position = PositionMode.Absolute;
			Fill.Style.Left = 0;
			Fill.Style.Bottom = 0;
			Fill.Style.Width = Length.Fraction( 1f );
			Fill.Style.Height = Length.Fraction( 0f );
			Fill.Style.BackgroundColor = YaHudRoleTheme.Alone.MeterPrimary;
			Fill.Style.Opacity = 0.88f;
			Fill.Style.ZIndex = 0;
			Fill.Style.PointerEvents = PointerEvents.None;

			KeyLabel = AddChild( new Label( "?", "ya-hud-alone-ability-name" ) );
			KeyLabel.Style.Position = PositionMode.Relative;
			KeyLabel.Style.ZIndex = 2;
			KeyLabel.Style.FontSize = 11;
			KeyLabel.Style.FontWeight = 800;
			KeyLabel.Style.FontColor = YaHudTheme.TextPrimary;
			KeyLabel.Style.TextAlign = TextAlign.Center;
			KeyLabel.Style.PointerEvents = PointerEvents.None;
		}

		public void ApplyFill01( float fill01 )
		{
			var t = Math.Clamp( fill01, 0f, 1f );
			var ready = t >= 0.98f;
			var alone = YaHudRoleTheme.Alone;

			Fill.Style.Height = Length.Fraction( t );
			Fill.Style.Opacity = ready ? 1f : 0.72f;
			Fill.Style.BackgroundColor = alone.MeterPrimary;
			Style.BorderColor = ready ? alone.AccentStrong : alone.Border;
			Style.BackgroundColor = ready ? alone.SlotReadyBg : YaHudTheme.PanelDeep;
		}
	}

	sealed class ScoreboardRowPanel : Panel
	{
		readonly Label _name;
		readonly Label _sessionKills;
		readonly Label _sessionDeaths;
		readonly Label _sessionWins;

		public ScoreboardRowPanel()
		{
			AddClass( "ya-scoreboard__row" );
			_name = AddChild( new Label( "Player", "ya-scoreboard__cell ya-scoreboard__cell--name" ) );
			_sessionKills = AddChild( new Label( "0", "ya-scoreboard__cell" ) );
			_sessionDeaths = AddChild( new Label( "0", "ya-scoreboard__cell" ) );
			_sessionWins = AddChild( new Label( "0", "ya-scoreboard__cell" ) );
		}

		public void Apply( string name, int sessionKills, int sessionDeaths, int sessionWins )
		{
			_name.Text = name;
			_sessionKills.Text = sessionKills.ToString();
			_sessionDeaths.Text = sessionDeaths.ToString();
			_sessionWins.Text = sessionWins.ToString();
		}
	}

	sealed class PracticeRoleButtonPanel : Panel
	{
		public YaPlayerRole Role { get; set; }
		public YaPlayerHud Hud { get; set; }

		public PracticeRoleButtonPanel()
		{
			AddClass( "ya-practice-choice__button" );
		}

		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );
			if ( e.MouseButton != MouseButtons.Left )
				return;
			Hud?.TryRequestPracticeRole( Role );
		}
	}

	void TryPlayLocalParanoiaDebuffSting()
	{
		var p = YaHunterParanoia.ParanoiaDebuffSoundResource;
		if ( string.IsNullOrWhiteSpace( p ) )
			return;
		var h = Sound.Play( p.Trim() );
		if ( h is { IsValid: true } snd )
			snd.Volume = 0.5f;
	}

	internal void SetSlotHover( int slot, bool over )
	{
		if ( over )
			_hoverSlot = slot;
		else if ( _hoverSlot == slot )
			_hoverSlot = null;
	}

	/// <summary>Called when the owning player receives damage — brief red edge vignette.</summary>
	internal void NotifyDamageTakenLocal( float damageAmount, float sourceYawDeltaDegrees = 0f )
	{
		if ( !_damageVignetteRoot.IsValid() )
		{
			_pendingDamageFlash = Math.Max( _pendingDamageFlash, damageAmount );
			_pendingDamageYawDelta = sourceYawDeltaDegrees;
			return;
		}

		ApplyDamageFlash( damageAmount, sourceYawDeltaDegrees );
	}

	void ApplyDamageFlash( float damageAmount, float sourceYawDeltaDegrees = 0f )
	{
		if ( !_damageVignetteRoot.IsValid() )
			return;

		var peak = Math.Clamp( 0.42f + damageAmount * 0.0075f, 0.5f, 0.95f );
		_damageFlashPeakOpacity = peak;
		_damageFlashEndRealtime = Time.Now + DamageFlashDurationSeconds;
		_damageVignetteRoot.Style.Opacity = peak;
		ShowDamageDirection( sourceYawDeltaDegrees );
	}

	void ShowDamageDirection( float yawDeltaDegrees )
	{
		if ( !_damageDirectionRoot.IsValid() )
			return;

		if ( MathF.Abs( yawDeltaDegrees ) < 2f )
		{
			_damageDirectionRoot.Style.Display = DisplayMode.None;
			return;
		}

		var rad = yawDeltaDegrees * (MathF.PI / 180f);
		var edgeX = 0.5f + MathF.Sin( rad ) * 0.38f;
		var edgeY = 0.42f - MathF.Cos( rad ) * 0.28f;
		_damageDirectionRoot.Style.Display = DisplayMode.Flex;
		_damageDirectionRoot.Style.Left = Length.Fraction( edgeX );
		_damageDirectionRoot.Style.Top = Length.Fraction( edgeY );
		_damageDirectionRoot.Style.MarginLeft = Length.Pixels( -18 );
		_damageDirectionEndAt = Time.Now + DamageFlashDurationSeconds;
	}

	/// <summary>Owner-only: confirmed gun or melee hit from <see cref="YaWeapon.RpcFireOutcome"/>.</summary>
	internal void NotifyHitmarkerLocal( bool headshotTint )
	{
		if ( !_hitmarkerRoot.IsValid() )
		{
			_hasPendingHitmarker = true;
			_pendingHitmarkerHeadshot = headshotTint;
			return;
		}

		ApplyHitmarkerFlash( headshotTint );
	}

	internal void NotifyFloatingMessageLocal( string text ) => PushFloatingMessage( text );

	internal void NotifyKillConfirmedLocal()
	{
		_localKillStreak = Math.Max( 1, _localKillStreak + 1 );
		PushFloatingMessage( _localKillStreak switch
		{
			>= 3 => "RAMPAGE",
			2 => "DOUBLE KILL",
			_ => "ELIMINATED"
		} );
	}

	void PushFloatingMessage( string text )
	{
		PushFloatingMessageToQueue( text );
	}

	void TryShowFirstRunOnboarding()
	{
		if ( _onboardingAutoShown || YaClientPrefs.HasSeenControlsTutorial )
			return;
		if ( !_controlsTutorial.IsValid() )
			return;

		_onboardingAutoShown = true;
		_onboardingAutoHideAt = Time.Now + 8.0;
	}

	void TryDismissOnboarding()
	{
		if ( !_onboardingAutoShown || _onboardingDismissedByInput )
			return;
		if ( !IsControlsTutorialHeld() && Time.Now < _onboardingAutoHideAt )
			return;

		_onboardingDismissedByInput = true;
		YaClientPrefs.HasSeenControlsTutorial = true;
	}

	void ShowRoundStartAnnouncement( string text )
	{
		if ( !_roundStartAnnouncementRoot.IsValid() || !_roundStartAnnouncementLabel.IsValid() )
			return;

		_roundStartAnnouncementLabel.Text = text;
		_roundStartAnnouncementEndAt = Time.Now + RoundStartAnnouncementSeconds;
	}

	void TryPlayRoundVictorySting()
	{
		if ( Time.Now - _lastRoundVictorySoundAt < 0.5 )
			return;

		_lastRoundVictorySoundAt = Time.Now;
		if ( string.IsNullOrWhiteSpace( RoundVictorySoundResource ) )
			return;

		var h = Sound.Play( RoundVictorySoundResource.Trim() );
		if ( h is { IsValid: true } snd )
			snd.Volume = 0.62f;
	}

	void TryPlayFinalSecondsHeartbeat( int roundSecondsRemaining )
	{
		if ( roundSecondsRemaining > 30 || roundSecondsRemaining <= 0 )
		{
			_lastHeartbeatSecond = -1;
			return;
		}

		if ( roundSecondsRemaining == _lastHeartbeatSecond )
			return;

		_lastHeartbeatSecond = roundSecondsRemaining;
		if ( string.IsNullOrWhiteSpace( RoundVictorySoundResource ) )
			return;

		var h = Sound.Play( RoundVictorySoundResource.Trim() );
		if ( h is { IsValid: true } snd )
			snd.Volume = 0.18f;
	}

	void ApplyHitmarkerFlash( bool headshotTint )
	{
		if ( !_hitmarkerRoot.IsValid() )
			return;

		_hitmarkerEndRealtime = Time.Now + (headshotTint ? HitmarkerDurationSeconds * 1.35f : HitmarkerDurationSeconds);
		var c = headshotTint ? HitmarkerHeadshotColor : HitmarkerBodyColor;
		foreach ( var leg in _hitmarkerXLegs )
		{
			if ( leg.IsValid() )
			{
				leg.Style.BackgroundColor = c;
				if ( headshotTint )
				{
					leg.Style.Width = 18;
					leg.Style.Height = 3;
				}
				else
				{
					leg.Style.Width = 14;
					leg.Style.Height = 2;
				}
			}
		}

		_hitmarkerRoot.Style.Display = DisplayMode.Flex;
		_hitmarkerRoot.Style.Opacity = 1f;

		if ( headshotTint && _headshotFlashRoot.IsValid() )
		{
			_headshotFlashRoot.Style.Display = DisplayMode.Flex;
			_headshotFlashRoot.Style.Opacity = 0.85f;
			_headshotFlashEndAt = Time.Now + 0.18;
		}
	}

	void FlushPendingHitmarkerIfAny()
	{
		if ( !_hasPendingHitmarker || !_hitmarkerRoot.IsValid() )
			return;

		_hasPendingHitmarker = false;
		ApplyHitmarkerFlash( _pendingHitmarkerHeadshot );
	}

	public void OnNetworkSpawn( Connection owner ) => TryInitializeLocalHud();

	protected override void OnStart() => TryInitializeLocalHud();

	void TryInitializeLocalHud()
	{
		if ( _treeReady )
			return;

		var local = Connection.Local;
		if ( local is null || GameObject.Network.OwnerId != local.Id )
			return;

		if ( !Components.Get<ScreenPanel>( FindMode.EnabledInSelf ).IsValid() )
			_ = Components.Create<ScreenPanel>();

		var sp = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( sp.IsValid() )
		{
			sp.AutoScreenScale = true;
			sp.ZIndex = 50;
		}

		if ( !Panel.IsValid() )
			return;

		Panel.AddClass( "ya-player-hud" );
		Panel.Style.Width = Length.Fraction( 1f );
		Panel.Style.Height = Length.Fraction( 1f );
		Panel.Style.PointerEvents = PointerEvents.None;

		BuildTree();
		_treeReady = true;

		FlushPendingHitmarkerIfAny();

		if ( _pendingDamageFlash >= 0f )
		{
			var d = _pendingDamageFlash;
			var yaw = _pendingDamageYawDelta;
			_pendingDamageFlash = -1f;
			_pendingDamageYawDelta = 0f;
			ApplyDamageFlash( d, yaw );
		}

		TryShowFirstRunOnboarding();
		InitializeUiManager();
	}

	void BuildTree()
	{
		_modalScrimRoot = Panel.AddChild<Panel>( "ya-ui-modal-scrim" );
		_modalScrimRoot.Style.Display = DisplayMode.None;
		_modalScrimRoot.Style.Position = PositionMode.Absolute;
		_modalScrimRoot.Style.Left = 0;
		_modalScrimRoot.Style.Top = 0;
		_modalScrimRoot.Style.Width = Length.Fraction( 1f );
		_modalScrimRoot.Style.Height = Length.Fraction( 1f );
		_modalScrimRoot.Style.BackgroundColor = new Color( 0f, 0f, 0f, YaUiDesignTokens.ModalScrimOpacity );
		_modalScrimRoot.Style.PointerEvents = PointerEvents.None;

		_deathOverlay = Panel.AddChild<YaDeathOverlayPanel>();
		_deathOverlay.Style.Display = DisplayMode.None;

		_roundVictoryRoot = Panel.AddChild<Panel>( "ya-hud-round-victory" );
		_roundVictoryRoot.AddClass( "ya-hud-round-victory" );
		_roundVictoryRoot.Style.Display = DisplayMode.None;
		_roundVictoryRoot.Style.Position = PositionMode.Absolute;
		_roundVictoryRoot.Style.Left = 0;
		_roundVictoryRoot.Style.Top = 0;
		_roundVictoryRoot.Style.Width = Length.Fraction( 1f );
		_roundVictoryRoot.Style.Height = Length.Fraction( 1f );
		_roundVictoryRoot.Style.PointerEvents = PointerEvents.All;
		_roundVictoryRoot.Style.BackgroundColor = Color.Transparent;
		_roundVictoryRoot.Style.JustifyContent = Justify.Center;
		_roundVictoryRoot.Style.AlignItems = Align.Center;
		_roundVictoryRoot.Style.FlexDirection = FlexDirection.Column;

		var victoryCard = _roundVictoryRoot.AddChild<Panel>( "ya-hud-round-victory__card" );
		victoryCard.AddClass( "ya-hud-round-victory__card" );
		victoryCard.Style.FlexDirection = FlexDirection.Column;
		victoryCard.Style.AlignItems = Align.Center;
		victoryCard.Style.Padding = 28;
		victoryCard.Style.BackgroundColor = YaHudTheme.Panel;
		victoryCard.Style.BorderWidth = 2;
		victoryCard.Style.BorderColor = YaHudTheme.BorderStrong;

		_roundVictoryHeadline = victoryCard.AddChild( new Label( "", "ya-hud-round-victory__title" ) );
		_roundVictoryHeadline.Style.FontSize = 36;
		_roundVictoryHeadline.Style.FontWeight = 900;
		_roundVictoryHeadline.Style.FontColor = YaHudTheme.Teal;
		_roundVictoryHeadline.Style.TextAlign = TextAlign.Center;
	_roundVictoryHeadline.Style.MarginBottom = 12;

		_roundVictorySummary = victoryCard.AddChild( new Label( "", "ya-hud-round-victory__summary" ) );
		_roundVictorySummary.Style.FontSize = 16;
		_roundVictorySummary.Style.FontWeight = 600;
		_roundVictorySummary.Style.FontColor = YaHudTheme.TextSecondary;
		_roundVictorySummary.Style.TextAlign = TextAlign.Center;
		_roundVictorySummary.Style.MarginBottom = 8;

		_roundVictoryCountdown = victoryCard.AddChild( new Label( "", "ya-hud-round-victory__timer" ) );
		_roundVictoryCountdown.Style.FontSize = 22;
		_roundVictoryCountdown.Style.FontWeight = 700;
		_roundVictoryCountdown.Style.FontColor = YaHudTheme.TextSecondary;
		_roundVictoryCountdown.Style.TextAlign = TextAlign.Center;

		_roundStartAnnouncementRoot = Panel.AddChild<Panel>( "ya-hud-round-start" );
		_roundStartAnnouncementRoot.AddClass( "ya-hud-round-start" );
		_roundStartAnnouncementRoot.Style.Display = DisplayMode.None;
		_roundStartAnnouncementRoot.Style.Position = PositionMode.Absolute;
		_roundStartAnnouncementRoot.Style.Left = 0;
		_roundStartAnnouncementRoot.Style.Top = Length.Pixels( YaUiDesignTokens.NotificationTopCenterPx );
		_roundStartAnnouncementRoot.Style.Width = Length.Fraction( 1f );
		_roundStartAnnouncementRoot.Style.JustifyContent = Justify.Center;
		_roundStartAnnouncementRoot.Style.AlignItems = Align.Center;
		_roundStartAnnouncementRoot.Style.PointerEvents = PointerEvents.None;

		var roundStartCard = _roundStartAnnouncementRoot.AddChild<Panel>( "ya-hud-round-start__card" );
		roundStartCard.AddClass( "ya-hud-round-start__card" );
		_roundStartAnnouncementLabel = roundStartCard.AddChild( new Label( "", "ya-hud-round-start__title" ) );
		_roundStartAnnouncementLabel.Style.FontSize = 38;
		_roundStartAnnouncementLabel.Style.FontWeight = 900;
		_roundStartAnnouncementLabel.Style.FontColor = YaHudTheme.Teal;
		_roundStartAnnouncementLabel.Style.TextAlign = TextAlign.Center;

		_floatingMessageStackRoot = Panel.AddChild<Panel>( "ya-hud-floating-message-wrap" );
		_floatingMessageStackRoot.Style.Display = DisplayMode.None;
		_floatingMessageStackRoot.Style.Position = PositionMode.Absolute;
		_floatingMessageStackRoot.Style.Left = 0;
		_floatingMessageStackRoot.Style.Top = 0;
		_floatingMessageStackRoot.Style.Width = Length.Fraction( 1f );
		_floatingMessageStackRoot.Style.Height = Length.Fraction( 1f );
		_floatingMessageStackRoot.Style.PointerEvents = PointerEvents.None;

		_floatingPersonalRoot = _floatingMessageStackRoot.AddChild<Panel>( "ya-hud-floating-message-personal" );
		_floatingPersonalRoot.AddClass( "ya-hud-floating-message-personal" );
		_floatingPersonalRoot.Style.Position = PositionMode.Absolute;
		_floatingPersonalRoot.Style.Left = Length.Pixels( YaUiDesignTokens.ScreenEdgeInsetPx );
		_floatingPersonalRoot.Style.Bottom = Length.Pixels( YaUiDesignTokens.NotificationBottomLeftAboveHealthPx );
		_floatingPersonalRoot.Style.MaxWidth = Length.Pixels( 320 );
		_floatingPersonalRoot.Style.FlexDirection = FlexDirection.Column;
		_floatingPersonalRoot.Style.AlignItems = Align.FlexStart;
		_floatingPersonalRoot.Style.PointerEvents = PointerEvents.None;

		_floatingCombatRoot = _floatingMessageStackRoot.AddChild<Panel>( "ya-hud-floating-message-combat" );
		_floatingCombatRoot.AddClass( "ya-hud-floating-message-combat" );
		_floatingCombatRoot.Style.Position = PositionMode.Absolute;
		_floatingCombatRoot.Style.Right = Length.Pixels( YaUiDesignTokens.ScreenEdgeInsetPx );
		_floatingCombatRoot.Style.Top = Length.Pixels( YaUiDesignTokens.NotificationTopRightBelowFeedPx );
		_floatingCombatRoot.Style.MaxWidth = Length.Pixels( 360 );
		_floatingCombatRoot.Style.FlexDirection = FlexDirection.Column;
		_floatingCombatRoot.Style.AlignItems = Align.FlexEnd;
		_floatingCombatRoot.Style.PointerEvents = PointerEvents.None;

		_damageFeedbackRoot = Panel.AddChild<Panel>( "ya-hud-damage-feedback-root" );
		_damageFeedbackRoot.Style.Display = DisplayMode.None;
		_damageFeedbackRoot.Style.Position = PositionMode.Absolute;
		_damageFeedbackRoot.Style.Left = 0;
		_damageFeedbackRoot.Style.Top = 0;
		_damageFeedbackRoot.Style.Width = Length.Fraction( 1f );
		_damageFeedbackRoot.Style.Height = Length.Fraction( 1f );
		_damageFeedbackRoot.Style.PointerEvents = PointerEvents.None;

		_damageVignetteRoot = _damageFeedbackRoot.AddChild<Panel>( "ya-hud-damage-vignette" );
		_damageVignetteRoot.Style.Display = DisplayMode.None;
		_damageVignetteRoot.Style.Position = PositionMode.Absolute;
		_damageVignetteRoot.Style.Left = 0;
		_damageVignetteRoot.Style.Top = 0;
		_damageVignetteRoot.Style.Width = Length.Fraction( 1f );
		_damageVignetteRoot.Style.Height = Length.Fraction( 1f );
		_damageVignetteRoot.Style.PointerEvents = PointerEvents.None;
		_damageVignetteRoot.Style.Opacity = 1f;

		_damageDirectionRoot = _damageFeedbackRoot.AddChild<Panel>( "ya-hud-damage-direction" );
		_damageDirectionRoot.Style.Display = DisplayMode.None;
		_damageDirectionRoot.Style.Position = PositionMode.Absolute;
		_damageDirectionRoot.Style.Left = Length.Fraction( 0.5f );
		_damageDirectionRoot.Style.Top = Length.Pixels( 120 );
		_damageDirectionRoot.Style.Width = Length.Pixels( 36 );
		_damageDirectionRoot.Style.Height = Length.Pixels( 36 );
		_damageDirectionRoot.Style.MarginLeft = Length.Pixels( -18 );
		var chevron = _damageDirectionRoot.AddChild( new Label( "▲", "ya-hud-damage-direction__chev" ) );
		chevron.Style.FontSize = 28;
		chevron.Style.FontWeight = 900;
		chevron.Style.FontColor = new Color( 1f, 0.35f, 0.28f, 1f );

		_headshotFlashRoot = _damageFeedbackRoot.AddChild<Panel>( "ya-hud-headshot-flash" );
		_headshotFlashRoot.Style.Display = DisplayMode.None;
		_headshotFlashRoot.Style.Position = PositionMode.Absolute;
		_headshotFlashRoot.Style.Left = 0;
		_headshotFlashRoot.Style.Top = 0;
		_headshotFlashRoot.Style.Width = Length.Fraction( 1f );
		_headshotFlashRoot.Style.Height = Length.Fraction( 1f );
		_headshotFlashRoot.Style.PointerEvents = PointerEvents.None;
		_headshotFlashRoot.Style.BackgroundColor = new Color( 1f, 0.86f, 0.2f, 0.12f );

		_paranoiaOverlayRoot = Panel.AddChild<Panel>( "ya-hud-paranoia-overlay-root" );
		_paranoiaOverlayRoot.Style.Display = DisplayMode.None;
		_paranoiaOverlayRoot.Style.Position = PositionMode.Absolute;
		_paranoiaOverlayRoot.Style.Left = 0;
		_paranoiaOverlayRoot.Style.Top = 0;
		_paranoiaOverlayRoot.Style.Width = Length.Fraction( 1f );
		_paranoiaOverlayRoot.Style.Height = Length.Fraction( 1f );
		_paranoiaOverlayRoot.Style.PointerEvents = PointerEvents.None;

		_paranoiaBrightnessOverlay = _paranoiaOverlayRoot.AddChild<Panel>( "ya-hud-paranoia-brightness" );
		_paranoiaBrightnessOverlay.Style.Display = DisplayMode.None;
		_paranoiaBrightnessOverlay.Style.Position = PositionMode.Absolute;
		_paranoiaBrightnessOverlay.Style.Left = 0;
		_paranoiaBrightnessOverlay.Style.Top = 0;
		_paranoiaBrightnessOverlay.Style.Width = Length.Fraction( 1f );
		_paranoiaBrightnessOverlay.Style.Height = Length.Fraction( 1f );
		_paranoiaBrightnessOverlay.Style.BackgroundColor = new Color( 0f, 0f, 0f, 1f );
		_paranoiaBrightnessOverlay.Style.PointerEvents = PointerEvents.None;

		_paranoiaBrightnessOverlayExtra = _paranoiaOverlayRoot.AddChild<Panel>( "ya-hud-paranoia-brightness-extra" );
		_paranoiaBrightnessOverlayExtra.Style.Display = DisplayMode.None;
		_paranoiaBrightnessOverlayExtra.Style.Position = PositionMode.Absolute;
		_paranoiaBrightnessOverlayExtra.Style.Left = 0;
		_paranoiaBrightnessOverlayExtra.Style.Top = 0;
		_paranoiaBrightnessOverlayExtra.Style.Width = Length.Fraction( 1f );
		_paranoiaBrightnessOverlayExtra.Style.Height = Length.Fraction( 1f );
		_paranoiaBrightnessOverlayExtra.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0.5f );
		_paranoiaBrightnessOverlayExtra.Style.PointerEvents = PointerEvents.None;

		_paranoiaHunterFullscreenBlur = _paranoiaOverlayRoot.AddChild<Panel>( "ya-hud-paranoia-hunter-fullscreen-blur" );
		_paranoiaHunterFullscreenBlur.Style.Display = DisplayMode.None;
		_paranoiaHunterFullscreenBlur.Style.Position = PositionMode.Absolute;
		_paranoiaHunterFullscreenBlur.Style.Left = 0;
		_paranoiaHunterFullscreenBlur.Style.Top = 0;
		_paranoiaHunterFullscreenBlur.Style.Width = Length.Fraction( 1f );
		_paranoiaHunterFullscreenBlur.Style.Height = Length.Fraction( 1f );
		_paranoiaHunterFullscreenBlur.Style.BackgroundColor = new Color( 0.04f, 0.03f, 0.07f, 0.06f );
		_paranoiaHunterFullscreenBlur.Style.BackdropFilterBlur = Length.Pixels( (int)ParanoiaHunterFullscreenBlurPixels );
		_paranoiaHunterFullscreenBlur.Style.Overflow = OverflowMode.Hidden;
		_paranoiaHunterFullscreenBlur.Style.PointerEvents = PointerEvents.None;

		_lobbySoloHintWrap = Panel.AddChild<Panel>( "ya-hud-lobby-solo-hint" );
		_lobbySoloHintWrap.Style.Display = DisplayMode.None;
		_lobbySoloHintWrap.Style.Position = PositionMode.Absolute;
		_lobbySoloHintWrap.Style.Left = 0;
		_lobbySoloHintWrap.Style.Top = 78;
		_lobbySoloHintWrap.Style.Width = Length.Fraction( 1f );
		_lobbySoloHintWrap.Style.Height = Length.Pixels( 72 );
		_lobbySoloHintWrap.Style.JustifyContent = Justify.Center;
		_lobbySoloHintWrap.Style.AlignItems = Align.Center;
		_lobbySoloHintWrap.Style.FlexDirection = FlexDirection.Column;
		_lobbySoloHintWrap.Style.PointerEvents = PointerEvents.None;

		_lobbySoloHint = _lobbySoloHintWrap.AddChild( new Label( "", "ya-hud-lobby-solo-hint__line" ) );
		_lobbySoloHint.Style.FontSize = 26;
		_lobbySoloHint.Style.FontWeight = 900;
		_lobbySoloHint.Style.FontColor = YaHudTheme.TextOnTeal;
		_lobbySoloHint.Style.MaxWidth = Length.Pixels( 1240 );
		_lobbySoloHint.Style.TextAlign = TextAlign.Center;

		_scoreboardRoot = Panel.AddChild<Panel>( "ya-scoreboard" );
		_scoreboardRoot.Style.Display = DisplayMode.None;
		_scoreboardRoot.Style.Position = PositionMode.Absolute;
		_scoreboardRoot.Style.Left = 0;
		_scoreboardRoot.Style.Top = 0;
		_scoreboardRoot.Style.Width = Length.Fraction( 1f );
		_scoreboardRoot.Style.Height = Length.Fraction( 1f );
		_scoreboardRoot.Style.AlignItems = Align.Center;
		_scoreboardRoot.Style.JustifyContent = Justify.Center;
		_scoreboardRoot.Style.PointerEvents = PointerEvents.All;
		_scoreboardRoot.Style.BackgroundColor = new Color( 0.02f, 0.03f, 0.05f, 0.88f );

		var scoreboardCard = _scoreboardRoot.AddChild<Panel>( "ya-scoreboard__card" );
		var header = scoreboardCard.AddChild<Panel>( "ya-scoreboard__header" );
		header.AddChild( new Label( "Player", "ya-scoreboard__headcell ya-scoreboard__headcell--name" ) );
		header.AddChild( new Label( "Kills", "ya-scoreboard__headcell" ) );
		header.AddChild( new Label( "Deaths", "ya-scoreboard__headcell" ) );
		header.AddChild( new Label( "Wins", "ya-scoreboard__headcell" ) );

		_scoreboardRows = scoreboardCard.AddChild<Panel>( "ya-scoreboard__rows" );

		_controlsTutorial = Panel.AddChild<YaControlsTutorialPanel>();
		_controlsTutorial.Style.Display = DisplayMode.None;

		_practiceChoiceRoot = Panel.AddChild<Panel>( "ya-practice-choice" );
		_practiceChoiceRoot.Style.Display = DisplayMode.None;
		_practiceChoiceRoot.Style.Position = PositionMode.Absolute;
		_practiceChoiceRoot.Style.Left = 0;
		_practiceChoiceRoot.Style.Top = 0;
		_practiceChoiceRoot.Style.Width = Length.Fraction( 1f );
		_practiceChoiceRoot.Style.Height = Length.Fraction( 1f );
		_practiceChoiceRoot.Style.AlignItems = Align.Center;
		_practiceChoiceRoot.Style.JustifyContent = Justify.Center;
		_practiceChoiceRoot.Style.PointerEvents = PointerEvents.All;

		var practiceCard = _practiceChoiceRoot.AddChild<Panel>( "ya-practice-choice__card" );
		practiceCard.AddChild( new Label( "Solo Practice", "ya-practice-choice__title" ) );
		practiceCard.AddChild( new Label( "Choose your side (click or press 1 / 2):", "ya-practice-choice__sub" ) );
		var row = practiceCard.AddChild<Panel>( "ya-practice-choice__row" );

		var aloneBtn = row.AddChild( new PracticeRoleButtonPanel
		{
			Hud = this,
			Role = YaPlayerRole.Alone
		} );
		aloneBtn.AddChild( new Label( "Play as Alone", "ya-practice-choice__button-label" ) );
		practiceCard.AddChild( new Label( "Alone: you are the monster. Hunt down every Not Alone player.", "ya-practice-choice__blurb" ) );

		var hunterBtn = row.AddChild( new PracticeRoleButtonPanel
		{
			Hud = this,
			Role = YaPlayerRole.NotAlone
		} );
		hunterBtn.AddChild( new Label( "Play as Not Alone", "ya-practice-choice__button-label" ) );
		practiceCard.AddChild( new Label( "Not Alone: you are a hunter. Track and survive against the Alone.", "ya-practice-choice__blurb" ) );

		// --- Top center: match (replicated state only) ---
		_topObjectiveRoot = Panel.AddChild<Panel>( "ya-hud-top-stack" );
		_topObjectiveRoot.Style.Position = PositionMode.Absolute;
		_topObjectiveRoot.Style.Top = YaUiDesignTokens.ScreenEdgeInsetPx;
		_topObjectiveRoot.Style.Left = 0;
		_topObjectiveRoot.Style.Width = Length.Fraction( 1f );
		_topObjectiveRoot.Style.JustifyContent = Justify.Center;
		_topObjectiveRoot.Style.AlignItems = Align.Center;
		_topObjectiveRoot.Style.FlexDirection = FlexDirection.Column;
		_topObjectiveRoot.Style.PointerEvents = PointerEvents.None;

		_timerPanel = _topObjectiveRoot.AddChild<YaTimerPanel>();
		_mutatorLabel = _topObjectiveRoot.AddChild( new Label( "", "ya-hud-mutator-label" ) );
		_mutatorLabel.Style.FontSize = YaUiDesignTokens.TopStackMutatorFontPx;
		_mutatorLabel.Style.FontWeight = 700;
		_mutatorLabel.Style.FontColor = YaHudTheme.TextMuted;
		_mutatorLabel.Style.TextAlign = TextAlign.Center;
		_mutatorLabel.Style.Width = Length.Fraction( 1f );
		_mutatorLabel.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );
		_mutatorLabel.Style.MarginTop = YaUiDesignTokens.TopStackRowGapPx;
		_intermissionReadyLabel = _topObjectiveRoot.AddChild( new Label( "", "ya-hud-intermission-ready" ) );
		_intermissionReadyLabel.Style.FontSize = YaUiDesignTokens.TopStackIntermissionFontPx;
		_intermissionReadyLabel.Style.FontWeight = 700;
		_intermissionReadyLabel.Style.FontColor = YaHudTheme.Teal;
		_intermissionReadyLabel.Style.TextAlign = TextAlign.Center;
		_intermissionReadyLabel.Style.Width = Length.Fraction( 1f );
		_intermissionReadyLabel.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );
		_intermissionReadyLabel.Style.MarginTop = YaUiDesignTokens.TopStackRowGapPx;
		_rolePanel = _topObjectiveRoot.AddChild<YaRolePanel>();
		_objectivePanel = _topObjectiveRoot.AddChild<YaObjectivePanel>();
		_alivePanel = _topObjectiveRoot.AddChild<YaAliveCounterPanel>();
		_aloneStatusPanel = _topObjectiveRoot.AddChild<YaAloneStatusPanel>();
		_aloneStatusPanel.Style.Display = DisplayMode.None;

		// --- Top right: kill feed ---
		_killFeed = Panel.AddChild<YaKillFeedPanel>();
		_killFeed.Style.Position = PositionMode.Absolute;
		_killFeed.Style.Top = YaUiDesignTokens.ScreenEdgeInsetPx;
		_killFeed.Style.Right = YaUiDesignTokens.ScreenEdgeInsetPx;
		_killFeed.Style.MaxWidth = Length.Pixels( 420 );

		// --- Bottom-left: health / stamina / ability status bars ---
		_combatHudRoot = Panel.AddChild<Panel>( "ya-hud-health-anchor" );
		_combatHudRoot.Style.Position = PositionMode.Absolute;
		_combatHudRoot.Style.Bottom = YaUiDesignTokens.ScreenEdgeInsetPx;
		_combatHudRoot.Style.Left = YaUiDesignTokens.ScreenEdgeInsetPx;
		_combatHudRoot.Style.FlexDirection = FlexDirection.Column;
		_combatHudRoot.Style.PointerEvents = PointerEvents.None;

		var card = _combatHudRoot.AddChild<Panel>( "ya-hud-health-card" );
		_healthCardPanel = card;
		card.AddClass( "ya-hud-health-card" );
		card.Style.BackgroundColor = YaHudTheme.Panel;
		card.Style.BorderWidth = 1;
		card.Style.BorderColor = YaHudTheme.Border;
		card.Style.Padding = 10;
		card.Style.FlexDirection = FlexDirection.Column;
		card.Style.MinWidth = HealthBarWidth;

		var wrap = card.AddChild<Panel>( "ya-hud-health-track" );
		wrap.Style.Width = HealthBarWidth - 24;
		wrap.Style.Height = 18;
		wrap.Style.BackgroundColor = YaHudTheme.TrackBg;
		wrap.Style.Overflow = OverflowMode.Hidden;
		wrap.Style.MarginBottom = 4;

		_healthFill = wrap.AddChild<Panel>( "ya-hud-health-fill" );
		_healthFill.AddClass( "ya-hud-health-fill" );
		_healthFill.Style.Position = PositionMode.Absolute;
		_healthFill.Style.Left = 0;
		_healthFill.Style.Top = 0;
		_healthFill.Style.Bottom = 0;
		_healthFill.Style.Width = Length.Fraction( 1f );
		_healthFill.Style.BackgroundColor = YaHudTheme.Health;

		_staminaTrack = card.AddChild<Panel>( "ya-hud-stamina-track" );
		_staminaTrack.Style.Display = DisplayMode.None;
		_staminaTrack.Style.Width = Length.Pixels( (int)(HealthBarWidth - 24) );
		_staminaTrack.Style.Height = 12;
		_staminaTrack.Style.MarginBottom = 4;
		_staminaTrack.Style.BackgroundColor = YaHudTheme.TrackBg;
		_staminaTrack.Style.BorderWidth = 1;
		_staminaTrack.Style.BorderColor = YaHudTheme.Border;
		_staminaTrack.Style.Overflow = OverflowMode.Hidden;

		_staminaFill = _staminaTrack.AddChild<Panel>( "ya-hud-stamina-fill" );
		_staminaFill.Style.Position = PositionMode.Absolute;
		_staminaFill.Style.Left = 0;
		_staminaFill.Style.Top = 0;
		_staminaFill.Style.Bottom = 0;
		_staminaFill.Style.Width = Length.Fraction( 1f );
		_staminaFill.Style.BackgroundColor = YaHudTheme.MeterStamina;

		_dashCooldownTrack = card.AddChild<Panel>( "ya-hud-dash-cooldown-track" );
		_dashCooldownTrack.Style.Display = DisplayMode.None;
		_dashCooldownTrack.Style.Width = Length.Pixels( (int)(HealthBarWidth - 24) );
		_dashCooldownTrack.Style.Height = 12;
		_dashCooldownTrack.Style.MarginBottom = 4;
		_dashCooldownTrack.Style.BackgroundColor = YaHudTheme.TrackBg;
		_dashCooldownTrack.Style.BorderWidth = 1;
		_dashCooldownTrack.Style.BorderColor = YaHudTheme.Border;
		_dashCooldownTrack.Style.Overflow = OverflowMode.Hidden;

		_dashCooldownFill = _dashCooldownTrack.AddChild<Panel>( "ya-hud-dash-cooldown-fill" );
		_dashCooldownFill.Style.Position = PositionMode.Absolute;
		_dashCooldownFill.Style.Left = 0;
		_dashCooldownFill.Style.Top = 0;
		_dashCooldownFill.Style.Bottom = 0;
		_dashCooldownFill.Style.Width = Length.Fraction( 1f );
		_dashCooldownFill.Style.BackgroundColor = YaHudTheme.MeterDash;

		_paranoiaHunterDebuffTrack = card.AddChild<Panel>( "ya-hud-paranoia-hunter-debuff-track" );
		_paranoiaHunterDebuffTrack.Style.Display = DisplayMode.None;
		_paranoiaHunterDebuffTrack.Style.Width = Length.Pixels( (int)(HealthBarWidth - 24) );
		_paranoiaHunterDebuffTrack.Style.Height = 10;
		_paranoiaHunterDebuffTrack.Style.MarginBottom = 4;
		_paranoiaHunterDebuffTrack.Style.BackgroundColor = new Color( 0.06f, 0.04f, 0.08f, 1f );
		_paranoiaHunterDebuffTrack.Style.BorderWidth = 1;
		_paranoiaHunterDebuffTrack.Style.BorderColor = YaHudTheme.Border;
		_paranoiaHunterDebuffTrack.Style.Overflow = OverflowMode.Hidden;

		_paranoiaHunterDebuffFill = _paranoiaHunterDebuffTrack.AddChild<Panel>( "ya-hud-paranoia-hunter-debuff-fill" );
		_paranoiaHunterDebuffFill.Style.Position = PositionMode.Absolute;
		_paranoiaHunterDebuffFill.Style.Left = 0;
		_paranoiaHunterDebuffFill.Style.Top = 0;
		_paranoiaHunterDebuffFill.Style.Bottom = 0;
		_paranoiaHunterDebuffFill.Style.Width = Length.Fraction( 1f );
		_paranoiaHunterDebuffFill.Style.BackgroundColor = YaHudTheme.MeterParanoia;

		_m2ChargeTrack = card.AddChild<Panel>( "ya-hud-m2-charge-track" );
		_m2ChargeTrack.Style.Display = DisplayMode.None;
		_m2ChargeTrack.Style.Width = Length.Pixels( (int)(HealthBarWidth - 24) );
		_m2ChargeTrack.Style.Height = 10;
		_m2ChargeTrack.Style.MarginBottom = 6;
		_m2ChargeTrack.Style.BackgroundColor = YaHudTheme.TrackBg;
		_m2ChargeTrack.Style.BorderWidth = 1;
		_m2ChargeTrack.Style.BorderColor = YaHudTheme.Border;
		_m2ChargeTrack.Style.Overflow = OverflowMode.Hidden;

		_m2ChargeFill = _m2ChargeTrack.AddChild<Panel>( "ya-hud-m2-charge-fill" );
		_m2ChargeFill.Style.Position = PositionMode.Absolute;
		_m2ChargeFill.Style.Left = 0;
		_m2ChargeFill.Style.Top = 0;
		_m2ChargeFill.Style.Bottom = 0;
		_m2ChargeFill.Style.Width = Length.Fraction( 0f );
		_m2ChargeFill.Style.BackgroundColor = YaHudTheme.MeterCharge;

		_hpReadout = card.AddChild( new Label( "— / —", "ya-hud-hp-readout" ) );
		_hpReadout.Style.FontSize = 13;
		_hpReadout.Style.FontWeight = 700;
		_hpReadout.Style.FontColor = YaHudTheme.TextPrimary;

		// --- Center: crosshair + hitmarkers (host-confirmed hits) ---
		_crosshairRoot = Panel.AddChild<Panel>( "ya-hud-crosshair" );
		var cx = _crosshairRoot;
		cx.Style.Position = PositionMode.Absolute;
		cx.Style.Left = 0;
		cx.Style.Top = 0;
		cx.Style.Width = Length.Fraction( 1f );
		cx.Style.Height = Length.Fraction( 1f );
		cx.Style.JustifyContent = Justify.Center;
		cx.Style.AlignItems = Align.Center;
		cx.Style.PointerEvents = PointerEvents.None;

		const float hmSize = 72f;
		const float hmHalf = hmSize * 0.5f;
		_hitmarkerRoot = cx.AddChild<Panel>( "ya-hud-hitmarker" );
		_hitmarkerRoot.AddClass( "ya-hud-hitmarker" );
		_hitmarkerRoot.Style.Display = DisplayMode.None;
		_hitmarkerRoot.Style.Position = PositionMode.Absolute;
		_hitmarkerRoot.Style.Left = Length.Fraction( 0.5f );
		_hitmarkerRoot.Style.Top = Length.Fraction( 0.5f );
		_hitmarkerRoot.Style.Width = hmSize;
		_hitmarkerRoot.Style.Height = hmSize;
		_hitmarkerRoot.Style.MarginLeft = Length.Pixels( -hmHalf - 0.5f );
		_hitmarkerRoot.Style.MarginTop = Length.Pixels( -hmHalf - 1f );
		_hitmarkerRoot.Style.Opacity = 1f;
		_hitmarkerRoot.Style.PointerEvents = PointerEvents.None;

		for ( var i = 0; i < 2; i++ )
		{
			var leg = _hitmarkerRoot.AddChild<Panel>( $"ya-hud-hitmarker-x-leg-{i}" );
			leg.AddClass( "ya-hud-hitmarker-x-leg" );
			leg.AddClass( i == 0 ? "ya-hud-hitmarker-x-leg--a" : "ya-hud-hitmarker-x-leg--b" );
			leg.Style.BackgroundColor = HitmarkerBodyColor;
			_hitmarkerXLegs[i] = leg;
		}

		var dot = cx.AddChild<Panel>( "ya-hud-crosshair-dot" );
		dot.Style.Width = 3;
		dot.Style.Height = 3;
		dot.Style.BackgroundColor = new Color( 1f, 1f, 1f, 0.85f );

		// --- Bottom: weapon name + ammo + 3 slots ---
		_weaponDock = Panel.AddChild<Panel>( "ya-hud-dock" );
		var dock = _weaponDock;
		dock.Style.Position = PositionMode.Absolute;
		dock.Style.Bottom = YaUiDesignTokens.ScreenEdgeInsetPx;
		dock.Style.Left = 0;
		dock.Style.Width = Length.Fraction( 1f );
		dock.Style.JustifyContent = Justify.Center;
		dock.Style.AlignItems = Align.Center;
		dock.Style.FlexDirection = FlexDirection.Column;
		dock.Style.PointerEvents = PointerEvents.All;

		_mimicProgressRoot = dock.AddChild<Panel>( "ya-hud-mimic-progress" );
		_mimicProgressRoot.AddClass( "ya-hud-mimic-progress" );
		_mimicProgressRoot.Style.Display = DisplayMode.None;
		_mimicProgressRoot.Style.FlexDirection = FlexDirection.Column;
		_mimicProgressRoot.Style.AlignItems = Align.Center;
		_mimicProgressRoot.Style.MarginBottom = 10;
		_mimicProgressRoot.Style.PointerEvents = PointerEvents.None;

		_mimicProgressLabel = _mimicProgressRoot.AddChild( new Label( "Mimic in Progress:", "ya-hud-mimic-progress__label" ) );
		_mimicProgressLabel.AddClass( "ya-hud-mimic-progress__label" );
		_mimicProgressLabel.Style.FontSize = 14;
		_mimicProgressLabel.Style.FontWeight = 900;
		_mimicProgressLabel.Style.FontColor = YaHudTheme.Teal;
		_mimicProgressLabel.Style.MarginBottom = 6;
		_mimicProgressLabel.Style.TextAlign = TextAlign.Center;
		_mimicProgressLabel.Style.LetterSpacing = Length.Pixels( 1 );

		_mimicProgressTrack = _mimicProgressRoot.AddChild<Panel>( "ya-hud-mimic-progress__track" );
		_mimicProgressTrack.AddClass( "ya-hud-mimic-progress__track" );
		_mimicProgressTrack.Style.Width = Length.Pixels( 300 );
		_mimicProgressTrack.Style.Height = 16;
		_mimicProgressTrack.Style.BackgroundColor = YaHudTheme.PanelDeep;
		_mimicProgressTrack.Style.BorderWidth = 2;
		_mimicProgressTrack.Style.BorderColor = YaHudTheme.TealStrong;
		_mimicProgressTrack.Style.Overflow = OverflowMode.Hidden;

		_mimicProgressFill = _mimicProgressTrack.AddChild<Panel>( "ya-hud-mimic-progress__fill" );
		_mimicProgressFill.AddClass( "ya-hud-mimic-progress__fill" );
		_mimicProgressFill.Style.Position = PositionMode.Absolute;
		_mimicProgressFill.Style.Left = 0;
		_mimicProgressFill.Style.Top = 0;
		_mimicProgressFill.Style.Bottom = 0;
		_mimicProgressFill.Style.Width = Length.Fraction( 1f );
		_mimicProgressFill.Style.BackgroundColor = YaHudTheme.MeterMimic;

		var rail = dock.AddChild<Panel>( "ya-hud-rail" );
		_weaponRail = rail;
		rail.AddClass( "ya-hud-rail" );
		rail.Style.FlexDirection = FlexDirection.Column;
		rail.Style.AlignItems = Align.Center;
		rail.Style.BackgroundColor = YaHudTheme.Panel;
		rail.Style.BorderTopWidth = 2;
		rail.Style.BorderTopColor = YaHudTheme.TealStrong;
		rail.Style.BorderBottomWidth = 1;
		rail.Style.BorderBottomColor = YaHudTheme.Border;
		rail.Style.Padding = 10;
		rail.Style.MinWidth = 320;

		var weaponRow = rail.AddChild<Panel>( "ya-hud-weapon-row" );
		_weaponTitleRow = weaponRow;
		weaponRow.Style.FlexDirection = FlexDirection.Row;
		weaponRow.Style.AlignItems = Align.Center;
		weaponRow.Style.JustifyContent = Justify.Center;
		weaponRow.Style.MarginBottom = 6;
		weaponRow.Style.PointerEvents = PointerEvents.None;

		_weaponNameLbl = weaponRow.AddChild( new Label( "—", "ya-hud-weapon-name" ) );
		_weaponNameLbl.Style.FontSize = 14;
		_weaponNameLbl.Style.FontWeight = 900;
		_weaponNameLbl.Style.FontColor = YaHudTheme.TextPrimary;
		_weaponNameLbl.Style.MarginRight = 12;

		_ammoReadout = weaponRow.AddChild( new Label( "", "ya-hud-ammo" ) );
		_ammoReadout.Style.FontSize = 13;
		_ammoReadout.Style.FontColor = YaHudTheme.TextSecondary;

		var slotRow = rail.AddChild<Panel>( "ya-hud-slots" );
		_defaultSlotsRow = slotRow;
		slotRow.Style.FlexDirection = FlexDirection.Row;
		slotRow.Style.JustifyContent = Justify.Center;
		slotRow.Style.PointerEvents = PointerEvents.All;

		string[] names = { "M4", "Shotgun", "Bayonet" };
		for ( var i = 0; i < 3; i++ )
		{
			var idx = i;
			var cell = slotRow.AddChild( new HotbarSlotPanel
			{
				SlotIndex = idx,
				Hud = this
			} );
			_slotCells[i] = cell;

			cell.Style.Width = SlotPx;
			cell.Style.Height = SlotPx;
			cell.Style.MarginLeft = i == 0 ? 0 : 6;
			cell.Style.FlexDirection = FlexDirection.Column;
			cell.Style.JustifyContent = Justify.Center;
			cell.Style.AlignItems = Align.Center;
			cell.Style.Padding = 4;
			cell.Style.PointerEvents = PointerEvents.All;

			var key = cell.AddChild( new Label( $"{i + 1}", "ya-hud-slot-key" ) );
			key.Style.FontSize = 9;
			key.Style.FontColor = YaHudTheme.TextMuted;
			key.Style.PointerEvents = PointerEvents.None;

			var body = cell.AddChild( new Label( names[i], "ya-hud-slot-name" ) );
			cell.NameLabel = body;
			body.Style.FontSize = 10;
			body.Style.FontWeight = 700;
			body.Style.FontColor = YaHudTheme.TextPrimary;
			body.Style.TextAlign = TextAlign.Center;
			body.Style.PointerEvents = PointerEvents.None;
		}

		_aloneAbilitiesRow = rail.AddChild<Panel>( "ya-hud-alone-abilities" );
		_aloneAbilitiesRow.Style.Display = DisplayMode.None;
		_aloneAbilitiesRow.Style.FlexDirection = FlexDirection.Row;
		_aloneAbilitiesRow.Style.JustifyContent = Justify.Center;
		_aloneAbilitiesRow.Style.PointerEvents = PointerEvents.None;
		_aloneAbilitiesRow.Style.MarginBottom = 2;

		string[] aloneLbl = { "LMB", "RMB", "Q", "E", "F" };
		for ( var a = 0; a < 5; a++ )
		{
			var cell = _aloneAbilitiesRow.AddChild( new AloneAbilityCellPanel() );
			cell.Style.MarginLeft = a == 0 ? 0 : 6;
			cell.KeyLabel.Text = aloneLbl[a];
			cell.ApplyFill01( 1f );
			_aloneAbilityCells[a] = cell;
		}

		_hotbarHint = rail.AddChild( new Label( "1 / 2 / 3  —  weapons  ·  R reload", "ya-hud-hint" ) );
		_hotbarHint.AddClass( "ya-hud-hint" );
		_hotbarHint.Style.FontSize = 11;
		_hotbarHint.Style.FontColor = YaHudTheme.TextMuted;
		_hotbarHint.Style.MarginTop = 8;
		_hotbarHint.Style.PointerEvents = PointerEvents.None;
		_hotbarHint.Style.WhiteSpace = WhiteSpace.Normal;
		_hotbarHint.Style.TextAlign = TextAlign.Center;

		// --- Top-left: TAB / C shortcuts ---
		_topLeftHintsRoot = Panel.AddChild<Panel>( "ya-hud-top-left-stack" );
		_topLeftHintsRoot.Style.Position = PositionMode.Absolute;
		_topLeftHintsRoot.Style.Top = YaUiDesignTokens.ScreenEdgeInsetPx;
		_topLeftHintsRoot.Style.Left = YaUiDesignTokens.ScreenEdgeInsetPx;
		_topLeftHintsRoot.Style.PointerEvents = PointerEvents.None;
		_topLeftHintsRoot.Style.FlexDirection = FlexDirection.Column;
		_topLeftHintsRoot.Style.AlignItems = Align.Stretch;

		_inputHintsPanel = _topLeftHintsRoot.AddChild<Panel>( "ya-hud-input-hints-panel" );
		_inputHintsPanel.AddClass( "ya-hud-input-hints-panel" );
		_inputHintsPanel.Style.PointerEvents = PointerEvents.None;
		_inputHintsPanel.Style.BackgroundColor = YaHudTheme.Panel;
		_inputHintsPanel.Style.BorderWidth = 1;
		_inputHintsPanel.Style.BorderColor = YaHudTheme.Border;
		_inputHintsPanel.Style.BorderTopWidth = 2;
		_inputHintsPanel.Style.BorderTopColor = YaHudTheme.TealStrong;
		_inputHintsPanel.Style.Padding = 10;
		_inputHintsPanel.Style.FlexDirection = FlexDirection.Column;
		_inputHintsPanel.Style.AlignItems = Align.Stretch;

		var hintsTitle = _inputHintsPanel.AddChild( new Label( "Shortcuts", "ya-hud-input-hints-panel__title" ) );
		hintsTitle.AddClass( "ya-hud-input-hints-panel__title" );
		hintsTitle.Style.FontSize = 10;
		hintsTitle.Style.FontWeight = 800;
		hintsTitle.Style.LetterSpacing = Length.Pixels( 2 );
		hintsTitle.Style.FontColor = YaHudTheme.Teal;
		hintsTitle.Style.MarginBottom = 8;
		hintsTitle.Style.TextAlign = TextAlign.Left;

		AddInputHintRow( _inputHintsPanel, "TAB", "Scoreboard" );
		AddInputHintRow( _inputHintsPanel, "C", "Controls" );
		AddInputHintRow( _inputHintsPanel, "X", "Ping (hunters)", isLast: true );
	}

	static void AddInputHintRow( Panel parent, string key, string action, bool isLast = false )
	{
		var row = parent.AddChild<Panel>( "ya-hud-input-hints-panel__row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		if ( !isLast )
			row.Style.MarginBottom = 6;

		var keyBadge = row.AddChild<Panel>( "ya-hud-input-hints-panel__key" );
		keyBadge.AddClass( "ya-hud-input-hints-panel__key" );
		keyBadge.Style.MinWidth = 44;
		keyBadge.Style.Height = 26;
		keyBadge.Style.PaddingLeft = 8;
		keyBadge.Style.PaddingRight = 8;
		keyBadge.Style.JustifyContent = Justify.Center;
		keyBadge.Style.AlignItems = Align.Center;
		keyBadge.Style.BackgroundColor = YaHudTheme.PanelDeep;
		keyBadge.Style.BorderWidth = 1;
		keyBadge.Style.BorderColor = YaHudTheme.BorderStrong;
		keyBadge.Style.FlexShrink = 0;

		var keyLbl = keyBadge.AddChild( new Label( key, "ya-hud-input-hints-panel__key-text" ) );
		keyLbl.AddClass( "ya-hud-input-hints-panel__key-text" );
		keyLbl.Style.FontSize = 11;
		keyLbl.Style.FontWeight = 900;
		keyLbl.Style.FontColor = YaHudTheme.Teal;
		keyLbl.Style.TextAlign = TextAlign.Center;

		var arrow = row.AddChild( new Label( "→", "ya-hud-input-hints-panel__arrow" ) );
		arrow.AddClass( "ya-hud-input-hints-panel__arrow" );
		arrow.Style.FontSize = 12;
		arrow.Style.FontWeight = 700;
		arrow.Style.FontColor = YaHudTheme.TealDim;
		arrow.Style.MarginLeft = 10;
		arrow.Style.MarginRight = 10;
		arrow.Style.FlexShrink = 0;

		var actionLbl = row.AddChild( new Label( action, "ya-hud-input-hints-panel__action" ) );
		actionLbl.Style.FontSize = 12;
		actionLbl.Style.FontWeight = 600;
		actionLbl.Style.FontColor = YaHudTheme.TextPrimary;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !_treeReady )
			TryInitializeLocalHud();
		if ( !_treeReady )
			return;

		var local = Connection.Local;
		if ( local is null || GameObject.Network.OwnerId != local.Id )
			return;

		var health = Components.Get<YaPlayerHealth>();
		// ScreenPanel defaults to a visible cursor; without this, Input.AnalogLook stays zeroed (no mouselook).
		// Mouse.Visibility set below once spectating / death is known.
		var hotbar = Components.Get<YaHotbarEquipment>();
		var weapon = Components.Get<YaWeapon>();
		var roleCmpHud = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var roleHud = roleCmpHud.IsValid() ? roleCmpHud.Role : YaPlayerRole.Unassigned;

		var isDead = health.IsValid() && health.IsDeadState;
		var onboardingForcedVisible = _onboardingAutoShown && !_onboardingDismissedByInput && Time.Now < _onboardingAutoHideAt;
		var controlsTutorialVisible = (IsControlsTutorialHeld() || onboardingForcedVisible)
		                              && YaUiInputRouter.CanOpenControls;
		TryDismissOnboarding();
		var scoreboardVisible = IsScoreboardHeld() && !controlsTutorialVisible && YaUiInputRouter.CanOpenScoreboard;

		var scene = GameObject.Scene;
		var gs = YaHudMatchSnapshot.TryGameState( scene );
		var connectedPlayers = YaTeamSystem.CountConnectedPlayers( scene );
		var inRoundClient = gs is { CurrentState: YaGameState.InRound };
		var showDeathOverlay = isDead && inRoundClient;
		var spectatingInRound = inRoundClient && ( isDead || roleHud == YaPlayerRole.Unassigned );
		var hideCombatHud = spectatingInRound;
		var practice = YaPracticeModeSystem.Instance;
		var hostAwaitingChoice = practice is { IsValid: true, AwaitingSideChoice: true, PracticeActive: false }
		                        && gs is { CurrentState: YaGameState.Lobby }
		                        && connectedPlayers == 1;
		var localAlreadyAssignedRole = roleHud == YaPlayerRole.Alone || roleHud == YaPlayerRole.NotAlone;
		var anyPracticeBotsPresent = false;
		if ( scene is { IsValid: true } )
		{
			foreach ( var bot in scene.GetAllComponents<YaBotBrain>() )
			{
				if ( bot.IsValid() )
				{
					anyPracticeBotsPresent = true;
					break;
				}
			}
		}
		var practiceLooksActiveLocally = practice is { IsValid: true, PracticeActive: true }
		                                || (connectedPlayers == 1 && localAlreadyAssignedRole && gs is { CurrentState: YaGameState.Lobby })
		                                || anyPracticeBotsPresent;
		var showCombatHudForAssignedRole = localAlreadyAssignedRole
		                                   && (inRoundClient || practiceLooksActiveLocally);
		var showPracticeChoice = hostAwaitingChoice
		                         && !_practiceChoiceDismissedLocal
		                         && !practiceLooksActiveLocally;

		// Only clear local dismissal once host state actually leaves choose-side mode.
		if ( !hostAwaitingChoice )
			_practiceChoiceDismissedLocal = false;

		if ( _deathOverlay.IsValid() && showDeathOverlay )
		{
			var countdownUi = YaHudMatchSnapshot.GetPhaseCountdownForUi( scene );
			_deathOverlay.Apply( Math.Max( 0, (int)Math.Ceiling( countdownUi ) ) );
		}

		if ( isDead && inRoundClient )
			_localKillStreak = 0;

		var showRoundVictory = gs is { IsValid: true, CurrentState: YaGameState.RoundVictory }
		                       && !string.IsNullOrWhiteSpace( gs.RoundVictoryBannerHeadline );

		if ( _roundVictoryRoot.IsValid() && _roundVictoryHeadline.IsValid() && _roundVictoryCountdown.IsValid() )
		{
			if ( showRoundVictory )
			{
				_roundVictoryHeadline.Text = gs!.RoundVictoryBannerHeadline;
				if ( !string.Equals( _lastVictoryHeadlineShown, gs.RoundVictoryBannerHeadline, StringComparison.Ordinal ) )
				{
					_lastVictoryHeadlineShown = gs.RoundVictoryBannerHeadline;
					TryPlayRoundVictorySting();
				}

				var stats = Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
				if ( _roundVictorySummary.IsValid() )
				{
					var parts = new List<string>();
					if ( stats.IsValid() )
						parts.Add( $"Session — {stats.SessionKills}K / {stats.SessionDeaths}D / {stats.SessionWins}W" );
					if ( stats.IsValid() && stats.WinStreak > 1 )
						parts.Add( $"Win streak: {stats.WinStreak}" );
					if ( gs.RoundMvpKillCount > 0 && !string.IsNullOrWhiteSpace( gs.RoundMvpDisplayName ) )
						parts.Add( $"MVP — {gs.RoundMvpDisplayName} ({gs.RoundMvpKillCount} kills)" );
					_roundVictorySummary.Text = string.Join( "  ·  ", parts );
				}

				var sec = Math.Max( 0, (int)Math.Ceiling( Math.Max( 0f, gs.RoundVictoryBannerSecondsRemaining ) - 0.001f ) );
				_roundVictoryCountdown.Text = sec > 0 ? $"Next round in {sec}s" : "";
			}
			else
			{
				_lastVictoryHeadlineShown = "";
			}
		}

		var damageFeedbackActive = _damageVignetteRoot.IsValid()
		                           && ( (_damageFlashEndRealtime - Time.Now) > 0.0
		                                || Time.Now < _damageDirectionEndAt
		                                || Time.Now < _headshotFlashEndAt );

		if ( _damageVignetteRoot.IsValid() )
		{
			var damageLayerVisible = !_uiReady || _uiManager.IsVisible( YaUiSurfaceId.PassiveDamage );
			var rem = _damageFlashEndRealtime - Time.Now;
			if ( damageLayerVisible && rem > 0.0 )
			{
				var fade01 = Math.Clamp( (float)(rem / DamageFlashDurationSeconds), 0f, 1f );
				_damageVignetteRoot.Style.Display = DisplayMode.Flex;
				_damageVignetteRoot.Style.Opacity = _damageFlashPeakOpacity * fade01;
			}
			else
			{
				_damageVignetteRoot.Style.Display = DisplayMode.None;
				_damageVignetteRoot.Style.Opacity = 1f;
			}
		}

		if ( _damageDirectionRoot.IsValid() )
		{
			var damageLayerVisible = !_uiReady || _uiManager.IsVisible( YaUiSurfaceId.PassiveDamage );
			_damageDirectionRoot.Style.Display = damageLayerVisible && Time.Now < _damageDirectionEndAt
				? DisplayMode.Flex
				: DisplayMode.None;
		}

		if ( _headshotFlashRoot.IsValid() )
		{
			var damageLayerVisible = !_uiReady || _uiManager.IsVisible( YaUiSurfaceId.PassiveDamage );
			var hsRem = _headshotFlashEndAt - Time.Now;
			if ( damageLayerVisible && hsRem > 0.0 )
			{
				_headshotFlashRoot.Style.Display = DisplayMode.Flex;
				_headshotFlashRoot.Style.Opacity = Math.Clamp( (float)(hsRem / 0.18), 0f, 0.85f );
			}
			else
				_headshotFlashRoot.Style.Display = DisplayMode.None;
		}

		if ( _crosshairRoot.IsValid() )
			_crosshairRoot.Style.Opacity = hideCombatHud ? 0f : 1f;

		if ( _hitmarkerRoot.IsValid() )
		{
			var hmRem = _hitmarkerEndRealtime - Time.Now;
			if ( hmRem > 0.0 )
			{
				var fade01 = Math.Clamp( (float)(hmRem / HitmarkerDurationSeconds), 0f, 1f );
				var ease = fade01 * fade01;
				_hitmarkerRoot.Style.Display = DisplayMode.Flex;
				_hitmarkerRoot.Style.Opacity = ease;
			}
			else if ( _hitmarkerRoot.Style.Display != DisplayMode.None )
			{
				_hitmarkerRoot.Style.Display = DisplayMode.None;
				_hitmarkerRoot.Style.Opacity = 1f;
			}
		}

		if ( _weaponTitleRow.IsValid() )
		{
			var dockShown = !hideCombatHud && showCombatHudForAssignedRole;
			_weaponTitleRow.Style.Display = dockShown && roleHud != YaPlayerRole.Alone ? DisplayMode.Flex : DisplayMode.None;
		}

		var remParanoiaGlobalEarly = gs is { IsValid: true } ? gs.ParanoiaDebuffSecondsRemaining : 0f;
		var paranoiaHunterOn = roleHud == YaPlayerRole.NotAlone
		                       && inRoundClient
		                       && !spectatingInRound
		                       && remParanoiaGlobalEarly > 0.02f;

		var showRoundStartAnnouncement = Time.Now < _roundStartAnnouncementEndAt;
		var practiceActiveForHint = practiceLooksActiveLocally;
		var soloLobby = gs is { CurrentState: YaGameState.Lobby, IsValid: true }
		                && connectedPlayers == 1
		                && !practiceActiveForHint;

		if ( _uiReady && _uiManager is not null )
			_uiManager.Popups.Tick( Time.Delta );

		var showFloatingMessages = _uiManager?.Popups.AnyVisible() == true;

		BuildUiFrameContext(
			isDead,
			inRoundClient,
			spectatingInRound,
			showPracticeChoice,
			controlsTutorialVisible,
			scoreboardVisible,
			showDeathOverlay,
			showCombatHudForAssignedRole,
			showRoundVictory,
			paranoiaHunterOn,
			showRoundStartAnnouncement,
			soloLobby,
			showFloatingMessages,
			damageFeedbackActive || (inRoundClient && !spectatingInRound && !isDead) );

		TickUiManager( Time.Delta );

		if ( showPracticeChoice )
		{
			if ( Input.Pressed( "Slot1" ) || Input.Keyboard.Pressed( "1" ) )
				TryRequestPracticeRole( YaPlayerRole.Alone );
			if ( Input.Pressed( "Slot2" ) || Input.Keyboard.Pressed( "2" ) )
				TryRequestPracticeRole( YaPlayerRole.NotAlone );
		}

		if ( _rolePanel.IsValid() )
			_rolePanel.ApplyFromRole( roleHud, spectatingInRound );

		ApplyRoleChromeTheme( roleHud );

		var remParanoiaGlobal = gs is { IsValid: true } ? gs.ParanoiaDebuffSecondsRemaining : 0f;
		if ( roleHud == YaPlayerRole.NotAlone && inRoundClient && !spectatingInRound && Game.IsPlaying )
		{
			const float edgeEps = 0.04f;
			if ( remParanoiaGlobal > edgeEps && _prevParanoiaDebuffSecondsRemaining <= edgeEps )
				TryPlayLocalParanoiaDebuffSting();
			if ( _prevParanoiaDebuffSecondsRemaining > edgeEps && remParanoiaGlobal <= edgeEps )
				NotifyFloatingMessageLocal( "+Stamina" );
		}

		_prevParanoiaDebuffSecondsRemaining = remParanoiaGlobal;

		if ( ( _paranoiaBrightnessOverlay.IsValid() || _paranoiaBrightnessOverlayExtra.IsValid()
		       || _paranoiaHunterFullscreenBlur.IsValid() )
		     && gs is not null && gs.IsValid() )
		{
			var dur = gs.ParanoiaDebuffDurationSeconds > 0.01f ? gs.ParanoiaDebuffDurationSeconds : 8f;
			var rem = gs.ParanoiaDebuffSecondsRemaining;
			var tNorm = Math.Clamp( rem / dur, 0f, 1f );
			var fade = Math.Clamp( MathF.Pow( tNorm, ParanoiaFadeCurveExponent ), 0.04f, 1f );
			var paranoiaVisible = paranoiaHunterOn
			                      && _uiReady
			                      && _uiManager.IsVisible( YaUiSurfaceId.PassiveParanoia );

			if ( _paranoiaBrightnessOverlay.IsValid() )
			{
				_paranoiaBrightnessOverlay.Style.Display = paranoiaVisible ? DisplayMode.Flex : DisplayMode.None;
				if ( paranoiaVisible )
					_paranoiaBrightnessOverlay.Style.Opacity = fade;
			}

			if ( _paranoiaBrightnessOverlayExtra.IsValid() )
			{
				_paranoiaBrightnessOverlayExtra.Style.Display = paranoiaVisible ? DisplayMode.Flex : DisplayMode.None;
				if ( paranoiaVisible )
					_paranoiaBrightnessOverlayExtra.Style.Opacity = fade;
			}

			if ( _paranoiaHunterFullscreenBlur.IsValid() )
			{
				_paranoiaHunterFullscreenBlur.Style.Display = paranoiaVisible ? DisplayMode.Flex : DisplayMode.None;
				if ( paranoiaVisible )
					_paranoiaHunterFullscreenBlur.Style.Opacity = fade;
			}
		}

		if ( gs is { IsValid: true, CurrentState: YaGameState.Intermission } )
		{
			if ( _intermissionReadyLabel.IsValid() )
			{
				_intermissionReadyLabel.Text =
					$"Ready: {gs.IntermissionReadyCount}/{Math.Max( 1, gs.IntermissionPlayerCount )}  —  press R when ready";
				_intermissionReadyLabel.Style.Display = DisplayMode.Flex;
			}

			if ( Input.Pressed( "reload" ) || Input.Pressed( "Reload" ) )
				gs.RequestMarkIntermissionReady();
		}
		else if ( _intermissionReadyLabel.IsValid() )
		{
			_intermissionReadyLabel.Style.Display = DisplayMode.None;
		}

		var mut = YaWeeklyMutatorSystem.Instance;
		if ( _mutatorLabel.IsValid() )
		{
			var label = mut is { IsValid: true } ? mut.ActiveMutatorLabel : "";
			_mutatorLabel.Text = label ?? "";
			_mutatorLabel.Style.Display = string.IsNullOrWhiteSpace( label ) ? DisplayMode.None : DisplayMode.Flex;
		}

		_killFeed?.TickFade();
		YaKillFeed.DrainPendingTo( _killFeed );
		var countdown = YaHudMatchSnapshot.GetPhaseCountdownForUi( scene );
		if ( _timerPanel.IsValid() && gs is not null )
			_timerPanel.Apply( gs.CurrentState, countdown, connectedPlayers );
		else if ( _timerPanel.IsValid() )
			_timerPanel.Apply( YaGameState.Lobby, 0f, connectedPlayers );

		if ( gs is { IsValid: true, CurrentState: YaGameState.InRound } && inRoundClient && !spectatingInRound )
			TryPlayFinalSecondsHeartbeat( Math.Max( 0, (int)Math.Ceiling( countdown ) ) );

		if ( gs is { IsValid: true } && gs.CurrentState == YaGameState.InRound && _lastMatchState != YaGameState.InRound )
		{
			_roundStartAnnouncementShownThisRound = false;
			_localKillStreak = 0;
		}

		if ( gs is { IsValid: true } )
			_lastMatchState = gs.CurrentState;

		if ( inRoundClient && !spectatingInRound && localAlreadyAssignedRole && !_roundStartAnnouncementShownThisRound )
		{
			_roundStartAnnouncementShownThisRound = true;
			if ( roleHud == YaPlayerRole.Alone )
				ShowRoundStartAnnouncement( "You are the Alone — eliminate all hunters" );
			else if ( roleHud == YaPlayerRole.NotAlone )
				ShowRoundStartAnnouncement( "Round started — hunt the Alone" );
		}

		if ( _lobbySoloHintWrap.IsValid() )
		{
			if ( soloLobby && _lobbySoloHint.IsValid() && gs is not null )
			{
				var autoSec = practice is { IsValid: true, AwaitingSideChoice: true }
				              ? Math.Max( 0, (int)Math.Ceiling( practice.SoloPracticeAutoStartSecondsRemaining ) )
				              : 0;

				_lobbySoloHint.Text = autoSec > 0
					? $"Need 2+ players. Practice auto-starts in {autoSec}s — or pick a side below."
					: "Need 2+ players — choose practice below, or wait for someone to join.";
			}
		}

		if ( Time.Now >= _nextHudMatchDebugLog )
		{
			_nextHudMatchDebugLog = Time.Now + 6.0;
			var t = YaHudMatchSnapshot.TryTimer( scene );
			var tp = t.IsValid() ? t.ActivePurpose : YaTimerPurpose.None;
			var tr = t.IsValid() ? t.SyncedRemaining : 0f;
			var fs = gs is not null && gs.IsValid() ? gs.SyncedPhaseSecondsRemaining : 0f;
			var st = gs is not null && gs.IsValid() ? gs.CurrentState.ToString() : "null";
			Log.Info( $"[YA][HUD] state={st} countdownUi={countdown:F2} flowSyncedSec={fs:F2} timerPurpose={tp} timerRem={tr:F2}" );
		}

		if ( Time.Now >= _nextMatchUiTick )
		{
			_nextMatchUiTick = Time.Now + 0.1;
			RefreshMatchHudSlow( scene, gs, isDead );
		}

		if ( _healthFill.IsValid() && health.IsValid() )
		{
			var t = health.MaxHealth > 0.01f
				? Math.Clamp( health.CurrentHealth / health.MaxHealth, 0f, 1f )
				: 0f;
			_healthFill.Style.Width = Length.Fraction( t );
			if ( !health.IsAlive )
				_healthFill.Style.BackgroundColor = YaHudTheme.HealthLow;
			else if ( roleHud == YaPlayerRole.Alone )
				_healthFill.Style.BackgroundColor = YaHudRoleTheme.Alone.MeterPrimary;
			else
				_healthFill.Style.BackgroundColor = YaHudTheme.Health;
		}

		if ( _hpReadout.IsValid() && health.IsValid() )
			_hpReadout.Text = $"{health.CurrentHealth:F0} / {health.MaxHealth:F0}";

		var vitals = Components.Get<YaVitalsStub>( FindMode.EnabledInSelf );
		var aloneMech = Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		if ( _staminaTrack.IsValid() )
			_staminaTrack.Style.Display = !spectatingInRound && roleHud == YaPlayerRole.NotAlone ? DisplayMode.Flex : DisplayMode.None;
		if ( _dashCooldownTrack.IsValid() )
			_dashCooldownTrack.Style.Display = DisplayMode.None;
		if ( _paranoiaHunterDebuffTrack.IsValid() )
		{
			var showParanoiaFx = !spectatingInRound
			                     && roleHud == YaPlayerRole.Alone
			                     && inRoundClient
			                     && gs is { IsValid: true }
			                     && gs.ParanoiaDebuffSecondsRemaining > 0.02f;
			_paranoiaHunterDebuffTrack.Style.Display = showParanoiaFx ? DisplayMode.Flex : DisplayMode.None;
		}

		if ( _staminaFill.IsValid() && vitals.IsValid() && roleHud == YaPlayerRole.NotAlone && !spectatingInRound )
		{
			var st = Math.Clamp( vitals.StaminaNormalized, 0f, 1f );
			var hunterChrome = YaHudRoleTheme.Hunter;
			_staminaFill.Style.Width = Length.Fraction( st );
			_staminaFill.Style.BackgroundColor = st < 0.12f
				? YaHudTheme.HealthLow
				: hunterChrome.MeterPrimary;
		}
		else if ( _staminaFill.IsValid() )
			_staminaFill.Style.Width = Length.Fraction( 0f );

		UpdateAloneAbilityCells( roleHud, aloneMech, weapon, spectatingInRound );
		UpdateMimicProgressBanner( roleHud, aloneMech, spectatingInRound );

		if ( _paranoiaHunterDebuffFill.IsValid() && gs is { IsValid: true }
		     && roleHud == YaPlayerRole.Alone && !spectatingInRound && inRoundClient )
		{
			var dur = Math.Max( 0.01f, gs.ParanoiaDebuffDurationSeconds );
			var rem = Math.Max( 0f, gs.ParanoiaDebuffSecondsRemaining );
			var p = Math.Clamp( rem / dur, 0f, 1f );
			_paranoiaHunterDebuffFill.Style.Width = Length.Fraction( p );
		}
		else if ( _paranoiaHunterDebuffFill.IsValid() )
			_paranoiaHunterDebuffFill.Style.Width = Length.Fraction( 0f );

		if ( _m2ChargeTrack.IsValid() )
			_m2ChargeTrack.Style.Display = DisplayMode.None;

		var chrome = YaHudRoleTheme.For( roleHud );
		var sel = hotbar.IsValid() ? hotbar.ClientMirrorSelectedHotbar : -1;
		for ( var i = 0; i < 3; i++ )
		{
			var cell = _slotCells[i];
			if ( cell is null || !cell.IsValid() )
				continue;
			var selected = sel == i;
			var hover = _hoverSlot == i;
			cell.Style.BackgroundColor = selected ? chrome.SlotSelected : hover ? YaHudTheme.SlotHover : YaHudTheme.SlotEmpty;
			cell.Style.BorderWidth = selected ? 2 : 1;
			cell.Style.BorderColor = selected ? chrome.AccentStrong : YaHudTheme.Border;
		}

		if ( _weaponNameLbl.IsValid() && weapon.IsValid() && hotbar.IsValid() )
		{
			if ( roleHud == YaPlayerRole.Alone )
				_weaponNameLbl.Text = "Alone";
			else
			{
				var cid = weapon.ClientMirrorCombatDefinitionId ?? "";
				var name = cid switch
				{
					"m4" => "M4",
					"shotgun" => "SHOTGUN",
					"m9_bayonet" => "M9 BAYONET",
					_ => string.IsNullOrEmpty( cid ) ? "—" : cid.ToUpperInvariant()
				};
				_weaponNameLbl.Text = name;
			}
		}

		if ( _ammoReadout.IsValid() && weapon.IsValid() )
		{
			if ( spectatingInRound )
			{
				_ammoReadout.Text = "M1 next view · M2 previous";
			}
			else if ( roleHud == YaPlayerRole.Alone )
			{
				_ammoReadout.Text = "";
			}
			else
			{
				var cid = weapon.ClientMirrorCombatDefinitionId ?? "";
				var def = YaWeaponDefinitions.Get( cid );
				if ( YaWeaponDefinitions.TreatsAsMeleeWeapon( def, cid ) )
					_ammoReadout.Text = "Melee";
				else if ( weapon.ClientMirrorReloading )
					_ammoReadout.Text = "Reloading…";
				else
					_ammoReadout.Text = $"{weapon.ClientMirrorLoadedAmmo} / {weapon.ClientMirrorReserveAmmo}";
			}
		}

	}

	void RefreshMatchHudSlow( Scene scene, YaGameStateSystem gs, bool localDead )
	{
		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
		var practice = YaPracticeModeSystem.Instance;
		var practiceActive = practice is { IsValid: true, PracticeActive: true };
		if ( !practiceActive && scene is { IsValid: true } && gs is { CurrentState: YaGameState.Lobby } && role == YaPlayerRole.Alone )
		{
			foreach ( var bot in scene.GetAllComponents<YaBotBrain>() )
			{
				if ( bot.IsValid() )
				{
					practiceActive = true;
					break;
				}
			}
		}

		if ( _objectivePanel.IsValid() )
			_objectivePanel.ApplyFromRole( role );

		if ( _alivePanel.IsValid() )
		{
			var inRound = gs is { CurrentState: YaGameState.InRound };
			var showInPracticeAsAlone = practiceActive && role == YaPlayerRole.Alone;
			var showAlive = inRound || showInPracticeAsAlone;
			_alivePanel.Style.Display = showAlive ? DisplayMode.Flex : DisplayMode.None;
			if ( showAlive )
			{
				int count;
				var hunterTeamRoster = false;
				if ( role == YaPlayerRole.NotAlone )
				{
					count = YaHudMatchSnapshot.CountNotAloneTeamAlive( scene );
					hunterTeamRoster = true;
				}
				else if ( showInPracticeAsAlone )
					count = YaHudMatchSnapshot.CountAliveNotAloneBots( scene );
				else
					count = YaHudMatchSnapshot.CountHumansAlive( scene );

				_alivePanel.SetCount( count, hunterTeamRoster );
			}
		}

		if ( _aloneStatusPanel.IsValid() )
		{
			var showAloneLine = role == YaPlayerRole.NotAlone
			                    && gs is { CurrentState: YaGameState.InRound }
			                    && gs.AloneConnectionId != default;
			_aloneStatusPanel.Style.Display = showAloneLine ? DisplayMode.Flex : DisplayMode.None;
			if ( showAloneLine )
				_aloneStatusPanel.Apply( YaHudMatchSnapshot.IsAloneAlive( scene, gs.AloneConnectionId ) );
		}

		if ( !localDead )
			TryPushKillFeedInfoEvents( scene, gs );

		ApplyHotbarLayoutForRole( role );
	}

	static bool IsControlsTutorialHeld()
	{
		return Input.Down( "View" ) || Input.Down( "view" )
		       || Input.Keyboard.Down( "c" ) || Input.Keyboard.Down( "C" );
	}

	static bool IsScoreboardHeld()
	{
		return Input.Down( "score" ) || Input.Down( "Score" )
			|| Input.Keyboard.Down( "Tab" ) || Input.Keyboard.Down( "tab" );
	}

	void RefreshScoreboardRows( Scene scene )
	{
		if ( !_scoreboardRows.IsValid() || scene is null || !scene.IsValid() )
			return;

		_scoreboardRows.DeleteChildren( true );
		foreach ( var root in YaTeamSystem.EnumeratePlayerRoots( scene ) )
		{
			var id = root.Network.OwnerId;
			var name = YaHudMatchSnapshot.GetConnectionDisplayName( id );
			var stats = root.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf );
			var row = _scoreboardRows.AddChild<ScoreboardRowPanel>();
			if ( stats.IsValid() )
				row.Apply( name, stats.SessionKills, stats.SessionDeaths, stats.SessionWins );
			else
				row.Apply( name, 0, 0, 0 );
		}
	}

	void TryRequestPracticeRole( YaPlayerRole role )
	{
		if ( Time.Now < _nextPracticeRoleRequestAt )
			return;
		_nextPracticeRoleRequestAt = Time.Now + 0.25;

		var practice = YaPracticeModeSystem.Instance;
		if ( practice is null || !practice.IsValid() )
			return;

		// Dismiss locally immediately so camera/control returns without waiting for sync.
		_practiceChoiceDismissedLocal = true;

		var value = role == YaPlayerRole.Alone ? (int)YaPlayerRole.Alone : (int)YaPlayerRole.NotAlone;
		practice.RequestChoosePracticeRole( value );
	}

	void UpdateAloneAbilityCells( YaPlayerRole role, YaAloneMechanics aloneMech, YaWeapon weapon, bool spectatingInRound )
	{
		if ( _aloneAbilityCells[0] is null || !_aloneAbilityCells[0].IsValid() )
			return;

		if ( role != YaPlayerRole.Alone || spectatingInRound )
		{
			for ( var i = 0; i < _aloneAbilityCells.Length; i++ )
			{
				if ( _aloneAbilityCells[i] is { IsValid: true } c )
					c.ApplyFill01( 0f );
			}

			return;
		}

		var dash = aloneMech.IsValid() ? aloneMech.GetDashCooldownFill01() : 1f;
		var paranoia = aloneMech.IsValid() ? aloneMech.GetParanoiaCooldownFill01() : 1f;
		var mimic = aloneMech.IsValid() ? aloneMech.GetMimicCooldownFill01() : 1f;
		var m2 = weapon.IsValid() ? weapon.LocalAloneM2ChargeNormalized : 0f;

		_aloneAbilityCells[0]?.ApplyFill01( 1f );
		_aloneAbilityCells[1]?.ApplyFill01( m2 );
		_aloneAbilityCells[2]?.ApplyFill01( dash );
		_aloneAbilityCells[3]?.ApplyFill01( paranoia );
		_aloneAbilityCells[4]?.ApplyFill01( mimic );
	}

	void UpdateMimicProgressBanner( YaPlayerRole role, YaAloneMechanics aloneMech, bool spectatingInRound )
	{
		if ( !_mimicProgressRoot.IsValid() )
			return;

		var show = role == YaPlayerRole.Alone
		           && !spectatingInRound
		           && aloneMech.IsValid()
		           && aloneMech.MimicPresentationActive
		           && aloneMech.MimicHudActiveDurationSeconds > 0.01f
		           && aloneMech.MimicHudSecondsRemaining > 0.02f;

		_mimicProgressRoot.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
		if ( !show || !_mimicProgressFill.IsValid() )
			return;

		var dur = aloneMech.MimicHudActiveDurationSeconds;
		var rem = aloneMech.MimicHudSecondsRemaining;
		var p = Math.Clamp( rem / dur, 0f, 1f );
		_mimicProgressFill.Style.Width = Length.Fraction( p );
	}

	void ApplyHotbarLayoutForRole( YaPlayerRole role )
	{
		if ( !_slotCells[0].IsValid() )
			return;

		switch ( role )
		{
			case YaPlayerRole.NotAlone:
				if ( _defaultSlotsRow.IsValid() )
					_defaultSlotsRow.Style.Display = DisplayMode.Flex;
				if ( _aloneAbilitiesRow.IsValid() )
					_aloneAbilitiesRow.Style.Display = DisplayMode.None;
				if ( _m2ChargeTrack.IsValid() )
					_m2ChargeTrack.Style.Display = DisplayMode.None;
				_slotCells[0].Style.Display = DisplayMode.Flex;
				_slotCells[1].Style.Display = DisplayMode.Flex;
				_slotCells[2].Style.Display = DisplayMode.None;
				if ( _slotCells[0].NameLabel.IsValid() )
					_slotCells[0].NameLabel.Text = "M4";
				if ( _slotCells[1].NameLabel.IsValid() )
					_slotCells[1].NameLabel.Text = "Shotgun";
				if ( _hotbarHint.IsValid() )
					_hotbarHint.Text = "1 / 2  —  weapons  ·  R reload";
				if ( _weaponRail.IsValid() )
					_weaponRail.Style.MinWidth = 260;
				break;
			case YaPlayerRole.Alone:
				if ( _defaultSlotsRow.IsValid() )
					_defaultSlotsRow.Style.Display = DisplayMode.None;
				if ( _aloneAbilitiesRow.IsValid() )
					_aloneAbilitiesRow.Style.Display = DisplayMode.Flex;
				if ( _dashCooldownTrack.IsValid() )
					_dashCooldownTrack.Style.Display = DisplayMode.None;
				if ( _m2ChargeTrack.IsValid() )
					_m2ChargeTrack.Style.Display = DisplayMode.None;
				if ( _hotbarHint.IsValid() )
					_hotbarHint.Text = AloneAbilitiesHintLine;
				if ( _weaponRail.IsValid() )
					_weaponRail.Style.MinWidth = 440;
				break;
			default:
				if ( _defaultSlotsRow.IsValid() )
					_defaultSlotsRow.Style.Display = DisplayMode.Flex;
				if ( _aloneAbilitiesRow.IsValid() )
					_aloneAbilitiesRow.Style.Display = DisplayMode.None;
				if ( _m2ChargeTrack.IsValid() )
					_m2ChargeTrack.Style.Display = DisplayMode.None;
				_slotCells[0].Style.Display = DisplayMode.Flex;
				_slotCells[1].Style.Display = DisplayMode.Flex;
				_slotCells[2].Style.Display = DisplayMode.None;
				if ( _slotCells[0].NameLabel.IsValid() )
					_slotCells[0].NameLabel.Text = "M4";
				if ( _slotCells[1].NameLabel.IsValid() )
					_slotCells[1].NameLabel.Text = "Shotgun";
				if ( _hotbarHint.IsValid() )
					_hotbarHint.Text = "1 / 2  —  weapons  ·  R reload";
				if ( _weaponRail.IsValid() )
					_weaponRail.Style.MinWidth = 320;
				break;
		}
	}

	void TryPushKillFeedInfoEvents( Scene scene, YaGameStateSystem gs )
	{
		if ( _killFeed is null || !_killFeed.IsValid() )
			return;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( gs is not null && Time.Now >= _nextFeedThrottle )
		{
			var aid = gs.AloneConnectionId;
			if ( aid != _lastAloneConnectionId && aid != default )
			{
				var localId = Connection.Local?.Id ?? default;
				if ( aid == localId )
					_killFeed.PushInfo( "You are the Alone" );

				_lastAloneConnectionId = aid;
				_nextFeedThrottle = Time.Now + 0.35;
			}
			else if ( aid == default && _lastAloneConnectionId != default )
				_lastAloneConnectionId = default;
		}
	}

}
