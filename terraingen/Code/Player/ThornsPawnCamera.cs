using Terraingen;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.World.Environment;
using Terraingen.Multiplayer;
namespace Sandbox;

/// <summary>
/// ADS FOV + sky tint on the pawn camera. When <see cref="DriveCameraTransform"/> is false (default),
/// the stock <see cref="PlayerController"/> owns look, movement, and camera pose.
/// </summary>
[Title( "Thorns — Pawn Camera" )]
[Category( "Thorns" )]
[Icon( "videocam" )]
[Order( 100 )]
public sealed class ThornsPawnCamera : Component
{
	[Property] public bool DriveCameraTransform { get; set; }

	[Property] public Vector3 EyeOffsetLocal { get; set; } = new Vector3( 0f, 0f, ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ );

	[Property] public float ZNearPlay { get; set; } = 0.08f;

	[Property] public float ZFarPlay { get; set; } = 72000f;

	[Property] public float HipFieldOfView { get; set; } = 80f;

	[Property] public float AdsFieldOfView { get; set; } = 20f;

	[Property] public float AdsFovLerpSpeed { get; set; } = 20f;

	[Property] public bool SmoothVerticalEyeMotion { get; set; }

	[Property] public float VerticalEyeSmoothRate { get; set; } = 56f;

	[Property] public float VerticalEyeSnapDistance { get; set; } = 14f;

	[Property] public float VerticalEyeMaxLag { get; set; } = 1.8f;

	CameraComponent _camera;
	PlayerController _controller;
	ThornsPlayerLocomotion _locomotion;
	ThornsFpPresentation _fp;
	ThornsAdsSightController _adsSight;
	bool _eyeWorldZPrimed;
	float _eyeWorldZSmoothed;

	bool _mainCameraActivated;
	double _nextMainCameraDiscoveryRealtime;
	double _nextForeignCameraGuardRealtime;
	bool _fovSmoothedPrimed;
	float _fovSmoothed;

	protected override void OnAwake()
	{
		_camera = Components.Get<CameraComponent>();
		if ( !_camera.IsValid() )
			_camera = Components.Create<CameraComponent>();

		_camera.FieldOfView = HipFieldOfView;
		ApplyClipPlanes( ZFarPlay );

		_camera.IsMainCamera = false;
		_camera.Enabled = false;

		var parent = GameObject.Parent;
		if ( parent.IsValid() )
		{
			_controller = parent.Components.Get<PlayerController>( FindMode.EverythingInSelf );
			_locomotion = parent.Components.Get<ThornsPlayerLocomotion>();
			_fp = parent.Components.Get<ThornsFpPresentation>();
		}
	}

	protected override void OnStart()
	{
		if ( DriveCameraTransform )
			TryActivateMainCameraForLocalPawn();
		else
			TryActivateStockMainCameraForLocalPawn();
	}

	protected override void OnUpdate()
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
		{
			if ( _camera.IsValid() && ( _camera.Enabled || _camera.IsMainCamera ) )
			{
				_camera.Enabled = false;
				_camera.IsMainCamera = false;
				_mainCameraActivated = false;
			}

			return;
		}

		if ( !DriveCameraTransform && ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			GuardAgainstForeignMainCamera();

		if ( DriveCameraTransform )
		{
			if ( !_mainCameraActivated )
				TryActivateMainCameraForLocalPawn();

			TickCustomCameraTransform();
		}
		else if ( !_mainCameraActivated )
		{
			TryActivateStockMainCameraForLocalPawn();
		}
		else
		{
			EnsureSceneCameraStillBelongsToLocalPawn();
		}

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var renderCamera = ResolveRenderCamera();
		if ( !renderCamera.IsValid() )
			return;

		// Stock PlayerController owns pose + default FOV on the render camera — only touch it from this GO.
		if ( !DriveCameraTransform && renderCamera.GameObject != GameObject )
		{
			ApplySkyFallbackBackgroundColor( renderCamera );
			return;
		}

		ApplySkyFallbackBackgroundColor( renderCamera );

		if ( DriveCameraTransform )
			TickAdsFov( renderCamera );
	}

