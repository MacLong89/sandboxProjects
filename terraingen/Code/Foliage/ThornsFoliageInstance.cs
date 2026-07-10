namespace Terraingen.Foliage;

/// <summary>
/// Tags spawned foliage for distance LOD updates (shadows, mesh/billboard swap, visibility).
/// </summary>
public sealed class ThornsFoliageInstance : Component
{
	internal ModelRenderer Renderer { get; set; }

	internal ModelRenderer BillboardRenderer { get; set; }

	internal Collider Collider { get; set; }

	internal FoliageSpecies Species { get; set; }

	internal float BillboardWorldHeight { get; set; }

	/// <summary>0 = hidden, 1 = billboard, 2 = mesh far (no shadows), 3 = mesh near (shadows).</summary>
	internal byte LodState { get; set; }
}
