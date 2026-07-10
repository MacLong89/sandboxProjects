using System.Linq;
using System.Text;

namespace Sandbox;

/// <summary>End-of-pass world generation quality summary for tuning and seed review.</summary>
public static class ThornsWorldGenerationQaReport
{
	public static void PublishSummary( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host = null )
	{
		if ( context is null )
			return;

		var plan = context.Plan;
		var spec = context.Spec;
		var targetTotal = host is { OrganicClusterPlacement: true }
			? host.OrganicBuildingCount
			: ThornsWorldSettlementPlan.TotalBuildingCount;
		var totalPlaced = context.SpawnedBuildingCount;
		var townA = context.TownPlacedPerTown is { Length: > 0 } ? context.TownPlacedPerTown[0] : 0;
		var townB = context.TownPlacedPerTown is { Length: > 1 } ? context.TownPlacedPerTown[1] : 0;
		var townC = context.TownPlacedPerTown is { Length: > 2 } ? context.TownPlacedPerTown[2] : 0;

		var roadCorridors = spec?.RoadCorridors;
		var roadCount = roadCorridors?.Count ?? 0;
		var roadLength = ComputeTotalRoadLength( roadCorridors );

		CountBlockPlanStats(
			context.BlockPlan,
			out var cityBlocks,
			out var townBlocks,
			out var vacantLots,
			out var assignedLots,
			out var roadFacingLots );

		var dominant = ThornsWorldGenerationQaMetrics.DominantFailureReason();
		var mainFailure = dominant?.ToString() ?? ( totalPlaced >= targetTotal ? "None" : "IncompletePlacement" );

		var sb = new StringBuilder();
		sb.AppendLine( "[Thorns WorldGen QA]" );
		if ( host is { OrganicClusterPlacement: true } )
			sb.AppendLine( $"Placement=OrganicCluster bias={host.OrganicClusterBias:F2} bufferCells={host.OrganicBuildingBufferCells:F1}" );
		sb.AppendLine( $"Seed={context.WorldSeed}" );
		if ( plan?.MainCity is not null )
		{
			var cc = plan.MainCity.CenterLocal;
			sb.AppendLine( $"MainCityCenter=({cc.x:F0},{cc.y:F0}) Radius={plan.MainCity.Radius:F0}" );
			for ( var t = 0; t < plan.Towns.Count; t++ )
			{
				var tc = plan.Towns[t].CenterLocal;
				sb.AppendLine( $"Town{t}Center=({tc.x:F0},{tc.y:F0}) Radius={plan.Towns[t].Radius:F0}" );
			}
		}
		sb.AppendLine( $"TotalBuildings={totalPlaced}/{targetTotal}" );
		sb.AppendLine( $"City={context.CityPlacedCount}/{ThornsWorldSettlementPlan.MainCityBuildingCount}" );
		sb.AppendLine( $"TownA={townA}/{ThornsWorldSettlementPlan.BuildingsPerTown}" );
		sb.AppendLine( $"TownB={townB}/{ThornsWorldSettlementPlan.BuildingsPerTown}" );
		sb.AppendLine( $"TownC={townC}/{ThornsWorldSettlementPlan.BuildingsPerTown}" );
		sb.AppendLine( $"Isolated={context.IsolatedPlacedCount}/{ThornsWorldSettlementPlan.IsolatedSiteCount}" );
		sb.AppendLine( $"CityPresent={( context.CityPlacedCount > 0 ? "yes" : "no" )} TownsPlaced={townA + townB + townC} TownTargets={plan?.Towns?.Count ?? 3}" );
		sb.AppendLine( $"BuildingTypes={ThornsWorldGenerationQaMetrics.FormatBuildingTypeCounts()}" );
		sb.AppendLine(
			$"BlueprintValid={ThornsProcBuildingSettlementDiagnostics.BlueprintStrictValidationPassed} "
			+ $"BlueprintInvalid={ThornsProcBuildingSettlementDiagnostics.BlueprintStrictValidationFailed} "
			+ $"CompileOk={ThornsProcBuildingSettlementDiagnostics.BlueprintCompileSuccess} "
			+ $"CompileFail={ThornsProcBuildingSettlementDiagnostics.BlueprintCompileFailed}" );
		sb.AppendLine(
			$"FallbackUsed={ThornsProcBuildingSettlementDiagnostics.FallbackUsed} "
			+ $"FallbackValid={ThornsProcBuildingSettlementDiagnostics.FallbackStrictValidationPassed} "
			+ $"FallbackInvalid={ThornsProcBuildingSettlementDiagnostics.FallbackStrictValidationFailed} "
			+ $"BuildingsOnFallback={ThornsWorldGenerationQaMetrics.BuildingsPlacedWithFallbackLayout}" );
		sb.AppendLine(
			$"PlacementRejected={ThornsProcBuildingSettlementDiagnostics.PlacementRejected} "
			+ $"SlotsSkipped={CountSkippedSlots()}" );
		sb.AppendLine(
			$"OverlapFailures={ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.Overlap )} "
			+ $"TerrainFailures={SumTerrainRelatedFailures()} "
			+ $"BlueprintFailures={ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.BlueprintInvalid ) + ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.FallbackInvalid )} "
			+ $"NoValidYaw={ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.NoValidYaw )}" );
		sb.AppendLine(
			$"TerrainValidationPass={ThornsWorldSettlementTerrainDiagnostics.TerrainValidationPassed} "
			+ $"TerrainValidationReject={ThornsWorldSettlementTerrainDiagnostics.TerrainValidationRejected} "
			+ $"TerrainRejectReasons={ThornsWorldGenerationQaMetrics.FormatTerrainRejectReasons()}" );
		sb.AppendLine(
			$"WorstSlope={ThornsWorldGenerationQaMetrics.WorstMaxSlope:F1} "
			+ $"AvgPlacedSlope={ThornsWorldGenerationQaMetrics.AveragePlacedMaxSlope:F1} "
			+ $"WorstCornerDelta={ThornsWorldGenerationQaMetrics.WorstCornerDelta:F1} "
			+ $"WorstCliff={ThornsWorldGenerationQaMetrics.WorstCliffSeverity:F1}" );
		sb.AppendLine(
			$"RoadCorridors={roadCount} RoadLength={roadLength:F0} "
			+ $"FoliageRoadSkips={ThornsWorldGenerationQaMetrics.FoliageScatterRoadSkips} "
			+ $"BoulderRoadSkips={ThornsWorldGenerationQaMetrics.BoulderScatterRoadSkips}" );
		sb.AppendLine(
			$"CityBlocks={cityBlocks} TownBlocks={townBlocks} VacantLots={vacantLots} "
			+ $"AssignedLots={assignedLots} RoadFacingLots={roadFacingLots}" );
		sb.AppendLine(
			$"LocalFeatherPads={ThornsWorldSettlementTerrainDiagnostics.LocalFeatherPadsAdded} "
			+ $"PadsSkippedSteep={ThornsWorldSettlementTerrainDiagnostics.TerrainPadsSkippedSteep} "
			+ $"MacroZones={ThornsWorldSettlementTerrainShaping.LastMacroZones.Count}" );
		sb.AppendLine( $"SpawnedPieces={context.SpawnedPieceCount} PlacementFailureAttempts={ThornsWorldGenerationQaMetrics.TotalPlacementFailureAttempts()}" );
		sb.AppendLine( $"MainFailureReason={mainFailure}" );

		Log.Info( sb.ToString() );
	}

