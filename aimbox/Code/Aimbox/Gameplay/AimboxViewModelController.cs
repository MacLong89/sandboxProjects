namespace Sandbox;

/// <summary>
/// First-person weapon viewmodel on the player camera — matches terraingen's local grip + anim-graph ADS setup.
/// </summary>
[Title( "Aimbox Viewmodel Controller" )]
[Category( "Aimbox" )]
public sealed class AimboxViewModelController : Component
{
	public const float FpWeaponMeshRootScaleMul = 10f;
	public const string WeaponViewmodelChildName = "WeaponViewmodel";

	[Property] public Vector3 ViewModelGripLocalPosition { get; set; }
	[Property] public float ViewModelAdsForwardOffset { get; set; }
	[Property] public float ViewModelAdsOffsetLerpSpeed { get; set; } = 32f;
	[Property] public bool ViewModelUseOverlayPass { get; set; } = true;
	[Property] public bool UseFirstPersonArmsHuman { get; set; } = true;

	/// <summary>Slide the viewmodel so the anim-graph camera bone stays at the view origin — hands/gun follow skeleton.</summary>
	[Property] public bool UseCameraBoneGripTracking { get; set; } = true;

	GameObject _viewmodel;
	string _activeModelPath = "";
	bool _fpBowUsesStockPlaceholderMesh;
	bool _viewModelVisuallyHidden;
	Vector3 _adsOffsetCurrent;
	float _sightAdsForwardOffset;
	float _sightAdsForwardBlend;
	Vector3 _sightEyeViewmodelOffset;
	AimboxM700ScopeSightSnapshot? _lastM700ScopeSnapshot;
	float _viewKickPitch;
	float _viewKickYaw;
	float _viewKickRoll;
	AimboxPlayerController _ownerPlayer;
	AimboxWeaponId? _grenadePresentationWeaponId;

	public void BindOwner( AimboxPlayerController owner ) => _ownerPlayer = owner;

	public bool HasActiveViewModel => _viewmodel.IsValid();
	public GameObject ViewModelRoot => _viewmodel;
	public SkinnedModelRenderer WeaponSkin { get; private set; }
	public AimboxViewModelFpAnimator Animator { get; private set; }
	public AimboxViewModelAttachmentMount AttachmentMount { get; private set; }

	public float AdsBlend01 => Animator?.IronsightsBlend01 ?? 0f;
	public bool PresentationAllowsScopePip => Animator?.PresentationAllowsScopePip ?? false;
	public Vector3 DebugSightEyeViewmodelOffset => _sightEyeViewmodelOffset;
	public AimboxM700ScopeSightSnapshot? LastM700ScopeSnapshot => _lastM700ScopeSnapshot;
	public string ActiveModelPath => _activeModelPath;
	public Vector3 DebugViewModelLocalPosition => _viewmodel.IsValid() ? _viewmodel.LocalPosition : Vector3.Zero;
	public Vector3 DebugAdsOffsetCurrent => _adsOffsetCurrent;
	public float DebugViewModelScale => _viewmodel.IsValid() ? _viewmodel.WorldScale.x : 0f;
	public bool DebugViewModelEnabled => _viewmodel.IsValid() && WeaponSkin is { Enabled: true };
	public bool DebugWeaponSkinEnabled => WeaponSkin is { Enabled: true };
	public Vector3 DebugViewKickDegrees => new( _viewKickPitch, _viewKickYaw, _viewKickRoll );
	public float DebugAttackHold01 => Animator?.DebugAttackHold01 ?? 0f;
	public bool IsGrenadePresentationActive => _grenadePresentationWeaponId.HasValue;
	public AimboxWeaponId? GrenadePresentationWeaponId => _grenadePresentationWeaponId;

	public void ApplySightPresentation( AimboxAdsSightMode mode, float adsBlend, bool classicScopeVisualActive = false )
	{
		if ( !_viewmodel.IsValid() )
			return;

		SetViewModelRenderersEnabled( true );
		AttachmentMount?.SetRedDotStyleMeshesVisible( true );
		SetTunedRedDotMeshesVisible( true );

		// Overlay pass draws on top of the world (you can't see through geometry).
		// During ADS, switch to the world pass and clear opaque optic lens materials so you see the scene.
		var overlayPass = mode == AimboxAdsSightMode.None || adsBlend < AimboxAdsSightTuning.WorldPassAdsBlend;
		SetViewModelOverlayPass( overlayPass );

		var clearOpticLens = adsBlend >= AimboxAdsSightTuning.WorldPassAdsBlend;

		// Apply after render-pass change — holo lens overrides must win over overlay-pass refresh.
		if ( AttachmentMount?.HasRedDotStyleAttachment() == true )
		{
			AttachmentMount.ApplyRedDotLensPresentation( clearOpticLens );
			ApplyTunedRedDotLensPresentation( clearOpticLens );
		}

		// M700 scope PiP is composited in UI only — never override scope mesh materials.
		UpdateM700IntegratedScopePresentation();

		_sightAdsForwardOffset = ResolveSightAdsForwardOffset( mode, adsBlend );
		_sightAdsForwardBlend = adsBlend;
		ComputeSightEyeViewmodelOffset( mode, adsBlend );
		SetViewModelVisuallyHidden( classicScopeVisualActive );
	}

