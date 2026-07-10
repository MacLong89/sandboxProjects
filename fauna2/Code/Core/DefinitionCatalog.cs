namespace Fauna2;

/// <summary>
/// Loads data-driven definitions from compiled assets and the generated animal catalog.
/// </summary>
public static class DefinitionCatalog
{
	private static IReadOnlyList<AnimalDefinition> _animals;
	private static IReadOnlyList<PlaceableDefinition> _placeables;
	private static readonly Dictionary<AnimalDefinition, string> _animalIds = new();
	private static readonly Dictionary<PlaceableDefinition, string> _placeableIds = new();
	private static bool _initialized;
	private static bool _loading;
	private static int _loadAttempts;
	private static int _lastDiscoveredPlaceFiles;
	private static TimeSince _lastLoadAttempt;

	public static int Revision { get; private set; }

	public static IReadOnlyList<AnimalDefinition> Animals
	{
		get
		{
			EnsureInitialized();
			return _animals;
		}
	}

	public static IReadOnlyList<PlaceableDefinition> Placeables
	{
		get
		{
			EnsureInitialized();
			return _placeables;
		}
	}

	public static void EnsureInitialized()
	{
		if ( _initialized && CatalogLooksComplete() )
			return;

		if ( _loading )
			return;

		if ( _initialized && !CatalogLooksComplete() && _lastLoadAttempt < 0.25f )
			return;

		if ( _initialized && _loadAttempts >= 8 )
			return;

		_loading = true;
		try
		{
			_initialized = true;
			_loadAttempts++;
			_lastLoadAttempt = 0f;
			_animals = LoadAnimals();
			_placeables = LoadPlaceables();
			Revision++;

			var habitats = _placeables.Count( p => p.IsHabitat );
			Log.Info( $"[Fauna] Definition catalog: {_animals.Count} animals, {_placeables.Count} placeables ({habitats} habitats, attempt {_loadAttempts}, discovered {_lastDiscoveredPlaceFiles} .place files)." );
			AnimalHabitatRules.LogMissingHabitatCoverage( _animals, _placeables );
		}
		finally
		{
			_loading = false;
		}

		if ( _placeables.Count == 0 )
			Log.Warning( "[Fauna] No placeables loaded — build menu will be empty until data/placeables/*.place assets compile." );
		else if ( !_placeables.Any( p => p.IsHabitat ) )
			Log.Warning( $"[Fauna] No habitats in catalog yet ({_placeables.Count} placeables, {_lastDiscoveredPlaceFiles} .place files discovered) — build menu will retry." );
	}

	/// <summary>
	/// Paths/entrance work from built-ins alone; keep retrying until data-driven habitats load
	/// (first boot often runs before mounted *.place assets are visible).
	/// </summary>
	private static bool CatalogLooksComplete()
	{
		if ( _placeables is null || _placeables.Count == 0 )
			return false;

		if ( _placeables.Any( p => p.IsHabitat ) )
			return true;

		// Nothing to load from disk — accept built-ins only.
		return _loadAttempts >= 3 && _lastDiscoveredPlaceFiles == 0;
	}

	private static IReadOnlyList<AnimalDefinition> LoadAnimals()
	{
		var map = new Dictionary<string, AnimalDefinition>( StringComparer.OrdinalIgnoreCase );

		foreach ( var path in FindAssetPaths( "*.animal" ) )
		{
			try
			{
				var loaded = LoadResource<AnimalDefinition>( path );
				if ( loaded is not null )
					TryAddAnimal( map, loaded, Defs.ResourceStem( path ) );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[Fauna] Failed to load animal '{path}': {e.Message}" );
			}
		}

		foreach ( var def in ResourceLibrary.GetAll<AnimalDefinition>() )
			TryAddAnimal( map, def );

		return map.Values.ToList();
	}

