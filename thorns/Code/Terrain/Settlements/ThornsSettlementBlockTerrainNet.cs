namespace Sandbox;

/// <summary>Replicated terraced block target for settlement height blending.</summary>
public sealed class ThornsSettlementBlockTerrainNet
{
	public float CenterX { get; set; }
	public float CenterY { get; set; }
	public float HalfW { get; set; }
	public float HalfD { get; set; }
	public float YawRadians { get; set; }
	public float TargetZ { get; set; }
	public ThornsWorldSettlementKind Kind { get; set; }
	public int BlockIndex { get; set; } = -1;
	public int BuildingCount { get; set; } = 1;
	/// <summary>0–1 blend strength for shared block surface pass.</summary>
	public float SurfaceStrength { get; set; } = 0.4f;
}
