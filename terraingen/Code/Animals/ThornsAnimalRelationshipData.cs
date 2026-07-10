namespace Terraingen.Animals;

/// <summary>Optional shared relationship table — v1 uses per-species lists on <see cref="ThornsAnimalSpeciesData"/>.</summary>
public sealed class ThornsAnimalRelationshipData
{
	public ushort AttackerSpeciesId { get; init; }
	public ushort TargetSpeciesId { get; init; }
	public bool IsPrey { get; init; }
	public bool IsThreat { get; init; }
	public bool CanAttack { get; init; }
}
