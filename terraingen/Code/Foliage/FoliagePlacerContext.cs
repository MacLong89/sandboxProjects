namespace Terraingen.Foliage;

/// <summary>Per-chunk spawn target while populating (instanced or GameObject).</summary>
static class FoliagePlacerContext
{
	public static ThornsFoliageChunkInstances ActiveBuffer { get; set; }

	/// <summary>Scene-tree LOD tags collected while filling the current chunk.</summary>
	public static List<ThornsFoliageInstance> ActiveLodInstances { get; set; }

	/// <summary>Tree registry used during populate — avoids <see cref="ThornsTreeWorldService.Instance"/> race before OnStart.</summary>
	public static ThornsTreeWorldService ActiveTreeService { get; set; }
}
