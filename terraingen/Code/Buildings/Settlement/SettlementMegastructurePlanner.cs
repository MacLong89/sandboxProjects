namespace Terraingen.Buildings.Settlement;

using Terraingen.Buildings;

/// <summary>Merge adjacent block slots into multi-lot industrial footprints (eats the 150&quot; gap).</summary>
public static class SettlementMegastructurePlanner
{
	public static void Apply(
		SettlementBlockGridBuilder.BlockCitySpec spec,
		List<SettlementGridBuildingSlot> slots,
		ThornsPoiIdentity identity,
		Random rng )
	{
		if ( slots is null || slots.Count == 0 )
			return;

		var budget = MaxMegastructures( identity );
		if ( budget <= 0 )
			return;

		var slotsPerBlock = spec.BuildingsPerBlockCol * spec.BuildingsPerBlockRow;
		var blockCount = spec.BlockCols * spec.BlockRows;

		for ( var blockIndex = 0; blockIndex < blockCount && budget > 0; blockIndex++ )
		{
			var blockStart = blockIndex * slotsPerBlock;
			if ( blockStart + slotsPerBlock > slots.Count )
				break;

			TryPlaceInBlock(
				slots,
				blockStart,
				spec.BuildingsPerBlockCol,
				spec.BuildingsPerBlockRow,
				identity,
				rng,
				ref budget );
		}
	}

	static int MaxMegastructures( ThornsPoiIdentity identity ) => identity switch
	{
		ThornsPoiIdentity.Metropolis => 3,
		ThornsPoiIdentity.City => 3,
		ThornsPoiIdentity.Town => 2,
		ThornsPoiIdentity.Suburb => 1,
		ThornsPoiIdentity.Military => 1,
		_ => 0
	};

	static void TryPlaceInBlock(
		List<SettlementGridBuildingSlot> slots,
		int blockStart,
		int cols,
		int rows,
		ThornsPoiIdentity identity,
		Random rng,
		ref int budget )
	{
		if ( identity == ThornsPoiIdentity.Military )
		{
			for ( var row = 0; row <= rows - 2; row++ )
			{
				for ( var col = 0; col <= cols - 2; col++ )
				{
					if ( !TryMerge2x2( slots, blockStart, cols, rows, row, col, rng, ThornsProcBuildingType.MilitaryComplex ) )
						continue;

					budget--;
					return;
				}
			}

			if ( TryMerge2x1Horizontal( slots, blockStart, cols, rows, rng, ThornsProcBuildingType.MilitaryComplex ) )
				budget--;

			return;
		}

		var candidates = new List<(int Row, int Col, int Kind)>();
		for ( var row = 0; row < rows; row++ )
		{
			for ( var col = 0; col < cols - 1; col++ )
				candidates.Add( (row, col, 1) );
		}

		for ( var row = 0; row < rows - 1; row++ )
		{
			for ( var col = 0; col < cols - 1; col++ )
				candidates.Add( (row, col, 2) );
		}

		Shuffle( candidates, rng );

		foreach ( var (row, col, kind) in candidates )
		{
			if ( budget <= 0 )
				break;

			if ( kind == 2 )
			{
				if ( rng.NextSingle() > Factory2x2Chance( identity ) )
					continue;

				if ( !TryMerge2x2( slots, blockStart, cols, rows, row, col, rng, ThornsProcBuildingType.Factory ) )
					continue;

				budget--;
				continue;
			}

			if ( rng.NextSingle() > Wide2x1Chance( identity ) )
				continue;

			var type = rng.NextSingle() < 0.55f
				? ThornsProcBuildingType.Warehouse
				: ThornsProcBuildingType.MilitaryComplex;

			if ( !TryMerge2x1Horizontal( slots, blockStart, cols, rows, row, col, rng, type ) )
				continue;

			budget--;
		}
	}

	static float Factory2x2Chance( ThornsPoiIdentity identity ) => identity switch
	{
		ThornsPoiIdentity.Metropolis => 0.42f,
		ThornsPoiIdentity.City => 0.34f,
		ThornsPoiIdentity.Town => 0.22f,
		_ => 0.12f
	};

	static float Wide2x1Chance( ThornsPoiIdentity identity ) => identity switch
	{
		ThornsPoiIdentity.Metropolis => 0.48f,
		ThornsPoiIdentity.City => 0.42f,
		ThornsPoiIdentity.Town => 0.34f,
		ThornsPoiIdentity.Suburb => 0.28f,
		_ => 0f
	};

	static bool TryMerge2x2(
		List<SettlementGridBuildingSlot> slots,
		int blockStart,
		int cols,
		int rows,
		int row,
		int col,
		Random rng,
		ThornsProcBuildingType type )
	{
		if ( row < 0 || col < 0 || row + 1 >= rows || col + 1 >= cols )
			return false;

		var a = GetSlot( slots, blockStart, cols, row, col );
		var b = GetSlot( slots, blockStart, cols, row, col + 1 );
		var c = GetSlot( slots, blockStart, cols, row + 1, col );
		var d = GetSlot( slots, blockStart, cols, row + 1, col + 1 );
		if ( a is null || b is null || c is null || d is null )
			return false;

		if ( a.IsConsumed || b.IsConsumed || c.IsConsumed || d.IsConsumed )
			return false;

		a.LotSpanWidth = 2;
		a.LotSpanDepth = 2;
		a.CenterX = (a.CenterX + b.CenterX + c.CenterX + d.CenterX) * 0.25f;
		a.CenterY = (a.CenterY + b.CenterY + c.CenterY + d.CenterY) * 0.25f;
		a.ForcedBuildingType = type;
		b.IsConsumed = true;
		c.IsConsumed = true;
		d.IsConsumed = true;
		_ = rng;
		return true;
	}

	static bool TryMerge2x1Horizontal(
		List<SettlementGridBuildingSlot> slots,
		int blockStart,
		int cols,
		int rows,
		Random rng,
		ThornsProcBuildingType type )
	{
		for ( var row = 0; row < rows; row++ )
		{
			for ( var col = 0; col < cols - 1; col++ )
			{
				if ( TryMerge2x1Horizontal( slots, blockStart, cols, rows, row, col, rng, type ) )
					return true;
			}
		}

		return false;
	}

	static bool TryMerge2x1Horizontal(
		List<SettlementGridBuildingSlot> slots,
		int blockStart,
		int cols,
		int rows,
		int row,
		int col,
		Random rng,
		ThornsProcBuildingType type )
	{
		_ = rows;
		_ = rng;
		if ( col + 1 >= cols )
			return false;

		var a = GetSlot( slots, blockStart, cols, row, col );
		var b = GetSlot( slots, blockStart, cols, row, col + 1 );
		if ( a is null || b is null || a.IsConsumed || b.IsConsumed )
			return false;

		a.LotSpanWidth = 2;
		a.CenterX = (a.CenterX + b.CenterX) * 0.5f;
		a.ForcedBuildingType = type;
		b.IsConsumed = true;
		return true;
	}

	static SettlementGridBuildingSlot GetSlot(
		List<SettlementGridBuildingSlot> slots,
		int blockStart,
		int cols,
		int row,
		int col )
	{
		var index = blockStart + row * cols + col;
		return index < 0 || index >= slots.Count ? null : slots[index];
	}

	static void Shuffle<T>( List<T> list, Random rng )
	{
		for ( var i = list.Count - 1; i > 0; i-- )
		{
			var j = rng.Next( i + 1 );
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
