namespace Sandbox;

/// <summary>Ordered phases for the Thorns world-generation pipeline (pre- and post-chunk).</summary>
public enum ThornsWorldGenerationPhaseId
{
	GenerateMacroTerrain = 1,
	SelectSettlementLocations = 2,
	GenerateSettlementTerrain = 3,
	GenerateRoadNetwork = 4,
	GenerateSettlementBlocks = 5,
	ReserveBuildingFootprints = 6,
	GenerateBuildingLayouts = 7,
	SpawnBuildings = 8,
	GenerateLootAndProps = 9,
	GenerateEnvironmentDetails = 10,
	ApplyRoadTerrain = 11
}
