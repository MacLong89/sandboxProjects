namespace Terraingen.Animals;

using Terraingen;
using Terraingen.Rendering;

/// <summary>
/// Resolves wildlife meshes for crossbreeds under <c>models/{a}-{b}/</c> (and longer lineage keys).
/// Falls back to a parent species model when the hybrid <c>.vmdl</c> is not mounted yet.
/// </summary>
public static class ThornsAnimalModelResolve
{
	public const string ModelsRoot = "models";

	static readonly HashSet<string> LoggedHybridFallbacks = new( StringComparer.OrdinalIgnoreCase );

	public readonly struct ResolvedVisual
	{
		public string ModelPath { get; init; }
		public string AnimPrefix { get; init; }
		public bool UsedHybridModel { get; init; }
	}

	public static ResolvedVisual ResolveForSpecies( ThornsAnimalSpeciesData species )
	{
		if ( species is null )
			return default;

		return new ResolvedVisual
		{
			ModelPath = species.ModelPath ?? "",
			AnimPrefix = species.AnimPrefix ?? "",
			UsedHybridModel = false
		};
	}

	public static ResolvedVisual ResolveForBrain( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.IsValid() )
			return default;

		if ( !ThornsAnimalSpeciesRegistry.TryGet( brain.SpeciesId, out var bodySpecies ) )
			return default;

		var parentKeys = ResolveParentKeysFromGenetics( brain.GeneticSpeciesIdsCsv, brain.SpeciesId );
		if ( parentKeys.Count < 2 && !brain.IsCrossbreed )
			return ResolveForSpecies( bodySpecies );

		return ResolveHybridVisual( parentKeys, bodySpecies );
	}

	public static ResolvedVisual ResolveHybridVisual(
		IReadOnlyList<string> parentKeys,
		ThornsAnimalSpeciesData bodySpecies )
	{
		if ( bodySpecies is null )
			return default;

		var keys = CanonicalizeParentKeys( parentKeys );
		if ( keys.Count < 2 )
			return ResolveForSpecies( bodySpecies );

		var hybridKey = BuildHybridKey( keys );
		var hybridPath = BuildHybridModelPath( hybridKey );
		if ( TryLoadUsableModel( hybridPath ) )
		{
			return new ResolvedVisual
			{
				ModelPath = hybridPath,
				AnimPrefix = hybridKey,
				UsedHybridModel = true
			};
		}

		LogHybridFallbackOnce( hybridPath, keys );
		return ResolveParentFallbackVisual( keys, bodySpecies );
	}

	public static List<string> ResolveParentKeysFromGenetics( string geneticSpeciesIdsCsv, ushort fallbackSpeciesId )
	{
		var keys = new List<string>();
		foreach ( var part in (geneticSpeciesIdsCsv ?? "").Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( !ushort.TryParse( part, out var id ) || id == 0 )
				continue;

			if ( !ThornsAnimalSpeciesRegistry.TryGet( id, out var species ) || string.IsNullOrWhiteSpace( species.Key ) )
				continue;

			var key = NormalizeSpeciesKey( species.Key );
			if ( !keys.Contains( key, StringComparer.OrdinalIgnoreCase ) )
				keys.Add( key );
		}

		if ( keys.Count == 0
		     && fallbackSpeciesId > 0
		     && ThornsAnimalSpeciesRegistry.TryGet( fallbackSpeciesId, out var fallback )
		     && !string.IsNullOrWhiteSpace( fallback.Key ) )
		{
			keys.Add( NormalizeSpeciesKey( fallback.Key ) );
		}

		return CanonicalizeParentKeys( keys );
	}

	public static string BuildHybridKey( IReadOnlyList<string> canonicalParentKeys ) =>
		string.Join( "-", canonicalParentKeys );

	public static string BuildHybridModelPath( string hybridKey ) =>
		$"{ModelsRoot}/{hybridKey}/{hybridKey}.vmdl";

	public static Model LoadResolvedModel( in ResolvedVisual visual, ThornsAnimalSpeciesData bodySpecies )
	{
		if ( !string.IsNullOrWhiteSpace( visual.ModelPath )
		     && ThornsModelResourceLoad.TryLoadUsable( visual.ModelPath, out var visualModel ) )
			return visualModel;

		if ( bodySpecies is not null
		     && ThornsModelResourceLoad.TryLoadUsable( bodySpecies.ModelPath, out var bodyModel ) )
			return bodyModel;

		return default;
	}

	public static bool ApplyToGameObject( GameObject root, in ResolvedVisual visual, ThornsAnimalSpeciesData bodySpecies )
	{
		if ( !root.IsValid() )
			return false;

		var renderer = root.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( !renderer.IsValid() )
			return false;

		var model = LoadResolvedModel( visual, bodySpecies );
		if ( !ThornsModelResourceLoad.IsUsable( model ) )
			return false;

		renderer.Model = model;
		ThornsAnimalCameraGuard.ConfigureRenderer( renderer );
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );
		ThornsAnimalFactory.ConfigureAnimalHitCollider( root, model );
		return true;
	}

	static List<string> CanonicalizeParentKeys( IReadOnlyList<string> parentKeys )
	{
		if ( parentKeys is null || parentKeys.Count == 0 )
			return new List<string>();

		return parentKeys
			.Where( key => !string.IsNullOrWhiteSpace( key ) )
			.Select( NormalizeSpeciesKey )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.OrderBy( key => key, StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	static ResolvedVisual ResolveParentFallbackVisual( IReadOnlyList<string> parentKeys, ThornsAnimalSpeciesData bodySpecies )
	{
		foreach ( var key in PreferBodySpeciesFirst( parentKeys, bodySpecies?.Key ) )
		{
			if ( !ThornsAnimalSpeciesRegistry.TryGet( key, out var species ) )
				continue;

			if ( !TryLoadUsableModel( species.ModelPath ) )
				continue;

			return new ResolvedVisual
			{
				ModelPath = species.ModelPath,
				AnimPrefix = species.AnimPrefix ?? key,
				UsedHybridModel = false
			};
		}

		return ResolveForSpecies( bodySpecies );
	}

	static IEnumerable<string> PreferBodySpeciesFirst( IReadOnlyList<string> parentKeys, string bodySpeciesKey )
	{
		var bodyKey = NormalizeSpeciesKey( bodySpeciesKey );
		if ( !string.IsNullOrWhiteSpace( bodyKey ) )
			yield return bodyKey;

		foreach ( var key in parentKeys )
		{
			if ( string.Equals( key, bodyKey, StringComparison.OrdinalIgnoreCase ) )
				continue;

			yield return key;
		}
	}

	static string NormalizeSpeciesKey( string key ) => (key ?? "").Trim().ToLowerInvariant();

	static bool TryLoadUsableModel( string path ) =>
		ThornsModelResourceLoad.TryLoadUsable( path, out _ );

	static void LogHybridFallbackOnce( string hybridPath, IReadOnlyList<string> parentKeys )
	{
		if ( !LoggedHybridFallbacks.Add( hybridPath ) )
			return;

		Log.Info(
			$"[Thorns Animals] Hybrid model not mounted yet ('{hybridPath}'); using parent fallback [{string.Join( ", ", parentKeys )}]." );
	}
}