	/// <summary>
	/// Parented viewmodel: eye alignment is done by sliding the gun on screen, not moving the camera
	/// (moving the camera would drag the child viewmodel and cancel the effect).
	/// </summary>
	void ComputeSightEyeViewmodelOffset( AimboxAdsSightMode mode, float adsBlend )
	{
		_sightEyeViewmodelOffset = Vector3.Zero;
		if ( !_viewmodel.IsValid() || !WeaponSkin.IsValid() || adsBlend <= 0.001f || mode == AimboxAdsSightMode.None )
			return;

		switch ( mode )
		{
			case AimboxAdsSightMode.RedDot:
				ComputeRedDotViewmodelOffset( adsBlend );
				break;
			case AimboxAdsSightMode.SniperScope:
				ComputeSniperViewmodelOffset( adsBlend );
				break;
			default:
				_lastM700ScopeSnapshot = null;
				ComputeIronSightViewmodelOffset( adsBlend );
				break;
		}

		if ( mode != AimboxAdsSightMode.SniperScope )
			_lastM700ScopeSnapshot = null;

		SanitizeSightEyeViewmodelOffset();
	}

	/// <summary>Slide viewmodel so the optic lens plane meets the iron-sight camera bone (screen center).</summary>
	void ComputeRedDotViewmodelOffset( float adsBlend )
	{
		_sightEyeViewmodelOffset = ComputeLensViewmodelOffset( AimboxAdsSightMode.RedDot, adsBlend, AimboxAdsSightTuning.RedDotCameraOffset );
		_sightEyeViewmodelOffset += ResolveRedDotFineTune( adsBlend );
	}

	void ComputeSniperViewmodelOffset( float adsBlend )
	{
		var bonePos = Vector3.Zero;
		var hasBone = TryGetCameraBoneLocal( out bonePos, out _ );
		var hasLens = AimboxViewModelSightResolve.TryGetOpticLensSkinLocal(
			AimboxAdsSightMode.SniperScope, WeaponSkin, AttachmentMount, out var lensSkinLocal );

		_sightEyeViewmodelOffset = ComputeLensViewmodelOffset(
			AimboxAdsSightMode.SniperScope, adsBlend, AimboxAdsSightTuning.SniperScopeCameraOffset );
		_sightEyeViewmodelOffset += ResolveSniperScopeFineTune( adsBlend );

		var alignmentDelta = hasLens && hasBone
			? (lensSkinLocal - bonePos) * adsBlend
			: Vector3.Zero;
		var eyeLocalOffset = alignmentDelta + ResolveSniperScopeFineTune( adsBlend );

		_lastM700ScopeSnapshot = new AimboxM700ScopeSightSnapshot(
			hasBone,
			bonePos,
			hasLens,
			hasLens ? lensSkinLocal : Vector3.Zero,
			eyeLocalOffset,
			_sightEyeViewmodelOffset,
			_viewmodel.WorldScale.x,
			WeaponSkin is { RenderOptions.Overlay: false },
			_viewmodel.IsValid() && WeaponSkin is { Enabled: true } );
	}

	Vector3 ComputeLensViewmodelOffset( AimboxAdsSightMode mode, float adsBlend, Vector3 fallbackOffset )
	{
		var bonePos = Vector3.Zero;
		TryGetCameraBoneLocal( out bonePos, out _ );

		if ( AimboxViewModelSightResolve.TryGetOpticLensSkinLocal( mode, WeaponSkin, AttachmentMount, out var lensSkinLocal ) )
			return -(lensSkinLocal - bonePos) * adsBlend;

		return -fallbackOffset * adsBlend;
	}

	static bool IsFiniteVector( Vector3 value ) =>
		float.IsFinite( value.x ) && float.IsFinite( value.y ) && float.IsFinite( value.z );

	void SanitizeSightEyeViewmodelOffset()
	{
		if ( !IsFiniteVector( _sightEyeViewmodelOffset ) )
		{
			_sightEyeViewmodelOffset = Vector3.Zero;
			return;
		}

		const float maxLength = 18f;
		var length = _sightEyeViewmodelOffset.Length;
		if ( length > maxLength )
			_sightEyeViewmodelOffset *= maxLength / length;
	}

	void ComputeIronSightViewmodelOffset( float adsBlend )
	{
		if ( !TryGetCameraBoneLocal( out var bonePos, out _ ) )
			return;

		_sightEyeViewmodelOffset = -bonePos * adsBlend;
	}

	/// <summary>Deprecated — sight offset is applied on the viewmodel; kept for callers that expect camera setup.</summary>
	public void ApplyAnimatedCameraSetup( CameraComponent camera, AimboxAdsSightMode mode, float adsBlend )
	{
		ComputeSightEyeViewmodelOffset( mode, adsBlend );
	}

