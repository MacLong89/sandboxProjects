using Sandbox.UI;

namespace Sandbox;

[StyleSheet( "AimboxUiHost.cs.scss" )]
[Title( "Aimbox UI Host" )]
[Category( "Aimbox" )]
public sealed class AimboxUiHost : PanelComponent
{
	Panel _hud;
	Label _score;
	Label _scoreMode;
	Label _scoreMain;
	Label _timer;
	Label _health;
	Panel _healthBar;
	List<Panel> _healthSegments = [];
	Panel _ammoMagBar;
	List<Panel> _ammoMagSegments = [];
	Panel _hudFrame;
	Panel _spectraStatus;
	Label _spectraMode;
	Label _spectraScoreValue;
	Label _spectraTimer;
	Panel _spectraVitals;
	Panel _healthVerticalTrack;
	Panel _healthVerticalFill;
	Label _spectraHealthValue;
	Panel _spectraOrdnance;
	Label _spectraWeapon;
	Label _ammoCurrent;
	Label _ammoReserve;
	Label _weapon;
	Label _ammo;
	Label _damage;
	Panel _hitMarker;
	Panel _scopePipHitMarker;
	Label _scopePipHitDamage;
	Panel _crosshair;
	Panel _scopeCrosshair;
	Panel _scopeHitMarker;
	Label _scopeHitDamage;
	Image _scopePipView;
	Panel _scopePipReticle;
	Panel _scopeOverlay;
	Panel _scopeRing;
	Panel _redDotReticle;
	Panel _deathOverlay;
	Label _deathTitle;
	Label _deathTimer;
	Panel _freezeOverlay;
	Label _freezeTitle;
	Label _freezeTimer;
	Panel _arcadeWave;
	Label _arcadeWaveTag;
	Label _arcadeWaveValue;
	Panel _arcadeTime;
	Label _arcadeTimeTag;
	Label _arcadeTimeValue;
	Panel _arcadeRadar;
	Panel _arcadeRadarGrid;
	Panel _arcadeRadarPlayer;
	List<Panel> _radarBlips = [];
	Panel _arcadeHealth;
	Label _arcadeHealthText;
	List<Panel> _healthSlants = [];
	Panel _arcadeAmmo;
	Label _arcadeAmmoCurrent;
	Label _arcadeAmmoReserve;
	List<Panel> _ammoBullets = [];
	Panel _arcadeEquipment;
	Panel _equipWeapon1;
	Panel _equipWeapon2;
	Panel _equipLethal;
	Panel _equipTactical;
	Label _equipWeapon1Key;
	Label _equipWeapon1Label;
	Label _equipWeapon2Key;
	Label _equipWeapon2Label;
	Label _equipLethalKey;
	Label _equipLethalLabel;
	Label _equipTacticalKey;
	Label _equipTacticalLabel;
	Panel _flashOverlay;
	TimeUntil _hitMarkerVisibleUntil;
	bool _built;
	bool _arcadeHud;

	AimboxPlayerController Player => FindPlayer();
	AimboxMatchSystem Match => AimboxGame.Instance?.Match;

