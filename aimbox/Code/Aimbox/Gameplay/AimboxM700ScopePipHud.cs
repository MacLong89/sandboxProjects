namespace Sandbox;

using Sandbox.Rendering;

/// <summary>
/// M700 ADS: the scope camera renders a magnified scope view target; UI composites it as a circular
/// overlay aligned to the physical scope lens. The weapon mesh and materials are never touched.
/// </summary>
[Title( "Aimbox M700 Scope Camera HUD" )]
[Category( "Aimbox" )]
public sealed class AimboxM700ScopePipHud : Component
{
	const string ViewmodelExcludeTag = "aimbox_viewmodel";
	const int ScopeViewTargetSize = 512;

	GameObject _scopeCameraObject;
	CameraComponent _scopeCamera;
	Texture _scopeViewTarget;
	bool _loggedRender;

	protected override void OnDestroy()
	{
		AimboxM700ScopePipState.Clear();
		_scopeViewTarget?.Dispose();
		base.OnDestroy();
	}

	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		var player = Components.GetInParent<AimboxPlayerController>();
		var viewModel = Components.Get<AimboxViewModelController>();

		if ( player is null || viewModel is null )
		{
			HideScopePresentation();
			return;
		}

		if ( !player.ShowScopePip )
		{
			HideScopePresentation();
			return;
		}

		if ( !viewModel.TryGetScopePipLensWorld( player.EyeRotation, out var lensWorld ) )
		{
			HideScopePresentation();
			return;
		}

		var mainCamera = Components.Get<CameraComponent>();
		if ( mainCamera is null )
		{
			HideScopePresentation();
			return;
		}

		EnsureScopeCamera();
		SyncScopeCamera( viewModel, player.EyeRotation );

		var viewSetup = default( ViewSetup );
		ScopeCamera.Enabled = true;
		ScopeCamera.RenderToTexture( ScopeViewTarget, in viewSetup );
		ScopeCamera.Enabled = false;

		if ( !_loggedRender && ScopeViewTarget.IsValid() )
		{
			_loggedRender = true;
			Log.Info( "[Aimbox M700 Scope Camera] Scope view target rendering." );
		}

		if ( !TryProjectScopeLens( mainCamera, lensWorld, player.AdsPresentationBlend, player.ActiveWeapon,
			     out var center, out var radius,
			     out var lensProjected, out var screenCenter, out var centerLock01 ) )
		{
			HideScopePresentation();
			return;
		}

		if ( AimboxM700ScopeInvestigationDebug.Enabled )
		{
			AimboxM700ScopeInvestigationDebug.NotifyPipLayout(
				lensProjected,
				center,
				screenCenter,
				centerLock01,
				player.AdsPresentationBlend,
				viewModel.DebugViewKickDegrees,
				viewModel.DebugAttackHold01 );
		}

		radius *= player.ActiveWeapon switch
		{
			AimboxWeaponId.M700 => AimboxAdsSightTuning.M700ScopePipRadiusScale,
			_ => AimboxAdsSightTuning.RangedSightScopePipRadiusScale
		};

