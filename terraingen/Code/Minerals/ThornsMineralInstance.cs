namespace Terraingen.Minerals;

/// <summary>Harvestable stone/ore scatter node.</summary>
public sealed class ThornsMineralInstance : Component
{
	public int NodeId { get; internal set; }
	public MineralKind Kind { get; internal set; }
	internal bool ShadowsEnabled { get; set; } = true;
}
