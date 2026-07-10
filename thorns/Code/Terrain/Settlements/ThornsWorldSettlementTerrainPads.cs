namespace Sandbox;

/// <summary>Entry point for settlement terrain — delegates macro shaping to <see cref="ThornsWorldSettlementTerrainShaping"/>.</summary>
public static class ThornsWorldSettlementTerrainPads
{
	/// <summary>Macro settlement shaping on the working heightmap; pads on spec for client mesh replication.</summary>
	public static void FlattenSettlementZones(
		ThornsWorldSettlementPlan plan,
		ThornsTerrainNetSpec spec,
		Span<float> heights,
		float worldWidth,
		float worldDepth,
		bool collectTerrainDebug = false ) =>
		ThornsWorldSettlementTerrainShaping.ApplyMacroSettlementShaping(
			plan,
			spec,
			heights,
			worldWidth,
			worldDepth,
			collectTerrainDebug: collectTerrainDebug );
}
