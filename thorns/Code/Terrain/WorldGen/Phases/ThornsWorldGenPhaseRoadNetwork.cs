namespace Sandbox;

/// <summary>Phase 4 — road/trail spline data (terrain mesh and decals deferred).</summary>
public sealed class ThornsWorldGenPhaseRoadNetwork : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.GenerateRoadNetwork;
	public string Name => "GenerateRoadNetwork";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.OrganicClusterPlacement )
		{
			context.RoadNetwork = new ThornsWorldRoadNetwork();
			Log.Info( "[Thorns WorldGen] Skipping road network (organic cluster placement)." );
			return;
		}

		context.RoadNetwork = ThornsWorldRoadNetwork.FromSettlementPlan( context.Plan );
	}
}
