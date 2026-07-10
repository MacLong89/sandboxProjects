#nullable disable

using System;
using System.Collections.Generic;
using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// Weapon on the player network root. THORNS_EVERYTHING_DOCUMENT §3 (host hitscan, fire rate, ammo, durability per shot),
/// §13 server authority. Client sends intent only via RPCs.
/// </summary>
[Title( "Thorns — Weapon" )]
[Category( "Thorns" )]
[Icon( "swords" )]
[Order( 200 )]
public sealed partial class ThornsWeapon : Component
{
	public const string WorldVisualChildName = "WeaponWorld";

	/// <summary>s&amp;box <c>.sound</c> resource path (under project <c>Assets/</c>). Empty = skip playback (no compile/load spam).</summary>
	public const string M4FireSoundResource = "sounds/m4_shot.sound";

	public const string ShotgunFireSoundResource = "sounds/shotgun_shot.sound";

	/// <summary>M4-style magazine reload <c>.sound</c> (also used for mp5 / sniper).</summary>
	public const string M4ReloadSoundResource = "sounds/m4_reload.sound";

	public const string ShotgunReloadSoundResource = "sounds/shotgun_reload.sound";

	public const string KnifeStabLightSoundResource = "sounds/knife_stab_light.sound";

	public const string KnifeStabHeavySoundResource = "sounds/knife_stab_heavy.sound";

	/// <summary>FP deploy cue when equipping long-gun / shotgun-class FP models.</summary>
	public const string GunDeploySoundResource = "sounds/gun_deploy.sound";

	/// <summary>Optional kill-confirm sting — assign a <c>.sound</c> resource to enable (presentation only).</summary>
	public const string KillConfirmSoundResource = "";

	/// <summary>Optional default when <see cref="HitMarkerBodySound"/> is empty; leave empty to skip until you add a <c>.sound</c> asset.</summary>
	public const string HitMarkerBodySoundDefault = "";

	/// <summary><see cref="WorldVisualChildName"/> is created with a dev box; that mesh is huge in source units.</summary>
	public static readonly Vector3 WorldMeshLocalScaleDevBox = new( 0.1f, 0.1f, 0.28f );

	/// <summary>Uniform scale for loaded third-person <c>w_*</c> meshes parented under Citizen <c>Body</c> (tune if weapons read small/large vs rig).</summary>
	public static readonly Vector3 WorldMeshLocalScaleWeapon = new( 2f, 2f, 2f );

	/// <summary>
	/// When <see cref="ThornsWeaponResourceLoad.LoadWeaponModelOrFallback"/> falls back to <c>models/dev/box.vmdl</c> — <b>do not</b> use
	/// <see cref="WorldMeshLocalScaleDevBox"/> (that is only for the initial networked stub); 0.1 scale makes the orange placeholder microscopic.
	/// </summary>
	public static readonly Vector3 WorldMeshLocalScaleWeaponLoadFailed = new( 2f, 2f, 2f );

	/// <summary>Default offset when parented under Citizen <c>Body</c> — copy/tune the same values on <see cref="ThornsWeaponWorldVisual"/> in the inspector for live edits.</summary>
	public static readonly Vector3 WorldMeshLocalPositionRelBody = new( 12f, -8f, 32.5f );

	/// <summary>Default fallback if <c>Body</c> is missing (Z-up from feet). Mirrored on <see cref="ThornsWeaponWorldVisual.TpWeaponManualLocalPositionIfNoBody"/>.</summary>
	public static readonly Vector3 WorldMeshLocalPositionIfNoBody = new( 0f, 0f, 40f );

	// TP carry: bone names for SkinnedModelRenderer.GetBoneObject( string ) / wrist grip. First match wins.
	public static readonly string[] CitizenTpWeaponRightHandBoneCandidates =
	{
		"hand_R",
		"wrist_R",
		"Hold_R",
		"weapon_hand_R"
	};

	const float AimDotMin = 0.55f;

	[Property] public string WeaponDefinitionId { get; set; } = "dev_placeholder";

	[Property] public string ViewModelAsset { get; set; } = "models/dev/box.vmdl";

	/// <summary>
	/// Owner-client mirror: additive offset on the pawn <c>View</c> for the FP mesh root (see <see cref="ThornsViewModelController"/>).
	/// +X pushes toward the world in front of the camera (same axis as <see cref="ThornsViewModelController.ViewModelAdsForwardOffset"/>). Stock Facepunch FP rigs ignore this.
	/// </summary>
	[Property] public Vector3 ViewModelLocalPosition { get; set; } = Vector3.Zero;

	/// <summary>Owner-client mirror: local scale on that FP mesh root. Stock Facepunch FP rigs ignore this.</summary>
	[Property] public Vector3 ViewModelLocalScale { get; set; } = new Vector3( 1f, 1f, 1f );

	/// <summary>Owner-client mirror: additive local Euler degrees on FP mesh root (added to <see cref="ThornsViewModelController.ViewModelGripLocalEulerDegrees"/>). Stock Facepunch FP rigs ignore.</summary>
	[Property] public Vector3 ViewModelLocalEulerDegrees { get; set; } = Vector3.Zero;

	[Property, Category( "Thorns/Hitmarker" )] public string HitMarkerBodySound { get; set; } = "";

	[Property, Category( "Thorns/Hitmarker" )] public string HitMarkerHeadshotSound { get; set; } = "";

	[Property, Category( "Thorns/Hitmarker" )] public float HitMarkerBodyVolume { get; set; } = 0.2f;

	[Property, Category( "Thorns/Hitmarker" )] public float HitMarkerHeadshotVolume { get; set; } = 0.32f;

	double _nextFireAllowedHostTime;
	double _nextMeleeHeavyAllowedHostTime;

