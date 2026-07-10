namespace Terraingen.Buildings;

using Terraingen.Buildings.Settlement;
using Terraingen.GameData;

public readonly struct ThornsWeightedBuildingType
{
	public readonly ThornsProcBuildingType Type;
	public readonly int Weight;

	public ThornsWeightedBuildingType( ThornsProcBuildingType type, int weight )
	{
		Type = type;
		Weight = weight;
	}
}

public readonly struct ThornsPoiIdentityDefinition
{
	public readonly ThornsPoiIdentity Identity;
	public readonly string DisplayName;
	public readonly int MinBuildings;
	public readonly int MaxBuildings;
	public readonly int GalleryBuildingCount;
	public readonly int MaxStories;
	public readonly float RadiusInches;
	public readonly float MinLotSpacingInches;
	public readonly ThornsWeightedBuildingType[] WeightedBuildingTypes;

	public ThornsPoiIdentityDefinition(
		ThornsPoiIdentity identity,
		string displayName,
		int minBuildings,
		int maxBuildings,
		int galleryBuildingCount,
		int maxStories,
		float radiusInches,
		float minLotSpacingInches,
		ThornsWeightedBuildingType[] weightedBuildingTypes )
	{
		Identity = identity;
		DisplayName = displayName;
		MinBuildings = minBuildings;
		MaxBuildings = maxBuildings;
		GalleryBuildingCount = galleryBuildingCount;
		MaxStories = maxStories;
		RadiusInches = radiusInches;
		MinLotSpacingInches = minLotSpacingInches;
		WeightedBuildingTypes = weightedBuildingTypes ?? Array.Empty<ThornsWeightedBuildingType>();
	}

	public bool UsesBlockGrid( int buildingCount, bool streetFirstEnabled ) =>
		streetFirstEnabled && buildingCount >= SettlementGridConstants.MiniGridMinBuildings;
}

public static class ThornsPoiIdentityCatalog
{
	static readonly ThornsWeightedBuildingType[] MetropolisMix =
	{
		W( ThornsProcBuildingType.Skyscraper, 22 ),
		W( ThornsProcBuildingType.ApartmentTower, 20 ),
		W( ThornsProcBuildingType.OfficeBuilding, 16 ),
		W( ThornsProcBuildingType.Apartment, 14 ),
		W( ThornsProcBuildingType.Store, 10 ),
		W( ThornsProcBuildingType.Warehouse, 8 ),
		W( ThornsProcBuildingType.Factory, 6 ),
		W( ThornsProcBuildingType.RadioOutpost, 4 )
	};

	static readonly ThornsWeightedBuildingType[] CityMix =
	{
		W( ThornsProcBuildingType.Skyscraper, 14 ),
		W( ThornsProcBuildingType.ApartmentTower, 12 ),
		W( ThornsProcBuildingType.OfficeBuilding, 12 ),
		W( ThornsProcBuildingType.Apartment, 14 ),
		W( ThornsProcBuildingType.House, 10 ),
		W( ThornsProcBuildingType.Store, 12 ),
		W( ThornsProcBuildingType.Warehouse, 8 ),
		W( ThornsProcBuildingType.Factory, 6 )
	};

	static readonly ThornsWeightedBuildingType[] TownMix =
	{
		W( ThornsProcBuildingType.House, 24 ),
		W( ThornsProcBuildingType.Store, 20 ),
		W( ThornsProcBuildingType.Apartment, 14 ),
		W( ThornsProcBuildingType.OfficeBuilding, 10 ),
		W( ThornsProcBuildingType.Warehouse, 10 ),
		W( ThornsProcBuildingType.Factory, 8 ),
		W( ThornsProcBuildingType.Ruin, 4 )
	};

	static readonly ThornsWeightedBuildingType[] SuburbMix =
	{
		W( ThornsProcBuildingType.House, 52 ),
		W( ThornsProcBuildingType.Store, 28 ),
		W( ThornsProcBuildingType.Apartment, 14 ),
		W( ThornsProcBuildingType.Ruin, 6 )
	};

	static readonly ThornsWeightedBuildingType[] RuralMix =
	{
		W( ThornsProcBuildingType.House, 40 ),
		W( ThornsProcBuildingType.Barn, 35 ),
		W( ThornsProcBuildingType.Cabin, 25 )
	};

	static readonly ThornsWeightedBuildingType[] MilitaryMix =
	{
		W( ThornsProcBuildingType.MilitaryComplex, 72 ),
		W( ThornsProcBuildingType.RadioOutpost, 28 )
	};

	static readonly ThornsWeightedBuildingType[] CabinMix =
	{
		W( ThornsProcBuildingType.Cabin, 80 ),
		W( ThornsProcBuildingType.Barn, 20 )
	};

