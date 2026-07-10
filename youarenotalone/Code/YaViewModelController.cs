using System;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Local-owner first-person viewmodels parented to the pawn <c>View</c> GameObject — the same object as <see cref="YaPawnCamera"/>
/// and <see cref="CameraComponent"/> (see THORNS_EVERYTHING_DOCUMENT — never Scene.Camera).
/// Facepunch <c>v_m4a1</c> is weapon geometry only (no arms — add a separate arms asset / bone‑merge later).
/// Animations/recoil/reload originate from transforms here later; no networking.
/// </summary>
[Title( "Thorns — ViewModel Controller" )]
[Category( "Thorns" )]
[Icon( "view_in_ar" )]
[Order( 105 )]
public sealed class YaViewModelController : Component
{
	public const string FirstPersonArmsHumanPath = "models/first_person/v_first_person_arms_human.vmdl";

	public const string M4FirstPersonViewmodelPath = "models/weapons/sbox_assault_m4a1/v_m4a1.vmdl";

	/// <summary>Third-person / other players — use <c>w_*</c> from <see href="https://sbox.game/facepunch/sboxweapons">sboxweapons</see>; <c>v_*</c> is FP-only.</summary>
	public const string M4WorldModelPath = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";

	public const string Mp5FirstPersonViewmodelPath = "models/weapons/sbox_smg_mp5/v_mp5.vmdl";

	public const string Mp5WorldModelPath = "models/weapons/sbox_smg_mp5/w_mp5.vmdl";

	public const string ShotgunFirstPersonViewmodelPath = "models/weapons/sbox_shotgun_spaghellim4/v_spaghellim4.vmdl";

	public const string ShotgunWorldModelPath = "models/weapons/sbox_shotgun_spaghellim4/w_spaghellim4.vmdl";

	public const string SniperFirstPersonViewmodelPath = "models/weapons/sbox_sniper_m700/v_m700.vmdl";

	public const string SniperWorldModelPath = "models/weapons/sbox_sniper_m700/w_m700.vmdl";

	public const string BayonetM9FirstPersonViewmodelPath = "models/weapons/sbox_melee_m9bayonet/v_m9bayonet.vmdl";

	public const string BayonetM9WorldModelPath = "models/weapons/sbox_melee_m9bayonet/w_m9bayonet.vmdl";

	/// <summary>
	/// Grip offset vs <c>View</c> (<see cref="YaPawnCamera"/>). Defaults tuned to FP M4 aligned with barrel toward crosshair.
	/// </summary>
	[Property]
	public Vector3 ViewModelGripLocalPosition { get; set; } = Vector3.Zero;

	/// <summary>Added to <see cref="ViewModelGripLocalPosition"/> on spawn. Tune Y for screen height; Z for depth. Skeletal clips / graph params can still shift pose vs bind.</summary>
	[Property]
	public Vector3 ViewModelPresentationLocalOffset { get; set; } = Vector3.Zero;

	/// <summary>Euler offsets (degrees) on <c>WeaponViewmodel</c> — pitch·X, yaw·Y, roll·Z.</summary>
	[Property]
	public Vector3 ViewModelGripLocalEulerDegrees { get; set; } = Vector3.Zero;

	/// <summary>Extra forward offset (+X) applied only while ADS is held to reduce sight/camera clipping.</summary>
	[Property]
	public float ViewModelAdsForwardOffset { get; set; } = 0f;

	/// <summary>Smoothing speed for blending between hip and ADS offsets.</summary>
	[Property]
	public float ViewModelAdsOffsetLerpSpeed { get; set; } = 16f;

	/// <summary>Drive embedded FP anim graphs (deploy/idle paths). Disable only for rigid bind-pose troubleshooting.</summary>
	[Property]
	public bool ViewModelUseAnimGraph { get; set; } = true;

	/// <summary>Use overlay draw pass — required for many FP rigs or they draw behind world / get culled incorrectly.</summary>
	[Property]
	public bool ViewModelUseOverlayPass { get; set; } = true;

	/// <summary>Mount Facepunch first-person human arms beside the weapon viewmodel.</summary>
	[Property]
	public bool UseFirstPersonArmsHuman { get; set; } = true;

	GameObject _viewmodel;
	Vector3 _adsOffsetCurrent;
	bool _spectatorHidden;

	/// <summary>Explicit camera on this GameObject (same transform as parenting target per THORNS document).</summary>
	CameraComponent CameraExplicit => Components.Get<CameraComponent>();

