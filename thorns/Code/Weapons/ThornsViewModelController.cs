using System;
using System.Threading.Tasks;
using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// Local-owner first-person viewmodels parented to the pawn <c>View</c> GameObject — the same object as <see cref="ThornsPawnCamera"/>
/// and <see cref="CameraComponent"/> (see THORNS_EVERYTHING_DOCUMENT — never Scene.Camera).
/// Facepunch <c>v_m4a1</c> is weapon geometry only (no arms — add a separate arms asset / bone‑merge later).
/// Animations/recoil/reload originate from transforms here later; no networking.
/// </summary>
[Title( "Thorns — ViewModel Controller" )]
[Category( "Thorns" )]
[Icon( "view_in_ar" )]
[Order( 105 )]
public sealed class ThornsViewModelController : Component
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
	/// Uniform multiplier on equipped Facepunch <c>v_*</c> FP weapon mesh roots (not idle arms, not <c>models/tools/</c> axes/picks).
	/// </summary>
	public const float FpWeaponMeshRootScaleMul = 10f;

	// Grip vs View — inspector descriptions come from Property codegen (avoid /// on [Property] SB2000).
	[Property]
	public Vector3 ViewModelGripLocalPosition { get; set; } = Vector3.Zero;

	[Property]
	public Vector3 ViewModelPresentationLocalOffset { get; set; } = Vector3.Zero;

	[Property]
	public Vector3 ViewModelGripLocalEulerDegrees { get; set; } = Vector3.Zero;

	// View-local +X: ADS slide; same axis as ThornsWeapon.ViewModelLocalPosition for tool/medkit forward bias.
	[Property]
	public float ViewModelAdsForwardOffset { get; set; } = 0f;

	[Property]
	public float ViewModelAdsOffsetLerpSpeed { get; set; } = 16f;

	[Property]
	public bool ViewModelUseAnimGraph { get; set; } = true;

	[Property]
	public bool ViewModelUseOverlayPass { get; set; } = true;

	[Property]
	public bool UseFirstPersonArmsHuman { get; set; } = true;

	/// <summary>
	/// Extra root pose for <see cref="FirstPersonArmsHumanPath"/> when shown <b>without</b> a weapon (bone-merge rigs assume a grip parent; origin-only placement is wrong).
	/// View-local +X is toward the world in front of the camera (same as ADS slide). Tune on the <b>View</b> object’s controller.
	/// Each frame, <see cref="OnUpdate"/> writes this (plus grip/ADS offsets) onto the child <c>WeaponViewmodel</c> — editing that child’s transform in the hierarchy will snap back unless
	/// <see cref="PauseAutomaticWeaponViewmodelTransform"/> is enabled. Companion <see cref="IdleArmsOnlyRootLocalScale"/> scales the bare-hands root.
	/// </summary>
	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalOffset { get; set; } = new Vector3( 50f, 0f, -15f );

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalEulerDegrees { get; set; } = Vector3.Zero;

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalScale { get; set; } = new Vector3( 4f, 4f, 4f );

	/// <summary>Played once when bare hands appear (Facepunch arms punching set).</summary>
	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsDeploySequenceName { get; set; } = "Punching_Deploy";

	/// <summary>Looped after deploy when non-empty (default <c>Punching_Pose</c>).</summary>
	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsLoopSequenceName { get; set; } = "Punching_Pose";

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsMoveSequenceName { get; set; } = "Punching_Move";

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public float IdleArmsDeployDurationFallbackSeconds { get; set; } = 0.7f;

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsMeleeLightSequenceName { get; set; } = "Punching_Left_Hit_01";

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsMeleeLightAlternateSequenceName { get; set; } = "Punching_Right_Hit_01";

	/// <summary>Harvest axes / pickaxes: additive local pitch (degrees around view +X / camera right) for a chop swing.</summary>
	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestToolSwingPitchDegrees { get; set; } = 20f;

	/// <summary>Each ramp is this long in seconds (out + return); full swing is twice this.</summary>
	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestToolSwingHalfSeconds { get; set; } = 0.1f;

	/// <summary>Walk view-bob scale on FP axe/pick meshes (1 = same as camera; tools look best much subtler).</summary>
	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestAxePickaxeViewBobAmplitudeMul { get; set; } = 0.1f;

	/// <summary>
	/// When true, <see cref="ThornsWeapon"/> FP offset/scale/rotation inspector fields drive tools and held items instead of static registry defaults — updates every frame while playing.
	/// Grip offsets on this component always apply when automatic transform is not paused.
	/// </summary>
	[Property, Category( "Thorns/FP viewmodel (debug tuning)" )]
	public bool LiveInspectorPreview { get; set; } = true;

	/// <summary>
	/// When true, <see cref="OnUpdate"/> stops driving the child <c>WeaponViewmodel</c> transform so hierarchy edits stick.
	/// Auto-enables when you hand-edit <c>WeaponViewmodel</c> local position/rotation/scale during play (values are copied to <see cref="ThornsWeapon"/> or idle-arm fields).
	/// Uncheck to resume automatic pose from inspector fields.
	/// </summary>
	[Property, Category( "Thorns/FP viewmodel (debug tuning)" )]
	public bool PauseAutomaticWeaponViewmodelTransform { get; set; }

	GameObject _viewmodel;
	Vector3 _adsOffsetCurrent;
	Vector3 _lastDrivenViewmodelLocalPosition;
	Rotation _lastDrivenViewmodelLocalRotation;
	Vector3 _lastDrivenViewmodelLocalScale;
	bool _hasLastDrivenViewmodelTransform;
	bool _skipHierarchyEditLatchOnce;

	/// <summary>Last spawned FP model path on <c>_viewmodel</c> — drives per-item offset rules vs stock Facepunch rigs.</summary>
	string _fpVmActiveModelPath = "";

	bool _fpWeaponDrawingEnabled = true;

	/// <summary>Negative = idle. Local-owner FP swing for axe/pick mesh.</summary>
	double _harvestToolSwingStartRealtime = -1.0;

	string _fpVmDiagLastMsg = "";
	double _fpVmDiagNextLogTime;

	void FpVmDiag( string message )
	{
		if ( !ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs )
			return;

		var now = Time.Now;
		if ( message == _fpVmDiagLastMsg && now < _fpVmDiagNextLogTime )
			return;

		_fpVmDiagLastMsg = message;
		_fpVmDiagNextLogTime = now + 0.75;
		Log.Info( "[Thorns][FP-VM] " + message );
	}

	/// <summary>Explicit camera on this GameObject (same transform as parenting target per THORNS document).</summary>
	CameraComponent CameraExplicit => Components.Get<CameraComponent>();

	/// <summary>Stock FP weapons that share <see cref="ThornsViewModelFpAnimator"/> sequence names (deploy / idle / ADS / reload).</summary>
	public static bool UsesStockFpAnimatorSequences( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		var p = modelPath.Trim().Replace( '\\', '/' );
		return string.Equals( p, M4FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, Mp5FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, ShotgunFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, SniperFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, BayonetM9FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Combat ids that mount <see cref="ThornsViewModelFpAnimator"/> (deploy / reload presentation gates fire input).</summary>
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

	/// <summary>
	/// Idle arms + Facepunch stock FP rigs keep root at grip pose. Custom tool / held-consumable meshes use
	/// <see cref="ThornsItemRegistry"/> for the active hotbar item when possible (canonical at startup), then
	/// <see cref="ThornsWeapon"/> mirror fields. View-local +X is toward the world in front of the camera (ADS slide uses the same axis).
	/// </summary>
	void ResolveFpMeshRootPose( ThornsWeapon weapon, string loadedModelPath, out Vector3 itemOffset, out Vector3 itemScale, out Vector3 itemEulerDegrees )
	{
		itemOffset = Vector3.Zero;
		itemScale = Vector3.One;
		itemEulerDegrees = Vector3.Zero;
		var applyGunMeshScaleMul = true;
		if ( string.IsNullOrWhiteSpace( loadedModelPath ) )
			return;
		if ( string.Equals( loadedModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
		{
			// Facepunch arms are meant to bone-merge onto v_ weapons; alone they need a forward/down bias or they sit off-camera / inside the body.
			itemOffset = IdleArmsOnlyRootLocalOffset;
			itemScale = IdleArmsOnlyRootLocalScale;
			itemEulerDegrees = IdleArmsOnlyRootLocalEulerDegrees;
			applyGunMeshScaleMul = false;
		}
		else if ( UsesStockFpAnimatorSequences( loadedModelPath ) )
		{
			itemScale = weapon.IsValid()
				? ThornsItemRegistry.ResolveFpViewmodelRootScale( weapon.ViewModelLocalScale )
				: Vector3.One;
		}
		else if ( LiveInspectorPreview && weapon.IsValid() )
		{
			itemOffset = weapon.ViewModelLocalPosition;
			itemScale = ThornsItemRegistry.ResolveFpViewmodelRootScale( weapon.ViewModelLocalScale );
			itemEulerDegrees = weapon.ViewModelLocalEulerDegrees;
			if ( ThornsItemRegistry.IsHarvestToolViewModelPath( loadedModelPath ) )
			{
				var pawnRoot = GameObject.Parent;
				var hb = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsHotbarEquipment>() : default;
				var activeItemId = hb.IsValid() ? hb.ClientMirrorActiveItemId : "";
				if ( !string.IsNullOrWhiteSpace( activeItemId )
				     && ThornsItemRegistry.TryGet( activeItemId.Trim(), out var weaponDef )
				     && ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in weaponDef ) )
					applyGunMeshScaleMul = false;
			}
		}
		else
		{
			var pawnRoot = GameObject.Parent;
			var hb = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsHotbarEquipment>() : default;
			var activeItemId = hb.IsValid() ? hb.ClientMirrorActiveItemId : "";
			var resolved = false;
			if ( !string.IsNullOrWhiteSpace( activeItemId )
			     && ThornsItemRegistry.TryGet( activeItemId.Trim(), out var def )
			     && !string.IsNullOrWhiteSpace( def.ViewModelAsset )
			     && FpViewModelPathEquals( def.ViewModelAsset, loadedModelPath ) )
			{
				var useRegistryPose = def.ItemType == ThornsItemType.Tool
				                      || (ThornsItemRegistry.IsUsableConsumable( def )
				                          && !string.IsNullOrWhiteSpace( def.ViewModelAsset ));
				if ( useRegistryPose )
				{
					if ( def.ItemType == ThornsItemType.Tool )
					{
						itemOffset = ThornsItemRegistry.ComposeFpHarvestToolViewmodelOffset( in def );
						itemScale = ThornsItemRegistry.ResolveFpHarvestToolViewmodelScale( in def );
						if ( ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in def ) )
							applyGunMeshScaleMul = false;
					}
					else
					{
						itemOffset = def.FpViewmodelRootLocalOffset;
						itemScale = ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
					}

					itemEulerDegrees = def.ItemType == ThornsItemType.Tool
						? ThornsItemRegistry.ResolveFpHarvestToolViewmodelEulerDegrees( in def )
						: def.FpViewmodelRootLocalEulerDegrees;
					resolved = true;
				}
			}

			if ( !resolved && weapon.IsValid() )
			{
				itemOffset = weapon.ViewModelLocalPosition;
				itemScale = ThornsItemRegistry.ResolveFpViewmodelRootScale( weapon.ViewModelLocalScale );
				itemEulerDegrees = weapon.ViewModelLocalEulerDegrees;

				if ( ThornsItemRegistry.IsHarvestToolViewModelPath( loadedModelPath )
				     && !string.IsNullOrWhiteSpace( activeItemId )
				     && ThornsItemRegistry.TryGet( activeItemId.Trim(), out var weaponDef )
				     && ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in weaponDef ) )
					applyGunMeshScaleMul = false;
			}
		}

		if ( applyGunMeshScaleMul )
			itemScale *= FpWeaponMeshRootScaleMul;
	}

	static bool FpViewModelPathEquals( string a, string b )
	{
		if ( string.IsNullOrWhiteSpace( a ) || string.IsNullOrWhiteSpace( b ) )
			return false;
		var na = a.Trim().Replace( '\\', '/' );
		var nb = b.Trim().Replace( '\\', '/' );
		return string.Equals( na, nb, StringComparison.OrdinalIgnoreCase );
	}

	static GameObject FindNamedChild( GameObject root, string name )
	{
		if ( root is null || !root.IsValid() || string.IsNullOrEmpty( name ) )
			return default;

		foreach ( var c in root.Children )
		{
			if ( c.IsValid() && c.Name == name )
				return c;
		}

		return default;
	}

	/// <summary>Owner client: nudge FP axe/pick viewmodel through a short swing (harvest strike or tool melee).</summary>
	public static void TryTriggerHarvestToolSwingForLocalOwner( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionPawnRoot( pawnRoot ) )
			return;

		var view = FindNamedChild( pawnRoot, "View" );
		if ( !view.IsValid() )
			return;

		var vmc = view.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			return;

		vmc.TriggerHarvestToolSwing();
	}

	bool LocalOwnerEligibleForHarvestToolSwingPose()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return false;

		if ( string.IsNullOrWhiteSpace( _fpVmActiveModelPath )
		     || string.Equals( _fpVmActiveModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return false;

		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>();
		if ( !hb.IsValid() )
			return false;

		var id = hb.ClientMirrorActiveItemId?.Trim() ?? "";
		if ( string.IsNullOrEmpty( id )
		     || !ThornsItemRegistry.TryGet( id, out var def )
		     || def.ItemType != ThornsItemType.Tool
		     || def.HarvestToolKind == ThornsHarvestToolKind.None )
			return false;

		if ( string.IsNullOrWhiteSpace( def.ViewModelAsset ) )
			return false;

		return FpViewModelPathEquals( def.ViewModelAsset, _fpVmActiveModelPath );
	}

	bool LocalOwnerUsesHarvestAxeOrPickaxeFpPose()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return false;

		if ( string.IsNullOrWhiteSpace( _fpVmActiveModelPath )
		     || string.Equals( _fpVmActiveModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return false;

		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>();
		if ( !hb.IsValid() )
			return false;

		var id = hb.ClientMirrorActiveItemId?.Trim() ?? "";
		return !string.IsNullOrEmpty( id )
		       && ThornsItemRegistry.TryGet( id, out var def )
		       && ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in def )
		       && !string.IsNullOrWhiteSpace( def.ViewModelAsset )
		       && FpViewModelPathEquals( def.ViewModelAsset, _fpVmActiveModelPath );
	}

	float ResolveViewBobAmplitudeMulForActiveFpItem() =>
		LocalOwnerUsesHarvestAxeOrPickaxeFpPose()
			? MathF.Max( 0f, HarvestAxePickaxeViewBobAmplitudeMul )
			: 1f;

	/// <summary>Starts a one-shot swing if a harvest tool FP mesh is active (local owner).</summary>
	public void TriggerHarvestToolSwing()
	{
		if ( !Game.IsPlaying || !ThornsGameplayIsClientFxContext() )
			return;

		if ( !LocalOwnerEligibleForHarvestToolSwingPose() )
			return;

		_harvestToolSwingStartRealtime = Time.Now;
	}

	static float SmoothStep01( float t )
	{
		t = Math.Clamp( t, 0f, 1f );
		return t * t * (3f - 2f * t);
	}

	float ResolveHarvestToolSwingPitchSign()
	{
		var pawnRoot = GameObject.Parent;
		var hb = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsHotbarEquipment>() : default;
		var id = hb.IsValid() ? hb.ClientMirrorActiveItemId?.Trim() ?? "" : "";
		if ( !string.IsNullOrEmpty( id )
		     && ThornsItemRegistry.TryGet( id, out var def )
		     && def.HarvestToolKind == ThornsHarvestToolKind.Pickaxe )
			return 1f;

		return -1f;
	}

	float SampleHarvestToolSwingPitchDegrees()
	{
		if ( _harvestToolSwingStartRealtime < 0.0 )
			return 0f;

		var elapsed = (float)(Time.Now - _harvestToolSwingStartRealtime);
		var half = Math.Max( 0.02f, HarvestToolSwingHalfSeconds );
		var total = half * 2f;
		var peak = ResolveHarvestToolSwingPitchSign() * HarvestToolSwingPitchDegrees;

		if ( elapsed >= total )
		{
			_harvestToolSwingStartRealtime = -1.0;
			return 0f;
		}

		if ( elapsed < half )
			return peak * SmoothStep01( elapsed / half );

		return peak * (1f - SmoothStep01( (elapsed - half) / half ));
	}

	/// <summary>
	/// Stock FP arms mesh (Facepunch) — <see href="https://sbox.game/facepunch/v_first_person_arms_human">v_first_person_arms_human</see>.
	/// Shown alone when no hotbar item has a first-person weapon/tool/consumable mesh to draw.
	/// </summary>
	public void PresentEmptyFirstPersonHands()
	{
		ClearViewModel();

		var pawnRoot = GameObject.Parent;
		var hp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsHealth>() : default;
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
		{
			FpVmDiag( "PresentEmptyHands: skip (dead)" );
			return;
		}

		var isLocal = ThornsPawn.IsLocalConnectionOwner( this );
		if ( !isLocal )
		{
			FpVmDiag( "PresentEmptyHands: skip (not local owner)" );
			return;
		}

		if ( !Game.IsPlaying )
		{
			FpVmDiag( "PresentEmptyHands: skip (!Game.IsPlaying)" );
			return;
		}

		if ( !UseFirstPersonArmsHuman )
		{
			FpVmDiag( "PresentEmptyHands: skip (UseFirstPersonArmsHuman=false)" );
			return;
		}

		if ( !ThornsGameplayIsClientFxContext() )
		{
			FpVmDiag(
				$"PresentEmptyHands: skip (!clientFxCtx) playing={Game.IsPlaying} dedicated={Application.IsDedicatedServer} headless={Application.IsHeadless}" );
			return;
		}

		if ( !Components.Get<ThornsPawnCamera>().IsValid() || !CameraExplicit.IsValid() )
		{
			FpVmDiag(
				$"PresentEmptyHands: defer (camera not ready) pawnCam={Components.Get<ThornsPawnCamera>().IsValid()} camExplicit={CameraExplicit.IsValid()}" );
			_ = PresentEmptyFirstPersonHandsWhenCameraReadyAsync();
			return;
		}

		FpVmDiag( "PresentEmptyHands: core (idle arms path)" );
		PresentEmptyFirstPersonHandsCore();
	}

	async Task PresentEmptyFirstPersonHandsWhenCameraReadyAsync()
	{
		for ( var attempt = 0; attempt < 40; attempt++ )
		{
			await Task.DelayRealtimeSeconds( 0.05f );
			if ( !GameObject.IsValid() || !Game.IsPlaying )
				return;
			if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
				return;
			if ( !ThornsGameplayIsClientFxContext() )
				return;
			if ( Components.Get<ThornsPawnCamera>().IsValid() && CameraExplicit.IsValid() )
			{
				PresentEmptyFirstPersonHands();
				return;
			}
		}
	}

	void PresentEmptyFirstPersonHandsCore()
	{
		var mdl = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback( FirstPersonArmsHumanPath, "FP idle arms", out _ );
		if ( !mdl.IsValid() || mdl.IsError )
		{
			FpVmDiag(
				$"PresentEmptyHandsCore: arms model unusable path='{FirstPersonArmsHumanPath}' valid={mdl.IsValid()} error={mdl.IsError}" );
			return;
		}

		_fpWeaponDrawingEnabled = true;

		_viewmodel = new GameObject( true, "WeaponViewmodel" );
		_viewmodel.NetworkMode = NetworkMode.Never;
		_viewmodel.SetParent( GameObject );
		_fpVmActiveModelPath = FirstPersonArmsHumanPath;
		var pawnRootVm = GameObject.Parent;
		var weaponVm = pawnRootVm.IsValid() ? pawnRootVm.Components.Get<ThornsWeapon>() : default;
		ResolveFpMeshRootPose( weaponVm, FirstPersonArmsHumanPath, out var itemOffset, out var itemScale, out var itemEuler );
		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset + itemOffset;
		var r = ViewModelGripLocalEulerDegrees;
		_viewmodel.LocalRotation = Rotation.From( r.x + itemEuler.x, r.y + itemEuler.y, r.z + itemEuler.z );
		_viewmodel.LocalScale = itemScale;

		var vmr = _viewmodel.Components.Create<SkinnedModelRenderer>();
		vmr.Model = mdl;
		vmr.Tint = new Color( 0.94f, 0.94f, 0.94f, 1f );
		vmr.RenderType = ModelRenderer.ShadowRenderType.Off;
		vmr.Enabled = _fpWeaponDrawingEnabled;
		vmr.UseAnimGraph = true;

		vmr.RenderOptions.Game = true;
		vmr.RenderOptions.Overlay = ViewModelUseOverlayPass;

		var anim = _viewmodel.Components.Create<ThornsViewModelFpAnimator>();
		anim.DeploySequenceName = IdleArmsDeploySequenceName;
		anim.IdleSequenceName = IdleArmsLoopSequenceName;
		anim.LoopIdleWithDirectPlayback = !string.IsNullOrWhiteSpace( IdleArmsLoopSequenceName );
		anim.DeployDurationFallbackSeconds = IdleArmsDeployDurationFallbackSeconds;
		anim.PreferGraphParametersFirst = false;
		anim.UseGraphIronsightsParameterForAds = false;
		anim.MeleeLightAttackSequenceName = IdleArmsMeleeLightSequenceName;
		anim.MeleeLightAttackAlternateSequenceName = IdleArmsMeleeLightAlternateSequenceName;
		anim.BareHandsLocomotionBlendEnabled = true;
		anim.BareHandsLocomotionMoveSequenceName = IdleArmsMoveSequenceName;

		anim.BindAndRunEquipRoutine( vmr, mdl );

		SyncFpWeaponDrawWithBuildingController();
		_ = ApplyIdleHandsRenderOptionsWhenSceneReadyAsync( vmr );

		RememberDrivenViewmodelTransform();

		FpVmDiag( "PresentEmptyHandsCore: WeaponViewmodel spawned (idle arms)" );
	}

	async Task ApplyIdleHandsRenderOptionsWhenSceneReadyAsync( SkinnedModelRenderer vmr )
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !vmr.IsValid() || !GameObject.IsValid() || !_viewmodel.IsValid() )
			return;

		vmr.RenderOptions.Game = true;
		vmr.RenderOptions.Overlay = ViewModelUseOverlayPass;
		if ( vmr.SceneObject.IsValid() )
			vmr.RenderOptions.Apply( vmr.SceneObject );

		if ( _viewmodel.IsValid() && _viewmodel.Components.Get<ThornsViewModelFpAnimator>().IsValid() )
			vmr.UseAnimGraph = true;

		ApplyFpWeaponDrawingPreferenceToRenderers();
	}

	public void SpawnViewModel( string modelPath )
	{
		ClearViewModel();

		var isLocal = ThornsPawn.IsLocalConnectionOwner( this );

		if ( !isLocal )
		{
			FpVmDiag( $"SpawnViewModel: skip (not local owner) path='{modelPath}'" );
			return;
		}

		if ( !Game.IsPlaying )
		{
			FpVmDiag( $"SpawnViewModel: skip (!Game.IsPlaying) path='{modelPath}'" );
			return;
		}

		if ( !ThornsGameplayIsClientFxContext() )
		{
			FpVmDiag(
				$"SpawnViewModel: skip (!clientFxCtx) path='{modelPath}' dedicated={Application.IsDedicatedServer} headless={Application.IsHeadless}" );
			return;
		}

		if ( !Components.Get<ThornsPawnCamera>().IsValid() || !CameraExplicit.IsValid() )
		{
			FpVmDiag(
				$"SpawnViewModel: defer (camera) path='{modelPath}' pawnCam={Components.Get<ThornsPawnCamera>().IsValid()} camExplicit={CameraExplicit.IsValid()}" );
			_ = SpawnViewModelWhenCameraReadyAsync( modelPath );
			return;
		}

		FpVmDiag( $"SpawnViewModel: core path='{modelPath}'" );
		SpawnViewModelCore( modelPath );
	}

	async Task SpawnViewModelWhenCameraReadyAsync( string modelPath )
	{
		for ( var attempt = 0; attempt < 40; attempt++ )
		{
			await Task.DelayRealtimeSeconds( 0.05f );
			if ( !GameObject.IsValid() || !Game.IsPlaying )
				return;
			if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
				return;
			if ( !ThornsGameplayIsClientFxContext() )
				return;
			if ( Components.Get<ThornsPawnCamera>().IsValid() && CameraExplicit.IsValid() )
			{
				SpawnViewModel( modelPath );
				return;
			}
		}

	}

	void SpawnViewModelCore( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
		{
			FpVmDiag( "SpawnViewModelCore: abort (empty modelPath)" );
			return;
		}

		_fpWeaponDrawingEnabled = true;

		var mdl = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback( modelPath, "FP viewmodel", out var usedFallbackGeometry );
		if ( usedFallbackGeometry
		     && !string.IsNullOrWhiteSpace( modelPath )
		     && modelPath.Contains( "models/tools", StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Warning(
				$"[Thorns] FP viewmodel for '{modelPath}' fell back to dev geometry — the compiled .vmdl is missing or failed to build. Fix ModelDoc/materials (avoid Tripo absolute paths), then recompile assets." );
		}

		if ( !mdl.IsValid() || mdl.IsError )
		{
			FpVmDiag(
				$"SpawnViewModelCore: model unusable after load path='{modelPath}' valid={mdl.IsValid()} error={mdl.IsError} usedFallback={usedFallbackGeometry}" );
			return;
		}

		// bool = start active — `false` leaves the object disabled so nothing draws in-game (still visible in hierarchy / editor preview).
		_viewmodel = new GameObject( true, "WeaponViewmodel" );
		// Local-only FP presentation object: never replicate to other clients.
		_viewmodel.NetworkMode = NetworkMode.Never;
		_viewmodel.SetParent( GameObject );
		_fpVmActiveModelPath = modelPath;
		var pawnRootVm = GameObject.Parent;
		var weaponVm = pawnRootVm.IsValid() ? pawnRootVm.Components.Get<ThornsWeapon>() : default;
		ResolveFpMeshRootPose( weaponVm, modelPath, out var itemOffset, out var itemScale, out var itemEuler );
		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset + itemOffset;
		var g = ViewModelGripLocalEulerDegrees;
		_viewmodel.LocalRotation = Rotation.From( g.x + itemEuler.x, g.y + itemEuler.y, g.z + itemEuler.z );
		_viewmodel.LocalScale = itemScale;

		// FP weapon assets (v_m4a1 etc.) are skinned rigs — ModelRenderer previews in inspector but SkinnedModelRenderer drives bones in-game.
		var vmr = _viewmodel.Components.Create<SkinnedModelRenderer>();
		vmr.Model = mdl;
		vmr.Tint = new Color( 0.94f, 0.94f, 0.94f, 1f );

		vmr.RenderType = ModelRenderer.ShadowRenderType.Off;
		vmr.Enabled = _fpWeaponDrawingEnabled;
		vmr.UseAnimGraph = ViewModelUseAnimGraph;

		vmr.RenderOptions.Game = true;
		vmr.RenderOptions.Overlay = ViewModelUseOverlayPass;

		if ( ViewModelUseAnimGraph && UsesStockFpAnimatorSequences( modelPath ) && !usedFallbackGeometry )
		{
			var anim = _viewmodel.Components.Create<ThornsViewModelFpAnimator>();
			ConfigureAnimatorPresetForModel( anim, modelPath );
			TryPlayFpGunDeploySoundForModel( modelPath );
			anim.BindAndRunEquipRoutine( vmr, mdl );
			_ = TryCreateFirstPersonArmsRenderer( vmr );
		}
		else
		{
			vmr.UseAnimGraph = false;
			if ( UsesStockFpAnimatorSequences( modelPath ) )
				_ = TryCreateFirstPersonArmsRenderer( vmr );
		}

		SyncFpWeaponDrawWithBuildingController();

		_ = ApplyViewmodelRenderOptionsWhenSceneReadyAsync( vmr );

		RememberDrivenViewmodelTransform();

		FpVmDiag(
			$"SpawnViewModelCore: WeaponViewmodel spawned path='{modelPath}' fallbackGeo={usedFallbackGeometry} overlay={ViewModelUseOverlayPass}" );
	}

	void SyncFpWeaponDrawWithBuildingController()
	{
		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return;

		var weapon = pawnRoot.Components.Get<ThornsWeapon>();
		var build = pawnRoot.Components.Get<ThornsBuildingController>();
		if ( !weapon.IsValid() )
			return;

		weapon.ApplyLocalFpWeaponDrawForBuildMode( build.IsValid() && build.BuildModeActive );
	}

	protected override void OnUpdate()
	{
		if ( !_viewmodel.IsValid() || !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		TickViewModelAdsOffsetLerp();
		TryLatchHierarchyViewmodelEdit();
		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		ApplyViewModelTransformFromInspector();
	}

	protected override void OnValidate()
	{
		if ( GameObject.Scene is null || !GameObject.Scene.IsValid() )
			return;

		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		ApplyViewModelTransformFromInspector();
	}

	void TickViewModelAdsOffsetLerp()
	{
		var pawnRoot = GameObject.Parent;
		var equipWeapon = pawnRoot.IsValid()
			? pawnRoot.Components.Get<ThornsWeapon>()
			: default;
		var combatId = equipWeapon.IsValid() ? equipWeapon.ClientMirrorCombatDefinitionId ?? "" : "";
		var meleeEquipped = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( ThornsWeaponDefinitions.Get( combatId ), combatId );
		var fpAllowsAds = !equipWeapon.IsValid() || equipWeapon.ClientMirrorFpPresentationAllowsCombatLayers();
		var adsHeld = !meleeEquipped && fpAllowsAds && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var target = adsHeld ? new Vector3( ViewModelAdsForwardOffset, 0f, 0f ) : Vector3.Zero;
		var t = Math.Clamp( Time.Delta * ViewModelAdsOffsetLerpSpeed, 0f, 1f );
		_adsOffsetCurrent = Vector3.Lerp( _adsOffsetCurrent, target, t );
	}

	/// <summary>Writes grip + item pose onto the child <c>WeaponViewmodel</c> (called every frame and from <see cref="OnValidate"/>).</summary>
	public void ApplyViewModelTransformFromInspector()
	{
		if ( !_viewmodel.IsValid() )
			return;

		if ( Game.IsPlaying && !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		var pawnRoot = GameObject.Parent;
		var equipWeapon = pawnRoot.IsValid()
			? pawnRoot.Components.Get<ThornsWeapon>()
			: default;

		ResolveFpMeshRootPose( equipWeapon, _fpVmActiveModelPath, out var itemOff, out var itemSc, out var itemEu );
		var gripE = ViewModelGripLocalEulerDegrees;
		var swingPitch = SampleHarvestToolSwingPitchDegrees();
		var bob = Vector3.Zero;
		if ( pawnRoot.IsValid() )
		{
			var move = pawnRoot.Components.Get<ThornsPawnMovement>();
			if ( move.IsValid() )
				bob = move.GetViewBobOffsetWithPitch( move.LookAngles.pitch ) * ResolveViewBobAmplitudeMulForActiveFpItem();
		}

		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset + itemOff + _adsOffsetCurrent + bob;
		_viewmodel.LocalRotation = Rotation.From( gripE.x + itemEu.x + swingPitch, gripE.y + itemEu.y, gripE.z + itemEu.z );
		_viewmodel.LocalScale = itemSc;
		RememberDrivenViewmodelTransform();
	}

	void RememberDrivenViewmodelTransform()
	{
		if ( !_viewmodel.IsValid() )
			return;

		_lastDrivenViewmodelLocalPosition = _viewmodel.LocalPosition;
		_lastDrivenViewmodelLocalRotation = _viewmodel.LocalRotation;
		_lastDrivenViewmodelLocalScale = _viewmodel.LocalScale;
		_hasLastDrivenViewmodelTransform = true;
		_skipHierarchyEditLatchOnce = true;
	}

	void ResetDrivenViewmodelTransformTracking()
	{
		_hasLastDrivenViewmodelTransform = false;
		_skipHierarchyEditLatchOnce = false;
	}

	void TryLatchHierarchyViewmodelEdit()
	{
		if ( !_viewmodel.IsValid() || PauseAutomaticWeaponViewmodelTransform )
			return;

		if ( _skipHierarchyEditLatchOnce )
		{
			_skipHierarchyEditLatchOnce = false;
			return;
		}

		if ( !_hasLastDrivenViewmodelTransform )
			return;

		if ( MathF.Abs( SampleHarvestToolSwingPitchDegrees() ) > 0.35f )
			return;

		var posDrift = (_viewmodel.LocalPosition - _lastDrivenViewmodelLocalPosition).Length;
		var scaleDrift = (_viewmodel.LocalScale - _lastDrivenViewmodelLocalScale).Length;
		var curAngles = _viewmodel.LocalRotation.Angles();
		var lastAngles = _lastDrivenViewmodelLocalRotation.Angles();
		var rotDrift = MathF.Max(
			MathF.Abs( curAngles.pitch - lastAngles.pitch ),
			MathF.Max( MathF.Abs( curAngles.yaw - lastAngles.yaw ), MathF.Abs( curAngles.roll - lastAngles.roll ) ) );

		if ( posDrift < 0.08f && scaleDrift < 0.03f && rotDrift < 0.75f )
			return;

		SyncHierarchyViewmodelTransformToInspectorFields();
		PauseAutomaticWeaponViewmodelTransform = true;
	}

	void SyncHierarchyViewmodelTransformToInspectorFields()
	{
		if ( !_viewmodel.IsValid() )
			return;

		var swingPitch = SampleHarvestToolSwingPitchDegrees();
		var gripE = ViewModelGripLocalEulerDegrees;
		var itemPos = _viewmodel.LocalPosition - ViewModelGripLocalPosition - ViewModelPresentationLocalOffset - _adsOffsetCurrent;
		var angles = _viewmodel.LocalRotation.Angles();
		var itemEuler = new Vector3(
			angles.pitch - gripE.x - swingPitch,
			angles.yaw - gripE.y,
			angles.roll - gripE.z );

		if ( string.Equals( _fpVmActiveModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
		{
			IdleArmsOnlyRootLocalOffset = itemPos;
			IdleArmsOnlyRootLocalEulerDegrees = itemEuler;
			IdleArmsOnlyRootLocalScale = _viewmodel.LocalScale;
			return;
		}

		var pawnRoot = GameObject.Parent;
		var weapon = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsWeapon>() : default;
		if ( !weapon.IsValid() )
			return;

		weapon.ViewModelLocalPosition = itemPos;
		weapon.ViewModelLocalEulerDegrees = itemEuler;
		weapon.ViewModelLocalScale = _viewmodel.LocalScale;
	}

	/// <summary>Clears hierarchy lock and re-applies pose from <see cref="ThornsWeapon"/> / grip fields.</summary>
	public void ResumeAutomaticViewmodelTransform()
	{
		PauseAutomaticWeaponViewmodelTransform = false;
		ApplyViewModelTransformFromInspector();
	}

	SkinnedModelRenderer TryCreateFirstPersonArmsRenderer( SkinnedModelRenderer weaponSkin )
	{
		if ( !UseFirstPersonArmsHuman || !_viewmodel.IsValid() || !weaponSkin.IsValid() )
			return default;

		var armsModel = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback( FirstPersonArmsHumanPath, "FP arms", out var usedFallbackGeometry );
		if ( !armsModel.IsValid() || armsModel.IsError || usedFallbackGeometry )
		{
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
		arms.Enabled = _fpWeaponDrawingEnabled;

		if ( arms.SceneObject.IsValid() )
			arms.RenderOptions.Apply( arms.SceneObject );

		return arms;
	}

	/// <summary>Same cue as YouAreNotAlone — long-gun / SMG / shotgun / sniper FP equip.</summary>
	void TryPlayFpGunDeploySoundForModel( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( ThornsWeapon.GunDeploySoundResource ) )
			return;

		if ( !string.Equals( modelPath, M4FirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, Mp5FirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, SniperFirstPersonViewmodelPath, StringComparison.Ordinal ) )
			return;

		var pawnRoot = GameObject.Parent;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var path = ThornsWeapon.GunDeploySoundResource.Trim();
		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( pawnRoot, out var ear, out _ ) )
			Sound.Play( path, ear );
		else
			Sound.Play( path, pawnRoot.WorldPosition + Vector3.Up * 40f );
	}

	static void ConfigureAnimatorPresetForModel( ThornsViewModelFpAnimator anim, string modelPath )
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
		if ( _viewmodel.IsValid() && _viewmodel.Components.Get<ThornsViewModelFpAnimator>().IsValid() )
			vmr.UseAnimGraph = true;

		ApplyFpWeaponDrawingPreferenceToRenderers();

	}

	public void ClearViewModel()
	{
		if ( _viewmodel.IsValid() )
		{
			_viewmodel.Destroy();
			_viewmodel = default;
			_adsOffsetCurrent = Vector3.Zero;
		}

		_fpVmActiveModelPath = "";
		_harvestToolSwingStartRealtime = -1.0;
		PauseAutomaticWeaponViewmodelTransform = false;
		ResetDrivenViewmodelTransformTracking();
	}

	void ApplyFpWeaponDrawingPreferenceToRenderers()
	{
		if ( !_viewmodel.IsValid() )
			return;

		foreach ( var smr in _viewmodel.Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( smr.IsValid() )
				smr.Enabled = _fpWeaponDrawingEnabled;
		}
	}

	/// <summary>
	/// Toggle SkinnedModelRenderer draws without destroying the viewmodel (e.g. build toolbar hides FP weapon until closed).
	/// </summary>
	public void SetFirstPersonWeaponDrawingEnabled( bool enabled )
	{
		_fpWeaponDrawingEnabled = enabled;
		ApplyFpWeaponDrawingPreferenceToRenderers();
	}

	protected override void OnDestroy()
	{
		if ( _viewmodel.IsValid() )
			_viewmodel.Destroy();
		_fpVmActiveModelPath = "";
		_harvestToolSwingStartRealtime = -1.0;
	}

	static bool ThornsGameplayIsClientFxContext()
	{
		return Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
	}
}
