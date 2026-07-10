using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// THORNS_EVERYTHING_DOCUMENT §Crafting — data-driven recipes; tier gates future upgrade / milestone systems.
/// </summary>
public static class ThornsCraftingRecipes
{
	public sealed record ThornsCraftIngredient( string ItemId, int Quantity );

	/// <param name="RequiredCraftingTier">Compared on host to <see cref="ThornsPlayerUpgrades.GetEffectiveCraftingTier"/> (<see cref="ThornsUpgradeCategory.Technician"/> ranks).</param>
	public sealed record ThornsCraftRecipe(
		string Id,
		string OutputItemId,
		int OutputQuantity,
		ThornsCraftIngredient[] Ingredients,
		int RequiredCraftingTier = 1 );

	static readonly ThornsCraftRecipe BedKitRecipe = new(
		"bed_kit",
		"bed_kit",
		1,
		new[]
		{
			new ThornsCraftIngredient( "wood", 40 ),
			new ThornsCraftIngredient( "cloth", 10 )
		},
		RequiredCraftingTier: 1 );

	static ThornsCraftingRecipes()
	{
		RegisterPlaceableFurnitureRecipesFromCatalog();
	}

	static readonly Dictionary<string, ThornsCraftRecipe> _byId = new()
	{
		["smelt_metal"] = new ThornsCraftRecipe(
			"smelt_metal",
			"metal",
			3,
			new[] { new ThornsCraftIngredient( "metal_ore", 6 ) },
			RequiredCraftingTier: 2 ),

		["leather_scrap"] = new ThornsCraftRecipe(
			"leather_scrap",
			"leather_scrap",
			1,
			new[]
			{
				new ThornsCraftIngredient( "animal_hide", 3 ),
				new ThornsCraftIngredient( "cloth", 2 )
			},
			RequiredCraftingTier: 2 ),

		["stone_hatchet"] = new ThornsCraftRecipe(
			"stone_hatchet",
			"stone_hatchet",
			1,
			new[]
			{
				new ThornsCraftIngredient( "wood", 14 ),
				new ThornsCraftIngredient( "stone", 8 )
			},
			RequiredCraftingTier: 1 ),

		["stone_pick"] = new ThornsCraftRecipe(
			"stone_pick",
			"stone_pick",
			1,
			new[]
			{
				new ThornsCraftIngredient( "wood", 14 ),
				new ThornsCraftIngredient( "stone", 12 )
			},
			RequiredCraftingTier: 1 ),

		["axe"] = new ThornsCraftRecipe(
			"axe",
			"axe",
			1,
			new[]
			{
				new ThornsCraftIngredient( "wood", 25 ),
				new ThornsCraftIngredient( "metal", 12 )
			},
			RequiredCraftingTier: 1 ),

		["pickaxe"] = new ThornsCraftRecipe(
			"pickaxe",
			"pickaxe",
			1,
			new[]
			{
				new ThornsCraftIngredient( "wood", 25 ),
				new ThornsCraftIngredient( "metal", 18 )
			},
			RequiredCraftingTier: 1 ),

		["bandage"] = new ThornsCraftRecipe(
			"bandage",
			"bandage",
			1,
			new[] { new ThornsCraftIngredient( "cloth", 5 ) },
			RequiredCraftingTier: 1 ),

		["bandage_sturdy"] = new ThornsCraftRecipe(
			"bandage_sturdy",
			"bandage",
			2,
			new[]
			{
				new ThornsCraftIngredient( "cloth", 12 ),
				new ThornsCraftIngredient( "animal_hide", 1 )
			},
			RequiredCraftingTier: 5 ),

		["pistol_ammo"] = new ThornsCraftRecipe(
			"pistol_ammo",
			"pistol_ammo",
			24,
			new[] { new ThornsCraftIngredient( "metal", 3 ) },
			RequiredCraftingTier: 8 ),

		["shotgun_ammo"] = new ThornsCraftRecipe(
			"shotgun_ammo",
			"shotgun_ammo",
			12,
			new[]
			{
				new ThornsCraftIngredient( "metal", 4 ),
				new ThornsCraftIngredient( "cloth", 2 )
			},
			RequiredCraftingTier: 8 ),

		["smg_ammo"] = new ThornsCraftRecipe(
			"smg_ammo",
			"smg_ammo",
			30,
			new[] { new ThornsCraftIngredient( "metal", 4 ) },
			RequiredCraftingTier: 8 ),

		["rifle_ammo"] = new ThornsCraftRecipe(
			"rifle_ammo",
			"rifle_ammo",
			20,
			new[] { new ThornsCraftIngredient( "metal", 5 ) },
			RequiredCraftingTier: 8 ),

		["sniper_ammo"] = new ThornsCraftRecipe(
			"sniper_ammo",
			"sniper_ammo",
			8,
			new[] { new ThornsCraftIngredient( "metal", 6 ) },
			RequiredCraftingTier: 8 ),

		["scrap_helmet"] = new ThornsCraftRecipe(
			"scrap_helmet",
			"scrap_helmet",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 8 ),
				new ThornsCraftIngredient( "cloth", 12 ),
				new ThornsCraftIngredient( "bone_fragments", 6 )
			},
			RequiredCraftingTier: 3 ),

		["scrap_chest"] = new ThornsCraftRecipe(
			"scrap_chest",
			"scrap_chest",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 12 ),
				new ThornsCraftIngredient( "cloth", 14 ),
				new ThornsCraftIngredient( "bone_fragments", 8 )
			},
			RequiredCraftingTier: 3 ),

		["scrap_pants"] = new ThornsCraftRecipe(
			"scrap_pants",
			"scrap_pants",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 7 ),
				new ThornsCraftIngredient( "cloth", 12 ),
				new ThornsCraftIngredient( "bone_fragments", 6 )
			},
			RequiredCraftingTier: 3 ),

		["kevlar_helmet"] = new ThornsCraftRecipe(
			"kevlar_helmet",
			"kevlar_helmet",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 22 ),
				new ThornsCraftIngredient( "leather_scrap", 6 ),
				new ThornsCraftIngredient( "cloth", 12 )
			},
			RequiredCraftingTier: 9 ),

		["kevlar_chest"] = new ThornsCraftRecipe(
			"kevlar_chest",
			"kevlar_chest",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 28 ),
				new ThornsCraftIngredient( "leather_scrap", 8 ),
				new ThornsCraftIngredient( "cloth", 14 )
			},
			RequiredCraftingTier: 9 ),

		["kevlar_pants"] = new ThornsCraftRecipe(
			"kevlar_pants",
			"kevlar_pants",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 18 ),
				new ThornsCraftIngredient( "leather_scrap", 6 ),
				new ThornsCraftIngredient( "cloth", 12 )
			},
			RequiredCraftingTier: 9 ),

		[ThornsC4.ItemId] = new ThornsCraftRecipe(
			ThornsC4.ItemId,
			ThornsC4.ItemId,
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 12 ),
				new ThornsCraftIngredient( "cloth", 6 ),
				new ThornsCraftIngredient( "bone_fragments", 4 )
			},
			RequiredCraftingTier: 9 ),

		["storage_chest_kit"] = new ThornsCraftRecipe(
			"storage_chest_kit",
			"storage_chest_kit",
			1,
			new[] { new ThornsCraftIngredient( "wood", 55 ) },
			RequiredCraftingTier: 1 ),

		["campfire_kit"] = new ThornsCraftRecipe(
			"campfire_kit",
			"campfire_kit",
			1,
			new[]
			{
				new ThornsCraftIngredient( "wood", 28 ),
				new ThornsCraftIngredient( "stone", 8 )
			},
			RequiredCraftingTier: 1 ),

		["workbench_kit"] = new ThornsCraftRecipe(
			"workbench_kit",
			"workbench_kit",
			1,
			new[]
			{
				new ThornsCraftIngredient( "metal", 22 ),
				new ThornsCraftIngredient( "wood", 35 ),
				new ThornsCraftIngredient( "cloth", 10 )
			},
			RequiredCraftingTier: 3 ),

		["bed_kit"] = BedKitRecipe
	};

	static void RegisterPlaceableFurnitureRecipesFromCatalog()
	{
		_byId[BedKitRecipe.Id] = BedKitRecipe;

		foreach ( var entry in ThornsPlaceableFurnitureCatalog.All )
		{
			if ( entry.CraftIngredients is null || entry.CraftIngredients.Length == 0 )
				continue;

			if ( entry.KitItemId is "storage_chest_kit" or "campfire_kit" or "workbench_kit" )
				continue;

			_byId[entry.KitItemId] = new ThornsCraftRecipe(
				entry.KitItemId,
				entry.KitItemId,
				1,
				entry.CraftIngredients,
				RequiredCraftingTier: PlaceableCraftTierFor( entry.StructureDefId ) );
		}
	}

	static void EnsurePlaceableCraftRecipesOnEveryList() => RegisterPlaceableFurnitureRecipesFromCatalog();

	static int PlaceableCraftTierFor( string structureDefId ) =>
		structureDefId switch
		{
			"workbench" => 3,
			"kitchen_fridge" or "fridge" => 2,
			_ => 1
		};

	public static IReadOnlyList<ThornsCraftRecipe> All
	{
		get
		{
			EnsurePlaceableCraftRecipesOnEveryList();

			var list = new List<ThornsCraftRecipe>( _byId.Count );
			foreach ( var kv in _byId )
				list.Add( kv.Value );

			static string SortKey( ThornsCraftRecipe r )
			{
				var def = ThornsItemRegistry.GetOrNull( r.OutputItemId );
				return def?.DisplayName ?? r.OutputItemId;
			}

			list.Sort( ( a, b ) => string.Compare( SortKey( a ), SortKey( b ), StringComparison.OrdinalIgnoreCase ) );
			return list;
		}
	}

	public static bool TryGet( string recipeId, out ThornsCraftRecipe recipe )
	{
		recipe = default;
		if ( string.IsNullOrWhiteSpace( recipeId ) )
			return false;

		EnsurePlaceableCraftRecipesOnEveryList();
		return _byId.TryGetValue( recipeId, out recipe );
	}
}
