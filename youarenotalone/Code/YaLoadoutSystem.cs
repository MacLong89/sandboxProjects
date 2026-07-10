namespace Sandbox;

/// <summary>Host-only weapon grid setup for asym roles. Clears inventory before applying.</summary>
public static class YaLoadoutSystem
{
	const int AmmoReserveSlot = YaGameInventory.HotbarSlotCount;
	const int AmmoQuantity = 240;

	/// <summary>Strip everything and apply role weapons. Caller should call <see cref="YaHotbarEquipment.HostApplyHotbarSlot"/> after respawn.</summary>
	public static void HostApplyRoleLoadout( GameObject playerRoot, YaPlayerRole role )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return;

		var inv = playerRoot.Components.Get<YaGameInventory>( FindMode.EnabledInSelf );
		var hotbar = playerRoot.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
		if ( !inv.IsValid() || !hotbar.IsValid() )
			return;

		inv.HostClearAllSlotsAndPush();
		hotbar.HostClearEquipmentForRoundReset();

		switch ( role )
		{
			case YaPlayerRole.Alone:
				inv.ServerWriteSlot( 0, YaGameInventory.CreateWeaponStack( "m9_bayonet" ) );
				break;
			case YaPlayerRole.NotAlone:
				inv.ServerWriteSlot( 0, YaGameInventory.CreateWeaponStack( "m4" ) );
				inv.ServerWriteSlot( 1, YaGameInventory.CreateWeaponStack( "shotgun" ) );
				inv.ServerWriteSlot( AmmoReserveSlot, new YaInventorySlot
				{
					ItemId = "ammo_basic",
					Quantity = AmmoQuantity,
					HasDurability = false,
					Durability = 0f,
					WeaponInstanceId = "",
					WeaponLoadedAmmo = 0
				} );
				break;
			default:
				break;
		}
	}

	/// <summary>Remove weapons and ammo after a round (neutral state).</summary>
	public static void HostStripToNeutral( GameObject playerRoot )
	{
		if ( !Networking.IsHost || playerRoot is null || !playerRoot.IsValid() )
			return;

		var inv = playerRoot.Components.Get<YaGameInventory>( FindMode.EnabledInSelf );
		var hotbar = playerRoot.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
		if ( !inv.IsValid() )
			return;

		inv.HostClearAllSlotsAndPush();
		if ( hotbar.IsValid() )
			hotbar.HostClearEquipmentForRoundReset();
	}
}
