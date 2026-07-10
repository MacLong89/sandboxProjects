namespace Sandbox;

/// <summary>
/// Server-authoritative hotbar selection (slots 0–7) and equip binding to inventory rows.
/// THORNS_EVERYTHING_DOCUMENT: weapon instance state, host inventory authority; owner-only equip sync (no inventory broadcast).
/// </summary>
[Title( "YouAreNotAlone — Hotbar" )]
[Category( "YouAreNotAlone" )]
[Icon( "touch_app" )]
[Order( 140 )]
public sealed class YaHotbarEquipment : Component
{
	/// <summary>Host-only active combat tuning id (maps to <see cref="YaWeaponDefinitions"/>). Empty = cannot fire hitscan.</summary>
	string _serverCombatWeaponDefinitionId = "";

	string _serverActiveItemId = "";
	string _serverWeaponInstanceId = "";
	int _serverSelectedHotbarIndex = -1;

	/// <summary>Client mirror for UX / fire prediction (non-authoritative).</summary>
	string _clientCombatWeaponDefinitionId = "";

	public bool ClientMirrorCanFireHitscan => !string.IsNullOrEmpty( _clientCombatWeaponDefinitionId );

	public int ClientMirrorSelectedHotbar => _clientSelectedHotbar;
	public string ClientMirrorActiveItemId => _clientActiveItemId;

	/// <summary>Owner-only: combat id from last <see cref="ClientReceiveEquipmentState"/> — actual hotbar weapon (not third-person mimic presentation).</summary>
	public string ClientMirrorEquippedCombatWeaponDefinitionId => _clientCombatWeaponDefinitionId ?? "";

	int _clientSelectedHotbar = -1;
	string _clientActiveItemId = "";

	/// <summary>Host-replicated combat def id for third-person hold pose; visible to all clients (owner FP mirror stays owner-RPC).</summary>
	[Sync( SyncFlags.FromHost )] public string ObserversCombatWeaponDefinitionId { get; set; } = "";

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( roleCmp.IsValid() && roleCmp.Role == YaPlayerRole.Alone )
			return;

		var inv = Components.Get<YaGameInventory>();

		for ( var i = 0; i < YaGameInventory.HotbarSlotCount; i++ )
		{
			if ( !IsHotbarKeyPressed( i ) )
				continue;

			Log.Info( $"[YA] Hotbar select request (client intent): slot={i}" );
			RequestSelectHotbarSlot( i );
		}

		var mw = Input.MouseWheel;
		if ( MathF.Abs( mw.y ) > 0.01f && inv.IsValid() )
		{
			var dir = mw.y > 0f ? 1 : -1;
			var cur = _clientSelectedHotbar >= 0 ? _clientSelectedHotbar : 0;
			for ( var k = 1; k <= YaGameInventory.HotbarSlotCount; k++ )
			{
				var idx = ( cur + dir * k + YaGameInventory.HotbarSlotCount * 16 ) % YaGameInventory.HotbarSlotCount;
				if ( inv.TryGetClientMirrorSlot( idx, out var slot ) && !string.IsNullOrEmpty( slot.ItemId ) && slot.Quantity > 0 )
				{
					if ( idx != cur )
					{
						Log.Info( $"[YA] Hotbar wheel → slot={idx}" );
						RequestSelectHotbarSlot( idx );
					}
					break;
				}
			}
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
		Log.Info( $"[YA] Hotbar select RPC received: slot={slotIndex}, caller={Rpc.Caller?.Id}, calling={Rpc.Calling}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[YA] Hotbar select rejected: caller does not own pawn" );
			return;
		}

