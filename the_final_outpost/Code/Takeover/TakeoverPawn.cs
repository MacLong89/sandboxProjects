namespace FinalOutpost;

/// <summary>First-person recruit possession pawn — aimbox look, move, and gunfeel.</summary>
public sealed class TakeoverPawn : Component
{
	public TakeoverWeaponRuntime Weapon { get; private set; }
	public float Health => _unit?.Health ?? 0f;
	public float MaxHealth => _unit?.MaxHealth ?? 1f;
	public int Ammo => Weapon?.Ammo ?? 0;
	public int Reserve => Weapon?.Reserve ?? 0;
	public int MagSize => Weapon?.Definition.MagazineSize ?? 0;
	public bool IsAds => _ads;
	public bool IsAlive => _unit is not null && _unit.IsAlive;
	public CameraComponent Camera => _camera;
	public TakeoverViewModel ViewModel => _viewModel;
	public Vector3 FlatVelocity => _move.Velocity.WithZ( 0f );

	DefenderManager.DefenderUnit _unit;
	CameraComponent _camera;
	TakeoverViewModel _viewModel;
	TakeoverMoveState _move;
	float _pitch;
	float _yaw;
	bool _ads;
	bool _crouch;
	const float HipFieldOfView = 80f;
	/// <summary>Aimbox <c>AimboxAdsSightTuning.IronSightAdsFov</c>.</summary>
	const float IronSightAdsFov = 20f;
	/// <summary>Aimbox 4× iron → ~5° vertical FOV for sniper scope.</summary>
	const float SniperScopeAdsFov = 5.05f;
	const float AdsFovLerpSpeed = 20f;
	const float HideCrosshairBlend = 0.45f;
	float _currentFov = HipFieldOfView;
	TimeUntil _hitFeedbackUntil;
	bool _hitWasHeadshot;

	public bool ShowHitFeedback => _hitFeedbackUntil > 0f;
	public bool HitWasHeadshot => _hitWasHeadshot;
	/// <summary>True once ADS presentation is far enough to hide the hip crosshair (aimbox).</summary>
	public bool HideHipCrosshair =>
		_ads && (_viewModel?.AdsBlend01 ?? 0f) >= HideCrosshairBlend;

	public Vector3 EyePosition =>
		WorldPosition + Vector3.Up * (_crouch ? TakeoverMovement.EyeCrouch : TakeoverMovement.EyeStand);

	public Rotation EyeRotation => Rotation.From( new Angles( _pitch, _yaw, 0f ) );

	public void Possess( DefenderManager.DefenderUnit unit )
	{
		_unit = unit;
		_move.Position = WorldPosition;
		_move.Velocity = Vector3.Zero;
		TakeoverMovement.EscapeIfEmbedded( ref _move );
		WorldPosition = _move.Position;
		_yaw = unit.Aim.Yaw();
		_pitch = 0f;
		WorldRotation = Rotation.FromYaw( _yaw );

		var dmg = DefenderManager.Instance?.DamageOf( unit.Type ) ?? RecruitWeapons.Get( unit.Type ).Damage;
		var range = DefenderManager.Instance?.RangeOf( unit.Type ) ?? RecruitWeapons.Get( unit.Type ).Range;
		var upgrades = GameCore.Instance?.Upgrades;
		if ( upgrades is not null )
			range += upgrades.TurretRangeBonus * 0.5f;

		var def = TakeoverWeaponCatalog.BuildForRecruit( unit.Type, dmg, range );
		Weapon = new TakeoverWeaponRuntime( def );

		EnsureCamera();
		_viewModel?.Destroy();
		_viewModel = new TakeoverViewModel( _camera, def );
		TakeoverSfx.PlayDeploy( this );
	}

	void EnsureCamera()
	{
		var camGo = new GameObject( GameObject, true, "TakeoverCamera" );
		_camera = camGo.Components.Create<CameraComponent>();
		_camera.FieldOfView = HipFieldOfView;
		_camera.ZNear = 0.08f;
		_camera.ZFar = 20000f;
		_camera.IsMainCamera = true;
		_currentFov = HipFieldOfView;

		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
			screen.TargetCamera = _camera;
	}

	public void SyncToUnit( DefenderManager.DefenderUnit unit )
	{
		if ( unit is null || !unit.Go.IsValid() ) return;
		// Keep the invisible body on the FP pawn so zombies path/melee the player.
		unit.Go.WorldPosition = WorldPosition;
		unit.Go.WorldRotation = Rotation.FromYaw( _yaw );
		unit.Aim = EyeRotation;
	}

	/// <summary>Place the recruit back into the world when leaving FP.</summary>
	public void RestoreUnitBody( DefenderManager.DefenderUnit unit )
	{
		if ( unit is null || !unit.Go.IsValid() ) return;
		unit.Go.WorldPosition = WorldPosition;
		unit.Go.WorldRotation = Rotation.FromYaw( _yaw );
		unit.Aim = EyeRotation;
		OutpostHitboxes.SetHierarchyCollisionEnabled( unit.Go, true );
	}

