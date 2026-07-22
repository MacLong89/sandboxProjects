namespace Sandbox;

[Title( "Aimbox Bot Controller" )]
[Category( "Aimbox" )]
public sealed class AimboxBotController : Component, IAimboxCombatActor
{
	static readonly AimboxWeaponId[] WeaponPool =
	[
		AimboxWeaponId.M4A1,
		AimboxWeaponId.Mp5,
		AimboxWeaponId.Usp,
		AimboxWeaponId.SpaghelliM4
	];

	[Property] public string BotId { get; set; } = "bot_001";
	[Property] public string Gamertag { get; set; } = "";
	[Property, Sync] public AimboxTeam Team { get; set; }
	// AUDIT FIX C5 (2026-07-13): replicate life state so NetworkSpawn'd bots aren't
	// "always alive" ghosts on joiners. Host still owns writes via FromHost.
	[Property, Sync( SyncFlags.FromHost )] public int Health { get; set; } = 100;
	[Property, Sync( SyncFlags.FromHost )] public bool IsAlive { get; set; } = true;
	[Property, Sync] public AimboxWeaponId ActiveWeapon { get; set; }
	[Property] public float StatMultiplier { get; set; } = 1f;

	public string DisplayName =>
		!string.IsNullOrWhiteSpace( Gamertag )
			? Gamertag
			: AimboxBotGamertags.ForSlot( ParseSlotIndex() );
	public float DamageMultiplier => StatMultiplier;
	public int MaxHealth => (int)MathF.Round( 100f * StatMultiplier );

	GameObject IAimboxCombatActor.GameObject => GameObject;
	Scene IAimboxCombatActor.Scene => Scene;
	string IAimboxCombatActor.CombatId => BotId;
	bool IAimboxCombatActor.IsHumanPlayer => false;
	bool IAimboxCombatActor.ShowThirdPersonBody => IsAlive;

	public Vector3 AimForward => EyeRotation.Forward;
	public Rotation EyeRotation { get; private set; } = Rotation.Identity;
	public Vector3 EyePosition => WorldPosition + Vector3.Up * (_crouching ? 42f : 64f);
	public AimboxWeaponRuntime CurrentWeapon => _inventory.GetById( ActiveWeapon );
	public bool IsCrouching => _crouching;
	public bool IsSprintMoving => _sprinting && _velocity.WithZ( 0f ).LengthSquared > 400f;
	public bool WantsAds { get; set; }
	public bool WantsFire { get; set; }

	readonly AimboxWeaponInventory _inventory = new();
	readonly AimboxBotBrain _brain = new();
	AimboxWeaponCombatComponent _weaponCombat;
	Vector3 _velocity;
	float _pitch;
	bool _crouching;
	bool _sprinting;
	float _speedMultiplier = 1f;
	Vector3 _wishDirection;
	Vector3 _wishDirectionTarget;
	Vector3 _wishDirectionSmoothed;
	TimeSince _deadTime;
	TimeSince _movementNoiseEmit;
	CapsuleCollider _collider;

	public void ApplyWaveScaling( bool hardMode )
	{
		StatMultiplier = hardMode ? AimboxArenaConfig.SurvivalHardStatMultiplier : 1f;
	}

	public void TakeDamage( IAimboxCombatActor attacker, AimboxWeaponId weapon, float damage, bool headshot, float distance )
	{
		if ( !IsAlive )
			return;

		Health = Math.Max( 0, Health - (int)MathF.Round( damage ) );
		if ( Health > 0 )
			return;

		Die( attacker, weapon, headshot, distance );
	}

	public void ConfirmKill( IAimboxCombatActor victim, AimboxWeaponId weapon, bool headshot, float distance )
	{
		AimboxGame.Instance?.Match.RegisterKill( this, victim, weapon, headshot );
	}

	public void RegisterCombatHitFeedback( float damage, bool headshot )
	{
	}

	public bool IsTeammate( IAimboxCombatActor other ) =>
		other is not null && Team != AimboxTeam.None && Team == other.Team;

	public Vector3 GetMovementVelocity() => _velocity;

	public float GetCombatPitch() => _pitch;

	public void SetCombatPitch( float pitch ) => _pitch = Math.Clamp( pitch, -85f, 85f );

	protected override void OnStart()
	{
		if ( string.IsNullOrWhiteSpace( Gamertag ) )
			Gamertag = AimboxBotGamertags.ForSlot( ParseSlotIndex() );

		AimboxCitizenPresentation.EnsureCitizenBody( this );
		_weaponCombat = Components.GetOrCreate<AimboxWeaponCombatComponent>();
		_collider = Components.Get<CapsuleCollider>();
		RollRandomWeapon();
		AimboxGame.Instance?.RegisterBot( this );
		_brain.OnSpawned( this );
		Log.Info( $"[Aimbox] Bot '{DisplayName}' ready with {ActiveWeapon} on team {Team}." );
	}

