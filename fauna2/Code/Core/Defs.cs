namespace Fauna2;

/// <summary>
/// Data-driven definition lookups. All content (animals, variants, placeables)
/// is defined as GameResources on disk — no hardcoded IDs anywhere.
/// IDs are stored as short resource stems (e.g. "habitat_grassland_s").
/// </summary>
public static class Defs
{
	public static IEnumerable<AnimalDefinition> Animals => DefinitionCatalog.Animals;
	public static IEnumerable<VariantDefinition> Variants => ResourceLibrary.GetAll<VariantDefinition>();
	public static IEnumerable<PlaceableDefinition> Placeables => DefinitionCatalog.Placeables;
	public static AnimalDefinition Animal( string id ) =>
		DefinitionCatalog.FindAnimal( id ) ?? FindByStem( Animals, id, a => a.ResourceName, a => a.DisplayName != "Animal" );

	public static VariantDefinition Variant( string id ) =>
		string.IsNullOrEmpty( id ) ? null : FindByStem( Variants, id, v => v.ResourceName, v => v.DisplayName != "Variant" );

	public static PlaceableDefinition Placeable( string id ) =>
		DefinitionCatalog.FindPlaceable( id ) ?? FindByStem( Placeables, id, p => p.ResourceName, p => p.DisplayName != "Placeable" && (p.Visuals?.Count ?? 0) > 0 );

	public static string IdOf( Resource resource )
	{
		if ( resource is AnimalDefinition animal )
			return DefinitionCatalog.AnimalId( animal );

		if ( resource is PlaceableDefinition placeable )
			return DefinitionCatalog.PlaceableId( placeable );

		return ResourceStem( resource?.ResourceName ?? "" );
	}

	/// <summary>Score how fully-loaded a resource definition is (higher = better).</summary>
	public static int DefinitionScore( PlaceableDefinition def )
	{
		if ( def is null ) return -1;
		var score = 0;
		if ( def.DisplayName != "Placeable" ) score += 10;
		if ( (def.Visuals?.Count ?? 0) > 0 ) score += 5;
		if ( def.Cost > 0 ) score += 1;
		if ( def.Description?.Length > 0 ) score += 1;
		score += def.ResourceName?.Length ?? 0;
		return score;
	}

	/// <summary>Normalize a resource path/name to its short id stem.</summary>
	public static string ResourceStem( string resourceName )
	{
		if ( string.IsNullOrEmpty( resourceName ) ) return "";

		var name = resourceName.Replace( '\\', '/' );
		var slash = name.LastIndexOf( '/' );
		if ( slash >= 0 ) name = name[(slash + 1)..];

		foreach ( var extension in new[] { ".place", ".animal", ".variant" } )
		{
			if ( name.EndsWith( extension, StringComparison.OrdinalIgnoreCase ) )
			{
				name = name[..^extension.Length];
				break;
			}
		}

		return name;
	}

	private static T FindByStem<T>( IEnumerable<T> all, string id, Func<T, string> getName, Func<T, bool> prefer ) where T : class
	{
		if ( string.IsNullOrEmpty( id ) ) return null;

		var stem = ResourceStem( id );
		T best = null;
		var bestScore = int.MinValue;

		foreach ( var item in all )
		{
			var itemName = getName( item );
			if ( itemName != id && ResourceStem( itemName ) != stem ) continue;

			var score = prefer( item ) ? 100 : 0;
			score += itemName.Length;

			if ( item is PlaceableDefinition placeable )
				score += DefinitionScore( placeable );

			if ( best is null || score > bestScore )
			{
				best = item;
				bestScore = score;
			}
		}

		return best;
	}
}
