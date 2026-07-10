namespace Terraingen.Rendering;

/// <summary>Shared client visual LOD bands — visibility/cull radii stay on per-system configs; these tune shadow cost only.</summary>
public static class ThornsVisualPerformanceDistances
{
	public const float TreeShadowInches = 20000f;
	public const float BoulderShadowInches = 32000f;
	public const float MineralShadowInches = 22000f;
	public const float ProcBuildingShadowInches = 26000f;
	public const float ShadowLodHysteresisInches = 8000f;
}