	Vector3 ResolveSniperScopeFineTune( float adsBlend )
	{
		if ( adsBlend <= 0.001f )
			return Vector3.Zero;

		var tunerFine = Vector3.Zero;

		if ( IsM700ViewModel() )
		{
			if ( AttachmentMount?.HasAttachment( AimboxAttachmentId.RangedSight ) != true )
				return Vector3.Zero;

			var fine = ResolveM700ScopeAdsFineTune();
			tunerFine = AimboxViewModelAttachmentTuner.Instance?.M700ScopeAdsFineTune ?? Vector3.Zero;
			return (fine + tunerFine) * adsBlend;
		}

		if ( AttachmentMount?.HasAttachment( AimboxAttachmentId.RangedSight ) == true )
			return AimboxOpticAdsLayout.M4RangedSightFineTune * adsBlend;

		return Vector3.Zero;
	}

	static Vector3 ResolveM700ScopeAdsFineTune()
	{
		if ( AimboxGame.Instance?.GunBuilderScene == true
		     && AimboxGunBuilder.Instance is { ApplyM700ScopeAdsFineTune: true } gunBuilder )
			return gunBuilder.M700ScopeAdsViewmodelFineTune;

		if ( AimboxOpticAdsTuner.IsActive )
			return AimboxOpticAdsLayout.M700RangedSightFineTune;

		return AimboxAdsSightTuning.M700ScopeAdsViewmodelFineTune;
	}

	Vector3 ResolveRedDotFineTune( float adsBlend )
	{
		if ( adsBlend <= 0.001f || AttachmentMount is null )
			return Vector3.Zero;

		if ( !AttachmentMount.TryGetEquippedRedDotStyleAttachment( out var equippedSight ) )
			return Vector3.Zero;

		var fine = equippedSight switch
		{
			AimboxAttachmentId.HoloSight => AimboxOpticAdsLayout.HoloFineTune,
			AimboxAttachmentId.RaisedRedDot => AimboxOpticAdsLayout.RaisedRedDotFineTune,
			_ => Vector3.Zero
		};

		return fine * adsBlend;
	}

	float ResolveSightAdsForwardOffset( AimboxAdsSightMode mode, float adsBlend )
	{
		if ( adsBlend <= 0.001f || !WeaponSkin.IsValid() )
			return 0f;

		switch ( mode )
		{
			case AimboxAdsSightMode.RedDot:
				if ( AimboxViewModelSightResolve.TryGetOpticLensSkinLocal(
					     AimboxAdsSightMode.RedDot, WeaponSkin, AttachmentMount, out _ ) )
					return 0f;

				return AimboxAdsSightTuning.RedDotAdsForwardOffset;

			case AimboxAdsSightMode.SniperScope:
				if ( AimboxViewModelSightResolve.TryGetOpticLensSkinLocal(
					     AimboxAdsSightMode.SniperScope, WeaponSkin, AttachmentMount, out _ ) )
					return 0f;

				return AimboxAdsSightTuning.SniperAdsForwardOffset;

			case AimboxAdsSightMode.IronSight:
				return AimboxAdsSightTuning.IronSightAdsForwardOffset;

			default:
				return 0f;
		}
	}

	bool TryGetCameraBoneLocal( out Vector3 localPosition, out Rotation localRotation )
	{
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;

		if ( !WeaponSkin.IsValid() )
			return false;

		var boneObject = WeaponSkin.GetBoneObject( "camera" );
		if ( !boneObject.IsValid() && WeaponSkin.Model.IsValid() && WeaponSkin.Model.Bones.HasBone( "camera" ) )
			boneObject = WeaponSkin.GetBoneObject( WeaponSkin.Model.Bones.GetBone( "camera" ) );

		if ( !boneObject.IsValid() )
			return false;

		localPosition = WeaponSkin.WorldTransform.PointToLocal( boneObject.WorldPosition );
		localRotation = boneObject.LocalRotation;
		return true;
	}

	bool IsM700ViewModel()
	{
		return _activeModelPath.Contains( "sniper_m700", StringComparison.OrdinalIgnoreCase )
		       || _activeModelPath.Contains( "v_m700", StringComparison.OrdinalIgnoreCase );
	}

	public bool TryGetM700ScopePipAnchorWorld( out Vector3 worldPosition ) =>
		TryGetScopePipAnchorWorld( out worldPosition );

	public bool TryGetScopePipAnchorWorld( out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( !WeaponSkin.IsValid() )
			return false;

		if ( AimboxViewModelSightResolve.TryGetOpticAnchorSkinLocal(
			     AimboxAdsSightMode.SniperScope, WeaponSkin, AttachmentMount, out var skinLocal ) )
		{
			worldPosition = WeaponSkin.WorldTransform.PointToWorld( skinLocal );
			return true;
		}

		if ( IsM700ViewModel() && TryGetCameraBoneLocal( out var boneLocal, out _ ) )
		{
			worldPosition = WeaponSkin.WorldTransform.PointToWorld( boneLocal );
			return true;
		}

		return false;
	}

