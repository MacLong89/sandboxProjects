namespace Terraingen.Buildings.Settlement;

using Terraingen.Buildings;

/// <summary>
/// City blocks separated by 500&quot; streets; buildings inside a block sit 150&quot; apart with no dirt path between them.
/// </summary>
public static class SettlementBlockGridBuilder
{
	public readonly struct BlockCitySpec
	{
		public readonly int BlockCols;
		public readonly int BlockRows;
		public readonly int BuildingsPerBlockCol;
		public readonly int BuildingsPerBlockRow;

		public BlockCitySpec( int blockCols, int blockRows, int buildingsPerBlockCol, int buildingsPerBlockRow )
		{
			BlockCols = blockCols;
			BlockRows = blockRows;
			BuildingsPerBlockCol = buildingsPerBlockCol;
			BuildingsPerBlockRow = buildingsPerBlockRow;
		}

		public int TotalBuildingSlots =>
			BlockCols * BlockRows * BuildingsPerBlockCol * BuildingsPerBlockRow;
	}

	public static BlockCitySpec ResolveSpec( int targetCount, bool compact )
	{
		var target = Math.Clamp( targetCount, 2, 20 );
		BlockCitySpec best = default;
		var bestScore = int.MaxValue;
		var maxBlocks = compact ? 2 : 3;
		var maxBuildingsPerBlock = compact ? 3 : 4;

		for ( var blockRows = 1; blockRows <= maxBlocks; blockRows++ )
		{
			for ( var blockCols = 1; blockCols <= maxBlocks; blockCols++ )
			{
				for ( var buildingsPerRow = 1; buildingsPerRow <= maxBuildingsPerBlock; buildingsPerRow++ )
				{
					for ( var buildingsPerCol = 1; buildingsPerCol <= maxBuildingsPerBlock; buildingsPerCol++ )
					{
						var total = blockCols * blockRows * buildingsPerCol * buildingsPerRow;
						if ( total < target - 2 || total > target + 4 )
							continue;

						var score = Math.Abs( total - target ) * 12
						            + Math.Abs( blockCols - blockRows ) * 3
						            + Math.Abs( buildingsPerCol - buildingsPerRow ) * 2
						            + (blockCols * blockRows == 1 ? 4 : 0);
						if ( score >= bestScore )
							continue;

						bestScore = score;
						best = new BlockCitySpec(
							blockCols,
							blockRows,
							buildingsPerCol,
							buildingsPerRow );
					}
				}
			}
		}

		if ( bestScore == int.MaxValue )
		{
			var blocks = Math.Clamp( (int)MathF.Round( MathF.Sqrt( target / 4f ) ), 1, 2 );
			var perSide = Math.Clamp( (int)MathF.Ceiling( MathF.Sqrt( target / (blocks * blocks) ) ), 2, 3 );
			best = new BlockCitySpec( blocks, blocks, perSide, perSide );
		}

		return best;
	}

	public static (int HalfWidthCells, int HalfHeightCells) MeasureExtents( BlockCitySpec spec )
	{
		var (width, height) = MeasureExtentsInches( spec );
		return (
			Math.Max( 4, (int)MathF.Ceiling( width * 0.5f / SettlementGridConstants.CellInches ) ),
			Math.Max( 4, (int)MathF.Ceiling( height * 0.5f / SettlementGridConstants.CellInches ) ) );
	}

	public static void Build(
		BlockCitySpec spec,
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		List<SettlementGridBuildingSlot> slots )
	{
		roads.Clear();
		roadTypes.Clear();
		slots.Clear();

		var (totalWidth, totalHeight) = MeasureExtentsInches( spec );
		var blockWidth = BlockSpanInches( spec.BuildingsPerBlockCol );
		var blockHeight = BlockSpanInches( spec.BuildingsPerBlockRow );
		var street = SettlementGridConstants.StreetWidthInches;
		var originX = -totalWidth * 0.5f;
		var originY = -totalHeight * 0.5f;

		AddStreetGrid(
			roads,
			roadTypes,
			spec,
			originX,
			originY,
			totalWidth,
			totalHeight,
			blockWidth,
			blockHeight,
			street );

		for ( var blockRow = 0; blockRow < spec.BlockRows; blockRow++ )
		{
			for ( var blockCol = 0; blockCol < spec.BlockCols; blockCol++ )
			{
				var blockOriginX = originX + street + blockCol * (blockWidth + street);
				var blockOriginY = originY + street + blockRow * (blockHeight + street);
				PlaceBlockBuildings(
					spec,
					blockOriginX,
					blockOriginY,
					slots );
			}
		}

		ClassifyRoadTypes( roads, roadTypes, spec, originX, originY, blockWidth, blockHeight, street );
	}