	protected override void OnDestroy()
	{
		AimboxGame.Instance?.UnregisterBot( this );
	}

	protected override void OnUpdate()
	{
		// AUDIT FIX C5: bots are host-authoritative. Joiners receive Sync'd life/team
		// for presentation, but must NOT run AI or local damage paths.
		if ( Networking.IsActive && !Networking.IsHost )
		{
			SyncEyeRotation();
			return;
		}

		if ( AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing )
			return;

		if ( AimboxGame.Instance.IsCombatLocked )
		{
			WantsFire = false;
			// Clear residual wish so FixedUpdate cannot keep skating after freeze / intermission.
			ClearMovementWish();
			if ( IsAlive )
				SyncEyeRotation();
			return;
		}

		if ( !IsAlive )
		{
			ClearMovementWish();
			if ( AimboxGame.Instance.Match.Mode == AimboxGameMode.Duel )
				return;

			if ( _deadTime >= AimboxCombatRagdoll.LifetimeSeconds )
				Respawn();
			return;
		}

		CurrentWeapon?.Update( Time.Delta );
		_brain.Tick( this );
		if ( _brain.State is AimboxBotState.Engage or AimboxBotState.Reload )
			IntegrateWeaponRecoil();
		else if ( MathF.Abs( _pitch ) > 0.01f )
			_pitch = 0f;

		SyncEyeRotation();
	}

	protected override void OnFixedUpdate()
	{
		// AUDIT FIX H8 (2026-07-13): FixedUpdate used to ignore Phase — leftover wish
		// from the last Playing frame could slide bots during Intermission/Starting.
		// Keep this gate in lockstep with OnUpdate. Revert both together if locomotion
		// feels stuck at round boundaries.
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing )
			return;

		if ( !IsAlive || AimboxGame.Instance?.IsCombatLocked == true )
			return;