	public bool TryGetM700ScopePipLensWorld( Rotation eyeRotation, out Vector3 lensWorld ) =>
		TryGetScopePipLensWorld( eyeRotation, out lensWorld );

	public bool TryGetScopePipLensWorld( Rotation eyeRotation, out Vector3 lensWorld )
	{
		lensWorld = default;
		if ( !WeaponSkin.IsValid() )
			return false;

		if ( AimboxViewModelSightResolve.TryGetOpticLensSkinLocal(
			     AimboxAdsSightMode.SniperScope, WeaponSkin, AttachmentMount, out var skinLocal ) )
		{
			lensWorld = WeaponSkin.WorldTransform.PointToWorld( skinLocal );
			return true;
		}

		if ( !TryGetScopePipAnchorWorld( out var scopeEye ) )
			return false;

		lensWorld = scopeEye + eyeRotation.Forward * AimboxAdsSightTuning.M700ScopeLensForwardOffset;
		return true;
	}

	void SetViewModelOverlayPass( bool overlayPass )
	{
		if ( !_viewmodel.IsValid() )
			return;

		foreach ( var renderer in _viewmodel.GetComponentsInChildren<Component>( true ) )
		{
			switch ( renderer )
			{
				case SkinnedModelRenderer skin:
					skin.RenderOptions.Game = true;
					skin.RenderOptions.Overlay = overlayPass;
					if ( skin.SceneObject.IsValid() )
						skin.RenderOptions.Apply( skin.SceneObject );
					break;
				case ModelRenderer model:
					model.RenderOptions.Game = true;
					model.RenderOptions.Overlay = overlayPass;
					if ( model.SceneObject.IsValid() )
						model.RenderOptions.Apply( model.SceneObject );
					break;
			}
		}
	}

	static void TagViewmodelForScopeCameraExclusion( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		root.Tags.Add( "aimbox_viewmodel" );
		foreach ( var child in root.Children )
		{
			if ( child.IsValid() )
				TagViewmodelForScopeCameraExclusion( child );
		}
	}

	void SetViewModelRenderersEnabled( bool enabled )
	{
		if ( !_viewmodel.IsValid() )
			return;

		foreach ( var renderer in _viewmodel.GetComponentsInChildren<Component>( true ) )
		{
			switch ( renderer )
			{
				case SkinnedModelRenderer skin:
					skin.Enabled = enabled;
					break;
				case ModelRenderer model:
					model.Enabled = enabled;
					break;
			}
		}
	}

	void SetViewModelVisuallyHidden( bool hidden )
	{
		if ( !_viewmodel.IsValid() || _viewModelVisuallyHidden == hidden )
			return;

		_viewModelVisuallyHidden = hidden;

		foreach ( var renderer in _viewmodel.GetComponentsInChildren<Component>( true ) )
		{
			switch ( renderer )
			{
				case SkinnedModelRenderer skin:
					skin.Tint = hidden
						? skin.Tint.WithAlpha( 0f )
						: skin.Tint.WithAlpha( 1f );
					break;
				case ModelRenderer model:
					model.Tint = hidden
						? model.Tint.WithAlpha( 0f )
						: model.Tint.WithAlpha( 1f );
					break;
			}
		}
	}

	void SetTunedRedDotMeshesVisible( bool visible )
	{
		if ( !_viewmodel.IsValid() )
			return;

		foreach ( var renderer in _viewmodel.GetComponentsInChildren<ModelRenderer>( true ) )
		{
			if ( renderer.GameObject.Name.Contains( "RedDot", StringComparison.OrdinalIgnoreCase ) )
				renderer.Enabled = visible;
		}

		AimboxViewModelAttachmentTuner.Instance?.SetRedDotMeshVisible( visible );
	}

	void ApplyTunedRedDotLensPresentation( bool useClearLens )
	{
		if ( !_viewmodel.IsValid() )
			return;

		var profile = AimboxOpticLensPresentation.GetRedDotLensProfile(
			AttachmentMount?.HasAttachment( AimboxAttachmentId.HoloSight ) == true
				? AimboxAttachmentId.HoloSight
				: AimboxAttachmentId.RaisedRedDot );
		var hideLens = useClearLens || AimboxOpticLensPresentation.ShouldAlwaysHideM4RedDotLens( profile );

		foreach ( var renderer in _viewmodel.GetComponentsInChildren<ModelRenderer>( true ) )
		{
			if ( !renderer.GameObject.Name.Contains( "RedDot", StringComparison.OrdinalIgnoreCase ) )
				continue;

			AimboxOpticLensPresentation.Apply( renderer, hideLens, profile );
		}

		AimboxViewModelAttachmentTuner.Instance?.ApplyRedDotLensPresentation( useClearLens );
	}

