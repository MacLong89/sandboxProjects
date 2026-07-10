using System;
using System.Threading.Tasks;
using Sandbox;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Sandbox.Network;

namespace Sandbox;

[Title( "Thorns — FP Presentation" )]
[Category( "Thorns/Player" )]
[Icon( "view_in_ar" )]
[Order( 199 )]
public sealed class ThornsFpPresentation : Component, Component.INetworkSpawn
{
	public const string GunDeploySoundResource = "sounds/gun_deploy.sound";

	[Property] public Vector3 ViewModelLocalPosition { get; set; } = Vector3.Zero;

	[Property] public Vector3 ViewModelLocalScale { get; set; } = new( 1f, 1f, 1f );

	[Property] public Vector3 ViewModelLocalEulerDegrees { get; set; } = Vector3.Zero;

	public string ClientMirrorCombatDefinitionId => _ownerMirrorCombatWeaponDefinitionId;

	public string ClientMirrorActiveItemId => _clientMirrorActiveItemId;

	string _ownerMirrorCombatWeaponDefinitionId = "";
	string _clientMirrorActiveItemId = "";

	double _lastFpPresentationEnsureRealtime;
	int _fpPresentationEnsureAttempts;

	protected override void OnStart()
	{
		if ( ThornsLightingTestSceneBootstrap.IsActive )
			return;

		ThornsFpDebug.ApplyToWeaponResourceLoad();

		if ( Game.IsPlaying && ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			RefreshFromActiveHotbar();
	}

	public void OnNetworkSpawn( Connection owner )
	{
		if ( ThornsLightingTestSceneBootstrap.IsActive || !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		ThornsFpDebug.WriteOnce( "fp-spawn", $"OnNetworkSpawn owner={owner?.DisplayName ?? "(null)"} go={GameObject.Name}" );
		_ = DeferredPresentationEnsureAsync();
	}

	async Task DeferredPresentationEnsureAsync()
	{
		RefreshFromActiveHotbar();

		for ( var i = 0; i < 24; i++ )
		{
			await Task.DelayRealtimeSeconds( 0.05f );
			if ( !GameObject.IsValid() || !Game.IsPlaying )
				return;
			if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
				return;

			if ( IsHotbarPresentationSatisfied() )
			{
				ThornsFpDebug.WriteOnce( "fp-deferred-ok", $"DeferredPresentationEnsure: satisfied after {( i + 1 ) * 0.05f:F1}s" );
				return;
			}

			if ( i > 0 && (i % 4) == 3 )
				RefreshFromActiveHotbar();
		}

		ThornsFpDebug.WriteOnce( "fp-deferred-miss", "DeferredPresentationEnsure: timed out (see [Thorns][FP-VM] defer lines)" );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || ThornsLightingTestSceneBootstrap.IsActive )
			return;

		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
		{
			ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( GameObject );
			ThornsCitizenRig.EnsureRemotePlayerThirdPersonPresentation( GameObject );
			return;
		}

		TryEnsureFirstPersonWeaponPresentation();
		TickFpViewmodelSequences();
	}

	public void NotifyLocalConfirmedFire()
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		ResolveLocalFpAnimator( GameObject )?.OwnerNotifyServerConfirmedFire();
	}

	/// <summary>Host/local: one-shot reload clip on the active viewmodel animator.</summary>
	public void NotifyReloadPresentation( float gameplayReloadSeconds )
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
			return;

		var fp = ResolveLocalFpAnimator( GameObject );
		if ( !fp.IsValid() )
			return;

