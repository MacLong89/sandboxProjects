namespace Terraingen.Buildings.Settlement;

public readonly struct SettlementGridCell : IEquatable<SettlementGridCell>
{
	public readonly int X;
	public readonly int Y;

	public SettlementGridCell( int x, int y )
	{
		X = x;
		Y = y;
	}

	public bool Equals( SettlementGridCell other ) => X == other.X && Y == other.Y;

	public override bool Equals( object obj ) => obj is SettlementGridCell other && Equals( other );

	public override int GetHashCode() => HashCode.Combine( X, Y );

	public static bool operator ==( SettlementGridCell a, SettlementGridCell b ) => a.Equals( b );
	public static bool operator !=( SettlementGridCell a, SettlementGridCell b ) => !a.Equals( b );
}

public enum SettlementGridRoadAxis
{
	Horizontal,
	Vertical
}

	public sealed class SettlementGridBuildingSlot
{
	public float CenterX;
	public float CenterY;
	public float YawDegrees;
	public SettlementRoadType RoadType;
	public int LocalCol;
	public int LocalRow;
	public int LotSpanWidth = 1;
	public int LotSpanDepth = 1;
	public bool IsConsumed;
	public ThornsProcBuildingType? ForcedBuildingType;
}
