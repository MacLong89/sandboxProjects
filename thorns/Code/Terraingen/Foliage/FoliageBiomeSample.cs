namespace Terraingen.Foliage;

/// <summary>
/// Biome and ecosystem weights at a world position.
/// </summary>
public readonly struct FoliageBiomeSample
{
	public float Height { get; init; }
	public float Slope { get; init; }
	public float Moisture { get; init; }
	public float Valley { get; init; }
	public float Alpine { get; init; }
	public float Cliff { get; init; }

	public float ForestMass { get; init; }
	public float Opening { get; init; }
	public float Treeline { get; init; }
	public float RiverCorridor { get; init; }
	public float Wetland { get; init; }
	public float RidgeExposure { get; init; }
	public float TreeDensityScale { get; init; }
	public float GrassDensityScale { get; init; }
	public Vector2 FlowDirection { get; init; }

	public float PineWeight { get; init; }
	public float AspenWeight { get; init; }
	public float OakWeight { get; init; }
	public float GrassWeight { get; init; }
	public bool CanPlaceTrees { get; init; }
	public bool CanPlaceGrass { get; init; }
}
