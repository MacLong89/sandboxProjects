using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// Server-authoritative hotbar selection (slots 0–7) and equip binding to inventory rows.
/// THORNS_EVERYTHING_DOCUMENT: weapon instance state, host inventory authority; owner-only equip sync (no inventory broadcast).
/// </summary>
[Title( "Thorns — Hotbar / Equipment" )]
[Category( "Thorns" )]
[Icon( "touch_app" )]
[Order( 140 )]
public sealed class ThornsHotbarEquipment : Component
{
	/// <summary>Host-only active combat tuning id (maps to <see cref="ThornsWeaponDefinitions"/>). Empty = cannot fire hitscan.</summary>
	string _serverCombatWeaponDefinitionId = "";

	string _serverActiveItemId = "";
	string _serverWeaponInstanceId = "";
	int _serverSelectedHotbarIndex = -1;

	/// <summary>Client mirror for UX / fire prediction (non-authoritative).</summary>
	string _clientCombatWeaponDefinitionId = "";

	public bool ClientMirrorCanFireHitscan => !string.IsNullOrEmpty( _clientCombatWeaponDefinitionId );

	public int ClientMirrorSelectedHotbar => _clientSelectedHotbar;
	public string ClientMirrorActiveItemId => _clientActiveItemId;

	int _clientSelectedHotbar = -1;
	string _clientActiveItemId = "";

	double _nextClientBootstrapFromObservers;
	bool _clientEquipmentBootstrapComplete;

	[Sync( SyncFlags.FromHost )] public string ObserversCombatWeaponDefinitionId { get; set; } = "";

	/// <summary>Host hotbar row <c>ItemId</c> for the equipped slot — proxies resolve <see cref="ThornsItemRegistry.ThornsItemDefinition.WorldModelAsset"/> for tools (combat id is only <c>tool_melee_*</c>).</summary>
	[Sync( SyncFlags.FromHost )] public string ObserversEquippedHotbarItemId { get; set; } = "";

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		if ( !_clientEquipmentBootstrapComplete && Time.Now >= _nextClientBootstrapFromObservers )
		{
			_nextClientBootstrapFromObservers = Time.Now + 0.12;
			ClientTryBootstrapEquipmentFromObservers();
		}

		var inv = Components.Get<ThornsInventory>();

		for ( var i = 0; i < ThornsInventory.HotbarSlotCount; i++ )
		{
			if ( !IsHotbarKeyPressed( i ) )
				continue;

			ClientApplyOptimisticHotbarSelection( i, inv );
			RequestSelectHotbarSlot( i );
		}

