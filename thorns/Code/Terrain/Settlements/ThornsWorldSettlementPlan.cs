namespace Sandbox;

/// <summary>Deterministic macro layout: one main city (12), three towns (5 each), three isolated wilderness POIs (~30 total).</summary>
public sealed class ThornsWorldSettlementPlan
{
	public const int MainCityBuildingCount = 12;
	public const int BuildingsPerTown = 5;
	public const int IsolatedSiteCount = 3;
	public const int TotalBuildingCount = MainCityBuildingCount + BuildingsPerTown * 3 + IsolatedSiteCount;

	public ThornsWorldSettlementZone MainCity { get; init; }
	public IReadOnlyList<ThornsWorldSettlementZone> Towns { get; init; }
	public IReadOnlyList<ThornsWorldIsolatedSite> IsolatedSites { get; init; }
	public IReadOnlyList<ThornsWorldTrailSegment> Trails { get; init; }
	public float IsolatedMinDistanceFromSettlements { get; init; }
	public int Seed { get; init; }
}

public sealed class ThornsWorldSettlementZone
{
	public ThornsWorldSettlementKind Kind { get; init; }
	public string Label { get; init; }
	public Vector2 CenterLocal { get; init; }
	public float Radius { get; init; }
	public ThornsProcBuildingDistrict PrimaryDistrict { get; init; }
	public float SpacingMultiplier { get; init; } = 1f;
	public IReadOnlyList<ThornsWorldBuildingSlot> BuildingSlots { get; init; }
}

public sealed class ThornsWorldBuildingSlot
{
	public ThornsProcBuildingType Type { get; init; }
	public ThornsWorldCityRing? CityRing { get; init; }
	public int Index { get; init; }
}

public sealed class ThornsWorldIsolatedSite
{
	public ThornsProcBuildingType Type { get; init; }
	public int Index { get; init; }
}

public sealed class ThornsWorldTrailSegment
{
	public Vector2 FromLocal { get; init; }
	public Vector2 ToLocal { get; init; }
	public ThornsWorldTrailKind Kind { get; init; }
}

public enum ThornsWorldTrailKind
{
	DirtRoad,
	Trail
}

public sealed class ThornsWorldSettlementConfig
{
	public float MainCityRadiusFraction { get; set; } = 0.065f;
	public float TownRadiusFraction { get; set; } = 0.052f;
	public float TownOrbitFractionMin { get; set; } = 0.42f;
	public float TownOrbitFractionMax { get; set; } = 0.58f;
	public float MinCityTownSeparationFraction { get; set; } = 0.28f;
	public float MinTownTownSeparationFraction { get; set; } = 0.22f;
	public float IsolatedSettlementClearanceFraction { get; set; } = 0.2f;
	public float CityCenterJitterFraction { get; set; } = 0.06f;
}
