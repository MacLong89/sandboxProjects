using Dynasty.Core.Enums;
using Dynasty.Systems.Formation;

namespace Dynasty.Data;

public static class DepthChartPositionRules
{
	public static IReadOnlyList<Position> GetEligiblePositions( string slotKey )
		=> FormationLayoutRegistry.GetEligiblePositions( slotKey );

	public static bool IsEligible( string slotKey, Position position )
	{
		var eligible = GetEligiblePositions( slotKey );
		return eligible.Count > 0 && eligible.Contains( position );
	}
}