		var mw = Input.MouseWheel;
		if ( MathF.Abs( mw.y ) > 0.01f && inv.IsValid() )
		{
			var dir = mw.y > 0f ? 1 : -1;
			var cur = _clientSelectedHotbar >= 0 ? _clientSelectedHotbar : 0;
			var idx = ( cur + dir + ThornsInventory.HotbarSlotCount * 16 ) % ThornsInventory.HotbarSlotCount;
			if ( idx != cur )
			{
				ClientApplyOptimisticHotbarSelection( idx, inv );
				RequestSelectHotbarSlot( idx );
			}
		}
	}

	/// <summary>
	/// Owner client: apply synced host equip state locally when equip RPC / owner mirror lag after join (common first seconds).
	/// </summary>
	public void ClientTryBootstrapEquipmentFromObservers()
	{
		if ( !ThornsPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( ThornsWorldBootGate.BlocksLocalOwnerPresentation )
			return;

		var observersCombat = ObserversCombatWeaponDefinitionId?.Trim() ?? "";
		if ( string.IsNullOrEmpty( observersCombat ) )
			return;

		var ownerMirror = Components.Get<ThornsWeapon>()?.ClientMirrorCombatDefinitionId?.Trim() ?? "";
		if ( !string.IsNullOrEmpty( ownerMirror )
		     && string.Equals( ownerMirror, observersCombat, StringComparison.OrdinalIgnoreCase )
		     && !string.IsNullOrEmpty( _clientCombatWeaponDefinitionId ) )
		{
			_clientEquipmentBootstrapComplete = true;
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		var slotIndex = _clientSelectedHotbar >= 0 ? _clientSelectedHotbar : 0;
		var itemId = ObserversEquippedHotbarItemId?.Trim() ?? "";

		if ( string.IsNullOrEmpty( itemId ) && inv.IsValid() && inv.TryGetClientMirrorSlot( slotIndex, out var net )
		     && net.Quantity > 0 && !string.IsNullOrWhiteSpace( net.ItemId ) )
			itemId = net.ItemId.Trim();

		ClientApplyOptimisticHotbarSelection( slotIndex, inv, itemId, observersCombat );
		_clientEquipmentBootstrapComplete = true;
	}

	/// <summary>Owner client: immediate hotbar UX while host equip RPC is in flight.</summary>
	public void ClientApplyOptimisticHotbarSelection( int slotIndex, ThornsInventory inv, string itemIdOverride = null, string combatIdOverride = null )
	{
		if ( slotIndex < 0 || slotIndex >= ThornsInventory.HotbarSlotCount )
			return;

		_clientSelectedHotbar = slotIndex;

		var itemId = itemIdOverride?.Trim() ?? "";
		if ( string.IsNullOrEmpty( itemId ) && inv.IsValid() && inv.TryGetClientMirrorSlot( slotIndex, out var net )
		     && net.Quantity > 0 && !string.IsNullOrWhiteSpace( net.ItemId ) )
			itemId = net.ItemId.Trim();

		_clientActiveItemId = itemId;

		var combatId = combatIdOverride?.Trim() ?? "";
		if ( string.IsNullOrEmpty( combatId ) )
		{
			if ( string.IsNullOrEmpty( itemId ) )
				combatId = ThornsToolMeleeCombat.CombatIdPrimitive;
			else if ( ThornsItemRegistry.TryGet( itemId, out var def ) )
			{
				if ( def.ItemType == ThornsItemType.Tool )
					combatId = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( itemId ) ?? "";
				else if ( def.ItemType == ThornsItemType.Weapon )
					combatId = string.IsNullOrWhiteSpace( def.CombatWeaponDefinitionId ) ? itemId : def.CombatWeaponDefinitionId.Trim();
			}
		}

		if ( string.IsNullOrEmpty( combatId ) )
			return;

		_clientCombatWeaponDefinitionId = combatId;

		var showFp = false;
		var vm = "";
		var fpOff = Vector3.Zero;
		var fpEu = Vector3.Zero;
		var fpSc = ThornsItemRegistry.FpViewmodelRootLocalScaleOne;

		if ( string.IsNullOrEmpty( itemId ) )
		{
			showFp = true;
		}
		else if ( ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			if ( def.ItemType == ThornsItemType.Tool )
			{
				vm = def.ViewModelAsset ?? "";
				showFp = !string.IsNullOrEmpty( vm );
				if ( showFp )
					MirrorFpPoseFromItemDef( def, out fpOff, out fpEu, out fpSc );
			}
			else if ( def.ItemType == ThornsItemType.Weapon )
			{
				vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
				showFp = true;
				MirrorFpPoseFromItemDef( def, out fpOff, out fpEu, out fpSc );
			}
			else if ( ThornsItemRegistry.IsUsableConsumable( def ) && !string.IsNullOrWhiteSpace( def.ViewModelAsset ) )
			{
				vm = def.ViewModelAsset;
				showFp = true;
				MirrorFpPoseFromItemDef( def, out fpOff, out fpEu, out fpSc );
			}
		}

		var weapon = Components.Get<ThornsWeapon>();
		if ( weapon.IsValid() )
		{
			weapon.ApplyOwnerEquipmentPresentation(
				showFp,
				vm,
				combatId,
				fpOff,
				fpSc,
				fpEu,
				itemId );
		}
	}

	static bool IsHotbarKeyPressed( int slotZeroBased )
	{
		var key = (slotZeroBased + 1).ToString();
		return Input.Keyboard.Pressed( key );
	}

	/// <summary>Host combat validation — only meaningful on server.</summary>
	public string ServerGetActiveCombatWeaponDefinitionId()
	{
		if ( !Networking.IsHost )
			return "";
		return _serverCombatWeaponDefinitionId ?? "";
	}

	/// <summary>Host-only selected hotbar index (0–7), or -1 if unset.</summary>
	public int ServerGetSelectedHotbarIndex()
	{
		if ( !Networking.IsHost )
			return -1;
		return _serverSelectedHotbarIndex;
	}

	[Rpc.Host]
	public void RequestSelectHotbarSlot( int slotIndex )
	{

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			return;
		}

		var health = Components.Get<ThornsHealth>();
		if ( health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			return;
		}

		if ( slotIndex < 0 || slotIndex >= ThornsInventory.HotbarSlotCount )
		{
			return;
		}

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
		{
			return;
		}

		if ( !inv.TryGetHostSlot( slotIndex, out var slot ) )
		{
			return;
		}

		if ( slot.IsEmpty )
		{
			HostCommitEmptyHotbarSlot( slotIndex );
			return;
		}

		if ( !TryResolveHotbarItemDefinition( slot.ItemId, out var def ) )
		{
			return;
		}

		HostCommitHotbarSelection( slotIndex, inv, def );
	}

	/// <summary>Host-only: equip a hotbar row after inventory init (no RPC — <see cref="ThornsInventory.OnNetworkSpawn"/>).</summary>
	public void HostTryEquipHotbarSlotAfterSpawn( int slotIndex )
	{
		if ( !Networking.IsHost )
			return;

		if ( slotIndex < 0 || slotIndex >= ThornsInventory.HotbarSlotCount )
			return;

		var inv = Components.Get<ThornsInventory>();
		if ( !inv.IsValid() )
			return;

		if ( !inv.TryGetHostSlot( slotIndex, out var slot ) )
			return;

		if ( slot.IsEmpty )
		{
			HostCommitEmptyHotbarSlot( slotIndex );
			return;
		}

		if ( !TryResolveHotbarItemDefinition( slot.ItemId, out var def ) )
			return;

		HostCommitHotbarSelection( slotIndex, inv, def );
	}

	/// <summary>Host: select an empty toolbar row — bare hands FP + same melee/harvest profile as <see cref="ThornsItemRegistry.PrimitiveToolDefinition"/>.</summary>
	void HostCommitEmptyHotbarSlot( int slotIndex )
	{
		_serverSelectedHotbarIndex = slotIndex;
		_serverActiveItemId = "";
		_serverWeaponInstanceId = "";
		var handsCombat = ThornsToolMeleeCombat.CombatIdPrimitive;
		_serverCombatWeaponDefinitionId = handsCombat;
		ObserversCombatWeaponDefinitionId = handsCombat;
		ObserversEquippedHotbarItemId = "";

		var weaponCmp = Components.Get<ThornsWeapon>();
		// showWeaponFp true + empty vm → <see cref="ThornsWeapon.ApplyOwnerEquipmentPresentation"/> presents idle arms only.
		PushEquipmentToOwner(
			slotIndex,
			"",
			"",
			handsCombat,
			showWeaponFp: true,
			"",
			Vector3.Zero,
			ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
			Vector3.Zero );

		if ( weaponCmp.IsValid() )
		{
			weaponCmp.HostOnSelectedNonWeapon();
			weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
		}
	}

	static bool TryResolveHotbarItemDefinition( string itemId, out ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( ThornsItemRegistry.TryGet( itemId, out def ) )
			return true;

		if ( string.Equals( itemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
		{
			def = new ThornsItemRegistry.ThornsItemDefinition(
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
			def = new ThornsItemRegistry.ThornsItemDefinition(
				Id: "m9_bayonet",
				DisplayName: "M9 Bayonet",
				MaxStack: 1,
				ItemType: ThornsItemType.Weapon,
				CombatWeaponDefinitionId: "m9_bayonet",
				ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
				WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath );
			return true;
		}

		if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
		{
			def = ThornsItemRegistry.PrimitiveToolDefinition;
			return true;
		}

		def = default;
		return false;
	}

	static void MirrorFpPoseFromItemDef( ThornsItemRegistry.ThornsItemDefinition def, out Vector3 offset, out Vector3 euler, out Vector3 scale )
	{
		offset = def.ItemType == ThornsItemType.Tool
			? ThornsItemRegistry.ComposeFpHarvestToolViewmodelOffset( in def )
			: def.FpViewmodelRootLocalOffset;
		euler = def.ItemType == ThornsItemType.Tool
			? ThornsItemRegistry.ResolveFpHarvestToolViewmodelEulerDegrees( in def )
			: def.FpViewmodelRootLocalEulerDegrees;
		scale = def.ItemType == ThornsItemType.Tool
			? ThornsItemRegistry.ResolveFpHarvestToolViewmodelScale( in def )
			: ThornsItemRegistry.ResolveFpViewmodelRootScale( def.FpViewmodelRootLocalScale );
	}

	void HostCommitHotbarSelection( int slotIndex, ThornsInventory inv, ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( !inv.TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty )
			return;

		_serverSelectedHotbarIndex = slotIndex;
		_serverActiveItemId = slot.ItemId;

		var weaponCmp = Components.Get<ThornsWeapon>();

		if ( def.ItemType == ThornsItemType.Weapon )
		{
			if ( !inv.ServerEnsureWeaponInstanceForHotbarSlot( slotIndex ) )
			{
				return;
			}

			if ( !inv.TryGetHostSlot( slotIndex, out slot ) )
				return;

			_serverWeaponInstanceId = slot.WeaponInstanceId ?? "";
			_serverCombatWeaponDefinitionId = string.IsNullOrEmpty( def.CombatWeaponDefinitionId )
				? slot.ItemId
				: def.CombatWeaponDefinitionId;

			var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "models/dev/box.vmdl" : def.ViewModelAsset;
			var combatId = _serverCombatWeaponDefinitionId;


			ObserversCombatWeaponDefinitionId = combatId;
			ObserversEquippedHotbarItemId = slot.ItemId ?? "";

			MirrorFpPoseFromItemDef( def, out var fpOff, out var fpEu, out var fpSc );
			PushEquipmentToOwner( slotIndex, slot.ItemId, _serverWeaponInstanceId, combatId, showWeaponFp: true, vm, fpOff, fpSc, fpEu );

			if ( weaponCmp.IsValid() )
			{
				weaponCmp.HostResetCooldownAfterWeaponEquip();
				weaponCmp.HostPushWeaponHudFromInventory();
				weaponCmp.HostApplyEquippedWorldPresentation( def, treatAsHeldWeapon: true );
			}
		}
		else if ( def.ItemType == ThornsItemType.Tool )
		{
			var toolCombat = ThornsToolMeleeCombat.GetCombatDefinitionIdForToolItemId( slot.ItemId );
			_serverWeaponInstanceId = "";
			_serverCombatWeaponDefinitionId = toolCombat;
			ObserversCombatWeaponDefinitionId = toolCombat;

			var vm = string.IsNullOrEmpty( def.ViewModelAsset ) ? "" : def.ViewModelAsset;
			var showToolFp = !string.IsNullOrEmpty( vm );
			var fpOff = Vector3.Zero;
			var fpEu = Vector3.Zero;
			var fpSc = ThornsItemRegistry.FpViewmodelRootLocalScaleOne;
			if ( showToolFp )
				MirrorFpPoseFromItemDef( def, out fpOff, out fpEu, out fpSc );
			PushEquipmentToOwner( slotIndex, slot.ItemId, "", toolCombat, showWeaponFp: showToolFp, vm, fpOff, fpSc, fpEu );

			if ( weaponCmp.IsValid() )
			{
				if ( string.IsNullOrEmpty( toolCombat ) )
					weaponCmp.HostOnSelectedNonWeapon();
				else
				{
					weaponCmp.HostResetCooldownAfterWeaponEquip();
					weaponCmp.HostPushWeaponHudFromInventory();
				}

				weaponCmp.HostApplyEquippedWorldPresentation( def, treatAsHeldWeapon: false );
			}
		}
		else if ( def.ItemType == ThornsItemType.Consumable
		          && ThornsItemRegistry.IsUsableConsumable( def )
		          && !string.IsNullOrWhiteSpace( def.ViewModelAsset ) )
		{
			_serverWeaponInstanceId = "";
			_serverCombatWeaponDefinitionId = "";
			ObserversCombatWeaponDefinitionId = "";
			ObserversEquippedHotbarItemId = slot.ItemId ?? "";

			MirrorFpPoseFromItemDef( def, out var fpOff, out var fpEu, out var fpSc );
			PushEquipmentToOwner( slotIndex, slot.ItemId, "", "", showWeaponFp: true, def.ViewModelAsset, fpOff, fpSc, fpEu );

			if ( weaponCmp.IsValid() )
			{
				weaponCmp.HostOnSelectedNonWeapon();
				weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
			}
		}
		else
		{
			_serverWeaponInstanceId = "";
			_serverCombatWeaponDefinitionId = "";


			ObserversCombatWeaponDefinitionId = "";

			PushEquipmentToOwner(
				slotIndex,
				slot.ItemId,
				"",
				"",
				showWeaponFp: false,
				"",
				Vector3.Zero,
				ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
				Vector3.Zero );

			if ( weaponCmp.IsValid() )
			{
				weaponCmp.HostOnSelectedNonWeapon();
				weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
			}
		}
	}

	static bool HostEquipmentTargetsListenServerLocalOwner( GameObject go )
	{
		if ( !Networking.IsActive )
			return true;
		var local = Connection.Local;
		return local is not null && local.Id == go.Network.OwnerId;
	}

	void ApplyOwnerEquipmentMirror(
		int selectedHotbar,
		string itemId,
		string weaponInstanceId,
		string combatWeaponDefId,
		int showWeaponFpInt,
		string viewModelAsset,
		Vector3 fpViewmodelRootLocalOffset,
		Vector3 fpViewmodelRootLocalScale,
		Vector3 fpViewmodelRootLocalEulerDegrees )
	{
		_ = weaponInstanceId;
		_clientSelectedHotbar = selectedHotbar;
		_clientActiveItemId = itemId ?? "";
		_clientCombatWeaponDefinitionId = combatWeaponDefId ?? "";
		_clientEquipmentBootstrapComplete = !string.IsNullOrWhiteSpace( combatWeaponDefId );

		var showFp = showWeaponFpInt != 0;
		if ( ThornsWeaponResourceLoad.FpViewmodelDiagnosticLogs )
		{
			Log.Info(
				$"[Thorns][FP-Mirror] hotbar={selectedHotbar} item='{itemId}' showFp={showFp} vm='{viewModelAsset}' combat='{combatWeaponDefId}' fpOff={fpViewmodelRootLocalOffset} fpEu={fpViewmodelRootLocalEulerDegrees} fpSc={fpViewmodelRootLocalScale}" );
		}

		var w = Components.Get<ThornsWeapon>();
		if ( w.IsValid() )
			w.ApplyOwnerEquipmentPresentation(
				showFp,
				viewModelAsset,
				combatWeaponDefId,
				fpViewmodelRootLocalOffset,
				fpViewmodelRootLocalScale,
				fpViewmodelRootLocalEulerDegrees,
				itemId ?? "" );
	}

	void PushEquipmentToOwner(
		int selectedHotbar,
		string itemId,
		string weaponInstanceId,
		string combatWeaponDefId,
		bool showWeaponFp,
		string viewModelAsset,
		Vector3 fpViewmodelRootLocalOffset,
		Vector3 fpViewmodelRootLocalScale,
		Vector3 fpViewmodelRootLocalEulerDegrees )
	{
		var fp = showWeaponFp ? 1 : 0;
		var vm = viewModelAsset ?? "";
		if ( HostEquipmentTargetsListenServerLocalOwner( GameObject ) )
			ApplyOwnerEquipmentMirror(
				selectedHotbar,
				itemId,
				weaponInstanceId,
				combatWeaponDefId,
				fp,
				vm,
				fpViewmodelRootLocalOffset,
				fpViewmodelRootLocalScale,
				fpViewmodelRootLocalEulerDegrees );
		else
			ClientReceiveEquipmentState(
				selectedHotbar,
				itemId,
				weaponInstanceId,
				combatWeaponDefId,
				fp,
				vm,
				fpViewmodelRootLocalOffset,
				fpViewmodelRootLocalScale,
				fpViewmodelRootLocalEulerDegrees );
	}

	[Rpc.Owner]
	void ClientReceiveEquipmentState(
		int selectedHotbar,
		string itemId,
		string weaponInstanceId,
		string combatWeaponDefId,
		int showWeaponFpInt,
		string viewModelAsset,
		Vector3 fpViewmodelRootLocalOffset,
		Vector3 fpViewmodelRootLocalScale,
		Vector3 fpViewmodelRootLocalEulerDegrees )
	{
		ApplyOwnerEquipmentMirror(
			selectedHotbar,
			itemId,
			weaponInstanceId,
			combatWeaponDefId,
			showWeaponFpInt,
			viewModelAsset,
			fpViewmodelRootLocalOffset,
			fpViewmodelRootLocalScale,
			fpViewmodelRootLocalEulerDegrees );
	}

	/// <summary>Host: reset hotbar/equipment selection after death strip.</summary>
	public void HostClearEquipmentAfterDeath()
	{
		if ( !Networking.IsHost )
			return;

		_serverSelectedHotbarIndex = -1;
		_serverActiveItemId = "";
		_serverWeaponInstanceId = "";
		_serverCombatWeaponDefinitionId = "";
		ObserversCombatWeaponDefinitionId = "";
		ObserversEquippedHotbarItemId = "";

		var weaponCmp = Components.Get<ThornsWeapon>();
		if ( weaponCmp.IsValid() )
		{
			weaponCmp.HostOnSelectedNonWeapon();
			weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
		}

		if ( HostEquipmentTargetsListenServerLocalOwner( GameObject ) )
			ApplyOwnerEquipmentMirror(
				-1,
				"",
				"",
				"",
				0,
				"",
				Vector3.Zero,
				ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
				Vector3.Zero );
		else
			ClientReceiveEquipmentState(
				-1,
				"",
				"",
				"",
				0,
				"",
				Vector3.Zero,
				ThornsItemRegistry.FpViewmodelRootLocalScaleOne,
				Vector3.Zero );

	}

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );
}