	private static IReadOnlyList<PlaceableDefinition> LoadPlaceables()
	{
		var map = new Dictionary<string, PlaceableDefinition>( StringComparer.OrdinalIgnoreCase );
		var discovered = FindAssetPaths( "*.place" ).ToList();
		_lastDiscoveredPlaceFiles = discovered.Count;

		if ( discovered.Count == 0 )
			Log.Warning( "[Fauna] FindFile found 0 *.place files on mounted filesystem." );
		else
			Log.Info( $"[Fauna] Discovered {discovered.Count} .place files." );

		foreach ( var path in discovered )
		{
			try
			{
				var loaded = LoadResource<PlaceableDefinition>( path );
				if ( loaded is not null )
					TryAddPlaceable( map, loaded, Defs.ResourceStem( path ) );
				else
					Log.Warning( $"[Fauna] Skipped placeable '{path}' — could not parse resource." );
			}
			catch ( Exception e )
			{
				Log.Warning( $"[Fauna] Failed to load placeable '{path}': {e.Message}" );
			}
		}

		foreach ( var def in ResourceLibrary.GetAll<PlaceableDefinition>() )
			TryAddPlaceable( map, def );

		foreach ( var (id, builtin) in BuiltinPlaceables.All() )
		{
			if ( BuiltinPlaceables.IsCorePath( id ) )
			{
				map[id] = builtin;
				_placeableIds[builtin] = id;
				Log.Info( $"[Fauna] Registered core built-in placeable '{id}'." );
				continue;
			}

			if ( map.ContainsKey( id ) ) continue;
			TryAddPlaceable( map, builtin, id );
			Log.Info( $"[Fauna] Using built-in placeable '{id}'." );
		}

		return map.Values.ToList();
	}

