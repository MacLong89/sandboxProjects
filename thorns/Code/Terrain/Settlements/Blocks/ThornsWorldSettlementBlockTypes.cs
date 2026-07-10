using System.Collections.Generic;

namespace Sandbox;

public enum ThornsWorldSettlementDistrictKind
{
	Core,
	MidCommercialResidential,
	OuterIndustrial,
	TownCenter,
	TownResidential,
	TownEdge
}

public enum ThornsWorldSettlementLotState
{
	Vacant,
	Assigned,
	Placed
}

public enum ThornsWorldRoadCorridorKind
{
	Radial,
	Ring,
	MainStreet,
	Trail
}

/// <summary>Reserved spline strip for implied roads (no mesh yet).</summary>
public sealed class ThornsWorldRoadCorridor
{
	public Vector2 A { get; init; }
	public Vector2 B { get; init; }
	public float HalfWidth { get; init; }
	public ThornsWorldRoadCorridorKind Kind { get; init; }
}

public sealed class ThornsWorldSettlementLot
{
	public int LotIndex { get; init; }
	public int BlockIndex { get; init; }
	public ThornsWorldSettlementDistrictKind District { get; init; }
	public ThornsWorldCityRing? CityRing { get; init; }
	public Vector2 CenterLocal { get; set; }
	public float HalfW { get; set; }
	public float HalfD { get; set; }
	public float YawRadians { get; set; }
	/// <summary>Unit vector — building front faces along this (toward road).</summary>
	public Vector2 FrontageDirection { get; set; }
	public ThornsWorldSettlementLotState State { get; set; }
	public ThornsProcBuildingType? AssignedType { get; set; }
	public int SlotIndex { get; set; } = -1;
	/// <summary>Manifest slot index this lot was laid out for (-1 = any).</summary>
	public int PreferredSlotIndex { get; set; } = -1;
	/// <summary>Copied from parent block after terracing — buildings in a lot share this.</summary>
	public float TargetSurfaceZ { get; set; }
}

public sealed class ThornsWorldSettlementBlock
{
	public int Index { get; init; }
	public ThornsWorldSettlementDistrictKind District { get; init; }
	public ThornsWorldCityRing? CityRing { get; init; }
	public Vector2 CenterLocal { get; init; }
	public float HalfW { get; init; }
	public float HalfD { get; init; }
	public float YawRadians { get; init; }
	/// <summary>Terraced local floor target for lots/buildings in this block.</summary>
	public float TargetSurfaceZ { get; set; }
	/// <summary>Assigned/placed buildings in this block — drives density-aware aprons.</summary>
	public int BuildingCount { get; set; } = 1;
	public List<ThornsWorldSettlementLot> Lots { get; init; } = new();
}

public sealed class ThornsWorldSettlementDistrictPlan
{
	public ThornsWorldSettlementDistrictKind Kind { get; init; }
	public ThornsWorldCityRing? CityRing { get; init; }
	public float InnerRadius { get; init; }
	public float OuterRadius { get; init; }
	public List<ThornsWorldSettlementBlock> Blocks { get; init; } = new();
}

public sealed class ThornsWorldSettlementAreaBlockPlan
{
	public ThornsWorldSettlementKind SettlementKind { get; init; }
	public int TownIndex { get; init; } = -1;
	public string Label { get; init; }
	public Vector2 CenterLocal { get; init; }
	public List<ThornsWorldSettlementDistrictPlan> Districts { get; init; } = new();
	public List<ThornsWorldRoadCorridor> Corridors { get; init; } = new();
	public List<ThornsWorldSettlementLot> Lots { get; init; } = new();
}
