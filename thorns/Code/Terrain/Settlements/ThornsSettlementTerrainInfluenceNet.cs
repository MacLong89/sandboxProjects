namespace Sandbox;

/// <summary>Replicated settlement terrain influence (multi-ring blend + noise attenuation).</summary>
public sealed class ThornsSettlementTerrainInfluenceNet
{
	public float CenterX { get; set; }
	public float CenterY { get; set; }
	public float HubRadius { get; set; }
	public float CoreRadius { get; set; }
	public float TransitionRadius { get; set; }
	public float OuterFeatherRadius { get; set; }
	public float TargetZ { get; set; }
	public ThornsWorldSettlementKind Kind { get; set; }
}
