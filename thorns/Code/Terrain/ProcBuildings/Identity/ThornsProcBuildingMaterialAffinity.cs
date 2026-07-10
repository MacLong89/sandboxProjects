namespace Sandbox;

/// <summary>
/// Maps procedural building archetypes to wood / stone / metal visuals and loot tiers.
/// Overlapping types roll tier from context (settlement ring) so the same blueprint reads rural vs urban.
/// </summary>
public static class ThornsProcBuildingMaterialAffinity
{
	public const int AffinityRulesetVersion = 1;

	public static bool IsPureWoodOnly( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Cabin or ThornsProcBuildingType.Barn;

	public static bool IsPureMetalOnly( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Skyscraper
			or ThornsProcBuildingType.MilitaryComplex
			or ThornsProcBuildingType.ApartmentTower
			or ThornsProcBuildingType.RadioOutpost;

	public static bool IsStoneOnly( ThornsProcBuildingType type ) =>
		type is ThornsProcBuildingType.Apartment;

	public static string PickMaterialSlug(
		ThornsProcBuildingType type,
		Random rnd,
		ThornsWorldSettlementKind settlement = ThornsWorldSettlementKind.Isolated,
		ThornsWorldCityRing? cityRing = null ) =>
		ThornsProcBuildingMaterialPalette.PickMaterialSlug( type, rnd, settlement, cityRing );

	public static string PickRepresentativeMaterialSlug(
		ThornsProcBuildingType type,
		bool applySettlementBias = false,
		ThornsWorldSettlementKind settlement = ThornsWorldSettlementKind.Isolated,
		ThornsWorldCityRing? cityRing = null ) =>
		ThornsProcBuildingMaterialPalette.PickRepresentativeMaterialSlug(
			type,
			applySettlementBias,
			settlement,
			cityRing );

	public static ThornsBuildingMaterialTier PickMaterialTier(
		ThornsProcBuildingType type,
		Random rnd,
		ThornsWorldSettlementKind settlement = ThornsWorldSettlementKind.Isolated,
		ThornsWorldCityRing? cityRing = null ) =>
		ThornsProcBuildingMaterialPalette.DurabilityTierFromSlug(
			PickMaterialSlug( type, rnd, settlement, cityRing ) );

	static void FillBaseOverlapWeights( ThornsProcBuildingType type, ref float wood, ref float stone, ref float metal )
	{
		switch ( type )
		{
			case ThornsProcBuildingType.House:
			case ThornsProcBuildingType.Ruin:
				wood = 0.5f;
				stone = 0.5f;
				break;
			case ThornsProcBuildingType.Store:
				wood = 0.45f;
				stone = 0.55f;
				break;
			case ThornsProcBuildingType.Warehouse:
				stone = 0.42f;
				metal = 0.58f;
				break;
			case ThornsProcBuildingType.Factory:
				stone = 0.38f;
				metal = 0.62f;
				break;
			case ThornsProcBuildingType.OfficeBuilding:
				stone = 0.32f;
				metal = 0.68f;
				break;
			default:
				stone = 1f;
				break;
		}
	}

	static void ApplySettlementBias(
		ThornsWorldSettlementKind settlement,
		ThornsWorldCityRing? cityRing,
		ref float wood,
		ref float stone,
		ref float metal )
	{
		var woodMul = 1f;
		var stoneMul = 1f;
		var metalMul = 1f;

		switch ( settlement )
		{
			case ThornsWorldSettlementKind.MainCity:
				switch ( cityRing )
				{
					case ThornsWorldCityRing.Core:
						woodMul = 0.12f;
						stoneMul = 0.95f;
						metalMul = 1.75f;
						break;
					case ThornsWorldCityRing.MidRing:
						woodMul = 0.32f;
						stoneMul = 1.15f;
						metalMul = 1.35f;
						break;
					case ThornsWorldCityRing.OuterRing:
						woodMul = 0.62f;
						stoneMul = 1.22f;
						metalMul = 1.12f;
						break;
					default:
						woodMul = 0.45f;
						stoneMul = 1.1f;
						metalMul = 1.2f;
						break;
				}

				break;
			case ThornsWorldSettlementKind.Town:
				woodMul = 1.55f;
				stoneMul = 1.08f;
				metalMul = 0.12f;
				break;
			default:
				woodMul = 1.65f;
				stoneMul = 0.82f;
				metalMul = 0.06f;
				break;
		}

		wood *= woodMul;
		stone *= stoneMul;
		metal *= metalMul;
	}

	static ThornsBuildingMaterialTier SampleTier( Random rnd, float wood, float stone, float metal )
	{
		var total = Math.Max( 0f, wood ) + Math.Max( 0f, stone ) + Math.Max( 0f, metal );
		if ( total <= 0.001f )
			return ThornsBuildingMaterialTier.Stone;

		var roll = (float)rnd.NextDouble() * total;
		if ( ( roll -= Math.Max( 0f, wood ) ) <= 0f )
			return ThornsBuildingMaterialTier.Wood;
		if ( ( roll -= Math.Max( 0f, stone ) ) <= 0f )
			return ThornsBuildingMaterialTier.Stone;
		return ThornsBuildingMaterialTier.Metal;
	}

	public static string MaterialTierKey( ThornsBuildingMaterialTier tier ) =>
		tier switch
		{
			ThornsBuildingMaterialTier.Stone => "stone",
			ThornsBuildingMaterialTier.Metal => "metal",
			_ => "wood"
		};
}