		HostApplyHotbarSlot( slotIndex, requireAlive: true );
	}

	/// <summary>Host-only: equip hotbar index (used by round loadout and RPC path).</summary>
	public void HostApplyHotbarSlot( int slotIndex, bool requireAlive = true )
	{
		if ( !Networking.IsHost )
			return;

		var health = Components.Get<YaPlayerHealth>();
		if ( requireAlive && health.IsValid() && ( health.IsDeadState || !health.IsAlive ) )
		{
			Log.Warning( "[YA] Hotbar apply rejected: dead" );
			return;
		}

		if ( slotIndex < 0 || slotIndex >= YaGameInventory.HotbarSlotCount )
		{
			Log.Warning( $"[YA] Hotbar apply rejected: invalid slot {slotIndex}" );
			return;
		}

		var inv = Components.Get<YaGameInventory>();
		if ( !inv.IsValid() )
		{
			Log.Warning( "[YA] Hotbar apply rejected: no inventory" );
			return;
		}

		if ( !inv.TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty )
		{
			Log.Warning( $"[YA] Hotbar apply rejected: empty slot {slotIndex}" );
			return;
		}

		if ( !YaWeaponItemCatalog.TryGet( slot.ItemId, out var def ) )
		{
			if ( string.Equals( slot.ItemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new YaWeaponItemCatalog.YaItemDefinition(
					Id: "sniper",
					DisplayName: "Sniper",
					MaxStack: 1,
					ItemType: YaItemType.Weapon,
					CombatWeaponDefinitionId: "sniper",
					ViewModelAsset: YaViewModelController.SniperFirstPersonViewmodelPath,
					WorldModelAsset: YaViewModelController.SniperWorldModelPath );
				Log.Warning( "[YA] Hotbar select: using resilient fallback definition for 'sniper' (runtime registry stale)." );
			}
			else if ( string.Equals( slot.ItemId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new YaWeaponItemCatalog.YaItemDefinition(
					Id: "m9_bayonet",
					DisplayName: "M9 Bayonet",
					MaxStack: 1,
					ItemType: YaItemType.Weapon,
					CombatWeaponDefinitionId: "m9_bayonet",
					ViewModelAsset: YaViewModelController.BayonetM9FirstPersonViewmodelPath,
					WorldModelAsset: YaViewModelController.BayonetM9WorldModelPath );
				Log.Warning( "[YA] Hotbar select: using resilient fallback definition for 'm9_bayonet' (runtime registry stale)." );
			}
			else
			{
				Log.Warning( $"[YA] Hotbar select rejected: unknown item '{slot.ItemId}'" );
				return;
			}
		}

		_serverSelectedHotbarIndex = slotIndex;
		_serverActiveItemId = slot.ItemId;

		var weaponCmp = Components.Get<YaWeapon>();

		if ( def.ItemType == YaItemType.Weapon )
		{
			if ( !inv.ServerEnsureWeaponInstanceForHotbarSlot( slotIndex ) )
			{
				Log.Warning( "[YA] Hotbar equip rejected: weapon instance binding failed" );
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

			// Third-person world presentation is now driven on each client from ObserversCombatWeaponDefinitionId.

			Log.Info( $"[YA] Item equipped (weapon): slot={slotIndex} item={slot.ItemId} instance={_serverWeaponInstanceId} combatDef={combatId}" );

			ObserversCombatWeaponDefinitionId = combatId;

			PushEquipmentToOwner( slotIndex, slot.ItemId, _serverWeaponInstanceId, combatId, showWeaponFp: true, vm );

			if ( weaponCmp.IsValid() )
			{
				weaponCmp.HostResetCooldownAfterWeaponEquip();
				weaponCmp.HostPushWeaponHudFromInventory();
				weaponCmp.HostApplyEquippedWorldPresentation( def, treatAsHeldWeapon: true );
			}
		}
		else
		{
			_serverWeaponInstanceId = "";
			_serverCombatWeaponDefinitionId = "";

			// Third-person world presentation is now driven on each client from ObserversCombatWeaponDefinitionId.

			Log.Info( $"[YA] Item selected (non-weapon): slot={slotIndex} item={slot.ItemId} type={def.ItemType}" );

			ObserversCombatWeaponDefinitionId = "";

			PushEquipmentToOwner( slotIndex, slot.ItemId, "", "", showWeaponFp: false, "" );

			if ( weaponCmp.IsValid() )
			{
				weaponCmp.HostOnSelectedNonWeapon();
				weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
			}
		}
	}

	/// <summary>Host: clear equipped presentation between rounds (player may be alive).</summary>
	public void HostClearEquipmentForRoundReset() => HostClearEquipmentAfterDeath();

	void PushEquipmentToOwner( int selectedHotbar, string itemId, string weaponInstanceId, string combatWeaponDefId, bool showWeaponFp, string viewModelAsset )
	{
		Log.Info( $"[YA] Equipment snapshot sent (owner-only): slot={selectedHotbar} item={itemId} instance={weaponInstanceId} combat={combatWeaponDefId} showFp={showWeaponFp}" );
		ClientReceiveEquipmentState( selectedHotbar, itemId, weaponInstanceId, combatWeaponDefId, showWeaponFp ? 1 : 0, viewModelAsset ?? "" );
	}

	[Rpc.Owner]
	void ClientReceiveEquipmentState( int selectedHotbar, string itemId, string weaponInstanceId, string combatWeaponDefId, int showWeaponFpInt, string viewModelAsset )
	{
		_clientSelectedHotbar = selectedHotbar;
		_clientActiveItemId = itemId ?? "";
		_clientCombatWeaponDefinitionId = combatWeaponDefId ?? "";

		var showFp = showWeaponFpInt != 0;
		var w = Components.Get<YaWeapon>();
		if ( w.IsValid() )
			w.ApplyOwnerEquipmentPresentation( showFp, viewModelAsset, combatWeaponDefId );

		Log.Info( $"[YA] Owner viewmodel/equipment mirror updated: slot={selectedHotbar} item={itemId} instance={weaponInstanceId} combat={combatWeaponDefId} showFp={showFp}" );
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

		var weaponCmp = Components.Get<YaWeapon>();
		if ( weaponCmp.IsValid() )
		{
			weaponCmp.HostOnSelectedNonWeapon();
			weaponCmp.HostApplyEquippedWorldPresentation( null, treatAsHeldWeapon: false );
		}

		ClientReceiveEquipmentState( -1, "", "", "", 0, "" );

		Log.Info( "[YA] Hotbar/equipment cleared (death)" );
	}

	bool ValidateRpcCallerOwnsPawn() => YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	protected override void OnStart()
	{
		base.OnStart();
		_ = AutoEquipFirstWeaponAsync();
	}

	async Task AutoEquipFirstWeaponAsync()
	{
		await Task.DelayRealtimeSeconds( 0.12f );
		if ( !GameObject.IsValid() || !Game.IsPlaying )
			return;
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;
		var inv = Components.Get<YaGameInventory>();
		if ( !inv.IsValid() )
			return;
		if ( !inv.TryGetClientMirrorSlot( 0, out var s ) || string.IsNullOrEmpty( s.ItemId ) || s.Quantity <= 0 )
			return;
		RequestSelectHotbarSlot( 0 );
	}
}