		AimboxM700ScopePipState.Publish( new AimboxM700ScopePipFrame
		{
			Active = ScopeViewTarget.IsValid(),
			Center = center,
			Radius = radius,
			ScopeView = ScopeViewTarget
		} );
	}

	CameraComponent ScopeCamera => _scopeCamera;
	Texture ScopeViewTarget => _scopeViewTarget;

	void HideScopePresentation()
	{
		if ( ScopeCamera.IsValid() )
			ScopeCamera.Enabled = false;

		AimboxM700ScopePipState.Clear();
	}

	void EnsureScopeCamera()
	{
		if ( _scopeCameraObject.IsValid() && ScopeCamera.IsValid() && ScopeViewTarget.IsValid() )
			return;

		_scopeCameraObject = new GameObject( true, "M700 Scope Camera" );
		_scopeCameraObject.SetParent( GameObject );
		_scopeCameraObject.NetworkMode = NetworkMode.Never;

		_scopeCamera = _scopeCameraObject.Components.Create<CameraComponent>();
		ScopeCamera.IsMainCamera = false;
		ScopeCamera.Enabled = false;
		ScopeCamera.Priority = -128;
		ScopeCamera.FieldOfView = AimboxAdsSightTuning.SniperScopeViewFov;
		ScopeCamera.ZNear = 0.08f;
		ScopeCamera.ZFar = 10000f;
		ScopeCamera.RenderExcludeTags.Set( ViewmodelExcludeTag, true );

		_scopeViewTarget = Texture.CreateRenderTarget(
			"aimbox_m700_scope_view",
			ImageFormat.RGBA8888,
			new Vector2( ScopeViewTargetSize, ScopeViewTargetSize ) );
	}

	void SyncScopeCamera( AimboxViewModelController viewModel, Rotation eyeRotation )
	{
		if ( !viewModel.TryGetScopePipAnchorWorld( out var scopeEye ) )
			return;

		ScopeCamera.WorldPosition = scopeEye;
		ScopeCamera.WorldRotation = eyeRotation;
		ScopeCamera.FieldOfView = AimboxAdsSightTuning.SniperScopeViewFov;
	}

	static bool TryProjectScopeLens(
		CameraComponent mainCamera,
		Vector3 lensWorld,
		float adsBlend,
		AimboxWeaponId weaponId,
		out Vector2 center,
		out float radius,
		out Vector2 lensProjected,
		out Vector2 screenCenter,
		out float centerLock01 )
	{
		center = default;
		radius = 0f;
		lensProjected = default;
		screenCenter = default;
		centerLock01 = 0f;

		var blend = Math.Clamp( adsBlend, 0f, 1f );
		if ( blend <= 0.001f )
			return false;

		screenCenter = new Vector2( Screen.Width * 0.5f, Screen.Height * 0.5f );

		var projectionHalfExtent = weaponId == AimboxWeaponId.M700
			? AimboxAdsSightTuning.SniperScopePipProjectionHalfExtent
			: AimboxAdsSightTuning.RangedSightScopePipProjectionHalfExtent;
		var halfExtent = projectionHalfExtent * blend;
		var cameraRight = mainCamera.WorldRotation.Right;
		var cameraUp = mainCamera.WorldRotation.Up;
		var pointBox = new BBox(
			lensWorld - cameraRight * halfExtent - cameraUp * halfExtent,
			lensWorld + cameraRight * halfExtent + cameraUp * halfExtent );
		var pixelRect = mainCamera.BBoxToScreenPixels( pointBox, out var onScreen );
		lensProjected = onScreen ? pixelRect.Center : screenCenter;

		// PiP stays on the physical lens (moves with fire kick). Scope-in may ease from screen center first.
		centerLock01 = Math.Clamp(
			(blend - AimboxAdsSightTuning.M700ScopePipVerticalLockStartBlend)
			/ (1f - AimboxAdsSightTuning.M700ScopePipVerticalLockStartBlend),
			0f,
			1f );
		center = blend >= AimboxAdsSightTuning.SniperScopePipBlend - 0.001f
			? lensProjected
			: Vector2.Lerp( screenCenter, lensProjected, centerLock01 );

		if ( onScreen && pixelRect.Width > 2f && pixelRect.Height > 2f )
			radius = MathF.Min( pixelRect.Width, pixelRect.Height ) * 0.5f;
		else
		{
			var radiusFraction = MathX.Lerp(
				AimboxAdsSightTuning.M700ScopePipMinRadiusFraction,
				AimboxAdsSightTuning.M700ScopePipMaxRadiusFraction,
				blend );
			radius = MathF.Min( Screen.Width, Screen.Height ) * radiusFraction * blend;
		}

		return radius > 2f;
	}
}