	static int CountSkippedSlots()
	{
		var skipped = 0;
		skipped += Math.Max( 0, ThornsWorldSettlementPlacementDiagnostics.City.Attempted - ThornsWorldSettlementPlacementDiagnostics.City.Placed );
		for ( var t = 0; t < 3; t++ )
		{
			var z = ThornsWorldSettlementPlacementDiagnostics.Town( t );
			skipped += Math.Max( 0, z.Attempted - z.Placed );
		}

		var iso = ThornsWorldSettlementPlacementDiagnostics.Isolated;
		skipped += Math.Max( 0, iso.Attempted - iso.Placed );
		return skipped;
	}

	static int SumTerrainRelatedFailures()
	{
		return ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.TerrainSlope )
		       + ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.TerrainCornerDelta )
		       + ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.TerrainVariance )
		       + ThornsWorldGenerationQaMetrics.SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason.CliffSeverity )
		       + _terrainRejectSum();
	}

	static int _terrainRejectSum()
	{
		var sum = 0;
		foreach ( var kv in ThornsWorldGenerationQaMetrics.TerrainRejectByReason )
			sum += kv.Value;
		return sum;
	}

	static float ComputeTotalRoadLength( IReadOnlyList<ThornsWorldRoadCorridor> corridors )
	{
		if ( corridors is null )
			return 0f;

		var total = 0f;
		for ( var i = 0; i < corridors.Count; i++ )
		{
			var c = corridors[i];
			total += ( c.B - c.A ).Length;
		}

		return total;
	}

	static void CountBlockPlanStats(
		ThornsWorldSettlementBlockPlan blockPlan,
		out int cityBlocks,
		out int townBlocks,
		out int vacantLots,
		out int assignedLots,
		out int roadFacingLots )
	{
		cityBlocks = 0;
		townBlocks = 0;
		vacantLots = 0;
		assignedLots = 0;
		roadFacingLots = 0;

		if ( blockPlan is not { IsPopulated: true } || blockPlan.Areas is null )
			return;

		foreach ( var area in blockPlan.Areas )
		{
			if ( area?.Districts is null )
				continue;

			foreach ( var district in area.Districts )
			{
				if ( district.Blocks is null )
					continue;

				if ( area.SettlementKind == ThornsWorldSettlementKind.MainCity )
					cityBlocks += district.Blocks.Count;
				else if ( area.SettlementKind == ThornsWorldSettlementKind.Town )
					townBlocks += district.Blocks.Count;
			}

			if ( area.Lots is null )
				continue;

			foreach ( var lot in area.Lots )
			{
				if ( lot.State == ThornsWorldSettlementLotState.Vacant )
					vacantLots++;
				if ( lot.State == ThornsWorldSettlementLotState.Assigned || lot.State == ThornsWorldSettlementLotState.Placed )
					assignedLots++;
				if ( lot.FrontageDirection.LengthSquared > 0.01f )
					roadFacingLots++;
			}
		}
	}
}
