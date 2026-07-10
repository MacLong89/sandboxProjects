namespace Sandbox;

/// <summary>Phase 2 — city, towns, isolated sites, and trail graph using terrain-aware scoring.</summary>
public sealed class ThornsWorldGenPhaseSelectSettlementLocations : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.SelectSettlementLocations;
	public string Name => "SelectSettlementLocations";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.OrganicClusterPlacement )
		{
			Log.Info( "[Thorns WorldGen] Skipping settlement site plan (organic cluster placement)." );
			return;
		}

		context.SettlementConfig = host.BuildSettlementConfig( context.WorldWidth, context.WorldDepth );
		context.FoliagePropsNoise = ThornsWorldNoise.CreateFoliagePropsNoise( context.WorldSeed );
		context.PlacementRng = new Random( unchecked( context.WorldSeed ^ (int)0x61d8f02fu ) );
		context.DistrictPlanner = new ThornsProcBuildingDistrictPlanner( unchecked( context.WorldSeed ^ (int)0x77a90131 ) );

		ThornsProcBuildingIdentityGenerator.ClearSettlementPlacementDiagnostics();

		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldSettlementPlacementDebugViz.Clear();
			ThornsWorldSettlementSiteAnalysis.CollectDebug = true;
			ThornsWorldSettlementSiteAnalysis.ClearDebug();
		}
		else
		{
			ThornsWorldSettlementSiteAnalysis.CollectDebug = false;
		}

		context.Plan = ThornsWorldSettlementPlanner.Plan(
			context.WorldSeed,
			context.MinX,
			context.MaxX,
			context.MinY,
			context.MaxY,
			context.SettlementConfig,
			context.HeightsSpan,
			context.HeightRx,
			context.HeightRz,
			context.WorldWidth,
			context.WorldDepth,
			context.Spec.CenterOnWorldOrigin,
			context.FoliagePropsNoise,
			context.Spec );

		ThornsWorldSettlementDebugViz.LogPlan( context.Plan );
		if ( host.DrawSettlementLayoutDebug )
		{
			ThornsWorldSettlementDebugViz.DrawPlan( host.Scene, host.ChunkRoot, context.Plan );
			ThornsWorldSettlementSiteSelectionDebugViz.Draw(
				host.Scene,
				host.ChunkRoot,
				context.HeightsSpan,
				context.Spec,
				context.WorldWidth,
				context.WorldDepth,
				context.Plan.MainCity.CenterLocal );
		}
	}
}
