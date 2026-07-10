namespace Terraingen.Buildings;

using Terraingen.Buildings.Settlement;

/// <summary>Lot span → interior grid cells and exterior inches (gap eaten between merged lots).</summary>
public static class ThornsProcBuildingFootprintCatalog
{
	public static int GridCellsForLotSpan( int lotSpan )
	{
		if ( lotSpan <= 1 )
			return ThornsProcBuildingInterior.GridCells;

		var gapCells = (int)MathF.Round(
			SettlementGridConstants.BuildingGapNoRoadInches / SettlementGridConstants.CellInches );
		return lotSpan * SettlementGridConstants.BuildingCells + ( lotSpan - 1 ) * Math.Max( 1, gapCells );
	}

	public static (int WidthCells, int DepthCells) GridCellsForLotSpans( int spanWidth, int spanDepth ) => (
		GridCellsForLotSpan( spanWidth ),
		GridCellsForLotSpan( spanDepth ) );

	public static float ExteriorWidthInches( int widthCells ) =>
		ThornsBuildingModule.Cell * widthCells + ThornsBuildingModule.WallThickness * 2f;

	public static float ExteriorDepthInches( int depthCells ) =>
		ThornsBuildingModule.Cell * depthCells + ThornsBuildingModule.WallThickness * 2f;

	public static float RoofSurfaceWidthInches( int widthCells ) =>
		ExteriorWidthInches( widthCells ) + ThornsProcBuildingSilhouetteCatalog.RoofEaveOverhangInches * 2f;

	public static float RoofSurfaceDepthInches( int depthCells ) =>
		ExteriorDepthInches( depthCells ) + ThornsProcBuildingSilhouetteCatalog.RoofEaveOverhangInches * 2f;

	public static float TerrainHalfExtentXInches( int widthCells ) =>
		widthCells * ThornsBuildingModule.Cell * 0.5f + ThornsBuildingModule.WallThickness + 24f;

	public static float TerrainHalfExtentYInches( int depthCells ) =>
		depthCells * ThornsBuildingModule.Cell * 0.5f + ThornsBuildingModule.WallThickness + 24f;

	public static bool IsMegastructureType( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Warehouse
			or ThornsProcBuildingType.Factory
			or ThornsProcBuildingType.MilitaryComplex;
}
