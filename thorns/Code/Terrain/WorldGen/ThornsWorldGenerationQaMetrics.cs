using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sandbox;

/// <summary>Aggregated world-gen QA counters (reset at start of each scatter pass).</summary>
public static class ThornsWorldGenerationQaMetrics
{
	static readonly Dictionary<ThornsProcBuildingType, int> _placedByType = new();
	static readonly Dictionary<ThornsWorldSettlementPlacementFailureReason, int> _terrainRejectByReason = new();

	public static int BuildingsPlacedWithFallbackLayout { get; private set; }
	public static int FoliageScatterRoadSkips { get; private set; }
	public static int BoulderScatterRoadSkips { get; private set; }

	public static float WorstCornerDelta { get; private set; }
	public static float WorstCliffSeverity { get; private set; }
	public static float WorstMaxSlope { get; private set; }
	public static float SumPlacedMaxSlope { get; private set; }
	public static int PlacedFootprintTerrainSamples { get; private set; }

	public static void Reset()
	{
		_placedByType.Clear();
		_terrainRejectByReason.Clear();
		BuildingsPlacedWithFallbackLayout = 0;
		FoliageScatterRoadSkips = 0;
		BoulderScatterRoadSkips = 0;
		WorstCornerDelta = 0f;
		WorstCliffSeverity = 0f;
		WorstMaxSlope = 0f;
		SumPlacedMaxSlope = 0f;
		PlacedFootprintTerrainSamples = 0;
	}

	public static void RecordBuildingPlaced( ThornsProcBuildingType type, bool usedFallbackLayout )
	{
		if ( !_placedByType.TryGetValue( type, out var n ) )
			n = 0;
		_placedByType[type] = n + 1;

		if ( usedFallbackLayout )
			BuildingsPlacedWithFallbackLayout++;
	}

	public static void RecordTerrainReject( ThornsWorldSettlementPlacementFailureReason reason )
	{
		if ( !_terrainRejectByReason.TryGetValue( reason, out var n ) )
			n = 0;
		_terrainRejectByReason[reason] = n + 1;
	}

	public static void RecordPlacedFootprintTerrain( ThornsWorldSettlementTerrainValidation.FootprintTerrainMetrics metrics )
	{
		PlacedFootprintTerrainSamples++;
		SumPlacedMaxSlope += metrics.MaxLocalSlope;
		WorstCornerDelta = MathF.Max( WorstCornerDelta, metrics.CornerDelta );
		WorstCliffSeverity = MathF.Max( WorstCliffSeverity, metrics.CliffSeverity );
		WorstMaxSlope = MathF.Max( WorstMaxSlope, metrics.MaxLocalSlope );
	}

	public static void RecordFoliageScatterRoadSkip() => FoliageScatterRoadSkips++;
	public static void RecordBoulderScatterRoadSkip() => BoulderScatterRoadSkips++;

	public static IReadOnlyDictionary<ThornsProcBuildingType, int> PlacedByType => _placedByType;

	public static IReadOnlyDictionary<ThornsWorldSettlementPlacementFailureReason, int> TerrainRejectByReason =>
		_terrainRejectByReason;

	public static float AveragePlacedMaxSlope =>
		PlacedFootprintTerrainSamples > 0 ? SumPlacedMaxSlope / PlacedFootprintTerrainSamples : 0f;

	public static int SumPlacementFailuresByReason( ThornsWorldSettlementPlacementFailureReason reason )
	{
		var sum = ThornsWorldSettlementPlacementDiagnostics.City.FailuresByReason.TryGetValue( reason, out var c ) ? c : 0;
		for ( var t = 0; t < 3; t++ )
		{
			if ( ThornsWorldSettlementPlacementDiagnostics.Town( t ).FailuresByReason.TryGetValue( reason, out var tc ) )
				sum += tc;
		}

		if ( ThornsWorldSettlementPlacementDiagnostics.Isolated.FailuresByReason.TryGetValue( reason, out var iso ) )
			sum += iso;

		return sum;
	}

	public static int TotalPlacementFailureAttempts()
	{
		var sum = 0;
		foreach ( var kv in ThornsWorldSettlementPlacementDiagnostics.City.FailuresByReason )
			sum += kv.Value;
		for ( var t = 0; t < 3; t++ )
		{
			foreach ( var kv in ThornsWorldSettlementPlacementDiagnostics.Town( t ).FailuresByReason )
				sum += kv.Value;
		}

		foreach ( var kv in ThornsWorldSettlementPlacementDiagnostics.Isolated.FailuresByReason )
			sum += kv.Value;

		return sum;
	}

	public static ThornsWorldSettlementPlacementFailureReason? DominantFailureReason()
	{
		var totals = new Dictionary<ThornsWorldSettlementPlacementFailureReason, int>();
		void AddFrom( Dictionary<ThornsWorldSettlementPlacementFailureReason, int> src )
		{
			foreach ( var kv in src )
			{
				if ( !totals.TryGetValue( kv.Key, out var n ) )
					n = 0;
				totals[kv.Key] = n + kv.Value;
			}
		}

		AddFrom( ThornsWorldSettlementPlacementDiagnostics.City.FailuresByReason );
		for ( var t = 0; t < 3; t++ )
			AddFrom( ThornsWorldSettlementPlacementDiagnostics.Town( t ).FailuresByReason );
		AddFrom( ThornsWorldSettlementPlacementDiagnostics.Isolated.FailuresByReason );
		foreach ( var kv in _terrainRejectByReason )
		{
			if ( !totals.TryGetValue( kv.Key, out var n ) )
				n = 0;
			totals[kv.Key] = n + kv.Value;
		}

		if ( totals.Count == 0 )
			return null;

		return totals.OrderByDescending( kv => kv.Value ).First().Key;
	}

	public static string FormatBuildingTypeCounts()
	{
		if ( _placedByType.Count == 0 )
			return "none";

		return string.Join( ", ", _placedByType
			.OrderByDescending( kv => kv.Value )
			.Select( kv => $"{kv.Key}={kv.Value}" ) );
	}

	public static string FormatTerrainRejectReasons()
	{
		if ( _terrainRejectByReason.Count == 0 )
			return "none";

		return string.Join( ", ", _terrainRejectByReason
			.OrderByDescending( kv => kv.Value )
			.Select( kv => $"{kv.Key}={kv.Value}" ) );
	}
}
