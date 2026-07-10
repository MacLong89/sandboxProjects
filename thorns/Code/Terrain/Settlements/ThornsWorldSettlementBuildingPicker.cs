namespace Sandbox;

/// <summary>Settlement placement tuning (fixed manifests live in <see cref="ThornsWorldSettlementComposition"/>).</summary>
public static class ThornsWorldSettlementBuildingPicker
{
	public static void BeginMainCityCluster( ThornsProcBuildingDistrictPlanner planner ) =>
		planner.BeginCluster( ThornsProcBuildingDistrict.Commercial );

	public static void BeginTownCluster( ThornsProcBuildingDistrictPlanner planner ) =>
		planner.BeginCluster( ThornsProcBuildingDistrict.Mixed );

	public static void BeginIsolatedCluster( ThornsProcBuildingDistrictPlanner planner ) =>
		planner.BeginCluster( ThornsProcBuildingDistrict.Rural );

	/// <summary>Edge clearance between footprints (1 = default street gap; lower = denser blocks).</summary>
	public static float CityRingClearanceMul( ThornsWorldCityRing ring ) =>
		ring switch
		{
			ThornsWorldCityRing.Core => 0.34f,
			ThornsWorldCityRing.MidRing => 0.56f,
			_ => 0.82f
		};
}
