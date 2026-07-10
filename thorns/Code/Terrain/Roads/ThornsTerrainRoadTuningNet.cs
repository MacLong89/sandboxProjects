namespace Sandbox;

/// <summary>Replicated road terrain tuning (flatten, falloff, scatter clearance).</summary>
public sealed class ThornsTerrainRoadTuningNet
{
	public float CityFlattenStrength { get; set; } = 0.74f;
	public float TownFlattenStrength { get; set; } = 0.56f;
	public float TrailFlattenStrength { get; set; } = 0.26f;

	public float CityEdgeFalloff { get; set; } = 52f;
	public float TownEdgeFalloff { get; set; } = 38f;
	public float TrailEdgeFalloff { get; set; } = 30f;

	public float CityInfluenceWidthMul { get; set; } = 1.18f;
	public float TownInfluenceWidthMul { get; set; } = 1.1f;
	public float TrailInfluenceWidthMul { get; set; } = 0.92f;

	public float FoliageClearanceRadius { get; set; } = 44f;
	public float BoulderClearanceRadius { get; set; } = 36f;

	public float DirtMaterialBlendStart { get; set; } = 0.22f;
	public float DirtMaterialBlendFull { get; set; } = 0.58f;

	public static ThornsTerrainRoadTuningNet EngineDefaults() => new();
}
