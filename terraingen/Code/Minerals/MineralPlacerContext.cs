namespace Terraingen.Minerals;

/// <summary>Active mineral world service while scatter populate runs.</summary>
static class MineralPlacerContext
{
	public static ThornsMineralWorldService ActiveWorld { get; set; }
	public static ThornsMineralChunkInstances ActiveBuffer { get; set; }
}
