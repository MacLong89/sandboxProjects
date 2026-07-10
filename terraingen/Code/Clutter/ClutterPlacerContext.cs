namespace Terraingen.Clutter;

/// <summary>Thread-local-style buffer for instanced mesh clutter during chunk build.</summary>
public static class ClutterPlacerContext
{
	public static Dictionary<int, List<Transform>> ActiveInstancedBuffer { get; set; }
}
