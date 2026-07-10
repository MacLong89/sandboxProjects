namespace Terraingen.Foliage;

/// <summary>
/// Chunk-scale ecology summary for multi-scale spawning budgets.
/// </summary>
public readonly struct ThornsFoliageChunkEcology
{
	public float ForestMass { get; init; }
	public float Opening { get; init; }
	public float Treeline { get; init; }
	public float RiverCorridor { get; init; }
	public float Wetland { get; init; }
	public float TreeSuitability { get; init; }
	public float GrassSuitability { get; init; }
	public float TreeDensityScale { get; init; }
	public float GrassDensityScale { get; init; }
	public Vector2 FlowDirection { get; init; }
	public float HeroTreeChance { get; init; }
}
