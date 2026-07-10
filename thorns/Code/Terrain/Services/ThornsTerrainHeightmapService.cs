namespace Sandbox;

/// <summary>Heightmap bake cache binding and scatter height sampling for terrain.</summary>
public static class ThornsTerrainHeightmapService
{
	public static void AdoptWorldGenScatterHeightmap(
		ThornsTerrainOrchestrationState state,
		ThornsWorldGenerationContext context )
	{
		ReleaseWorldGenScatterHeightmap( state );
		var transferred = context.TransferHeightsOwnership();
		if ( transferred is null || context.HeightCells < 1 )
			return;

		ThornsTerrainRepairService.RepairMeshBreakingDiscontinuities(
			context.Spec,
			transferred.AsSpan( 0, context.HeightCells ) );

		state.WorldGenScatterHeights = transferred;
		ThornsHeightmapBakeCache.Register( context.Spec, transferred, context.HeightRx, context.HeightRz, context.HeightCells );
	}

	public static void ReleaseWorldGenScatterHeightmap( ThornsTerrainOrchestrationState state )
	{
		if ( state.WorldGenScatterHeights is null )
			return;

		ThornsHeightmapBakeCache.Clear();
		state.WorldGenScatterHeights = null;
	}

	public static void RentScatterHeightmapOrFill( in ThornsTerrainNetSpec spec, out float[] heights, out int cells ) =>
		ThornsHeightmapBakeCache.RentFilled( in spec, out heights, out cells );
}
