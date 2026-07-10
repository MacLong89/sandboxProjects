namespace Sandbox;

/// <summary>Why a single settlement building placement attempt was rejected.</summary>
public enum ThornsWorldSettlementPlacementFailureReason
{
	Overlap,
	TerrainSlope,
	TerrainCornerDelta,
	TerrainVariance,
	CliffSeverity,
	BlueprintInvalid,
	FallbackInvalid,
	NoValidYaw,
	NoValidRingSlot,
	Unknown
}