	public static void ExtendMidLane(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		int extensionCells )
	{
		if ( extensionCells <= 0 || roads.Count == 0 )
			return;

		var centerX = roads
			.Where( c => roadTypes.GetValueOrDefault( c ) == SettlementRoadType.Primary )
			.Select( c => c.X )
			.DefaultIfEmpty( 0 )
			.GroupBy( x => x )
			.OrderByDescending( g => g.Count() )
			.First()
			.Key;
		var maxY = roads.Max( c => c.Y );
		var minY = roads.Min( c => c.Y );

		for ( var y = maxY + 1; y <= maxY + extensionCells; y++ )
			AddRoad( roads, roadTypes, new SettlementGridCell( centerX, y ), SettlementRoadType.Primary );
		for ( var y = minY - 1; y >= minY - extensionCells; y-- )
			AddRoad( roads, roadTypes, new SettlementGridCell( centerX, y ), SettlementRoadType.Primary );
	}

	static (float Width, float Height) MeasureExtentsInches( BlockCitySpec spec )
	{
		var street = SettlementGridConstants.StreetWidthInches;
		var blockWidth = BlockSpanInches( spec.BuildingsPerBlockCol );
		var blockHeight = BlockSpanInches( spec.BuildingsPerBlockRow );
		var width = (spec.BlockCols + 1) * street + spec.BlockCols * blockWidth;
		var height = (spec.BlockRows + 1) * street + spec.BlockRows * blockHeight;
		return (width, height);
	}

	static float BlockSpanInches( int buildingCount )
	{
		if ( buildingCount <= 0 )
			return 0f;

		return buildingCount * SettlementGridConstants.BuildingFootprintInches
		       + (buildingCount - 1) * SettlementGridConstants.BuildingGapNoRoadInches;
	}

	static void AddStreetGrid(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		BlockCitySpec spec,
		float originX,
		float originY,
		float totalWidth,
		float totalHeight,
		float blockWidth,
		float blockHeight,
		float streetWidth )
	{
		for ( var streetRow = 0; streetRow <= spec.BlockRows; streetRow++ )
		{
			var centerY = originY + streetWidth * 0.5f + streetRow * (blockHeight + streetWidth);
			AddStreetBand(
				roads,
				roadTypes,
				originX,
				originX + totalWidth,
				centerY,
				streetWidth,
				SettlementRoadType.Secondary );
		}

		for ( var streetCol = 0; streetCol <= spec.BlockCols; streetCol++ )
		{
			var centerX = originX + streetWidth * 0.5f + streetCol * (blockWidth + streetWidth);
			AddStreetBandVertical(
				roads,
				roadTypes,
				originY,
				originY + totalHeight,
				centerX,
				streetWidth,
				SettlementRoadType.Secondary );
		}
	}

	static void AddStreetBand(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		float xMin,
		float xMax,
		float centerY,
		float streetWidth,
		SettlementRoadType type )
	{
		var half = streetWidth * 0.5f;
		var cell = SettlementGridConstants.CellInches;
		for ( var y = centerY - half; y < centerY + half - 0.01f; y += cell )
		{
			for ( var x = xMin; x <= xMax - 0.01f; x += cell )
				AddRoad( roads, roadTypes, ToCell( x, y ), type );
		}
	}

	static void AddStreetBandVertical(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		float yMin,
		float yMax,
		float centerX,
		float streetWidth,
		SettlementRoadType type )
	{
		var half = streetWidth * 0.5f;
		var cell = SettlementGridConstants.CellInches;
		for ( var x = centerX - half; x < centerX + half - 0.01f; x += cell )
		{
			for ( var y = yMin; y <= yMax - 0.01f; y += cell )
				AddRoad( roads, roadTypes, ToCell( x, y ), type );
		}
	}

