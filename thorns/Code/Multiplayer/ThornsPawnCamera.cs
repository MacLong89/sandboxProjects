namespace Sandbox;

/// <summary>
/// Local-only active camera on a child of the pawn. Remote pawns keep this disabled (document: camera is local-player only).
/// Eye is offset in <see cref="EyeOffsetLocal"/> (default Z-up: along +Z from pawn feet).
/// First-person viewmodels should parent to this component's <see cref="GameObject"/> (same transform as <see cref="CameraComponent"/>), not Scene.Camera.
/// </summary>
[Title( "Thorns — Pawn Camera" )]
[Category( "Thorns" )]
[Icon( "videocam" )]
[Order( 100 )]
public sealed class ThornsPawnCamera : Component
{
	// Inspector: Property codegen supplies descriptions — avoid /// above [Property] (SB2000).
	[Property] public Vector3 EyeOffsetLocal { get; set; } = new Vector3( 0f, 0f, 52f );

	/// <summary>Subtracts from <see cref="EyeOffsetLocal"/>.z (Z‑up pawn) while <see cref="ThornsVitals.ServerCrouching"/> — matches authoritative traces.</summary>
	[Property] public float CrouchEyeDropZ { get; set; } = 30f;

	[Property] public float ZNearPlay { get; set; } = 0.08f;

	/// <summary>Far clip while playing — overridden by <see cref="ThornsTerrainSystem.VisibilityTier"/> when a terrain system is present.</summary>
	[Property] public float ZFarPlay { get; set; } = 72000f;

	[Property] public float HipFieldOfView { get; set; } = 80f;

	[Property] public float AdsFieldOfView { get; set; } = 20f;

	[Property] public float AdsFovLerpSpeed { get; set; } = 10f;

	[Property] public bool SmoothVerticalEyeMotion { get; set; } = true;

	[Property] public float VerticalEyeSmoothRate { get; set; } = 56f;

	[Property] public float VerticalEyeSnapDistance { get; set; } = 14f;

	[Property] public float VerticalEyeMaxLag { get; set; } = 1.8f;

	CameraComponent _camera;
	ThornsPawnMovement _movement;
	ThornsWeapon _weapon;
	ThornsVitals _vitals;
	bool _eyeWorldZPrimed;
	float _eyeWorldZSmoothed;
	float _debugEyeTargetWorldZ;
	float _debugEyeOutputWorldZ;
	float _debugEyeLocalZ;
	bool _debugEyeSnap;

	bool _mainCameraActivated;
	double _nextMainCameraDiscoveryRealtime;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.IsMainCamera = false;
		_camera.Enabled = false;
		_camera.FieldOfView = HipFieldOfView;
		ApplyClipPlanes( ZFarPlay );

		_ = Components.Get<ThornsCelestialSprites>() ?? Components.Create<ThornsCelestialSprites>();

