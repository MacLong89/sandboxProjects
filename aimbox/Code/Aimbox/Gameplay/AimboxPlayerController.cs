namespace Sandbox;

[Title( "Aimbox Player Controller" )]
[Category( "Aimbox" )]
public sealed class AimboxPlayerController : Component, IAimboxCombatActor
{
	[Property, Sync] public string AccountId { get; set; } = "offline";
	[Property, Sync] public AimboxTeam Team { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public int Health { get; set; } = 100;
	[Property, Sync( SyncFlags.FromHost )] public bool IsAlive { get; set; } = true;
	[Property, Sync] public AimboxWeaponId ActiveWeapon { get; set; }
	[Property, Sync] public float SyncEyePitch { get; set; }
	[Property, Sync] public bool SyncCrouching { get; set; }
	[Property, Sync] public int SyncPlayerLevel { get; set; } = 1;
	[Property, Sync] public string SyncDisplayName { get; set; } = "";

	GameObject IAimboxCombatActor.GameObject => GameObject;
	Scene IAimboxCombatActor.Scene => Scene;
	string IAimboxCombatActor.CombatId => AccountId;
	bool IAimboxCombatActor.IsHumanPlayer => true;
	bool IAimboxCombatActor.ShowThirdPersonBody => IsProxy;

	public Vector3 EyePosition => WorldPosition + Vector3.Up * (IsCrouching ? 42f : 64f);
	public Vector3 AimForward => EyeRotation.Forward;
	public Rotation EyeRotation { get; private set; } = Rotation.Identity;
	public AimboxPlayerData Data { get; private set; }
	public int KillStreak { get; private set; }
	public int RecentKillCount { get; private set; }
	public string LastKillerAccountId { get; set; }
	public AimboxWeaponRuntime CurrentWeapon => _inventory.GetById( ActiveWeapon );

	public AimboxWeaponRuntime GetWeaponInventorySlot( int slot ) => _inventory.GetSlot( slot );
	public float CombatDamageMultiplier => _perkRuntime.DamageMultiplier;
	public float MovementNoiseMultiplier => _perkRuntime.MovementNoiseMultiplier;
	public IReadOnlyList<AimboxMedalId> RecentMedals => _recentMedals;
	public string KillstreakNotification { get; private set; }
	public TimeUntil KillstreakNotificationVisibleUntil { get; private set; }
	public int EarnedKillstreakCount => AimboxGame.Instance?.Killstreaks.GetEarned( AccountId ).Count ?? 0;
	public string ProgressionFeed { get; private set; }
	public string ProgressionFeedDetail { get; private set; }
	public bool ProgressionFeedIsLevelUp { get; private set; }
	public TimeUntil ProgressionFeedVisibleUntil { get; private set; }
	public string MedalFeed { get; private set; }
	public TimeUntil MedalFeedVisibleUntil { get; private set; }
	public AimboxUnlockCelebrationKind UnlockCelebrationKind { get; private set; }
	public string UnlockCelebrationKicker { get; private set; }
	public string UnlockCelebrationTitle { get; private set; }
	public string UnlockCelebrationDetail { get; private set; }
	public IReadOnlyList<string> UnlockCelebrationExtras { get; private set; } = [];
	public TimeUntil UnlockCelebrationVisibleUntil { get; private set; }
	public int LethalGrenadesRemaining { get; private set; }
	public int TacticalGrenadesRemaining { get; private set; }
	public bool GrenadeThrowCooldownActive => _grenadeThrowCooldown > 0f;
	public float FlashBlind01 { get; private set; }
	public AimboxGrenadeEquipPhase GrenadeEquipPhase { get; private set; }
	public AimboxWeaponId? GrenadePresentationWeaponId { get; private set; }
	public bool GrenadeEquipIsLethal { get; private set; }
	public bool IsGrenadeEquipped => GrenadeEquipPhase != AimboxGrenadeEquipPhase.None;
	public AimboxWeaponId ThirdPersonPresentationWeaponId => GrenadePresentationWeaponId ?? ActiveWeapon;
	public bool IsCrouching => IsProxy ? SyncCrouching : _isSliding || Input.Down( "Duck" );
	public int DisplayLevel => IsProxy ? SyncPlayerLevel : Data?.PlayerLevel ?? SyncPlayerLevel;
	public string DisplayName => ResolveDisplayName();
	public bool IsSliding => _isSliding;
	public Vector3 GetMovementVelocity() => _velocity;

	readonly AimboxWeaponInventory _inventory = new();
	readonly AimboxPerkRuntime _perkRuntime = new();
	readonly List<AimboxMedalId> _recentMedals = [];
	readonly List<AimboxUnlock> _sessionUnlocks = [];
	readonly List<string> _sessionCompletedChallenges = [];
	readonly Dictionary<AimboxWeaponId, (int Level, int Xp)> _matchStartWeaponProgress = [];
	readonly Dictionary<AimboxWeaponId, int> _sessionMasteryXp = [];
	int _matchStartRank;
	int _matchStartXp;
	int _matchStartKills;
	bool _restoreWeaponAfterGrenadeThrow;
	int _matchStartDeaths;
	Vector3 _velocity;
	bool _isSliding;
	bool _jumpQueued;
	TimeSince _movementNoiseEmit;
	float _pitch;
	TimeSince _lastKillTime;
	TimeUntil _grenadeThrowCooldown;
	TimeUntil _flashBlindUntil;
	float _flashBlindDuration;
	bool _grenadePrimaryWasDown;
	TimeSince _deadTime;
	TimeSince _lastHitMarker = 999f;
	TimeSince _lastHeadshotMarker = 999f;
	bool _deathPresentationApplied;
	bool _wasAlive = true;
	AimboxViewModelController _viewModelController;
	AimboxWeaponCombatComponent _weaponCombat;
	CapsuleCollider _collider;
	AimboxWeaponId? _lastEquippedWeapon;
	AimboxWeaponId? _grenadeViewmodelWeaponId;
	List<AimboxAttachmentId> _lastEquippedAttachments = [];
	CameraComponent _playerCamera;
	GameObject _playerCameraObject;
	bool IsSprinting => Input.Down( "Run" ) && !IsCrouching;
	public bool IsSprintMoving =>
		(IsSprinting || _perkRuntime.UnlimitedSprint)
		&& _velocity.WithZ( 0f ).LengthSquared > 400f;
	bool IsAds => Input.Down( "Attack2" );
	bool _fovSmoothedPrimed;
	float _fovSmoothed;
	AimboxAdsSightMode _adsSightMode;
	float _adsPresentationBlend;
	TimeSince _adsHeld;
	bool _wasAds;
	bool _classicScopeOverlayArmed;
	TimeSince _classicScopeOverlayHold;
	bool _classicScopeEngaged;

	public AimboxAdsSightMode AdsSightMode => _adsSightMode;
	public float AdsPresentationBlend => _adsPresentationBlend;
	public bool HideStandardCrosshair =>
		WantsAds
		&& _adsPresentationBlend >= AimboxAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode != AimboxAdsSightMode.SniperScope;

	/// <summary>Classic sniper scope — black ring overlay, main-camera zoom, HUD stripped (crosshair + hitmarkers only).</summary>
	public bool ShowClassicSniperScope => IsClassicSniperScopeActive();

	/// <summary>Scope-eye render target composited onto the physical optic lens (legacy PiP — disabled).</summary>
	public bool ShowScopePip =>
		false;

	/// <summary>Legacy name — same as <see cref="ShowScopePip"/>.</summary>
	public bool ShowM700ScopePip => ShowScopePip;

	/// <summary>Screen-center aim dot while ADS with a holo sight (replaces the hidden hip crosshair).</summary>
	public bool ShowHoloSightCenterDot =>
		HideStandardCrosshair
		&& _adsSightMode == AimboxAdsSightMode.RedDot
		&& CurrentWeapon?.Attachments.Contains( AimboxAttachmentId.HoloSight ) == true;

	public bool UseScopedLookSensitivity => ShowClassicSniperScope;

	public bool UseRedDotLookSensitivity =>
		WantsAds
		&& _adsPresentationBlend >= AimboxAdsSightTuning.HideCrosshairBlend
		&& _adsSightMode == AimboxAdsSightMode.RedDot;

	public bool ShowHitMarker => _lastHitMarker < 0.16f;
	public bool ShowHeadshotMarker => _lastHeadshotMarker < 0.2f;
	public int LastDamageDealt { get; private set; }
	public float RespawnTimeRemaining => !IsAlive ? MathF.Max( 0f, RespawnDelaySeconds - _deadTime ) : 0f;

	float RespawnDelaySeconds => AimboxGame.Instance?.Respawns.RespawnDelay ?? 3f;

	const float HipFieldOfView = AimboxAdsSightTuning.HipFieldOfView;
	const float AdsFieldOfView = AimboxAdsSightTuning.IronSightAdsFov;
	const float AdsFovLerpSpeed = 20f;

	bool WantsAds =>
		!IsGrenadeEquipped
		&& ((AimboxM700ScopePipTuner.IsActive
		     && AimboxM700ScopePipTuner.SupportsPlayer( this )
		     && AllowsAdsPresentation())
		    || (AimboxOpticAdsTuner.IsActive
		        && AimboxOpticAdsTuner.SupportsWeapon( this )
		        && AllowsAdsPresentation())
		    || (IsAds
		        && CurrentWeapon is { Definition.IsMelee: false, Definition.IsBow: false }
		        && AllowsAdsPresentation()));

	public bool WantsAdsPresentationForDebug => WantsAds;

	protected override void OnStart()
	{
		AccountId = ResolveAccountId();
		AimboxCitizenPresentation.EnsureCitizenBody( this );
		AimboxCitizenPresentation.SetLocalBodyHidden( this, !IsProxy );
		_collider = Components.Get<CapsuleCollider>();
		AimboxGame.Instance?.RegisterPlayer( this );

		if ( IsProxy )
		{
			Log.Info( $"[Aimbox] Remote player registered. Account={AccountId}, Name={DisplayName}." );
			return;
		}

		_ = InitializeLocalPlayerAsync();
	}

	async Task InitializeLocalPlayerAsync()
	{
		var game = AimboxGame.Instance;
		if ( game.IsValid() )
			await game.WaitForWeaponPackagesAsync();

		Data = AimboxGame.Instance?.PlayerDataService.LoadPlayer( AccountId ) ?? new AimboxPlayerData { AccountId = AccountId };
		ApplyLoadout( AimboxGame.Instance?.Loadouts.GetActiveLoadout( Data ) ?? AimboxLoadoutData.Default() );
		RefillGrenades();
		SyncProfileToNetwork();
		EnsurePlayerCamera();
		EnsureWeaponCombat();
		Log.Info( $"[Aimbox] Local player ready. Account={AccountId}, Rank={Data.PlayerLevel}, Xp={Data.TotalXp}, Weapon={ActiveWeapon}." );
	}

	protected override void OnDestroy()
	{
		AimboxGame.Instance?.UnregisterPlayer( this );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
		{
			TickProxyPresentation();
			return;
		}

		SyncEyePitch = _pitch;
		SyncCrouching = _isSliding || Input.Down( "Duck" );
		SyncProfileToNetwork();
		TickSyncedLifeState();

		EnsureFirstPersonBodyHidden();
		TickDeathRespawn();

		if ( AimboxCursor.GameplayMenuOpen || AimboxMetaNavigation.BlocksGameplay )
		{
			UpdateCamera();
			return;
		}

		if ( AimboxGame.Instance?.IsCombatLocked == true )
		{
			SyncEyeRotation();
			UpdateCamera();
			return;
		}

		if ( !IsAlive )
		{
			SyncEyeRotation();
			UpdateCamera();
			return;
		}

		UpdateWeaponInput();
		TickFlashBlind();
		AimboxM700ScopePipTuner.Tick( this );
		AimboxOpticAdsTuner.Tick( this );
		IntegrateWeaponRecoil();
		UpdateLookInput();

		if ( Input.Pressed( "Jump" ) )
			_jumpQueued = true;

		UpdateMovement();
		SyncEyeRotation();
		TickAdsSightState();
		UpdateViewModel();
		UpdateCamera();
		TickM700ScopeDebug();
		TickM700ScopeInvestigation();
	}

	void TickM700ScopeInvestigation()
	{
		if ( IsProxy )
			return;

		AimboxM700ScopeInvestigationDebug.Tick(
			this,
			_viewModelController,
			_adsSightMode,
			_adsPresentationBlend );
	}

	void TickM700ScopeDebug()
	{
		if ( IsProxy )
			return;

		AimboxM700ScopeDebug.Tick(
			this,
			_viewModelController,
			_adsSightMode,
			_adsPresentationBlend,
			_viewModelController?.AdsBlend01 ?? 0f,
			WantsAds && _adsSightMode != AimboxAdsSightMode.None ? Math.Clamp( _adsHeld / 0.35f, 0f, 1f ) : 0f,
			ResolveTargetFieldOfView(),
			_fovSmoothed );
	}

	void TickDeathRespawn()
	{
		if ( AimboxNetworkCombat.UseHostAuthority && !Networking.IsHost )
			return;

		if ( IsAlive || AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing )
			return;

		if ( AimboxGame.Instance?.Match.Mode == AimboxGameMode.Duel )
			return;

		if ( AimboxGame.Instance?.Match.Mode == AimboxGameMode.Survival )
			return;

		if ( _deadTime >= RespawnDelaySeconds )
			Respawn();
	}

	void EnsureFirstPersonBodyHidden()
	{
		AimboxCitizenPresentation.SetLocalBodyHidden( this, true );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !IsAlive || AimboxMetaNavigation.BlocksGameplay || AimboxGame.Instance?.IsCombatLocked == true )
			return;

		TickMovementNoise();
		Data.TimePlayed += Time.Delta;
	}