	static void PlaceBlockBuildings(
		BlockCitySpec spec,
		float blockOriginX,
		float blockOriginY,
		List<SettlementGridBuildingSlot> slots )
	{
		var halfFootprint = SettlementGridConstants.BuildingFootprintInches * 0.5f;
		var pitch = SettlementGridConstants.BuildingPitchNoRoadInches;
		var cell = SettlementGridConstants.CellInches;

		for ( var row = 0; row < spec.BuildingsPerBlockRow; row++ )
		{
			for ( var col = 0; col < spec.BuildingsPerBlockCol; col++ )
			{
				var centerX = blockOriginX + halfFootprint + col * pitch;
				var centerY = blockOriginY + halfFootprint + row * pitch;
				slots.Add( new SettlementGridBuildingSlot
				{
					CenterX = centerX / cell,
					CenterY = centerY / cell,
					YawDegrees = ResolveFacingYaw(
						centerX,
						centerY,
						blockOriginX,
						blockOriginY,
						spec ),
					RoadType = SettlementRoadType.Secondary,
					LocalCol = col,
					LocalRow = row
				} );
			}
		}
	}

	static float ResolveFacingYaw(
		float centerX,
		float centerY,
		float blockOriginX,
		float blockOriginY,
		BlockCitySpec spec )
	{
		var blockWidth = BlockSpanInches( spec.BuildingsPerBlockCol );
		var blockHeight = BlockSpanInches( spec.BuildingsPerBlockRow );

		var distNorth = MathF.Abs( centerY - (blockOriginY + blockHeight) );
		var distSouth = MathF.Abs( centerY - blockOriginY );
		var distEast = MathF.Abs( centerX - (blockOriginX + blockWidth) );
		var distWest = MathF.Abs( centerX - blockOriginX );

		if ( distNorth <= distSouth && distNorth <= distEast && distNorth <= distWest )
			return 0f;
		if ( distSouth <= distEast && distSouth <= distWest )
			return 180f;
		if ( distEast <= distWest )
			return 90f;

		return -90f;
	}

	static void ClassifyRoadTypes(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> types,
		BlockCitySpec spec,
		float originX,
		float originY,
		float blockWidth,
		float blockHeight,
		float streetWidth )
	{
		var centerCol = spec.BlockCols / 2;
		var centerRow = spec.BlockRows / 2;
		var centerX = originX + streetWidth * 0.5f + centerCol * (blockWidth + streetWidth);
		var centerY = originY + streetWidth * 0.5f + centerRow * (blockHeight + streetWidth);
		var halfStreet = streetWidth * 0.5f;
		var cell = SettlementGridConstants.CellInches;

		foreach ( var road in roads )
		{
			var worldX = road.X * cell;
			var worldY = road.Y * cell;
			var onCenterCol = MathF.Abs( worldX - centerX ) <= halfStreet + 0.01f;
			var onCenterRow = MathF.Abs( worldY - centerY ) <= halfStreet + 0.01f;
			types[road] = onCenterCol || onCenterRow
				? SettlementRoadType.Primary
				: SettlementRoadType.Secondary;
		}
	}

	static SettlementGridCell ToCell( float xInches, float yInches )
	{
		var cell = SettlementGridConstants.CellInches;
		return new SettlementGridCell(
			(int)MathF.Round( xInches / cell ),
			(int)MathF.Round( yInches / cell ) );
	}

	static void AddRoad(
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> types,
		SettlementGridCell cell,
		SettlementRoadType type )
	{
		roads.Add( cell );
		if ( !types.TryGetValue( cell, out var existing ) || RoadPriority( type ) > RoadPriority( existing ) )
			types[cell] = type;
	}

	static int RoadPriority( SettlementRoadType type ) => type switch
	{
		SettlementRoadType.Primary => 3,
		SettlementRoadType.Secondary => 2,
		SettlementRoadType.Connector => 1,
		SettlementRoadType.Alley => 0,
		_ => 0
	};
}
