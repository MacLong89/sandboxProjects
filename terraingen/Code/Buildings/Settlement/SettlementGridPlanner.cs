namespace Terraingen.Buildings.Settlement;

using Terraingen.Buildings;
using Terraingen.TerrainGen;

/// <summary>Count-scaled R/BBB tile block planner for organized POI layout.</summary>
public static class SettlementGridPlanner
{
	public static bool TryGenerate(
		Terrain terrain,
		ThornsTerrainConfig terrainConfig,
		SettlementSitePlan plan,
		int settlementIndex,
		int worldSeed,
		SettlementLayoutMode mode,
		float primaryPathWidth,
		float secondaryPathWidth,
		Random rng,
		out SettlementLayout layout )
	{
		layout = null;
		if ( mode == SettlementLayoutMode.Scatter || !terrain.IsValid() || terrainConfig is null )
			return false;

		if ( plan.TargetBuildingCount < SettlementGridConstants.MiniGridMinBuildings )
			return false;

		var orientation = rng.NextSingle() * 360f;
		var compact = mode == SettlementLayoutMode.SmallTown
		              || plan.TargetBuildingCount < SettlementGridConstants.FullBlockMinBuildings;
		var spec = SettlementBlockGridBuilder.ResolveSpec( plan.TargetBuildingCount, compact );
		var (halfWidth, halfHeight) = SettlementBlockGridBuilder.MeasureExtents( spec );

		if ( !TryFindAnchor(
			     terrain,
			     terrainConfig,
			     plan,
			     settlementIndex,
			     worldSeed,
			     halfWidth,
			     halfHeight,
			     orientation,
			     out var anchor,
			     out var anchorRelief ) )
			return false;

		var roads = new HashSet<SettlementGridCell>();
		var roadTypes = new Dictionary<SettlementGridCell, SettlementRoadType>();
		var slots = new List<SettlementGridBuildingSlot>();
		SettlementBlockGridBuilder.Build( spec, roads, roadTypes, slots );
		SettlementMegastructurePlanner.Apply( spec, slots, plan.Identity, rng );
		SettlementBlockGridBuilder.ExtendMidLane(
			roads,
			roadTypes,
			SettlementGridConstants.InterCityExtensionCells );

		if ( slots.Count == 0 )
			return false;

		layout = new SettlementLayout
		{
			SettlementIndex = settlementIndex,
			Center = anchor,
			Identity = plan.Identity,
			Mode = mode,
			TargetBuildingCount = plan.TargetBuildingCount,
			BoundsRadius = Math.Max( halfWidth, halfHeight ) * SettlementGridConstants.CellInches + 400f
		};

		AppendRoadCellSegments( layout, roads, roadTypes, anchor, orientation, primaryPathWidth, secondaryPathWidth );
		AppendLots( layout, slots, anchor, orientation, plan.Identity );

		if ( layout.Lots.Count == 0 )
			return false;

		Log.Info(
			$"[Thorns Settlement] Block grid #{settlementIndex} {plan.Identity}: compact={compact}, slots={layout.Lots.Count}/{spec.TotalBuildingSlots}, "
			+ $"blocks={spec.BlockCols}x{spec.BlockRows}, lotsPerBlock={spec.BuildingsPerBlockCol}x{spec.BuildingsPerBlockRow}, relief={anchorRelief:0.0}." );

		return true;
	}

	static bool TryFindAnchor(
		Terrain terrain,
		ThornsTerrainConfig config,
		SettlementSitePlan plan,
		int settlementIndex,
		int worldSeed,
		int halfWidthCells,
		int halfHeightCells,
		float orientationYaw,
		out Vector3 anchor,
		out float relief )
	{
		anchor = plan.Center;
		relief = 0f;
		var rotation = Rotation.FromYaw( orientationYaw );

		if ( TryFinalizeAnchor( terrain, config, plan.Center, rotation, halfWidthCells, halfHeightCells, out anchor, out relief ) )
			return true;

		var probeRadius = Math.Max( halfWidthCells, halfHeightCells ) * SettlementGridConstants.CellInches + 200f;
		var bestScore = float.MaxValue;
		var bestAnchor = plan.Center;
		var found = false;
		var rng = new Random( HashCode.Combine( worldSeed, settlementIndex, 0xACC0A5 ) );

		for ( var attempt = 1; attempt <= 6; attempt++ )
		{
			var offset = new Vector3(
				rng.NextSingle() * 2f - 1f,
				rng.NextSingle() * 2f - 1f,
				0f ) * probeRadius * (attempt / 6f);

			var candidate = plan.Center + offset;
			if ( !TryScoreAnchorSite(
				     terrain,
				     config,
				     candidate,
				     rotation,
				     halfWidthCells,
				     halfHeightCells,
				     out var score,
				     out var sampleRelief ) )
				continue;

			if ( score >= bestScore )
				continue;

			bestScore = score;
			bestAnchor = candidate;
			relief = sampleRelief;
			found = true;
		}

		return found
		       && TryFinalizeAnchor(
			       terrain,
			       config,
			       bestAnchor,
			       rotation,
			       halfWidthCells,
			       halfHeightCells,
			       out anchor,
			       out relief );
	}