	void TickMovementNoise()
	{
		if ( !IsGrounded() )
			return;

		var speed = _velocity.WithZ( 0f ).Length;
		if ( speed < 80f || _movementNoiseEmit < AimboxBotTuning.MovementNoiseEmitInterval )
			return;

		_movementNoiseEmit = 0;
		var sprinting = IsSprinting || _perkRuntime.UnlimitedSprint;
		var loudness = sprinting
			? AimboxBotTuning.HearingRadiusSprintFootsteps
			: AimboxBotTuning.HearingRadiusWalkFootsteps;
		if ( IsCrouching )
			loudness *= 0.6f;

		AimboxCombatNoiseBus.EmitMovement( this, loudness, MovementNoiseMultiplier );
		AimboxGameplaySfx.PlayFootstep( this, sprinting, IsCrouching, MovementNoiseMultiplier );
	}

	void ApplyLoadout( AimboxLoadoutData loadout )
	{
		if ( Data is null || IsProxy )
			return;
		var validated = AimboxGame.Instance?.Loadouts.ValidateLoadout( Data, loadout ) ?? loadout;
		_perkRuntime.ApplyFromLoadout( validated, Data );

		if ( !IsProxy && AimboxGame.Instance?.GunBuilderScene == true )
		{
			AimboxAttachmentPipelineDebug.Reg(
				"ApplyLoadout skipped — gun builder scene; Aimbox Gun Builder inspector drives weapon/attachments." );
			AimboxGunBuilder.Instance?.ForceReapply();
			return;
		}

		if ( !IsProxy && AimboxGame.Instance?.DebugSandboxLoadout == true )
		{
			_inventory.ApplyDebugSandboxLoadout( _perkRuntime );
			ActiveWeapon = AimboxWeaponId.M4A1;
			_lastEquippedWeapon = null;
			return;
		}

		_inventory.ApplyLoadout( validated, Data, _perkRuntime );
		ActiveWeapon = _inventory.Slots.Count > 0 ? _inventory.Slots[0].Definition.Id : AimboxWeaponId.Usp;
	}

	void IntegrateWeaponRecoil()
	{
		if ( _weaponCombat is null )
			return;

		var yaw = WorldRotation;
		_weaponCombat.IntegrateRecoil( ref _pitch, ref yaw );
		WorldRotation = yaw;
	}

	public float GetCombatPitch() => _pitch;

	public void SetCombatPitch( float pitch ) => _pitch = Math.Clamp( pitch, -85f, 85f );

	void EnsureWeaponCombat()
	{
		_weaponCombat ??= Components.GetOrCreate<AimboxWeaponCombatComponent>();
	}

	bool AllowsAdsPresentation()
	{
		EnsureViewModelController();
		return AimboxDefaultPresentationGate.Instance.AllowsAds( this, _viewModelController );
	}

	bool AllowsScopePipPresentation()
	{
		if ( CurrentWeapon is { IsReloading: true } )
			return false;

		EnsureViewModelController();
		return _viewModelController is { IsValid: true, PresentationAllowsScopePip: true };
	}

	bool IsScopeAdsMotionComplete()
	{
		if ( AimboxM700ScopePipTuner.IsActive
		     && AimboxM700ScopePipTuner.SupportsPlayer( this ) )
			return true;

		if ( AimboxOpticAdsTuner.IsActive && AimboxOpticAdsTuner.SupportsWeapon( this ) )
			return true;

		var adsBlend = _viewModelController?.AdsBlend01 ?? 0f;
		return adsBlend >= AimboxAdsSightTuning.SniperClassicScopeEnterBlend;
	}

	bool ResolveScopePipEnabled() =>
		CurrentWeapon?.Attachments.Contains( AimboxAttachmentId.RangedSight ) == true;

