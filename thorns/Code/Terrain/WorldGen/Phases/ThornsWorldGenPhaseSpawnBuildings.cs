namespace Sandbox;

/// <summary>Phase 8 — place and spawn all settlement structures (city, towns, isolated).</summary>
public sealed class ThornsWorldGenPhaseSpawnBuildings : IThornsWorldGenerationPhase
{
	public ThornsWorldGenerationPhaseId Id => ThornsWorldGenerationPhaseId.SpawnBuildings;
	public string Name => "SpawnBuildings";

	public void Execute( ThornsWorldGenerationContext context, ThornsWorldGenerationHostBridge host )
	{
		if ( host.OrganicClusterPlacement )
			ThornsWorldGenOrganicClusterPlacer.PlaceAll( context, host );
		else
			ThornsWorldGenSettlementPlacer.PlaceAll( context, host );
	}
}