		var cid = _ownerMirrorCombatWeaponDefinitionId?.Trim() ?? "";
		var melee = ThornsFpToolCombat.TreatsAsMeleeWeapon( cid );
		fp.OwnerTickPresentation(
			false,
			reloadGameplayStartedThisTick: true,
			gameplayReloadSeconds: Math.Max( 0.05f, gameplayReloadSeconds ),
			primaryFireHeld: false,
			firingModeGraphEnum: ResolveFiringModeGraphEnum( cid ),
			driveMeleeKnifeExtras: melee && string.Equals( cid, "m9_bayonet", StringComparison.OrdinalIgnoreCase ),
			sprintHeld: false,
			shellReloadPresentation: string.Equals( cid, "shotgun", StringComparison.OrdinalIgnoreCase ),
			shellReloadAmmoBeforeRpc: -1,
			shotgunPumpSessionHeld: false );
	}

	protected override void OnDestroy()
	{
		var viewChild = ResolvePresentationRig();
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( vmc.IsValid() )
			vmc.ClearViewModel();
	}

	/// <summary>Re-read active hotbar from <see cref="ThornsPlayerGameplay"/> and refresh the FP mesh.</summary>
	public void RefreshFromActiveHotbar()
	{
		if ( !ThornsLocalPlayer.IsLocalConnectionOwner( this ) )
		{
			var pawn = ThornsLocalPlayer.ResolvePawnRoot( GameObject );
			var local = Connection.Local;
			ThornsFpDebug.WriteOnce(
				"refresh-skip-owner",
				$"RefreshFromActiveHotbar: skip (not local owner) pawn='{pawn.Name}' ownerId={pawn.Network.OwnerId} localId={local?.Id ?? Guid.Empty}" );
			return;
		}

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
		{
			ThornsFpDebug.WriteOnce( "fp-no-gameplay", "RefreshFromActiveHotbar: skip (no ThornsPlayerGameplay)" );
			return;
		}

		if ( !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
		{
			if ( string.IsNullOrEmpty( _clientMirrorActiveItemId ) && IsHotbarPresentationSatisfied() )
				return;

			_clientMirrorActiveItemId = "";
			ThornsFpDebug.WriteOnce( "fp-idle-hands", "RefreshFromActiveHotbar: empty hotbar → idle hands" );
			ApplyOwnerEquipmentPresentation(
				true,
				"",
				ThornsFpToolCombat.CombatIdBareHands,
				Vector3.Zero,
				ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
				Vector3.Zero,
				"" );
			return;
		}

		itemId = itemId.Trim();
		if ( string.Equals( itemId, _clientMirrorActiveItemId, StringComparison.OrdinalIgnoreCase )
		     && IsHotbarPresentationSatisfied() )
			return;

		ThornsFpDebug.WriteOnce( $"fp-item-{itemId}", $"RefreshFromActiveHotbar: item='{itemId}'" );
		PresentItemFromDefinition( itemId );
	}

	void PresentItemFromDefinition( string itemId )
	{
		var weaponChanged = !string.Equals( itemId, _clientMirrorActiveItemId, StringComparison.OrdinalIgnoreCase );
		_clientMirrorActiveItemId = itemId;

		if ( weaponChanged )
			GameObject.Components.Get<ThornsPlayerWeaponCombat>()?.ResetClientCombatPresentation();

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			ApplyOwnerEquipmentPresentation( false, "", "", Vector3.Zero, ThornsItemRegistry.FpViewmodelRootLocalScaleOne, Vector3.Zero, itemId );
			return;
		}

		if ( def.ItemType == ThornsItemType.Weapon )
		{
			var combat = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
				? itemId
				: def.CombatWeaponDefinitionId.Trim();
			var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
			ApplyOwnerEquipmentPresentation(
				true,
				vm,
				combat,
				def.FpViewmodelRootLocalOffset,
				ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale ),
				def.FpViewmodelRootLocalEulerDegrees,
				itemId );
			return;
		}

		if ( def.ItemType == ThornsItemType.Tool && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			var combat = ThornsFpToolCombat.GetCombatDefinitionIdForToolItemId( itemId );
			ApplyOwnerEquipmentPresentation(
				true,
				def.ViewModelAsset,
				combat,
				ThornsItemRegistry.ComposeFpHarvestToolViewmodelOffset( in def ),
				ThornsItemRegistry.ResolveFpHarvestToolViewmodelScale( in def ),
				ThornsItemRegistry.ResolveFpHarvestToolViewmodelEulerDegrees( in def ),
				itemId );
			return;
		}

		if ( def.ItemType == ThornsItemType.Consumable
		     && ThornsItemRegistry.IsUsableConsumable( def )
		     && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			ApplyOwnerEquipmentPresentation(
				true,
				def.ViewModelAsset,
				"",
				def.FpViewmodelRootLocalOffset,
				ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale ),
				def.FpViewmodelRootLocalEulerDegrees,
				itemId );
			return;
		}

		ApplyOwnerEquipmentPresentation(
			false,
			"",
			"",
			Vector3.Zero,
			ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
			Vector3.Zero,
			itemId );
	}

	void TryEnsureFirstPersonWeaponPresentation()
	{
		if ( _fpPresentationEnsureAttempts >= 30 )
			return;

		if ( Time.Now - _lastFpPresentationEnsureRealtime < 0.22 )
			return;

		var viewChild = ResolvePresentationRig();
		if ( !viewChild.IsValid() )
			return;

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( vmc.IsValid() && vmc.HasActiveViewModel && IsHotbarPresentationSatisfied() )
		{
			_fpPresentationEnsureAttempts = 0;
			return;
		}

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return;

		_lastFpPresentationEnsureRealtime = Time.Now;
		_fpPresentationEnsureAttempts++;
		RefreshFromActiveHotbar();
		_fpPresentationEnsureAttempts = 0;
	}

	public bool ClientMirrorFpPresentationAllowsCombatLayers( string combatIdOverride = null )
	{
		var cid = combatIdOverride?.Trim() ?? _ownerMirrorCombatWeaponDefinitionId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( cid ) )
			return true;

		if ( ThornsFpToolCombat.IsToolMeleeCombatId( cid ) )
			return true;

		if ( !ThornsViewModelController.CombatDefinitionUsesStockFpAnimator( cid ) )
			return true;

		var fp = ResolveLocalFpAnimator( GameObject );
		return fp.IsValid() && fp.PresentationAllowsCombatFire;
	}

	public void ApplyOwnerEquipmentPresentation(
		bool showWeaponFp,
		string viewModelAsset,
		string combatWeaponDefinitionId,
		Vector3 fpViewmodelRootLocalOffset = default,
		Vector3 fpViewmodelRootLocalScale = default,
		Vector3 fpViewmodelRootLocalEulerDegrees = default,
		string fpPoseSourceItemId = "" )
	{
		if ( !string.IsNullOrWhiteSpace( combatWeaponDefinitionId ) )
			_ownerMirrorCombatWeaponDefinitionId = combatWeaponDefinitionId.Trim();

		var poseItemId = fpPoseSourceItemId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( poseItemId ) )
			poseItemId = _clientMirrorActiveItemId?.Trim() ?? "";

		if ( showWeaponFp
		     && !string.IsNullOrWhiteSpace( poseItemId )
		     && ThornsItemRegistry.TryGet( poseItemId, out var poseDef ) )
		{
			if ( poseDef.ItemType == ThornsItemType.Tool || ThornsItemRegistry.IsUsableConsumable( poseDef ) )
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

		var viewChild = ResolvePresentationRig();
		if ( !viewChild.IsValid() )
		{
			ThornsFpDebug.Write( "ApplyOwnerEquipmentPresentation: no presentation rig" );
			return;
		}

		var vmc = viewChild.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() )
			vmc = viewChild.Components.Create<ThornsViewModelController>();

		if ( string.IsNullOrEmpty( viewModelAsset ) )
		{
			var bareHands = showWeaponFp && string.IsNullOrWhiteSpace( poseItemId );
			if ( bareHands )
			{
				if ( vmc.IsPresentingIdleArms() )
					return;

				ThornsFpDebug.WriteOnce( "fp-apply-idle", "ApplyOwnerEquipmentPresentation → idle arms" );
				vmc.PresentEmptyFirstPersonHands();
			}
			else
			{
				if ( !vmc.HasActiveViewModel )
					return;

				ThornsFpDebug.WriteOnce( "fp-apply-clear", "ApplyOwnerEquipmentPresentation → clear" );
				vmc.ClearViewModel();
			}

			return;
		}

		if ( !showWeaponFp )
		{
			if ( !vmc.HasActiveViewModel )
				return;

			vmc.ClearViewModel();
			return;
		}

		var vmPath = ThornsViewModelController.ResolveStockCombatViewModelPath( combatWeaponDefinitionId, viewModelAsset );
		if ( vmc.IsPresentingModelPath( vmPath ) )
		{
			var gameplay = Components.Get<ThornsPlayerGameplay>();
			vmc.SyncAttachments( combatWeaponDefinitionId, ResolveOwnerAttachmentsForPresentation( gameplay ) );
			vmc.ResumeAutomaticViewmodelTransform();
			return;
		}

		ThornsFpDebug.WriteOnce(
			$"fp-apply-{vmPath}",
			$"ApplyOwnerEquipmentPresentation → spawn vm='{vmPath}' combat='{combatWeaponDefinitionId}' rig='{viewChild.Name}'" );

		vmc.SpawnViewModel( vmPath );
	}

	bool IsHotbarPresentationSatisfied()
	{
		var rig = ResolvePresentationRig();
		if ( !rig.IsValid() )
			return false;

		var vmc = rig.Components.Get<ThornsViewModelController>();
		if ( !vmc.IsValid() || !vmc.HasActiveViewModel )
			return false;

		var gameplay = Components.Get<ThornsPlayerGameplay>();
		if ( !gameplay.IsValid() )
			return false;

		if ( !gameplay.TryGetActiveHotbarItemId( out var itemId ) )
			return vmc.IsPresentingIdleArms();

		return TryGetResolvedViewModelPathForItem( itemId.Trim(), out var path )
		       && vmc.IsPresentingModelPath( path );
	}

	bool TryGetResolvedViewModelPathForItem( string itemId, out string vmPath )
	{
		vmPath = "";
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		if ( def.ItemType == ThornsItemType.Weapon )
		{
			var combat = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId )
				? itemId
				: def.CombatWeaponDefinitionId.Trim();
			var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
			vmPath = ThornsViewModelController.ResolveStockCombatViewModelPath( combat, vm );
			return true;
		}

		if ( def.ItemType == ThornsItemType.Tool && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			vmPath = def.ViewModelAsset;
			return true;
		}

		if ( def.ItemType == ThornsItemType.Consumable
		     && ThornsItemRegistry.IsUsableConsumable( def )
		     && !string.IsNullOrEmpty( def.ViewModelAsset ) )
		{
			vmPath = def.ViewModelAsset;
			return true;
		}

		return false;
	}

	void TickFpViewmodelSequences()
	{
		var fp = ResolveLocalFpAnimator( GameObject );
		if ( !fp.IsValid() )
			return;

		var cid = _ownerMirrorCombatWeaponDefinitionId?.Trim() ?? "";
		if ( string.IsNullOrWhiteSpace( cid ) )
			return;

		var meleeEquipped = ThornsFpToolCombat.TreatsAsMeleeWeapon( cid );
		var wantAds = !meleeEquipped && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var primaryHeld = !meleeEquipped && (Input.Down( "Attack1" ) || Input.Down( "attack1" ));
		var sprintHeld = ThornsPlayerMovementDefaults.ResolveSprintHeld( GameObject );
		var combat = GameObject.Components.Get<ThornsPlayerWeaponCombat>();
		var shotgunPump = combat.IsValid() && combat.MirrorReloading
		                  && string.Equals( cid, "shotgun", StringComparison.OrdinalIgnoreCase );

		ThornsPlayerMovementDefaults.TryResolvePresentationLocomotion(
			GameObject,
			out var crouching,
			out var eyeAngles,
			out var planarVelocity,
			out var grounded,
			out var runSpeed );

		fp.OwnerTickPresentation(
			wantAds,
			reloadGameplayStartedThisTick: false,
			gameplayReloadSeconds: 2f,
			primaryFireHeld: primaryHeld,
			firingModeGraphEnum: ResolveFiringModeGraphEnum( cid ),
			driveMeleeKnifeExtras: meleeEquipped && string.Equals( cid, "m9_bayonet", StringComparison.OrdinalIgnoreCase ),
			sprintHeld: sprintHeld,
			shellReloadPresentation: false,
			shellReloadAmmoBeforeRpc: -1,
			shotgunPumpSessionHeld: shotgunPump,
			crouching: crouching,
			eyeAngles: eyeAngles,
			velocityWorld: planarVelocity,
			grounded: grounded,
			runSpeed: runSpeed );
	}

	GameObject ResolvePresentationRig() => ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( GameObject );

	static ThornsViewModelFpAnimator ResolveLocalFpAnimator( GameObject pawnRoot )
	{
		var view = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( pawnRoot );
		if ( !view.IsValid() )
			return default;

		var wm = FindChild( view, "WeaponViewmodel" );
		if ( !wm.IsValid() )
			return default;

		return wm.Components.Get<ThornsViewModelFpAnimator>();
	}

	static GameObject FindChild( GameObject root, string name )
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

	static int ResolveFiringModeGraphEnum( string combatId )
	{
		if ( string.IsNullOrWhiteSpace( combatId ) )
			return 1;

		var def = ThornsWeaponDefinitions.Get( combatId.Trim() );
		return ThornsWeaponDefinitions.FiringModeGraphValue( def );
	}

	static IReadOnlyList<ThornsAttachmentId> ResolveOwnerAttachmentsForPresentation( ThornsPlayerGameplay gameplay )
	{
		if ( !gameplay.IsValid() || !gameplay.TryGetActiveHotbarIndex( out var hotbar ) )
			return Array.Empty<ThornsAttachmentId>();

		var stack = gameplay.GetHotbarSlot( hotbar );
		return ThornsWeaponAttachmentState.GetAttachments( stack );
	}
}
