namespace Sandbox;

/// <summary>Host-authoritative 8 hotbar slots + 1 hidden reserve slot (ammo).</summary>
[Title( "YouAreNotAlone — Game Inventory" )]
[Category( "YouAreNotAlone" )]
[Icon( "inventory_2" )]
[Order( 75 )]
public sealed class YaGameInventory : Component, Component.INetworkSpawn
{
	public const int HotbarSlotCount = 8;

	/// <summary>Hidden reserve row (slot index <see cref="HotbarSlotCount"/> = ammo stack).</summary>
	public const int BackpackSlotCount = 1;
	public const int TotalSlots = HotbarSlotCount + BackpackSlotCount;

	readonly YaInventorySlot[] _slots = new YaInventorySlot[TotalSlots];
	YaInventorySlotNet[] _clientMirror;

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !Networking.IsHost )
			return;

		HostGrantEmptyLoadout();
		_ = PushSnapshotAfterSpawnAsync();
	}

	async System.Threading.Tasks.Task PushSnapshotAfterSpawnAsync()
	{
		await Task.DelayRealtimeSeconds( 0.08f );
		if ( GameObject.IsValid() && Networking.IsHost )
			PushSnapshotToOwner();
	}

	void HostGrantEmptyLoadout()
	{
		for ( var i = 0; i < TotalSlots; i++ )
			_slots[i] = YaInventorySlot.Empty;
	}

	/// <summary>Host: wipe grid and notify owner (round end / reset).</summary>
	public void HostClearAllSlotsAndPush()
	{
		if ( !Networking.IsHost )
			return;
		for ( var i = 0; i < TotalSlots; i++ )
			_slots[i] = YaInventorySlot.Empty;
		PushSnapshotToOwner();
	}

	/// <summary>Host: push current slots to owning client (after batch edits that skip <see cref="ServerWriteSlot"/>).</summary>
	public void HostPushSnapshotToOwner()
	{
		if ( !Networking.IsHost )
			return;
		PushSnapshotToOwner();
	}

	public static YaInventorySlot CreateWeaponStack( string itemId )
	{
		return new YaInventorySlot
		{
			ItemId = itemId,
			Quantity = 1,
			HasDurability = false,
			Durability = 100f,
			WeaponInstanceId = "",
			WeaponLoadedAmmo = 0
		};
	}

	bool IsValidSlot( int index ) => index >= 0 && index < TotalSlots;

	public bool TryGetHostSlot( int index, out YaInventorySlot slot )
	{
		slot = default;
		if ( !Networking.IsHost || !IsValidSlot( index ) )
			return false;
		slot = _slots[index];
		return true;
	}

	public bool ServerEnsureWeaponInstanceForHotbarSlot( int slotIndex )
	{
		if ( !Networking.IsHost )
			return false;
		if ( slotIndex < 0 || slotIndex >= HotbarSlotCount )
			return false;

		ref var s = ref _slots[slotIndex];
		if ( s.IsEmpty )
			return false;

		if ( !YaWeaponItemCatalog.TryGet( s.ItemId, out var def ) || def.ItemType != YaItemType.Weapon )
			return false;

		if ( !string.IsNullOrEmpty( s.WeaponInstanceId ) )
			return true;

		s.WeaponInstanceId = Guid.NewGuid().ToString( "N" );
		ApplyNewWeaponRowDefaults( ref s, def, s.ItemId );
		PushSnapshotToOwner();
		return true;
	}

	internal static void ApplyNewWeaponRowDefaults( ref YaInventorySlot s, YaWeaponItemCatalog.YaItemDefinition def, string itemId )
	{
		var combatId = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? itemId : def.CombatWeaponDefinitionId;
		var w = YaWeaponDefinitions.Get( combatId );
		s.HasDurability = false;
		s.Durability = w.MaxDurability;
		s.WeaponLoadedAmmo = w.ClipSize;
	}

	public void ServerWriteSlot( int index, YaInventorySlot slot )
	{
		if ( !Networking.IsHost || !IsValidSlot( index ) )
			return;
		_slots[index] = slot;
		PushSnapshotToOwner();
	}

	public int ServerCountAmmoMatchingType( string ammoTypeId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( ammoTypeId ) )
			return 0;

		var count = 0;
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !YaWeaponItemCatalog.TryGet( s.ItemId, out var d ) || d.ItemType != YaItemType.Ammo )
				continue;
			if ( d.AmmoTypeId != ammoTypeId )
				continue;
			count += s.Quantity;
		}

		return count;
	}

	public int ServerRemoveAmmoMatchingType( string ammoTypeId, int amount )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( ammoTypeId ) || amount <= 0 )
			return 0;

		var need = amount;
		var removed = 0;
		for ( var i = 0; i < TotalSlots && need > 0; i++ )
		{
			ref var s = ref _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !YaWeaponItemCatalog.TryGet( s.ItemId, out var d ) || d.ItemType != YaItemType.Ammo )
				continue;
			if ( d.AmmoTypeId != ammoTypeId )
				continue;

			var take = Math.Min( need, s.Quantity );
			s.Quantity -= take;
			need -= take;
			removed += take;
			if ( s.Quantity <= 0 )
				s = YaInventorySlot.Empty;
		}

		if ( removed > 0 )
			PushSnapshotToOwner();
		return removed;
	}

	public bool TryGetClientMirrorSlot( int index, out YaInventorySlotNet slot )
	{
		slot = default;
		if ( _clientMirror is null || index < 0 || index >= TotalSlots )
			return false;
		slot = _clientMirror[index];
		return true;
	}

	void PushSnapshotToOwner()
	{
		if ( !Networking.IsHost )
			return;
		var arr = new YaInventorySlotNet[TotalSlots];
		for ( var i = 0; i < TotalSlots; i++ )
			arr[i] = ToNet( _slots[i] );
		ClientReceiveInventorySnapshot( arr );
	}

	[Rpc.Owner]
	void ClientReceiveInventorySnapshot( YaInventorySlotNet[] slots )
	{
		_clientMirror = slots;
	}

	static YaInventorySlotNet ToNet( YaInventorySlot s )
	{
		return new YaInventorySlotNet
		{
			ItemId = s.ItemId ?? "",
			Quantity = s.Quantity,
			HasDurability = s.HasDurability ? 1 : 0,
			Durability = s.Durability,
			WeaponInstanceId = s.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = s.WeaponLoadedAmmo,
			WeaponRollPayload = s.WeaponRollPayload ?? "",
			ArmorRollPayload = s.ArmorRollPayload ?? ""
		};
	}

	[Rpc.Host]
	public void RequestUseItemFromSlot( int _ )
	{
		// Consumables not used in YA weapon demo.
	}
}
