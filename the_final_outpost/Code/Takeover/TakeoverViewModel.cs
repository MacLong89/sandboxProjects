namespace FinalOutpost;

/// <summary>
/// First-person weapon viewmodel under the takeover camera.
/// ADS matches aimbox: anim-graph <c>ironsights</c> + camera-bone grip tracking (not a manual pitch shove).
/// </summary>
public sealed class TakeoverViewModel
{
	public const float FpScale = 10f;
	const float IronsightsBlendPerSecond = 7f;
	const float AdsForwardOffset = 2f;
	const float AdsOffsetLerpSpeed = 32f;
	const float WorldPassAdsBlend = 0.2f;

	readonly GameObject _root;
	readonly SkinnedModelRenderer _renderer;
	readonly List<SkinnedModelRenderer> _overlaySkins = new();
	readonly string _weaponPath;
	readonly bool _isSniper;
	Vector3 _kickOffset;
	Vector3 _adsForwardCurrent;
	float _adsBlend;
	float _attackPulse;
	bool _loggedReady;
	TimeUntil _nextOverlayRetry;

	public GameObject Root => _root;
	public SkinnedModelRenderer Renderer => _renderer;
	/// <summary>0–1 ADS presentation blend (drives FOV / crosshair like aimbox).</summary>
	public float AdsBlend01 => _adsBlend;

	public TakeoverViewModel( CameraComponent camera, TakeoverWeaponDef def )
	{
		_weaponPath = def.ViewModelPath ?? "(null)";
		_isSniper = def.RecruitType == RecruitWeaponType.Sniper;
		_root = new GameObject( camera.GameObject, true, "WeaponViewmodel" );
		_renderer = _root.Components.Create<SkinnedModelRenderer>();
		_renderer.Model = TakeoverWeaponCatalog.LoadModel( def.ViewModelPath );
		_renderer.UseAnimGraph = TakeoverWeaponCatalog.UsesStockFpAnimator( def.ViewModelPath );
		_renderer.CreateBoneObjects = _renderer.UseAnimGraph;
		RegisterOverlaySkin( _renderer );

		_root.LocalScale = Vector3.One * FpScale;
		_root.LocalPosition = Vector3.Zero;
		_root.LocalRotation = Rotation.Identity;

		TryAttachArms();
		SetInt( "firing_mode", IsAuto( def ) ? 3 : 1 );
		SetFloat( "ironsights", 0f );
		SetFloat( "ironsights_fire_scale", 1f );
		SetFloat( "camera_rotation_scale", 0.68f );

		var modelOk = _renderer.Model is not null && _renderer.Model.IsValid() && !_renderer.Model.IsError;
		Log.Info(
			$"[FinalOutpost][TakeoverVM] spawn path={_weaponPath} modelOk={modelOk} " +
			$"animGraph={_renderer.UseAnimGraph} cam={camera?.GameObject?.Name ?? "?"} " +
			$"camMain={camera?.IsMainCamera} zNear={camera?.ZNear}" );
		_nextOverlayRetry = 0f;
	}

	static bool IsAuto( TakeoverWeaponDef def ) =>
		def.RecruitType is RecruitWeaponType.AssaultRifle or RecruitWeaponType.Smg;

	void TryAttachArms()
	{
		var arms = new GameObject( _root, true, "FpArms" );
		var skin = arms.Components.Create<SkinnedModelRenderer>();
		skin.Model = TakeoverWeaponCatalog.LoadModel( TakeoverWeaponCatalog.ArmsPath );
		skin.UseAnimGraph = false;
		skin.BoneMergeTarget = _renderer;
		RegisterOverlaySkin( skin );

		var armsOk = skin.Model is not null && skin.Model.IsValid() && !skin.Model.IsError;
		Log.Info( $"[FinalOutpost][TakeoverVM] arms path={TakeoverWeaponCatalog.ArmsPath} modelOk={armsOk}" );
	}

	void RegisterOverlaySkin( SkinnedModelRenderer skin )
	{
		if ( !skin.IsValid() ) return;

		skin.RenderType = ModelRenderer.ShadowRenderType.Off;
		skin.RenderOptions.Game = true;
		skin.RenderOptions.Overlay = true;
		skin.Enabled = true;
		_overlaySkins.Add( skin );
		PushOverlayPass( skin, overlay: true );
	}

	static void PushOverlayPass( SkinnedModelRenderer skin, bool overlay )
	{
		if ( !skin.IsValid() ) return;

		skin.RenderOptions.Game = true;
		skin.RenderOptions.Overlay = overlay;

		var so = skin.SceneObject;
		if ( !so.IsValid() ) return;

		skin.RenderOptions.Apply( so );
	}