	/// <summary>Host: shared primary-action cooldown for primitive harvest + primitive primary melee (Attack1).</summary>
	public bool HostIsPrimaryMeleeCooldownActive()
	{
		return Networking.IsHost && Time.Now < _nextFireAllowedHostTime;
	}

	/// <summary>Host: apply primary melee / bare-hands harvest pacing (see <see cref="ThornsHarvestInteractor"/>).</summary>
	public void HostApplyPrimaryMeleeCooldownSeconds( float seconds )
	{
		if ( !Networking.IsHost )
			return;

		var now = Time.Now;
		var until = now + Math.Max( 0.01f, seconds );
		_nextFireAllowedHostTime = Math.Max( _nextFireAllowedHostTime, until );
	}

	// Host-only: authoritative Valorant-style spray index + bloom ordinal (reset after RecoilResetDelaySeconds gap).
	double _hostRecoilLastShotTime;
	int _hostRecoilPatternIndex;
	int _hostRecoilSprayOrdinal;

	/// <summary>Owner-client mirror of combat def id from equip RPC (non-authoritative).</summary>
	string _ownerMirrorCombatWeaponDefinitionId = "";

	/// <summary>Prior frame owner HUD reloading mirror — for FP reload anim rising edge.</summary>
	bool _fpVmLastReloadHud;

	/// <summary>Retries <see cref="ApplyOwnerEquipmentPresentation"/> when equip RPC ran before View/camera/viewmodel controller was ready (listen server / first spawn).</summary>
	double _lastFpPresentationEnsureRealtime;

	int _fpPresentationEnsureAttempts;

	string _fpDiagLastPresentationSig = "";
	double _fpDiagNextPresentationLogTime;
	double _fpTryEnsureNextDiagLogTime;

	ThornsWeaponAmmoService MirrorAmmo => _weaponServices.Ammo;