	/// <summary>Hide first-person weapon mesh while <see cref="YaRoundSpectator"/> drives the camera at another pawn.</summary>
	public void SetSpectatorPresentationHidden( bool hide )
	{
		_spectatorHidden = hide;
		if ( _viewmodel.IsValid() )
			_viewmodel.Enabled = !hide;
	}

	/// <summary>Stock FP weapons that share <see cref="YaViewModelFpAnimator"/> sequence names (deploy / idle / ADS / reload).</summary>
	public static bool UsesStockFpAnimatorSequences( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		return string.Equals( modelPath, M4FirstPersonViewmodelPath, StringComparison.Ordinal )
		       || string.Equals( modelPath, Mp5FirstPersonViewmodelPath, StringComparison.Ordinal )
		       || string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal )
		       || string.Equals( modelPath, SniperFirstPersonViewmodelPath, StringComparison.Ordinal )
		       || string.Equals( modelPath, BayonetM9FirstPersonViewmodelPath, StringComparison.Ordinal );
	}

	/// <summary>Combat ids that mount <see cref="YaViewModelFpAnimator"/> (deploy / reload presentation gates fire input).</summary>
	public static bool CombatDefinitionUsesStockFpAnimator( string combatWeaponDefinitionId )
	{
		if ( string.IsNullOrWhiteSpace( combatWeaponDefinitionId ) )
			return false;

		return string.Equals( combatWeaponDefinitionId, "m4", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "mp5", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "shotgun", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "sniper", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "m9_bayonet", StringComparison.OrdinalIgnoreCase );
	}

	public void SpawnViewModel( string modelPath )
	{
		ClearViewModel();

		var isLocal = YaPawn.IsLocalConnectionOwner( this );
		Log.Info( $"IsLocal check: {(isLocal ? "true" : "false")}" );

		if ( !isLocal )
			return;

		if ( !Game.IsPlaying )
			return;

		if ( !YaGameplayIsClientFxContext() )
			return;

		if ( !Components.Get<YaPawnCamera>().IsValid() || !CameraExplicit.IsValid() )
		{
			Log.Info( "[YA] FP viewmodel: camera not ready yet — retrying (common on join / first equip)." );
			_ = SpawnViewModelWhenCameraReadyAsync( modelPath );
			return;
		}

		SpawnViewModelCore( modelPath );
	}

	async Task SpawnViewModelWhenCameraReadyAsync( string modelPath )
	{
		for ( var attempt = 0; attempt < 40; attempt++ )
		{
			await Task.DelayRealtimeSeconds( 0.05f );
			if ( !GameObject.IsValid() || !Game.IsPlaying )
				return;
			if ( !YaPawn.IsLocalConnectionOwner( this ) )
				return;
			if ( !YaGameplayIsClientFxContext() )
				return;
			if ( Components.Get<YaPawnCamera>().IsValid() && CameraExplicit.IsValid() )
			{
				SpawnViewModel( modelPath );
				return;
			}
		}

		Log.Warning( $"[YA] FP viewmodel: camera never became ready; skipped spawn for '{modelPath}'." );
	}

	void SpawnViewModelCore( string modelPath )
	{
		if ( string.Equals( modelPath, M4FirstPersonViewmodelPath, StringComparison.Ordinal ) )
			Log.Info( "Spawning M4 viewmodel" );
		else if ( string.Equals( modelPath, Mp5FirstPersonViewmodelPath, StringComparison.Ordinal ) )
			Log.Info( "Spawning MP5 viewmodel" );
		else if ( string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal ) )
			Log.Info( "Spawning Shotgun viewmodel" );
		else if ( string.Equals( modelPath, SniperFirstPersonViewmodelPath, StringComparison.Ordinal ) )
			Log.Info( "Spawning Sniper viewmodel" );
		else if ( string.Equals( modelPath, BayonetM9FirstPersonViewmodelPath, StringComparison.Ordinal ) )
			Log.Info( "Spawning M9 bayonet viewmodel" );

		if ( string.IsNullOrWhiteSpace( modelPath ) )
		{
			Log.Info( "[YA] FP viewmodel: empty model path." );
			return;
		}

		var mdl = YaWeaponResourceLoad.LoadWeaponModelOrFallback( modelPath, "FP viewmodel", out var usedFallbackGeometry );
		if ( !mdl.IsValid() || mdl.IsError )
		{
			Log.Warning( $"[YA] FP viewmodel: model invalid after load for '{modelPath}'." );
			return;
		}

		if ( usedFallbackGeometry )
			Log.Warning( $"[YA] FP viewmodel: using dev fallback for '{modelPath}' — ensure thorns.sbproj references facepunch.sboxweapons (and mounted on client)." );
		else
			Log.Info( $"[YA] FP viewmodel: loaded '{modelPath}'." );

		// bool = start active — `false` leaves the object disabled so nothing draws in-game (still visible in hierarchy / editor preview).
		_viewmodel = new GameObject( true, "WeaponViewmodel" );
		// Local-only FP presentation object: never replicate to other clients.
		_viewmodel.NetworkMode = NetworkMode.Never;
		_viewmodel.SetParent( GameObject );
		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset;
		var r = ViewModelGripLocalEulerDegrees;
		_viewmodel.LocalRotation = Rotation.From( r.x, r.y, r.z );
		_viewmodel.LocalScale = Vector3.One;

		// FP weapon assets (v_m4a1 etc.) are skinned rigs — ModelRenderer previews in inspector but SkinnedModelRenderer drives bones in-game.
		var vmr = _viewmodel.Components.Create<SkinnedModelRenderer>();
		vmr.Model = mdl;
		vmr.Tint = new Color( 0.94f, 0.94f, 0.94f, 1f );

		vmr.RenderType = ModelRenderer.ShadowRenderType.Off;
		vmr.Enabled = true;
		vmr.UseAnimGraph = ViewModelUseAnimGraph;

		vmr.RenderOptions.Game = true;
		vmr.RenderOptions.Overlay = ViewModelUseOverlayPass;

		if ( ViewModelUseAnimGraph && UsesStockFpAnimatorSequences( modelPath ) && !usedFallbackGeometry )
		{
			var anim = _viewmodel.Components.Create<YaViewModelFpAnimator>();
			ConfigureAnimatorPresetForModel( anim, modelPath );
			TryPlayFpGunDeploySoundForModel( modelPath );
			anim.BindAndRunEquipRoutine( vmr, mdl );
			_ = TryCreateFirstPersonArmsRenderer( vmr );
		}
		else
		{
			_ = TryCreateFirstPersonArmsRenderer( vmr );
		}

		_ = ApplyViewmodelRenderOptionsWhenSceneReadyAsync( vmr );
	}

	protected override void OnUpdate()
	{
		if ( !_viewmodel.IsValid() || !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( _spectatorHidden )
			return;

		var pawnRoot = GameObject.Parent;
		var equipWeapon = pawnRoot.IsValid()
			? pawnRoot.Components.Get<YaWeapon>()
			: default;
		var combatId = equipWeapon.IsValid() ? equipWeapon.ClientMirrorCombatDefinitionId ?? "" : "";
		var meleeEquipped = YaWeaponDefinitions.TreatsAsMeleeWeapon( YaWeaponDefinitions.Get( combatId ), combatId );
		var adsHeld = !meleeEquipped && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var target = adsHeld ? new Vector3( ViewModelAdsForwardOffset, 0f, 0f ) : Vector3.Zero;
		var t = Math.Clamp( Time.Delta * ViewModelAdsOffsetLerpSpeed, 0f, 1f );
		_adsOffsetCurrent = Vector3.Lerp( _adsOffsetCurrent, target, t );

		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset + _adsOffsetCurrent;
	}

	SkinnedModelRenderer TryCreateFirstPersonArmsRenderer( SkinnedModelRenderer weaponSkin )
	{
		if ( !UseFirstPersonArmsHuman || !_viewmodel.IsValid() || !weaponSkin.IsValid() )
			return default;

		var armsModel = YaWeaponResourceLoad.LoadWeaponModelOrFallback( FirstPersonArmsHumanPath, "FP arms", out var usedFallbackGeometry );
		if ( !armsModel.IsValid() || armsModel.IsError || usedFallbackGeometry )
		{
			Log.Warning( $"[YA] FP arms: failed to load '{FirstPersonArmsHumanPath}'." );
			return default;
		}

		var armsGo = new GameObject( true, "FirstPersonArms" );
		armsGo.NetworkMode = NetworkMode.Never;
		armsGo.SetParent( _viewmodel );
		armsGo.LocalPosition = Vector3.Zero;
		armsGo.LocalRotation = Rotation.Identity;
		armsGo.LocalScale = Vector3.One;

		var arms = armsGo.Components.Create<SkinnedModelRenderer>();
		arms.Model = armsModel;
		// Facepunch FP setup: bonemerge arms -> onto -> weapon.
		arms.BoneMergeTarget = weaponSkin;
		arms.RenderType = ModelRenderer.ShadowRenderType.Off;
		arms.UseAnimGraph = false;
		arms.RenderOptions.Game = true;
		arms.RenderOptions.Overlay = ViewModelUseOverlayPass;
		arms.Enabled = true;

		if ( arms.SceneObject.IsValid() )
			arms.RenderOptions.Apply( arms.SceneObject );

		Log.Info( $"[YA] FP arms loaded '{FirstPersonArmsHumanPath}'." );
		return arms;
	}

	void TryPlayFpGunDeploySoundForModel( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( YaWeapon.GunDeploySoundResource ) )
			return;
		if ( !string.Equals( modelPath, M4FirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal ) )
			return;

		var pawnRoot = GameObject.Parent;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var path = YaWeapon.GunDeploySoundResource.Trim();
		var paranoiaMul = YaHunterParanoia.LocalOwnedPawnSoundVolumeScale( pawnRoot );
		if ( YaCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var ear, out _ ) )
		{
			var h = Sound.Play( path, ear );
			if ( h is { IsValid: true } snd && paranoiaMul < 0.999f )
				snd.Volume *= paranoiaMul;
		}
		else
		{
			var h = Sound.Play( path, pawnRoot.WorldPosition + Vector3.Up * 40f );
			if ( h is { IsValid: true } snd && paranoiaMul < 0.999f )
				snd.Volume *= paranoiaMul;
		}
	}

	static void ConfigureAnimatorPresetForModel( YaViewModelFpAnimator anim, string modelPath )
	{
		if ( !anim.IsValid() )
			return;

		if ( string.Equals( modelPath, SniperFirstPersonViewmodelPath, StringComparison.Ordinal ) )
		{
			anim.DeploySequenceName = "Deploy";
			anim.IdleSequenceName = "IdlePose";
			anim.AdsSequenceName = "Ironsights_Pose_Normal";
			anim.ReloadSequenceName = "Reload_Pull";
			anim.UseGraphIronsightsParameterForAds = false;
		}

		if ( string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal ) )
		{
			anim.ReloadFirstShellSequenceName = "Reload_FirstShell";
			anim.ReloadSequenceName = "Reload_Shell";
			anim.ReloadDurationFallbackSeconds = 0.52f;
			anim.ShellReloadGraphPulseHoldSeconds = 0.08f;
		}

		if ( string.Equals( modelPath, BayonetM9FirstPersonViewmodelPath, StringComparison.Ordinal ) )
		{
			anim.DeploySequenceName = "Deploy";
			anim.IdleSequenceName = "IdlePose";
			anim.ReloadSequenceName = "";
			anim.AdsSequenceName = "";
			anim.UseGraphIronsightsParameterForAds = false;
			anim.MeleeLightAttackSequenceName = "Attack_01a";
			anim.MeleeHeavyAttackSequenceName = "Backstab_Attack";
		}
	}

	async Task ApplyViewmodelRenderOptionsWhenSceneReadyAsync( SkinnedModelRenderer vmr )
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !vmr.IsValid() || !GameObject.IsValid() || !_viewmodel.IsValid() )
			return;

		vmr.RenderOptions.Game = true;
		vmr.RenderOptions.Overlay = ViewModelUseOverlayPass;
		if ( vmr.SceneObject.IsValid() )
			vmr.RenderOptions.Apply( vmr.SceneObject );

		vmr.UseAnimGraph = ViewModelUseAnimGraph;
		// Animator child requires skeletal eval; keep enabled even if inspector once saved ViewModelUseAnimGraph false alongside an FP animator component.
		if ( _viewmodel.IsValid() && _viewmodel.Components.Get<YaViewModelFpAnimator>().IsValid() )
			vmr.UseAnimGraph = true;

		vmr.Enabled = true;

		Log.Info( $"[YA] Viewmodel render ready — SceneObject={vmr.SceneObject.IsValid()} game=true overlay={ViewModelUseOverlayPass} animGraph={vmr.UseAnimGraph}" );
	}

	public void ClearViewModel()
	{
		if ( _viewmodel.IsValid() )
		{
			Log.Info( "Destroying old viewmodel" );
			_viewmodel.Destroy();
			_viewmodel = default;
			_adsOffsetCurrent = Vector3.Zero;
		}
	}

	protected override void OnDestroy()
	{
		if ( _viewmodel.IsValid() )
			_viewmodel.Destroy();
	}

	static bool YaGameplayIsClientFxContext()
	{
		return Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
	}
}
