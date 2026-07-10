using System.Collections.Generic;

namespace Sandbox;

/// <summary>Fixed building manifests for sparse survival-world structure (~30 POIs total).</summary>
public static class ThornsWorldSettlementComposition
{
	public static IReadOnlyList<ThornsWorldBuildingSlot> MainCitySlots { get; } = BuildMainCity();

	public static IReadOnlyList<ThornsWorldBuildingSlot> TownSlots( int townIndex ) =>
		townIndex switch
		{
			0 => TownA,
			1 => TownB,
			_ => TownC
		};

	public static string TownLabel( int townIndex ) =>
		townIndex switch
		{
			0 => "Town A",
			1 => "Town B",
			_ => "Town C"
		};

	static readonly IReadOnlyList<ThornsWorldBuildingSlot> TownA = new List<ThornsWorldBuildingSlot>
	{
		Slot( ThornsProcBuildingType.House, 0 ),
		Slot( ThornsProcBuildingType.House, 1 ),
		Slot( ThornsProcBuildingType.Store, 2 ),
		Slot( ThornsProcBuildingType.Warehouse, 3 ),
		Slot( ThornsProcBuildingType.Barn, 4 )
	};

	static readonly IReadOnlyList<ThornsWorldBuildingSlot> TownB = new List<ThornsWorldBuildingSlot>
	{
		Slot( ThornsProcBuildingType.House, 0 ),
		Slot( ThornsProcBuildingType.House, 1 ),
		Slot( ThornsProcBuildingType.Cabin, 2 ),
		Slot( ThornsProcBuildingType.Store, 3 ),
		Slot( ThornsProcBuildingType.Warehouse, 4 )
	};

	static readonly IReadOnlyList<ThornsWorldBuildingSlot> TownC = new List<ThornsWorldBuildingSlot>
	{
		Slot( ThornsProcBuildingType.House, 0 ),
		Slot( ThornsProcBuildingType.House, 1 ),
		Slot( ThornsProcBuildingType.House, 2 ),
		Slot( ThornsProcBuildingType.Store, 3 ),
		Slot( ThornsProcBuildingType.Ruin, 4 )
	};

	static List<ThornsWorldBuildingSlot> BuildMainCity()
	{
		// Outer: industrial + edge residential. Mid: blocks/stores/houses. Core: skyline landmarks.
		return new List<ThornsWorldBuildingSlot>
		{
			CitySlot( ThornsProcBuildingType.House, ThornsWorldCityRing.OuterRing, 0 ),
			CitySlot( ThornsProcBuildingType.House, ThornsWorldCityRing.OuterRing, 1 ),
			CitySlot( ThornsProcBuildingType.Factory, ThornsWorldCityRing.OuterRing, 2 ),
			CitySlot( ThornsProcBuildingType.Warehouse, ThornsWorldCityRing.OuterRing, 3 ),
			CitySlot( ThornsProcBuildingType.Apartment, ThornsWorldCityRing.MidRing, 4 ),
			CitySlot( ThornsProcBuildingType.Apartment, ThornsWorldCityRing.MidRing, 5 ),
			CitySlot( ThornsProcBuildingType.Store, ThornsWorldCityRing.MidRing, 6 ),
			CitySlot( ThornsProcBuildingType.Store, ThornsWorldCityRing.MidRing, 7 ),
			CitySlot( ThornsProcBuildingType.House, ThornsWorldCityRing.MidRing, 8 ),
			CitySlot( ThornsProcBuildingType.Skyscraper, ThornsWorldCityRing.Core, 9 ),
			CitySlot( ThornsProcBuildingType.OfficeBuilding, ThornsWorldCityRing.Core, 10 ),
			CitySlot( ThornsProcBuildingType.ApartmentTower, ThornsWorldCityRing.Core, 11 )
		};
	}

	public static IReadOnlyList<ThornsWorldIsolatedSite> PickIsolatedSites( int seed )
	{
		var pool = new List<ThornsProcBuildingType>
		{
			ThornsProcBuildingType.MilitaryComplex,
			ThornsProcBuildingType.Cabin,
			ThornsProcBuildingType.Barn,
			ThornsProcBuildingType.Ruin,
			ThornsProcBuildingType.RadioOutpost
		};

		var rnd = new Random( unchecked( seed ^ (int)0x6f4a12bc ) );
		for ( var i = pool.Count - 1; i > 0; i-- )
		{
			var j = rnd.Next( 0, i + 1 );
			var tmp = pool[i];
			pool[i] = pool[j];
			pool[j] = tmp;
		}

		return new List<ThornsWorldIsolatedSite>
		{
			new ThornsWorldIsolatedSite { Type = pool[0], Index = 0 },
			new ThornsWorldIsolatedSite { Type = pool[1], Index = 1 },
			new ThornsWorldIsolatedSite { Type = pool[2], Index = 2 }
		};
	}

	static ThornsWorldBuildingSlot CitySlot( ThornsProcBuildingType type, ThornsWorldCityRing ring, int index )
	{
		return new ThornsWorldBuildingSlot
		{
			Type = type,
			CityRing = ring,
			Index = index
		};
	}

	static ThornsWorldBuildingSlot Slot( ThornsProcBuildingType type, int index )
	{
		return new ThornsWorldBuildingSlot
		{
			Type = type,
			Index = index,
			CityRing = null
		};
	}
}
