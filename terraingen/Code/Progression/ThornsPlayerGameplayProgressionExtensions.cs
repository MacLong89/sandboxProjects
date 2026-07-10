namespace Terraingen.Player;

using Terraingen.GameData;

public static class ThornsPlayerGameplayProgressionExtensions
{
	public static bool HostHasSurvivalArmament( this ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null )
			return false;

		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
		{
			if ( IsSurvivalArmament( gameplay.Inventory.GetSlot( ThornsContainerKind.Inventory, i ).ItemId ) )
				return true;
		}

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
		{
			if ( IsSurvivalArmament( gameplay.Inventory.GetSlot( ThornsContainerKind.Hotbar, i ).ItemId ) )
				return true;
		}

		return false;
	}

	static bool IsSurvivalArmament( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		if ( def.ItemType == ThornsItemType.Weapon )
			return true;

		return string.Equals( itemId, "stone_hatchet", StringComparison.OrdinalIgnoreCase )
		       || string.Equals( itemId, "stone_pickaxe", StringComparison.OrdinalIgnoreCase );
	}
}
