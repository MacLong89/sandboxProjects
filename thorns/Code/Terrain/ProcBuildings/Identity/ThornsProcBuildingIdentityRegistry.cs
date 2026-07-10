namespace Sandbox;

public readonly record struct ThornsProcBuildingTypeDefinition(
	ThornsProcBuildingType Type,
	string DisplayName,
	string MinimapCategory,
	int StoriesMin,
	int StoriesMax,
	int WidthMin,
	int WidthMax,
	int DepthMin,
	int DepthMax,
	float ExteriorWindowChance,
	bool PreferLargeFootprint,
	float ClusterSpacingMul,
	ThornsProcBuildingType? RuinSourceType = null );

/// <summary>
/// Type metadata, district weights, and neighbor adjacency boosts.
/// Visual/loot material tier (wood / stone / metal) is assigned at spawn via <see cref="ThornsProcBuildingMaterialAffinity"/>.
/// </summary>
public static class ThornsProcBuildingIdentityRegistry
{
	public const int IdentityRulesetVersion = 7;

	public static int TypeCount => Enum.GetValues<ThornsProcBuildingType>().Length;

	static Dictionary<ThornsProcBuildingType, ThornsProcBuildingTypeDefinition> _types;
	static Dictionary<ThornsProcBuildingDistrict, float[]> _districtWeights;
	static Dictionary<ThornsProcBuildingType, float[]> _adjacencyBoost;
	static bool _validated;

	public static IReadOnlyDictionary<ThornsProcBuildingType, ThornsProcBuildingTypeDefinition> Types
	{
		get
		{
			EnsureBuilt();
			return _types;
		}
	}

	public static IReadOnlyDictionary<ThornsProcBuildingDistrict, float[]> DistrictBaseWeights
	{
		get
		{
			EnsureBuilt();
			return _districtWeights;
		}
	}

	public static IReadOnlyDictionary<ThornsProcBuildingType, float[]> AdjacencyBoost
	{
		get
		{
			EnsureBuilt();
			return _adjacencyBoost;
		}
	}

	static void EnsureBuilt()
	{
		var expected = TypeCount;
		if ( _types is not null && _types.Count == expected && _validated )
			return;

		_types = BuildTypes();
		_districtWeights = BuildDistrictWeights();
		_adjacencyBoost = BuildAdjacency();
		ValidateCoverage();
		_validated = true;
	}

	static void ValidateCoverage()
	{
		foreach ( var type in Enum.GetValues<ThornsProcBuildingType>() )
		{
			if ( !_types.ContainsKey( type ) )
				Log.Error( $"[Thorns ProcBuilding] Missing type definition for {type} — add to {nameof( BuildTypes )}." );

			if ( !_adjacencyBoost.ContainsKey( type ) )
				Log.Warning( $"[Thorns ProcBuilding] Missing adjacency row for {type}." );
		}

		foreach ( var pair in _districtWeights )
		{
			if ( pair.Value.Length != TypeCount )
			{
				Log.Error(
					$"[Thorns ProcBuilding] District {pair.Key} weight length {pair.Value.Length} != type count {TypeCount}." );
			}
		}
	}

	static Dictionary<ThornsProcBuildingType, ThornsProcBuildingTypeDefinition> BuildTypes()
	{
		var dict = new Dictionary<ThornsProcBuildingType, ThornsProcBuildingTypeDefinition>
		{
			[ThornsProcBuildingType.House] = Def( ThornsProcBuildingType.House, "House", "residential", 1, 3, 4, 5, 4, 5, 0.32f, false, 0.92f ),
			[ThornsProcBuildingType.Ruin] = Def( ThornsProcBuildingType.Ruin, "Ruin", "residential", 1, 3, 4, 5, 4, 5, 0.08f, false, 0.95f, ThornsProcBuildingType.House ),
			[ThornsProcBuildingType.Warehouse] = Def( ThornsProcBuildingType.Warehouse, "Warehouse", "industrial", 1, 3, 8, 10, 4, 5, 0.06f, true, 1.15f ),
			[ThornsProcBuildingType.MilitaryComplex] = Def( ThornsProcBuildingType.MilitaryComplex, "Military Compound", "military", 1, 1, 9, 12, 9, 12, 0.05f, true, 1.35f ),
			[ThornsProcBuildingType.Cabin] = Def( ThornsProcBuildingType.Cabin, "Cabin", "rural", 1, 1, 3, 4, 3, 4, 0.18f, false, 1.25f ),
			[ThornsProcBuildingType.Store] = Def( ThornsProcBuildingType.Store, "Store", "commercial", 1, 1, 5, 6, 3, 4, 0.42f, false, 1.0f ),
			[ThornsProcBuildingType.Apartment] = Def( ThornsProcBuildingType.Apartment, "Apartment Block", "residential", 1, 3, 4, 5, 5, 7, 0.28f, false, 0.88f ),
			[ThornsProcBuildingType.Factory] = Def( ThornsProcBuildingType.Factory, "Factory", "industrial", 1, 3, 7, 9, 5, 7, 0.08f, true, 1.2f ),
			[ThornsProcBuildingType.Barn] = Def( ThornsProcBuildingType.Barn, "Barn", "rural", 1, 3, 5, 7, 4, 5, 0.05f, true, 1.3f ),
			[ThornsProcBuildingType.RadioOutpost] = Def( ThornsProcBuildingType.RadioOutpost, "Radio Outpost", "military", 1, 3, 4, 5, 4, 5, 0.12f, false, 1.25f ),
			[ThornsProcBuildingType.ApartmentTower] = Def( ThornsProcBuildingType.ApartmentTower, "Apartment Tower", "urban_highrise", 5, 5, 5, 5, 5, 5, 0.34f, false, 1.05f ),
			[ThornsProcBuildingType.Skyscraper] = Def( ThornsProcBuildingType.Skyscraper, "Skyscraper", "urban_landmark", 8, 8, 6, 6, 6, 6, 0.22f, false, 1.45f ),
			[ThornsProcBuildingType.OfficeBuilding] = Def( ThornsProcBuildingType.OfficeBuilding, "Office Building", "urban_commercial", 4, 4, 6, 6, 5, 5, 0.26f, false, 1.12f ),
		};

		foreach ( var type in Enum.GetValues<ThornsProcBuildingType>() )
		{
			if ( dict.ContainsKey( type ) )
				continue;

			Log.Warning( $"[Thorns ProcBuilding] Auto-registering fallback definition for {type}." );
			dict[type] = Def( type, type.ToString(), "mixed", 1, 3, 4, 4, 4, 4, 0.2f, false, 1f );
		}

		return dict;
	}

