namespace Terraingen.Buildings;

/// <summary>Extra wall-adjacent props for multi-lot footprints (keeps large interiors sparse).</summary>
public static class ThornsInteriorMegastructureFill
{
	public const int LargeFootprintMinCells = ThornsProcBuildingInterior.GridCells + 1;
	public const int MaxPlacementsPerStory = 4;

	public static bool IsLargeFootprint( int widthCells, int depthCells ) =>
		widthCells >= LargeFootprintMinCells || depthCells >= LargeFootprintMinCells;

	public static int CollectFillPlacements(
		ThornsProcBuildingType type,
		int variantIndex,
		int storyIndex,
		int widthCells,
		int depthCells,
		List<ThornsProcBuildingInterior.CellPlacement> into,
		int stories = 3,
		int maxAdditional = MaxPlacementsPerStory,
		bool[,,] skipFloor = null )
	{
		if ( into is null || !IsLargeFootprint( widthCells, depthCells ) || maxAdditional <= 0 )
			return 0;

		if ( !ThornsProcBuildingFootprintCatalog.IsMegastructureType( type )
		     && type is not ThornsProcBuildingType.Barn )
			return 0;

		var pool = GetFillPool( type, storyIndex );
		if ( pool.Length == 0 )
			return 0;

		var budget = Math.Min( maxAdditional, MaxPlacementsPerStory );
		if ( budget <= 0 )
			return 0;

		var placedCells = new HashSet<(int x, int y)>();
		for ( var i = 0; i < into.Count; i++ )
		{
			var cell = into[i];
			if ( cell.Story == storyIndex )
				placedCells.Add( (cell.GridX, cell.GridY) );
		}

		var added = 0;
		var stride = widthCells >= 8 || depthCells >= 8 ? 3 : 2;
		var phase = (storyIndex + variantIndex) % stride;

		TryWallFillOnEdge( stride, phase, 0, 0, widthCells - 1, 0, isHorizontal: true );
		TryWallFillOnEdge( stride, phase, 0, depthCells - 1, widthCells - 1, depthCells - 1, isHorizontal: true );
		TryWallFillOnEdge( stride, phase, 0, 1, 0, depthCells - 2, isHorizontal: false );
		TryWallFillOnEdge( stride, phase, widthCells - 1, 1, widthCells - 1, depthCells - 2, isHorizontal: false );

		return added;

		void TryWallFillOnEdge(
			int edgeStride,
			int edgePhase,
			int x0,
			int y0,
			int x1,
			int y1,
			bool isHorizontal )
		{
			if ( isHorizontal )
			{
				var y = y0;
				for ( var x = x0 + edgePhase; x <= x1; x += edgeStride )
					TryPlaceWallCell( x, y );
			}
			else
			{
				var x = x0;
				for ( var y = y0 + edgePhase; y <= y1; y += edgeStride )
					TryPlaceWallCell( x, y );
			}
		}

		void TryPlaceWallCell( int x, int y )
		{
			if ( added >= budget )
				return;

			if ( !ThornsProcBuildingInterior.IsWallAdjacentFloorCell( x, y, widthCells, depthCells ) )
				return;

			if ( !placedCells.Add( (x, y) ) )
				return;

			if ( ThornsInteriorFurnitureCanonicalSlots.IsFurnitureCellBlocked(
				     storyIndex,
				     x,
				     y,
				     widthCells,
				     depthCells,
				     stories,
				     skipFloor ) )
			{
				placedCells.Remove( (x, y) );
				return;
			}

			var pick = pool[PickIndex( type, variantIndex, storyIndex, x, y ) % pool.Length];
			into.Add( new ThornsProcBuildingInterior.CellPlacement( storyIndex, x, y, pick ) );
			added++;
		}
	}

	public static int CountStoryPlacements(
		IReadOnlyList<ThornsProcBuildingInterior.CellPlacement> placements,
		int storyIndex )
	{
		if ( placements is null )
			return 0;

		var count = 0;
		for ( var i = 0; i < placements.Count; i++ )
		{
			if ( placements[i].Story == storyIndex )
				count++;
		}

		return count;
	}

	public static void TrimStoryPlacements(
		List<ThornsProcBuildingInterior.CellPlacement> placements,
		int storyIndex,
		int maxCount )
	{
		if ( placements is null || maxCount < 0 )
			return;

		for ( var i = placements.Count - 1; i >= 0 && CountStoryPlacements( placements, storyIndex ) > maxCount; i-- )
		{
			if ( placements[i].Story == storyIndex )
				placements.RemoveAt( i );
		}
	}

	static int PickIndex( ThornsProcBuildingType type, int variantIndex, int storyIndex, int gridX, int gridY ) =>
		Math.Abs( HashCode.Combine( (int)type, variantIndex, storyIndex, gridX, gridY, 0xBEE71D ) );

	static string[] GetFillPool( ThornsProcBuildingType type, int storyIndex )
	{
		if ( storyIndex <= 0 )
		{
			return type switch
			{
				ThornsProcBuildingType.Warehouse => ["pallets", "pallets", "chair", "fridge", "pallets"],
				ThornsProcBuildingType.Factory => ["workbench", "pallets", "pallets", "chair", "workbench"],
				ThornsProcBuildingType.MilitaryComplex => ["military_supply", "pallets", "chair", "conference", "military_supply"],
				ThornsProcBuildingType.Barn => ["pallets", "pallets", "chair", "fridge"],
				_ => []
			};
		}

		return type switch
		{
			ThornsProcBuildingType.Warehouse => ["pallets", "pallets", "workbench", "chair", "pallets"],
			ThornsProcBuildingType.Factory => ["workbench", "pallets", "workbench", "chair", "pallets"],
			ThornsProcBuildingType.MilitaryComplex => ["military_supply", "pallets", "chair", "military_supply", "conference"],
			ThornsProcBuildingType.Barn => ["pallets", "chair", "pallets"],
			_ => []
		};
	}
}