	protected override void OnAwake()
	{
		EnsureScreenPanel();
		EnsureCursorGuard();
	}

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();
		EnsureScreenPanel();
		BuildUi();
	}

	protected override void OnStart()
	{
		EnsureScreenPanel();
		EnsureProgressionHud();
		EnsureKillFeedHud();
		var screenPanel = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		Log.Info( $"[Aimbox UI v3] OnStart. PanelValid={Panel?.IsValid == true}, ScreenPanel={screenPanel is not null}, AutoScale={screenPanel?.AutoScreenScale}, Opacity={screenPanel?.Opacity}, Scale={screenPanel?.Scale}." );
		BuildUi();
	}

	protected override void OnUpdate()
	{
		AimboxMetaNavigation.HandlePauseToggleInput();

		BindScreenPanelCamera();

		if ( !_built || !HasValidUiTree() )
		{
			_built = false;
			BuildUi();
		}

		Refresh();
		AimboxCursor.SyncAfterUi();
	}

	protected override void OnPreRender()
	{
		AimboxCursor.Sync();
	}

	void BuildUi()
	{
		if ( _built || Panel is null || !Panel.IsValid )
			return;

		Panel.AddClass( "aimbox-ui-host" );
		Panel.DeleteChildren();
		_healthSegments.Clear();
		_ammoMagSegments.Clear();
		_healthSlants.Clear();
		_radarBlips.Clear();
		_ammoBullets.Clear();

		_arcadeHud = !AimboxArcadeHudUi.UseLegacyHud;
		_hud = AddPanel( Panel, _arcadeHud ? "aimbox-hud-layer arcade" : "aimbox-hud-layer" );

		if ( _arcadeHud )
			BuildArcadeHud();
		else
			BuildLegacyHud();

		_built = true;
		Log.Info( $"[Aimbox UI v3] Built C# panel tree ({(_arcadeHud ? "arcade" : "legacy")}). rootChildren={Panel.Children.Count()} hudChildren={_hud.Children.Count()}." );
		Refresh();
	}

	void BuildArcadeHud()
	{
		_arcadeWave = AddPanel( _hud, "arcade-block arcade-wave" );
		AddPanel( _arcadeWave, "accent accent-left" );
		_arcadeWaveTag = AddLabel( _arcadeWave, "WAVE", "block-tag" );
		_arcadeWaveValue = AddLabel( _arcadeWave, "01", "block-value" );

		_arcadeTime = AddPanel( _hud, "arcade-block arcade-time" );
		AddPanel( _arcadeTime, "accent accent-left" );
		AddPanel( _arcadeTime, "accent accent-right" );
		_arcadeTimeTag = AddLabel( _arcadeTime, "TIME", "block-tag" );
		_arcadeTimeValue = AddLabel( _arcadeTime, "00:00", "block-value" );

		_arcadeRadar = AddPanel( _hud, "arcade-radar" );
		_arcadeRadarGrid = AddPanel( _arcadeRadar, "radar-grid" );
		_arcadeRadarPlayer = AddPanel( _arcadeRadar, "radar-player" );
		for ( var i = 0; i < 16; i++ )
			_radarBlips.Add( AddPanel( _arcadeRadar, "radar-blip" ) );

		_crosshair = AddPanel( _hud, "crosshair arcade-crosshair" );
		AddPanel( _crosshair, "dot" );

		_scopePipView = AddImage( _hud, "scope-pip-view" );
		_scopePipReticle = AddPanel( _hud, "scope-pip-reticle" );
		AddPanel( _scopePipReticle, "scope-pip-line scope-pip-line-h" );
		AddPanel( _scopePipReticle, "scope-pip-line scope-pip-line-v" );
		AddPanel( _scopePipReticle, "scope-pip-dot" );
		_scopePipHitMarker = AddPanel( _scopePipReticle, "scope-pip-hitmarker" );
		AddPanel( _scopePipHitMarker, "slash a" );
		AddPanel( _scopePipHitMarker, "slash b" );
		AddPanel( _scopePipHitMarker, "slash c" );
		AddPanel( _scopePipHitMarker, "slash d" );
		_scopePipHitDamage = AddLabel( _scopePipHitMarker, "", "damage" );

		_redDotReticle = AddPanel( _hud, "reddot-reticle" );

		_scopeOverlay = AddPanel( _hud, "scope-overlay" );
		AddPanel( _scopeOverlay, "scope-mask" );
		AddPanel( _scopeOverlay, "scope-ring" );
		BuildScopeOverlayCombatHud( _scopeOverlay, arcade: true );

		_hitMarker = AddPanel( _hud, "hitmarker" );
		AddPanel( _hitMarker, "slash a" );
		AddPanel( _hitMarker, "slash b" );
		AddPanel( _hitMarker, "slash c" );
		AddPanel( _hitMarker, "slash d" );
		_damage = AddLabel( _hitMarker, "", "damage" );

		_deathOverlay = AddPanel( _hud, "death-overlay" );
		_deathTitle = AddLabel( _deathOverlay, "YOU DIED", "death-title" );
		_deathTimer = AddLabel( _deathOverlay, "Respawning in 3", "death-timer" );

		_freezeOverlay = AddPanel( _hud, "freeze-overlay" );
		_freezeTitle = AddLabel( _freezeOverlay, "FREEZE TIME", "freeze-title" );
		_freezeTimer = AddLabel( _freezeOverlay, "3", "freeze-timer" );

		_arcadeHealth = AddPanel( _hud, "arcade-block arcade-health" );
		AddPanel( _arcadeHealth, "accent accent-left" );
		_arcadeHealthText = AddLabel( _arcadeHealth, "+ 100 / 100", "health-text" );
		var slants = AddPanel( _arcadeHealth, "health-slants" );
		for ( var i = 0; i < 6; i++ )
			_healthSlants.Add( AddPanel( slants, "health-slant" ) );

		_arcadeAmmo = AddPanel( _hud, "arcade-block arcade-ammo" );
		AddPanel( _arcadeAmmo, "accent accent-right" );
		_arcadeAmmoCurrent = AddLabel( _arcadeAmmo, "28", "ammo-current" );
		_arcadeAmmoReserve = AddLabel( _arcadeAmmo, "/ 120", "ammo-reserve" );
		var bullets = AddPanel( _arcadeAmmo, "ammo-bullets" );
		for ( var i = 0; i < 3; i++ )
			_ammoBullets.Add( AddPanel( bullets, "ammo-bullet" ) );

		_arcadeEquipment = AddPanel( _hud, "arcade-equipment" );
		_equipWeapon1 = AddPanel( _arcadeEquipment, "equip-tile" );
		_equipWeapon1Key = AddLabel( _equipWeapon1, "1", "equip-key" );
		_equipWeapon1Label = AddLabel( _equipWeapon1, "RIFLE", "equip-label" );
		_equipWeapon2 = AddPanel( _arcadeEquipment, "equip-tile" );
		_equipWeapon2Key = AddLabel( _equipWeapon2, "2", "equip-key" );
		_equipWeapon2Label = AddLabel( _equipWeapon2, "PISTOL", "equip-label" );
		_equipLethal = AddPanel( _arcadeEquipment, "equip-tile" );
		_equipLethalKey = AddLabel( _equipLethal, "G", "equip-key" );
		_equipLethalLabel = AddLabel( _equipLethal, "FRAG", "equip-label" );
		_equipTactical = AddPanel( _arcadeEquipment, "equip-tile" );
		_equipTacticalKey = AddLabel( _equipTactical, "F", "equip-key" );
		_equipTacticalLabel = AddLabel( _equipTactical, "FLASH", "equip-label" );

		_flashOverlay = AddPanel( _hud, "flash-overlay" );
	}

	void BuildLegacyHud()
	{
		var top = AddPanel( _hud, "top" );
		AddLabel( top, "AIMBOX", "brand" );
		_scoreMode = AddLabel( top, "FFA", "score-mode" );
		var topCenter = AddPanel( top, "top-center" );
		_scoreMain = AddLabel( topCenter, "0", "score-main" );
		_score = AddLabel( top, "FFA 0", "score" );
		_timer = AddLabel( top, "10:00", "timer" );

		_crosshair = AddPanel( _hud, "crosshair" );
		AddPanel( _crosshair, "dot" );

		_hudFrame = AddPanel( _hud, "hud-frame" );
		AddPanel( _hudFrame, "frame-corner frame-corner-tl" );
		AddPanel( _hudFrame, "frame-corner frame-corner-tr" );
		AddPanel( _hudFrame, "frame-corner frame-corner-bl" );
		AddPanel( _hudFrame, "frame-corner frame-corner-br" );

		_spectraStatus = AddPanel( _hud, "spectra-status" );
		AddLabel( _spectraStatus, "MATCH", "spectra-status-tag" );
		_spectraMode = AddLabel( _spectraStatus, "FFA", "spectra-mode" );
		_spectraScoreValue = AddLabel( _spectraStatus, "0", "spectra-score" );
		_spectraTimer = AddLabel( _spectraStatus, "10:00", "spectra-timer" );

		_spectraVitals = AddPanel( _hud, "spectra-vitals" );
		AddLabel( _spectraVitals, "SHIELD", "spectra-vitals-label" );
		_healthVerticalTrack = AddPanel( _spectraVitals, "health-vertical-track" );
		_healthVerticalFill = AddPanel( _healthVerticalTrack, "health-vertical-fill" );
		_spectraHealthValue = AddLabel( _spectraVitals, "100", "spectra-health-value" );

		_spectraOrdnance = AddPanel( _hud, "spectra-ordnance" );
		_spectraWeapon = AddLabel( _spectraOrdnance, "RIFLE", "spectra-weapon" );
		_ammoCurrent = AddLabel( _spectraOrdnance, "30", "ammo-current" );
		AddLabel( _spectraOrdnance, "RESERVE", "ammo-reserve-label" );
		_ammoReserve = AddLabel( _spectraOrdnance, "120", "ammo-reserve" );

		_scopePipView = AddImage( _hud, "scope-pip-view" );

		_scopePipReticle = AddPanel( _hud, "scope-pip-reticle" );
		AddPanel( _scopePipReticle, "scope-pip-line scope-pip-line-h" );
		AddPanel( _scopePipReticle, "scope-pip-line scope-pip-line-v" );
		AddPanel( _scopePipReticle, "scope-pip-dot" );

		_scopePipHitMarker = AddPanel( _scopePipReticle, "scope-pip-hitmarker" );
		AddPanel( _scopePipHitMarker, "slash a" );
		AddPanel( _scopePipHitMarker, "slash b" );
		AddPanel( _scopePipHitMarker, "slash c" );
		AddPanel( _scopePipHitMarker, "slash d" );
		_scopePipHitDamage = AddLabel( _scopePipHitMarker, "", "damage" );

		_scopeOverlay = AddPanel( _hud, "scope-overlay" );
		AddPanel( _scopeOverlay, "scope-mask" );
		_scopeRing = AddPanel( _scopeOverlay, "scope-ring" );
		BuildScopeOverlayCombatHud( _scopeOverlay, arcade: false );

		_redDotReticle = AddPanel( _hud, "reddot-reticle" );

		_hitMarker = AddPanel( _hud, "hitmarker" );
		AddPanel( _hitMarker, "slash a" );
		AddPanel( _hitMarker, "slash b" );
		AddPanel( _hitMarker, "slash c" );
		AddPanel( _hitMarker, "slash d" );
		_damage = AddLabel( _hitMarker, "", "damage" );

		_deathOverlay = AddPanel( _hud, "death-overlay" );
		_deathTitle = AddLabel( _deathOverlay, "YOU DIED", "death-title" );
		_deathTimer = AddLabel( _deathOverlay, "Respawning in 3", "death-timer" );

		_freezeOverlay = AddPanel( _hud, "freeze-overlay" );
		_freezeTitle = AddLabel( _freezeOverlay, "FREEZE TIME", "freeze-title" );
		_freezeTimer = AddLabel( _freezeOverlay, "3", "freeze-timer" );

		var left = AddPanel( _hud, "bottom-left" );
		_healthBar = AddPanel( left, "health-bar" );
		for ( var i = 0; i < 10; i++ )
			_healthSegments.Add( AddPanel( _healthBar, "health-segment" ) );
		_health = AddLabel( left, "100", "health" );
		AddLabel( left, "HEALTH", "label" );

		var right = AddPanel( _hud, "bottom-right" );
		_weapon = AddLabel( right, "Assault Rifle", "weapon" );
		_ammo = AddLabel( right, "30 / 120", "ammo" );
		_ammoMagBar = AddPanel( right, "ammo-mag-bar" );
		for ( var i = 0; i < 8; i++ )
			_ammoMagSegments.Add( AddPanel( _ammoMagBar, "ammo-mag-segment" ) );

		_flashOverlay = AddPanel( _hud, "flash-overlay" );
	}

	bool HasValidUiTree()
	{
		if ( Panel?.IsValid != true || _hud?.IsValid != true )
			return false;

		return _arcadeHud ? HasValidArcadeUiTree() : HasValidLegacyUiTree();
	}

	bool HasValidArcadeUiTree()
	{
		return _arcadeWave?.IsValid == true
			&& _arcadeWaveValue?.IsValid == true
			&& _arcadeTimeValue?.IsValid == true
			&& _arcadeRadar?.IsValid == true
			&& _arcadeHealthText?.IsValid == true
			&& _healthSlants.Count == 6
			&& _healthSlants.All( x => x?.IsValid == true )
			&& _arcadeAmmoCurrent?.IsValid == true
			&& _crosshair?.IsValid == true
			&& _scopeOverlay?.IsValid == true
			&& _scopeCrosshair?.IsValid == true
			&& _scopeHitMarker?.IsValid == true
			&& _hitMarker?.IsValid == true
			&& _deathOverlay?.IsValid == true
			&& _freezeOverlay?.IsValid == true;
	}

	bool HasValidLegacyUiTree()
	{
		return _score?.IsValid == true
			&& _scoreMode?.IsValid == true
			&& _scoreMain?.IsValid == true
			&& _timer?.IsValid == true
			&& _health?.IsValid == true
			&& _healthBar?.IsValid == true
			&& _ammoMagBar?.IsValid == true
			&& _hudFrame?.IsValid == true
			&& _spectraStatus?.IsValid == true
			&& _spectraMode?.IsValid == true
			&& _spectraScoreValue?.IsValid == true
			&& _spectraTimer?.IsValid == true
			&& _spectraVitals?.IsValid == true
			&& _healthVerticalTrack?.IsValid == true
			&& _healthVerticalFill?.IsValid == true
			&& _spectraHealthValue?.IsValid == true
			&& _spectraOrdnance?.IsValid == true
			&& _spectraWeapon?.IsValid == true
			&& _ammoCurrent?.IsValid == true
			&& _ammoReserve?.IsValid == true
			&& _weapon?.IsValid == true
			&& _ammo?.IsValid == true
			&& _damage?.IsValid == true
			&& _hitMarker?.IsValid == true
			&& _scopePipHitMarker?.IsValid == true
			&& _scopePipHitDamage?.IsValid == true
			&& _crosshair?.IsValid == true
			&& _scopePipView?.IsValid == true
			&& _scopePipReticle?.IsValid == true
			&& _scopeOverlay?.IsValid == true
			&& _scopeRing?.IsValid == true
			&& _scopeCrosshair?.IsValid == true
			&& _scopeHitMarker?.IsValid == true
			&& _redDotReticle?.IsValid == true
			&& _deathOverlay?.IsValid == true
			&& _deathTitle?.IsValid == true
			&& _deathTimer?.IsValid == true
			&& _freezeOverlay?.IsValid == true
			&& _freezeTitle?.IsValid == true
			&& _freezeTimer?.IsValid == true;
	}

	void EnsureScreenPanel()
	{
		var screenPanel = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		if ( screenPanel is not null )
			return;

		screenPanel = Components.Create<ScreenPanel>();
		screenPanel.AutoScreenScale = true;
		screenPanel.Opacity = 1f;
		screenPanel.Scale = 1f;
		Log.Warning( "[Aimbox UI v3] ScreenPanel was missing; created one at runtime." );
	}

	void EnsureCursorGuard()
	{
		if ( Components.Get<AimboxCursorGuard>( FindMode.EnabledInSelf ) is not null )
			return;

		Components.Create<AimboxCursorGuard>();
	}

	void EnsureProgressionHud()
	{
		if ( Components.Get<AimboxProgressionHud>( FindMode.EnabledInSelf ) is not null )
			return;

		Components.Create<AimboxProgressionHud>();
		Log.Info( "[Aimbox UI v3] Created AimboxProgressionHud at runtime." );
	}

	void EnsureKillFeedHud()
	{
		if ( Components.Get<AimboxKillFeedHud>( FindMode.EnabledInSelf ) is not null )
			return;

		Components.Create<AimboxKillFeedHud>();
		Log.Info( "[Aimbox UI v3] Created AimboxKillFeedHud at runtime." );
	}

	void BindScreenPanelCamera()
	{
		var screenPanel = Components.Get<ScreenPanel>( FindMode.EnabledInSelf );
		var camera = Scene?.Camera;
		if ( screenPanel is null || camera is null || !camera.IsValid() )
			return;

		if ( screenPanel.TargetCamera != camera )
			screenPanel.TargetCamera = camera;
	}

	void Refresh()
	{
		if ( !_built || !HasValidUiTree() )
			return;

		var player = Player;
		var match = Match;
		var game = AimboxGame.Instance;
		var weapon = player?.CurrentWeapon;

		SyncThemeClasses();

		if ( _arcadeHud )
		{
			RefreshArcadeHud( player, match, game, weapon );
			return;
		}

		_score.Text = $"{ModeLabel} {ScoreText}";
		_scoreMode.Text = ModeLabel;
		_scoreMain.Text = ScoreText;
		_timer.Text = TimerText;
		_health.Text = (player?.Health ?? 0).ToString();
		_weapon.Text = weapon?.Definition.Name ?? "No Weapon";
		_ammo.Text = weapon is null ? "--" : weapon.Definition.IsMelee ? "MELEE" : AimAmmoText( match, weapon );

		var isDeadPreview = player is { IsAlive: false } && game?.Phase == AimboxSessionPhase.Playing;
		RefreshHealthSegments( player?.Health ?? 0, isDeadPreview );
		RefreshAmmoMagSegments( weapon );
		RefreshSpectraHud( player?.Health ?? 0, isDeadPreview, weapon, TimerText, ModeLabel, ScoreText );

		_damage.Text = ShouldShowHitFeedback( match )
			? (player?.LastDamageDealt > 0 ? player.LastDamageDealt.ToString() : "")
			: "";

		var isDead = player is { IsAlive: false }
		             && game?.Phase == AimboxSessionPhase.Playing;
		RefreshCombatOverlay( player, game, isDead );

		var isDuel = match?.Mode == AimboxGameMode.Duel;
		var isSurvival = match?.Mode == AimboxGameMode.Survival;
		_deathOverlay.SetClass( "visible", isDead );
		if ( isDead )
		{
			if ( isDuel == true )
			{
				_deathTimer.Text = game?.IsDuelRoundResetPending == true
					? "Resetting round..."
					: "Round over";
			}
			else if ( isSurvival == true )
			{
				_deathTimer.Text = match?.SurvivalFailed == true
					? "Run failed — restart from Wave 1"
					: "Eliminated";
			}
			else
			{
				var seconds = MathF.Ceiling( player.RespawnTimeRemaining );
				_deathTimer.Text = seconds <= 0 ? "Respawning..." : $"Respawning in {seconds:0}";
			}
		}

		var frozen = game?.IsMatchFrozen == true;
		_freezeOverlay.SetClass( "visible", frozen );
		if ( frozen )
		{
			_freezeTitle.Text = game.FreezeLabel;
			_freezeTimer.Text = MathF.Ceiling( game.FreezeTimeRemaining ).ToString( "0" );
		}

		_health.SetClass( "dead", isDead );
		_hud.SetClass( "hidden", AimboxMetaNavigation.BlocksGameplay );
	}

	void RefreshArcadeHud( AimboxPlayerController player, AimboxMatchSystem match, AimboxGame game, AimboxWeaponRuntime weapon )
	{
		_arcadeWaveTag.Text = PrimaryMetricLabel;
		_arcadeWaveValue.Text = PrimaryMetricValue;
		_arcadeTimeTag.Text = game?.IsMatchFrozen == true ? "FREEZE" : "TIME";
		_arcadeTimeValue.Text = TimerText;

		var health = player?.Health ?? 0;
		var maxHealth = AimboxArcadeHudUi.MaxHealth;
		var isDeadPreview = player is { IsAlive: false } && game?.Phase == AimboxSessionPhase.Playing;
		_arcadeHealthText.Text = $"+ {Math.Max( 0, health )} / {maxHealth}";
		RefreshArcadeHealthSlants( health, maxHealth, isDeadPreview );

		if ( weapon is null || weapon.Definition.IsMelee )
		{
			_arcadeAmmoCurrent.Text = weapon?.Definition.IsMelee == true ? "MELEE" : "--";
			_arcadeAmmoReserve.Text = "";
			RefreshArcadeAmmoBullets( 0f );
		}
		else
		{
			_arcadeAmmoCurrent.Text = AimAmmoCurrentText( match, weapon );
			_arcadeAmmoReserve.Text = AimboxAimModeRules.IsAimMode( match?.Mode ?? default ) ? "" : $"/ {weapon.Reserve}";
			var magSize = Math.Max( 1, weapon.EffectiveMagazineSize );
			RefreshArcadeAmmoBullets( AimboxAimModeRules.IsAimMode( match?.Mode ?? default ) ? 1f : weapon.Ammo / (float)magSize );
		}

		RefreshArcadeRadar( player, game );
		RefreshArcadeEquipment( player, game );

		var isDead = player is { IsAlive: false } && game?.Phase == AimboxSessionPhase.Playing;
		_damage.Text = ShouldShowHitFeedback( match )
			? (player?.LastDamageDealt > 0 ? player.LastDamageDealt.ToString() : "")
			: "";
		RefreshCombatOverlay( player, game, isDead );

		var isDuel = match?.Mode == AimboxGameMode.Duel;
		var isSurvival = match?.Mode == AimboxGameMode.Survival;
		_deathOverlay.SetClass( "visible", isDead );

		if ( isDead )
		{
			if ( isDuel == true )
			{
				_deathTimer.Text = game?.IsDuelRoundResetPending == true
					? "Resetting round..."
					: "Round over";
			}
			else if ( isSurvival == true )
			{
				_deathTimer.Text = match?.SurvivalFailed == true
					? "Run failed — restart from Wave 1"
					: "Eliminated";
			}
			else
			{
				var seconds = MathF.Ceiling( player.RespawnTimeRemaining );
				_deathTimer.Text = seconds <= 0 ? "Respawning..." : $"Respawning in {seconds:0}";
			}
		}

		var frozen = game?.IsMatchFrozen == true;
		_freezeOverlay.SetClass( "visible", frozen );
		if ( frozen )
		{
			_freezeTitle.Text = game.FreezeLabel;
			_freezeTimer.Text = MathF.Ceiling( game.FreezeTimeRemaining ).ToString( "0" );
		}

		_arcadeHealth.SetClass( "dead", isDeadPreview );
		_hud.SetClass( "hidden", AimboxMetaNavigation.BlocksGameplay );
	}

	void RefreshArcadeHealthSlants( int health, int maxHealth, bool isDead )
	{
		var filled = Math.Clamp( (int)MathF.Ceiling( health / (float)maxHealth * _healthSlants.Count ), 0, _healthSlants.Count );
		for ( var i = 0; i < _healthSlants.Count; i++ )
			_healthSlants[i].SetClass( "filled", !isDead && i < filled );
	}

	void RefreshArcadeAmmoBullets( float fill01 )
	{
		var filled = Math.Clamp( (int)MathF.Ceiling( fill01 * _ammoBullets.Count ), 0, _ammoBullets.Count );
		for ( var i = 0; i < _ammoBullets.Count; i++ )
			_ammoBullets[i].SetClass( "filled", i < filled );
	}

	void RefreshArcadeRadar( AimboxPlayerController player, AimboxGame game )
	{
		var contacts = player is null || game is null
			? []
			: AimboxArcadeRadarHelper.BuildContacts( player, game );

		_arcadeRadar.SetClass( "uav-active", game?.Killstreaks.IsUavActive( player?.AccountId ) == true );

		for ( var i = 0; i < _radarBlips.Count; i++ )
		{
			var blip = _radarBlips[i];
			if ( i >= contacts.Count )
			{
				blip.SetClass( "visible", false );
				continue;
			}

			var contact = contacts[i];
			var half = 46f;
			blip.Style.Left = Length.Pixels( half + contact.NormalizedX * half );
			blip.Style.Top = Length.Pixels( half - contact.NormalizedY * half );
			blip.SetClass( "visible", true );
			blip.SetClass( "hostile", contact.IsHostile );
			blip.SetClass( "friendly", contact.IsTeammate );
		}
	}

	void RefreshArcadeEquipment( AimboxPlayerController player, AimboxGame game )
	{
		if ( player is null || game is null )
			return;

		var loadout = game.Loadouts.GetActiveLoadout( player.Data );

		RefreshArcadeWeaponEquipTile( player, _equipWeapon1, _equipWeapon1Key, _equipWeapon1Label, inventorySlot: 0 );
		RefreshArcadeWeaponEquipTile( player, _equipWeapon2, _equipWeapon2Key, _equipWeapon2Label, inventorySlot: 1 );

		var lethal = loadout?.LethalGrenade ?? "FRAG";
		_equipLethalLabel.Text = lethal.ToUpperInvariant();
		_equipLethal.SetClass( "ready", player.LethalGrenadesRemaining > 0 );

		if ( _equipTactical is null )
			return;

		var tactical = loadout?.TacticalGrenade ?? "FLASH";
		_equipTacticalLabel.Text = tactical.ToUpperInvariant();
		_equipTactical.SetClass( "ready", player.TacticalGrenadesRemaining > 0 );
	}

	static void RefreshArcadeWeaponEquipTile(
		AimboxPlayerController player,
		Panel tile,
		Label keyLabel,
		Label nameLabel,
		int inventorySlot )
	{
		if ( tile is null || keyLabel is null || nameLabel is null )
			return;

		var weapon = player.GetWeaponInventorySlot( inventorySlot );
		var visible = weapon is not null && !weapon.Definition.IsMelee;

		tile.SetClass( "hidden", !visible );
		if ( !visible )
			return;

		keyLabel.Text = (inventorySlot + 1).ToString();
		nameLabel.Text = CompactWeaponTypeLabel( weapon.Definition.Id );
		tile.SetClass( "ready", player.ActiveWeapon == weapon.Definition.Id );
	}

	static string CompactWeaponTypeLabel( AimboxWeaponId id )
	{
		var label = AimboxClassUiHelpers.WeaponClassLabel( id );
		return label == "ASSAULT RIFLE" ? "RIFLE" : label;
	}

	string PrimaryMetricLabel => Match?.Mode switch
	{
		AimboxGameMode.Survival => "WAVE",
		AimboxGameMode.Range => "PRACTICE",
		_ when AimboxAimModeRules.IsAimMode( Match?.Mode ?? default ) => "SCORE",
		AimboxGameMode.TeamDeathmatch => "SCORE",
		AimboxGameMode.Duel => "DUEL",
		_ => "SCORE"
	};

	string PrimaryMetricValue
	{
		get
		{
			if ( Match is null || Player is null )
				return "00";

			return Match.Mode switch
			{
				AimboxGameMode.Survival => Match.SurvivalWave.ToString( "00" ),
				AimboxGameMode.Range => (Player?.Data?.PracticeKills ?? 0).ToString( "N0" ),
				_ when AimboxAimModeRules.IsAimMode( Match.Mode ) => Match.GetAimScore( Player.AccountId ).ToString( "N0" ),
				AimboxGameMode.TeamDeathmatch => $"{Match.GetTeamScore( Player.Team ):N0}",
				AimboxGameMode.Duel => $"{GetPlayerKills( Player.AccountId )}-{GetDuelOpponentKills()}",
				_ => Match.GetScore( Player.AccountId ).ToString( "N0" )
			};
		}
	}

	static bool ShouldShowHitFeedback( AimboxMatchSystem match ) =>
		!AimboxAimModeRules.IsAimMode( match?.Mode ?? default );

	void RefreshCombatOverlay( AimboxPlayerController player, AimboxGame game, bool isDead )
	{
		var classicScope = player?.ShowClassicSniperScope == true && !isDead && game?.IsMatchFrozen != true;
		var hideCrosshair = isDead || player?.HideStandardCrosshair == true || game?.IsMatchFrozen == true;
		var suppressHitFeedback = AimboxAimModeRules.IsAimMode( game?.Match.Mode ?? default );

		_hud.SetClass( "scoped", classicScope );
		_scopeOverlay?.SetClass( "visible", classicScope );

		if ( suppressHitFeedback )
			_hitMarkerVisibleUntil = 0f;
		else if ( player?.ShowHitMarker == true )
			_hitMarkerVisibleUntil = 0.35f;

		var showHitMarker = !suppressHitFeedback && _hitMarkerVisibleUntil > 0f;

		_crosshair?.SetClass( "hidden", hideCrosshair || classicScope );
		_scopeCrosshair?.SetClass( "hidden", hideCrosshair || !classicScope );

		_hitMarker?.SetClass( "visible", showHitMarker && !classicScope );
		_hitMarker?.SetClass( "headshot", player?.ShowHeadshotMarker == true );
		_scopeHitMarker?.SetClass( "visible", showHitMarker && classicScope );
		_scopeHitMarker?.SetClass( "headshot", player?.ShowHeadshotMarker == true );
		if ( _scopeHitDamage is not null )
			_scopeHitDamage.Text = _damage?.Text ?? "";

		_scopePipHitMarker?.SetClass( "visible", false );
		if ( _scopePipHitDamage is not null )
			_scopePipHitDamage.Text = _damage?.Text ?? "";

		_redDotReticle?.SetClass( "visible", player?.ShowHoloSightCenterDot == true && !isDead && game?.IsMatchFrozen != true );
		RefreshScopePipView();
		RefreshFlashOverlay( player );
	}

	AimboxPlayerController FindPlayer()
	{
		var players = AimboxGame.Instance?.Players;
		if ( players is null )
			return null;

		AimboxPlayerController fallback = null;
		foreach ( var player in players )
		{
			fallback ??= player;
			if ( !player.IsProxy )
				return player;
		}

		return fallback;
	}

	void RefreshFlashOverlay( AimboxPlayerController player )
	{
		if ( _flashOverlay is null )
			return;

		var blind = player?.FlashBlind01 ?? 0f;
		_flashOverlay.SetClass( "visible", blind > 0.01f );
		_flashOverlay.Style.Opacity = blind;
	}

	void BuildScopeOverlayCombatHud( Panel scopeOverlay, bool arcade )
	{
		_scopeCrosshair = AddPanel(
			scopeOverlay,
			arcade ? "crosshair arcade-crosshair scope-crosshair" : "crosshair scope-crosshair" );
		AddPanel( _scopeCrosshair, "dot" );

		_scopeHitMarker = AddPanel( scopeOverlay, "hitmarker scope-hitmarker" );
		AddPanel( _scopeHitMarker, "slash a" );
		AddPanel( _scopeHitMarker, "slash b" );
		AddPanel( _scopeHitMarker, "slash c" );
		AddPanel( _scopeHitMarker, "slash d" );
		_scopeHitDamage = AddLabel( _scopeHitMarker, "", "damage" );
	}

	void RefreshScopePipView()
	{
		var pip = AimboxM700ScopePipState.Frame;
		var showPip = pip.Active && pip.ScopeView.IsValid() && pip.Radius > 2f;
		_scopePipView.SetClass( "visible", showPip );
		_scopePipReticle.SetClass( "visible", showPip );
		if ( !showPip )
		{
			_scopePipView.Texture = default;
			return;
		}

		var scale = Panel?.ScaleFromScreen ?? 1f;
		var center = pip.Center * scale;
		center += AimboxM700ScopePipLayout.PanelOffset;

		var radius = pip.Radius * scale * AimboxM700ScopePipLayout.RadiusScale;
		var diameter = radius * 2f;
		var left = center.x - radius;
		var top = center.y - radius;

		_scopePipView.Style.Left = left;
		_scopePipView.Style.Top = top;
		_scopePipView.Style.Width = diameter;
		_scopePipView.Style.Height = diameter;
		_scopePipView.Texture = pip.ScopeView;

		_scopePipReticle.Style.Left = left;
		_scopePipReticle.Style.Top = top;
		_scopePipReticle.Style.Width = diameter;
		_scopePipReticle.Style.Height = diameter;
	}

	string ModeLabel
	{
		get
		{
			var mode = Match?.Mode ?? AimboxGameMode.FreeForAll;
			if ( !AimboxAimModeRules.IsAimMode( mode ) )
				return AimboxGameModeLabels.Short( mode );

			return $"AIM · {AimboxAimDrillLabels.Short( Match.ActiveAimDrill )} · {MathF.Ceiling( Match.TimeRemaining ):0}s";
		}
	}

	int GetDuelOpponentKills()
	{
		if ( Match is null || Player is null )
			return 0;

		foreach ( var (combatId, kills) in Match.PlayerKills )
		{
			if ( combatId != Player.AccountId )
				return kills;
		}

		return 0;
	}

	int GetPlayerKills( string accountId )
	{
		if ( Match is null || string.IsNullOrEmpty( accountId ) )
			return 0;

		return Match.PlayerKills.TryGetValue( accountId, out var kills ) ? kills : 0;
	}

	int CountAliveBots()
	{
		var bots = AimboxGame.Instance?.Bots;
		if ( bots is null )
			return 0;

		var count = 0;
		foreach ( var bot in bots )
		{
			if ( bot.IsAlive )
				count++;
		}

		return count;
	}

	string ScoreText
	{
		get
		{
			if ( Match is null || Player is null )
				return "0";

			if ( Match.Mode == AimboxGameMode.Survival )
			{
				var alive = CountAliveBots();
				return $"W{Match.SurvivalWave} · {alive}/{Match.SurvivalWaveBotTarget}";
			}

			if ( Match.Mode == AimboxGameMode.TeamDeathmatch )
				return $"{Match.GetTeamScore( AimboxTeam.Red ):N0} - {Match.GetTeamScore( AimboxTeam.Blue ):N0}";

			if ( Match.Mode == AimboxGameMode.Duel )
				return $"{GetPlayerKills( Player.AccountId )} - {GetDuelOpponentKills()}";

			if ( AimboxAimModeRules.IsAimMode( Match.Mode ) )
				return Match.GetAimScore( Player.AccountId ).ToString( "N0" );

			return Match.GetScore( Player.AccountId ).ToString( "N0" );
		}
	}

	string TimerText
	{
		get
		{
			if ( AimboxGame.Instance?.IsMatchFrozen == true )
			{
				var freeze = MathF.Ceiling( AimboxGame.Instance.FreezeTimeRemaining );
				return $"{AimboxGame.Instance.FreezeLabel} {freeze:0}";
			}

			if ( Match?.Mode == AimboxGameMode.Range )
				return "RANGE";

			var seconds = (int)(Match?.TimeRemaining ?? 0f);
			return $"{seconds / 60:00}:{seconds % 60:00}";
		}
	}

	void SetMode( AimboxGameMode mode )
	{
		AimboxGame.Instance?.StartMatch( mode );
		AimboxMetaNavigation.Close();
	}

	static Panel AddPanel( Panel parent, string classes )
	{
		var panel = new Panel();
		parent.AddChild( panel );
		foreach ( var cls in classes.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			panel.AddClass( cls );
		return panel;
	}

	static Image AddImage( Panel parent, string classes )
	{
		var image = new Image();
		parent.AddChild( image );
		foreach ( var cls in classes.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			image.AddClass( cls );
		return image;
	}

	static Label AddLabel( Panel parent, string text, string classes )
	{
		var label = new Label( text );
		parent.AddChild( label );
		foreach ( var cls in classes.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			label.AddClass( cls );
		return label;
	}

	static Panel AddButton( Panel parent, string text, Action onClick )
	{
		var button = AddPanel( parent, "menu-button" );
		AddLabel( button, text, "menu-button-label" );
		button.AddEventListener( "onclick", () => onClick?.Invoke() );
		return button;
	}

	void SyncThemeClasses() => AimboxUiTheme.SyncPanel( Panel );

	void RefreshSpectraHud( int health, bool isDead, AimboxWeaponRuntime weapon, string timer, string mode, string score )
	{
		_spectraMode.Text = mode;
		_spectraScoreValue.Text = score;
		_spectraTimer.Text = timer;
		_spectraHealthValue.Text = health.ToString();

		var healthPct = Math.Clamp( health / 100f, 0f, 1f );
		_healthVerticalFill.Style.Height = Length.Percent( healthPct * 100f );
		_spectraVitals.SetClass( "dead", isDead );

		if ( weapon is null )
		{
			_spectraWeapon.Text = "NO WEAPON";
			_ammoCurrent.Text = "--";
			_ammoReserve.Text = "--";
			return;
		}

		_spectraWeapon.Text = weapon.Definition.Name;
		if ( weapon.Definition.IsMelee )
		{
			_ammoCurrent.Text = "MELEE";
			_ammoReserve.Text = "";
			return;
		}

		_ammoCurrent.Text = AimAmmoCurrentText( Match, weapon );
		_ammoReserve.Text = AimAmmoReserveText( Match, weapon );
	}

	static string AimAmmoText( AimboxMatchSystem match, AimboxWeaponRuntime weapon ) =>
		AimboxAimModeRules.IsAimMode( match?.Mode ?? default ) ? "∞ / ∞" : $"{weapon.Ammo} / {weapon.Reserve}";

	static string AimAmmoCurrentText( AimboxMatchSystem match, AimboxWeaponRuntime weapon ) =>
		AimboxAimModeRules.IsAimMode( match?.Mode ?? default ) ? "∞" : weapon.Ammo.ToString();

	static string AimAmmoReserveText( AimboxMatchSystem match, AimboxWeaponRuntime weapon ) =>
		AimboxAimModeRules.IsAimMode( match?.Mode ?? default ) ? "" : weapon.Reserve.ToString();

	void RefreshHealthSegments( int health, bool isDead )
	{
		var filled = Math.Clamp( (int)MathF.Ceiling( health / 10f ), 0, _healthSegments.Count );
		_healthBar.SetClass( "dead", isDead );

		for ( var i = 0; i < _healthSegments.Count; i++ )
			_healthSegments[i].SetClass( "filled", i < filled );
	}

	void RefreshAmmoMagSegments( AimboxWeaponRuntime weapon )
	{
		if ( weapon is null || weapon.Definition.IsMelee )
		{
			foreach ( var segment in _ammoMagSegments )
				segment.SetClass( "filled", false );
			return;
		}

		if ( AimboxAimModeRules.IsAimMode( Match?.Mode ?? default ) )
		{
			foreach ( var segment in _ammoMagSegments )
				segment.SetClass( "filled", true );
			return;
		}

		var magSize = weapon.EffectiveMagazineSize;
		var filled = magSize <= 0
			? 0
			: Math.Clamp( (int)MathF.Round( weapon.Ammo / (float)magSize * _ammoMagSegments.Count ), 0, _ammoMagSegments.Count );

		for ( var i = 0; i < _ammoMagSegments.Count; i++ )
			_ammoMagSegments[i].SetClass( "filled", i < filled );
	}
}
