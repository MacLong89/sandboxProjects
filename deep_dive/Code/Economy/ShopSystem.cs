namespace DeepDive;

public sealed class ShopSystem
{
	public bool CanBuy( ShopItemDefinition item, PlayerProgressionData progression, LoadoutInventory loadout )
	{
		if ( item is null || progression is null || loadout is null ) return false;
		if ( item.GoldCost > 0f && progression.Money + 0.001f < item.GoldCost ) return false;
		if ( item.ShellCost > 0f && progression.Shells + 0.001f < item.ShellCost ) return false;

		if ( item.RequiresUnlockedTool && item.ToolSlot >= 0 && !loadout.IsUnlocked( item.ToolSlot ) )
			return false;

		if ( item.UnlockOnly && item.ToolSlot >= 0 && loadout.IsUnlocked( item.ToolSlot ) )
			return false;

		return true;
	}

	public bool TryBuy( ShopItemDefinition item, PlayerProgressionData progression, LoadoutInventory loadout )
	{
		if ( !CanBuy( item, progression, loadout ) )
			return false;

		if ( item.GoldCost > 0f && !progression.TrySpend( item.GoldCost ) )
			return false;

		if ( item.ShellCost > 0f && !progression.TrySpendShells( item.ShellCost ) )
		{
			if ( item.GoldCost > 0f )
				progression.AddMoney( item.GoldCost );
			return false;
		}

		if ( item.GrantsShells )
			progression.AddShells( item.ShellsGranted );
		else if ( item.ToolSlot >= 0 && item.ChargesGranted > 0 )
			loadout.AddCharges( item.ToolSlot, item.ChargesGranted );
		else if ( item.UnlockOnly && item.ToolSlot >= 0 )
			loadout.Unlock( item.ToolSlot );

		return true;
	}
}
