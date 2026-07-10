namespace Sandbox;

/// <summary>Phase 6 — initializes footprint reservation service (per-building tests run during phase 8).</summary>
public sealed class ThornsWorldGenPhaseReserveBuildingFootprints : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.ReserveBuildingFootprints;
	public string Name => "ReserveBuildingFootprints";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		context.PlacementRng ??= new Random( unchecked( context.WorldSeed ^ (int)0x61d8f02fu ) );
		context.DistrictPlanner ??= new ThornsProcBuildingDistrictPlanner(
			unchecked( context.WorldSeed ^ (int)0x77a90131 ) );
		context.LayoutFactory ??= new ThornsWorldGenBuildingLayoutFactory( context.PlacementRng );

		context.FootprintReservation = new ThornsWorldGenFootprintReservation(
			context,
			host,
			host.BuildingFootprints );
	}
}