	/// <summary>Called from <see cref="ThornsPlayerLocomotion"/> via <see cref="PlayerController.IEvents.PostCameraSetup"/>.</summary>
	public void ApplyPostCameraSetupFov( CameraComponent renderCamera )
	{
		if ( DriveCameraTransform || !renderCamera.IsValid() || renderCamera.GameObject != GameObject )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		ApplySmoothedAdsFov( renderCamera, renderCamera.FieldOfView );
	}

	CameraComponent ResolveRenderCamera()
	{
		if ( DriveCameraTransform && _camera.IsValid() && _camera.Enabled )
			return _camera;

		var scene = Scene;
		if ( scene is not null && scene.IsValid && scene.Camera.IsValid() )
			return scene.Camera;

		return _camera.IsValid() && _camera.Enabled ? _camera : default;
	}

	void TickCustomCameraTransform()
	{
		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return;

		if ( _controller is null || !_controller.IsValid() )
			_controller = parent.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( _locomotion is null || !_locomotion.IsValid() )
			_locomotion = parent.Components.Get<ThornsPlayerLocomotion>();

		var eyeAngles = _locomotion is not null && _locomotion.IsValid()
			? _locomotion.LookAngles
			: _controller.IsValid() ? _controller.EyeAngles : Angles.Zero;

		GameObject.LocalPosition = ResolveSmoothedEyeOffsetLocal( parent, EyeOffsetLocal );
		GameObject.LocalRotation = Rotation.FromAxis( Vector3.Right, eyeAngles.pitch );
	}

	void TickAdsFov( CameraComponent renderCamera ) =>
		ApplySmoothedAdsFov( renderCamera, HipFieldOfView );

	void ApplySmoothedAdsFov( CameraComponent renderCamera, float hipFovTarget )
	{
		if ( !renderCamera.IsValid() || !TryGetAdsFovIntent( out var wantAds, out _ ) )
			return;

		if ( _adsSight is null || !_adsSight.IsValid() )
			_adsSight = Components.Get<ThornsAdsSightController>();

		IReadOnlyList<Terraingen.Combat.Attachments.ThornsAttachmentId> attachments;
		if ( TryResolveActiveAttachments( out attachments ) && _adsSight.IsValid() )
		{
			var targetFov = _adsSight.ResolveTargetFieldOfView( hipFovTarget, attachments );
			if ( TryGetBowDrawFovTarget( hipFovTarget, out var bowFov ) )
				targetFov = bowFov;

			ApplySmoothedFov( renderCamera, targetFov );
			return;
		}

		var targetFovLegacy = wantAds ? AdsFieldOfView : hipFovTarget;
		if ( TryGetBowDrawFovTarget( hipFovTarget, out var bowFovLegacy ) )
			targetFovLegacy = bowFovLegacy;

		ApplySmoothedFov( renderCamera, targetFovLegacy );
	}

	void ApplySmoothedFov( CameraComponent renderCamera, float targetFov )
	{
		if ( !renderCamera.IsValid() )
			return;

		if ( !_fovSmoothedPrimed )
		{
			_fovSmoothed = renderCamera.FieldOfView;
			_fovSmoothedPrimed = true;
		}

		var t = Math.Clamp( Time.Delta * AdsFovLerpSpeed, 0f, 1f );
		_fovSmoothed = MathX.Lerp( _fovSmoothed, targetFov, t );
		renderCamera.FieldOfView = _fovSmoothed;
	}

	bool TryResolveActiveAttachments( out IReadOnlyList<Terraingen.Combat.Attachments.ThornsAttachmentId> attachments )
	{
		attachments = Array.Empty<Terraingen.Combat.Attachments.ThornsAttachmentId>();
		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return false;

		var gameplay = parent.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return false;

		var stack = gameplay.GetHotbarSlot( hotbar );
		attachments = Terraingen.Combat.Attachments.ThornsWeaponAttachmentState.GetAttachments( stack );
		return true;
	}