	private static IEnumerable<string> FindAssetPaths( string pattern )
	{
		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var path in FileSystem.Mounted.FindFile( "/", pattern, true ) )
		{
			if ( seen.Add( path ) )
				yield return path;
		}
	}

	private static T LoadResource<T>( string path ) where T : GameResource, new()
	{
		try
		{
			var loaded = ResourceLibrary.Get<T>( path );
			if ( loaded is not null && IsPopulated( loaded ) )
				return loaded;
		}
		catch
		{
			// Fall through to JSON.
		}

		try
		{
			var async = ResourceLibrary.LoadAsync<T>( path ).GetAwaiter().GetResult();
			if ( async is not null && IsPopulated( async ) )
				return async;
		}
		catch
		{
			// Fall through to JSON.
		}

		var json = DefinitionJsonLoader.ReadAssetText( path );
		if ( string.IsNullOrWhiteSpace( json ) )
			return null;

		try
		{
			if ( typeof( T ) == typeof( PlaceableDefinition ) )
				return DefinitionJsonLoader.ParsePlaceable( json, path ) as T;

			if ( typeof( T ) == typeof( AnimalDefinition ) )
				return DefinitionJsonLoader.ParseAnimal( json, path ) as T;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna] JSON parse for '{path}': {e.Message}" );
		}

		return null;
	}

	private static bool IsPopulated<T>( T resource ) where T : GameResource
	{
		if ( resource is PlaceableDefinition placeable )
			return placeable.DisplayName != "Placeable";

		if ( resource is AnimalDefinition animal )
			return animal.DisplayName != "Animal";

		return resource is not null;
	}

	private static void TryAddAnimal( Dictionary<string, AnimalDefinition> map, AnimalDefinition def, string sourceStem = null )
	{
		if ( !IsValidAnimal( def ) )
			return;

		var stem = StemFor( def.ResourceName, sourceStem );
		if ( string.IsNullOrEmpty( stem ) )
			return;

		if ( !map.TryGetValue( stem, out var existing ) || AnimalScore( def ) >= AnimalScore( existing ) )
		{
			map[stem] = def;
			_animalIds[def] = stem;
		}
	}

	private static void TryAddPlaceable( Dictionary<string, PlaceableDefinition> map, PlaceableDefinition def, string sourceStem = null )
	{
		if ( !IsValidPlaceable( def ) )
			return;

		var stem = StemFor( def.ResourceName, sourceStem );
		if ( string.IsNullOrEmpty( stem ) )
			return;

		if ( !map.TryGetValue( stem, out var existing ) || Defs.DefinitionScore( def ) >= Defs.DefinitionScore( existing ) )
		{
			map[stem] = def;
			_placeableIds[def] = stem;
		}
	}

	private static string StemFor( string resourceName, string sourceStem )
	{
		var stem = Defs.ResourceStem( resourceName ?? "" );
		if ( !string.IsNullOrEmpty( stem ) )
			return stem;

		return Defs.ResourceStem( sourceStem ?? "" );
	}

	private static int AnimalScore( AnimalDefinition def )
	{
		var score = def.ResourceName?.Length ?? 0;
		if ( def.DisplayName != "Animal" ) score += 20;
		if ( !string.IsNullOrWhiteSpace( def.Description ) ) score += 10;
		if ( def.Cost > 0 ) score += 5;
		return score;
	}

	private static bool IsValidAnimal( AnimalDefinition def ) =>
		def is not null
		&& !string.IsNullOrWhiteSpace( def.DisplayName )
		&& !(def.DisplayName == "Animal" && def.Species == "animal" && string.IsNullOrWhiteSpace( def.Description ))
		&& HasAnimalSprite( def );

	private static bool HasAnimalSprite( AnimalDefinition def )
	{
		var stem = Defs.ResourceStem( def.ResourceName ?? "" );
		if ( string.IsNullOrEmpty( stem ) )
			stem = Defs.ResourceStem( def.Species ?? "" );

		return !string.IsNullOrEmpty( stem )
			&& FileSystem.Mounted.FileExists( SuppliedSpriteManifest.AnimalModelsRoot + $"{stem}.png" );
	}

	private static bool IsValidPlaceable( PlaceableDefinition def ) =>
		def is not null && def.DisplayName != "Placeable";

	public static string AnimalId( AnimalDefinition def )
	{
		EnsureInitialized();
		if ( def is null )
			return "";

		if ( _animalIds.TryGetValue( def, out var stem ) )
			return stem;

		return Defs.ResourceStem( def.ResourceName ?? "" );
	}

	public static string PlaceableId( PlaceableDefinition def )
	{
		EnsureInitialized();
		if ( def is null )
			return "";

		if ( _placeableIds.TryGetValue( def, out var stem ) )
			return stem;

		return Defs.ResourceStem( def.ResourceName ?? "" );
	}

	public static AnimalDefinition FindAnimal( string id )
	{
		EnsureInitialized();
		if ( string.IsNullOrEmpty( id ) )
			return null;

		var stem = Defs.ResourceStem( id );
		return _animals.FirstOrDefault( a =>
			a.ResourceName == id
			|| Defs.ResourceStem( a.ResourceName ) == stem
			|| _animalIds.GetValueOrDefault( a ) == stem );
	}

	public static PlaceableDefinition FindPlaceable( string id )
	{
		EnsureInitialized();
		if ( string.IsNullOrEmpty( id ) )
			return null;

		var stem = Defs.ResourceStem( id );
		var found = _placeables.FirstOrDefault( p =>
			p.ResourceName == id
			|| Defs.ResourceStem( p.ResourceName ) == stem
			|| _placeableIds.GetValueOrDefault( p ) == stem );

		return found ?? BuiltinPlaceables.Get( stem );
	}

	public static IEnumerable<PlaceableDefinition> PathBuildables()
	{
		EnsureInitialized();

		var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var id in new[] { "entrance", "path_straight" } )
		{
			var def = FindPlaceable( id );
			if ( def is null || !seen.Add( id ) ) continue;
			yield return def;
		}

		foreach ( var def in _placeables.Where( p => p.Category == BuildCategory.Paths ) )
		{
			var id = PlaceableId( def );
			if ( string.IsNullOrEmpty( id ) || !seen.Add( id ) )
				continue;

			yield return def;
		}
	}
}