	void EnsureOverlayPass( bool overlay )
	{
		if ( _nextOverlayRetry > 0f && _loggedReady )
		{
			foreach ( var skin in _overlaySkins )
				PushOverlayPass( skin, overlay );
			return;
		}

		var allReady = true;
		foreach ( var skin in _overlaySkins )
		{
			PushOverlayPass( skin, overlay );
			if ( !skin.IsValid() || !skin.SceneObject.IsValid() )
				allReady = false;
		}

		if ( allReady && !_loggedReady )
		{
			_loggedReady = true;
			Log.Info(
				$"[FinalOutpost][TakeoverVM] overlay ready skins={_overlaySkins.Count} " +
				$"weaponEnabled={_renderer.IsValid() && _renderer.Enabled} " +
				$"weaponSo={_renderer.IsValid() && _renderer.SceneObject.IsValid()}" );
		}
		else if ( !allReady )
		{
			_nextOverlayRetry = 0.05f;
		}
	}

	public void Tick( bool ads, bool reloading, bool sprinting )
	{
		// Aimbox: hip = overlay; ADS = world pass so you can see through geometry past the sights.
		var useOverlay = !ads || _adsBlend < WorldPassAdsBlend;
		EnsureOverlayPass( useOverlay );

		var target = ads ? 1f : 0f;
		var step = Time.Delta * IronsightsBlendPerSecond;
		_adsBlend = target > _adsBlend
			? Math.Min( target, _adsBlend + step )
			: Math.Max( target, _adsBlend - step );

		_kickOffset = Vector3.Lerp( _kickOffset, Vector3.Zero, Time.Delta * 8f );
		if ( _attackPulse > 0f )
			_attackPulse = MathF.Max( 0f, _attackPulse - Time.Delta );

		var forwardTarget = ads ? AdsForwardOffset : 0f;
		_adsForwardCurrent = Vector3.Lerp(
			_adsForwardCurrent,
			new Vector3( forwardTarget, 0f, 0f ),
			Math.Clamp( Time.Delta * AdsOffsetLerpSpeed, 0f, 1f ) );

		var grip = _adsForwardCurrent + _kickOffset;

		// Aimbox camera-bone tracking: keep the anim-graph "camera" bone at the eye so ironsights align.
		if ( _renderer.UseAnimGraph && TryGetCameraBoneLocal( out var cameraBoneLocal ) )
			grip -= cameraBoneLocal;

		_root.LocalPosition = grip;
		_root.LocalRotation = Rotation.Identity;
		_root.LocalScale = Vector3.One * FpScale;

		SetFloat( "ironsights", _adsBlend );
		SetFloat( "ironsights_fire_scale", ads ? 0.72f : 1f );
		SetFloat( "camera_rotation_scale", ads ? 0.58f : 0.68f );
		SetBool( "b_reloading", reloading );
		SetBool( "b_reload", reloading );
		SetBool( "b_sprint", sprinting && !ads );
		SetBool( "b_attack", _attackPulse > 0f );
		SetBool( "b_grounded", true );

		// Classic sniper: hide gun mesh once fully scoped (aimbox scope overlay path).
		if ( _isSniper && _adsBlend >= 0.98f )
			SetWeaponVisible( false );
		else
			SetWeaponVisible( true );
	}

	void SetWeaponVisible( bool visible )
	{
		foreach ( var skin in _overlaySkins )
		{
			if ( skin.IsValid() )
				skin.Enabled = visible;
		}
	}

	bool TryGetCameraBoneLocal( out Vector3 localPosition )
	{
		localPosition = Vector3.Zero;
		if ( !_renderer.IsValid() )
			return false;

		var boneObject = _renderer.GetBoneObject( "camera" );
		if ( !boneObject.IsValid()
		     && _renderer.Model.IsValid()
		     && _renderer.Model.Bones.HasBone( "camera" ) )
		{
			boneObject = _renderer.GetBoneObject( _renderer.Model.Bones.GetBone( "camera" ) );
		}

		if ( !boneObject.IsValid() )
			return false;

		localPosition = _renderer.WorldTransform.PointToLocal( boneObject.WorldPosition );
		return float.IsFinite( localPosition.x )
			&& float.IsFinite( localPosition.y )
			&& float.IsFinite( localPosition.z );
	}

	public void PulseAttack()
	{
		_attackPulse = 0.12f;
		SetBool( "b_attack", true );
	}

	public void ApplyKick( float pitchDeg, float yawDeg )
	{
		_kickOffset += new Vector3( -yawDeg * 0.15f, 0f, pitchDeg * 0.08f );
	}

	void SetFloat( string name, float value )
	{
		if ( !_renderer.IsValid() || !_renderer.UseAnimGraph ) return;
		try { _renderer.Set( name, value ); }
		catch { /* parameter may not exist on all graphs */ }
	}

	void SetBool( string name, bool value )
	{
		if ( !_renderer.IsValid() || !_renderer.UseAnimGraph ) return;
		try { _renderer.Set( name, value ); }
		catch { /* parameter may not exist on all graphs */ }
	}

	void SetInt( string name, int value )
	{
		if ( !_renderer.IsValid() || !_renderer.UseAnimGraph ) return;
		try { _renderer.Set( name, value ); }
		catch { /* parameter may not exist on all graphs */ }
	}

	public void Destroy()
	{
		_overlaySkins.Clear();
		_root?.Destroy();
	}
}
