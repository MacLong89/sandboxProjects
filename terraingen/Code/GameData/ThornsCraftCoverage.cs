namespace Terraingen.GameData;

/// <summary>
/// Design rule: every non-raw catalog item should be craftable directly or via an equivalent kit recipe.
/// </summary>
public static class ThornsCraftCoverage
{
	public static readonly HashSet<string> RawMaterialIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"wood",
		"stone",
		"cloth",
		"metal_ore",
		"animal_hide",
		"raw_meat",
		"water"
	};

	/// <summary>Outputs produced at stations outside the recipe catalog (e.g. campfire smelting).</summary>
	public static readonly HashSet<string> StationProcessedIds = new( StringComparer.OrdinalIgnoreCase )
	{
		"smelt_metal"
	};

	static readonly Dictionary<string, string> DeployableKitOutputs = new( StringComparer.OrdinalIgnoreCase )
	{
		["campfire"] = "campfire_kit",
		["bed"] = "bed_kit",
		["storage_chest"] = "storage_chest_kit",
		["workbench"] = "workbench_kit",
		["research"] = "research_kit"
	};

	public static bool IsRawMaterial( string itemId )
		=> !string.IsNullOrWhiteSpace( itemId ) && RawMaterialIds.Contains( itemId );

	public static bool IsCraftableOutput( string itemId, IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) || recipes is null )
			return false;

		foreach ( var recipe in recipes.Values )
		{
			if ( recipe is null )
				continue;

			if ( string.Equals( recipe.OutputItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		if ( DeployableKitOutputs.TryGetValue( itemId, out var kitOutput ) )
			return IsCraftableOutput( kitOutput, recipes );

		return false;
	}

	public static List<string> FindUncraftableItems( IReadOnlyDictionary<string, ThornsItemDefinition> items,
		IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes )
	{
		var missing = new List<string>();
		if ( items is null || recipes is null )
			return missing;

		foreach ( var item in items.Values )
		{
			if ( item is null || string.IsNullOrWhiteSpace( item.Id ) )
				continue;

			if ( IsRawMaterial( item.Id ) )
				continue;

			if ( StationProcessedIds.Contains( item.Id ) )
				continue;

			if ( IsCraftableOutput( item.Id, recipes ) )
				continue;

			missing.Add( item.Id );
		}

		missing.Sort( StringComparer.OrdinalIgnoreCase );
		return missing;
	}

	public static void LogCoverageWarnings( IReadOnlyDictionary<string, ThornsItemDefinition> items,
		IReadOnlyDictionary<string, ThornsRecipeDefinition> recipes )
	{
		var missing = FindUncraftableItems( items, recipes );
		if ( missing.Count == 0 )
			return;

		Log.Warning(
			$"[Thorns Craft] {missing.Count} catalog item(s) have no recipe yet (raw materials excluded): {string.Join( ", ", missing )}" );
	}
}