	public bool EnsureWeapon(
		AimboxWeaponDefinition weapon,
		IReadOnlyCollection<AimboxAttachmentId> attachments = null,
		float presentationSpeedMultiplier = 1f,
		bool skipDeployRoutine = false )
	{
		if ( weapon is null )
			return false;

		var path = weapon.ViewModelPath?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		if ( IsPresentingModelPath( path ) )
		{
			SyncAttachments( weapon.Id, attachments );
			return false;
		}

		SpawnViewModel( path, weapon, attachments, presentationSpeedMultiplier, skipDeployRoutine );
		return true;
	}

	public bool EnsureWeaponAfterGrenadeThrow(
		AimboxWeaponDefinition weapon,
		IReadOnlyCollection<AimboxAttachmentId> attachments = null,
		float presentationSpeedMultiplier = 1f ) =>
		EnsureWeapon( weapon, attachments, presentationSpeedMultiplier, skipDeployRoutine: true );

	public void SyncAttachments( AimboxWeaponId weaponId, IReadOnlyCollection<AimboxAttachmentId> attachments )
	{
		if ( !WeaponSkin.IsValid() || AttachmentMount is null || !AttachmentMount.IsValid() )
		{
			AimboxAttachmentPipelineDebug.Reg(
				$"SyncAttachments skipped weapon={weaponId} — skin/mount invalid (skin={WeaponSkin.IsValid()}, mount={AttachmentMount?.IsValid() == true})." );
			return;
		}

		var listText = AimboxAttachmentPipelineDebug.FormatList( attachments );
		AimboxAttachmentPipelineDebug.Reg( $"SyncAttachments weapon={weaponId} attachments=[{listText}]" );
		AimboxViewModelAttachmentDebug.Info(
			$"SyncAttachments weapon={weaponId} attachments=[{listText}]" );
		AttachmentMount.Apply( weaponId, WeaponSkin, attachments, ViewModelUseOverlayPass );
		Animator?.SetWeaponPose( AttachmentMount.RequiresWeaponPose ? 1 : 0 );
		UpdateM700IntegratedScopePresentation();
	}

	void UpdateM700IntegratedScopePresentation()
	{
		if ( !IsM700ViewModel() || !WeaponSkin.IsValid() )
			return;

		if ( AttachmentMount?.HasAttachment( AimboxAttachmentId.RangedSight ) == true )
		{
			AimboxSboxAttachmentCatalog.ApplyM700StockScopeBodyGroups( WeaponSkin );
			AimboxM700ScopeLensPresentation.EnsureStockScopeVisible( WeaponSkin );
			return;
		}

		AimboxSboxAttachmentCatalog.ApplyM700IronSightBodyGroups( WeaponSkin );
		AimboxM700ScopeLensPresentation.Apply( WeaponSkin, hideScopeLens: true );
	}

	public void TickGrenadePresentation(
		bool sprintHeld,
		bool crouching,
		Angles eyeAngles,
		Vector3 velocityWorld,
		bool grounded,
		float runSpeed )
	{
		TickViewKickRecovery();
		_adsOffsetCurrent = Vector3.Lerp( _adsOffsetCurrent, Vector3.Zero, Math.Clamp( Time.Delta * ViewModelAdsOffsetLerpSpeed, 0f, 1f ) );
		Animator?.OwnerTickGrenadePresentation( sprintHeld, crouching, eyeAngles, velocityWorld, grounded, runSpeed );
		ApplyViewModelTransform();
	}

	public bool EnsureGrenadePresentation( AimboxWeaponDefinition grenade )
	{
		if ( grenade is null || !grenade.IsGrenade )
			return false;

		var path = grenade.ViewModelPath?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		_grenadePresentationWeaponId = grenade.Id;
		if ( IsPresentingModelPath( path ) )
			return true;

		SpawnViewModel( path, grenade, [], 1f );
		return true;
	}

	public bool BeginGrenadeCharge() => Animator?.BeginGrenadeCharge() == true;

	public void ReleaseGrenadeThrow( Action onRelease, Action onComplete ) =>
		Animator?.ReleaseGrenadeThrow( onRelease, onComplete );

	public void ReleaseGrenadeQuickToss( Action onRelease, Action onComplete ) =>
		Animator?.ReleaseGrenadeQuickToss( onRelease, onComplete );

	public void EndGrenadePresentation()
	{
		_grenadePresentationWeaponId = null;
		Animator?.CancelActivePresentation();
		ClearViewModel();
	}

