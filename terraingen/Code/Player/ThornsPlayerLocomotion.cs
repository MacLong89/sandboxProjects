namespace Terraingen.Player;

using Sandbox;
using Terraingen.Combat;
using Terraingen.UI.Core;

/// <summary>
/// First-person setup for the stock <see cref="PlayerController"/> prefab: native look/move/camera, hidden local body.
/// Viewmodels attach to the prefab camera rig via <see cref="ThornsPlayerFirstPersonRig"/>.
/// </summary>
[Title( "Thorns — Player Locomotion" )]
[Category( "Thorns/Player" )]
[Icon( "directions_walk" )]
[Order( 45 )]
public sealed class ThornsPlayerLocomotion : Component, PlayerController.IEvents, IScenePhysicsEvents
{
	PlayerController _player;
	ThornsAdsSightController _cachedAdsSight;
	GameObject _cachedAdsRig;

	float _configuredRunSpeed;

	float _recoilPitchTarget;
	float _recoilYawTarget;
	float _recoilPitchDisplay;
	float _recoilYawDisplay;
	float _recoilPitchVel;
	float _recoilYawVel;
	float _recoilPitchApplied;
	float _recoilYawApplied;

	[Property] public float WeaponRecoilSpringStiffness { get; set; } = 220f;
	[Property] public float WeaponRecoilSpringDamping { get; set; } = 12f;

	/// <summary>Fraction of each kick applied instantly so recoil is felt on the same frame as the shot (aimbox parity).</summary>
	public const float ImmediateKickFraction = 0.55f;

	public Angles LookAngles => _player.IsValid() ? _player.EyeAngles : Angles.Zero;

	/// <summary>Zero planar movement immediately (overlay open, container use, etc.).</summary>
	public static void StopMovementImmediate( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return;

		var controller = playerRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		controller.WishVelocity = Vector3.Zero;

		var body = controller.Body;
		if ( body.IsValid() )
			body.Velocity = Vector3.Zero;
	}

	/// <summary>Block look/move/camera while a gameplay overlay is open; clears velocity when blocked.</summary>
	public static void SetOverlayInputBlocked( GameObject playerRoot, bool blocked )
	{
		if ( !playerRoot.IsValid() )
			return;

		var controller = playerRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return;

		var allow = !blocked;
		controller.UseInputControls = allow;
		controller.UseLookControls = allow;
		controller.UseCameraControls = allow;

		if ( blocked )
			StopMovementImmediate( playerRoot );
		else if ( allow )
			ThornsPlayerFirstPersonRig.EnsureLocalPresentationCamera( playerRoot );
	}

	/// <summary>Queues view kick (positive pitch = climb). Smoothed with a light spring overshoot each frame in <see cref="IntegrateWeaponRecoil"/>.</summary>
	public void ApplyWeaponRecoilKick( float pitchDegreesUp, float yawDegreesRight )
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		if ( MathF.Abs( pitchDegreesUp ) < 1e-5f && MathF.Abs( yawDegreesRight ) < 1e-5f )
			return;

		_recoilPitchTarget += pitchDegreesUp;
		_recoilYawTarget += yawDegreesRight;

