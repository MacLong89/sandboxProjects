namespace Terraingen.Economy;

using Terraingen.GameData;

/// <summary>Static catalog entries for rotating radio stock and sell-price lookup.</summary>
public static class ThornsRadioShopCatalog
{
	public const string CurrencyItemId = "smelt_metal";

	public static bool IsCurrencyTradeBlockedFromRadioShop( string itemId ) =>
		!string.IsNullOrEmpty( itemId )
		&& ( string.Equals( itemId, CurrencyItemId, StringComparison.OrdinalIgnoreCase )
		     || string.Equals( itemId, "metal_ore", StringComparison.OrdinalIgnoreCase ) );

	public sealed record ThornsShopStockEntry( string ItemId, int BuyPricePerUnit, int MaxBuyPerTransaction = 50 );

	public const int SellPercentOfBuyPrice = 50;

	public static readonly ThornsShopStockEntry[] StockPool =
	[
		new ThornsShopStockEntry( "pistol_ammo", 7, 120 ),
		new ThornsShopStockEntry( "medkit", 85, 3 ),
		new ThornsShopStockEntry( "morphine_pen", 55, 6 ),
		new ThornsShopStockEntry( "canned_stew", 12, 20 ),
		new ThornsShopStockEntry( "metal_ore", 4, 80 ),
		new ThornsShopStockEntry( "bandage", 25, 10 ),
		new ThornsShopStockEntry( "smg_ammo", 8, 150 ),
		new ThornsShopStockEntry( "shotgun_ammo", 10, 80 ),
		new ThornsShopStockEntry( "rifle_ammo", 9, 120 ),
		new ThornsShopStockEntry( "sniper_ammo", 14, 60 ),
		new ThornsShopStockEntry( "water", 5, 30 ),
		new ThornsShopStockEntry( "apple", 4, 40 ),
		new ThornsShopStockEntry( "cloth", 3, 100 ),
		new ThornsShopStockEntry( "stone", 2, 200 ),
		new ThornsShopStockEntry( "wood", 2, 200 ),
		new ThornsShopStockEntry( "field_rations", 14, 24 ),
		new ThornsShopStockEntry( "raw_meat", 6, 40 ),
		new ThornsShopStockEntry( "animal_hide", 8, 60 ),
		new ThornsShopStockEntry( "leather_scrap", 9, 50 )
	];

	public static ThornsShopStockEntry[] ValidStockPool()
	{
		var list = new List<ThornsShopStockEntry>( StockPool.Length );
		foreach ( var e in StockPool )
		{
			if ( ThornsItemRegistry.TryGet( e.ItemId, out _ ) )
				list.Add( e );
		}

		return list.ToArray();
	}

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

	static int HostBaseSellBeforeMultipliers( string itemId, ThornsItemDefinition def )
	{
		if ( TryGetStockEntry( itemId, out var e ) )
			return Math.Max( 1, e.BuyPricePerUnit * SellPercentOfBuyPrice / 100 );

		return def.Category switch
		{
			ThornsItemCategory.Weapon => 48,
			ThornsItemCategory.Armor => 42,
			ThornsItemCategory.Tool => 26,
			ThornsItemCategory.Ammo => 3,
			ThornsItemCategory.Consumable => 5,
			ThornsItemCategory.Resource => 3,
			_ => 2
		};
	}

	public static int HostComputeSellForStack( in ThornsItemStack stack, ThornsItemDefinition def )
	{
		if ( stack.IsEmpty )
			return 0;

		var baseMetal = HostBaseSellBeforeMultipliers( stack.ItemId, def );
		var durMul = 1f;
		if ( stack.HasDurability && def.ToolMaxDurability > 0.01f )
			durMul = Math.Clamp( stack.Durability / def.ToolMaxDurability, 0.35f, 1f );

		return Math.Max( 1, (int)MathF.Floor( baseMetal * durMul ) );
	}

	public static int HostComputeSellForSlot( ThornsItemStack stack )
	{
		if ( stack.IsEmpty )
			return 0;

		if ( !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return 1;

		return HostComputeSellForStack( in stack, def );
	}

	public static int ClientEstimateSellDisplay( ThornsInventorySlotMirrorDto slot )
	{
		if ( slot is null || string.IsNullOrEmpty( slot.ItemId ) || slot.Count <= 0 )
			return 0;

		if ( !ThornsItemRegistry.TryGet( slot.ItemId, out var def ) )
			return 1;

		var stack = new ThornsItemStack
		{
			ItemId = slot.ItemId,
			Count = slot.Count,
			HasDurability = slot.HasDurability,
			Durability = slot.Durability
		};

		return HostComputeSellForStack( in stack, def );
	}
}
