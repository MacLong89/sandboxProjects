namespace Sandbox;

/// <summary>
/// Per-archetype facade materials for procedural buildings (plus legacy wood/stone/metal).
/// <see cref="DurabilityTierFromSlug"/> maps slugs back to wood/stone/metal for loot and health.
/// </summary>
public static class ThornsProcBuildingMaterialPalette
{
	public const int SlugCount = 14;

	public static readonly string[] AllSlugs =
	[
		"wood",
		"stone",
		"metal",
		"barn_wood",
		"light_wood_siding",
		"brick",
		"stone_brick",
		"cobblestone_brick",
		"stucco",
		"concrete",
		"concrete_dark",
		"sheet_metal",
		"glass_panes_light",
		"glass_panes_dark"
	];

	static readonly float[] _scratch = new float[SlugCount];

	public static int IndexOfSlug( string slug )
	{
		if ( string.IsNullOrEmpty( slug ) )
			return 0;

		for ( var i = 0; i < AllSlugs.Length; i++ )
		{
			if ( AllSlugs[i] == slug )
				return i;
		}

		return 0;
	}

	public static string SlugFromIndex( int index )
	{
		if ( index < 0 || index >= AllSlugs.Length )
			return AllSlugs[0];

		return AllSlugs[index];
	}

	public static string VmatPath( string slug ) => $"materials/{SlugFromIndex( IndexOfSlug( slug ) )}.vmat";

	/// <summary>
	/// Interior walk surface (floors + ramps) — paired with facade slug so decks read distinct from walls.
	/// Uses existing palette vmats only (no duplicate art per building).
	/// </summary>
	public static string InteriorFloorSlugForFacade( string facadeSlug )
	{
		var slug = SlugFromIndex( IndexOfSlug( facadeSlug ) );
		return slug switch
		{
			"wood" or "brick" or "stucco" or "sheet_metal" => "concrete",
			"light_wood_siding" or "barn_wood" => "wood",
			"stone_brick" or "cobblestone_brick" => "stone",
			"concrete" => "concrete_dark",
			"concrete_dark" => "concrete",
			"metal" => "metal",
			"glass_panes_light" => "concrete",
			"glass_panes_dark" => "concrete_dark",
			_ => "concrete"
		};
	}

	/// <summary>Loot / C4 / health tier derived from facade material.</summary>
	public static ThornsBuildingMaterialTier DurabilityTierFromSlug( string slug ) =>
		slug switch
		{
			"wood" or "barn_wood" or "light_wood_siding" => ThornsBuildingMaterialTier.Wood,
			"metal" or "sheet_metal" or "glass_panes_light" or "glass_panes_dark" => ThornsBuildingMaterialTier.Metal,
			_ => ThornsBuildingMaterialTier.Stone
		};

	public static string PickMaterialSlug(
		ThornsProcBuildingType type,
		Random rnd,
		ThornsWorldSettlementKind settlement = ThornsWorldSettlementKind.Isolated,
		ThornsWorldCityRing? cityRing = null )
	{
		FillWeightsForType( type, _scratch );
		ApplySettlementMaterialBias( settlement, cityRing, _scratch );
		return SampleSlug( rnd, _scratch );
	}

	/// <summary>Dominant facade slug for an archetype (stable — dev galleries / labels).</summary>
	public static string PickRepresentativeMaterialSlug(
		ThornsProcBuildingType type,
		bool applySettlementBias = false,
		ThornsWorldSettlementKind settlement = ThornsWorldSettlementKind.Isolated,
		ThornsWorldCityRing? cityRing = null )
	{
		FillWeightsForType( type, _scratch );
		if ( applySettlementBias )
			ApplySettlementMaterialBias( settlement, cityRing, _scratch );

		var bestIndex = 0;
		var bestWeight = -1f;
		for ( var i = 0; i < _scratch.Length; i++ )
		{
			var w = _scratch[i];
			if ( w <= bestWeight )
				continue;

			bestWeight = w;
			bestIndex = i;
		}

		return AllSlugs[bestIndex];
	}

