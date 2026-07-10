using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sandbox;

/// <summary>Per-zone placement counters and failure reasons (reset each world-gen scatter pass).</summary>
public static class ThornsWorldSettlementPlacementDiagnostics
{
	public sealed class ZoneStats
	{
		public string Label { get; init; }
		public int Attempted { get; set; }
		public int Placed { get; set; }
		public readonly Dictionary<ThornsWorldSettlementPlacementFailureReason, int> FailuresByReason = new();

		public void RecordFailure( ThornsWorldSettlementPlacementFailureReason reason )
		{
			if ( !FailuresByReason.TryGetValue( reason, out var n ) )
				n = 0;
			FailuresByReason[reason] = n + 1;
		}
	}

	static ZoneStats _city;
	static readonly ZoneStats[] _towns = new ZoneStats[3];
	static ZoneStats _isolated;

	public static ZoneStats City => _city ??= new ZoneStats { Label = "Main City" };

	public static ZoneStats Town( int townIndex ) =>
		_towns[townIndex] ??= new ZoneStats { Label = ThornsWorldSettlementComposition.TownLabel( townIndex ) };

	public static ZoneStats Isolated => _isolated ??= new ZoneStats { Label = "Isolated" };

	public static void Reset()
	{
		_city = new ZoneStats { Label = "Main City" };
		for ( var i = 0; i < _towns.Length; i++ )
			_towns[i] = new ZoneStats { Label = ThornsWorldSettlementComposition.TownLabel( i ) };
		_isolated = new ZoneStats { Label = "Isolated" };
	}

	public static void LogSummaryDetailed(
		int cityPlaced,
		IReadOnlyList<int> townPlacedPerIndex,
		int isolatedPlaced )
	{
		Log.Info( FormatZone( City, ThornsWorldSettlementPlan.MainCityBuildingCount, cityPlaced ) );
		for ( var i = 0; i < 3; i++ )
		{
			var placed = townPlacedPerIndex is not null && i < townPlacedPerIndex.Count
				? townPlacedPerIndex[i]
				: 0;
			Log.Info( FormatZone( Town( i ), ThornsWorldSettlementPlan.BuildingsPerTown, placed ) );
		}

		Log.Info( FormatZone( Isolated, ThornsWorldSettlementPlan.IsolatedSiteCount, isolatedPlaced ) );
	}

	static string FormatZone( ZoneStats stats, int targetCount, int placed )
	{
		var sb = new StringBuilder();
		sb.Append( $"[Thorns Placement] {stats.Label}: placed {placed}/{targetCount}" );
		sb.Append( $" attempted={stats.Attempted} skipped={Math.Max( 0, stats.Attempted - placed )}" );
		if ( stats.FailuresByReason.Count > 0 )
		{
			sb.Append( " failures={" );
			sb.Append( string.Join( ", ", stats.FailuresByReason
				.OrderByDescending( kv => kv.Value )
				.Select( kv => $"{kv.Key}={kv.Value}" ) ) );
			sb.Append( '}' );
		}

		return sb.ToString();
	}
}
