namespace Sandbox;

/// <summary>Phase 3 — settlement zone plateaus and heightfield blending under hubs.</summary>
public sealed class ThornsWorldGenPhaseSettlementTerrain : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateSettlementTerrain;
	public string Name => "GenerateSettlementTerrain";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.SkipLegacySettlementLayout )
		{
			Log.Info( "[Thorns WorldGen] Skipping macro settlement terrain (organic / terrain-first placement)." );
			return;
		}

		ThornsWorldSettlementTerrainPads.FlattenSettlementZones(
			context.Plan,
			context.Spec,
			context.Heights.AsSpan( 0, context.HeightCells ),
			context.WorldWidth,
			context.WorldDepth,
			collectTerrainDebug: host.DrawSettlementLayoutDebug );

		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldSettlementTerrainDebugViz.DrawMacroInfluence( host.Scene, host.ChunkRoot );
			ThornsWorldSettlementTerrainDebugViz.DrawNoiseAttenuationHeatmap(
				host.Scene,
				host.ChunkRoot,
				context.Spec );
			ThornsWorldSettlementTerrainDebugViz.DrawReconciliationOverlays(
				host.Scene,
				host.ChunkRoot,
				context.Spec,
				context.HeightsSpan,
				context.WorldWidth,
				context.WorldDepth );
			ThornsWorldSettlementTerrainDebugViz.DrawSlopeHeatmap(
				host.Scene,
				host.ChunkRoot,
				context.HeightsSpan,
				context.Spec,
				context.WorldWidth,
				context.WorldDepth );
		}
	}
}
