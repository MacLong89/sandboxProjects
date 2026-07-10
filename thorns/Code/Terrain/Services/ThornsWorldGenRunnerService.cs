namespace Sandbox;

/// <summary>Pre-chunk world generation, deferred session bridge, and post-chunk pipeline kickoff.</summary>
public static class ThornsWorldGenRunnerService
{
	public static ThornsWorldGenerationHostBridge CreateHostBridge(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state ) =>
		new ThornsWorldGenerationHostBridge(
			terrain,
			state.ChunkRoot,
			state.SiteFootprintsChunkLocal,
			state.ProcBuildingFootprintsChunk,
			state.ProcBuildingsForLoot );

	public static bool BeginPreChunkWorldGen(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		ThornsWorldScatterService.HostScatterProceduralSitesAndRingCrates( terrain, state, spec );
		return state.AwaitingPreChunkWorldGen;
	}

	public static void TickDeferredPreChunkWorldGen(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state )
	{
		if ( !state.AwaitingPreChunkWorldGen || state.DeferredPreChunkWorldGen is null )
			return;

		if ( !state.DeferredPreChunkWorldGen.TickOnePhase() )
			return;

		state.DeferredPreChunkWorldGen.FinalizeHost();
		state.DeferredPreChunkWorldGen.Dispose();
		state.DeferredPreChunkWorldGen = null;
		state.AwaitingPreChunkWorldGen = false;
		ThornsTerrainChunkLifecycleService.CompleteChunkNetworkSpawn( terrain, state, state.PendingChunkSpec );
		state.PendingChunkSpec = null;
	}

	public static void RunPostChunkPhases( ThornsTerrainSystem terrain, ThornsTerrainOrchestrationState state, ThornsTerrainNetSpec spec )
	{
		var bridge = CreateHostBridge( terrain, state );
		new ThornsWorldGenerationPipeline( bridge ).RunPostChunkPhases( spec );
	}

	public static void FinalizeTerrainSpecWithoutBuildings(
		ThornsTerrainSystem terrain,
		ThornsTerrainOrchestrationState state,
		ThornsTerrainNetSpec spec )
	{
		spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();
		ThornsTerrainDecorScatter.CopyHostDecorTuning( spec, terrain );
		ThornsTerrainChunkLifecycleService.PushSpecToChunk( state, spec );
	}
}
