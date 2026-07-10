namespace Terraingen.Buildings.Settlement;

using Terraingen.Buildings;

/// <summary>Local grid cell sizes for tac-map settlement layout (100&quot; cells).</summary>
public static class SettlementGridConstants
{
	public const int BuildingCells = 3;
	public const int RoadCells = 1;
	public const float CellInches = ThornsBuildingModule.Cell;

	/// <summary>Default proc building floor footprint (3×3 cells).</summary>
	public const float BuildingFootprintInches = CellInches * BuildingCells;

	/// <summary>Painted dirt path width for block streets and arterials.</summary>
	public const float GridPathWidthInches = 500f;

	public const float GridMainLanePathWidthInches = GridPathWidthInches;
	public const float GridSidePathWidthInches = GridPathWidthInches;

	/// <summary>Street corridor width between building blocks (not between every lot).</summary>
	public const float StreetWidthInches = 500f;

	public const int StreetCells = 5;

	/// <summary>Edge-to-edge gap between buildings in the same block when no street sits between them.</summary>
	public const float BuildingGapNoRoadInches = 150f;

	/// <summary>Center-to-center pitch for adjacent lots in the same block row (300 + 150).</summary>
	public const float BuildingPitchNoRoadInches = BuildingFootprintInches + BuildingGapNoRoadInches;

	/// <summary>Empty cells kept between road paint and building footprint in grid occupancy.</summary>
	public const int RoadBuildingGapCells = 1;

	public const int MiniGridMinBuildings = 2;
	public const int CompactMinBuildings = 6;
	public const int FullBlockMinBuildings = 11;
	public const int InterCityExtensionCells = 6;

	public static int FootprintHalfCells =>
		(int)MathF.Ceiling( (ThornsBuildingModule.Cell * 1.5f + 24f) / CellInches );

	static float FootprintHalfInches => ThornsBuildingModule.Cell * 1.5f + 24f;

	public static int BuildingCenterOffsetFromMainCells =>
		(int)MathF.Ceiling( (FootprintHalfInches + GridPathWidthInches * 0.5f) / CellInches );

	public static int BuildingCenterOffsetFromSubStreetCells =>
		(int)MathF.Ceiling( (FootprintHalfInches + GridPathWidthInches * 0.5f) / CellInches );

	public static int BuildingCenterOffsetCells => BuildingCenterOffsetFromMainCells;
}
