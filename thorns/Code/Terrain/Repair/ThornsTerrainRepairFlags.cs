namespace Sandbox;

[Flags]
public enum ThornsTerrainCellFault : byte
{
	None = 0,
	NonFinite = 1 << 0,
	OutOfRange = 1 << 1,
	ExcessiveStep = 1 << 2,
	SteepSlope = 1 << 3,
	StretchedQuad = 1 << 4,
	IsolatedSpike = 1 << 5,
	Repaired = 1 << 6,
}
