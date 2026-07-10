using System;

namespace Sandbox;

/// <summary>
/// Local-owner movement. <see cref="UseTerraingenLocomotion"/> matches terraingen: stock <see cref="PlayerController"/>
/// input + physics on terrain collision (no heightmap Z hacks). Legacy manual wish-velocity mode remains for tuning.
/// </summary>
[Title( "Thorns — Pawn Movement" )]
[Category( "Thorns" )]
[Icon( "directions_walk" )]
[Order( 46 )]
public sealed class ThornsPawnMovement : Component
{
	/// <summary>Terraingen explorer: native PC move / jump / sprint on terrain collision only.</summary>
	[Property] public bool UseTerraingenLocomotion { get; set; } = true;

	[Property] public float WalkSpeed { get; set; } = 320f;
	[Property] public float RunSpeed { get; set; } = 560f;
	/// <summary>0 = do not override <see cref="PlayerController.JumpSpeed"/> (terraingen prefab default).</summary>
	[Property] public float JumpSpeed { get; set; }

	/// <summary>Manual-wish mode only — terraingen leaves this off.</summary>
	[Property] public bool EnableTerrainGroundFollow { get; set; }

	[Property] public bool TerrainGroundFollowOnlyWhenAirborne { get; set; } = true;

	[Property] public bool EnableWaterInteraction { get; set; } = true;
	[Property] public float SwimSpeed { get; set; } = 200f;
	[Property] public float SwimAcceleration { get; set; } = 8f;
	[Property] public float SwimVerticalSpeed { get; set; } = 165f;
	[Property] public float WaterDrag { get; set; } = 2.2f;
	[Property] public float WaterGravityScale { get; set; } = 0.15f;
	[Property] public float WaterGravity { get; set; } = 800f;

	[Property] public float StandingHeight { get; set; } = 72f;
	[Property] public float CollisionRadius { get; set; } = ThornsTerraingenParity.PlayerBodyRadius;

	[Property] public float WeaponRecoilSmoothRate { get; set; } = 20f;

	[Property, Group( "View bob" )] public bool EnableViewBob { get; set; } = true;
	[Property, Group( "View bob" )] public float ViewBobVerticalAmplitude { get; set; } = 1.6f;
	[Property, Group( "View bob" )] public float ViewBobHorizontalAmplitude { get; set; } = 0.8f;
	[Property, Group( "View bob" )] public float ViewBobFrequency { get; set; } = 2.1f;
	[Property, Group( "View bob" )] public float ViewBobMinPlanarSpeed { get; set; } = 20f;
	[Property, Group( "View bob" )] public float ViewBobFullPlanarSpeed { get; set; } = 160f;
	[Property, Group( "View bob" )] public float ViewBobSprintAmplitudeMul { get; set; } = 1.1f;
	[Property, Group( "View bob" )] public float ViewBobCrouchAmplitudeMul { get; set; } = 0.5f;
	[Property, Group( "View bob" )] public float ViewBobAdsAmplitudeMul { get; set; } = 0.3f;
	[Property, Group( "View bob" )] public float ViewBobBlendSpeed { get; set; } = 12f;
	[Property, Group( "View bob" )] public bool ViewBobOnlyWhenGrounded { get; set; } = true;

	PlayerController _player;
	Angles _look;

	float _recoilPitchPending;
	float _recoilYawPending;
	bool _isInWater;
	bool _wasBootBlocked;
	float _viewBobPhase;
	float _viewBobIntensitySmoothed;

	const float TerrainGroundSurfaceOffset = 0.15f;
	const float TerrainGroundMaxFollowDelta = 64f;
	const float TerrainGroundFollowSmoothSpeed = 260f;
	const float TerrainGroundHardSnapDelta = 80f;
	const float TerrainGroundFollowDeadband = 0.04f;
	const float NearGroundPlanarFriction = 9f;

	public PlayerController PlayerController => _player;
	public Angles LookAngles => _player.IsValid() ? _player.EyeAngles : _look;
	public Vector3 Velocity => _player.IsValid() ? _player.Velocity : Vector3.Zero;
	public bool IsInWater => _isInWater;