		TickMovement();
	}

	/// <summary>Zeros wish vectors so FixedUpdate cannot integrate stale brain intent.</summary>
	void ClearMovementWish()
	{
		_wishDirection = Vector3.Zero;
		_wishDirectionTarget = Vector3.Zero;
		_wishDirectionSmoothed = Vector3.Zero;
		_sprinting = false;
		WantsFire = false;
	}

	public void RollRandomWeapon()
	{
		_inventory.ApplySingleWeapon( WeaponPool[Game.Random.Int( 0, WeaponPool.Length - 1 )] );
		ActiveWeapon = _inventory.Slots[0].Definition.Id;
	}

	public void SetMovement( Vector3 wishDirection, bool sprint, bool crouch, float speedMultiplier = 1f )
	{
		_wishDirectionTarget = wishDirection.WithZ( 0 );
		_sprinting = sprint && !crouch;
		_crouching = crouch;
		_speedMultiplier = MathF.Max( 0.1f, speedMultiplier );
	}

	public void SetLookAngles( float pitch, float yaw )
	{
		_pitch = Math.Clamp( pitch, -85f, 85f );
		WorldRotation = Rotation.FromYaw( yaw );
	}

	public void SetLookScan( float yaw )
	{
		WorldRotation = Rotation.FromYaw( yaw );
	}

	public void ResetLocomotionPresentation()
	{
		_pitch = 0f;
		_weaponCombat?.ResetRecoilState();
	}

	public void StartReload()
	{
		CurrentWeapon?.StartReload();
		if ( CurrentWeapon?.ReloadStartedThisTick == true )
			AimboxGameplaySfx.PlayReload( this, CurrentWeapon.Definition );
	}

	public bool TryFireWeapon()
	{
		if ( _weaponCombat is null || CurrentWeapon is null )
			return false;

		var moving = _velocity.WithZ( 0f ).Length > 55f;
		return _weaponCombat.TryFire(
			CurrentWeapon,
			WantsAds,
			moving,
			_crouching,
			false,
			null,
			null,
			out _ );
	}

	void TickMovement()
	{
		var dt = Time.Delta;
		var wishBlend = Math.Clamp( dt * AimboxBotTuning.WishDirectionSmoothSpeed, 0f, 1f );
		if ( _wishDirectionTarget.Length > 0.01f )
			_wishDirectionSmoothed = Vector3.Lerp( _wishDirectionSmoothed, _wishDirectionTarget.Normal, wishBlend );
		else
			_wishDirectionSmoothed = Vector3.Lerp( _wishDirectionSmoothed, Vector3.Zero, wishBlend );

		_wishDirection = _wishDirectionSmoothed.Length > 0.01f ? _wishDirectionSmoothed.Normal : Vector3.Zero;

		var state = new AimboxCitizenMovementState
		{
			Velocity = _velocity,
			Position = WorldPosition
		};

		AimboxCitizenMovementMotor.Tick(
			Scene,
			GameObject,
			ref state,
			new AimboxCitizenMovementInput
			{
				WishDirection = _wishDirection,
				Sprint = _sprinting,
				Crouch = _crouching,
				AdsSlowdown = WantsAds,
				SpeedMultiplier = _speedMultiplier
			},
			WorldRotation,
			Time.Delta );

		_velocity = state.Velocity;
		WorldPosition = state.Position;
		SyncCitizenHitbox();
		TickMovementNoise();
	}

	void SyncCitizenHitbox()
	{
		if ( _collider.IsValid() && _collider.Enabled )
			AimboxHitboxes.ApplyCitizenHitbox( _collider, _crouching );
	}

	void TickMovementNoise()
	{
		if ( !AimboxCitizenMovementMotor.IsGrounded( Scene, GameObject, WorldPosition ) )
			return;

		var speed = _velocity.WithZ( 0f ).Length;
		if ( speed < 80f || _movementNoiseEmit < AimboxBotTuning.MovementNoiseEmitInterval )
			return;

		_movementNoiseEmit = 0;
		var loudness = _sprinting
			? AimboxBotTuning.HearingRadiusSprintFootsteps
			: AimboxBotTuning.HearingRadiusWalkFootsteps;
		if ( _crouching )
			loudness *= 0.6f;

		AimboxCombatNoiseBus.EmitMovement( this, loudness );
		AimboxGameplaySfx.PlayFootstep( this, _sprinting, _crouching );
	}

	void IntegrateWeaponRecoil()
	{
		if ( _weaponCombat is null )
			return;

		var yaw = WorldRotation;
		_weaponCombat.IntegrateRecoil( ref _pitch, ref yaw );
		WorldRotation = yaw;
	}

	void SyncEyeRotation()
	{
		var yaw = WorldRotation.Angles().yaw;
		EyeRotation = Rotation.From( new Angles( Math.Clamp( _pitch, -89f, 89f ), yaw, 0f ) );
	}

	void Die( IAimboxCombatActor attacker, AimboxWeaponId weapon, bool headshot, float distance )
	{
		IsAlive = false;
		_deadTime = 0;
		Health = 0;
		WantsFire = false;
		if ( _collider.IsValid() )
			_collider.Enabled = false;
		_brain.Perception.ClearTarget();

		HidePresentation();
		var damageOrigin = attacker?.EyePosition ?? EyePosition + AimForward * -128f;
		AimboxCombatRagdoll.SpawnFromCitizenBody( GameObject, damageOrigin );

		if ( attacker is null || attacker == this )
			return;

		// AUDIT FIX: MP bot victims previously only called ConfirmKill on the host pawn — joiners
		// are IsProxy there so XP/killfeed never landed. Mirror the player-kill broadcast path.
		if ( AimboxNetworkCombat.UseHostAuthority )
		{
			if ( Networking.IsHost )
				AimboxGame.Instance?.RpcBroadcastBotVictimKill( attacker.CombatId, BotId, weapon, headshot, distance );
			return;
		}

		attacker.ConfirmKill( this, weapon, headshot, distance );
	}

	public void Respawn()
	{
		var game = AimboxGame.Instance;
		if ( game is null )
			return;

		var spawn = game.Respawns.SelectSpawn( Scene, this, game.GetAllCombatActors() );
		WorldTransform = spawn;
		Health = MaxHealth;
		IsAlive = true;
		_velocity = Vector3.Zero;
		_wishDirection = Vector3.Zero;
		_wishDirectionTarget = Vector3.Zero;
		_wishDirectionSmoothed = Vector3.Zero;
		_speedMultiplier = 1f;
		_crouching = false;
		if ( _collider.IsValid() )
			_collider.Enabled = true;
		SyncCitizenHitbox();
		RollRandomWeapon();
		_brain.OnSpawned( this );
		ShowPresentation();
	}

	void HidePresentation()
	{
		SetPresentationVisible( false );
	}

	void ShowPresentation()
	{
		SetPresentationVisible( true );
	}

	void SetPresentationVisible( bool visible )
	{
		var body = AimboxCitizenPresentation.FindChild( GameObject, AimboxCitizenPresentation.BodyChildName );
		if ( body.IsValid() )
		{
			foreach ( var renderer in body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( renderer.IsValid() )
					renderer.Enabled = visible;
			}
		}

		var weapon = AimboxCitizenPresentation.FindChild( GameObject, AimboxCitizenPresentation.WorldWeaponChildName );
		if ( weapon.IsValid() )
		{
			foreach ( var renderer in weapon.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( renderer.IsValid() )
					renderer.Enabled = visible;
			}
		}
	}

	int ParseSlotIndex()
	{
		if ( BotId.StartsWith( "bot_", StringComparison.OrdinalIgnoreCase )
			&& int.TryParse( BotId.AsSpan( 4 ), out var slot ) )
			return Math.Max( 1, slot );

		return 1;
	}
}
