namespace Sandbox;



/// <summary>Phase 7 — blueprint compilation and strict layout selection for settlement placement.</summary>

public sealed class ThornsWorldGenBuildingLayoutFactory

{

	readonly Random _rnd;



	public ThornsWorldGenBuildingLayoutFactory( Random placementRng ) => _rnd = placementRng;



	/// <summary>Resolves a structurally valid layout or returns false to reject the building slot.</summary>

	public bool TryCreateForSettlement(

		ThornsProcBuildingType buildingType,

		ThornsProcBuildingDistrict district,

		ThornsWorldSettlementKind settlementKind,

		ThornsWorldCityRing? cityRing,

		out ThornsWorldGenBuildingLayoutResult result,

		float? maxHalfW = null,

		float? maxHalfD = null )

	{

		result = default;

		var maxTries = maxHalfW.HasValue ? 8 : 14;
		for ( var t = 0; t < maxTries; t++ )
		{
			if ( !ThornsProcBuildingIdentityGenerator.TryGenerateForSettlement(
				     buildingType,
				     district,
				     _rnd,
				     out var layout ) )
				continue;

			layout.GetFootprintHalfExtents( out var halfW, out var halfD );
			if ( maxHalfW.HasValue
			     && ( halfW > maxHalfW.Value || halfD > ( maxHalfD ?? maxHalfW.Value ) ) )
				continue;



			var tierType = layout.Identity?.Type ?? buildingType;
			var materialSlug = ThornsProcBuildingMaterialAffinity.PickMaterialSlug(
				tierType,
				_rnd,
				settlementKind,
				cityRing );
			var tier = (int)ThornsProcBuildingMaterialPalette.DurabilityTierFromSlug( materialSlug );
			var destroyed = layout.Identity?.IsRuinVariant == true
			                || ( buildingType == ThornsProcBuildingType.Ruin && _rnd.NextDouble() < 0.08 );

			result = new ThornsWorldGenBuildingLayoutResult( layout, tier, destroyed, halfW, halfD, materialSlug );
			return true;
		}

		return false;
	}

	/// <summary>Organic scatter: compact landmark blueprints so tall buildings fit between neighbors.</summary>
	public bool TryCreateForOrganic(
		ThornsProcBuildingType buildingType,
		ThornsProcBuildingDistrict district,
		ThornsWorldSettlementKind settlementKind,
		out ThornsWorldGenBuildingLayoutResult result )
	{
		result = default;
		var useCompact = ThornsProcBuildingIdentityRegistry.IsVerticalLandmark( buildingType );
		var maxTries = useCompact ? 12 : 14;
		for ( var t = 0; t < maxTries; t++ )
		{
			if ( !ThornsProcBuildingIdentityGenerator.TryGenerateForSettlement(
				     buildingType,
				     district,
				     _rnd,
				     out var layout,
				     organicCompactFootprint: useCompact ) )
				continue;

			layout.GetFootprintHalfExtents( out var halfW, out var halfD );
			var tierType = layout.Identity?.Type ?? buildingType;
			var materialSlug = ThornsProcBuildingMaterialAffinity.PickMaterialSlug(
				tierType,
				_rnd,
				settlementKind,
				cityRing: null );
			var tier = (int)ThornsProcBuildingMaterialPalette.DurabilityTierFromSlug( materialSlug );
			var destroyed = layout.Identity?.IsRuinVariant == true
			                || ( buildingType == ThornsProcBuildingType.Ruin && _rnd.NextDouble() < 0.08 );

			result = new ThornsWorldGenBuildingLayoutResult( layout, tier, destroyed, halfW, halfD, materialSlug );
			return true;
		}

		return false;
	}

}



public readonly struct ThornsWorldGenBuildingLayoutResult

{

	public readonly ThornsProcBuildingLayout Layout;

	public readonly int Tier;

	public readonly bool Destroyed;

	public readonly float HalfW;

	public readonly float HalfD;

	public readonly string MaterialSlug;



	public ThornsWorldGenBuildingLayoutResult(

		ThornsProcBuildingLayout layout,

		int tier,

		bool destroyed,

		float halfW,

		float halfD,

		string materialSlug )

	{

		Layout = layout;

		Tier = tier;

		Destroyed = destroyed;

		HalfW = halfW;

		HalfD = halfD;

		MaterialSlug = materialSlug ?? ThornsProcBuildingMaterialPalette.AllSlugs[0];

	}

}


