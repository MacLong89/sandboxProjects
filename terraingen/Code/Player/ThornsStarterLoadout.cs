namespace Terraingen.Player;

using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;

/// <summary>Default gear for new players on first world spawn (empty inventory).</summary>
public static class ThornsStarterLoadout
{
	/// <summary>TEMP — remove when gun playtesting is done.</summary>
	public const bool IncludeTemporaryGunTestKit = false;

	static readonly (string WeaponId, string AmmoId, int AmmoCount)[] TemporaryGunTestEntries =
	{
		( "m4", "rifle_ammo", 120 ),
		( "mp5", "smg_ammo", 120 ),
		( "usp", "pistol_ammo", 60 ),
		( "shotgun", "shotgun_ammo", 48 ),
		( "sniper", "sniper_ammo", 40 ),
		( "m9_bayonet", "", 0 ),
	};

	static readonly string[] StarterAttachmentEntries = ThornsAttachmentItemIds.EnabledInGame;
	public static void ApplyBanditTest( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		inventory.ClearAll();

		var m4 = Stack( "m4", 1 );
		SeedStarterM4( ref m4 );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 0, m4 );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 1, Stack( "raw_meat", 20 ) );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 2, Stack( "food", 20 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 0, Stack( "rifle_ammo", 120 ) );
		ApplyMobTestTameFood( inventory );
	}

	/// <summary>Bow + arrows and craft mats for the bow test sandbox.</summary>
	public static void ApplyBowTest( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		inventory.ClearAll();

		var bow = Stack( "bow", 1 );
		ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref bow, "bow" );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 0, bow );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 1, Stack( "arrow", 40 ) );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 2, Stack( "bandage", 5 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 0, Stack( "cloth", 12 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 1, Stack( "stone", 20 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 2, Stack( "wood", 30 ) );
	}

	/// <summary>Tame-feed consumables for mob/combat sandboxes (inventory slots after combat kit).</summary>
	public static void ApplyMobTestTameFood( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		var inventoryIndex = 1;
		AddFoodStacks( inventory, ref inventoryIndex, "raw_meat", 120 );
		AddFoodStacks( inventory, ref inventoryIndex, "food", 80 );
	}

	static void AddFoodStacks( ThornsInventoryContainer inventory, ref int inventoryIndex, string itemId, int totalCount )
	{
		if ( totalCount <= 0 )
			return;

		ThornsDefinitionRegistry.EnsureInitialized();
		var def = ThornsDefinitionRegistry.GetItem( itemId );
		if ( def is null )
			return;

		var remaining = totalCount;
		while ( remaining > 0 )
		{
			var put = Math.Min( remaining, def.MaxStack );
			if ( !TrySetNextEmptyInventorySlot( inventory, ref inventoryIndex, Stack( itemId, put ) ) )
				return;

			remaining -= put;
		}
	}

	public static void Apply( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		inventory.ClearAll();

		inventory.SetSlot( ThornsContainerKind.Hotbar, 0, Stack( "bandage", 5 ) );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 1, Stack( "water_bottle", 4 ) );
		inventory.SetSlot( ThornsContainerKind.Hotbar, 2, Stack( "apple", 4 ) );

		inventory.SetSlot( ThornsContainerKind.Inventory, 0, Stack( "wood", 30 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 1, Stack( "stone", 20 ) );
		inventory.SetSlot( ThornsContainerKind.Inventory, 2, Stack( "cloth", 5 ) );
	}

	/// <summary>TEMP — stacks each gun + ammo in inventory on top of <see cref="Apply"/>.</summary>
	public static void ApplyTemporaryGunTestKit( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		var inventoryIndex = 0;
		foreach ( var (weaponId, ammoId, ammoCount) in TemporaryGunTestEntries )
		{
			var weapon = Stack( weaponId, 1 );
			ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref weapon, weaponId );
			if ( !TrySetNextEmptyInventorySlot( inventory, ref inventoryIndex, weapon ) )
				break;

			if ( string.IsNullOrWhiteSpace( ammoId ) || ammoCount <= 0 )
				continue;

			if ( !TrySetNextEmptyInventorySlot( inventory, ref inventoryIndex, Stack( ammoId, ammoCount ) ) )
				break;
		}
	}

	/// <summary>One of each attachment mod — granted on every new-world spawn (inventory).</summary>
	public static void ApplyStarterAttachments( ThornsInventoryContainer inventory )
	{
		if ( inventory is null )
			return;

		var inventoryIndex = 0;
		foreach ( var attachmentId in StarterAttachmentEntries )
		{
			if ( !TrySetNextEmptyInventorySlot( inventory, ref inventoryIndex, Stack( attachmentId, 1 ) ) )
				break;
		}
	}

	static bool TrySetNextEmptyInventorySlot(
		ThornsInventoryContainer inventory,
		ref int inventoryIndex,
		ThornsItemStack stack )
	{
		while ( inventoryIndex < ThornsInventoryContainer.InventorySlotCount )
		{
			if ( inventory.GetSlot( ThornsContainerKind.Inventory, inventoryIndex ).IsEmpty )
			{
				inventory.SetSlot( ThornsContainerKind.Inventory, inventoryIndex, stack );
				inventoryIndex++;
				return true;
			}

			inventoryIndex++;
		}

		return false;
	}

	public static void ApplyFullVitals( ThornsVitalsSnapshotDto vitals )
	{
		if ( vitals is null )
			return;

		vitals.Food = vitals.MaxFood = 100f;
		vitals.Water = vitals.MaxWater = 100f;
		vitals.Stamina = vitals.MaxStamina = 100f;
		vitals.ShowFood = true;
		vitals.ShowWater = true;
		vitals.ShowStamina = true;
	}

	/// <summary>First spawn pressure — not starving, but hunger/thirst are felt within minutes.</summary>
	public static void ApplyNewPlayerVitals( ThornsVitalsSnapshotDto vitals )
	{
		if ( vitals is null )
			return;

		vitals.MaxFood = vitals.MaxWater = vitals.MaxStamina = 100f;
		vitals.Food = 76f;
		vitals.Water = 74f;
		vitals.Stamina = 100f;
		vitals.ShowFood = true;
		vitals.ShowWater = true;
		vitals.ShowStamina = true;
	}

	static ThornsItemStack Stack( string itemId, int count ) =>
		new() { ItemId = itemId, Count = count };

	static void SeedStarterM4( ref ThornsItemStack m4 )
	{
		ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref m4, "m4" );
		ThornsWeaponAttachmentState.SetAttachmentItemIdAtSlot(
			ref m4,
			0,
			ThornsAttachmentItemIds.RedDot,
			"m4" );
	}
}