	public void ApplyViewKick( float pitchDeg, float yawDeg )
	{
		// Aimbox recoil: kick "up" subtracts from pitch (Source-style: +pitch looks down).
		_pitch = Math.Clamp( _pitch - pitchDeg, -85f, 85f );
		_yaw += yawDeg;
		_viewModel?.ApplyKick( pitchDeg, yawDeg );
	}

	public void PulseAttackAnim() => _viewModel?.PulseAttack();

	public void RegisterHitFeedback( bool headshot )
	{
		_hitWasHeadshot = headshot;
		_hitFeedbackUntil = 0.18f;
	}

	protected override void OnUpdate()
	{
		if ( _unit is null || !_unit.IsAlive ) return;
		if ( GameCore.Instance?.Phase != GamePhase.Night ) return;

		TickLook();
		TickMove();
		Weapon?.Update( Time.Delta );
		TickCombatInput();
		ApplyCamera();
		_viewModel?.Tick( _ads, Weapon?.IsReloading == true, Input.Down( "Run" ) );
		TakeoverCursor.SyncAfterUi();
	}

	protected override void OnDestroy()
	{
		_viewModel?.Destroy();
		_viewModel = null;
	}

	void TickLook()
	{
		// Mirror AimboxPlayerController.UpdateLookInput — not inverted.
		var lookScale = TakeoverWeaponCatalog.DefaultLookScale * AudioSettings.CameraSensitivity;
		var adsBlend = _viewModel?.AdsBlend01 ?? 0f;
		if ( _ads && Weapon?.Definition.RecruitType == RecruitWeaponType.Sniper && adsBlend >= HideCrosshairBlend )
			lookScale *= TakeoverWeaponCatalog.SniperLookMultiplier;
		else if ( _ads && adsBlend >= HideCrosshairBlend )
			lookScale *= TakeoverWeaponCatalog.IronSightLookMultiplier;

		_yaw -= Input.MouseDelta.x * lookScale;
		_pitch = Math.Clamp( _pitch + Input.MouseDelta.y * lookScale, -85f, 85f );
		WorldRotation = Rotation.FromYaw( _yaw );
	}

	void TickMove()
	{
		_crouch = Input.Down( "Duck" );
		_ads = Input.Down( "Attack2" ) && Weapon?.IsReloading != true;

		var wish = Vector3.Zero;
		var rot = Rotation.FromYaw( _yaw );
		if ( Input.Down( "Forward" ) ) wish += rot.Forward;
		if ( Input.Down( "Backward" ) ) wish -= rot.Forward;
		if ( Input.Down( "Left" ) ) wish += rot.Left;
		if ( Input.Down( "Right" ) ) wish += rot.Right;

		TakeoverMovement.Tick(
			ref _move,
			wish,
			Input.Down( "Run" ) && !_crouch && !_ads,
			_crouch,
			_ads,
			Input.Pressed( "Jump" ),
			Time.Delta );

		WorldPosition = _move.Position;
	}

	void TickCombatInput()
	{
		if ( Weapon is null ) return;

		// Re-lock before fire — LMB click otherwise flips ScreenPanel cursor visible.
		if ( Input.Pressed( "Attack1" ) || Input.Down( "Attack1" ) || Input.Pressed( "Attack2" ) )
			TakeoverCursor.SyncAfterUi();

		if ( Input.Pressed( "Reload" ) && Weapon.TryStartReload() )
			TakeoverSfx.PlayReload( this, Weapon.Definition );

		var wantsFire = Weapon.Definition.RecruitType is RecruitWeaponType.AssaultRifle or RecruitWeaponType.Smg
			? Input.Down( "Attack1" )
			: Input.Pressed( "Attack1" );

		if ( !wantsFire ) return;
		if ( !Weapon.TryConsumeShot( out var startedReload ) ) 
		{
			if ( startedReload )
				TakeoverSfx.PlayReload( this, Weapon.Definition );
			return;
		}

		var moving = FlatVelocity.Length > 55f;
		TakeoverCombat.Fire( this, Weapon, EyeRotation.Forward, _ads, moving, _crouch );
		TakeoverCursor.SyncAfterUi();
	}

	void ApplyCamera()
	{
		if ( _camera is null || !_camera.IsValid() ) return;

		var adsBlend = _viewModel?.AdsBlend01 ?? (_ads ? 1f : 0f);
		var adsFov = Weapon?.Definition.RecruitType == RecruitWeaponType.Sniper
			? SniperScopeAdsFov
			: IronSightAdsFov;
		var targetFov = HipFieldOfView + (adsFov - HipFieldOfView) * adsBlend;
		var t = Math.Clamp( Time.Delta * AdsFovLerpSpeed, 0f, 1f );
		_currentFov += (targetFov - _currentFov) * t;
		_camera.FieldOfView = _currentFov;
		_camera.WorldPosition = EyePosition;
		_camera.WorldRotation = EyeRotation;
		_camera.IsMainCamera = true;
	}
}