		_movement = GameObject.Parent?.Components.Get<ThornsPawnMovement>();
		_weapon = GameObject.Parent?.Components.Get<ThornsWeapon>();
	}

	protected override void OnStart()
	{
		TryActivateMainCameraForLocalPawn();
	}

	protected override void OnUpdate()
	{
		if ( !_mainCameraActivated )
			TryActivateMainCameraForLocalPawn();

		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return;

		if ( _vitals is null || !_vitals.IsValid() )
			_vitals = parent.Components.Get<ThornsVitals>();
		if ( _movement is null || !_movement.IsValid() )
			_movement = parent.Components.Get<ThornsPawnMovement>();

		var pitch = _movement is not null && _movement.IsValid() ? _movement.LookAngles.pitch : 0f;
		var yaw = _movement is not null && _movement.IsValid() ? _movement.LookAngles.yaw : 0f;

		var eyeLocal = ComposeEyeOffsetLocal( this, _vitals );
		if ( _movement is not null && _movement.IsValid() )
			eyeLocal += _movement.GetViewBobOffsetWithPitch( pitch );

		GameObject.LocalPosition = ResolveSmoothedEyeOffsetLocal( parent, eyeLocal );

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) || !_camera.Enabled )
			return;

		ApplySkyFallbackBackgroundColor();

		if ( _weapon is null || !_weapon.IsValid() )
			_weapon = parent.Components.Get<ThornsWeapon>();
		var hasWeaponEquipped = _weapon is not null && _weapon.IsValid() && !string.IsNullOrWhiteSpace( _weapon.ClientMirrorCombatDefinitionId );
		var combatIdCam = _weapon.ClientMirrorCombatDefinitionId ?? "";
		var meleeEquipped = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( ThornsWeaponDefinitions.Get( combatIdCam ), combatIdCam );
		var attack2Held = Input.Down( "Attack2" ) || Input.Down( "attack2" );
		var fpAllowsAds = !_weapon.IsValid() || _weapon.ClientMirrorFpPresentationAllowsCombatLayers();
		var wantAds = hasWeaponEquipped && !meleeEquipped && attack2Held && fpAllowsAds;
		var targetFov = wantAds ? AdsFieldOfView : HipFieldOfView;
		_camera.FieldOfView = MathX.Lerp( _camera.FieldOfView, targetFov, Math.Clamp( Time.Delta * AdsFovLerpSpeed, 0f, 1f ) );

		var mountIx = parent.Components.Get<ThornsWildlifeMountInteractor>();
		if ( mountIx.IsValid() && mountIx.MountedWildlifeId != Guid.Empty )
		{
			// Pawn root inherits the beast's rotation; put full FPS yaw+pitch on View so we never fight the parent each frame.
			var desiredWorld = Rotation.From( 0f, yaw, 0f ) * Rotation.FromAxis( Vector3.Right, pitch );
			GameObject.LocalRotation = parent.WorldRotation.Inverse * desiredWorld;
		}
		else
			GameObject.LocalRotation = Rotation.FromAxis( Vector3.Right, pitch );
	}

	void ApplySkyFallbackBackgroundColor()
	{
		if ( !_camera.IsValid() )
			return;

		if ( ThornsCelestialSystem.TryGetSkyFallbackColor( Scene, out var fallbackColor ) )
		{
			_camera.BackgroundColor = fallbackColor;
			if ( ( _camera.ClearFlags & ClearFlags.Color ) == 0 )
				_camera.ClearFlags |= ClearFlags.Color;
		}
		else
		{
			_camera.BackgroundColor = Color.Transparent;
		}
	}

	/// <summary>Eye anchor in pawn-root local space (<see cref="ThornsCombatAuthority.TryGetAuthoritativeEye"/>).</summary>
	public static Vector3 ComposeEyeOffsetLocal( ThornsPawnCamera camOrNull, ThornsVitals vitalsOrNull )
	{
		Vector3 eye = camOrNull is { IsValid: true } ? camOrNull.EyeOffsetLocal : new Vector3( 0f, 0f, 52f );
		var drop = camOrNull is { IsValid: true } ? camOrNull.CrouchEyeDropZ : DefaultCrouchEyeDropZFallback;
		if ( vitalsOrNull is { IsValid: true, ServerCrouching: true } )
			eye.z -= drop;
		return eye;
	}

	internal const float DefaultCrouchEyeDropZFallback = 30f;

	/// <summary>Apply near/far clip — called from <see cref="ThornsVisibilityPresets"/> when terrain visibility tier is known.</summary>
	Vector3 ResolveSmoothedEyeOffsetLocal( GameObject parent, Vector3 targetLocal )
	{
		_debugEyeLocalZ = targetLocal.z;
		if ( !SmoothVerticalEyeMotion || !Game.IsPlaying || !ThornsPawn.IsLocalConnectionOwner( this ) )
		{
			_eyeWorldZPrimed = false;
			_debugEyeSnap = true;
			return targetLocal;
		}

		var targetWorld = parent.WorldTransform.PointToWorld( targetLocal );
		_debugEyeTargetWorldZ = targetWorld.z;
		if ( !_eyeWorldZPrimed || MathF.Abs( targetWorld.z - _eyeWorldZSmoothed ) > MathF.Max( 1f, VerticalEyeSnapDistance ) )
		{
			_eyeWorldZSmoothed = targetWorld.z;
			_eyeWorldZPrimed = true;
			_debugEyeOutputWorldZ = _eyeWorldZSmoothed;
			_debugEyeSnap = true;
			return targetLocal;
		}

		var rate = MathF.Max( 0.01f, VerticalEyeSmoothRate );
		var t = 1f - MathF.Exp( -rate * Time.Delta );
		_eyeWorldZSmoothed = MathX.Lerp( _eyeWorldZSmoothed, targetWorld.z, Math.Clamp( t, 0f, 1f ) );
		_eyeWorldZSmoothed = Math.Clamp( _eyeWorldZSmoothed, targetWorld.z - MathF.Max( 0f, VerticalEyeMaxLag ), targetWorld.z + MathF.Max( 0f, VerticalEyeMaxLag ) );
		_debugEyeOutputWorldZ = _eyeWorldZSmoothed;
		_debugEyeSnap = false;

		var adjusted = targetLocal;
		adjusted.z += _eyeWorldZSmoothed - targetWorld.z;
		return adjusted;
	}

	public string GetMovementProbeDebugLine()
	{
		return $"camTargetZ={_debugEyeTargetWorldZ:F2} camOutZ={_debugEyeOutputWorldZ:F2} camLag={_debugEyeOutputWorldZ - _debugEyeTargetWorldZ:F2} camLocalZ={_debugEyeLocalZ:F2} camSnap={_debugEyeSnap}";
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
		if ( _mainCameraActivated || !Game.IsPlaying )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		// Scene.Directory is relatively expensive — avoid hammering it every frame during startup.
		var now = Time.Now;
		if ( now < _nextMainCameraDiscoveryRealtime )
			return;

		_nextMainCameraDiscoveryRealtime = now + 0.05;

		var sceneCameras = Scene.Directory.FindByName( "Camera" );
		if ( sceneCameras is not null )
		{
			foreach ( var go in sceneCameras )
			{
				var other = go.Components.Get<CameraComponent>();
				if ( other.IsValid() && other != _camera )
				{
					other.IsMainCamera = false;
					other.Enabled = false;
				}
			}
		}

		_camera.Enabled = true;
		_camera.IsMainCamera = true;
		_mainCameraActivated = true;

		foreach ( var ts in Scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( ts.IsValid() && ts.Enabled )
			{
				ApplyClipPlanes( ThornsVisibilityPresets.Get( ts.VisibilityTier ).PawnCameraZFarInches );
				break;
			}
		}
	}

}
