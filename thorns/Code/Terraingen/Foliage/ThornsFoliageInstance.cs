namespace Terraingen.Foliage;

/// <summary>
/// Tags spawned foliage for distance LOD updates (shadows, visibility).
/// </summary>
public sealed class ThornsFoliageInstance : Component
{
	internal ModelRenderer Renderer { get; set; }

	/// <summary>0 = hidden, 1 = visible far (no shadows), 2 = visible near (shadows).</summary>
	internal byte LodState { get; set; }
}
