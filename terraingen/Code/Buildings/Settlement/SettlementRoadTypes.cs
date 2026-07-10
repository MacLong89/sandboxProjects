namespace Terraingen.Buildings.Settlement;

/// <summary>Hierarchy level for painted dirt roads in street-first settlements.</summary>
public enum SettlementRoadType
{
	Primary,
	Secondary,
	Alley,
	Connector
}

/// <summary>Promoted road segment used for layout generation and terrain painting.</summary>
public readonly struct SettlementRoadSegment
{
	public readonly Vector3 Start;
	public readonly Vector3 End;
	public readonly float Width;
	public readonly SettlementRoadType Type;

	public SettlementRoadSegment( Vector3 start, Vector3 end, float width, SettlementRoadType type )
	{
		Start = start;
		End = end;
		Width = width;
		Type = type;
	}

	public float LengthSquared => (End - Start).LengthSquared;

	public Vector3 Direction
	{
		get
		{
			var d = End - Start;
			d.z = 0f;
			return d.LengthSquared > 1e-6f ? d.Normal : Vector3.Forward;
		}
	}
}