	static void FillWeightsForType( ThornsProcBuildingType type, float[] weights )
	{
		Array.Clear( weights, 0, weights.Length );
		void W( string slug, float w ) => weights[IndexOfSlug( slug )] += w;

		switch ( type )
		{
			case ThornsProcBuildingType.Cabin:
				W( "barn_wood", 4f );
				W( "light_wood_siding", 2.5f );
				W( "wood", 2f );
				break;
			case ThornsProcBuildingType.Barn:
				W( "barn_wood", 4.5f );
				W( "wood", 1.5f );
				W( "sheet_metal", 1.2f );
				break;
			case ThornsProcBuildingType.House:
				W( "light_wood_siding", 3f );
				W( "wood", 2f );
				W( "brick", 2f );
				W( "stucco", 1.2f );
				break;
			case ThornsProcBuildingType.Ruin:
				W( "barn_wood", 2f );
				W( "brick", 2f );
				W( "stone_brick", 1.8f );
				W( "cobblestone_brick", 1.5f );
				W( "stone", 1f );
				break;
			case ThornsProcBuildingType.Store:
				W( "brick", 2.5f );
				W( "stucco", 2f );
				W( "light_wood_siding", 1.2f );
				W( "concrete", 1f );
				break;
			case ThornsProcBuildingType.Apartment:
				W( "brick", 2.5f );
				W( "stucco", 2f );
				W( "concrete", 1.5f );
				W( "stone_brick", 1f );
				break;
			case ThornsProcBuildingType.Warehouse:
				W( "sheet_metal", 3f );
				W( "concrete", 2f );
				W( "concrete_dark", 1.5f );
				break;
			case ThornsProcBuildingType.Factory:
				W( "sheet_metal", 2.5f );
				W( "concrete", 2f );
				W( "concrete_dark", 1.8f );
				W( "metal", 1.2f );
				break;
			case ThornsProcBuildingType.MilitaryComplex:
				W( "concrete_dark", 3f );
				W( "sheet_metal", 2.5f );
				W( "metal", 2f );
				break;
			case ThornsProcBuildingType.RadioOutpost:
				W( "metal", 3f );
				W( "sheet_metal", 2f );
				W( "concrete_dark", 1f );
				break;
			case ThornsProcBuildingType.OfficeBuilding:
				W( "glass_panes_light", 2.5f );
				W( "glass_panes_dark", 2f );
				W( "concrete", 2f );
				W( "stone_brick", 1f );
				break;
			case ThornsProcBuildingType.ApartmentTower:
				W( "concrete", 2.5f );
				W( "glass_panes_light", 2f );
				W( "glass_panes_dark", 1.5f );
				W( "stucco", 1f );
				W( "metal", 0.8f );
				break;
			case ThornsProcBuildingType.Skyscraper:
				W( "glass_panes_light", 3f );
				W( "glass_panes_dark", 2.5f );
				W( "concrete", 2f );
				W( "metal", 1.5f );
				break;
			default:
				W( "stone", 1.5f );
				W( "brick", 1f );
				W( "wood", 1f );
				break;
		}
	}

	static void ApplySettlementMaterialBias(
		ThornsWorldSettlementKind settlement,
		ThornsWorldCityRing? cityRing,
		float[] weights )
	{
		void MulSlug( string slug, float mul ) => weights[IndexOfSlug( slug )] *= mul;

		switch ( settlement )
		{
			case ThornsWorldSettlementKind.MainCity:
				var glassMul = cityRing == ThornsWorldCityRing.Core ? 1.85f : 1.35f;
				var concreteMul = 1.45f;
				var woodMul = cityRing == ThornsWorldCityRing.Core ? 0.15f : 0.45f;
				MulSlug( "glass_panes_light", glassMul );
				MulSlug( "glass_panes_dark", glassMul );
				MulSlug( "concrete", concreteMul );
				MulSlug( "concrete_dark", concreteMul );
				MulSlug( "sheet_metal", 1.35f );
				MulSlug( "metal", 1.25f );
				MulSlug( "wood", woodMul );
				MulSlug( "barn_wood", woodMul * 0.6f );
				MulSlug( "light_wood_siding", woodMul * 0.85f );
				break;
			case ThornsWorldSettlementKind.Town:
				MulSlug( "wood", 1.4f );
				MulSlug( "barn_wood", 1.35f );
				MulSlug( "light_wood_siding", 1.3f );
				MulSlug( "brick", 1.2f );
				MulSlug( "glass_panes_light", 0.35f );
				MulSlug( "glass_panes_dark", 0.35f );
				break;
			default:
				MulSlug( "wood", 1.55f );
				MulSlug( "barn_wood", 1.5f );
				MulSlug( "light_wood_siding", 1.35f );
				MulSlug( "glass_panes_light", 0.12f );
				MulSlug( "glass_panes_dark", 0.12f );
				MulSlug( "metal", 0.2f );
				break;
		}
	}

	static string SampleSlug( Random rnd, float[] weights )
	{
		var total = 0f;
		for ( var i = 0; i < weights.Length; i++ )
			total += Math.Max( 0f, weights[i] );

		if ( total <= 0.001f )
			return AllSlugs[0];

		var roll = (float)rnd.NextDouble() * total;
		for ( var i = 0; i < weights.Length; i++ )
		{
			roll -= Math.Max( 0f, weights[i] );
			if ( roll <= 0f )
				return AllSlugs[i];
		}

		return AllSlugs[^1];
	}
}
