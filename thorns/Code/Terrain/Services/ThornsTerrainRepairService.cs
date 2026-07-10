namespace Sandbox;

/// <summary>Terrain height repair hooks — mesh discontinuity fixes after world-gen.</summary>
public static class ThornsTerrainRepairService
{
	public static void RepairMeshBreakingDiscontinuities( in ThornsTerrainNetSpec spec, Span<float> heights ) =>
		ThornsTerrainHeightRepair.RepairMeshBreakingDiscontinuities( spec, heights );
}
