namespace Terraingen.GameData;

using Terraingen.Buildings;
using Terraingen.Economy;
using Terraingen.Player;
using Terraingen.World;

/// <summary>
/// Validates that every catalog item can be acquired through at least one gameplay path
/// (gather, starter, shop, loot, airdrop, animal drops, research rewards, or crafting).
/// </summary>
public static class ThornsAcquisitionCoverage
{
	public static readonly HashSet<string> GatherItemIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"wood",
		"stone",
		"metal_ore"
	};

	public static readonly HashSet<string> StarterItemIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"bandage",
		"water",
		"food",
		"wood",
		"stone",
		"cloth"
	};

	public static readonly HashSet<string> AnimalLootItemIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"raw_meat",
		"water",
		"cloth",
		"animal_hide",
		"animal_hide",
		"m4",
		"mp5",
		"shotgun",
		"sniper",
		"usp"
	};

	public static HashSet<string> CollectShopItemIds()
	{
		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var entry in ThornsRadioShopCatalog.StockPool )
		{
			if ( string.IsNullOrWhiteSpace( entry.ItemId ) )
				continue;

			ids.Add( ThornsItemIdAliases.Canonicalize( entry.ItemId ) );
		}

		return ids;
	}

	public static HashSet<string> CollectResearchRewardItemIds()
		=> ThornsResearchCatalog.CollectRewardItemIds();

	public static bool HasAcquisitionPath(
		string itemId,
		IReadOnlyDictionary<string, ThornsItemDefinition> items,
		IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes,
		HashSet<string> lootIds,
		HashSet<string> shopIds,
		HashSet<string> researchRewardIds )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		itemId = ThornsItemIdAliases.Canonicalize( itemId );

		if ( GatherItemIds.Contains( itemId ) )
			return true;

		if ( StarterItemIds.Contains( itemId ) )
			return true;

		if ( shopIds.Contains( itemId ) )
			return true;

		if ( lootIds.Contains( itemId ) )
			return true;

		if ( ThornsAirdropLootTables.CollectAllPossibleItemIds().Contains( itemId ) )
			return true;

		if ( AnimalLootItemIds.Contains( itemId ) )
			return true;

		if ( researchRewardIds.Contains( itemId ) )
			return true;

		if ( ThornsCraftCoverage.IsRawMaterial( itemId ) )
			return false;

		return ThornsCraftCoverage.IsCraftableOutput( itemId, recipes );
	}

	public static List<string> FindItemsWithoutAcquisitionPath(
		IReadOnlyDictionary<string, ThornsItemDefinition> items,
		IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes )
	{
		var missing = new List<string>();
		if ( items is null || items.Count == 0 )
			return missing;

		var lootIds = ThornsBuildingLootTables.CollectAllPossibleLootItemIds();
		var shopIds = CollectShopItemIds();
		var researchRewardIds = CollectResearchRewardItemIds();

		foreach ( var item in items.Values )
		{
			if ( item is null || string.IsNullOrWhiteSpace( item.Id ) )
				continue;

			if ( HasAcquisitionPath( item.Id, items, recipes, lootIds, shopIds, researchRewardIds ) )
				continue;

			missing.Add( item.Id );
		}

		missing.Sort( StringComparer.OrdinalIgnoreCase );
		return missing;
	}

	public static void LogCoverageWarnings(
		IReadOnlyDictionary<string, ThornsItemDefinition> items,
		IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes )
	{
		var missing = FindItemsWithoutAcquisitionPath( items, recipes );
		if ( missing.Count == 0 )
			return;

		Log.Warning(
			$"[Thorns Acquisition] {missing.Count} catalog item(s) have no known acquisition path: {string.Join( ", ", missing )}" );
	}
}