	public void TickPresentation(
		bool wantsAds,
		AimboxWeaponRuntime weaponRuntime,
		bool primaryHeld,
		bool sprintHeld,
		float adsPresentationSpeedMul = 1f )
	{
		TickViewKickRecovery();
		TickAdsOffsetLerp( wantsAds, adsPresentationSpeedMul );

		if ( Animator is not null && Animator.IsValid() && weaponRuntime is not null )
		{
			var def = weaponRuntime.Definition;
			TryGetOwnerPresentationState( out var crouching, out var eyeAngles, out var velocity, out var grounded, out var runSpeed );
			Animator.OwnerTickPresentation(
				wantsAds,
				weaponRuntime.ReloadStartedThisTick,
				weaponRuntime.UsesPerShellReload ? weaponRuntime.EffectiveShellReloadSeconds : weaponRuntime.EffectiveReloadSeconds,
				primaryHeld,
				ResolveFiringModeGraphEnum( def ),
				def.IsMelee,
				sprintHeld,
				weaponRuntime.PerkPresentationSpeedMultiplier,
				weaponRuntime.UsesPerShellReload && weaponRuntime.ReloadStartedThisTick,
				weaponRuntime.AmmoBeforeShellReload,
				weaponRuntime.UsesPerShellReload && weaponRuntime.IsReloading,
				crouching,
				eyeAngles,
				velocity,
				grounded,
				runSpeed );
		}
		else if ( weaponRuntime is not null )
		{
			AimboxViewModelMovementDebug.LogNoAnimator(
				_activeModelPath,
				WeaponSkin.IsValid(),
				WeaponSkin.IsValid() && WeaponSkin.UseAnimGraph );
		}

		ApplyViewModelTransform();
	}

	public void ClearViewModel()
	{
		if ( !_viewmodel.IsValid() )
			return;

		_viewmodel.Destroy();
		_viewmodel = default;
		_activeModelPath = "";
		_fpBowUsesStockPlaceholderMesh = false;
		WeaponSkin = default;
		Animator = default;
		AttachmentMount = default;
		_adsOffsetCurrent = Vector3.Zero;
		_sightAdsForwardOffset = 0f;
		_sightAdsForwardBlend = 0f;
		_sightEyeViewmodelOffset = Vector3.Zero;
		_lastM700ScopeSnapshot = null;
		_viewModelVisuallyHidden = false;
		ResetViewKick();
	}

	public void ApplyViewKick( float pitchDegreesUp, float yawDegreesRight )
	{
		if ( MathF.Abs( pitchDegreesUp ) < 1e-5f && MathF.Abs( yawDegreesRight ) < 1e-5f )
			return;

		_viewKickPitch += pitchDegreesUp * 0.42f;
		_viewKickYaw += yawDegreesRight * 0.55f;
		_viewKickRoll += yawDegreesRight * -0.18f;
	}

	public void ResetViewKick()
	{
		_viewKickPitch = 0f;
		_viewKickYaw = 0f;
		_viewKickRoll = 0f;
	}

	public void ResetCombatPresentation()
	{
		Animator?.ForceCombatReady();
	}

	void TickViewKickRecovery()
	{
		if ( MathF.Abs( _viewKickPitch ) < 1e-4f && MathF.Abs( _viewKickYaw ) < 1e-4f && MathF.Abs( _viewKickRoll ) < 1e-4f )
			return;

		var dt = Math.Clamp( Time.Delta, 0.001f, 0.05f );
		var t = Math.Clamp( dt * 16f, 0f, 1f );
		_viewKickPitch = MathX.Lerp( _viewKickPitch, 0f, t );
		_viewKickYaw = MathX.Lerp( _viewKickYaw, 0f, t );
		_viewKickRoll = MathX.Lerp( _viewKickRoll, 0f, t );
	}

	bool IsPresentingModelPath( string modelPath ) =>
		_viewmodel.IsValid()
		&& !string.IsNullOrWhiteSpace( _activeModelPath )
		&& string.Equals( _activeModelPath, modelPath.Trim(), StringComparison.OrdinalIgnoreCase );