	static ThornsProcBuildingTypeDefinition Def(
		ThornsProcBuildingType type,
		string name,
		string cat,
		int sMin,
		int sMax,
		int wMin,
		int wMax,
		int dMin,
		int dMax,
		float win,
		bool large,
		float spacing,
		ThornsProcBuildingType? ruinSource = null ) =>
		new( type, name, cat, sMin, sMax, wMin, wMax, dMin, dMax, win, large, spacing, ruinSource );

	static Dictionary<ThornsProcBuildingDistrict, float[]> BuildDistrictWeights()
	{
		float[] W(
			float house,
			float ruin,
			float warehouse,
			float military,
			float cabin,
			float store,
			float apartment,
			float factory,
			float barn,
			float radio,
			float apartmentTower,
			float skyscraper,
			float office )
		{
			var row = new[]
			{
				house, ruin, warehouse, military, cabin, store, apartment, factory, barn, radio, apartmentTower,
				skyscraper, office
			};
			if ( row.Length != TypeCount )
				Log.Error( $"[Thorns ProcBuilding] District weight row length {row.Length} != {TypeCount}." );
			return row;
		}

		return new Dictionary<ThornsProcBuildingDistrict, float[]>
		{
			[ThornsProcBuildingDistrict.Residential] = W( 34, 20, 2, 1, 8, 5, 10, 1, 2, 1, 14, 2, 4 ),
			[ThornsProcBuildingDistrict.Industrial] = W( 2, 4, 34, 4, 2, 3, 2, 28, 6, 3, 1, 0.5f, 2 ),
			[ThornsProcBuildingDistrict.Military] = W( 1, 3, 6, 38, 2, 1, 2, 6, 2, 18, 1, 0.5f, 1 ),
			[ThornsProcBuildingDistrict.Rural] = W( 6, 10, 2, 2, 32, 2, 2, 2, 28, 4, 0.5f, 0f, 0.5f ),
			[ThornsProcBuildingDistrict.Commercial] = W( 3, 5, 16, 2, 2, 28, 8, 8, 2, 2, 10, 8, 16 ),
			[ThornsProcBuildingDistrict.Mixed] = W( 12, 8, 10, 7, 9, 10, 8, 9, 7, 5, 11, 6, 12 ),
		};
	}

