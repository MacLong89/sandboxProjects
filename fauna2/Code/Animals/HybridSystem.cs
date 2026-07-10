namespace Fauna2;

/// <summary>Hybrids disabled in fauna2 MVP — breeding is same-species only.</summary>
public static class HybridSystem
{
	public static bool CouldHybridize( AnimalDefinition a, AnimalDefinition b ) => false;

	public static AnimalDefinition Resolve( AnimalDefinition a, AnimalDefinition b ) => a;
}
