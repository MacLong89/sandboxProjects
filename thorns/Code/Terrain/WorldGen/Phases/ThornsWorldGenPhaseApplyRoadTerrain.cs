namespace Sandbox;

/// <summary>Phase 11 — bake road corridors into spec + working heightmap (terrain + turf on mesh build).</summary>
public sealed class ThornsWorldGenPhaseApplyRoadTerrain : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.ApplyRoadTerrain;
	public string Name => "ApplyRoadTerrain";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.OrganicClusterPlacement )
		{
			context.Spec.RoadCorridors = new List<ThornsWorldRoadCorridor>();
			Log.Info( "[Thorns WorldGen] Skipping road terrain (organic cluster placement)." );
			return;
		}

		host.Terrain.CopyRoadTuningToSpec( context.Spec );

		var corridors = ThornsWorldRoadTerrain.CollectAllCorridors( context.BlockPlan, context.RoadNetwork );
		context.Spec.RoadCorridors = corridors;

		if ( context.Heights is not null )
		{
			if ( corridors.Count > 0 )
			{
				ThornsWorldRoadTerrain.ApplyRoadInfluenceToHeightmap(
					context.Spec,
					context.Heights.AsSpan( 0, context.HeightCells ) );
			}

			var span = context.Heights.AsSpan( 0, context.HeightCells );
			ThornsSettlementTerrainReconciliation.SoftenRoadExitBanks( context.Spec, span );
			if ( !host.SkipLegacySettlementLayout )
			{
				ThornsSettlementTerrainInfluence.ApplyToHeightmap(
					context.Spec,
					span,
					reconcile: true,
					collectDirectionalDebug: host.DrawSettlementLayoutDebug );
			}
		}

		Log.Info( $"[Thorns Roads] corridors={corridors.Count} tuning cityFlat={context.Spec.RoadTuning?.CityFlattenStrength:F2}" );

		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldRoadTerrainDebugViz.Draw( host.Scene, host.ChunkRoot, context.Spec );
		}
	}
}