	static readonly ThornsWeightedBuildingType[] FarmMix =
	{
		W( ThornsProcBuildingType.Barn, 55 ),
		W( ThornsProcBuildingType.House, 30 ),
		W( ThornsProcBuildingType.Cabin, 15 )
	};

	static readonly ThornsPoiIdentityDefinition Metropolis = new(
		ThornsPoiIdentity.Metropolis,
		"Metropolis",
		16,
		20,
		18,
		6,
		1750f,
		340f,
		MetropolisMix );

	static readonly ThornsPoiIdentityDefinition City = new(
		ThornsPoiIdentity.City,
		"City",
		12,
		16,
		14,
		6,
		1450f,
		350f,
		CityMix );

	static readonly ThornsPoiIdentityDefinition Town = new(
		ThornsPoiIdentity.Town,
		"Town",
		10,
		14,
		12,
		5,
		1250f,
		370f,
		TownMix );

	static readonly ThornsPoiIdentityDefinition Suburb = new(
		ThornsPoiIdentity.Suburb,
		"Suburb",
		8,
		12,
		10,
		4,
		1120f,
		390f,
		SuburbMix );

	static readonly ThornsPoiIdentityDefinition Rural = new(
		ThornsPoiIdentity.Rural,
		"Rural",
		4,
		6,
		5,
		3,
		720f,
		320f,
		RuralMix );

	static readonly ThornsPoiIdentityDefinition Military = new(
		ThornsPoiIdentity.Military,
		"Military",
		4,
		8,
		6,
		3,
		820f,
		340f,
		MilitaryMix );

	static readonly ThornsPoiIdentityDefinition CabinSite = new(
		ThornsPoiIdentity.CabinSite,
		"Cabin Site",
		1,
		3,
		2,
		2,
		420f,
		260f,
		CabinMix );

	static readonly ThornsPoiIdentityDefinition Farmstead = new(
		ThornsPoiIdentity.Farmstead,
		"Farmstead",
		2,
		4,
		2,
		2,
		460f,
		280f,
		FarmMix );

	static readonly ThornsPoiIdentity[] GalleryOrder =
	{
		ThornsPoiIdentity.Metropolis,
		ThornsPoiIdentity.City,
		ThornsPoiIdentity.Town,
		ThornsPoiIdentity.Suburb,
		ThornsPoiIdentity.Military,
		ThornsPoiIdentity.Rural,
		ThornsPoiIdentity.Farmstead,
		ThornsPoiIdentity.CabinSite
	};

	static readonly (ThornsPoiIdentity Identity, int Weight)[] MajorSpawnWeights =
	{
		(ThornsPoiIdentity.Town, 40),
		(ThornsPoiIdentity.City, 28),
		(ThornsPoiIdentity.Suburb, 18),
		(ThornsPoiIdentity.Military, 10),
		(ThornsPoiIdentity.Metropolis, 4)
	};

	static readonly (ThornsPoiIdentity Identity, int Count)[] MajorIdentityTemplate =
	{
		(ThornsPoiIdentity.Metropolis, 3),
		(ThornsPoiIdentity.Military, 3),
		(ThornsPoiIdentity.City, 4),
		(ThornsPoiIdentity.Town, 5),
		(ThornsPoiIdentity.Suburb, 2)
	};

	static readonly ThornsPoiIdentity[] MicroIdentityTemplate =
	{
		ThornsPoiIdentity.Rural,
		ThornsPoiIdentity.Rural,
		ThornsPoiIdentity.Farmstead,
		ThornsPoiIdentity.Farmstead,
		ThornsPoiIdentity.CabinSite,
		ThornsPoiIdentity.Military
	};

	static readonly ThornsPoiIdentity[] MajorTrimOrder =
	{
		ThornsPoiIdentity.Suburb,
		ThornsPoiIdentity.Town,
		ThornsPoiIdentity.City,
		ThornsPoiIdentity.Military,
		ThornsPoiIdentity.Metropolis
	};

	public const int MaxMetropolisesPerMap = 3;

	static readonly HashSet<ThornsProcBuildingType> UrbanTallTypes = new()
	{
		ThornsProcBuildingType.Skyscraper,
		ThornsProcBuildingType.ApartmentTower,
		ThornsProcBuildingType.OfficeBuilding
	};

	public static IReadOnlyList<ThornsPoiIdentity> GalleryIdentities => GalleryOrder;

