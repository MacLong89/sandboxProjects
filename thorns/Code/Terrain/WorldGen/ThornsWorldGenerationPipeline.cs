namespace Sandbox;

using System.Diagnostics;

/// <summary>
/// Orchestrates Thorns world generation in explicit phases for debuggability and future streaming.
/// Phases 1–8 run before the terrain chunk is networked; 9–10 run after chunk spawn.
/// </summary>
public sealed class ThornsWorldGenerationPipeline
{
	static readonly IThornsWorldGenerationPhase[] PreChunkPhases =
	[
		new ThornsWorldGenPhaseMacroTerrain(),
		new ThornsWorldGenPhaseSelectSettlementLocations(),
		new ThornsWorldGenPhaseSettlementTerrain(),
		new ThornsWorldGenPhaseRoadNetwork(),
		new ThornsWorldGenPhaseSettlementBlocks(),
		new ThornsWorldGenPhaseApplyRoadTerrain(),
		new ThornsWorldGenPhaseReserveBuildingFootprints(),
		new ThornsWorldGenPhaseGenerateBuildingLayouts(),
		new ThornsWorldGenPhaseSpawnBuildings()
	];

	// Order matches legacy host boot: resources/boulders before interior loot.
	static readonly IThornsWorldGenerationPhase[] PostChunkPhases =
	[
		new ThornsWorldGenPhaseEnvironmentDetails(),
		new ThornsWorldGenPhaseLootAndProps()
	];

	readonly ThornsWorldGenerationHostBridge _host;

	public ThornsWorldGenerationPipeline( ThornsWorldGenerationHostBridge host ) => _host = host;

	public void RunPreChunkSettlementPipeline( ThornsTerrainNetSpec spec )
	{
		if ( !_host.ChunkRoot.IsValid() )
			return;

		using var context = ThornsWorldGenerationContext.Create( spec, _host.ScatterEdgeInsetFraction );

		foreach ( var phase in PreChunkPhases )
			RunPhase( phase, context );

		ThornsWorldGenerationQaReport.PublishSummary( context, _host );

		_host.FinalizePreChunkGeneration( context );
	}

	public void RunPostChunkPhases( ThornsTerrainNetSpec spec )
	{
		using var context = ThornsWorldGenerationContext.CreatePostChunk( spec );
		context.Plan = ThornsWorldSettlementPlanner.LastPlan;

		foreach ( var phase in PostChunkPhases )
			RunPhase( phase, context );
	}

	void RunPhase( IThornsWorldGenerationPhase phase, ThornsWorldGenerationContext context )
	{
		Log.Info( $"[Thorns WorldGen] Phase {(int)phase.Id}: {phase.Name} …" );
		var sw = Stopwatch.StartNew();
		phase.Execute( context, _host );
		sw.Stop();
		var ms = sw.Elapsed.TotalMilliseconds;
		ThornsPerfDebug.RecordWorldGenPhase( phase.Name, ms );
		if (ThornsPerfDebug.Enabled)
			Log.Info( $"[Thorns WorldGen] Phase {(int)phase.Id}: {phase.Name} done in {ms:F1}ms" );
	}
}