	void SpawnViewModel(
		string modelPath,
		AimboxWeaponDefinition weapon,
		IReadOnlyCollection<AimboxAttachmentId> attachments,
		float presentationSpeedMultiplier = 1f,
		bool skipDeployRoutine = false )
	{
		Animator?.CancelActivePresentation();
		ClearViewModel();

		if ( !ClientFxContext() )
			return;

		var model = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback(
			modelPath,
			$"FP viewmodel {weapon.Id}",
			out var usedFallbackGeometry,
			out var usedBowStockFpPlaceholder );
		_fpBowUsesStockPlaceholderMesh = usedBowStockFpPlaceholder;
		if ( !model.IsValid() || model.IsError )
			return;

		_viewmodel = new GameObject( true, WeaponViewmodelChildName );
		_viewmodel.NetworkMode = NetworkMode.Never;
		_viewmodel.SetParent( GameObject );
		TagViewmodelForScopeCameraExclusion( _viewmodel );

		_activeModelPath = modelPath;

		var skin = _viewmodel.Components.Create<SkinnedModelRenderer>();
		skin.Model = model;
		skin.Tint = usedFallbackGeometry ? new Color( 0.1f, 0.85f, 1f, 1f ) : new Color( 0.94f, 0.94f, 0.94f, 1f );
		skin.RenderType = ModelRenderer.ShadowRenderType.Off;
		skin.Enabled = true;
		skin.RenderOptions.Game = true;
		skin.RenderOptions.Overlay = ViewModelUseOverlayPass;
		skin.UseAnimGraph = AimboxWeaponResourceLoad.UsesStockFpAnimatorSequences( modelPath ) && !usedFallbackGeometry;
		skin.CreateBoneObjects = true;
		skin.CreateAttachments = true;
		WeaponSkin = skin;

		if ( weapon.Id == AimboxWeaponId.M700 )
		{
			AimboxSboxAttachmentCatalog.CaptureM700DefaultBodyGroups( skin );
			AimboxM700ScopeInvestigationDebug.ResetForNewViewModel();
			AimboxM700ScopeDebug.NotifyWeaponEquipped( weapon.Id, modelPath );
		}

		AttachmentMount = _viewmodel.Components.Create<AimboxViewModelAttachmentMount>();
		AimboxViewModelAttachmentDebug.Info(
			$"SpawnViewModel weapon={weapon.Id} path={modelPath} attachments=[{string.Join( ", ", attachments ?? [] )}]" );
		AttachmentMount.Apply( weapon.Id, skin, attachments, ViewModelUseOverlayPass );
		UpdateM700IntegratedScopePresentation();

		if ( skin.UseAnimGraph )
		{
			Animator = _viewmodel.Components.Create<AimboxViewModelFpAnimator>();
			ConfigureAnimatorForWeapon( Animator, weapon, modelPath );
			if ( skipDeployRoutine )
				Animator.BindSkinReadyForCombat( skin, model, presentationSpeedMultiplier );
			else
				Animator.BindAndRunEquipRoutine( skin, model, presentationSpeedMultiplier );
			Animator.SetWeaponPose( AttachmentMount.RequiresWeaponPose ? 1 : 0 );
			var arms = TryCreateFirstPersonArmsRenderer( skin );
			if ( arms.IsValid() )
				Animator.AddLinkedArms( arms );
		}

		AimboxViewModelMovementDebug.LogSpawn(
			modelPath,
			skin.UseAnimGraph,
			usedFallbackGeometry,
			Animator is not null && Animator.IsValid(),
			AimboxViewModelMovementDebug.ProbeGraph( model ) );

		ApplyViewModelTransform();
		_ = ApplyRenderOptionsWhenSceneReadyAsync( skin );
	}

	static void ConfigureAnimatorForWeapon( AimboxViewModelFpAnimator anim, AimboxWeaponDefinition weapon, string modelPath )
	{
		if ( anim is null || weapon is null )
			return;

		if ( string.Equals( modelPath, AimboxWeaponResourceLoad.SniperFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase ) )
		{
			anim.DeploySequenceName = "Deploy";
			anim.IdleSequenceName = "IdlePose";
			anim.ReloadSequenceName = "Reload_Pull";
			anim.AdsSequenceName = "Ironsights_Pose_Normal";
			anim.UseGraphIronsightsParameterForAds = true;
			anim.IronsightsBlendPerSecond = 6f;
		}
		else if ( string.Equals( modelPath, AimboxWeaponResourceLoad.ShotgunFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase ) )
		{
			anim.ReloadFirstShellSequenceName = "Reload_FirstShell";
			anim.ReloadSequenceName = "Reload_Shell";
			anim.ReloadDurationFallbackSeconds = 0.52f;
		}
		else if ( string.Equals( modelPath, AimboxWeaponResourceLoad.UspFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase ) )
		{
			// Stock s&box pistol uses anim graph b_reload, not a Reload_Empty direct-playback sequence.
			anim.ReloadSequenceName = "";
			anim.ReloadDurationFallbackSeconds = weapon.ReloadSeconds;
		}
		else if ( weapon.IsMelee )
		{
			anim.DeploySequenceName = "Deploy";
			anim.IdleSequenceName = "IdlePose";
			anim.ReloadSequenceName = "";
			anim.AdsSequenceName = "";
			anim.UseGraphIronsightsParameterForAds = false;
			anim.MeleeLightAttackSequenceName = "Attack_01a";
			anim.MeleeHeavyAttackSequenceName = "Backstab_Attack";
		}
		else if ( weapon.IsGrenade )
		{
			anim.IsGrenadePresentation = true;
			anim.DeploySequenceName = "Deploy";
			anim.IdleSequenceName = "IdlePose";
			anim.ReloadSequenceName = "";
			anim.AdsSequenceName = "";
			anim.UseGraphIronsightsParameterForAds = false;
			anim.DeployDurationFallbackSeconds = 0.65f;
		}
	}

