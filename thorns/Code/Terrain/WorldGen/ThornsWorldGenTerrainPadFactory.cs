namespace Sandbox;

/// <summary>Creates terrain height pads for procedural buildings and settlement zones.</summary>
public static class ThornsWorldGenTerrainPadFactory
{
	public const float LocalFeatherApronWorld = 104f;
	public const float TownFeatherApronWorld = 168f;
	public const float MainCityFeatherApronWorld = 240f;

	public const float CityWallApronWorld = 76f;
	public const float TownWallApronWorld = 62f;
	public const float LocalWallApronWorld = 52f;

	public const float WallReachBeyondFootprint = ThornsBuildingModule.Cell * 1.05f;

	public static ThornsTerrainProcBuildingPad CreateLocalBuildingFeatherPad(
		float centerX,
		float centerY,
		float foundationHalfW,
		float foundationHalfD,
		float yawRadians,
		float surfaceZ,
		int doorSide = -1,
		bool mainCity = false,
		bool town = false,
		int blockIndex = -1,
		int blockBuildingCount = 1 )
	{
		var cell = ThornsBuildingModule.Cell;
		var restraint = ThornsSettlementDensityRestraint.Compute( blockBuildingCount );
		var densityMul = restraint.ApronStrengthMul;
		var dense = blockBuildingCount >= 2;
		var skirt = cell * ( mainCity ? (dense ? 1.35f : 2.4f) : town ? (dense ? 1.2f : 1.85f) : 1.35f );
		var wallApron = (mainCity ? CityWallApronWorld : town ? TownWallApronWorld : LocalWallApronWorld)
		                * (dense ? 0.26f : 1f);
		var outerApron = (mainCity ? MainCityFeatherApronWorld : town ? TownFeatherApronWorld : LocalFeatherApronWorld)
		                 * (dense ? 0.12f : 0.65f);
		var embed = (mainCity
				? ThornsBuildingFoundationTerrain.DefaultEmbedCity
				: town
					? ThornsBuildingFoundationTerrain.DefaultEmbedTown
					: ThornsBuildingFoundationTerrain.DefaultEmbedIsolated)
		            * (dense ? 0.18f : 1f);
		var door = ThornsBuildingFoundationTerrain.DoorOutwardWorld( doorSide, yawRadians );

		return new ThornsTerrainProcBuildingPad
		{
			Kind = ThornsSettlementTerrainPadKind.LocalBuilding,
			CenterX = centerX,
			CenterY = centerY,
			FoundationHalfW = foundationHalfW,
			FoundationHalfD = foundationHalfD,
			HalfW = foundationHalfW + skirt + WallReachBeyondFootprint,
			HalfD = foundationHalfD + skirt + WallReachBeyondFootprint,
			YawRadians = yawRadians,
			TargetZ = surfaceZ,
			FoundationEmbed = embed,
			WallApron = wallApron,
			Apron = outerApron,
			DoorOutwardX = door.x,
			DoorOutwardY = door.y,
			BlockIndex = blockIndex,
			ApronStrengthMul = densityMul,
			PeakBlend = restraint.PeakBlendCap
		};
	}

	/// <summary>Organic scatter: flat interior under the full footprint, moderate exterior feather.</summary>
	public static ThornsTerrainProcBuildingPad CreateOrganicBuildingPad(
		float centerX,
		float centerY,
		float foundationHalfW,
		float foundationHalfD,
		float yawRadians,
		float floorSurfaceZ,
		int doorSide = -1 )
	{
		const float skirt = ThornsBuildingModule.Cell * 1.1f;
		const float interiorPad = ThornsBuildingModule.Cell * 0.35f;
		var door = ThornsBuildingFoundationTerrain.DoorOutwardWorld( doorSide, yawRadians );
		return new ThornsTerrainProcBuildingPad
		{
			Kind = ThornsSettlementTerrainPadKind.LocalBuilding,
			CenterX = centerX,
			CenterY = centerY,
			FoundationHalfW = foundationHalfW + interiorPad,
			FoundationHalfD = foundationHalfD + interiorPad,
			HalfW = foundationHalfW + skirt + WallReachBeyondFootprint,
			HalfD = foundationHalfD + skirt + WallReachBeyondFootprint,
			YawRadians = yawRadians,
			TargetZ = floorSurfaceZ,
			FoundationEmbed = ThornsBuildingModule.FloorThickness * 0.5f,
			WallApron = LocalWallApronWorld * 0.55f,
			Apron = 96f,
			DoorOutwardX = door.x,
			DoorOutwardY = door.y,
			BlockIndex = -1,
			ApronStrengthMul = 1f,
			PeakBlend = 1f
		};
	}

	public static ThornsTerrainProcBuildingPad CreateIsolatedFeatherPad(
		float centerX,
		float centerY,
		float foundationHalfW,
		float foundationHalfD,
		float yawRadians,
		float surfaceZ,
		int doorSide = -1 )
	{
		const float skirt = ThornsBuildingModule.Cell * 1.35f;
		var door = ThornsBuildingFoundationTerrain.DoorOutwardWorld( doorSide, yawRadians );
		return new ThornsTerrainProcBuildingPad
		{
			Kind = ThornsSettlementTerrainPadKind.LocalBuilding,
			CenterX = centerX,
			CenterY = centerY,
			FoundationHalfW = foundationHalfW,
			FoundationHalfD = foundationHalfD,
			HalfW = foundationHalfW + skirt + WallReachBeyondFootprint,
			HalfD = foundationHalfD + skirt + WallReachBeyondFootprint,
			YawRadians = yawRadians,
			TargetZ = surfaceZ,
			FoundationEmbed = ThornsBuildingFoundationTerrain.DefaultEmbedIsolated,
			WallApron = LocalWallApronWorld,
			Apron = 168f,
			DoorOutwardX = door.x,
			DoorOutwardY = door.y,
			BlockIndex = -1,
			ApronStrengthMul = 1f,
			PeakBlend = 1f
		};
	}
}
