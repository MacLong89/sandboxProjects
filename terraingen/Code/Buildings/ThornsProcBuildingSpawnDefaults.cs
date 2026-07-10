namespace Terraingen.Buildings;

/// <summary>Shared proc-building spawn parameters (floorplan test gallery and world towns).</summary>
public static class ThornsProcBuildingSpawnDefaults
{
	/// <summary>Maximum storeys per proc building — world generation samples per-building heights up to this cap (1–6).</summary>
	public const int MaxStories = 6;

	/// <summary>Default cap passed to world building config.</summary>
	public const int Stories = MaxStories;

	/// <summary>ASCII furniture layout variant — matches floorplan test default.</summary>
	public const int LayoutVariantIndex = 0;

	/// <summary>Minimum foundation slab depth (inches) — matches floorplan test.</summary>
	public const float MinFoundationDepthInches = 24f;
}
