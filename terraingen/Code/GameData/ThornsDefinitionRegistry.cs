namespace Terraingen.GameData;

using Terraingen.Animals;

/// <summary>Central registry for item/recipe/skill/journal definitions. Replace stubs when content ships.</summary>
public static class ThornsDefinitionRegistry
{
	static bool _initialized;
	static bool _initializing;
	static readonly Dictionary<string, ThornsItemDefinition> Items = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, ThornsRecipeDefinition> Recipes = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, ThornsSkillDefinition> Skills = new( StringComparer.OrdinalIgnoreCase );
	static readonly Dictionary<string, ThornsJournalGoalDefinition> Goals = new( StringComparer.OrdinalIgnoreCase );
	static readonly List<ThornsDiscoveryEntryDto> DiscoveryTemplates = new();

	public static void EnsureInitialized()
	{
		if ( _initialized )
		{
			RefreshJournalGoalDefinitions();
			return;
		}

		if ( _initializing )
			return;

		_initializing = true;
		try
		{
			_initialized = true;
			RegisterProductionContent();
			RefreshJournalGoalDefinitions();
		}
		finally
		{
			_initializing = false;
		}
	}

	public static IReadOnlyDictionary<string, ThornsItemDefinition> AllItems
	{
		get
		{
			EnsureInitialized();
			return Items;
		}
	}

	public static IReadOnlyDictionary<string, ThornsRecipeDefinition> AllRecipes
	{
		get
		{
			EnsureInitialized();
			return Recipes;
		}
	}

	public static IReadOnlyDictionary<string, ThornsSkillDefinition> AllSkills
	{
		get
		{
			EnsureInitialized();
			return Skills;
		}
	}

	public static IReadOnlyDictionary<string, ThornsJournalGoalDefinition> AllGoals
	{
		get
		{
			EnsureInitialized();
			return Goals;
		}
	}

	public static IReadOnlyList<ThornsDiscoveryEntryDto> DiscoveryTemplatesList
	{
		get
		{
			EnsureInitialized();
			return DiscoveryTemplates;
		}
	}

	public static ThornsItemDefinition GetItem( string id )
	{
		EnsureInitialized();
		return TryGetRegisteredItem( id );
	}

	/// <summary>Item lookup without triggering registry sync (safe during catalog purge).</summary>
	internal static ThornsItemDefinition TryGetRegisteredItem( string id )
	{
		id = ThornsItemIdAliases.Canonicalize( id ?? "" );
		return Items.GetValueOrDefault( id );
	}

	internal static bool HasRegisteredItem( string id )
		=> TryGetRegisteredItem( id ) is not null;

	public static ThornsRecipeDefinition GetRecipe( string id )
	{
		EnsureInitialized();
		id = ThornsItemIdAliases.CanonicalizeRecipeId( id ?? "" );
		return Recipes.GetValueOrDefault( id );
	}

	public static ThornsSkillDefinition GetSkill( string id )
	{
		EnsureInitialized();
		return Skills.GetValueOrDefault( id ?? "" );
	}

	public static ThornsJournalGoalDefinition GetGoal( string id )
	{
		EnsureInitialized();
		return Goals.GetValueOrDefault( id ?? "" );
	}

	public static void RegisterItem( ThornsItemDefinition def )
	{
		if ( def is null || string.IsNullOrWhiteSpace( def.Id ) )
			return;

		Items[def.Id] = def;
	}

	public static void RegisterRecipe( ThornsRecipeDefinition def )
	{
		if ( def is null || string.IsNullOrWhiteSpace( def.Id ) )
			return;

		Recipes[def.Id] = def;
	}

	public static void RegisterSkill( ThornsSkillDefinition def )
	{
		if ( def is null || string.IsNullOrWhiteSpace( def.Id ) )
			return;

		Skills[def.Id] = def;
	}

	public static void RegisterGoal( ThornsJournalGoalDefinition def )
	{
		if ( def is null || string.IsNullOrWhiteSpace( def.Id ) )
			return;

		Goals[def.Id] = def;
	}

	public static void ClearAndReload( IEnumerable<ThornsItemDefinition> items,
		IEnumerable<ThornsRecipeDefinition> recipes,
		IEnumerable<ThornsSkillDefinition> skills,
		IEnumerable<ThornsJournalGoalDefinition> goals )
	{
		Items.Clear();
		Recipes.Clear();
		Skills.Clear();
		Goals.Clear();
		DiscoveryTemplates.Clear();
		_initialized = true;

		foreach ( var i in items )
			RegisterItem( i );
		foreach ( var r in recipes )
			RegisterRecipe( r );
		foreach ( var s in skills )
			RegisterSkill( s );
		foreach ( var g in goals )
			RegisterGoal( g );
	}

