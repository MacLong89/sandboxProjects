using System.Collections.Generic;

namespace Sandbox;

/// <summary>Delegates to <see cref="ThornsSettlementTerrainInfluence"/> multi-ring system.</summary>
public static class ThornsWorldSettlementTerrainShaping
{
	public static IReadOnlyList<ThornsWorldSettlementInfluenceZone> LastMacroZones =>
		ThornsSettlementTerrainInfluence.LastZones;

	public static void ApplyMacroSettlementShaping(
		ThornsWorldSettlementPlan plan,
		ThornsTerrainNetSpec spec,
		Span<float> heights,
		float worldWidth,
		float worldDepth,
		List<ThornsWorldSettlementInfluenceZone> zonesOut = null,
		bool collectTerrainDebug = false )
	{
		if ( plan is null || spec is null )
			return;

		ThornsSettlementTerrainInfluence.RegisterFromPlan(
			plan,
			spec,
			heights,
			worldWidth,
			worldDepth );

		ThornsSettlementTerrainInfluence.ApplyToHeightmap(
			spec,
			heights,
			reconcile: true,
			collectDirectionalDebug: collectTerrainDebug );

		if ( zonesOut is not null )
		{
			zonesOut.Clear();
			zonesOut.AddRange( ThornsSettlementTerrainInfluence.LastZones );
		}
	}
}
