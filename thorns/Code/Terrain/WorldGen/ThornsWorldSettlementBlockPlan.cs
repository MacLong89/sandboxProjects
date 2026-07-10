using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>District → block → lot organization for settlement placement (phase 5).</summary>
public sealed class ThornsWorldSettlementBlockPlan
{
	public static ThornsWorldSettlementBlockPlan Empty { get; } = new() { IsPopulated = false };

	public bool IsPopulated { get; init; }
	public int Seed { get; init; }
	public IReadOnlyList<ThornsWorldSettlementAreaBlockPlan> Areas { get; init; } = [];
	public IReadOnlyList<ThornsWorldRoadCorridor> InterSettlementCorridors { get; init; } = [];

	public ThornsWorldSettlementAreaBlockPlan MainCity =>
		Areas?.FirstOrDefault( a => a.SettlementKind == ThornsWorldSettlementKind.MainCity );

	public ThornsWorldSettlementAreaBlockPlan Town( int index ) =>
		Areas?.FirstOrDefault( a => a.SettlementKind == ThornsWorldSettlementKind.Town && a.TownIndex == index );

	/// <summary>All lots with building assignments, sorted for placement priority.</summary>
	public IEnumerable<ThornsWorldSettlementLot> AssignedLotsOrdered( ThornsWorldSettlementAreaBlockPlan area )
	{
		if ( area?.Lots is null )
			yield break;

		var list = area.Lots
			.Where( l => l.State == ThornsWorldSettlementLotState.Assigned && l.AssignedType.HasValue )
			.ToList();
		list.Sort( ( a, b ) =>
		{
			var ringA = (int)( a.CityRing ?? ThornsWorldCityRing.MidRing );
			var ringB = (int)( b.CityRing ?? ThornsWorldCityRing.MidRing );
			if ( ringA != ringB )
				return ringA.CompareTo( ringB );

			var ra = ThornsWorldSettlementPlacementPriority.GetRank( a.AssignedType!.Value );
			var rb = ThornsWorldSettlementPlacementPriority.GetRank( b.AssignedType!.Value );
			return rb.CompareTo( ra );
		} );
		foreach ( var lot in list )
			yield return lot;
	}
}