	bool TryGetBowDrawFovTarget( float hipFovTarget, out float targetFov )
	{
		targetFov = hipFovTarget;

		var parent = GameObject.Parent;
		if ( !parent.IsValid() || !ThornsPlayerBowCombat.IsBowEquipped( parent ) )
			return false;

		var bow = parent.Components.Get<ThornsPlayerBowCombat>();
		if ( !bow.IsValid() || bow.ChargeFraction <= 0.0001f )
			return false;

		var zoomMul = Math.Max( 1f, ThornsPlayerBowCombat.DrawFovZoomMultiplier );
		targetFov = MathX.Lerp( hipFovTarget, hipFovTarget / zoomMul, bow.ChargeFraction );
		return true;
	}

	bool TryGetAdsFovIntent( out bool wantAds, out string combatId )
	{
		wantAds = false;
		combatId = "";

		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return false;

		if ( _fp is null || !_fp.IsValid() )
			_fp = parent.Components.Get<ThornsFpPresentation>();

		combatId = _fp.IsValid() ? _fp.ClientMirrorCombatDefinitionId ?? "" : "";
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return true;

		var meleeEquipped = Terraingen.Combat.ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId );
		var attack2Held = Input.Down( "Attack2" ) || Input.Down( "attack2" );
		var fpAllowsAds = !_fp.IsValid() || _fp.ClientMirrorFpPresentationAllowsCombatLayers();
		wantAds = !meleeEquipped && attack2Held && fpAllowsAds;
		return true;
	}

	void ApplySkyFallbackBackgroundColor( CameraComponent renderCamera )
	{
		if ( !renderCamera.IsValid() )
			return;

		if ( ThornsTimeOfDaySystem.TryGet( Scene, out var time ) )
		{
			var state = time.CurrentState;
			renderCamera.BackgroundColor = ThornsSkyController.CameraBackgroundColor( state );
			if ( ( renderCamera.ClearFlags & ClearFlags.Color ) == 0 )
				renderCamera.ClearFlags |= ClearFlags.Color;
		}
		else
		{
			renderCamera.BackgroundColor = Color.Transparent;
		}
	}

	Vector3 ResolveSmoothedEyeOffsetLocal( GameObject parent, Vector3 targetLocal )
	{
		if ( !SmoothVerticalEyeMotion || !Game.IsPlaying || !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
		{
			_eyeWorldZPrimed = false;
			return targetLocal;
		}

		var targetWorld = parent.WorldTransform.PointToWorld( targetLocal );
		if ( !_eyeWorldZPrimed || MathF.Abs( targetWorld.z - _eyeWorldZSmoothed ) > MathF.Max( 1f, VerticalEyeSnapDistance ) )
		{
			_eyeWorldZSmoothed = targetWorld.z;
			_eyeWorldZPrimed = true;
			return targetLocal;
		}

		var rate = MathF.Max( 0.01f, VerticalEyeSmoothRate );
		var t = 1f - MathF.Exp( -rate * Time.Delta );
		_eyeWorldZSmoothed = MathX.Lerp( _eyeWorldZSmoothed, targetWorld.z, Math.Clamp( t, 0f, 1f ) );
		_eyeWorldZSmoothed = Math.Clamp( _eyeWorldZSmoothed, targetWorld.z - MathF.Max( 0f, VerticalEyeMaxLag ), targetWorld.z + MathF.Max( 0f, VerticalEyeMaxLag ) );

		var adjusted = targetLocal;
		adjusted.z += _eyeWorldZSmoothed - targetWorld.z;
		return adjusted;
	}

	public void ApplyClipPlanes( float zFarInches )
	{
		if ( !_camera.IsValid() )
			_camera = Components.GetOrCreate<CameraComponent>();

		_camera.ZNear = ZNearPlay;
		_camera.ZFar = Math.Max( ZNearPlay + 64f, zFarInches );
		ZFarPlay = _camera.ZFar;
	}

	void TryActivateMainCameraForLocalPawn()
	{
		if ( _mainCameraActivated || !Game.IsPlaying || !DriveCameraTransform || ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var now = Time.Now;
		if ( now < _nextMainCameraDiscoveryRealtime )
			return;

		_nextMainCameraDiscoveryRealtime = now + 0.05;

		if ( GameObject.Parent.IsValid() )
		{
			ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( GameObject.Parent );
			ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( Scene, GameObject.Parent );
		}

		_camera.Enabled = true;
		_camera.IsMainCamera = true;
		_mainCameraActivated = true;
		ApplyClipPlanes( ZFarPlay );
	}

	/// <summary>Promote the pawn View camera so stock <see cref="PlayerController"/> look controls apply.</summary>
	void TryActivateStockMainCameraForLocalPawn()
	{
		if ( _mainCameraActivated || !Game.IsPlaying || DriveCameraTransform || ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var now = Time.Now;
		if ( now < _nextMainCameraDiscoveryRealtime )
			return;

		_nextMainCameraDiscoveryRealtime = now + 0.05;

		if ( GameObject.Parent.IsValid() )
		{
			ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( GameObject.Parent );
			ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( Scene, GameObject.Parent );
		}

		_camera.Enabled = true;
		_camera.IsMainCamera = true;
		_mainCameraActivated = true;
		ApplyClipPlanes( ZFarPlay );
	}

	void GuardAgainstForeignMainCamera()
	{
		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return;

		var scene = Scene;
		if ( scene is null || !scene.IsValid )
			return;

		var sceneCam = scene.Camera;
		if ( sceneCam.IsValid() && sceneCam.Enabled && CameraBelongsToPawn( parent, sceneCam.GameObject ) )
		{
			_nextForeignCameraGuardRealtime = 0;
			return;
		}

		var now = Time.Now;
		if ( now < _nextForeignCameraGuardRealtime )
			return;

		_nextForeignCameraGuardRealtime = now + 0.12;
		_mainCameraActivated = false;
		_nextMainCameraDiscoveryRealtime = 0;
		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( scene, parent );
		TryActivateStockMainCameraForLocalPawn();
	}

	void EnsureSceneCameraStillBelongsToLocalPawn()
	{
		if ( DriveCameraTransform || !Game.IsPlaying )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return;

		var sceneCam = Scene?.Camera ?? default;
		if ( sceneCam.IsValid() && sceneCam.Enabled && CameraBelongsToPawn( parent, sceneCam.GameObject ) )
			return;

		_mainCameraActivated = false;
		_nextMainCameraDiscoveryRealtime = 0;
		ThornsSceneObserver.EnsureLocalPawnOwnsMainCamera( Scene, parent );
		TryActivateStockMainCameraForLocalPawn();
	}

	static bool CameraBelongsToPawn( GameObject pawn, GameObject cameraObject )
	{
		if ( !pawn.IsValid() || !cameraObject.IsValid() )
			return false;

		for ( var node = cameraObject; node.IsValid(); node = node.Parent )
		{
			if ( node == pawn )
				return true;
		}

		return false;
	}

	/// <summary>Re-run stock main-camera promotion (e.g. after terrain preview camera or menu input changes).</summary>
	public void EnsureStockMainCameraActive()
	{
		if ( DriveCameraTransform )
			return;

		_mainCameraActivated = false;
		_nextMainCameraDiscoveryRealtime = 0;
		TryActivateStockMainCameraForLocalPawn();
	}

	public static Vector3 ComposeEyeOffsetLocal( ThornsPawnCamera camOrNull ) =>
		camOrNull is { IsValid: true } ? camOrNull.EyeOffsetLocal : new Vector3( 0f, 0f, ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ );

}
