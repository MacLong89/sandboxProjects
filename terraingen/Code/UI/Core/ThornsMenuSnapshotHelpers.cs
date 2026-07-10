namespace Terraingen.UI;

using Terraingen.GameData;

/// <summary>Read-only helpers for menu UI bound to <see cref="ThornsUiClientState"/>.</summary>
public static class ThornsMenuSnapshotHelpers
{
	public static int CountItem( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) || !ThornsUiClientState.HasSnapshot )
			return 0;

		var total = 0;
		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory.Slots )
		{
			if ( string.Equals( slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				total += slot.Count;
		}

		return total;
	}

	public static float ComputeCarriedWeightKg()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return 0f;

		var total = 0f;
		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory.Slots )
		{
			if ( string.IsNullOrEmpty( slot.ItemId ) || slot.Count <= 0 )
				continue;

			var def = ThornsDefinitionRegistry.GetItem( slot.ItemId );
			total += (def?.WeightKg ?? 0.5f) * slot.Count;
		}

		return total;
	}

	public static int GetPlayerCraftTier()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return 1;

		return ThornsCraftProgression.ResolveCraftTier( ThornsUiClientState.Snapshot.Skills );
	}

	public static bool HasRecipeIngredients( ThornsRecipeDefinition recipe )
	{
		if ( recipe?.Ingredients is null )
			return false;

		foreach ( var ing in recipe.Ingredients )
		{
			if ( ing is null || string.IsNullOrEmpty( ing.ItemId ) )
				continue;

			if ( CountItem( ing.ItemId ) < ing.Count )
				return false;
		}

		return true;
	}

	public static bool HasCraftStation( ThornsRecipeDefinition recipe )
	{
		if ( recipe is null || recipe.Station == ThornsCraftStationKind.Hand )
			return true;

		if ( !ThornsUiClientState.HasSnapshot )
			return false;

		var craft = ThornsUiClientState.Snapshot.Craft;
		return recipe.Station <= craft.NearestStation;
	}

	public static bool MeetsRecipeCraftTier( ThornsRecipeDefinition recipe )
		=> ThornsCraftProgression.MeetsCraftTier( GetPlayerCraftTier(), recipe );

	public static bool CanCraftRecipe( ThornsRecipeDefinition recipe )
		=> recipe is not null
		   && MeetsRecipeCraftTier( recipe )
		   && HasRecipeIngredients( recipe )
		   && HasCraftStation( recipe );

	public static string DescribeCraftBlock( ThornsRecipeDefinition recipe )
	{
		if ( recipe is null )
			return "Invalid recipe.";

		if ( !MeetsRecipeCraftTier( recipe ) )
		{
			return $"Requires {ThornsCraftProgression.FormatRequiredTier( recipe.RequiredCraftTier )} " +
			       $"(yours: {ThornsCraftProgression.FormatRequiredTier( GetPlayerCraftTier() )}). " +
			       "Upgrade Technician in Skills.";
		}

		if ( !HasCraftStation( recipe ) )
			return $"Requires a nearby {FormatStation( recipe.Station )}.";

		if ( !HasRecipeIngredients( recipe ) )
			return "Missing materials.";

		return "Ready to craft.";
	}

	static string FormatStation( ThornsCraftStationKind station ) => station switch
	{
		ThornsCraftStationKind.Workbench => "workbench",
		ThornsCraftStationKind.Campfire => "campfire",
		ThornsCraftStationKind.Forge => "forge",
		ThornsCraftStationKind.Special => "special station",
		_ => "hand crafting"
	};

	public static IEnumerable<string> GetCraftCategories()
		=> ThornsCraftCatalog.GetCraftCategories();

	public const string AllCraftCategoryId = ThornsCraftCatalog.AllCraftCategoryId;

	public static string NormalizeCraftCategoryId( string categoryId )
		=> ThornsCraftCatalog.NormalizeCraftCategoryId( categoryId );

	public static bool RecipeMatchesCraftCategory( ThornsRecipeDefinition recipe, string categoryId )
		=> ThornsCraftCatalog.RecipeMatchesCraftCategory( recipe, categoryId );

	public static IEnumerable<ThornsRecipeDefinition> EnumerateRecipesForCategory( string categoryId )
		=> ThornsCraftCatalog.EnumerateRecipesForCategory( categoryId );

	public static int CountRecipesForCategory( string categoryId )
		=> ThornsCraftCatalog.CountRecipesForCategory( categoryId );
}