	static Dictionary<ThornsProcBuildingType, float[]> BuildAdjacency()
	{
		float[] B(
			float house,
			float ruin,
			float warehouse,
			float military,
			float cabin,
			float store,
			float apartment,
			float factory,
			float barn,
			float radio,
			float apartmentTower,
			float skyscraper,
			float office ) =>
			[house, ruin, warehouse, military, cabin, store, apartment, factory, barn, radio, apartmentTower, skyscraper, office];

		return new Dictionary<ThornsProcBuildingType, float[]>
		{
			[ThornsProcBuildingType.House] = B( 1.35f, 1.2f, 0.7f, 0.45f, 0.9f, 1.25f, 1.15f, 0.65f, 0.75f, 0.6f, 1.2f, 0.75f, 0.9f ),
			[ThornsProcBuildingType.Ruin] = B( 1.2f, 1.15f, 0.75f, 0.5f, 0.95f, 1.1f, 1.05f, 0.7f, 0.8f, 0.55f, 1.05f, 0.7f, 0.85f ),
			[ThornsProcBuildingType.Warehouse] = B( 0.75f, 0.8f, 1.4f, 0.7f, 0.6f, 0.95f, 0.7f, 1.35f, 0.85f, 0.75f, 0.65f, 0.55f, 0.85f ),
			[ThornsProcBuildingType.MilitaryComplex] = B( 0.4f, 0.55f, 0.85f, 1.3f, 0.5f, 0.45f, 0.5f, 0.8f, 0.5f, 1.25f, 0.45f, 0.5f, 0.45f ),
			[ThornsProcBuildingType.Cabin] = B( 0.85f, 0.95f, 0.55f, 0.45f, 1.1f, 0.6f, 0.55f, 0.5f, 1.2f, 0.55f, 0.4f, 0.35f, 0.4f ),
			[ThornsProcBuildingType.Store] = B( 1.1f, 0.9f, 1.05f, 0.5f, 0.65f, 1.25f, 1.05f, 0.85f, 0.7f, 0.65f, 0.95f, 0.8f, 1.2f ),
			[ThornsProcBuildingType.Apartment] = B( 1.1f, 0.95f, 0.65f, 0.45f, 0.55f, 1.05f, 1.3f, 0.6f, 0.5f, 0.5f, 1.35f, 0.9f, 1.05f ),
			[ThornsProcBuildingType.Factory] = B( 0.7f, 0.75f, 1.25f, 0.75f, 0.55f, 0.8f, 0.65f, 1.35f, 0.9f, 0.7f, 0.6f, 0.65f, 0.75f ),
			[ThornsProcBuildingType.Barn] = B( 0.8f, 0.9f, 0.7f, 0.5f, 1.05f, 0.65f, 0.5f, 0.75f, 1.25f, 0.5f, 0.35f, 0.3f, 0.4f ),
			[ThornsProcBuildingType.RadioOutpost] = B( 0.55f, 0.6f, 0.9f, 1.15f, 0.5f, 0.55f, 0.55f, 0.75f, 0.5f, 1.2f, 0.5f, 0.65f, 0.5f ),
			[ThornsProcBuildingType.ApartmentTower] = B( 1.15f, 1.05f, 0.6f, 0.4f, 0.45f, 1.0f, 1.25f, 0.55f, 0.45f, 0.45f, 1.4f, 1.1f, 1.15f ),
			[ThornsProcBuildingType.Skyscraper] = B( 0.7f, 0.75f, 0.75f, 0.55f, 0.4f, 0.85f, 0.8f, 0.7f, 0.4f, 0.6f, 1.05f, 1.25f, 1.1f ),
			[ThornsProcBuildingType.OfficeBuilding] = B( 0.85f, 0.8f, 0.9f, 0.5f, 0.45f, 1.15f, 0.95f, 0.8f, 0.45f, 0.5f, 1.1f, 1.05f, 1.3f ),
		};
	}

	public static bool TryGet( ThornsProcBuildingType type, out ThornsProcBuildingTypeDefinition definition )
	{
		EnsureBuilt();
		return _types.TryGetValue( type, out definition );
	}

	public static ThornsProcBuildingTypeDefinition Get( ThornsProcBuildingType type )
	{
		if ( TryGet( type, out var def ) )
			return def;

		Log.Error( $"[Thorns ProcBuilding] Unknown type {type} ({(int)type}); using House fallback." );
		return _types[ThornsProcBuildingType.House];
	}

	public static string GetDisplayName( ThornsProcBuildingType type, bool ruined )
	{
		if ( ruined && TryGet( ThornsProcBuildingType.Ruin, out var r ) )
			return r.DisplayName;

		return Get( type ).DisplayName;
	}

	public static bool IsVerticalLandmark( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.ApartmentTower
			or ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.OfficeBuilding;

	const float OrganicLandmarkFootprintCells = 16f;

	/// <summary>Footprint cells used for organic spawn weighting (landmarks use compact 4×4 blueprints).</summary>
	public static float OrganicSpawnFootprintCells( ThornsProcBuildingType type )
	{
		if ( IsVerticalLandmark( type ) )
			return OrganicLandmarkFootprintCells;

		var def = Get( type );
		var avgW = ( def.WidthMin + def.WidthMax ) * 0.5f;
		var avgD = ( def.DepthMin + def.DepthMax ) * 0.5f;
		return avgW * avgD;
	}

	/// <summary>Organic map scatter: penalize large footprints, not story height; landmarks get a weight floor.</summary>
	public static float OrganicSpawnSizeWeight( ThornsProcBuildingType type )
	{
		var footprintCells = OrganicSpawnFootprintCells( type );
		var weight = 1f / MathF.Max( 8f, footprintCells );
		if ( IsVerticalLandmark( type ) )
			return MathF.Max( 0.14f, weight * 2.8f );

		var def = Get( type );
		if ( def.PreferLargeFootprint )
			weight *= 0.72f;

		return weight;
	}
}
