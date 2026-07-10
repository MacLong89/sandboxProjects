namespace Sandbox;

/// <summary>
/// Design defaults for Thorns skill trees — costs use <see cref="NextPurchaseUpgradePointCost"/> (half of base×(1+rank), min 1).
/// </summary>
public static class ThornsUpgradeBalance
{
	public const int RankCapDefault = 10;
	public const int RankCapTechnician = 12;

	public const int DefaultCostPersistence = 2;
	public const int DefaultCostInstinct = 3;
	public const int DefaultCostIndustry = 3;

	public const float HarvestYieldBonusPerLumberjackRank = 0.10f;
	public const float HarvestYieldBonusPerMinerRank = 0.10f;

	public const float HydrationThirstDrainReductionPerRank = 0.035f;
	public const float HydrationLiquidRestoreBonusPerRank = 0.06f;

	public const float IronGutHungerDrainReductionPerRank = 0.035f;
	public const float IronGutFoodRestoreBonusPerRank = 0.06f;

	public const float StrongStomachPoisonTakenReductionPerRank = 0.07f;

	/// <summary>Environmental / starvation / poison-over-time damage multiplier reduction per Weathered rank.</summary>
	public const float WeatheredEnvironmentalDamageReductionPerRank = 0.045f;

	public const float ThickHideWildlifeDamageReductionPerRank = 0.04f;
	public const float HardenedHumanNpcDamageReductionPerRank = 0.04f;

	public const float EnduranceStaminaMaxBonusPerRank = 8f;
	public const float EnduranceStaminaDrainReductionPerRank = 0.03f;

	/// <summary>Effective detection radius multiplier reduction vs wildlife (player appears closer).</summary>
	public const float GhostDetectionRadiusShrinkPerRank = 0.035f;

	public const float TamingThresholdBaseHpFraction = 0.10f;
	public const float TamingThresholdBonusPerRank = 0.05f;
	public const float TamingThresholdCapHpFraction = 0.85f;

	public const float LuckyChamberFreeShotChancePerRank = 0.018f;

	public const float ScavengerExtraLootChancePerRank = 0.055f;

	public const float ReinforcedDurabilityLossReductionPerRank = 0.06f;

	public const int CraftingTierBaseline = 1;

	public static int RankCapFor( ThornsUpgradeCategory c ) =>
		c == ThornsUpgradeCategory.Technician ? RankCapTechnician : RankCapDefault;

	public static int DefaultTreeCostBase( ThornsUpgradeCategory c )
	{
		if ( c <= ThornsUpgradeCategory.ThickHide )
			return DefaultCostPersistence;
		if ( c <= ThornsUpgradeCategory.LuckyChamber )
			return DefaultCostInstinct;
		return DefaultCostIndustry;
	}

	public static int NextPurchaseUpgradePointCost( int baseCost, int currentRankBeforePurchase )
	{
		var c = baseCost * (1 + Math.Max( 0, currentRankBeforePurchase ));
		// Global tuning: half of the authored curve (ceil at 0.5), min 1 — host spend + skills UI both use this.
		var halved = (c + 1) / 2;
		return Math.Max( 1, halved );
	}
}
