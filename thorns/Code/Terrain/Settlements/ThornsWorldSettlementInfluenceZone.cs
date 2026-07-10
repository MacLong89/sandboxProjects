namespace Sandbox;

/// <summary>Debug / diagnostics snapshot of a hub multi-ring terrain influence zone.</summary>
public sealed class ThornsWorldSettlementInfluenceZone
{
	public ThornsWorldSettlementKind Kind { get; init; }
	public Vector2 CenterLocal { get; init; }
	public float InfluenceRadius { get; init; }
	public float CoreRadius { get; init; }
	public float TransitionRadius { get; init; }
	public float OuterFeatherRadius { get; init; }
	public float TargetSurfaceZ { get; init; }
	public float BlendOuterRadius { get; init; }
}