	SkinnedModelRenderer TryCreateFirstPersonArmsRenderer( SkinnedModelRenderer weaponSkin )
	{
		if ( !UseFirstPersonArmsHuman || !_viewmodel.IsValid() || !weaponSkin.IsValid() )
			return default;

		var armsModel = AimboxWeaponResourceLoad.LoadWeaponModelOrFallback(
			AimboxWeaponResourceLoad.FirstPersonArmsHumanPath,
			"FP arms",
			out var usedFallbackGeometry );
		if ( usedFallbackGeometry || !armsModel.IsValid() || armsModel.IsError )
			return default;

		var armsGo = new GameObject( true, "FirstPersonArms" );
		armsGo.NetworkMode = NetworkMode.Never;
		armsGo.SetParent( _viewmodel );
		armsGo.LocalPosition = Vector3.Zero;
		armsGo.LocalRotation = Rotation.Identity;
		armsGo.LocalScale = Vector3.One;

		var arms = armsGo.Components.Create<SkinnedModelRenderer>();
		arms.Model = armsModel;
		arms.BoneMergeTarget = weaponSkin;
		arms.RenderType = ModelRenderer.ShadowRenderType.Off;
		arms.UseAnimGraph = false;
		arms.RenderOptions.Game = true;
		arms.RenderOptions.Overlay = ViewModelUseOverlayPass;
		arms.Enabled = true;
		return arms;
	}

	void TickAdsOffsetLerp( bool adsHeld, float speedMul )
	{
		var forward = adsHeld
			? ViewModelAdsForwardOffset + _sightAdsForwardOffset * _sightAdsForwardBlend
			: 0f;
		var target = new Vector3( forward, 0f, 0f );
		var t = Math.Clamp( Time.Delta * ViewModelAdsOffsetLerpSpeed * MathF.Max( 0.25f, speedMul ), 0f, 1f );
		_adsOffsetCurrent = Vector3.Lerp( _adsOffsetCurrent, target, t );
	}

	void ApplyViewModelTransform()
	{
		if ( !_viewmodel.IsValid() )
			return;

		var itemOffset = Vector3.Zero;
		var itemEuler = Vector3.Zero;
		var itemScale = Vector3.One;
		var applyGunMeshScaleMul = AimboxWeaponResourceLoad.UsesStockFpAnimatorSequences( _activeModelPath );

		if ( !applyGunMeshScaleMul )
		{
			itemScale = Vector3.One;
		}

		var scale = applyGunMeshScaleMul
			? itemScale * FpWeaponMeshRootScaleMul
			: itemScale;

		SanitizeSightEyeViewmodelOffset();
		var gripPosition = ViewModelGripLocalPosition + itemOffset + _adsOffsetCurrent + _sightEyeViewmodelOffset;

		if ( UseCameraBoneGripTracking
		     && AimboxWeaponResourceLoad.UsesStockFpAnimatorSequences( _activeModelPath )
		     && TryGetCameraBoneLocal( out var cameraBoneLocal, out _ ) )
			gripPosition -= cameraBoneLocal;

		_viewmodel.LocalPosition = gripPosition;
		_viewmodel.LocalRotation = Rotation.From( new Angles(
			_viewKickPitch + itemEuler.x,
			_viewKickYaw + itemEuler.y,
			_viewKickRoll + itemEuler.z ) );
		_viewmodel.LocalScale = scale;
	}

	async Task ApplyRenderOptionsWhenSceneReadyAsync( SkinnedModelRenderer skin )
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !skin.IsValid() || !_viewmodel.IsValid() )
			return;

		skin.RenderOptions.Game = true;
		skin.RenderOptions.Overlay = ViewModelUseOverlayPass;
		if ( skin.SceneObject.IsValid() )
			skin.RenderOptions.Apply( skin.SceneObject );

		if ( Animator.IsValid() )
			skin.UseAnimGraph = true;
	}

	public static int ResolveFiringModeGraphEnum( AimboxWeaponDefinition def )
	{
		if ( def is null || def.IsMelee )
			return 1;

		return def.Id switch
		{
			AimboxWeaponId.M4A1 or AimboxWeaponId.Mp5 => 3,
			_ => 1
		};
	}

	static bool ClientFxContext() => Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;

	bool TryGetOwnerPresentationState(
		out bool crouching,
		out Angles eyeAngles,
		out Vector3 velocity,
		out bool grounded,
		out float runSpeed )
	{
		crouching = false;
		eyeAngles = Angles.Zero;
		velocity = Vector3.Zero;
		grounded = true;
		runSpeed = 320f;

		var player = _ownerPlayer;
		if ( player is null || !player.IsValid() )
			player = Components.GetInParent<AimboxPlayerController>();
		if ( player is null || !player.IsValid() )
		{
			AimboxViewModelMovementDebug.LogTickBlocked( "owner player not bound (camera is not parented to pawn)" );
			return false;
		}

		crouching = player.IsCrouching;
		eyeAngles = player.EyeRotation.Angles();
		velocity = player.GetMovementVelocity();
		grounded = AimboxCitizenMovementMotor.IsGrounded( Scene, player.GameObject, player.WorldPosition );
		var sprinting = Input.Down( "Run" ) && !player.IsCrouching;
		runSpeed = sprinting ? AimboxCitizenMovementMotor.SprintSpeed : AimboxCitizenMovementMotor.WalkSpeed;
		return true;
	}
}