	public static ThornsPoiIdentityDefinition Get( ThornsPoiIdentity identity ) => identity switch
	{
		ThornsPoiIdentity.Metropolis => Metropolis,
		ThornsPoiIdentity.City => City,
		ThornsPoiIdentity.Town => Town,
		ThornsPoiIdentity.Suburb => Suburb,
		ThornsPoiIdentity.Rural => Rural,
		ThornsPoiIdentity.Military => Military,
		ThornsPoiIdentity.CabinSite => CabinSite,
		ThornsPoiIdentity.Farmstead => Farmstead,
		_ => Town
	};

	public static int GetGalleryBuildingCount( ThornsPoiIdentity identity ) =>
		Math.Max( 1, Get( identity ).GalleryBuildingCount );

	public static int GetMaxStories( ThornsPoiIdentity identity ) =>
		Math.Clamp( Get( identity ).MaxStories, 1, ThornsProcBuildingSpawnDefaults.MaxStories );

	/// <summary>Shuffled major POI identities for a map (default template: 3 metro, 3 military, 4 city, 5 town, 2 suburb).</summary>
	public static ThornsPoiIdentity[] BuildMajorIdentityDeck( int townCount, Random rng )
	{
		townCount = Math.Max( 1, townCount );
		var deck = new List<ThornsPoiIdentity>( townCount );
		for ( var i = 0; i < MajorIdentityTemplate.Length; i++ )
		{
			var (identity, count) = MajorIdentityTemplate[i];
			for ( var n = 0; n < count; n++ )
				deck.Add( identity );
		}

		TrimMajorDeck( deck, townCount );
		while ( deck.Count < townCount )
			deck.Add( PickMajorIdentityWeighted( rng, deck ) );

		Shuffle( deck, rng );
		return deck.ToArray();
	}

	/// <summary>Shuffled micro POI identities (default: 2 rural, 2 farm, 1 cabin, 1 military).</summary>
	public static ThornsPoiIdentity[] BuildMicroIdentityDeck( int microCount, Random rng )
	{
		microCount = Math.Max( 0, microCount );
		if ( microCount == 0 )
			return Array.Empty<ThornsPoiIdentity>();

		var deck = new List<ThornsPoiIdentity>( microCount );
		for ( var i = 0; i < microCount; i++ )
			deck.Add( MicroIdentityTemplate[i % MicroIdentityTemplate.Length] );

		Shuffle( deck, rng );
		return deck.ToArray();
	}

	[Obsolete( "Use BuildMajorIdentityDeck." )]
	public static ThornsPoiIdentity PickMajorIdentity( Random rng, int slotIndex, int townCount, ref bool metropolisPlaced )
	{
		var deck = BuildMajorIdentityDeck( townCount, rng );
		return slotIndex >= 0 && slotIndex < deck.Length ? deck[slotIndex] : ThornsPoiIdentity.Town;
	}

	public static ThornsPoiIdentity PickMicroIdentity( Random rng ) =>
		MicroIdentityTemplate[rng.Next( MicroIdentityTemplate.Length )];

	public static ThornsProcBuildingType PickBuildingType(
		ThornsPoiIdentity identity,
		Random rng,
		int buildingIndex,
		SettlementRoadType? frontageRoad = null )
	{
		var def = Get( identity );
		if ( def.WeightedBuildingTypes.Length == 0 )
			return ThornsProcBuildingType.House;

		var weights = BuildPickWeights( def.WeightedBuildingTypes, identity, frontageRoad );
		var total = 0;
		for ( var i = 0; i < weights.Length; i++ )
			total += weights[i];

		if ( total <= 0 )
			return def.WeightedBuildingTypes[0].Type;

		var roll = Math.Abs( HashCode.Combine( buildingIndex, rng.Next(), identity, 0x701 ) ) % total;
		for ( var i = 0; i < weights.Length; i++ )
		{
			roll -= weights[i];
			if ( roll < 0 )
				return def.WeightedBuildingTypes[i].Type;
		}

		return def.WeightedBuildingTypes[^1].Type;
	}

	public static ThornsMapMarkerKind MapMarkerKind( ThornsPoiIdentity identity ) => identity switch
	{
		ThornsPoiIdentity.Metropolis => ThornsMapMarkerKind.Metropolis,
		ThornsPoiIdentity.City => ThornsMapMarkerKind.City,
		ThornsPoiIdentity.Town => ThornsMapMarkerKind.Town,
		ThornsPoiIdentity.Suburb => ThornsMapMarkerKind.Suburb,
		ThornsPoiIdentity.Rural => ThornsMapMarkerKind.RuralPoi,
		ThornsPoiIdentity.Military => ThornsMapMarkerKind.MilitaryPoi,
		ThornsPoiIdentity.CabinSite => ThornsMapMarkerKind.CabinSite,
		ThornsPoiIdentity.Farmstead => ThornsMapMarkerKind.Farmstead,
		_ => ThornsMapMarkerKind.Town
	};

