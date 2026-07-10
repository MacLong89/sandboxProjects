namespace Sandbox;

/// <summary>Static catalog entries used for rotation and sell-price lookup (THORNS_EVERYTHING_DOCUMENT §radio shop).</summary>
public static class ThornsRadioShopCatalog
{
	/// <summary>Item id used as radio shop currency (stacks in player inventory).</summary>
	public const string CurrencyItemId = "metal";

	/// <summary>Refined metal (currency) and ore — not listed for purchase and cannot be sold back at the radio.</summary>
	public static bool IsMetalTradeBlockedFromRadioShop( string itemId ) =>
		!string.IsNullOrEmpty( itemId )
		&& ( string.Equals( itemId, "metal", StringComparison.OrdinalIgnoreCase )
		     || string.Equals( itemId, "metal_ore", StringComparison.OrdinalIgnoreCase ) );

	public sealed record ThornsShopStockEntry( string ItemId, int BuyPricePerUnitMetal, int MaxBuyPerTransaction = 50 );

	/// <summary>Authorable pool the rotating shop draws from (buy price = metal per unit).</summary>
	public static readonly ThornsShopStockEntry[] StockPool =
	[
		new ThornsShopStockEntry( "bandage", 25, 10 ),
		new ThornsShopStockEntry( "medkit_field", 85, 3 ),
		new ThornsShopStockEntry( "morphine_pen", 55, 5 ),
		new ThornsShopStockEntry( "pistol_ammo", 8, 120 ),
		new ThornsShopStockEntry( "shotgun_ammo", 10, 80 ),
		new ThornsShopStockEntry( "smg_ammo", 8, 150 ),
		new ThornsShopStockEntry( "rifle_ammo", 9, 120 ),
		new ThornsShopStockEntry( "sniper_ammo", 14, 60 ),
		new ThornsShopStockEntry( "water", 5, 30 ),
		new ThornsShopStockEntry( "apple", 4, 40 ),
		new ThornsShopStockEntry( "cloth", 3, 100 ),
		new ThornsShopStockEntry( "stone", 2, 200 ),
		new ThornsShopStockEntry( "wood", 2, 200 ),
		new ThornsShopStockEntry( "ration_pack", 18, 20 ),
		new ThornsShopStockEntry( "field_rations", 14, 24 ),
		new ThornsShopStockEntry( "raw_meat", 6, 40 ),
		new ThornsShopStockEntry( "cooked_meat", 10, 40 ),
		new ThornsShopStockEntry( "animal_hide", 8, 60 ),
		new ThornsShopStockEntry( "bone_fragments", 4, 80 ),
		new ThornsShopStockEntry( "leather_scrap", 9, 50 ),
		new ThornsShopStockEntry( "electrolyte_drink", 12, 16 ),
	];

	public const int SellPercentOfBuyPrice = 50;

	public static bool TryGetStockEntry( string itemId, out ThornsShopStockEntry entry )
	{
		foreach ( var e in StockPool )
		{
			if ( string.Equals( e.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
			{
				entry = e;
				return true;
			}
		}

		entry = default;
		return false;
	}

	static int HostBaseScrapMetalBeforeRollMultipliers( string itemId, ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( TryGetStockEntry( itemId, out var e ) )
			return Math.Max( 1, e.BuyPricePerUnitMetal * SellPercentOfBuyPrice / 100 );

		return def.ItemType switch
		{
			ThornsItemType.Weapon => 48,
			ThornsItemType.Armor => 42,
			ThornsItemType.Tool => 26,
			ThornsItemType.Ammo => 3,
			ThornsItemType.Consumable => 5,
			ThornsItemType.Resource => 3,
			_ => 2
		};
	}

	/// <summary>Host sell payout per unit — rarity + rolled tier (weapon/armor payloads) and tool durability fraction.</summary>
	public static int HostComputeSellMetalForSlot( in ThornsInventorySlot slot, ThornsItemRegistry.ThornsItemDefinition def )
	{
		var baseMetal = HostBaseScrapMetalBeforeRollMultipliers( slot.ItemId, def );
		var rarityMul = 1f;
		var tierMul = 1f;

		if ( ThornsGearRoll.TryParseWeapon( slot.WeaponRollPayload ?? "", out var wr, out var dmg, out var fr ) )
		{
			rarityMul = 1f + (int)wr * 0.2f;
			var mid = (dmg + fr) * 0.5f;
			tierMul *= 1f + 0.14f * MathF.Max( 0f, mid - 1f );
		}
		else if ( ThornsGearRoll.TryParseArmor( slot.ArmorRollPayload ?? "", out var ar, out var dr ) )
		{
			rarityMul = 1f + (int)ar * 0.2f;
			tierMul *= 1f + 0.17f * MathF.Max( 0f, dr - 1f );
		}

		var durMul = 1f;
		if ( slot.HasDurability && def.ToolMaxDurability > 0.01f )
			durMul = Math.Clamp( slot.Durability / def.ToolMaxDurability, 0.35f, 1f );

		return Math.Max( 1, (int)MathF.Floor( baseMetal * rarityMul * tierMul * durMul ) );
	}

	public static int HostComputeSellMetalForInventorySlot( ThornsInventory inv, int slotIndex )
	{
		if ( inv is null || !inv.IsValid() || !inv.TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty )
			return 0;
		if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) )
			return 1;
		return HostComputeSellMetalForSlot( in slot, def );
	}

	public static ThornsInventorySlot SlotFromNet( ThornsInventorySlotNet n ) =>
		new()
		{
			ItemId = n.ItemId ?? "",
			Quantity = n.Quantity,
			HasDurability = n.HasDurability != 0,
			Durability = n.Durability,
			WeaponInstanceId = n.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = n.WeaponLoadedAmmo,
			WeaponRollPayload = n.WeaponRollPayload ?? "",
			ArmorRollPayload = n.ArmorRollPayload ?? ""
		};

	/// <summary>Client UI estimate — mirrors host formula on mirror slots.</summary>
	public static int ClientEstimateSellMetalDisplay( ThornsInventorySlotNet net )
	{
		if ( string.IsNullOrEmpty( net.ItemId ) || net.Quantity <= 0 )
			return 0;
		if ( !ThornsItemRegistry.TryGet( net.ItemId, out var def ) )
			return 1;
		var slot = SlotFromNet( net );
		return HostComputeSellMetalForSlot( in slot, def );
	}

	/// <summary>Vendor sell quote (per unit) for items without full slot context — catalog scrap or type baseline only.</summary>
	public static int HostGetSellPricePerUnit( string itemId )
	{
		if ( ThornsItemRegistry.TryGet( itemId, out var def ) )
			return HostBaseScrapMetalBeforeRollMultipliers( itemId, def );

		return 1;
	}
}
