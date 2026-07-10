using System;
using System.Threading.Tasks;
using Sandbox.Diagnostics;
using Terraingen;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using ThornsItemRegistry = Terraingen.GameData.ThornsItemRegistry;
using ThornsItemType = Terraingen.GameData.ThornsItemType;
using ThornsHarvestToolKind = Terraingen.GameData.ThornsHarvestToolKind;

namespace Sandbox;

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

	public const string UspFirstPersonViewmodelPath = "models/weapons/sbox_pistol_usp/v_usp.vmdl";

	public const string UspWorldModelPath = "models/weapons/sbox_pistol_usp/w_usp.vmdl";

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

	// View-local +X: ADS slide; same axis as ThornsFpPresentation.ViewModelLocalPosition for tool/medkit forward bias.
	[Property]
	public float ViewModelAdsForwardOffset { get; set; } = 0f;

	[Property]
	public float ViewModelAdsOffsetLerpSpeed { get; set; } = 32f;

	[Property]
	public bool ViewModelUseAnimGraph { get; set; } = true;

	[Property]
	public bool ViewModelUseOverlayPass { get; set; } = true;

	[Property]
	public bool UseFirstPersonArmsHuman { get; set; } = true;

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalOffset { get; set; } = new Vector3( 50f, 0f, -15f );

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalEulerDegrees { get; set; } = Vector3.Zero;

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public Vector3 IdleArmsOnlyRootLocalScale { get; set; } = new Vector3( 4f, 4f, 4f );

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public string IdleArmsDeploySequenceName { get; set; } = "Punching_Raise";

	[Property, Category( "Thorns/Idle arms (no weapon)" )]
	public float IdleArmsDeployDurationFallbackSeconds { get; set; } = 0.7f;

	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestToolSwingPitchDegrees { get; set; } = 20f;

	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestToolSwingHalfSeconds { get; set; } = 0.1f;

	[Property, Category( "Thorns/Harvest tool swing (FP)" )]
	public float HarvestAxePickaxeViewBobAmplitudeMul { get; set; } = 0.1f;

	[Property, Category( "Thorns/Bow viewmodel (FP)" )]
	public Vector3 BowViewmodelLocalOffset { get; set; } = ThornsFpItemHelpers.FpBowViewmodelRootOffset;

	[Property, Category( "Thorns/Bow viewmodel (FP)" )]
	public Vector3 BowViewmodelLocalEulerDegrees { get; set; } = ThornsFpItemHelpers.FpBowViewmodelRootEulerDegrees;

	[Property, Category( "Thorns/Bow viewmodel (FP)" )]
	public Vector3 BowViewmodelLocalScale { get; set; } = ThornsFpItemHelpers.FpBowViewmodelRootScale;

	[Property, Category( "Thorns/Bow viewmodel (FP)" )]
	public bool BowApplyStockTenTimesFpScale { get; set; }

	[Property, Category( "Thorns/FP viewmodel (debug tuning)" )]
	public bool LiveInspectorPreview { get; set; } = true;

	[Property, Category( "Thorns/FP viewmodel (debug tuning)" )]
	public bool PauseAutomaticWeaponViewmodelTransform { get; set; }

	GameObject _viewmodel;
	Vector3 _adsOffsetCurrent;
	float _viewKickPitch;
	float _viewKickYaw;
	float _viewKickRoll;
	Vector3 _sightEyeViewmodelOffset;
	float _sightAdsForwardOffset;
	float _sightAdsForwardBlend;
	bool _viewModelVisuallyHidden;
	SkinnedModelRenderer _weaponSkin;
	ThornsViewModelFpAnimator _animator;
	ThornsViewModelAttachmentMount _attachmentMount;
	string _activeCombatWeaponId = "";
	Vector3 _lastDrivenViewmodelLocalPosition;
	Rotation _lastDrivenViewmodelLocalRotation;
	Vector3 _lastDrivenViewmodelLocalScale;
	bool _hasLastDrivenViewmodelTransform;
	bool _skipHierarchyEditLatchOnce;

	/// <summary>Last spawned FP model path on <c>_viewmodel</c> — drives per-item offset rules vs stock Facepunch rigs.</summary>
	string _fpVmActiveModelPath = "";

	/// <summary>Homegrown bow prop — tuned via inspector Bow fields / <see cref="LogBowViewmodelPose"/>.</summary>
	bool _fpBowUsesStockPlaceholderMesh;

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

	CameraComponent CameraExplicit => Components.Get<CameraComponent>();

	bool IsPresentationCameraReady()
	{
		var scene = Scene;
		if ( scene is not null && scene.IsValid && scene.Camera.IsValid() && scene.Camera.GameObject == GameObject )
			return true;

		var cam = CameraExplicit;
		if ( cam is null || !cam.IsValid() )
			return false;

		return cam.Enabled || cam.IsMainCamera;
	}

	/// <summary>Stock FP weapons that share <see cref="ThornsViewModelFpAnimator"/> sequence names (deploy / idle / ADS / reload).</summary>
	public static bool UsesStockFpAnimatorSequences( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return false;

		var p = modelPath.Trim().Replace( '\\', '/' );
		return string.Equals( p, M4FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, Mp5FirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
		       || string.Equals( p, UspFirstPersonViewmodelPath, StringComparison.OrdinalIgnoreCase )
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
		       || string.Equals( combatWeaponDefinitionId, "usp", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "shotgun", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "sniper", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatWeaponDefinitionId, "m9_bayonet", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>
	/// Idle arms + Facepunch stock FP rigs keep root at grip pose. Custom tool / held-consumable meshes use
	/// <see cref="ThornsItemRegistry"/> for the active hotbar item when possible (canonical at startup), then
	/// <see cref="ThornsFpPresentation"/> mirror fields. View-local +X is toward the world in front of the camera (ADS slide uses the same axis).
	/// </summary>
	void ResolveFpMeshRootPose( ThornsFpPresentation weapon, string loadedModelPath, out Vector3 itemOffset, out Vector3 itemScale, out Vector3 itemEulerDegrees )
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
		else if ( ThornsWeaponResourceLoad.IsBowModelPath( loadedModelPath ) )
		{
			itemOffset = BowViewmodelLocalOffset;
			itemEulerDegrees = BowViewmodelLocalEulerDegrees;
			itemScale = BowViewmodelLocalScale;
			applyGunMeshScaleMul = false;
		}
		else if ( UsesStockFpAnimatorSequences( loadedModelPath ) )
		{
			itemScale = weapon.IsValid()
				? ThornsItemRegistry.ResolveFpViewmodelRootScale( weapon.ViewModelLocalScale )
				: Vector3.One;
		}
		else if ( ThornsItemRegistry.TryResolveFpViewmodelPoseForModelPath(
			         loadedModelPath,
			         out itemOffset,
			         out itemScale,
			         out itemEulerDegrees ) )
		{
			if ( ThornsItemRegistry.UsesDirectFpViewmodelScale( loadedModelPath ) )
				applyGunMeshScaleMul = false;
			else if ( ThornsItemRegistry.IsHarvestToolViewModelPath( loadedModelPath ) )
			{
				var pawnRoot = GameObject.Parent;
				var fp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsFpPresentation>() : default;
				var activeItemId = fp.IsValid() ? fp.ClientMirrorActiveItemId : "";
				if ( !string.IsNullOrWhiteSpace( activeItemId )
				     && ThornsItemRegistry.TryGet( activeItemId.Trim(), out var weaponDef )
				     && ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in weaponDef ) )
					applyGunMeshScaleMul = false;
			}
		}
		else if ( LiveInspectorPreview && weapon.IsValid() )
		{
			itemOffset = weapon.ViewModelLocalPosition;
			itemScale = ThornsItemRegistry.ResolveFpViewmodelRootScale( weapon.ViewModelLocalScale );
			itemEulerDegrees = weapon.ViewModelLocalEulerDegrees;
			if ( ThornsItemRegistry.IsHarvestToolViewModelPath( loadedModelPath ) )
			{
				var pawnRoot = GameObject.Parent;
				var fp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsFpPresentation>() : default;
				var activeItemId = fp.IsValid() ? fp.ClientMirrorActiveItemId : "";
				if ( !string.IsNullOrWhiteSpace( activeItemId )
				     && ThornsItemRegistry.TryGet( activeItemId.Trim(), out var weaponDef )
				     && ThornsItemRegistry.UsesHarvestAxeOrPickaxeFpPose( in weaponDef ) )
					applyGunMeshScaleMul = false;
			}
		}
		else
		{
			var pawnRoot = GameObject.Parent;
			var fp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsFpPresentation>() : default;
			var activeItemId = fp.IsValid() ? fp.ClientMirrorActiveItemId : "";
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

	public static string ResolveStockCombatViewModelPath( string combatWeaponDefinitionId, string viewModelAsset )
	{
		var vmPath = viewModelAsset ?? "";
		if ( string.Equals( combatWeaponDefinitionId, "m4", StringComparison.OrdinalIgnoreCase ) )
			return M4FirstPersonViewmodelPath;
		if ( string.Equals( combatWeaponDefinitionId, "mp5", StringComparison.OrdinalIgnoreCase ) )
			return Mp5FirstPersonViewmodelPath;
		if ( string.Equals( combatWeaponDefinitionId, "usp", StringComparison.OrdinalIgnoreCase ) )
			return UspFirstPersonViewmodelPath;
		if ( string.Equals( combatWeaponDefinitionId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			return ShotgunFirstPersonViewmodelPath;
		if ( string.Equals( combatWeaponDefinitionId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			return SniperFirstPersonViewmodelPath;
		if ( string.Equals( combatWeaponDefinitionId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			return BayonetM9FirstPersonViewmodelPath;
		return vmPath;
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

	/// <summary>Owner client: play FP attack presentation for the current hotbar item (swing / punch / knife).</summary>
	public static void TryPlayOwnerAttackPresentation( GameObject pawnRoot, string activeItemId, string combatId )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionPlayerRoot( pawnRoot ) )
			return;

		if ( ShouldUseHarvestToolSwingPresentation( activeItemId ) )
		{
			TryTriggerHarvestToolSwingForLocalOwner( pawnRoot );
			return;
		}

		if ( !ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId ) )
			return;

		var attackConnected = true;
		if ( ThornsFpToolCombat.IsPunchCombatId( combatId ) )
			TryResolveLocalPunchPresentationHit( pawnRoot, out attackConnected );

		TryPlayOwnerMeleeLightAttackPresentation( pawnRoot, attackConnected );
	}

	/// <summary>Client-only aim trace for FP punch hit/miss graph variation (<c>b_attack_has_hit</c>).</summary>
	static bool TryResolveLocalPunchPresentationHit( GameObject pawnRoot, out bool attackConnected )
	{
		attackConnected = false;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		if ( !ThornsSceneObserver.TryResolveLocalAimRay( pawnRoot, out var origin, out var direction, useScreenCenter: true ) )
			return false;

		direction = direction.Normal;
		if ( direction.Length < 0.95f )
			return false;

		if ( ThornsGatherSalvage.TryResolveTarget( pawnRoot, origin, direction, out var salvageKind, out _ )
		     && salvageKind != ThornsGatherSalvage.SalvageTargetKind.None )
		{
			attackConnected = true;
			return true;
		}

		var wdef = ThornsWeaponDefinitions.Get( ThornsFpToolCombat.CombatIdBareHands );
		var range = wdef?.MaxRange ?? 12000f;
		if ( ThornsCombatHitResolver.TryResolveVictimAlongRay( pawnRoot.Scene, origin, direction, range, pawnRoot, out _, out _ ) )
		{
			attackConnected = true;
			return true;
		}

		return true;
	}

	static bool ShouldUseHarvestToolSwingPresentation( string activeItemId )
	{
		if ( string.IsNullOrWhiteSpace( activeItemId ) )
			return false;

		return ThornsItemRegistry.TryGet( activeItemId.Trim(), out var def )
		       && def.HarvestToolKind is ThornsHarvestToolKind.Axe or ThornsHarvestToolKind.Pickaxe;
	}

	/// <summary>Owner client: FP melee presentation on idle arms, bayonet, etc.</summary>
	public static void TryPlayOwnerMeleeLightAttackPresentation( GameObject pawnRoot, bool attackConnected = true )
	{
		if ( !TryResolveOwnerFpAnimator( pawnRoot, out var anim ) )
			return;

		anim.OwnerNotifyMeleeAttackCommitted( heavy: false, attackConnected: attackConnected );
	}

	/// <summary>Owner client: Facepunch FP grab gesture for Use (E) world interactions.</summary>
	public static void TryPlayOwnerUseGrabPresentation( GameObject pawnRoot, ThornsFpGrabAction action )
	{
		if ( !TryResolveOwnerFpAnimator( pawnRoot, out var anim ) )
			return;

		anim.OwnerNotifyGrabInteraction( action );
	}

	/// <summary>Owner client: hold-use grab stance while charging tame, mount, drink, etc.</summary>
	public static void TrySetOwnerGrabStance( GameObject pawnRoot, bool active )
	{
		if ( !TryResolveOwnerFpAnimator( pawnRoot, out var anim ) )
			return;

		anim.OwnerSetGrabStance( active );
	}

	static bool TryResolveOwnerFpAnimator( GameObject pawnRoot, out ThornsViewModelFpAnimator anim )
	{
		anim = default;
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return false;

		if ( !ThornsLocalPlayer.IsLocalConnectionPlayerRoot( pawnRoot ) )
			return false;

		var view = Terraingen.Player.ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return false;

		var wm = FindNamedChild( view, "WeaponViewmodel" );
		if ( !wm.IsValid() )
			return false;

		anim = wm.Components.Get<ThornsViewModelFpAnimator>();
		return anim.IsValid();
	}

	/// <summary>Owner client: nudge FP axe/pick viewmodel through a short swing (harvest strike or tool melee).</summary>
	public static void TryTriggerHarvestToolSwingForLocalOwner( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionPlayerRoot( pawnRoot ) )
			return;

		var view = Terraingen.Player.ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return;

		var vmc = view.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			return;

		vmc.TriggerHarvestToolSwing();
	}

	bool LocalOwnerEligibleForHarvestToolSwingPose()
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return false;

		if ( string.IsNullOrWhiteSpace( _fpVmActiveModelPath )
		     || string.Equals( _fpVmActiveModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return false;

		var fp = pawnRoot.Components.Get<ThornsFpPresentation>();
		if ( !fp.IsValid() )
			return false;

		var id = fp.ClientMirrorActiveItemId?.Trim() ?? "";
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
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return false;

		if ( string.IsNullOrWhiteSpace( _fpVmActiveModelPath )
		     || string.Equals( _fpVmActiveModelPath, FirstPersonArmsHumanPath, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return false;

		var fp = pawnRoot.Components.Get<ThornsFpPresentation>();
		if ( !fp.IsValid() )
			return false;

		var id = fp.ClientMirrorActiveItemId?.Trim() ?? "";
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

	/// <summary>Starts or restarts a one-shot swing if a harvest tool FP mesh is active (local owner).</summary>
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
		var fp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsFpPresentation>() : default;
		var id = fp.IsValid() ? fp.ClientMirrorActiveItemId?.Trim() ?? "" : "";
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
	public bool HasActiveViewModel => _viewmodel.IsValid();

	public SkinnedModelRenderer WeaponSkin => _weaponSkin;
	public ThornsViewModelFpAnimator Animator => _animator;
	public ThornsViewModelAttachmentMount AttachmentMount => _attachmentMount;
	public float AdsBlend01 => _animator?.IronsightsBlend01 ?? 0f;

	public bool IsPresentingModelPath( string modelPath ) =>
		_viewmodel.IsValid()
		&& !string.IsNullOrWhiteSpace( modelPath )
		&& FpViewModelPathEquals( _fpVmActiveModelPath, modelPath );

	public bool IsPresentingIdleArms() =>
		IsPresentingModelPath( FirstPersonArmsHumanPath );

	public void PresentEmptyFirstPersonHands()
	{
		if ( IsPresentingIdleArms() )
		{
			FpVmDiag( "PresentEmptyHands: skip (already idle arms)" );
			return;
		}

		ClearViewModel();

		var pawnRoot = GameObject.Parent;
		var hp = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsPlayerHealth>() : default;
		if ( hp.IsValid() && !hp.IsAlive )
		{
			FpVmDiag( "PresentEmptyHands: skip (dead)" );
			return;
		}

		var isLocal = ThornsLocalPlayer.IsLocalConnectionOwner( this );
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

		if ( !IsPresentationCameraReady() )
		{
			FpVmDiag(
				$"PresentEmptyHands: defer (camera not ready) rig='{GameObject.Name}' cam={( CameraExplicit.IsValid() ? CameraExplicit.Enabled : false )} main={( CameraExplicit.IsValid() && CameraExplicit.IsMainCamera )}" );
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
			if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
				return;
			if ( !ThornsGameplayIsClientFxContext() )
				return;
			if ( IsPresentationCameraReady() )
			{
				PresentEmptyFirstPersonHands();
				return;
			}
		}

		FpVmDiag( "PresentEmptyHands: gave up waiting for camera (40 attempts)" );
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
		var weaponVm = pawnRootVm.IsValid() ? pawnRootVm.Components.Get<ThornsFpPresentation>() : default;
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
		anim.DeployDurationFallbackSeconds = IdleArmsDeployDurationFallbackSeconds;
		anim.LoopIdleWithDirectPlayback = false;
		anim.PreferGraphParametersFirst = false;
		anim.UseGraphIronsightsParameterForAds = false;
		anim.UseBareHandsPunchingGraph = true;

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
		if ( IsPresentingModelPath( modelPath ) )
		{
			FpVmDiag( $"SpawnViewModel: skip (already equipped) path='{modelPath}'" );
			ApplyViewModelTransformFromInspector();
			return;
		}

		ClearViewModel();

		var isLocal = ThornsLocalPlayer.IsLocalConnectionOwner( this );

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

		if ( !IsPresentationCameraReady() )
		{
			FpVmDiag(
				$"SpawnViewModel: defer (camera) path='{modelPath}' rig='{GameObject.Name}' cam={( CameraExplicit.IsValid() ? CameraExplicit.Enabled : false )} main={( CameraExplicit.IsValid() && CameraExplicit.IsMainCamera )}" );
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
			if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
				return;
			if ( !ThornsGameplayIsClientFxContext() )
				return;
			if ( IsPresentationCameraReady() )
			{
				SpawnViewModel( modelPath );
				return;
			}
		}

		FpVmDiag( $"SpawnViewModelWhenCameraReadyAsync: timeout path='{modelPath}'" );
	}

	void SpawnViewModelCore( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
		{
			FpVmDiag( "SpawnViewModelCore: abort (empty modelPath)" );
			return;
		}

		_fpWeaponDrawingEnabled = true;

		var mdl = ThornsWeaponResourceLoad.LoadWeaponModelOrFallback(
			modelPath,
			"FP viewmodel",
			out var usedFallbackGeometry,
			out var usedBowStockFpPlaceholder );
		_fpBowUsesStockPlaceholderMesh = usedBowStockFpPlaceholder;
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
		var weaponVm = pawnRootVm.IsValid() ? pawnRootVm.Components.Get<ThornsFpPresentation>() : default;
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
		vmr.CreateBoneObjects = true;
		vmr.CreateAttachments = true;
		_weaponSkin = vmr;

		var pawnRootSpawn = GameObject.Parent;
		var fpSpawn = pawnRootSpawn.IsValid() ? pawnRootSpawn.Components.Get<ThornsFpPresentation>() : default;
		_activeCombatWeaponId = fpSpawn.IsValid() ? fpSpawn.ClientMirrorCombatDefinitionId?.Trim() ?? "" : "";
		if ( IsSniperViewModel( modelPath ) )
			ThornsSboxAttachmentCatalog.CaptureM700DefaultBodyGroups( vmr );

		_attachmentMount = _viewmodel.Components.Create<ThornsViewModelAttachmentMount>();
		SyncAttachments( _activeCombatWeaponId, ResolveOwnerAttachments() );
		UpdateM700IntegratedScopePresentation();

		if ( ViewModelUseAnimGraph && UsesStockFpAnimatorSequences( modelPath ) && !usedFallbackGeometry )
		{
			var anim = _viewmodel.Components.Create<ThornsViewModelFpAnimator>();
			_animator = anim;
			ConfigureAnimatorPresetForModel( anim, modelPath );
			TryPlayFpGunDeploySoundForModel( modelPath );
			anim.BindAndRunEquipRoutine( vmr, mdl );
			anim.SetWeaponPose( _attachmentMount?.RequiresWeaponPose == true ? 1 : 0 );
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
		var building = pawnRoot.IsValid()
			? pawnRoot.Components.Get<Terraingen.Buildings.ThornsPlayerBuildingController>()
			: default;

		var shouldDraw = !building.IsValid() || !building.BuildMenuOpen;
		if ( _fpWeaponDrawingEnabled == shouldDraw )
			return;

		SetFirstPersonWeaponDrawingEnabled( shouldDraw );
	}

	protected override void OnUpdate()
	{
		if ( !_viewmodel.IsValid() || !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		SyncFpWeaponDrawWithBuildingController();

		var bowEquipped = ThornsWeaponResourceLoad.IsBowModelPath( _fpVmActiveModelPath );
		if ( PauseAutomaticWeaponViewmodelTransform )
		{
			if ( bowEquipped && LiveInspectorPreview )
				ApplyViewModelTransformFromInspector();

			return;
		}

		TickViewModelAdsOffsetLerp();
		TickViewKickRecovery();
		TryLatchHierarchyViewmodelEdit();
		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		ApplyViewModelTransformFromInspector();
	}

	protected override void OnValidate()
	{
		if ( GameObject.Scene is null || !GameObject.Scene.IsValid() )
			return;

		var bowEquipped = _viewmodel.IsValid()
		                  && ThornsWeaponResourceLoad.IsBowModelPath( _fpVmActiveModelPath );

		if ( PauseAutomaticWeaponViewmodelTransform && !bowEquipped )
			return;

		ApplyViewModelTransformFromInspector();
	}

	[Button( "Reset Bow Pose" )]
	public void ResetBowViewmodelPose()
	{
		BowViewmodelLocalOffset = ThornsFpItemHelpers.FpBowViewmodelRootOffset;
		BowViewmodelLocalEulerDegrees = ThornsFpItemHelpers.FpBowViewmodelRootEulerDegrees;
		BowViewmodelLocalScale = ThornsFpItemHelpers.FpBowViewmodelRootScale;
		BowApplyStockTenTimesFpScale = false;
		PauseAutomaticWeaponViewmodelTransform = false;
		ApplyViewModelTransformFromInspector();
	}

	[Button( "Log Bow Pose (for code)" )]
	public void LogBowViewmodelPose()
	{
		var o = BowViewmodelLocalOffset;
		var e = BowViewmodelLocalEulerDegrees;
		var s = BowViewmodelLocalScale;
		Log.Info(
			"[Thorns][Bow FP] Copy into ThornsFpItemHelpers / item catalog:\n" +
			$"FpBowViewmodelRootOffset = new Vector3( {o.x:F2}f, {o.y:F2}f, {o.z:F2}f );\n" +
			$"FpBowViewmodelRootEulerDegrees = new Vector3( {e.x:F2}f, {e.y:F2}f, {e.z:F2}f );\n" +
			$"FpBowViewmodelRootScale = new Vector3( {s.x:F2}f, {s.y:F2}f, {s.z:F2}f );" );
	}

	void TickViewModelAdsOffsetLerp()
	{
		var pawnRoot = GameObject.Parent;
		var equipWeapon = pawnRoot.IsValid()
			? pawnRoot.Components.Get<ThornsFpPresentation>()
			: default;
		var combatId = equipWeapon.IsValid() ? equipWeapon.ClientMirrorCombatDefinitionId ?? "" : "";
		var meleeEquipped = Terraingen.Combat.ThornsFpToolCombat.TreatsAsMeleeWeapon( combatId );
		var fpAllowsAds = !equipWeapon.IsValid() || equipWeapon.ClientMirrorFpPresentationAllowsCombatLayers();
		var adsHeld = !meleeEquipped && fpAllowsAds && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var forward = adsHeld
			? ViewModelAdsForwardOffset + _sightAdsForwardOffset * _sightAdsForwardBlend
			: 0f;
		var target = adsHeld ? new Vector3( forward, 0f, 0f ) : Vector3.Zero;
		var t = Math.Clamp( Time.Delta * ViewModelAdsOffsetLerpSpeed, 0f, 1f );
		_adsOffsetCurrent = Vector3.Lerp( _adsOffsetCurrent, target, t );
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

	/// <summary>Writes grip + item pose onto the child <c>WeaponViewmodel</c> (called every frame and from <see cref="OnValidate"/>).</summary>
	public void ApplyViewModelTransformFromInspector()
	{
		if ( !_viewmodel.IsValid() )
			return;

		if ( Game.IsPlaying && !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		if ( PauseAutomaticWeaponViewmodelTransform )
			return;

		var pawnRoot = GameObject.Parent;
		var equipWeapon = pawnRoot.IsValid()
			? pawnRoot.Components.Get<ThornsFpPresentation>()
			: default;

		ResolveFpMeshRootPose( equipWeapon, _fpVmActiveModelPath, out var itemOff, out var itemSc, out var itemEu );
		var gripE = ViewModelGripLocalEulerDegrees;
		var swingPitch = SampleHarvestToolSwingPitchDegrees();
		var bob = Vector3.Zero;

		_viewmodel.LocalPosition = ViewModelGripLocalPosition + ViewModelPresentationLocalOffset + itemOff + _adsOffsetCurrent + _sightEyeViewmodelOffset + bob;
		_viewmodel.LocalRotation = Rotation.From(
			gripE.x + itemEu.x + swingPitch + _viewKickPitch,
			gripE.y + itemEu.y + _viewKickYaw,
			gripE.z + itemEu.z + _viewKickRoll );
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
		var itemPos = _viewmodel.LocalPosition - ViewModelGripLocalPosition - ViewModelPresentationLocalOffset - _adsOffsetCurrent - _sightEyeViewmodelOffset;
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

		if ( ThornsWeaponResourceLoad.IsBowModelPath( _fpVmActiveModelPath ) )
		{
			BowViewmodelLocalOffset = itemPos;
			BowViewmodelLocalEulerDegrees = itemEuler;
			BowViewmodelLocalScale = _viewmodel.LocalScale;
			return;
		}

		var pawnRoot = GameObject.Parent;
		var weapon = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsFpPresentation>() : default;
		if ( !weapon.IsValid() )
			return;

		weapon.ViewModelLocalPosition = itemPos;
		weapon.ViewModelLocalEulerDegrees = itemEuler;
		weapon.ViewModelLocalScale = _viewmodel.LocalScale;
	}

	/// <summary>Clears hierarchy lock and re-applies pose from <see cref="ThornsFpPresentation"/> / grip fields.</summary>
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

		if ( _animator.IsValid() )
			_animator.AddLinkedSkin( arms );

		return arms;
	}

	/// <summary>Same cue as YouAreNotAlone — long-gun / SMG / shotgun / sniper FP equip.</summary>
	void TryPlayFpGunDeploySoundForModel( string modelPath )
	{
		if ( string.IsNullOrWhiteSpace( ThornsFpPresentation.GunDeploySoundResource ) )
			return;

		if ( !string.Equals( modelPath, M4FirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, Mp5FirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, ShotgunFirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, SniperFirstPersonViewmodelPath, StringComparison.Ordinal )
		     && !string.Equals( modelPath, UspFirstPersonViewmodelPath, StringComparison.Ordinal ) )
			return;

		var pawnRoot = GameObject.Parent;
		if ( pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var path = ThornsFpPresentation.GunDeploySoundResource.Trim();
		if ( ThornsLocalPlayer.TryGetAuthoritativeEye( pawnRoot, out var ear, out _ ) )
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
			anim.ReloadSequenceName = "Reload_Pull";
			// v_m700 ADS aligns via anim graph `ironsights` (Facepunch FP weapons doc) — not DirectPlayback pose swap.
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

		if ( string.Equals( modelPath, UspFirstPersonViewmodelPath, StringComparison.Ordinal ) )
		{
			anim.ReloadSequenceName = "";
			anim.ReloadDurationFallbackSeconds = 1.8f;
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

	public void SyncAttachments( string combatWeaponId, IEnumerable<ThornsAttachmentId> attachments )
	{
		if ( !_weaponSkin.IsValid() || _attachmentMount is null || !_attachmentMount.IsValid() )
			return;

		combatWeaponId = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatWeaponId );
		_activeCombatWeaponId = combatWeaponId;
		_attachmentMount.Apply( combatWeaponId, _weaponSkin, attachments, ViewModelUseOverlayPass );
		_animator?.SetWeaponPose( _attachmentMount.RequiresWeaponPose ? 1 : 0 );
		UpdateM700IntegratedScopePresentation();
	}

	public void ApplySightPresentation( ThornsAdsSightMode mode, float adsBlend, bool classicScopeVisualActive = false )
	{
		if ( !_viewmodel.IsValid() )
			return;

		_attachmentMount?.SetRedDotStyleMeshesVisible( true );

		var overlayPass = mode == ThornsAdsSightMode.None || adsBlend < ThornsAdsSightTuning.WorldPassAdsBlend;
		SetViewModelOverlayPass( overlayPass );

		var clearOpticLens = adsBlend >= ThornsAdsSightTuning.WorldPassAdsBlend;
		if ( _attachmentMount?.HasRedDotStyleAttachment() == true )
			_attachmentMount.ApplyRedDotLensPresentation( clearOpticLens );

		UpdateM700IntegratedScopePresentation();

		_sightAdsForwardOffset = ResolveSightAdsForwardOffset( mode, adsBlend );
		_sightAdsForwardBlend = adsBlend;
		ComputeSightEyeViewmodelOffset( mode, adsBlend );
		SetViewModelVisuallyHidden( classicScopeVisualActive );
	}

	void UpdateM700IntegratedScopePresentation()
	{
		if ( !IsSniperViewModel( _fpVmActiveModelPath ) || !_weaponSkin.IsValid() )
			return;

		if ( _attachmentMount?.HasAttachment( ThornsAttachmentId.RangedSight ) == true )
		{
			ThornsSboxAttachmentCatalog.ApplyM700StockScopeBodyGroups( _weaponSkin );
			ThornsM700ScopePresentation.EnsureStockScopeVisible( _weaponSkin );
			return;
		}

		ThornsSboxAttachmentCatalog.ApplyM700IronSightBodyGroups( _weaponSkin );
		ThornsM700ScopePresentation.Apply( _weaponSkin, hideScopeLens: true );
	}

	void ComputeSightEyeViewmodelOffset( ThornsAdsSightMode mode, float adsBlend )
	{
		_sightEyeViewmodelOffset = Vector3.Zero;
		if ( !_viewmodel.IsValid() || !_weaponSkin.IsValid() || adsBlend <= 0.001f || mode == ThornsAdsSightMode.None )
			return;

		switch ( mode )
		{
			case ThornsAdsSightMode.RedDot:
				_sightEyeViewmodelOffset = ComputeLensViewmodelOffset( mode, adsBlend, ThornsAdsSightTuning.RedDotCameraOffset );
				_sightEyeViewmodelOffset += ResolveRedDotFineTune( adsBlend );
				break;
			case ThornsAdsSightMode.SniperScope:
				_sightEyeViewmodelOffset = ComputeLensViewmodelOffset( mode, adsBlend, ThornsAdsSightTuning.SniperScopeCameraOffset );
				_sightEyeViewmodelOffset += ResolveSniperScopeFineTune( adsBlend );
				break;
			default:
				if ( TryGetCameraBoneLocal( out var bonePos, out _ ) )
					_sightEyeViewmodelOffset = -bonePos * adsBlend;
				break;
		}

		SanitizeSightEyeViewmodelOffset();
	}

	Vector3 ComputeLensViewmodelOffset( ThornsAdsSightMode mode, float adsBlend, Vector3 fallbackOffset )
	{
		TryGetCameraBoneLocal( out var bonePos, out _ );

		if ( ThornsViewModelSightResolve.TryGetOpticLensSkinLocal( mode, _weaponSkin, _attachmentMount, out var lensSkinLocal ) )
			return -(lensSkinLocal - bonePos) * adsBlend;

		return -fallbackOffset * adsBlend;
	}

	Vector3 ResolveRedDotFineTune( float adsBlend )
	{
		if ( adsBlend <= 0.001f || _attachmentMount is null )
			return Vector3.Zero;

		if ( !_attachmentMount.TryGetEquippedRedDotStyleAttachment( out var equippedSight ) )
			return Vector3.Zero;

		var fine = equippedSight switch
		{
			ThornsAttachmentId.HoloSight => ThornsAdsSightTuning.HoloSightAdsViewmodelFineTune,
			ThornsAttachmentId.RaisedRedDot => ThornsAdsSightTuning.RaisedRedDotAdsViewmodelFineTune,
			_ => Vector3.Zero
		};

		return fine * adsBlend;
	}

	Vector3 ResolveSniperScopeFineTune( float adsBlend )
	{
		if ( adsBlend <= 0.001f )
			return Vector3.Zero;

		if ( IsSniperViewModel( _fpVmActiveModelPath ) )
		{
			if ( _attachmentMount?.HasAttachment( ThornsAttachmentId.RangedSight ) != true )
				return Vector3.Zero;

			return ThornsAdsSightTuning.M700RangedSightAdsViewmodelFineTune * adsBlend;
		}

		if ( _attachmentMount?.HasAttachment( ThornsAttachmentId.RangedSight ) == true )
			return Vector3.Zero;

		return Vector3.Zero;
	}

	float ResolveSightAdsForwardOffset( ThornsAdsSightMode mode, float adsBlend )
	{
		if ( adsBlend <= 0.001f || !_weaponSkin.IsValid() )
			return 0f;

		switch ( mode )
		{
			case ThornsAdsSightMode.RedDot:
				if ( ThornsViewModelSightResolve.TryGetOpticLensSkinLocal(
					     ThornsAdsSightMode.RedDot, _weaponSkin, _attachmentMount, out _ ) )
					return 0f;

				return ThornsAdsSightTuning.RedDotAdsForwardOffset;
			case ThornsAdsSightMode.SniperScope:
				if ( ThornsViewModelSightResolve.TryGetOpticLensSkinLocal(
					     ThornsAdsSightMode.SniperScope, _weaponSkin, _attachmentMount, out _ ) )
					return 0f;

				return ThornsAdsSightTuning.SniperAdsForwardOffset;
			case ThornsAdsSightMode.IronSight:
				return ThornsAdsSightTuning.IronSightAdsForwardOffset;
			default:
				return 0f;
		}
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
					skin.Tint = hidden ? skin.Tint.WithAlpha( 0f ) : skin.Tint.WithAlpha( 1f );
					break;
				case ModelRenderer model:
					model.Tint = hidden ? model.Tint.WithAlpha( 0f ) : model.Tint.WithAlpha( 1f );
					break;
			}
		}
	}

	bool TryGetCameraBoneLocal( out Vector3 localPosition, out Rotation localRotation )
	{
		localPosition = Vector3.Zero;
		localRotation = Rotation.Identity;

		if ( !_weaponSkin.IsValid() )
			return false;

		var boneObject = _weaponSkin.GetBoneObject( "camera" );
		if ( !boneObject.IsValid() && _weaponSkin.Model.IsValid() && _weaponSkin.Model.Bones.HasBone( "camera" ) )
			boneObject = _weaponSkin.GetBoneObject( _weaponSkin.Model.Bones.GetBone( "camera" ) );

		if ( !boneObject.IsValid() )
			return false;

		localPosition = _weaponSkin.WorldTransform.PointToLocal( boneObject.WorldPosition );
		localRotation = boneObject.LocalRotation;
		return true;
	}

	static bool IsSniperViewModel( string modelPath ) =>
		!string.IsNullOrWhiteSpace( modelPath )
		&& (modelPath.Contains( "sniper_m700", StringComparison.OrdinalIgnoreCase )
		    || modelPath.Contains( "v_m700", StringComparison.OrdinalIgnoreCase ));

	static void SanitizeSightEyeViewmodelOffset( ref Vector3 offset )
	{
		if ( !float.IsFinite( offset.x ) || !float.IsFinite( offset.y ) || !float.IsFinite( offset.z ) )
		{
			offset = Vector3.Zero;
			return;
		}

		const float maxLength = 18f;
		var length = offset.Length;
		if ( length > maxLength )
			offset *= maxLength / length;
	}

	void SanitizeSightEyeViewmodelOffset() => SanitizeSightEyeViewmodelOffset( ref _sightEyeViewmodelOffset );

	IReadOnlyList<ThornsAttachmentId> ResolveOwnerAttachments()
	{
		var pawnRoot = GameObject.Parent;
		if ( !pawnRoot.IsValid() )
			return Array.Empty<ThornsAttachmentId>();

		var gameplay = pawnRoot.Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return Array.Empty<ThornsAttachmentId>();

		var stack = gameplay.GetHotbarSlot( hotbar );
		return ThornsWeaponAttachmentState.GetAttachments( stack );
	}

	public void ClearViewModel()
	{
		if ( _viewmodel.IsValid() )
		{
			_viewmodel.Destroy();
			_viewmodel = default;
			_adsOffsetCurrent = Vector3.Zero;
		}

		ResetViewKick();

		_weaponSkin = default;
		_animator = default;
		_attachmentMount = default;
		_activeCombatWeaponId = "";
		_sightEyeViewmodelOffset = Vector3.Zero;
		_sightAdsForwardOffset = 0f;
		_sightAdsForwardBlend = 0f;
		_viewModelVisuallyHidden = false;

		_fpVmActiveModelPath = "";
		_fpBowUsesStockPlaceholderMesh = false;
		_harvestToolSwingStartRealtime = -1.0;
		ResetDrivenViewmodelTransformTracking();
	}

	void ApplyFpWeaponDrawingPreferenceToRenderers()
	{
		if ( !_viewmodel.IsValid() )
			return;

		foreach ( var smr in _viewmodel.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
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
		_fpBowUsesStockPlaceholderMesh = false;
		_harvestToolSwingStartRealtime = -1.0;
	}

	static bool ThornsGameplayIsClientFxContext()
	{
		return Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
	}

	/// <summary>Owner FP muzzle for tracers — uses the live <c>WeaponViewmodel</c> mesh, not the aim camera.</summary>
	public static bool TryResolveOwnerMuzzleWorld( GameObject pawnRoot, string combatWeaponDefinitionId, out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !ThornsGameplayIsClientFxContext() || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var view = Terraingen.Player.ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return false;

		var vmc = view.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			return false;

		var combatId = combatWeaponDefinitionId?.Trim() ?? "";
		if ( ThornsWeaponDefinitions.IsBowWeapon( ThornsWeaponDefinitions.Get( combatId ), combatId )
		     && vmc.TryResolveBowViewmodelMuzzleWorld( out muzzleWorld ) )
			return true;

		return vmc.TryResolveActiveViewmodelMuzzleWorld( combatWeaponDefinitionId, view, out muzzleWorld );
	}

	/// <summary>Bow arrow origin on the right-side FP viewmodel (not the left-hand / camera-center fallback).</summary>
	public static bool TryResolveOwnerBowMuzzleWorld( GameObject pawnRoot, out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !ThornsGameplayIsClientFxContext() || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var view = Terraingen.Player.ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return false;

		var vmc = view.Components.Get<ThornsViewModelController>();
		return vmc.IsValid() && vmc.TryResolveBowViewmodelMuzzleWorld( out muzzleWorld );
	}

	public bool TryResolveBowViewmodelMuzzleWorld( out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !_viewmodel.IsValid() || !ThornsWeaponResourceLoad.IsBowModelPath( _fpVmActiveModelPath ) )
			return false;

		muzzleWorld = _viewmodel.WorldTransform.PointToWorld( ThornsFpItemHelpers.FpBowTracerLocalOffset );
		return true;
	}

	public bool TryResolveActiveViewmodelMuzzleWorld(
		string combatWeaponDefinitionId,
		GameObject cameraReference,
		out Vector3 muzzleWorld )
	{
		muzzleWorld = default;
		if ( !_viewmodel.IsValid() )
			return false;

		var smr = _viewmodel.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf );
		if ( !smr.IsValid() || !smr.Enabled || !smr.Model.IsValid() || smr.Model.IsError )
			return false;

		var combatId = string.IsNullOrWhiteSpace( combatWeaponDefinitionId )
			? ThornsCombatMuzzleResolve.InferCombatIdFromViewModelPath( _fpVmActiveModelPath )
			: combatWeaponDefinitionId;

		return ThornsCombatMuzzleResolve.TryResolveSkinnedMuzzleWorld(
			smr,
			combatId,
			_viewmodel.WorldRotation.Forward,
			cameraReference,
			useViewmodelLocalOffsets: true,
			out muzzleWorld );
	}
}
