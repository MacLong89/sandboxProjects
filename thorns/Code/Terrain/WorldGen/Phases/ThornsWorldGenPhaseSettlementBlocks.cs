using System.Linq;

namespace Sandbox;

/// <summary>Phase 5 — districts, blocks, lots, and intra-settlement road corridors.</summary>
public sealed class ThornsWorldGenPhaseSettlementBlocks : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateSettlementBlocks;
	public string Name => "GenerateSettlementBlocks";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.SkipLegacySettlementLayout )
		{
			context.BlockPlan = ThornsWorldSettlementBlockPlan.Empty;
			Log.Info( "[Thorns WorldGen] Skipping settlement blocks (organic / terrain-first placement)." );
			return;
		}

		context.BlockPlan = ThornsWorldSettlementBlockGenerator.Generate(
			context.Plan,
			context.RoadNetwork,
			context.WorldSeed );

		ThornsWorldSettlementBlockTerrain.ComputeAndRegister(
			context.BlockPlan,
			context.Spec,
			context.HeightsSpan,
			context.HeightRx,
			context.HeightRz,
			context.WorldWidth,
			context.WorldDepth,
			context.Spec.CenterOnWorldOrigin,
			collectDebug: host.DrawSettlementLayoutDebug );

		ThornsSettlementTerrainInfluence.ApplyToHeightmap(
			context.Spec,
			context.Heights.AsSpan( 0, context.HeightCells ),
			reconcile: true );

		ThornsWorldSettlementBlockTerrain.ApplySurfacesToHeightmap(
			context.Spec,
			context.Heights.AsSpan( 0, context.HeightCells ) );

		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldSettlementBlockDebugViz.Draw( host.Scene, host.ChunkRoot, context.BlockPlan );
			ThornsWorldSettlementBlockTerrainDebugViz.Draw(
				host.Scene,
				host.ChunkRoot,
				context.Spec,
				context.HeightsSpan,
				context.WorldWidth,
				context.WorldDepth );
			ThornsWorldSettlementBlockGroundingDebugViz.Draw(
				host.Scene,
				host.ChunkRoot,
				context.Spec,
				context.HeightsSpan,
				context.WorldWidth,
				context.WorldDepth );
			ThornsWorldSettlementInteriorReadabilityDebugViz.Draw(
				host.Scene,
				host.ChunkRoot,
				context.Spec,
				context.HeightsSpan,
				context.WorldWidth,
				context.WorldDepth );
		}

		var city = context.BlockPlan.MainCity;
		var assigned = city?.Lots.Count( l => l.State == ThornsWorldSettlementLotState.Assigned ) ?? 0;
		var vacant = city?.Lots.Count( l => l.State == ThornsWorldSettlementLotState.Vacant ) ?? 0;
		Log.Info(
			$"[Thorns Blocks] City lots: assigned={assigned} vacant={vacant} corridors={city?.Corridors.Count ?? 0} districts={city?.Districts.Count ?? 0}" );
	}
}
