namespace Fauna2;

/// <summary>
/// Fast local registry of live animals. Populated on every machine (animals are
/// networked objects) so both host simulation and client UI can query cheaply
/// without scene scans.
/// </summary>
public static class AnimalRegistry
{
	private static readonly List<AnimalComponent> _all = new();

	private static readonly HashSet<string> _distinctSpecies = new();

	public static IReadOnlyList<AnimalComponent> All => _all;
	public static int Count => _all.Count;

	public static void Register( AnimalComponent animal )
	{
		if ( !_all.Contains( animal ) )
		{
			_all.Add( animal );
			if ( !string.IsNullOrEmpty( animal.DefinitionId ) )
				_distinctSpecies.Add( animal.DefinitionId );
			GameEvents.RaiseAnimalSpawned( animal );
		}
	}

	public static void Unregister( AnimalComponent animal )
	{
		if ( !_all.Remove( animal ) )
			return;

		if ( !string.IsNullOrEmpty( animal.DefinitionId )
			&& !_all.Any( a => a.DefinitionId == animal.DefinitionId ) )
			_distinctSpecies.Remove( animal.DefinitionId );

		GameEvents.RaiseAnimalRemoved( animal );
	}

	public static AnimalComponent Find( string animalId ) =>
		_all.FirstOrDefault( a => a.AnimalId == animalId );

	public static IEnumerable<AnimalComponent> InHabitat( string habitatId ) =>
		_all.Where( a => a.HabitatId == habitatId );

	public static int CountInHabitat( string habitatId, string definitionId = null )
	{
		var count = 0;
		foreach ( var a in _all )
		{
			if ( a.HabitatId != habitatId ) continue;
			if ( definitionId is not null && a.DefinitionId != definitionId ) continue;
			count++;
		}
		return count;
	}

	public static AnimalComponent RandomInHabitat( string habitatId, string definitionId, AnimalComponent exclude )
	{
		var candidates = _all.Where( a =>
			a.HabitatId == habitatId &&
			a.DefinitionId == definitionId &&
			a != exclude ).ToList();

		return candidates.Count == 0 ? null : candidates[Game.Random.Int( 0, candidates.Count - 1 )];
	}

	public static int DistinctSpeciesCount() => _distinctSpecies.Count;

	public static void Clear()
	{
		_all.Clear();
		_distinctSpecies.Clear();
	}
}