	void UpdateLookInput()
	{
		var lookScale = AimboxAdsSightTuning.DefaultLookScale;
		if ( UseScopedLookSensitivity )
			lookScale *= AimboxAdsSightTuning.SniperLookMultiplier;
		else if ( UseRedDotLookSensitivity )
			lookScale *= AimboxAdsSightTuning.RedDotLookMultiplier;

		var angles = WorldRotation.Angles();
		angles.yaw -= Input.MouseDelta.x * lookScale;
		_pitch = Math.Clamp( _pitch + Input.MouseDelta.y * lookScale, -85f, 85f );
		WorldRotation = Rotation.FromYaw( angles.yaw );
	}

	public void RefreshGameplayCamera() => EnsurePlayerCamera();

	public void SetGameplayCameraActive( bool active )
	{
		EnsurePlayerCamera();
		if ( _playerCameraObject.IsValid() )
			_playerCameraObject.Enabled = active;

		if ( !_playerCamera.IsValid() )
			return;

		_playerCamera.Enabled = active;
		_playerCamera.IsMainCamera = active;
		if ( active )
		{
			_playerCamera.Priority = 32;
			SyncGameplayCameraPresentation();
		}
	}

	public void SyncGameplayCameraPresentation()
	{
		if ( IsProxy )
			return;

		SyncEyeRotation();
		UpdateCamera();
	}

	void EnsurePlayerCamera()
	{
		if ( _playerCameraObject.IsValid() && _playerCamera.IsValid() )
			return;

		var rootCamera = Components.Get<CameraComponent>();
		if ( rootCamera.IsValid() )
			rootCamera.Destroy();

		_playerCameraObject = new GameObject( true, "Aimbox Player Camera" );
		_playerCameraObject.NetworkMode = NetworkMode.Never;
		_playerCameraObject.Enabled = false;

		_playerCamera = _playerCameraObject.Components.Create<CameraComponent>();
		_playerCamera.IsMainCamera = false;
		_playerCamera.Enabled = false;
		_playerCamera.Priority = 32;
		_playerCamera.FieldOfView = HipFieldOfView;
		_playerCamera.ZNear = 0.08f;
		_playerCamera.ZFar = 10000f;
		_playerCameraObject.Components.GetOrCreate<AimboxM700ScopePipHud>();
		_playerCameraObject.Components.GetOrCreate<AimboxM700ScopePipTunerHud>();
		_playerCameraObject.Components.GetOrCreate<AimboxOpticAdsTunerHud>();
	}

	CameraComponent ActiveCamera
	{
		get
		{
			if ( !_playerCameraObject.IsValid() || _playerCamera is null || !_playerCamera.IsValid() )
				EnsurePlayerCamera();

			return _playerCamera;
		}
	}

	void SyncEyeRotation()
	{
		var yaw = WorldRotation.Angles().yaw;
		EyeRotation = Rotation.From( new Angles( Math.Clamp( _pitch, -89f, 89f ), yaw, 0f ) );
	}

	void UpdateCamera()
	{
		var camera = ActiveCamera;
		if ( camera is null || !_playerCameraObject.IsValid() )
			return;

		SyncPresentationCamera();

		if ( !_fovSmoothedPrimed )
		{
			_fovSmoothed = camera.FieldOfView;
			_fovSmoothedPrimed = true;
		}

		var adsSpeedMul = CurrentWeapon?.EffectiveAdsPresentationSpeedMultiplier ?? 1f;
		var targetFov = ResolveTargetFieldOfView();
		if ( TryGetBowDrawFovTarget( HipFieldOfView, out var bowFov ) )
			targetFov = bowFov;

		var t = Math.Clamp( Time.Delta * AdsFovLerpSpeed * adsSpeedMul, 0f, 1f );
		_fovSmoothed = MathX.Lerp( _fovSmoothed, targetFov, t );
		camera.FieldOfView = _fovSmoothed;
	}

	protected override void OnPreRender()
	{
		if ( !IsProxy )
			AimboxCursor.Sync();

		if ( IsProxy || !_playerCameraObject.IsValid() || _playerCamera is null || !_playerCamera.Enabled )
			return;

		SyncPresentationCamera();
	}

	void SyncPresentationCamera()
	{
		if ( !_playerCameraObject.IsValid() || _playerCamera is null || !_playerCamera.IsValid() )
			return;

		_playerCameraObject.WorldPosition = EyePosition;
		_playerCameraObject.WorldRotation = EyeRotation;
		_playerCameraObject.Transform.ClearInterpolation();
	}

	void UpdateViewModel()
	{
		EnsureViewModelController();
		if ( _viewModelController is null || !_viewModelController.IsValid() )
			return;

		if ( IsGrenadeEquipped && GrenadePresentationWeaponId is { } grenadeId )
		{
			var grenadeDef = AimboxWeapons.Get( grenadeId );
			if ( _grenadeViewmodelWeaponId != grenadeId )
			{
				_viewModelController.EnsureGrenadePresentation( grenadeDef );
				_grenadeViewmodelWeaponId = grenadeId;
			}

			TickGrenadePresentationState();
			_viewModelController.TickGrenadePresentation(
				IsSprinting,
				IsCrouching,
				EyeRotation.Angles(),
				_velocity,
				IsGrounded(),
				320f );
			return;
		}

		if ( CurrentWeapon is null )
			return;

		var attachments = CurrentWeapon.Attachments;
		if ( _lastEquippedWeapon != ActiveWeapon )
		{
			if ( _restoreWeaponAfterGrenadeThrow )
			{
				_viewModelController.EnsureWeaponAfterGrenadeThrow(
					CurrentWeapon.Definition,
					attachments,
					CurrentWeapon.PerkPresentationSpeedMultiplier );
				_restoreWeaponAfterGrenadeThrow = false;
			}
			else
			{
				_viewModelController.EnsureWeapon(
					CurrentWeapon.Definition,
					attachments,
					CurrentWeapon.PerkPresentationSpeedMultiplier );
			}

			_lastEquippedWeapon = ActiveWeapon;
			_lastEquippedAttachments = attachments.ToList();
		}
		else if ( !AttachmentSetsEqual( _lastEquippedAttachments, attachments ) )
		{
			_viewModelController.SyncAttachments( ActiveWeapon, attachments );
			_lastEquippedAttachments = attachments.ToList();
		}

		_viewModelController.TickPresentation(
			WantsAds,
			CurrentWeapon,
			Input.Down( "Attack1" ),
			IsSprinting,
			CurrentWeapon.EffectiveAdsPresentationSpeedMultiplier );
	}

	void TickGrenadePresentationState()
	{
		if ( GrenadeEquipPhase == AimboxGrenadeEquipPhase.Equipping
		     && _viewModelController?.Animator?.GrenadeReady == true )
		{
			SetGrenadeEquipPhase( AimboxGrenadeEquipPhase.Ready );
		}
	}

	void TickAdsSightState()
	{
		_adsSightMode = ResolveAdsSightMode();

		if ( AimboxM700ScopePipTuner.IsActive
		     && AimboxM700ScopePipTuner.SupportsPlayer( this ) )
		{
			_adsSightMode = AimboxAdsSightMode.SniperScope;
			_adsPresentationBlend = 1f;
			_wasAds = true;
			TickClassicScopeEngagement();
			TickClassicScopeOverlayDelay();
			_viewModelController?.ApplySightPresentation( _adsSightMode, _adsPresentationBlend, IsClassicSniperScopeActive() );
			return;
		}

		if ( AimboxOpticAdsTuner.IsActive && AimboxOpticAdsTuner.SupportsWeapon( this ) )
		{
			_adsSightMode = ResolveAdsSightModeForOpticTuner();
			_adsPresentationBlend = 1f;
			_wasAds = true;
			TickClassicScopeEngagement();
			TickClassicScopeOverlayDelay();
			_viewModelController?.ApplySightPresentation( _adsSightMode, _adsPresentationBlend, IsClassicSniperScopeActive() );
			return;
		}

		if ( WantsAds && _adsSightMode != AimboxAdsSightMode.None )
		{
			if ( !_wasAds )
				_adsHeld = 0;

			var animBlend = _viewModelController?.AdsBlend01 ?? 0f;
			var fallbackBlend = Math.Clamp( _adsHeld / 0.35f, 0f, 1f );
			_adsPresentationBlend = animBlend > 0.01f ? animBlend : fallbackBlend;
		}
		else
		{
			_adsPresentationBlend = 0f;
			_classicScopeEngaged = false;
		}

		_wasAds = WantsAds && _adsSightMode != AimboxAdsSightMode.None;
		TickClassicScopeEngagement();
		TickClassicScopeOverlayDelay();
		_viewModelController?.ApplySightPresentation( _adsSightMode, _adsPresentationBlend, IsClassicSniperScopeActive() );
	}

	bool IsClassicSniperScopeEligible() =>
		!IsGunBuilderLab()
		&& WantsAds
		&& _adsSightMode == AimboxAdsSightMode.SniperScope
		&& _classicScopeEngaged
		&& AllowsScopePipPresentation()
		&& ResolveScopePipEnabled();

