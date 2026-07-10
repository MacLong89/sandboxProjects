namespace Sandbox;

/// <summary>Data-driven layered tile definition for one <see cref="ThornsProcBuildingType"/>.</summary>
public sealed class ThornsProcTileBlueprint
{
	public ThornsProcBuildingType Type { get; init; }
	public string DisplayName { get; init; }
	public float WindowChance { get; init; } = 0.2f;
	public bool PreferFrontWindows { get; init; }
	/// <summary>When true, compile may apply <see cref="ThornsProcTileRuinMutator"/> for damaged variants.</summary>
	public bool AllowDamagedVariant { get; init; }
	public IReadOnlyList<ThornsProcTileLayer> Layers { get; init; }

	public int Stories => Layers?.Count ?? 0;
	public int Width => Layers is { Count: > 0 } ? Layers[0].Width : 0;
	public int Depth => Layers is { Count: > 0 } ? Layers[0].Depth : 0;
}
