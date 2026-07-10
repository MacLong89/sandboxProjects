using Dynasty.Core.Enums;

namespace Dynasty.Data;

public sealed class FormationSlot
{
	public string SlotKey { get; init; } = "";
	public string DisplayLabel { get; init; } = "";
	public float NormalizedX { get; init; }
	public float NormalizedY { get; init; }
	public Position[] EligiblePositions { get; init; } = Array.Empty<Position>();
	public bool IsOptional { get; init; }
}
