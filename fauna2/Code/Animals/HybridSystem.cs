namespace Fauna2;

/// <summary>
/// Breeding is same-species only. Cross-species pairs never produce offspring.
/// Kept as a single gate so genetics/breeding call sites stay explicit.
/// </summary>
public static class HybridSystem
{
	public static bool SameSpecies( AnimalDefinition a, AnimalDefinition b ) =>
		a is not null && b is not null && a.ResourceName == b.ResourceName;

	public static AnimalDefinition Resolve( AnimalDefinition a, AnimalDefinition b ) =>
		SameSpecies( a, b ) ? a : null;
}