	void TickClassicScopeEngagement()
	{
		if ( IsGunBuilderLab()
		     || !WantsAds
		     || _adsSightMode != AimboxAdsSightMode.SniperScope
		     || !AllowsScopePipPresentation()
		     || !ResolveScopePipEnabled() )
		{
			_classicScopeEngaged = false;
			return;
		}

		if ( _classicScopeEngaged )
		{
			if ( _adsPresentationBlend < AimboxAdsSightTuning.SniperClassicScopeExitBlend )
				_classicScopeEngaged = false;
		}
		else if ( _adsPresentationBlend >= AimboxAdsSightTuning.SniperClassicScopeEnterBlend )
		{
			_classicScopeEngaged = true;
		}
	}

	static bool IsGunBuilderLab() => AimboxGame.Instance?.GunBuilderScene == true;

	bool IsClassicSniperScopeActive() =>
		IsClassicSniperScopeEligible()
		&& _classicScopeOverlayArmed
		&& _classicScopeOverlayHold >= AimboxAdsSightTuning.SniperClassicScopeOverlayDelaySeconds;

	void TickClassicScopeOverlayDelay()
	{
		if ( IsClassicSniperScopeEligible() )
		{
			if ( !_classicScopeOverlayArmed )
			{
				_classicScopeOverlayArmed = true;
				_classicScopeOverlayHold = 0f;
			}

			return;
		}

		_classicScopeOverlayArmed = false;
	}

	AimboxAdsSightMode ResolveAdsSightMode()
	{
		if ( !WantsAds || CurrentWeapon is null )
			return AimboxAdsSightMode.None;

		if ( ActiveWeapon == AimboxWeaponId.M700 )
		{
			if ( CurrentWeapon?.Attachments.Contains( AimboxAttachmentId.RangedSight ) == true )
				return AimboxAdsSightMode.SniperScope;

			return AimboxAdsSightMode.IronSight;
		}

		var equippedSight = AimboxAttachmentCatalog.ResolveEquippedSight( CurrentWeapon.Attachments );
		if ( equippedSight.HasValue && AimboxAttachmentCatalog.ResolveAdsMode( equippedSight.Value ) is { } sightMode )
			return sightMode;

		return AimboxAdsSightMode.IronSight;
	}

	AimboxAdsSightMode ResolveAdsSightModeForOpticTuner()
	{
		return AimboxOpticAdsLayout.ResolveTuningTarget( this ) switch
		{
			OpticAdsTuningTarget.M4RangedSight or OpticAdsTuningTarget.M700RangedSight => AimboxAdsSightMode.SniperScope,
			OpticAdsTuningTarget.M4Holo or OpticAdsTuningTarget.M4RaisedRedDot => AimboxAdsSightMode.RedDot,
			_ => AimboxAdsSightMode.None
		};
	}

	float ResolveTargetFieldOfView()
	{
		if ( !WantsAds || _adsSightMode == AimboxAdsSightMode.None )
			return HipFieldOfView;

		if ( _adsSightMode == AimboxAdsSightMode.SniperScope )
		{
			if ( !IsClassicSniperScopeActive() )
				return HipFieldOfView;

			var scopeT = Math.Clamp(
				( _adsPresentationBlend - AimboxAdsSightTuning.SniperClassicScopeEnterBlend )
				/ ( 1f - AimboxAdsSightTuning.SniperClassicScopeEnterBlend ),
				0f,
				1f );
			return MathX.Lerp(
				HipFieldOfView,
				AimboxAdsSightTuning.SniperScopeViewFov,
				scopeT );
		}

		var scopedFov = AimboxAdsSightTuning.ResolveAdsFieldOfView( _adsSightMode, CurrentWeapon?.Attachments );

		return MathX.Lerp( HipFieldOfView, scopedFov, _adsPresentationBlend );
	}

	void EnsureViewModelController()
	{
		var cameraObject = _playerCameraObject;
		if ( !cameraObject.IsValid() )
			return;

		_viewModelController ??= cameraObject.Components.Get<AimboxViewModelController>();
		if ( _viewModelController is null || !_viewModelController.IsValid() )
			_viewModelController = cameraObject.Components.Create<AimboxViewModelController>();

		_viewModelController.BindOwner( this );
	}

	void UpdateMovement()
	{
		var wish = Vector3.Zero;
		if ( Input.Down( "Forward" ) ) wish += WorldRotation.Forward;
		if ( Input.Down( "Backward" ) ) wish -= WorldRotation.Forward;
		if ( Input.Down( "Right" ) ) wish += WorldRotation.Right;
		if ( Input.Down( "Left" ) ) wish -= WorldRotation.Right;

		var state = new AimboxCitizenMovementState
		{
			Velocity = _velocity,
			Position = WorldPosition,
			IsSliding = _isSliding
		};

		AimboxCitizenMovementMotor.Tick(
			Scene,
			GameObject,
			ref state,
			new AimboxCitizenMovementInput
			{
				WishDirection = wish,
				Sprint = IsSprinting || _perkRuntime.UnlimitedSprint,
				Crouch = IsCrouching,
				AdsSlowdown = WantsAds,
				Jump = _jumpQueued,
				SlideStartRequested = Input.Pressed( "Duck" ) && Input.Down( "Run" ) && !WantsAds,
				SpeedMultiplier = _perkRuntime.MoveSpeedMultiplier,
				UnlimitedSprint = _perkRuntime.UnlimitedSprint
			},
			WorldRotation,
			Time.Delta );

		_jumpQueued = false;

		_velocity = state.Velocity;
		_isSliding = state.IsSliding;
		WorldPosition = state.Position;
		Transform.ClearInterpolation();
		SyncCitizenHitbox();
	}

	void SyncCitizenHitbox()
	{
		if ( _collider.IsValid() && _collider.Enabled )
			AimboxHitboxes.ApplyCitizenHitbox( _collider, IsCrouching );
	}

	bool IsGrounded() => AimboxCitizenMovementMotor.IsGrounded( Scene, GameObject, WorldPosition );

	void UpdateWeaponInput()
	{
		if ( IsGrenadeEquipped )
		{
			UpdateGrenadeInput();
			return;
		}

		if ( Input.Pressed( "Slot1" ) ) EquipSlot( 0 );
		if ( Input.Pressed( "Slot2" ) ) EquipSlot( 1 );
		if ( Input.Pressed( "Slot3" ) ) EquipSlot( 2 );
		if ( Input.Pressed( "Slot4" ) ) EquipSlot( 3 );
		if ( Input.Pressed( "Slot5" ) ) EquipSlot( 4 );
		if ( Input.Pressed( "SlotPrev" ) ) EquipRelativeSlot( -1 );
		if ( Input.Pressed( "SlotNext" ) ) EquipRelativeSlot( 1 );
		if ( Input.Pressed( "Use" ) )
			AimboxGame.Instance?.Killstreaks.TryActivate( this );

		if ( Input.Pressed( "Drop" ) )
			AimboxGame.Instance?.Grenades.TryBeginEquipLethal( this );

		if ( Input.Pressed( "Flashlight" ) )
			AimboxGame.Instance?.Grenades.TryBeginEquipTactical( this );

		var weapon = CurrentWeapon;
		if ( weapon is null )
			return;

		weapon.Update( Time.Delta );

		if ( Input.Pressed( "Reload" ) && !AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default ) )
			weapon.StartReload();

		if ( Input.Down( "Attack1" ) )
		{
			var fired = TryFire( weapon );
			var animator = _viewModelController?.Animator;
			if ( fired && animator is not null )
			{
				if ( weapon.Definition.IsMelee )
					animator.OwnerNotifyMeleeAttackCommitted( Input.Down( "Attack2" ) );
				else
					animator.OwnerNotifyServerConfirmedFire();
			}
		}