	/// <summary>Local-only UX gate — server always re-validates.</summary>
	public bool ClientMirrorMayFireIntent()
	{
		var cidGate = ThornsToolMeleeCombat.ResolveClientCombatDefinitionIdForInput( this );
		if ( string.IsNullOrWhiteSpace( cidGate ) )
			return false;
		if ( MirrorAmmo?.ClientMirrorWeaponBroken == true )
			return false;
		if ( MirrorAmmo?.ClientMirrorReloading == true )
			return false;

		var cidMirror = cidGate;
		var defMirror = ThornsWeaponDefinitions.Get( cidMirror );
		if ( !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( defMirror, cidMirror )
		     && ThornsWeaponDefinitions.UsesPerShellReloadCycle( defMirror, cidMirror )
		     && MirrorAmmo?.ClientShotgunPumpHud == true )
			return false;
		if ( !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( defMirror, cidMirror ) )
		{
			if ( (MirrorAmmo?.ClientMirrorLoadedAmmo ?? 0) <= 0 )
				return false;
		}

		if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cidMirror ) )
			return true;

		if ( !ClientMirrorFpPresentationAllowsCombatLayers( cidMirror ) )
			return false;

		return true;
	}

	/// <summary>
	/// Owner-client: deploy/reload presentation gate shared by FP ironsights and combat intent — matches <see cref="ThornsViewModelFpAnimator.PresentationAllowsCombatFire"/>.
	/// Does not include ammo/broken checks (so empty-weapon ADS intent can exist elsewhere when desired).
	/// </summary>
	public bool ClientMirrorFpPresentationAllowsCombatLayers( string combatIdOverride = null )
	{
		var cid = combatIdOverride?.Trim() ?? ThornsToolMeleeCombat.ResolveClientCombatDefinitionIdForInput( this );
		if ( string.IsNullOrWhiteSpace( cid ) )
			return true;

		if ( ThornsToolMeleeCombat.IsToolMeleeCombatId( cid ) )
			return true;

		if ( !ThornsViewModelController.CombatDefinitionUsesStockFpAnimator( cid ) )
			return true;

		var fp = ResolveLocalFpAnimator();
		return fp.IsValid() && fp.PresentationAllowsCombatFire;
	}

	/// <summary>HUD magazine + reserve counters (ranged weapons only — not melee / no combat id).</summary>
	public static bool HudShouldShowGunAmmoCounters( ThornsWeapon weapon )
	{
		if ( weapon is null || !weapon.IsValid() )
			return false;

		var cid = weapon.ClientMirrorCombatDefinitionId ?? "";
		if ( string.IsNullOrWhiteSpace( cid ) )
			return false;

		var def = ThornsWeaponDefinitions.Get( cid );
		return !ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, cid );
	}

	// --- Debug UI / HUD (owner mirror only, non-authoritative) ---

	public int ClientMirrorLoadedAmmo => MirrorAmmo?.ClientMirrorLoadedAmmo ?? 0;
	public int ClientMirrorReserveAmmo => MirrorAmmo?.ClientMirrorReserveAmmo ?? 0;
	public bool ClientMirrorWeaponBroken => MirrorAmmo?.ClientMirrorWeaponBroken ?? false;
	public bool ClientMirrorReloading => MirrorAmmo?.ClientMirrorReloading ?? false;
	/// <summary>Best-known combat def for local UX (ADS, melee alt-fire, HUD). Prefers equip RPC mirror, then replicated <see cref="ThornsHotbarEquipment.ObserversCombatWeaponDefinitionId"/> if the mirror lagged one frame.</summary>
	public string ClientMirrorCombatDefinitionId
	{
		get
		{
			var equip = Components.Get<ThornsHotbarEquipment>();
			var inv = Components.Get<ThornsInventory>();

			if ( !string.IsNullOrWhiteSpace( _ownerMirrorCombatWeaponDefinitionId ) )
			{
				var owner = _ownerMirrorCombatWeaponDefinitionId.Trim();
				if ( equip.IsValid()
				     && ThornsToolMeleeCombat.ClientTryGetEquippedHotbarItemId( equip, inv, out var itemId )
				     && !string.IsNullOrWhiteSpace( itemId ) )
				{
					var toolCid = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( itemId )?.Trim() ?? "";
					if ( !string.IsNullOrEmpty( toolCid ) )
						return string.Equals( owner, toolCid, StringComparison.OrdinalIgnoreCase ) ? owner : toolCid;

					if ( ThornsItemRegistry.TryGet( itemId, out var def )
					     && def.ItemType == ThornsItemType.Weapon )
					{
						var weaponCid = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
							? itemId.Trim()
							: def.CombatWeaponDefinitionId.Trim();
						return string.Equals( owner, weaponCid, StringComparison.OrdinalIgnoreCase ) ? owner : weaponCid;
					}
				}

				return owner;
			}

			if ( equip.IsValid() )
			{
				var inferred = ThornsToolMeleeCombat.TryInferClientCombatDefinitionId( equip, inv );
				if ( !string.IsNullOrWhiteSpace( inferred ) )
					return inferred;
			}

			return "";
		}
	}

	protected override void OnAwake()
	{
		BindWeaponServices();
	}

	protected override void OnStart()
	{
		BindWeaponServices();
		// First-person viewmodel is driven only after server-authoritative equip.
	}

	protected override void OnValidate()
	{
		if ( GameObject.Scene is null || !GameObject.Scene.IsValid() )
			return;

		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			return;

		vmc.PauseAutomaticWeaponViewmodelTransform = false;
		vmc.ApplyViewModelTransformFromInspector();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		TryEnsureFirstPersonWeaponPresentation();

		if ( ThornsHarvestInteractor.LocalOwnerGameplayInputBlocked( GameObject ) )
			return;

		TickFpViewmodelSequences();

		_weaponServices.Input?.TickLocalOwnerInput();
	}

	void LogFpPresentationDiag( string phase, bool showWeaponFp, string viewModelAsset, string combatWeaponDefinitionId, string extra = "" )
	{
		if ( !ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs )
			return;

		var sig = $"{phase}|{showWeaponFp}|{viewModelAsset ?? ""}|{combatWeaponDefinitionId ?? ""}|{extra}";
		var now = Time.Now;
		if ( sig == _fpDiagLastPresentationSig && now < _fpDiagNextPresentationLogTime )
			return;

		_fpDiagLastPresentationSig = sig;
		_fpDiagNextPresentationLogTime = now + 0.85;

		var viewChild = FindChild( GameObject, "View" );
		var local = ThornsPawn.IsLocalConnectionOwner( this );
		var fx = Game.IsPlaying && !Application.IsDedicatedServer && !Application.IsHeadless;
		Log.Info(
			$"[Thorns][FP-Equip] {phase} showFp={showWeaponFp} vm='{viewModelAsset}' combat='{combatWeaponDefinitionId}' localOwner={local} viewChild={viewChild.IsValid()} clientFxCtx={fx} {extra}" );
	}

	void LogFpTryEnsureDiag( string message )
	{
		if ( !ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs )
			return;

		if ( Time.Now < _fpTryEnsureNextDiagLogTime )
			return;

		_fpTryEnsureNextDiagLogTime = Time.Now + 1.1;
		Log.Info( "[Thorns][FP-TryEnsure] " + message );
	}

	void TryEnsureFirstPersonWeaponPresentation()
	{
		var cid = ClientMirrorCombatDefinitionId;

		if ( _fpPresentationEnsureAttempts >= 30 )
			return;

		if ( Time.Now - _lastFpPresentationEnsureRealtime < 0.22 )
			return;

		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var hpEarly = Components.Get<ThornsHealth>();
		if ( hpEarly.IsValid() && ( hpEarly.IsDeadState || !hpEarly.IsAlive ) )
		{
			var vmcDead = viewChild.Components.Get<ThornsViewModelController>();
			if ( vmcDead.IsValid() )
				vmcDead.ClearViewModel();
			return;
		}

		var hasVmChild = false;
		foreach ( var ch in viewChild.Children )
		{
			if ( ch.IsValid() && ch.Name == "WeaponViewmodel" )
			{
				hasVmChild = true;
				break;
			}
		}

		if ( hasVmChild )
		{
			_fpPresentationEnsureAttempts = 0;
			return;
		}

		var hb = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !hb.IsValid() || !inv.IsValid() )
		{
			LogFpTryEnsureDiag(
				$"missing WeaponViewmodel; hbValid={hb.IsValid()} invValid={inv.IsValid()} attempts={_fpPresentationEnsureAttempts}" );
			return;
		}

		var sel = hb.ClientMirrorSelectedHotbar;
		if ( sel < 0 || sel >= ThornsInventory.HotbarSlotCount
		     || !inv.TryGetClientMirrorSlot( sel, out var net ) || net.Quantity <= 0 || string.IsNullOrWhiteSpace( net.ItemId ) )
		{
			_lastFpPresentationEnsureRealtime = Time.Now;
			_fpPresentationEnsureAttempts++;
			LogFpTryEnsureDiag( $"repair→idleHands sel={sel} emptySlot attempts={_fpPresentationEnsureAttempts}" );
			// Match <see cref="ThornsHotbarEquipment.HostCommitEmptyHotbarSlot"/> — idle arms + primitive melee/harvest profile.
			ApplyOwnerEquipmentPresentation(
				true,
				"",
				ThornsToolMeleeCombat.CombatIdPrimitive,
				Vector3.Zero,
				ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
				Vector3.Zero,
				"" );
			return;
		}

		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var def ) )
		{
			LogFpTryEnsureDiag( $"no item def for '{net.ItemId}' attempts={_fpPresentationEnsureAttempts}" );
			return;
		}

		if ( def.ItemType == ThornsItemType.Weapon )
		{
			var weaponCombat = cid;
			if ( string.IsNullOrWhiteSpace( weaponCombat ) )
				weaponCombat = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
					? net.ItemId.Trim()
					: def.CombatWeaponDefinitionId.Trim();

			if ( string.IsNullOrWhiteSpace( weaponCombat ) )
			{
				LogFpTryEnsureDiag(
					$"weapon '{net.ItemId}' but combatId empty — cannot spawn FP vm attempts={_fpPresentationEnsureAttempts}" );
				return;
			}

			var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
			_lastFpPresentationEnsureRealtime = Time.Now;
			_fpPresentationEnsureAttempts++;
			LogFpTryEnsureDiag( $"repair→weapon vm='{vm}' combat='{weaponCombat}'" );
			var fo = def.FpViewmodelRootLocalOffset;
			var fs = ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
			var fe = def.FpViewmodelRootLocalEulerDegrees;
			ApplyOwnerEquipmentPresentation( true, vm, weaponCombat, fo, fs, fe, net.ItemId );
			return;
		}

		if ( def.ItemType == ThornsItemType.Tool && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			var toolCombat = cid;
			if ( string.IsNullOrWhiteSpace( toolCombat ) )
				toolCombat = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( net.ItemId )?.Trim() ?? "";

			if ( string.IsNullOrWhiteSpace( toolCombat ) )
			{
				LogFpTryEnsureDiag(
					$"tool '{net.ItemId}' vm='{def.ViewModelAsset}' but combatId empty attempts={_fpPresentationEnsureAttempts}" );
				return;
			}

			_lastFpPresentationEnsureRealtime = Time.Now;
			_fpPresentationEnsureAttempts++;
			LogFpTryEnsureDiag( $"repair→tool vm='{def.ViewModelAsset}' combat='{toolCombat}'" );
			var foT = ThornsItemRegistry.ComposeFpHarvestToolViewmodelOffset( in def );
			var fsT = ThornsItemRegistry.ResolveFpHarvestToolViewmodelScale( in def );
			var feT = def.FpViewmodelRootLocalEulerDegrees;
			ApplyOwnerEquipmentPresentation( true, def.ViewModelAsset, toolCombat, foT, fsT, feT, net.ItemId );
			return;
		}

		if ( def.ItemType == ThornsItemType.Consumable
		     && ThornsItemRegistry.IsUsableConsumable( def )
		     && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			_lastFpPresentationEnsureRealtime = Time.Now;
			_fpPresentationEnsureAttempts++;
			LogFpTryEnsureDiag( $"repair→consumable vm='{def.ViewModelAsset}'" );
			var foC = def.FpViewmodelRootLocalOffset;
			var fsC = ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
			var feC = def.FpViewmodelRootLocalEulerDegrees;
			ApplyOwnerEquipmentPresentation( true, def.ViewModelAsset, "", foC, fsC, feC, net.ItemId );
			return;
		}

		_lastFpPresentationEnsureRealtime = Time.Now;
		_fpPresentationEnsureAttempts++;
		LogFpTryEnsureDiag(
			$"repair→noFpVm item='{net.ItemId}' type={def.ItemType} attempts={_fpPresentationEnsureAttempts}" );
		ApplyOwnerEquipmentPresentation(
			false,
			"",
			"",
			Vector3.Zero,
			ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
			Vector3.Zero,
			net.ItemId );
	}

	/// <summary>Debug HUD only — forwards reload intent RPC (same path as input).</summary>
	public void DebugUiSendReloadIntent()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		RequestReload();
	}

	static bool MagazineWeaponUsesM4StyleFireSound( string combatKey )
	{
		if ( string.IsNullOrWhiteSpace( combatKey ) )
			return false;

		return string.Equals( combatKey, "m4", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "mp5", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( combatKey, "sniper", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Observer / world one-shot gunfire path for <paramref name="combatDefinitionId"/> (shotgun + magazine rifles — same as owner FP stingers).</summary>
	public static bool TryGetObserverGunshotSoundResourceForCombatDefinitionId( string combatDefinitionId, out string resourcePath )
	{
		resourcePath = null;
		var cid = combatDefinitionId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( cid ) )
			return false;

		if ( string.Equals( cid, "shotgun", StringComparison.OrdinalIgnoreCase ) )
		{
			resourcePath = ShotgunFireSoundResource;
			return true;
		}

		if ( MagazineWeaponUsesM4StyleFireSound( cid ) )
		{
			resourcePath = M4FireSoundResource;
			return true;
		}

		return false;
	}

	[Rpc.Owner]
	void ClientNotifyReloadFailed( string reason )
	{
	}

	/// <summary>Host: reset fire-rate gate when switching equipped weapon.</summary>
	public void HostResetCooldownAfterWeaponEquip()
	{
		if ( !Networking.IsHost )
			return;

		_nextFireAllowedHostTime = 0;
		_nextMeleeHeavyAllowedHostTime = 0;
		_hostRecoilLastShotTime = 0;
		_hostRecoilPatternIndex = 0;
		_hostRecoilSprayOrdinal = 0;
		_weaponServices.Reload?.CancelReloadState();

		var equip = Components.Get<ThornsHotbarEquipment>();
		var combatId = equip.IsValid() ? equip.ServerGetActiveCombatWeaponDefinitionId() : "";
		if ( !string.IsNullOrWhiteSpace( combatId )
		     && ThornsViewModelController.CombatDefinitionUsesStockFpAnimator( combatId ) )
		{
			var now = Time.Now;
			var until = now + ThornsViewModelFpAnimator.StockFpDeployHostBlockSeconds;
			_nextFireAllowedHostTime = until;
			_nextMeleeHeavyAllowedHostTime = until;
		}
	}

	[Rpc.Owner]
	void ClientNotifyWeaponBroken()
	{
	}

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	internal static bool IsWeaponBrokenInSlot( ThornsInventorySlot slot )
	{
		return slot.HasDurability && slot.Durability <= 0f;
	}

	static bool TryResolveWeaponItemDefResilientImpl( string itemId, out ThornsItemRegistry.ThornsItemDefinition itemDef )
	{
		if ( ThornsItemRegistry.TryGet( itemId, out itemDef ) )
			return itemDef.ItemType == ThornsItemType.Weapon || itemDef.ItemType == ThornsItemType.Tool;

		if ( string.Equals( itemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
		{
			itemDef = new ThornsItemRegistry.ThornsItemDefinition(
				Id: "sniper",
				DisplayName: "Sniper",
				MaxStack: 1,
				ItemType: ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "sniper",
				ViewModelAsset: ThornsViewModelController.SniperFirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.SniperWorldModelPath );
			return true;
		}

		if ( string.Equals( itemId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
		{
			itemDef = new ThornsItemRegistry.ThornsItemDefinition(
				Id: "m9_bayonet",
				DisplayName: "M9 Bayonet",
				MaxStack: 1,
				ItemType: ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "m9_bayonet",
				ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath );
			return true;
		}

		itemDef = default;
		return false;
	}

	/// <summary>
	/// One ray segment: hitboxes first, then physics-only (same as legacy single shot).
	/// </summary>
	SceneTraceResult HostTraceHitscanSegment( Vector3 segmentStart, Vector3 dir, float segmentLen, List<GameObject> ignoredRoots ) =>
		ThornsSharedHostHitscan.TraceHitscanSegment( GameObject, segmentStart, dir, segmentLen, ignoredRoots );

	/// <summary>
	/// Host hitscan can strike a large world collider (e.g. scene Plane) before the pawn along the same ray.
	/// Step past penetrable surfaces until we hit a damageable pawn or wildlife, a placed structure (blocks), or run out of range.
	/// </summary>
	bool HostTryResolveHitscanDamageTarget(
		Vector3 start,
		Vector3 dir,
		float range,
		float meleeMaxAbsVerticalSeparationFeet,
		out SceneTraceResult tr,
		out GameObject hitGo,
		out ThornsPawn victimPawn,
		out ThornsHealth victimHealth,
		out bool usedAnalyticFallback,
		out Vector3 analyticHitPosition ) =>
		ThornsSharedHostHitscan.TryResolveHitscanDamageTarget(
			GameObject,
			start,
			dir,
			range,
			meleeMaxAbsVerticalSeparationFeet,
			out tr,
			out hitGo,
			out victimPawn,
			out victimHealth,
			out usedAnalyticFallback,
			out analyticHitPosition );

	bool IsOriginPlausible( Vector3 eyeWorld )
	{
		const float maxEyeSpanFromFeet = 180f;
		if ( (eyeWorld - GameObject.WorldPosition).Length > maxEyeSpanFromFeet )
		{
			return false;
		}

		return true;
	}

	/// <summary>Host-only: same hitscan fire .sound the owner plays for the selected slot (shotgun / magazine rifles only).</summary>
	bool HostTryResolveMirrorGunFireSoundPath( out string path )
	{
		path = null;
		var equip = Components.Get<ThornsHotbarEquipment>();
		var inv = Components.Get<ThornsInventory>();
		if ( !equip.IsValid() || !inv.IsValid() )
			return false;

		var hotbar = equip.ServerGetSelectedHotbarIndex();
		if ( hotbar < 0 || !inv.TryGetHostSlot( hotbar, out var slot ) || slot.IsEmpty )
			return false;

		if ( !TryResolveWeaponItemDefResilientImpl( slot.ItemId, out var itemDef ) )
			return false;

		string authoritativeCombatId;
		if ( itemDef.ItemType == ThornsItemType.Tool )
			authoritativeCombatId = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( slot.ItemId )?.Trim() ?? "";
		else
			authoritativeCombatId = ( string.IsNullOrEmpty( itemDef.CombatWeaponDefinitionId )
				? slot.ItemId
				: itemDef.CombatWeaponDefinitionId )?.Trim() ?? "";

		if ( string.IsNullOrEmpty( authoritativeCombatId )
		     || ThornsToolMeleeCombat.IsToolMeleeCombatId( authoritativeCombatId ) )
			return false;

		var def = ThornsWeaponDefinitions.Get( authoritativeCombatId );
		if ( ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, authoritativeCombatId ) )
			return false;

		return TryGetObserverGunshotSoundResourceForCombatDefinitionId( authoritativeCombatId, out path );
	}

	static ThornsWeaponImpactSurfaceKind HostClassifyFeedbackSurface( SceneTraceResult tr, GameObject hitGo )
	{
		if ( !tr.Hit || !hitGo.IsValid() )
			return ThornsWeaponImpactSurfaceKind.Terrain;
		if ( hitGo.Components.GetInAncestorsOrSelf<ThornsPlacedStructure>( true ).IsValid() )
			return ThornsWeaponImpactSurfaceKind.Metal;
		return ThornsWeaponImpactSurfaceKind.Terrain;
	}

	SceneTraceResult HostTraceFeedbackWorldFirstHit( Vector3 rayStart, Vector3 dirN, float range )
	{
		var ray = new Ray( rayStart, dirN );
		var scene = Scene;
		if ( scene is null || !scene.IsValid() )
			return default;

		return ThornsTraceUtility.RunRay( scene, ray, range, ThornsTraceProfile.WeaponFeedbackWorld, GameObject );
	}

	Vector3 HostFeedbackEndpointWorldTrace( Vector3 rayStart, Vector3 dirN, float range, out ThornsWeaponImpactSurfaceKind surfaceKind )
	{
		var tr = HostTraceFeedbackWorldFirstHit( rayStart, dirN, range );
		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			surfaceKind = HostClassifyFeedbackSurface( tr, tr.GameObject );
			return tr.HitPosition;
		}

		surfaceKind = ThornsWeaponImpactSurfaceKind.None;
		return rayStart + dirN * range;
	}

	/// <summary>Host only: updates third-person weapon mesh for network replication. Never uses dev-box fallback (avoids giant TP cube).</summary>
	public void HostApplyEquippedWorldPresentation( ThornsItemRegistry.ThornsItemDefinition def, bool treatAsHeldWeapon )
	{
		if ( !Networking.IsHost )
			return;

		var ww = FindDescendantNamed( GameObject, WorldVisualChildName );
		if ( !ww.IsValid() )
			return;

		var smr = GetOrCreateWorldSkinnedModelRenderer( ww );
		if ( !smr.IsValid() )
			return;

		if ( !treatAsHeldWeapon || def is null || string.IsNullOrEmpty( def.WorldModelAsset ) )
		{
			smr.Enabled = false;
			smr.Model = default;
			return;
		}

		if ( !ThornsWeaponResourceLoad.TryLoadWeaponWorldModel( def.WorldModelAsset, "TP world weapon (host)", out var worldModel ) )
		{
			Log.Warning( $"[Thorns] Host TP weapon: hide mesh (no valid world model for '{def.Id}' path='{def.WorldModelAsset}')." );
			smr.Enabled = false;
			smr.Model = default;
			return;
		}

		smr.Model = worldModel;
		smr.Tint = Color.White;
		smr.UseAnimGraph = false;
		smr.Enabled = true;
	}

	/// <summary>Third-person weapon child: clear any legacy dev mesh; <see cref="ThornsWeaponWorldVisual"/> assigns the real world model.</summary>
	public static void ResetThirdPersonWeaponWorldVisual( GameObject weaponWorldGo )
	{
		if ( !weaponWorldGo.IsValid() )
			return;

		var smr = GetOrCreateWorldSkinnedModelRenderer( weaponWorldGo );
		if ( !smr.IsValid() )
			return;

		smr.Model = default;
		smr.Enabled = false;
		weaponWorldGo.LocalScale = WorldMeshLocalScaleWeapon;
	}

	public static SkinnedModelRenderer GetOrCreateWorldSkinnedModelRenderer( GameObject weaponWorld )
	{
		if ( !weaponWorld.IsValid() )
			return default;

		var s = weaponWorld.Components.Get<SkinnedModelRenderer>();
		if ( s.IsValid() )
			return s;

		foreach ( var c in weaponWorld.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelf ) )
		{
			if ( c is SkinnedModelRenderer sm )
				return sm;
			if ( c is not null && c.IsValid() )
				c.Destroy();
		}

		return weaponWorld.Components.Create<SkinnedModelRenderer>();
	}

	/// <summary>
	/// Places the third-person weapon mesh on the animated right-hand bone (world space each frame).
	/// Prefer this over <see cref="ParentWorldWeaponToCitizenRig"/> when you want the carry pose to follow grip IK instead of a fixed torso offset.
	/// Returns false if Body/skin/bone is missing — caller should fall back to <see cref="ParentWorldWeaponToCitizenRig"/>.
	/// </summary>
	public static bool TryAlignThirdPersonWeaponToCitizenRightHand(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 gripOffsetInHandSpace,
		Rotation gripRotationAfterHand )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return false;

		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( !body.IsValid() )
			return false;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
			return false;

		foreach ( var boneName in CitizenTpWeaponRightHandBoneCandidates )
		{
			var boneGo = skin.GetBoneObject( boneName );
			if ( !boneGo.IsValid() )
				continue;

			var handWorld = boneGo.WorldTransform;

			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			var worldRot = handWorld.Rotation * gripRotationAfterHand;
			var worldPos = handWorld.Position + handWorld.Rotation * gripOffsetInHandSpace;
			weaponWorld.WorldRotation = worldRot;
			weaponWorld.WorldPosition = worldPos;
			return true;
		}

		return false;
	}

	public static void ParentWorldWeaponToCitizenRig( GameObject pawnRoot, GameObject weaponWorld ) =>
		ParentWorldWeaponToCitizenRig(
			pawnRoot,
			weaponWorld,
			WorldMeshLocalPositionRelBody,
			Rotation.Identity,
			WorldMeshLocalPositionIfNoBody );

	/// <param name="localPositionRelBody">Local position under <c>Body</c> (inspector: <see cref="ThornsWeaponWorldVisual.TpWeaponManualLocalPositionRelBody"/>).</param>
	/// <param name="localRotationRelBody">Local rotation under <c>Body</c>.</param>
	/// <param name="localPositionIfNoBody">Local position when parented to pawn root if <c>Body</c> is missing.</param>
	public static void ParentWorldWeaponToCitizenRig(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 localPositionRelBody,
		Rotation localRotationRelBody,
		Vector3 localPositionIfNoBody )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return;

		// Prefer depth-first name match — replication can attach Citizen Body after spawn; not always a direct child.
		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( body.IsValid() )
		{
			// SetParent only when needed — repeated reparents can stack LocalScale. Always re-apply local pose so
			// network transform replication cannot leave the weapon stuck at root/View offsets for remote viewers.
			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			weaponWorld.LocalPosition = localPositionRelBody;
			weaponWorld.LocalRotation = localRotationRelBody;
			return;
		}

		if ( weaponWorld.Parent != pawnRoot )
			weaponWorld.SetParent( pawnRoot );

		weaponWorld.LocalPosition = localPositionIfNoBody;
		weaponWorld.LocalRotation = Rotation.Identity;
	}

	/// <summary>Owner client only — spawn/update local FP viewmodel after server equip confirmation.</summary>
	public void ApplyOwnerEquipmentPresentation(
		bool showWeaponFp,
		string viewModelAsset,
		string combatWeaponDefinitionId,
		Vector3 fpViewmodelRootLocalOffset = default,
		Vector3 fpViewmodelRootLocalScale = default,
		Vector3 fpViewmodelRootLocalEulerDegrees = default,
		string fpPoseSourceItemId = "" )
	{
		if ( ThornsPawn.IsLocalConnectionOwner( this ) && ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		LogFpPresentationDiag( "ApplyOwnerEquipmentPresentation", showWeaponFp, viewModelAsset, combatWeaponDefinitionId );

		// Consumable / VM-only repairs pass "" — do not clobber a valid melee or gun profile.
		if ( !string.IsNullOrWhiteSpace( combatWeaponDefinitionId ) )
			_ownerMirrorCombatWeaponDefinitionId = combatWeaponDefinitionId.Trim();
		_weaponServices.Input?.ResetAutoFireIntentTime();

		var poseItemId = fpPoseSourceItemId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( poseItemId ) )
		{
			var hbPose = Components.Get<ThornsHotbarEquipment>();
			if ( hbPose.IsValid() && !string.IsNullOrWhiteSpace( hbPose.ClientMirrorActiveItemId ) )
				poseItemId = hbPose.ClientMirrorActiveItemId.Trim();
		}

		if ( showWeaponFp
		     && !string.IsNullOrWhiteSpace( poseItemId )
		     && ThornsItemRegistry.TryGet( poseItemId, out var poseDef ) )
		{
			// Static registry is canonical for FP root pose on tools / held consumables — avoids (0,0,0) when RPC args default or mirror order races startup.
			if ( poseDef.ItemType == ThornsItemType.Tool
			     || ThornsItemRegistry.IsUsableConsumable( poseDef ) )
			{
				fpViewmodelRootLocalOffset = poseDef.ItemType == ThornsItemType.Tool
					? ThornsItemRegistry.ComposeFpHarvestToolViewmodelOffset( in poseDef )
					: poseDef.FpViewmodelRootLocalOffset;
				fpViewmodelRootLocalEulerDegrees = poseDef.ItemType == ThornsItemType.Tool
					? ThornsItemRegistry.ResolveFpHarvestToolViewmodelEulerDegrees( in poseDef )
					: poseDef.FpViewmodelRootLocalEulerDegrees;
				fpViewmodelRootLocalScale = poseDef.ItemType == ThornsItemType.Tool
					? ThornsItemRegistry.ResolveFpHarvestToolViewmodelScale( in poseDef )
					: ThornsItemRegistry.ResolveFpViewmodelRootScale( poseDef.FpViewmodelRootLocalScale );
			}
		}

		var normScale = ThornsItemRegistry.ResolveFpViewmodelRootScale( fpViewmodelRootLocalScale );
		ViewModelLocalPosition = showWeaponFp ? fpViewmodelRootLocalOffset : Vector3.Zero;
		ViewModelLocalScale = showWeaponFp ? normScale : ThornsItemRegistry.FpViewmodelRootLocalScaleOne;
		ViewModelLocalEulerDegrees = showWeaponFp ? fpViewmodelRootLocalEulerDegrees : Vector3.Zero;

		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
		{
			LogFpPresentationDiag( "ApplyOwnerEquipmentPresentation:noView", showWeaponFp, viewModelAsset, combatWeaponDefinitionId );
			return;
		}

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			vmc = viewChild.Components.Create<ThornsViewModelController>();

		if ( string.IsNullOrEmpty( viewModelAsset ) )
		{
			// Bare hotbar slot only — held items with no ViewModelAsset stay empty (no idle-arms fallback).
			var bareHands = showWeaponFp && string.IsNullOrWhiteSpace( poseItemId );
			LogFpPresentationDiag(
				bareHands ? "ApplyOwnerEquipmentPresentation→PresentEmptyHands" : "ApplyOwnerEquipmentPresentation→ClearViewModel",
				showWeaponFp,
				viewModelAsset,
				combatWeaponDefinitionId,
				$"poseItem='{poseItemId}'" );
			if ( bareHands )
				vmc.PresentEmptyFirstPersonHands();
			else
				vmc.ClearViewModel();
			return;
		}

		if ( !showWeaponFp )
		{
			LogFpPresentationDiag(
				"ApplyOwnerEquipmentPresentation→ClearViewModel",
				showWeaponFp,
				viewModelAsset,
				combatWeaponDefinitionId );
			vmc.ClearViewModel();
			return;
		}

		var vmPath = viewModelAsset;
		if ( string.Equals( combatWeaponDefinitionId, "m4", StringComparison.OrdinalIgnoreCase ) )
			vmPath = ThornsViewModelController.M4FirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "mp5", StringComparison.OrdinalIgnoreCase ) )
			vmPath = ThornsViewModelController.Mp5FirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "shotgun", StringComparison.OrdinalIgnoreCase ) )
			vmPath = ThornsViewModelController.ShotgunFirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			vmPath = ThornsViewModelController.SniperFirstPersonViewmodelPath;
		else if ( string.Equals( combatWeaponDefinitionId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			vmPath = ThornsViewModelController.BayonetM9FirstPersonViewmodelPath;

		LogFpPresentationDiag(
			"ApplyOwnerEquipmentPresentation→SpawnViewModel",
			showWeaponFp,
			vmPath,
			combatWeaponDefinitionId,
			$"resolvedFrom='{viewModelAsset}'" );

		vmc.SpawnViewModel( vmPath );

	}

	/// <summary>Local owner: hide FP weapon mesh while build bar is open; show again when closed.</summary>
	public void ApplyLocalFpWeaponDrawForBuildMode( bool buildBarOpen )
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			return;

		vmc.SetFirstPersonWeaponDrawingEnabled( !buildBarOpen );
	}

	protected override void OnDestroy()
	{
		var viewChild = FindChild( GameObject, "View" );
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( vmc.IsValid() )
			vmc.ClearViewModel();
	}

	void TickFpViewmodelSequences()
	{
		var fp = ResolveLocalFpAnimator();
		if ( !fp.IsValid() )
			return;

		var cid = ClientMirrorCombatDefinitionId;
		if ( string.IsNullOrWhiteSpace( cid ) )
			return;

		var def = ThornsWeaponDefinitions.Get( cid );
		var meleeEquipped = ThornsWeaponDefinitions.TreatsAsMeleeWeapon( def, cid );
		var wantAds = !meleeEquipped && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var reloadEdge = ClientMirrorReloading && !_fpVmLastReloadHud;
		_fpVmLastReloadHud = ClientMirrorReloading;

		var shellReloadFx = !meleeEquipped && ThornsWeaponDefinitions.UsesPerShellReloadCycle( def, cid );
		// Magazine reload SFX: host only (HostReloadAsync → SendOwnerWeaponSound / M4ReloadSoundResource). Playing again on the
		// reloading HUD edge doubled the sample (listen server sounded like a short echo).
		var reloadSecs = shellReloadFx
			? ThornsWeaponDefinitions.ShellReloadGameplayGateSeconds( def, cid )
			: Math.Max( 0.01f, def.ReloadTimeSeconds );
		var primaryHeld = Input.Down( "Attack1" ) || Input.Down( "attack1" );
		var tubeAmmoBeforeShellRpc = shellReloadFx ? ClientMirrorLoadedAmmo : -1;

		var vitals = Components.Get<ThornsVitals>();
		var sprintHeldFp = meleeEquipped && vitals.IsValid() && vitals.ServerSprinting;

		fp.OwnerTickPresentation(
			wantAds,
			reloadEdge,
			reloadSecs,
			primaryHeld,
			ThornsWeaponDefinitions.FiringModeGraphValue( def ),
			driveMeleeKnifeExtras: meleeEquipped,
			sprintHeld: sprintHeldFp,
			shellReloadPresentation: shellReloadFx,
			shellReloadAmmoBeforeRpc: tubeAmmoBeforeShellRpc,
			shotgunPumpSessionHeld: MirrorAmmo?.ClientShotgunPumpHud ?? false );

		var hbLoco = Components.Get<ThornsHotbarEquipment>();
		var bareHandsLoco = hbLoco.IsValid()
		                    && string.IsNullOrWhiteSpace( hbLoco.ClientMirrorActiveItemId )
		                    && string.Equals( cid.Trim(), ThornsToolMeleeCombat.CombatIdPrimitive, StringComparison.OrdinalIgnoreCase );
		if ( bareHandsLoco )
		{
			var ccVel = ThornsPawnLocomotion.TryGetVelocity( GameObject );
			var planar = ccVel.WithZ( 0f ).Length;
			fp.OwnerTickBareHandsLocomotion( planar );
		}
	}

	static ThornsViewModelFpAnimator ResolveLocalFpAnimator( GameObject pawnRoot )
	{
		var view = FindChild( pawnRoot, "View" );
		if ( !view.IsValid() )
			return default;

		var wm = FindChild( view, "WeaponViewmodel" );
		if ( !wm.IsValid() )
			return default;

		return wm.Components.Get<ThornsViewModelFpAnimator>();
	}

	/// <summary>Owner client: FP arms punch for tool / primitive / bare-hands primary strikes.</summary>
	public static void TryPlayToolPrimaryStrikeFpAnimationForLocalOwner( GameObject pawnRoot )
	{
		if ( pawnRoot is null || !pawnRoot.IsValid() || !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionPawnRoot( pawnRoot ) )
			return;

		var weapon = pawnRoot.Components.Get<ThornsWeapon>();
		if ( !weapon.IsValid() )
			return;

		var cid = (weapon.ClientMirrorCombatDefinitionId ?? "").Trim();
		if ( !ThornsToolMeleeCombat.IsToolMeleeCombatId( cid ) )
			return;

		var fp = ResolveLocalFpAnimator( pawnRoot );
		if ( !fp.IsValid() )
			return;

		fp.OwnerNotifyMeleeAttackCommitted( heavy: false );
	}

	/// <summary>Legacy entry — routes to <see cref="TryPlayToolPrimaryStrikeFpAnimationForLocalOwner"/>.</summary>
	public static void TryNotifyBareHandsHarvestCommittedForLocalOwner( GameObject pawnRoot )
		=> TryPlayToolPrimaryStrikeFpAnimationForLocalOwner( pawnRoot );

	ThornsViewModelFpAnimator ResolveLocalFpAnimator() => ResolveLocalFpAnimator( GameObject );

	static GameObject FindChild( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;
		}

		return default;
	}

	static GameObject FindDescendantNamed( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;

			var nested = FindDescendantNamed( c, name );
			if ( nested.IsValid() )
				return nested;
		}

		return default;
	}

	public Transform GetPresentationMuzzleTransform()
	{
		var world = FindDescendantNamed( GameObject, WorldVisualChildName );
		if ( ThornsPawn.IsLocalConnectionOwner( this ) )
		{
			var view = FindChild( GameObject, "View" );
			if ( view.IsValid() )
				return view.WorldTransform;
		}

		if ( world.IsValid() )
			return world.WorldTransform;

		return GameObject.WorldTransform;
	}
}
