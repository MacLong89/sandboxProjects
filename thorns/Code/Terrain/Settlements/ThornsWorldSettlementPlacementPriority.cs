namespace Sandbox;

/// <summary>Deterministic placement order — larger / more important structures first.</summary>
public static class ThornsWorldSettlementPlacementPriority
{
	public static int GetRank( ThornsProcBuildingType type ) =>
		type switch
		{
			ThornsProcBuildingType.Skyscraper => 120,
			ThornsProcBuildingType.ApartmentTower => 110,
			ThornsProcBuildingType.OfficeBuilding => 100,
			ThornsProcBuildingType.Factory => 90,
			ThornsProcBuildingType.Warehouse => 85,
			ThornsProcBuildingType.Apartment => 80,
			ThornsProcBuildingType.Store => 70,
			ThornsProcBuildingType.House => 60,
			ThornsProcBuildingType.Cabin => 55,
			ThornsProcBuildingType.Barn => 50,
			ThornsProcBuildingType.Ruin => 45,
			ThornsProcBuildingType.RadioOutpost => 40,
			ThornsProcBuildingType.MilitaryComplex => 35,
			_ => 10
		};

	public static int CompareSlots( ThornsWorldBuildingSlot a, ThornsWorldBuildingSlot b )
	{
		var ra = GetRank( a.Type );
		var rb = GetRank( b.Type );
		if ( ra != rb )
			return rb.CompareTo( ra );

		var ringA = (int)( a.CityRing ?? ThornsWorldCityRing.MidRing );
		var ringB = (int)( b.CityRing ?? ThornsWorldCityRing.MidRing );
		if ( ringA != ringB )
			return ringA.CompareTo( ringB );

		return a.Index.CompareTo( b.Index );
	}

	public static int CompareIsolated( ThornsWorldIsolatedSite a, ThornsWorldIsolatedSite b ) =>
		GetRank( b.Type ).CompareTo( GetRank( a.Type ) );
}
