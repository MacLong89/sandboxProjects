namespace Terraingen.GameData;

/// <summary>Crafting recipe listing and category filters shared by UI and gameplay.</summary>
public static class ThornsCraftCatalog
{
	public const string AllCraftCategoryId = "all";

	static readonly string[] CraftCategoryOrder =
	[
		"tools", "build", "forge", "craft", "food", "medical", "armor", "ammo", "weapons", "attachments"
	];

	public static string NormalizeCraftCategoryId( string categoryId )
	{
		if ( string.IsNullOrWhiteSpace( categoryId ) )
			return AllCraftCategoryId;

		if ( string.Equals( categoryId, AllCraftCategoryId, StringComparison.OrdinalIgnoreCase ) )
			return AllCraftCategoryId;

		ThornsDefinitionRegistry.EnsureInitialized();
		var known = ThornsDefinitionRegistry.AllRecipes.Values
			.Where( ShouldShowInCraftMenu )
			.Select( ResolveRecipeCategory )
			.Where( c => !string.IsNullOrWhiteSpace( c ) )
			.Distinct( StringComparer.OrdinalIgnoreCase );

		return known.Any( c => string.Equals( c, categoryId, StringComparison.OrdinalIgnoreCase ) )
			? categoryId
			: AllCraftCategoryId;
	}

	public static string ResolveRecipeCategory( ThornsRecipeDefinition recipe )
	{
		if ( recipe is null )
			return "";

		var outputId = ThornsItemIdAliases.Canonicalize( recipe.OutputItemId ?? "" );
		var item = ThornsDefinitionRegistry.TryGetRegisteredItem( outputId );
		if ( item is not null )
		{
			return item.Category switch
			{
				ThornsItemCategory.Weapon => "weapons",
				ThornsItemCategory.Tool => "tools",
				ThornsItemCategory.Attachment => "attachments",
				ThornsItemCategory.Armor => "armor",
				ThornsItemCategory.Consumable when IsAmmoOutput( outputId ) => "ammo",
				_ => recipe.CategoryId
			};
		}

		return recipe.CategoryId;
	}

	static bool IsAmmoOutput( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		return string.Equals( itemId, "arrow", StringComparison.OrdinalIgnoreCase )
		       || itemId.Contains( "ammo", StringComparison.OrdinalIgnoreCase );
	}

	public static bool ShouldShowInCraftMenu( ThornsRecipeDefinition recipe )
	{
		if ( recipe is null || string.IsNullOrWhiteSpace( recipe.Id ) )
			return false;

		var canonicalRecipeId = ThornsItemIdAliases.CanonicalizeRecipeId( recipe.Id );
		if ( !string.Equals( recipe.Id, canonicalRecipeId, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var outputId = recipe.OutputItemId ?? "";
		var canonicalOutputId = ThornsItemIdAliases.Canonicalize( outputId );
		if ( !string.Equals( outputId, canonicalOutputId, StringComparison.OrdinalIgnoreCase ) )
			return false;

		var preferredRecipeId = $"recipe_{canonicalOutputId}";
		if ( !string.Equals( recipe.Id, preferredRecipeId, StringComparison.OrdinalIgnoreCase ) )
			return false;

		return ThornsDefinitionRegistry.HasRegisteredItem( canonicalOutputId );
	}

	public static bool RecipeMatchesCraftCategory( ThornsRecipeDefinition recipe, string categoryId )
	{
		if ( recipe is null || !ShouldShowInCraftMenu( recipe ) )
			return false;

		if ( string.IsNullOrWhiteSpace( categoryId )
		     || string.Equals( categoryId, AllCraftCategoryId, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return string.Equals( ResolveRecipeCategory( recipe ), categoryId, StringComparison.OrdinalIgnoreCase );
	}

	public static IEnumerable<ThornsRecipeDefinition> EnumerateRecipesForCategory( string categoryId )
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		categoryId = NormalizeCraftCategoryId( categoryId );

		return ThornsDefinitionRegistry.AllRecipes.Values
			.Where( r => RecipeMatchesCraftCategory( r, categoryId ) )
			.GroupBy(
				r => ThornsItemIdAliases.Canonicalize( r.OutputItemId ?? "" ),
				StringComparer.OrdinalIgnoreCase )
			.Select( PickPreferredRecipe )
			.OrderBy( r => r.RequiredCraftTier )
			.ThenBy( r => r.DisplayName );
	}

	static ThornsRecipeDefinition PickPreferredRecipe( IGrouping<string, ThornsRecipeDefinition> group )
	{
		var output = group.Key;
		var preferredId = $"recipe_{output}";
		var preferred = group.FirstOrDefault( r =>
			string.Equals( r.Id, preferredId, StringComparison.OrdinalIgnoreCase ) );
		if ( preferred is not null )
			return preferred;

		return group
			.OrderBy( r => r.RequiredCraftTier )
			.ThenBy( r => r.Id, StringComparer.OrdinalIgnoreCase )
			.First();
	}

	public static int CountRecipesForCategory( string categoryId )
		=> EnumerateRecipesForCategory( categoryId ).Count();

	public static IEnumerable<string> GetCraftCategories()
	{
		ThornsDefinitionRegistry.EnsureInitialized();
		yield return AllCraftCategoryId;

		var discovered = ThornsDefinitionRegistry.AllRecipes.Values
			.Where( ShouldShowInCraftMenu )
			.Select( ResolveRecipeCategory )
			.Where( c => !string.IsNullOrWhiteSpace( c ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();

		foreach ( var ordered in CraftCategoryOrder )
		{
			if ( discovered.Any( c => string.Equals( c, ordered, StringComparison.OrdinalIgnoreCase ) ) )
				yield return ordered;
		}

		foreach ( var extra in discovered.OrderBy( c => c, StringComparer.OrdinalIgnoreCase ) )
		{
			if ( CraftCategoryOrder.Any( c => string.Equals( c, extra, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			yield return extra;
		}
	}
}