		if ( weapon.ReloadStartedThisTick )
			AimboxGameplaySfx.PlayReload( this, weapon.Definition );
	}

	void UpdateGrenadeInput()
	{
		if ( Input.Pressed( "Drop" ) && GrenadeEquipIsLethal )
			AimboxGame.Instance?.Grenades.TryBeginEquipLethal( this );
		else if ( Input.Pressed( "Flashlight" ) && !GrenadeEquipIsLethal )
			AimboxGame.Instance?.Grenades.TryBeginEquipTactical( this );

		if ( Input.Pressed( "Slot1" ) ) { CancelGrenadeEquip(); EquipSlot( 0 ); return; }
		if ( Input.Pressed( "Slot2" ) ) { CancelGrenadeEquip(); EquipSlot( 1 ); return; }
		if ( Input.Pressed( "Slot3" ) ) { CancelGrenadeEquip(); EquipSlot( 2 ); return; }
		if ( Input.Pressed( "Slot4" ) ) { CancelGrenadeEquip(); EquipSlot( 3 ); return; }
		if ( Input.Pressed( "Slot5" ) ) { CancelGrenadeEquip(); EquipSlot( 4 ); return; }
		if ( Input.Pressed( "SlotPrev" ) ) { CancelGrenadeEquip(); EquipRelativeSlot( -1 ); return; }
		if ( Input.Pressed( "SlotNext" ) ) { CancelGrenadeEquip(); EquipRelativeSlot( 1 ); return; }
		if ( Input.Pressed( "Reload" ) )
		{
			CancelGrenadeEquip();
			return;
		}

		if ( Input.Pressed( "Attack2" )
		     && GrenadeEquipPhase is AimboxGrenadeEquipPhase.Ready or AimboxGrenadeEquipPhase.Charging )
		{
			AimboxGame.Instance?.Grenades.TryQuickTossEquipped( this );
			AimboxCursor.SyncAfterUi();
			return;
		}

		var attackDown = Input.Down( "Attack1" );
		var attackPressed = Input.Pressed( "Attack1" );
		var attackReleased = _grenadePrimaryWasDown && !attackDown;

		if ( GrenadeEquipPhase == AimboxGrenadeEquipPhase.Ready && attackPressed )
		{
			if ( _viewModelController?.BeginGrenadeCharge() == true )
				SetGrenadeEquipPhase( AimboxGrenadeEquipPhase.Charging );
		}
		else if ( GrenadeEquipPhase == AimboxGrenadeEquipPhase.Charging && attackReleased )
		{
			AimboxGame.Instance?.Grenades.TryThrowEquipped( this );
		}

		if ( attackPressed || attackReleased )
			AimboxCursor.SyncAfterUi();

		_grenadePrimaryWasDown = attackDown;
	}

	void EquipSlot( int slot )
	{
		var weapon = _inventory.GetSlot( slot );
		if ( weapon is null )
			return;

		ActiveWeapon = weapon.Definition.Id;
		_lastEquippedWeapon = null;
		_weaponCombat?.ResetRecoilState();
		_viewModelController?.ResetViewKick();
		Log.Info( $"[Aimbox] Equipped Slot {slot + 1}: {weapon.Definition.Name}." );
		AimboxAttachmentPipelineDebug.Reg(
			$"EquipSlot {slot + 1} weapon={weapon.Definition.Id} runtimeAttachments=[{AimboxAttachmentPipelineDebug.FormatList( weapon.Attachments )}]" );

		if ( AimboxGame.Instance?.DebugSandboxLoadout == true )
		{
			var loadout = AimboxGame.Instance.Loadouts.GetActiveLoadout( Data );
			var fromLoadout = loadout.Attachments.GetValueOrDefault( weapon.Definition.Id );
			if ( fromLoadout is { Count: > 0 } )
			{
				var sanitized = AimboxAttachmentCatalog.SanitizeForWeapon( weapon.Definition.Id, fromLoadout );
				if ( _inventory.TryReplaceSlotAttachments( weapon.Definition.Id, sanitized ) )
				{
					weapon = _inventory.GetSlot( slot );
					AimboxAttachmentPipelineDebug.Reg(
						$"EquipSlot merged loadout attachments for {weapon?.Definition.Id}: [{AimboxAttachmentPipelineDebug.FormatList( weapon?.Attachments )}]" );
				}
			}
		}

		AimboxGameplaySfx.PlayEquip( this, weapon.Definition );
	}

	void EquipRelativeSlot( int direction )
	{
		if ( _inventory.Slots.Count <= 0 )
			return;

		var index = _inventory.Slots.ToList().FindIndex( x => x.Definition.Id == ActiveWeapon );
		if ( index < 0 )
			index = 0;

		var next = (index + direction) % _inventory.Slots.Count;
		if ( next < 0 )
			next += _inventory.Slots.Count;

		EquipSlot( next );
	}

	bool TryFire( AimboxWeaponRuntime weapon )
	{
		EnsureWeaponCombat();
		var meleeHeavy = weapon.Definition.IsMelee && Input.Down( "Attack2" );
		return _weaponCombat.TryFire(
			weapon,
			WantsAds,
			_velocity.WithZ( 0f ).Length > 55f,
			IsCrouching,
			meleeHeavy,
			_viewModelController,
			ActiveCamera,
			out _ );
	}

	public bool TryBowReleaseFire( AimboxWeaponRuntime weapon ) => false;

	bool TryGetBowDrawFovTarget( float hipFovTarget, out float targetFov )
	{
		targetFov = hipFovTarget;
		return false;
	}

	public void RegisterCombatHitFeedback( float damage, bool headshot )
	{
		if ( AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default ) )
			return;

		LastDamageDealt = Math.Max( 1, (int)MathF.Round( damage ) );
		_lastHitMarker = 0;
		if ( headshot )
			_lastHeadshotMarker = 0;
	}

	public void TakeDamage( IAimboxCombatActor attacker, AimboxWeaponId weapon, float damage, bool headshot, float distance )
	{
		if ( !IsAlive || !AimboxNetworkCombat.ShouldApplyPlayerDamage )
			return;

		if ( attacker is AimboxBotController bot )
			damage *= bot.DamageMultiplier;

		Health = Math.Max( 0, Health - (int)MathF.Round( damage ) );
		if ( Health > 0 )
			return;

		Die( attacker, weapon, headshot, distance );
	}

	void Die( IAimboxCombatActor attacker, AimboxWeaponId weapon, bool headshot, float distance )
	{
		IsAlive = false;
		_deadTime = 0;
		Health = 0;
		CancelGrenadeEquip();
		LastKillerAccountId = attacker?.CombatId;

		var damageOrigin = attacker?.EyePosition ?? EyePosition + AimForward * -128f;
		ApplyDeathEffects( damageOrigin );

		if ( attacker is not null && attacker != this )
		{
			if ( AimboxNetworkCombat.UseHostAuthority )
				AimboxNetworkCombat.DispatchPlayerKill( attacker, this, weapon, headshot, distance );
			else
			{
				RegisterNetworkDeath();
				attacker.ConfirmKill( this, weapon, headshot, distance );
			}
		}
		else if ( !AimboxNetworkCombat.UseHostAuthority )
		{
			RegisterNetworkDeath();
		}

		if ( !IsProxy && AimboxGame.Instance?.Match.Mode == AimboxGameMode.Survival )
			AimboxGame.Instance.OnSurvivalPlayerEliminated( this );
	}

	public void RegisterNetworkDeath()
	{
		if ( IsProxy || Data is null )
			return;

		Data.Deaths++;
		KillStreak = 0;
		AimboxGame.Instance?.Killstreaks.ClearLifeStreaks( AccountId );
		QueueSaveProgress();
	}

	public void QueueSaveProgress()
	{
		if ( IsProxy || Data is null )
			return;

		AimboxGame.Instance?.QueueSave( Data );
	}

	void SyncProfileToNetwork()
	{
		if ( IsProxy || Data is null )
			return;

		SyncPlayerLevel = Data.PlayerLevel;
		SyncDisplayName = ResolveDisplayName();
	}

	void ApplyDeathEffects( Vector3 damageOrigin )
	{
		if ( _deathPresentationApplied )
			return;

		_deathPresentationApplied = true;

		if ( _collider.IsValid() )
			_collider.Enabled = false;

		EnsureViewModelController();
		_viewModelController?.ResetCombatPresentation();
		SetViewModelVisible( false );
		AimboxCombatRagdoll.SpawnFromCitizenBody( GameObject, damageOrigin );
	}

	void TickSyncedLifeState()
	{
		if ( IsProxy )
			return;

		if ( _wasAlive && !IsAlive )
			ApplyDeathEffects( EyePosition + AimForward * -64f );

		if ( !_wasAlive && IsAlive && AimboxNetworkCombat.UseHostAuthority && !Networking.IsHost )
			ApplyClientRespawnPresentation();

		_wasAlive = IsAlive;
	}

	void TickProxyPresentation()
	{
		var yaw = WorldRotation.Angles().yaw;
		EyeRotation = Rotation.From( new Angles( Math.Clamp( SyncEyePitch, -89f, 89f ), yaw, 0f ) );

		if ( IsAlive )
		{
			_deathPresentationApplied = false;
			if ( _collider.IsValid() )
				_collider.Enabled = true;
			return;
		}

		if ( _deathPresentationApplied )
			return;

		_deathPresentationApplied = true;
		ApplyDeathEffects( EyePosition + AimForward * -64f );
	}

	int Deaths
	{
		get => Data?.Deaths ?? 0;
		set
		{
			if ( Data is not null )
				Data.Deaths = value;
		}
	}

	public void ConfirmKill( IAimboxCombatActor victim, AimboxWeaponId weapon, bool headshot, float distance )
	{
		if ( IsProxy || Data is null )
			return;

		KillStreak++;
		RecentKillCount = _lastKillTime < 4f ? RecentKillCount + 1 : 1;
		_lastKillTime = 0;

		Data.Kills++;
		Data.LongestKillStreak = Math.Max( Data.LongestKillStreak, KillStreak );
		if ( headshot )
			Data.Headshots++;

		var weaponData = Data.GetWeapon( weapon );
		weaponData.Kills++;
		if ( headshot )
			weaponData.Headshots++;

		var rawPlayerXp = AimboxXpSystem.KillXp + (headshot ? AimboxXpSystem.HeadshotXp : 0);
		var rawMasteryXp = AimboxWeaponProgressionSystem.KillMasteryXp + (headshot ? AimboxWeaponProgressionSystem.HeadshotMasteryBonus : 0);
		var playerXp = AimboxXpSystem.ScaleEarnedXp( rawPlayerXp, Data );
		var masteryXp = AimboxXpSystem.ScaleEarnedXp( rawMasteryXp, Data );
		var unlocks = AimboxGame.Instance.Xp.AddPlayerXp( Data, rawPlayerXp );
		unlocks.AddRange( AimboxGame.Instance.WeaponProgression.AddMasteryXp( Data, weapon, rawMasteryXp, AimboxGame.Instance.AttachmentUnlocks ) );

		var completedChallenges = AimboxGame.Instance.Challenges.AddProgress( Data, "kills", 1, AimboxGame.Instance.Xp, unlocks );
		if ( headshot )
			completedChallenges.AddRange( AimboxGame.Instance.Challenges.AddProgress( Data, "headshots", 1, AimboxGame.Instance.Xp, unlocks ) );
		if ( weapon == AimboxWeaponId.Mp5 )
			completedChallenges.AddRange( AimboxGame.Instance.Challenges.AddProgress( Data, "smg_kills", 1, AimboxGame.Instance.Xp, unlocks ) );

		if ( _perkRuntime.RefillAmmoOnKill )
			RefillCurrentWeapon();

		var victimPlayer = victim as AimboxPlayerController;
		var medals = victimPlayer is not null
			? AimboxGame.Instance.Medals.EvaluateKill( this, victimPlayer, headshot, distance )
			: [];
		AimboxGame.Instance.Killstreaks.EvaluateKill( this );
		if ( !AimboxNetworkCombat.UseHostAuthority || victim is AimboxBotController )
			AimboxGame.Instance.Match.RegisterKill( this, victim, weapon, headshot );
		QueueSaveProgress();
		RecordSessionProgression( weapon, playerXp, masteryXp, unlocks );
		foreach ( var challenge in completedChallenges )
			_sessionCompletedChallenges.Add( challenge.Label );
		ShowCombatFeedback( weapon, headshot, playerXp, masteryXp, medals, unlocks );
	}

	public void ConfirmDummyKill( AimboxWeaponId weapon, bool headshot )
	{
		if ( IsProxy || Data is null )
			return;

		Data.PracticeKills++;

		var rawMasteryXp = (AimboxWeaponProgressionSystem.KillMasteryXp + (headshot ? AimboxWeaponProgressionSystem.HeadshotMasteryBonus : 0)) / 2;
		var masteryXp = AimboxXpSystem.ScaleEarnedXp( rawMasteryXp, Data );
		var unlocks = AimboxGame.Instance.WeaponProgression.AddMasteryXp( Data, weapon, rawMasteryXp, AimboxGame.Instance.AttachmentUnlocks );
		RecordSessionProgression( weapon, 0, masteryXp, unlocks );
		ShowCombatFeedback( weapon, headshot, 0, masteryXp, [], unlocks );
		QueueSaveProgress();
	}

	public void ConfirmAimDrillKill( int points )
	{
		if ( IsProxy || Data is null || points <= 0 )
			return;
	}

	public void ConfirmAimDrillHit( int points )
	{
		if ( IsProxy || Data is null || points <= 0 )
			return;
	}

	public void BeginMatch()
	{
		if ( IsProxy || Data is null )
			return;

		_matchStartRank = Data.PlayerLevel;
		_matchStartXp = Data.TotalXp;
		_matchStartKills = Data.Kills;
		_matchStartDeaths = Data.Deaths;
		_matchStartWeaponProgress.Clear();
		_sessionMasteryXp.Clear();
		foreach ( var weaponId in AimboxWeapons.All.Keys )
		{
			var weaponData = Data.GetWeapon( weaponId );
			_matchStartWeaponProgress[weaponId] = (weaponData.Level, weaponData.Xp);
		}
		_sessionUnlocks.Clear();
		_sessionCompletedChallenges.Clear();
		_recentMedals.Clear();
		MedalFeed = "";
		MedalFeedVisibleUntil = 0f;
		RefillGrenades();
	}

	public AimboxMatchSummary BuildMatchSummary( bool won )
	{
		var mode = AimboxGame.Instance?.Match.Mode ?? AimboxGameMode.FreeForAll;

		if ( IsProxy || Data is null )
		{
			return new AimboxMatchSummary
			{
				AccountId = AccountId,
				Mode = mode,
				Won = won
			};
		}

		var masteryEntries = _sessionMasteryXp
			.Where( x => x.Value > 0 )
			.Select( x =>
			{
				var start = _matchStartWeaponProgress.GetValueOrDefault( x.Key );
				var current = Data.GetWeapon( x.Key );
				return new AimboxMatchMasteryXpEntry( x.Key, x.Value, start.Level, current.Level );
			} )
			.OrderByDescending( x => x.XpEarned )
			.ToList();

		var aimScore = 0;
		var aimPersonalBest = 0;
		var isNewAimPersonalBest = false;
		var aimLeaderboardRank = 0;

		if ( !IsProxy && Data is not null && AimboxAimModeRules.IsAimMode( mode ) )
		{
			aimScore = AimboxGame.Instance?.Match.GetAimScore( AccountId ) ?? 0;
			var aimResult = AimboxGame.Instance?.Leaderboards.SubmitAimRun( mode, Data, DisplayName, aimScore )
			                ?? new AimboxAimLeaderboardSubmitResult();
			aimPersonalBest = aimResult.PersonalBest;
			isNewAimPersonalBest = aimResult.IsNewPersonalBest;
			aimLeaderboardRank = aimResult.LeaderboardRank;
			QueueSaveProgress();
		}

		return new AimboxMatchSummary
		{
			AccountId = AccountId,
			Mode = mode,
			Won = won,
			Kills = Data.Kills - _matchStartKills,
			Deaths = Data.Deaths - _matchStartDeaths,
			RankXpEarned = Data.TotalXp - _matchStartXp,
			StartingRank = _matchStartRank,
			EndingRank = Data.PlayerLevel,
			MasteryXpEntries = masteryEntries,
			Unlocks = _sessionUnlocks.ToList(),
			Medals = _recentMedals.ToList(),
			CompletedChallenges = _sessionCompletedChallenges.ToList(),
			AimScore = aimScore,
			AimPersonalBest = aimPersonalBest,
			IsNewAimPersonalBest = isNewAimPersonalBest,
			AimLeaderboardRank = aimLeaderboardRank
		};
	}

	void RecordSessionProgression( AimboxWeaponId weapon, int playerXp, int masteryXp, IEnumerable<AimboxUnlock> unlocks )
	{
		if ( masteryXp > 0 )
			_sessionMasteryXp[weapon] = _sessionMasteryXp.GetValueOrDefault( weapon ) + masteryXp;

		_sessionUnlocks.AddRange( unlocks );
	}

	public void NotifyKillstreakReady( string label )
	{
		KillstreakNotification = $"{label} Ready";
		KillstreakNotificationVisibleUntil = 4f;
	}

	public void NotifyKillstreakUsed( string label )
	{
		KillstreakNotification = label;
		KillstreakNotificationVisibleUntil = 3f;
	}

	public void RefillCurrentWeapon()
	{
		CurrentWeapon?.RefillAmmo();
	}

	public void RefillWeapon( AimboxWeaponId weaponId )
	{
		_inventory.GetById( weaponId )?.RefillAmmo();
		if ( ActiveWeapon == weaponId )
			_lastEquippedWeapon = null;
	}

	public void RefillGrenades()
	{
		if ( IsProxy || Data is null )
			return;

		var loadout = AimboxGame.Instance?.Loadouts.GetActiveLoadout( Data );
		if ( loadout is null )
		{
			LethalGrenadesRemaining = 0;
			TacticalGrenadesRemaining = 0;
			return;
		}

		var lethal = AimboxGrenadeCatalog.ResolveLoadoutGrenade( loadout.LethalGrenade, AimboxWeaponId.HeGrenade );
		var tactical = AimboxGrenadeCatalog.ResolveLoadoutGrenade( loadout.TacticalGrenade, AimboxWeaponId.FlashGrenade );

		LethalGrenadesRemaining = HasGrenadeCharges( lethal )
			? (AimboxGrenadeCatalog.IsUnlimitedChargesMode ? 99 : AimboxGrenadeCatalog.ChargesPerLife)
			: 0;
		TacticalGrenadesRemaining = HasGrenadeCharges( tactical )
			? (AimboxGrenadeCatalog.IsUnlimitedChargesMode ? 99 : AimboxGrenadeCatalog.ChargesPerLife)
			: 0;
	}

	bool HasGrenadeCharges( AimboxWeaponId grenadeId ) =>
		AimboxGrenadeCatalog.IsGrenadeWeapon( grenadeId )
		&& AimboxUnlockService.IsWeaponUnlocked( Data, grenadeId );

	public void ConsumeLethalGrenade()
	{
		if ( AimboxGrenadeCatalog.IsUnlimitedChargesMode )
			return;

		LethalGrenadesRemaining = Math.Max( 0, LethalGrenadesRemaining - 1 );
	}

	public void ConsumeTacticalGrenade()
	{
		if ( AimboxGrenadeCatalog.IsUnlimitedChargesMode )
			return;

		TacticalGrenadesRemaining = Math.Max( 0, TacticalGrenadesRemaining - 1 );
	}

	public void BeginGrenadeThrowCooldown() => _grenadeThrowCooldown = AimboxGrenadeSystem.ThrowCooldownSeconds;

	public void BeginGrenadeEquip( AimboxWeaponId grenadeId, bool isLethal )
	{
		GrenadePresentationWeaponId = grenadeId;
		GrenadeEquipIsLethal = isLethal;
		SetGrenadeEquipPhase( AimboxGrenadeEquipPhase.Equipping );
		_grenadeViewmodelWeaponId = null;
		_grenadePrimaryWasDown = Input.Down( "Attack1" );
		_weaponCombat?.ResetRecoilState();
		_viewModelController?.ResetViewKick();
		AimboxCursor.SyncAfterUi();
	}

	public void SetGrenadeEquipPhase( AimboxGrenadeEquipPhase phase ) => GrenadeEquipPhase = phase;

	public void CancelGrenadeEquip()
	{
		if ( GrenadeEquipPhase == AimboxGrenadeEquipPhase.None )
			return;

		GrenadePresentationWeaponId = null;
		GrenadeEquipIsLethal = false;
		GrenadeEquipPhase = AimboxGrenadeEquipPhase.None;
		_grenadeViewmodelWeaponId = null;
		_grenadePrimaryWasDown = false;
		_lastEquippedWeapon = null;
		_viewModelController?.EndGrenadePresentation();
	}

	public void ReleaseGrenadeThrowPresentation( Action onRelease, Action onComplete ) =>
		_viewModelController?.ReleaseGrenadeThrow( onRelease, onComplete );

	public void ReleaseGrenadeQuickTossPresentation( Action onRelease, Action onComplete ) =>
		_viewModelController?.ReleaseGrenadeQuickToss( onRelease, onComplete );

	public void FinishGrenadeThrow()
	{
		GrenadePresentationWeaponId = null;
		GrenadeEquipIsLethal = false;
		GrenadeEquipPhase = AimboxGrenadeEquipPhase.None;
		_grenadeViewmodelWeaponId = null;
		_grenadePrimaryWasDown = false;
		_lastEquippedWeapon = null;
		_restoreWeaponAfterGrenadeThrow = true;
		_viewModelController?.EndGrenadePresentation();
	}

	public void ApplyFlashBlind( float duration )
	{
		if ( IsProxy || duration <= 0f )
			return;

		_flashBlindDuration = MathF.Max( _flashBlindDuration, duration );
		_flashBlindUntil = _flashBlindDuration;
	}

	/// <summary>True when a world point is inside the local player's view (on screen).</summary>
	public bool IsWorldPointInView( Vector3 worldPoint )
	{
		if ( IsProxy )
			return false;

		var eye = EyePosition;
		var toPoint = worldPoint - eye;
		var dist = toPoint.Length;
		if ( dist <= 2f )
			return true;

		var dir = toPoint / dist;
		if ( Vector3.Dot( AimForward.Normal, dir ) <= 0.01f )
			return false;

		var camera = ActiveCamera;
		if ( camera is null || !camera.IsValid() || !camera.Enabled )
			return IsWithinViewCone( AimForward.Normal, dir, HipFieldOfView );

		var screenNormal = camera.PointToScreenNormal( worldPoint, out var behind );
		if ( behind )
			return false;

		return screenNormal.x is >= 0f and <= 1f && screenNormal.y is >= 0f and <= 1f;
	}

	static bool IsWithinViewCone( Vector3 forward, Vector3 dir, float fieldOfViewDegrees )
	{
		var halfFov = MathF.Max( 1f, fieldOfViewDegrees ) * 0.5f * (MathF.PI / 180f);
		var minDot = MathF.Cos( halfFov );
		return Vector3.Dot( forward.Normal, dir ) >= minDot;
	}

	void TickFlashBlind()
	{
		if ( IsProxy )
			return;

		var remaining = (float)_flashBlindUntil;
		if ( remaining <= 0f )
		{
			FlashBlind01 = 0f;
			_flashBlindDuration = 0f;
			return;
		}

		FlashBlind01 = _flashBlindDuration <= 0f
			? 0f
			: Math.Clamp( remaining / _flashBlindDuration, 0f, 1f );
	}

	void ShowCombatFeedback(
		AimboxWeaponId weapon,
		bool headshot,
		int playerXp,
		int masteryXp,
		IReadOnlyList<AimboxMedalId> medals,
		IReadOnlyList<AimboxUnlock> unlocks )
	{
		foreach ( var medal in medals )
		{
			_recentMedals.Add( medal );
			if ( _recentMedals.Count > 6 )
				_recentMedals.RemoveAt( 0 );
		}

		if ( medals.Count > 0 )
			PushMedalFeed( medals );

		var weaponName = AimboxWeapons.Get( weapon ).Name;
		var xpDetail = playerXp > 0
			? $"+{playerXp} Player XP · +{masteryXp} {weaponName} Mastery XP"
			: $"+{masteryXp} {weaponName} Mastery XP";
		var celebration = AimboxUnlockCelebration.Resolve( unlocks, Data.PlayerLevel, xpDetail );

		if ( celebration is not null )
		{
			PushUnlockCelebration( celebration );
			PushProgressionFeed( celebration.Title, celebration.Detail, true );
		}
		else
		{
			PushProgressionFeed( $"+{playerXp} Player XP", $"+{masteryXp} {weaponName} Mastery XP", false );
		}

		Log.Info( $"Aimbox kill: +{playerXp} Player XP, +{masteryXp} {weaponName} Mastery XP{(headshot ? " (headshot)" : "")}" );
		foreach ( var medal in medals )
			Log.Info( $"Medal: {medal}" );
		foreach ( var unlock in unlocks )
			Log.Info( $"Unlock: {unlock.Label}" );
	}

	void PushProgressionFeed( string primary, string detail, bool levelUp )
	{
		ProgressionFeed = primary;
		ProgressionFeedDetail = detail;
		ProgressionFeedIsLevelUp = levelUp;
		ProgressionFeedVisibleUntil = levelUp ? 4f : 2f;
	}

	void PushMedalFeed( IReadOnlyList<AimboxMedalId> medals )
	{
		MedalFeed = string.Join( " · ", medals.Select( AimboxMedalSystem.FormatLabel ) );
		MedalFeedVisibleUntil = 2.5f;
	}

	void PushUnlockCelebration( AimboxUnlockCelebrationMoment moment )
	{
		UnlockCelebrationKind = moment.Kind;
		UnlockCelebrationKicker = moment.Kicker;
		UnlockCelebrationTitle = moment.Title;
		UnlockCelebrationDetail = moment.Detail;
		UnlockCelebrationExtras = moment.Extras;
		UnlockCelebrationVisibleUntil = moment.Kind switch
		{
			AimboxUnlockCelebrationKind.RankUp => 5.5f,
			AimboxUnlockCelebrationKind.WeaponUnlock => 5f,
			_ => 4.5f
		};

		if ( !IsProxy )
			AimboxGameplaySfx.PlayUnlockCelebration( this, moment.Kind );
	}

	public void ReloadPlayerData( AimboxPlayerData data )
	{
		if ( IsProxy )
			return;

		Data = data;
		ApplyLoadout( AimboxGame.Instance?.Loadouts.GetActiveLoadout( Data ) ?? AimboxLoadoutData.Default() );
		SyncProfileToNetwork();
	}

	/// <summary>Attachment tuning scene — one weapon with exact attachments, no unlock filtering.</summary>
	public void ApplyDebugWeaponLoadout( AimboxWeaponId weapon, IReadOnlyList<AimboxAttachmentId> attachments )
	{
		weapon = AimboxAttachmentCatalog.NormalizeWeapon( weapon );
		var sanitized = AimboxAttachmentCatalog.SanitizeForWeapon( weapon, attachments ?? [] );
		AimboxAttachmentPipelineDebug.Reg(
			$"ApplyDebugWeaponLoadout weapon={weapon} requested=[{AimboxAttachmentPipelineDebug.FormatList( attachments )}] sanitized=[{AimboxAttachmentPipelineDebug.FormatList( sanitized )}] activeWas={ActiveWeapon}" );

		var runtimeBefore = CurrentWeapon;
		var weaponChanged = ActiveWeapon != weapon;
		var attachmentsChanged = runtimeBefore is null
		                         || runtimeBefore.Definition.Id != weapon
		                         || !AttachmentSetsEqual( runtimeBefore.Attachments, sanitized );

		_inventory.ApplySingleWeapon( weapon, sanitized );
		ActiveWeapon = weapon;

		if ( weaponChanged || attachmentsChanged )
			_lastEquippedWeapon = null;

		EnsureViewModelController();
		var runtime = CurrentWeapon;
		if ( runtime is not null )
			_viewModelController?.EnsureWeapon(
				runtime.Definition,
				runtime.Attachments,
				runtime.PerkPresentationSpeedMultiplier );

		AimboxAttachmentPipelineDebug.Reg(
			$"ApplyDebugWeaponLoadout done active={ActiveWeapon} runtimeAttachments=[{AimboxAttachmentPipelineDebug.FormatList( runtime?.Attachments )}] remount={weaponChanged || attachmentsChanged}" );
	}

	/// <summary>Push loadout attachment edits onto an equipped weapon (Create Class while playing).</summary>
	public void TryApplyAttachmentsToWeapon( AimboxWeaponId weaponId, IReadOnlyList<AimboxAttachmentId> attachments )
	{
		var sanitized = AimboxAttachmentCatalog.SanitizeForWeapon( weaponId, attachments ?? [] );
		AimboxAttachmentPipelineDebug.Reg(
			$"TryApplyAttachmentsToWeapon weapon={weaponId} active={ActiveWeapon} requested=[{AimboxAttachmentPipelineDebug.FormatList( attachments )}] sanitized=[{AimboxAttachmentPipelineDebug.FormatList( sanitized )}] gunBuilder={AimboxGame.Instance?.GunBuilderScene == true}" );

		if ( AimboxGame.Instance?.GunBuilderScene == true )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"TryApplyAttachmentsToWeapon ignored — use Aimbox Gun Builder inspector (Preview weapon={AimboxGunBuilder.Instance?.Weapon}, RangedSight={AimboxGunBuilder.Instance?.RangedSight})." );
			return;
		}

		if ( AimboxGame.Instance?.DebugSandboxLoadout == true )
		{
			if ( !_inventory.TryReplaceSlotAttachments( weaponId, sanitized ) )
				return;

			if ( ActiveWeapon != weaponId )
			{
				AimboxAttachmentPipelineDebug.Reg(
					$"TryApplyAttachmentsToWeapon updated sandbox slot for {weaponId} — equip that weapon (keys 1–4) to preview attachments=[{AimboxAttachmentPipelineDebug.FormatList( sanitized )}]" );
				return;
			}
		}
		else if ( ActiveWeapon != weaponId )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"TryApplyAttachmentsToWeapon ignored — {weaponId} is not equipped (active={ActiveWeapon}). Toggle attachments under that weapon's loadout slot, then equip it." );
			return;
		}

		if ( _inventory.Slots.Count == 1 && _inventory.Slots[0].Definition.Id == weaponId )
		{
			ApplyDebugWeaponLoadout( weaponId, sanitized );
			return;
		}

		if ( !_inventory.TryReplaceSlotAttachments( weaponId, sanitized ) )
			return;

		var runtime = CurrentWeapon;
		if ( runtime is null || runtime.Definition.Id != weaponId )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"TryApplyAttachmentsToWeapon failed — active weapon runtime mismatch (runtime={runtime?.Definition.Id})." );
			return;
		}

		if ( AttachmentSetsEqual( _lastEquippedAttachments, runtime.Attachments ) )
		{
			AimboxAttachmentPipelineDebug.Reg( "TryApplyAttachmentsToWeapon — attachments unchanged on viewmodel." );
			return;
		}

		_lastEquippedAttachments = [];
		EnsureViewModelController();
		_viewModelController?.SyncAttachments( weaponId, runtime.Attachments );
		_lastEquippedAttachments = runtime.Attachments.ToList();
		AimboxAttachmentPipelineDebug.Reg(
			$"TryApplyAttachmentsToWeapon synced viewmodel attachments=[{AimboxAttachmentPipelineDebug.FormatList( runtime.Attachments )}]" );
	}

	public void Respawn()
	{
		if ( AimboxNetworkCombat.UseHostAuthority && !Networking.IsHost )
			return;

		ApplyLocalRespawnTransform();
		ApplyHostRespawnState();
	}

	void ApplyClientRespawnPresentation()
	{
		_deathPresentationApplied = false;
		_deadTime = 0f;

		if ( _collider.IsValid() )
			_collider.Enabled = true;

		SyncGameplayCameraPresentation();
		SetViewModelVisible( true );
		_weaponCombat?.ResetRecoilState();
		_viewModelController?.ResetViewKick();
		_viewModelController?.ResetCombatPresentation();
		_lastEquippedWeapon = null;
		SyncCitizenHitbox();
	}

	void ApplyLocalRespawnTransform()
	{
		var game = AimboxGame.Instance;
		var spawn = game.Respawns.SelectSpawn( Scene, this, game.GetAllCombatActors() );
		WorldTransform = spawn;
		_pitch = 0f;
		_velocity = Vector3.Zero;
		_isSliding = false;
		_jumpQueued = false;
		_deadTime = 0f;

		if ( !IsProxy )
		{
			SyncGameplayCameraPresentation();
			SetViewModelVisible( true );
			_weaponCombat?.ResetRecoilState();
			_viewModelController?.ResetViewKick();
			_viewModelController?.ResetCombatPresentation();
			_lastEquippedWeapon = null;
			ApplyLoadout( game?.Loadouts.GetActiveLoadout( Data ) ?? AimboxLoadoutData.Default() );
			RefillGrenades();
		}

		if ( _collider.IsValid() )
			_collider.Enabled = true;

		SyncCitizenHitbox();
		Log.Info( $"[Aimbox] Respawned {GameObject.Name} at {WorldPosition}." );
	}

	void ApplyHostRespawnState()
	{
		Health = 100;
		IsAlive = true;
		_wasAlive = true;
		_deathPresentationApplied = false;
		RefillGrenades();
		CancelGrenadeEquip();
		_flashBlindUntil = 0f;
		_flashBlindDuration = 0f;
		FlashBlind01 = 0f;
	}

	void SetViewModelVisible( bool visible )
	{
		EnsureViewModelController();
		if ( _viewModelController?.ViewModelRoot is { IsValid: true } root )
			root.Enabled = visible;
	}

	public void FinishMatch( bool won )
	{
		if ( IsProxy || Data is null )
			return;

		var unlocks = new List<AimboxUnlock>();
		if ( won )
		{
			Data.Wins++;
			unlocks.AddRange( AimboxGame.Instance.Xp.AddPlayerXp( Data, AimboxXpSystem.WinXp ) );
			_sessionUnlocks.AddRange( unlocks );
			var completed = AimboxGame.Instance.Challenges.AddProgress( Data, "wins", 1, AimboxGame.Instance.Xp, unlocks );
			foreach ( var challenge in completed )
				_sessionCompletedChallenges.Add( challenge.Label );
		}
		else
		{
			Data.Losses++;
		}

		unlocks.AddRange( AimboxGame.Instance.Xp.AddPlayerXp( Data, AimboxXpSystem.MatchCompleteXp ) );
		_sessionUnlocks.AddRange( unlocks );
		Data.MatchesPlayed++;
		QueueSaveProgress();
	}

	public bool IsTeammate( IAimboxCombatActor other )
	{
		if ( other is null )
			return false;

		if ( other is AimboxPlayerController otherPlayer
		     && !string.IsNullOrWhiteSpace( AccountId )
		     && string.Equals( AccountId, otherPlayer.AccountId, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return Team != AimboxTeam.None && Team == other.Team;
	}

	public bool IsTeammate( AimboxPlayerController other ) => IsTeammate( other as IAimboxCombatActor );

	string ResolveAccountId()
	{
		var owner = GameObject.Network.Owner;
		if ( owner is null )
			return "offline";

		if ( owner.SteamId.Value != 0 )
			return owner.SteamId.ToString();

		return owner.Id.ToString();
	}

	string ResolveDisplayName()
	{
		var owner = GameObject.Network.Owner;
		if ( owner is not null && !string.IsNullOrWhiteSpace( owner.DisplayName ) )
			return owner.DisplayName;

		if ( !string.IsNullOrWhiteSpace( SyncDisplayName ) )
			return SyncDisplayName;

		if ( string.IsNullOrWhiteSpace( AccountId ) || AccountId.Equals( "offline", StringComparison.OrdinalIgnoreCase ) )
			return "Player";

		return AccountId.Length > 16 ? AccountId[..16] : AccountId;
	}

	static bool AttachmentSetsEqual(
		IReadOnlyCollection<AimboxAttachmentId> left,
		IReadOnlyCollection<AimboxAttachmentId> right )
	{
		if ( left.Count != right.Count )
			return false;

		var set = new HashSet<AimboxAttachmentId>( right );
		foreach ( var attachment in left )
		{
			if ( !set.Contains( attachment ) )
				return false;
		}

		return true;
	}
}