	static void RegisterProductionContent()
	{
		foreach ( var item in ThornsItemCatalog.Items )
			RegisterItem( item );
		foreach ( var recipe in ThornsItemCatalog.Recipes )
			RegisterRecipe( recipe );

		RemoveSuppressedCraftRecipes();
		RemoveStaleCatalogItems();
		foreach ( var skill in ThornsUpgradeDefinitions.All )
			RegisterSkill( skill );

		ThornsCraftCoverage.LogCoverageWarnings( Items, Recipes );
		ThornsAcquisitionCoverage.LogCoverageWarnings( Items, Recipes );

		RebuildDiscoveryTemplates();
	}

	static void RemoveSuppressedCraftRecipes()
	{
		foreach ( var recipeId in Recipes.Keys.ToList() )
		{
			if ( !ThornsCraftCatalog.ShouldShowInCraftMenu( Recipes[recipeId] ) )
				Recipes.Remove( recipeId );
		}
	}

	static void RemoveStaleCatalogItems()
	{
		var validIds = ThornsItemCatalog.Items
			.Select( i => i.Id )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		foreach ( var itemId in Items.Keys.ToList() )
		{
			if ( validIds.Contains( itemId ) )
				continue;

			var canonical = ThornsItemIdAliases.Canonicalize( itemId );
			if ( validIds.Contains( canonical ) )
			{
				Items.Remove( itemId );
				continue;
			}

			var def = Items[itemId];
			if ( def.Category is ThornsItemCategory.Tool or ThornsItemCategory.Weapon )
				Items.Remove( itemId );
		}
	}

	/// <summary>Re-apply milestone definitions (safe on hot reload; overwrites cached goals).</summary>
	public static void RefreshJournalGoalDefinitions()
	{
		var ids = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var goal in ThornsMilestoneDefinitions.All )
		{
			ids.Add( goal.Id );
			Goals[goal.Id] = goal;
		}

		foreach ( var staleId in Goals.Keys.Where( id => !ids.Contains( id ) ).ToList() )
			Goals.Remove( staleId );
	}

	/// <summary>Merge icon-scanned definitions (call from UI after <see cref="ThornsIconManifest.Refresh"/>).</summary>
	public static void RegisterDiscoveredIcons( IEnumerable<ThornsItemDefinition> items,
		IEnumerable<ThornsSkillDefinition> extraSkills )
	{
		EnsureInitialized();

		foreach ( var item in items ?? Array.Empty<ThornsItemDefinition>() )
		{
			if ( item is null || string.IsNullOrWhiteSpace( item.Id ) )
				continue;

			var canonical = ThornsItemIdAliases.Canonicalize( item.Id );
			if ( !string.Equals( item.Id, canonical, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( Items.ContainsKey( item.Id ) )
				continue;

			if ( item.Category is ThornsItemCategory.Tool or ThornsItemCategory.Weapon )
				continue;

			RegisterItem( item );
		}

		foreach ( var skill in extraSkills ?? Array.Empty<ThornsSkillDefinition>() )
		{
			if ( skill is null || string.IsNullOrWhiteSpace( skill.Id ) || Skills.ContainsKey( skill.Id ) )
				continue;

			RegisterSkill( skill );
		}

		RebuildDiscoveryTemplates();
	}

	static void RebuildDiscoveryTemplates()
	{
		DiscoveryTemplates.Clear();

		foreach ( var item in Items.Values.OrderBy( i => i.DisplayName ) )
		{
			if ( !ShouldTrackItemDiscovery( item ) )
				continue;

			DiscoveryTemplates.Add( new ThornsDiscoveryEntryDto
			{
				Id = DiscoveryIdForItem( item.Id ),
				Title = item.DisplayName,
				Category = DiscoveryCategoryForItem( item ),
				IconPath = item.IconPath
			} );
		}

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		foreach ( var species in ThornsAnimalSpeciesRegistry.All.OrderBy( s => s.DisplayName ) )
		{
			DiscoveryTemplates.Add( new ThornsDiscoveryEntryDto
			{
				Id = DiscoveryIdForCreature( species.Key ),
				Title = species.DisplayName,
				Category = "Creature",
				IconPath = ThornsTameCatalog.CreaturePortraitPath( species.Key )
			} );
		}
	}

	public static string DiscoveryIdForItem( string itemId ) => $"item_{itemId}";

	public static string DiscoveryIdForCreature( string speciesKey ) => $"creature_{speciesKey}";

	static bool ShouldTrackItemDiscovery( ThornsItemDefinition item ) =>
		item is not null
		&& !string.IsNullOrWhiteSpace( item.Id )
		&& item.Category is not ThornsItemCategory.Unknown and not ThornsItemCategory.Blueprint;

	static string DiscoveryCategoryForItem( ThornsItemDefinition item ) => item.Category switch
	{
		ThornsItemCategory.Resource => "Resource",
		ThornsItemCategory.Tool => "Tool",
		ThornsItemCategory.Weapon => "Weapon",
		ThornsItemCategory.Consumable => "Consumable",
		ThornsItemCategory.Ammo => "Ammo",
		ThornsItemCategory.Armor => "Armor",
		_ => "Item"
	};
}