	static ThornsPoiIdentity PickMajorIdentityWeighted( Random rng, IReadOnlyList<ThornsPoiIdentity> existing )
	{
		var metropolisCount = CountIdentity( existing, ThornsPoiIdentity.Metropolis );
		var allowMetropolis = metropolisCount < MaxMetropolisesPerMap;

		var total = 0;
		for ( var i = 0; i < MajorSpawnWeights.Length; i++ )
		{
			var entry = MajorSpawnWeights[i];
			if ( !allowMetropolis && entry.Identity == ThornsPoiIdentity.Metropolis )
				continue;
			total += entry.Weight;
		}

		if ( total <= 0 )
			return ThornsPoiIdentity.Town;

		var roll = rng.Next( total );
		for ( var i = 0; i < MajorSpawnWeights.Length; i++ )
		{
			var entry = MajorSpawnWeights[i];
			if ( !allowMetropolis && entry.Identity == ThornsPoiIdentity.Metropolis )
				continue;

			roll -= entry.Weight;
			if ( roll >= 0 )
				continue;

			return entry.Identity;
		}

		return ThornsPoiIdentity.Town;
	}

	static void TrimMajorDeck( List<ThornsPoiIdentity> deck, int townCount )
	{
		while ( deck.Count > townCount )
		{
			var removed = false;
			for ( var i = 0; i < MajorTrimOrder.Length; i++ )
			{
				var identity = MajorTrimOrder[i];
				var index = deck.LastIndexOf( identity );
				if ( index < 0 )
					continue;

				deck.RemoveAt( index );
				removed = true;
				break;
			}

			if ( !removed )
				deck.RemoveAt( deck.Count - 1 );
		}
	}

	static int CountIdentity( IReadOnlyList<ThornsPoiIdentity> identities, ThornsPoiIdentity identity )
	{
		var count = 0;
		for ( var i = 0; i < identities.Count; i++ )
		{
			if ( identities[i] == identity )
				count++;
		}

		return count;
	}

	static void Shuffle<T>( List<T> list, Random rng )
	{
		for ( var i = list.Count - 1; i > 0; i-- )
		{
			var j = rng.Next( i + 1 );
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	static int[] BuildPickWeights(
		ThornsWeightedBuildingType[] entries,
		ThornsPoiIdentity identity,
		SettlementRoadType? frontageRoad )
	{
		var weights = new int[entries.Length];
		for ( var i = 0; i < entries.Length; i++ )
			weights[i] = Math.Max( 0, entries[i].Weight );

		if ( frontageRoad is null || !IsUrbanIdentity( identity ) )
			return weights;

		var tallBoost = frontageRoad switch
		{
			SettlementRoadType.Primary => 3,
			SettlementRoadType.Secondary => 2,
			_ => 1
		};

		if ( tallBoost <= 1 )
			return weights;

		for ( var i = 0; i < entries.Length; i++ )
		{
			if ( UrbanTallTypes.Contains( entries[i].Type ) )
				weights[i] *= tallBoost;
		}

		return weights;
	}

	static bool IsUrbanIdentity( ThornsPoiIdentity identity ) =>
		identity is ThornsPoiIdentity.Metropolis
			or ThornsPoiIdentity.City
			or ThornsPoiIdentity.Town;

	static ThornsWeightedBuildingType W( ThornsProcBuildingType type, int weight ) =>
		new( type, weight );
}

public static class ThornsProcBuildingTypePicker
{
	static readonly ThornsProcBuildingType[] All =
	{
		ThornsProcBuildingType.House,
		ThornsProcBuildingType.Cabin,
		ThornsProcBuildingType.Store,
		ThornsProcBuildingType.Warehouse,
		ThornsProcBuildingType.Factory,
		ThornsProcBuildingType.Barn,
		ThornsProcBuildingType.MilitaryComplex,
		ThornsProcBuildingType.RadioOutpost,
		ThornsProcBuildingType.Apartment,
		ThornsProcBuildingType.ApartmentTower,
		ThornsProcBuildingType.Skyscraper,
		ThornsProcBuildingType.OfficeBuilding,
		ThornsProcBuildingType.Ruin
	};

	public static ThornsProcBuildingType Pick( int worldSeed, int buildingIndex ) =>
		All[Math.Abs( HashCode.Combine( worldSeed, buildingIndex, 0xB01D ) ) % All.Length];

	public static int PickVariantIndex( Random rng, ThornsProcBuildingType type, int buildingIndex )
	{
		var count = ThornsInteriorFurnitureAsciiLayouts.VariantCount( type );
		if ( count <= 1 )
			return 0;

		return Math.Abs( HashCode.Combine( buildingIndex, type, 0x4A21 ) ) % count;
	}
}