	public Vector3 ViewBobOffsetLocal { get; private set; }

	public Vector3 GetViewBobOffsetWithPitch( float pitchDegrees ) =>
		Rotation.FromAxis( Vector3.Right, pitchDegrees ) * ViewBobOffsetLocal;

	public void OwnerApplyMomentaryWeaponRecoil( float pitchDegreesKickUp, float yawDegreesRight )
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		_recoilPitchPending += pitchDegreesKickUp;
		_recoilYawPending += yawDegreesRight;
	}

	void IntegrateSmoothWeaponRecoil()
	{
		if ( MathF.Abs( _recoilPitchPending ) < 1e-5f && MathF.Abs( _recoilYawPending ) < 1e-5f )
			return;

		var k = MathF.Max( 0.01f, WeaponRecoilSmoothRate );
		var t = 1f - MathF.Exp( -k * Time.Delta );
		var dPitch = _recoilPitchPending * t;
		var dYaw = _recoilYawPending * t;
		_look.pitch += dPitch;
		_look.yaw += dYaw;
		_recoilPitchPending -= dPitch;
		_recoilYawPending -= dYaw;

		if ( MathF.Abs( _recoilPitchPending ) < 1e-4f )
			_recoilPitchPending = 0f;
		if ( MathF.Abs( _recoilYawPending ) < 1e-4f )
			_recoilYawPending = 0f;
	}

	public void HostApplyRespawnSnap()
	{
		if ( !Networking.IsHost || !_player.IsValid() )
			return;

		ClearPlayerControllerMotion();
	}

	public void HostResetCapsuleAfterMountDismount()
	{
		if ( !_player.IsValid() )
			return;

		if ( !Networking.IsHost && !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		ApplyBodyDimensions();
		ClearPlayerControllerMotion();
		_player.Enabled = true;
		ApplyLocomotionMode();
	}

	public void LocalResetAfterMountDismount() => HostResetCapsuleAfterMountDismount();

	public void StopLocalMovement()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		ClearPlayerControllerMotion();
	}

	void ClearPlayerControllerMotion()
	{
		if ( !_player.IsValid() )
			return;

		_player.WishVelocity = Vector3.Zero;
		if ( _player.Body.IsValid() )
			_player.Body.Velocity = Vector3.Zero;
	}

	protected override void OnAwake()
	{
		RemoveLegacyCharacterController();
		_player = Components.GetOrCreate<PlayerController>();
		ConfigurePlayerController();
	}

	protected override void OnStart()
	{
		TryBindCitizenRenderer();
		ApplyLocomotionMode();
	}

	void RemoveLegacyCharacterController()
	{
		var legacy = Components.Get<CharacterController>();
		if ( legacy.IsValid() )
			legacy.Destroy();
	}

	void ConfigurePlayerController()
	{
		if ( !_player.IsValid() )
			return;

		_player.UseCameraControls = false;
		_player.UseAnimatorControls = false;
		_player.EnablePressing = false;
		_player.RunByDefault = false;
		ApplyBodyDimensions();

		if ( JumpSpeed > 0.01f )
			_player.JumpSpeed = JumpSpeed;
	}

	void ApplyBodyDimensions()
	{
		if ( !_player.IsValid() )
			return;

		_player.BodyHeight = StandingHeight;
		_player.BodyRadius = Math.Clamp( CollisionRadius, 8f, 48f );
	}

	bool LocomotionBlocked()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && health.IsDeadState )
			return true;

		return false;
	}

	void ApplyLocomotionMode()
	{
		if ( !_player.IsValid() )
			return;

		var terraingen = UseTerraingenLocomotion;
		var allowNativeMove = terraingen && !LocomotionBlocked() && !_isInWater;

		_player.UseInputControls = allowNativeMove;
		_player.UseLookControls = false;

		if ( terraingen )
			_player.EnableFootstepSounds = true;
	}

	void SyncTerraingenControllerSpeeds( ThornsVitals vitals )
	{
		var walk = WalkSpeed;
		if ( vitals.IsValid() && vitals.ServerCrouching )
			walk *= 0.5f;

		_player.WalkSpeed = walk;

		var sprintMul = vitals.IsValid() && vitals.ServerSprinting
			? ThornsTerraingenParity.PlayerSprintSpeedMultiplier
			: 1f;
		_player.RunSpeed = walk * sprintMul;

		_player.UpdateDucking( vitals.IsValid() && vitals.ServerCrouching );
	}

	void TryBindCitizenRenderer()
	{
		if ( !_player.IsValid() || _player.Renderer.IsValid() )
			return;

		foreach ( var smr in GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !smr.IsValid() || smr.GameObject.Name != "Body" )
				continue;

			_player.Renderer = smr;
			return;
		}
	}

	protected override void OnUpdate()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		TryBindCitizenRenderer();
		TickViewBob();

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && health.IsDeadState )
			return;

		if ( LocalModalUiBlocksMouseLook() )
			return;

		IntegrateSmoothWeaponRecoil();

		var look = Input.AnalogLook;
		look.pitch = -look.pitch;
		_look += look;
		_look.pitch = Math.Clamp( _look.pitch, -89f, 89f );
		_look.roll = 0f;

		if ( _player.IsValid() )
			_player.EyeAngles = _look;

		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
			return;

		GameObject.WorldRotation = Rotation.From( 0f, _look.yaw, 0f );
	}

	void TickViewBob()
	{
		ViewBobOffsetLocal = Vector3.Zero;

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && health.IsDeadState )
		{
			_viewBobIntensitySmoothed = MathX.Lerp( _viewBobIntensitySmoothed, 0f, Math.Clamp( Time.Delta * ViewBobBlendSpeed, 0f, 1f ) );
			return;
		}

		if ( !EnableViewBob || !_player.IsValid() || _isInWater )
		{
			_viewBobIntensitySmoothed = MathX.Lerp( _viewBobIntensitySmoothed, 0f, Math.Clamp( Time.Delta * ViewBobBlendSpeed, 0f, 1f ) );
			return;
		}

		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
		{
			_viewBobIntensitySmoothed = MathX.Lerp( _viewBobIntensitySmoothed, 0f, Math.Clamp( Time.Delta * ViewBobBlendSpeed, 0f, 1f ) );
			return;
		}

		if ( ViewBobOnlyWhenGrounded && !_player.IsOnGround )
		{
			_viewBobIntensitySmoothed = MathX.Lerp( _viewBobIntensitySmoothed, 0f, Math.Clamp( Time.Delta * ViewBobBlendSpeed, 0f, 1f ) );
			return;
		}

		var planarSpeed = _player.Velocity.WithZ( 0f ).Length;
		var minSpd = MathF.Max( 1f, ViewBobMinPlanarSpeed );
		var fullSpd = MathF.Max( minSpd + 1f, ViewBobFullPlanarSpeed );
		var speed01 = Math.Clamp( (planarSpeed - minSpd) / (fullSpd - minSpd), 0f, 1f );

		var blendT = Math.Clamp( Time.Delta * ViewBobBlendSpeed, 0f, 1f );
		_viewBobIntensitySmoothed = MathX.Lerp( _viewBobIntensitySmoothed, speed01, blendT );

		if ( _viewBobIntensitySmoothed < 0.01f )
			return;

		var vitals = Components.Get<ThornsVitals>();
		var ampMul = 1f;
		if ( vitals.IsValid() && vitals.ServerCrouching )
			ampMul *= ViewBobCrouchAmplitudeMul;
		if ( vitals.IsValid() && vitals.ServerSprinting )
			ampMul *= ViewBobSprintAmplitudeMul;

		var weapon = Components.Get<ThornsWeapon>();
		if ( weapon.IsValid() && !string.IsNullOrWhiteSpace( weapon.ClientMirrorCombatDefinitionId ) )
		{
			var combatId = weapon.ClientMirrorCombatDefinitionId ?? "";
			var melee = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( ThornsWeaponDefinitions.Get( combatId ), combatId );
			var fpAds = weapon.ClientMirrorFpPresentationAllowsCombatLayers();
			var adsHeld = !melee && fpAds && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
			if ( adsHeld )
				ampMul *= ViewBobAdsAmplitudeMul;
		}

		var freq = ViewBobFrequency * MathX.Lerp( 0.75f, 1.08f, _viewBobIntensitySmoothed );
		_viewBobPhase += Time.Delta * freq;

		var twoPi = MathF.PI * 2f;
		var vert = MathF.Sin( _viewBobPhase * twoPi ) * ViewBobVerticalAmplitude * ampMul * _viewBobIntensitySmoothed;
		var horiz = MathF.Cos( _viewBobPhase * twoPi ) * ViewBobHorizontalAmplitude * ampMul * _viewBobIntensitySmoothed;
		ViewBobOffsetLocal = new Vector3( 0f, horiz, vert );
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) || !_player.IsValid() || !_player.Enabled )
			return;

		var bootBlocked = ThornsWorldBootGate.BlocksLocalOwnerPresentation;
		if ( bootBlocked )
		{
			ClearPlayerControllerMotion();
			_wasBootBlocked = true;
			ApplyLocomotionMode();
			return;
		}

		if ( _wasBootBlocked )
		{
			_wasBootBlocked = false;
			ClearPlayerControllerMotion();
			if ( !UseTerraingenLocomotion )
				SnapLocalFeetToTerrain();
		}

		if ( LocomotionBlocked() )
		{
			ClearPlayerControllerMotion();
			ApplyLocomotionMode();
			return;
		}

		var mountIx = Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
			return;

		UpdateLocalWaterState();
		ApplyLocomotionMode();

		if ( EnableWaterInteraction && _isInWater )
		{
			var vitals = Components.Get<ThornsVitals>();
			var speedMul = vitals.IsValid() ? vitals.GetMoveSpeedMultiplier() : 1f;
			TickSwim( speedMul );
			return;
		}

		if ( UseTerraingenLocomotion )
		{
			var vitals = Components.Get<ThornsVitals>();
			SyncTerraingenControllerSpeeds( vitals );
			return;
		}

		if ( EnableTerrainGroundFollow )
			TryApplyPlayerTerrainGroundFollow();

		TickManualLandMovementInput();
		ApplyNearGroundPlanarFrictionWhenIdle();
	}

	void UpdateLocalWaterState()
	{
		var inWater = false;
		if ( EnableWaterInteraction && ThornsTerrainSystem.TryResolveWaterPlaneWorldZ( GameObject.Scene, out var waterPlaneZ ) )
			inWater = GameObject.WorldPosition.z <= waterPlaneZ;
		_isInWater = inWater;
	}

	bool TrySampleLocalTerrainGround( out float terrainZ )
	{
		terrainZ = 0f;
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var pos = GameObject.WorldPosition;
		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld( scene, pos.x, pos.y, 0f, out var groundPos ) )
		{
			terrainZ = groundPos.z;
			return true;
		}

		if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround( scene, pos, 96f, 2048f, out var snapped ) )
		{
			terrainZ = snapped.z;
			return true;
		}

		return false;
	}

	void TryApplyPlayerTerrainGroundFollow()
	{
		if ( TerrainGroundFollowOnlyWhenAirborne && _player.IsOnGround )
			return;

		if ( _player.Velocity.z > 48f )
			return;

		if ( !TrySampleLocalTerrainGround( out var terrainZ ) )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( ThornsTerrainGroundFollow.IsStandingOnNonTerrainSurface( scene, GameObject, terrainZ ) )
			return;

		ThornsTerrainGroundFollow.TryApplyForPlayer(
			GameObject,
			_player,
			terrainZ,
			TerrainGroundSurfaceOffset,
			TerrainGroundMaxFollowDelta,
			TerrainGroundFollowSmoothSpeed,
			TerrainGroundHardSnapDelta,
			TerrainGroundFollowDeadband );
	}

	void SnapLocalFeetToTerrain()
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var pos = GameObject.WorldPosition;
		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld(
			     scene,
			     pos.x,
			     pos.y,
			     ThornsPawnLocomotion.DefaultFeetAboveTerrainSpawn,
			     out var snapped ) )
		{
			GameObject.WorldPosition = snapped;
			return;
		}

		if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround( scene, pos.WithZ( 0f ), 512f, 2048f, out var ground ) )
			GameObject.WorldPosition = ground + Vector3.Up * ThornsPawnLocomotion.DefaultFeetAboveTerrainSpawn;
	}

	void ApplyNearGroundPlanarFrictionWhenIdle()
	{
		if ( _player.WishVelocity.WithZ( 0f ).LengthSquared > 1f )
			return;

		if ( _player.IsOnGround )
			return;

		if ( !TrySampleLocalTerrainGround( out var terrainZ ) )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		if ( ThornsTerrainGroundFollow.IsStandingOnNonTerrainSurface( scene, GameObject, terrainZ ) )
			return;

		var delta = GameObject.WorldPosition.z - terrainZ;
		if ( delta < -2.5f || delta > TerrainGroundMaxFollowDelta )
			return;

		var vel = _player.Velocity;
		var planar = vel.WithZ( 0f );
		var speed = planar.Length;
		if ( speed < 1f )
			return;

		var drop = speed * NearGroundPlanarFriction * Time.Delta;
		var newSpeed = MathF.Max( 0f, speed - drop );
		var newPlanar = newSpeed <= 1e-4f ? Vector3.Zero : planar * ( newSpeed / speed );
		var newVel = new Vector3( newPlanar.x, newPlanar.y, vel.z );

		if ( _player.Body.IsValid() )
			_player.Body.Velocity = newVel;
	}

	void TickManualLandMovementInput()
	{
		if ( !_player.IsValid() || !_player.Enabled )
			return;

		var vitals = Components.Get<ThornsVitals>();
		var speedMul = vitals.IsValid() ? vitals.GetMoveSpeedMultiplier() : 1f;
		_player.UpdateDucking( vitals.IsValid() && vitals.ServerCrouching );

		var input = Input.AnalogMove.WithZ( 0f );
		if ( input.Length > 1f )
			input = input.Normal;

		var wishDir = _look.ToRotation() * input;
		var speed = WalkSpeed * speedMul;
		_player.WishVelocity = wishDir * speed;

		if ( Input.Pressed( "jump" ) && _player.IsOnGround )
		{
			_player.Jump( Vector3.Up * ( JumpSpeed > 0.01f ? JumpSpeed : 400f ) );
			var citizenDriver = Components.Get<ThornsCitizenBodyDriver>();
			if ( citizenDriver.IsValid() )
				citizenDriver.NotifyJumpAnim();
		}
	}

	void TickSwim( float speedMul )
	{
		_player.UseInputControls = false;

		var moveFlat = _look.ToRotation() * Input.AnalogMove.WithZ( 0f );
		if ( moveFlat.Length > 1f )
			moveFlat = moveFlat.Normal;

		var verticalInput = 0f;
		if ( Input.Down( "jump" ) )
			verticalInput += 1f;
		if ( Input.Down( "duck" ) )
			verticalInput -= 1f;

		_player.WishVelocity = new Vector3(
			moveFlat.x * SwimSpeed * speedMul,
			moveFlat.y * SwimSpeed * speedMul,
			verticalInput * SwimVerticalSpeed * speedMul );
	}

	bool LocalModalUiBlocksMouseLook()
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( shell.IsValid() && shell.Enabled && shell.BlocksGameplayShellOverlay )
			return true;

		var hud = Components.Get<ThornsDebugHudHost>();
		if ( hud.IsValid() && (hud.ShowFullInventory || hud.ShowDebugOverlay || hud.ShowRadioShop) )
			return true;

		return false;
	}
}