	static bool TryFinalizeAnchor(
		Terrain terrain,
		ThornsTerrainConfig config,
		Vector3 candidate,
		Rotation rotation,
		int halfWidthCells,
		int halfHeightCells,
		out Vector3 anchor,
		out float relief )
	{
		anchor = candidate;
		relief = 0f;

		if ( !TryScoreAnchorSite(
			     terrain,
			     config,
			     candidate,
			     rotation,
			     halfWidthCells,
			     halfHeightCells,
			     out _,
			     out relief ) )
			return false;

		if ( !ThornsProcBuildingTerrainUtil.TryResolveLowlandLotBase(
			     terrain,
			     config,
			     candidate,
			     rotation,
			     ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches,
			     out var baseZ,
			     out _,
			     out _ ) )
			return false;

		anchor = new Vector3( candidate.x, candidate.y, baseZ );
		return true;
	}

	static bool TryScoreAnchorSite(
		Terrain terrain,
		ThornsTerrainConfig config,
		Vector3 center,
		Rotation rotation,
		int halfWidthCells,
		int halfHeightCells,
		out float score,
		out float relief )
	{
		score = float.MaxValue;
		relief = 999f;

		if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, center, out var ground )
		     || !ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, config, ground.z ) )
			return false;

		var samples = 0;
		var minZ = float.MaxValue;
		var maxZ = float.MinValue;
		var seaRejects = 0;
		var cell = SettlementGridConstants.CellInches;
		const int stride = 3;

		for ( var gx = -halfWidthCells; gx <= halfWidthCells; gx += stride )
		{
			for ( var gy = -halfHeightCells; gy <= halfHeightCells; gy += stride )
			{
				var world = ground + rotation * new Vector3( gx * cell, gy * cell, 0f );
				if ( !ThornsTerrainSurface.TrySnapToTerrain( terrain, world, out var hit ) )
					continue;

				if ( !ThornsProcBuildingTerrainUtil.IsWithinLowlandElevation( terrain, config, hit.z ) )
				{
					seaRejects++;
					continue;
				}

				samples++;
				minZ = Math.Min( minZ, hit.z );
				maxZ = Math.Max( maxZ, hit.z );
			}
		}

		if ( samples < 6 )
			return false;

		relief = maxZ - minZ;
		if ( relief > ThornsProcBuildingTerrainUtil.TownCenterMaxReliefInches )
			return false;

		score = relief + seaRejects * 40f + (ground - center).Length * 0.02f;
		return true;
	}

	static void AppendRoadCellSegments(
		SettlementLayout layout,
		HashSet<SettlementGridCell> roads,
		Dictionary<SettlementGridCell, SettlementRoadType> roadTypes,
		Vector3 anchor,
		float orientationYaw,
		float mainLaneWidth,
		float sideRoadWidth )
	{
		var rotation = Rotation.FromYaw( orientationYaw );
		var cell = SettlementGridConstants.CellInches;
		var nudge = rotation * new Vector3( 1f, 0f, 0f );

		foreach ( var road in roads )
		{
			var type = roadTypes.GetValueOrDefault( road, SettlementRoadType.Secondary );
			var width = SettlementGridConstants.GridPathWidthInches;
			var center = GridToWorld( road, anchor, rotation, cell );
			layout.Roads.Add( new SettlementRoadSegment( center, center + nudge, width, type ) );
		}
	}

	static void AppendLots(
		SettlementLayout layout,
		List<SettlementGridBuildingSlot> slots,
		Vector3 anchor,
		float orientationYaw,
		ThornsPoiIdentity identity )
	{
		var rotation = Rotation.FromYaw( orientationYaw );
		var cell = SettlementGridConstants.CellInches;

		for ( var i = 0; i < slots.Count; i++ )
		{
			var slot = slots[i];
			if ( slot.IsConsumed )
				continue;

			var (widthCells, depthCells) = ThornsProcBuildingFootprintCatalog.GridCellsForLotSpans(
				slot.LotSpanWidth,
				slot.LotSpanDepth );
			var world = GridToWorld( slot.CenterX, slot.CenterY, anchor, rotation, cell );
			layout.Lots.Add( new SettlementFrontageLot
			{
				Position = world,
				Rotation = Rotation.FromYaw( orientationYaw + slot.YawDegrees ),
				RoadIndex = i,
				FrontageWidth = widthCells * SettlementGridConstants.CellInches,
				LotSpanWidth = slot.LotSpanWidth,
				LotSpanDepth = slot.LotSpanDepth,
				WidthCells = widthCells,
				DepthCells = depthCells,
				ForcedBuildingType = slot.ForcedBuildingType,
				Identity = identity,
				RoadType = slot.RoadType
			} );
		}
	}

	static Vector3 GridToWorld( SettlementGridCell cell, Vector3 anchor, Rotation rotation, float cellSize ) =>
		GridToWorld( cell.X, cell.Y, anchor, rotation, cellSize );

	static Vector3 GridToWorld( int gx, int gy, Vector3 anchor, Rotation rotation, float cellSize ) =>
		anchor + rotation * new Vector3( gx * cellSize, gy * cellSize, 0f );

	static Vector3 GridToWorld( float gx, float gy, Vector3 anchor, Rotation rotation, float cellSize ) =>
		anchor + rotation * new Vector3( gx * cellSize, gy * cellSize, 0f );
}