		_recoilPitchDisplay += pitchDegreesUp * ImmediateKickFraction;
		_recoilYawDisplay += yawDegreesRight * ImmediateKickFraction;
	}

	/// <summary>Clears spring state when swapping weapons (aimbox parity).</summary>
	public void ResetWeaponRecoilState()
	{
		_recoilPitchTarget = 0f;
		_recoilYawTarget = 0f;
		_recoilPitchDisplay = 0f;
		_recoilYawDisplay = 0f;
		_recoilPitchVel = 0f;
		_recoilYawVel = 0f;
		_recoilPitchApplied = 0f;
		_recoilYawApplied = 0f;
	}

	protected override void OnAwake()
	{
		_player = Components.GetOrCreate<PlayerController>();
		ConfigurePlayerController();
	}

	public void ConfigurePlayerController()
	{
		if ( !_player.IsValid() )
			return;

		_player.ThirdPerson = false;
		// AUDIT FIX: also honor death so configure after death doesn't re-enable look.
		var allowInput = (!Networking.IsActive || ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ))
		                 && !ThornsUiInputGate.BlocksGameplayInput
		                 && !ThornsPlayerActionGate.IsDeadPawn( GameObject );
		_player.UseInputControls = allowInput;
		_player.UseCameraControls = allowInput;
		_player.UseLookControls = allowInput;
		_player.BodyHeight = ThornsPlayerFirstPersonRig.DefaultBodyHeight;
		_player.BodyRadius = ThornsPlayerFirstPersonRig.DefaultBodyRadius;
		_player.LookSensitivity = ThornsPlayerFirstPersonRig.DefaultLookSensitivity;
		_player.EnableFootstepSounds = false;
		ThornsPlayerMovementDefaults.Apply( _player );
		ThornsPlayerPhysicsStability.Apply( _player );
		_configuredRunSpeed = _player.RunSpeed;
	}

	void IScenePhysicsEvents.PrePhysicsStep() =>
		ThornsPlayerPhysicsStability.StabilizeAgainstObstacles( GameObject, _player );

	void ApplySprintSpeedCap()
	{
		if ( !_player.IsValid() || !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		var allowSprint = gameplay.IsValid() && gameplay.CanSprint;
		_player.RunSpeed = allowSprint ? _configuredRunSpeed : _player.WalkSpeed;
	}

	protected override void OnUpdate()
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		if ( _player.IsValid() && _player.ThirdPerson )
			_player.ThirdPerson = false;

		EnforceOverlayInputBlock();

		if ( !IsOverlayInputBlocked() && !IsDeathInputBlocked() )
		{
			ApplySprintSpeedCap();
			IntegrateWeaponRecoil();
		}

		ApplyAdsLookSensitivity();
		ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( GameObject );
	}

	/// <summary>
	/// True while inventory/container overlays OR death presentation should block look/move.
	/// AUDIT FIX: death was NOT part of <see cref="ThornsUiInputGate.BlocksGameplayInput"/>, so
	/// <see cref="EnforceOverlayInputBlock"/> re-enabled UseLookControls every frame after death
	/// presentation turned them off (controller.Enabled stayed false, but flags fought death cam).
	/// </summary>
	public static bool IsOverlayInputBlocked() => ThornsUiInputGate.BlocksGameplayInput;

	bool IsDeathInputBlocked() => ThornsPlayerActionGate.IsDeadPawn( GameObject );

	void EnforceOverlayInputBlock()
	{
		if ( !_player.IsValid() )
			return;

		// Combined gate: UI overlay OR dead. Do not re-enable look while corpse camera is pinned.
		var blocked = IsOverlayInputBlocked() || IsDeathInputBlocked();

		if ( !blocked )
		{
			if ( ThornsLocalPlayer.IsLocallyControlledPawn( GameObject )
			     && ( !_player.UseInputControls || !_player.UseLookControls || !_player.UseCameraControls ) )
			{
				_player.UseInputControls = true;
				_player.UseLookControls = true;
				_player.UseCameraControls = true;
			}

			return;
		}

		if ( _player.UseInputControls || _player.UseLookControls || _player.UseCameraControls )
		{
			_player.UseInputControls = false;
			_player.UseLookControls = false;
			_player.UseCameraControls = false;
			StopMovementImmediate( GameObject );
		}
	}

	void ApplyAdsLookSensitivity()
	{
		if ( !_player.IsValid() )
			return;

		var sensitivity = ThornsPlayerFirstPersonRig.DefaultLookSensitivity;
		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		if ( rig.IsValid() )
		{
			if ( rig != _cachedAdsRig || _cachedAdsSight is null || !_cachedAdsSight.IsValid() )
			{
				_cachedAdsRig = rig;
				_cachedAdsSight = rig.Components.Get<ThornsAdsSightController>();
			}

			if ( _cachedAdsSight.IsValid() )
				_cachedAdsSight.ApplyLookSensitivityScale( ref sensitivity );
		}
		else
		{
			_cachedAdsRig = null;
			_cachedAdsSight = null;
		}

		_player.LookSensitivity = sensitivity;
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) )
			return;

		if ( !_player.IsValid() || _player.UseInputControls )
		{
			ApplySprintSpeedCap();
			return;
		}

		StopMovementImmediate( GameObject );
	}

	void IntegrateWeaponRecoil()
	{
		if ( !_player.IsValid() )
			return;

		var hasTarget = MathF.Abs( _recoilPitchTarget - _recoilPitchDisplay ) > 1e-4f
		                || MathF.Abs( _recoilYawTarget - _recoilYawDisplay ) > 1e-4f;
		var hasMotion = MathF.Abs( _recoilPitchVel ) > 1e-4f || MathF.Abs( _recoilYawVel ) > 1e-4f;
		if ( !hasTarget && !hasMotion )
			return;

		var dt = Math.Clamp( Time.Delta, 0.001f, 0.05f );
		var k = MathF.Max( 1f, WeaponRecoilSpringStiffness );
		var d = MathF.Max( 0.5f, WeaponRecoilSpringDamping );

		StepRecoilSpring( _recoilPitchTarget, ref _recoilPitchDisplay, ref _recoilPitchVel, k, d, dt );
		StepRecoilSpring( _recoilYawTarget, ref _recoilYawDisplay, ref _recoilYawVel, k, d, dt );

		var pitchDelta = _recoilPitchDisplay - _recoilPitchApplied;
		var yawDelta = _recoilYawDisplay - _recoilYawApplied;
		if ( MathF.Abs( pitchDelta ) < 1e-5f && MathF.Abs( yawDelta ) < 1e-5f )
			return;

		_recoilPitchApplied = _recoilPitchDisplay;
		_recoilYawApplied = _recoilYawDisplay;

		_recoilPitchTarget -= pitchDelta;
		_recoilYawTarget -= yawDelta;

		var angles = _player.EyeAngles;
		angles.pitch = Math.Clamp( angles.pitch - pitchDelta, -89f, 89f );
		angles.yaw += yawDelta;
		_player.EyeAngles = angles;
	}

	static void StepRecoilSpring( float target, ref float display, ref float vel, float stiffness, float damping, float dt )
	{
		var error = target - display;
		vel += (error * stiffness - vel * damping) * dt;
		display += vel * dt;
	}

	/// <summary>Apply ADS FOV after stock <see cref="PlayerController"/> sets hip FOV (avoids per-frame FOV fight).</summary>
	void PlayerController.IEvents.PostCameraSetup( CameraComponent cam )
	{
		if ( !ThornsLocalPlayer.IsLocallyControlledPawn( GameObject ) || cam is null || !cam.IsValid() )
			return;

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );
		if ( !rig.IsValid() || cam.GameObject != rig )
			return;

		var pawnCam = rig.Components.Get<ThornsPawnCamera>();
		if ( pawnCam.IsValid() )
			pawnCam.ApplyPostCameraSetupFov( cam );
	}
}
