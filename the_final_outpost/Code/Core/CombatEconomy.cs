namespace FinalOutpost;

/// <summary>Derives scrap costs from combat output (DPS) plus small range / mobility premiums.</summary>
public static class CombatEconomy
{
	/// <summary>Baseline range used when pricing range bonuses (gun tower / pistol band).</summary>
	public const float ReferenceRange = 320f;

	public const double ScrapPerDps = 2.4;
	public const double RangePremiumPerUnit = 0.12;
	public const double TowerOverhead = 15;
	public const double RecruitOverhead = 10;
	/// <summary>Flat premium for mobile defenders that reposition during a wave.</summary>
	public const double RecruitManeuverPremium = 30;
	public const double TrainCostPerDpsGain = 7;
	public const double ScrapPerStructureHp = 0.35;
	public const double BarracksOverhead = 120;
	/// <summary>Each barracks already built multiplies the next purchase by this factor.</summary>
	public const double BarracksCostScalePerOwned = 1.6;

	public static float Dps( float damagePerShot, float fireInterval, int pellets = 1 ) =>
		fireInterval > 0f ? damagePerShot * pellets / fireInterval : 0f;

	public static double RangePremium( float range ) =>
		Math.Max( 0f, range - ReferenceRange ) * RangePremiumPerUnit;

	public static double RoundCost( double raw ) => Math.Max( 5, Math.Round( raw / 5 ) * 5 );

	public static double TowerPlaceCost( float dps, float range ) =>
		RoundCost( TowerOverhead + dps * ScrapPerDps + RangePremium( range ) );

	public static double RecruitPlaceCost( float dps, float range ) =>
		RoundCost( RecruitOverhead + RecruitManeuverPremium + dps * ScrapPerDps + RangePremium( range ) );

	public static double TrainCost( float dpsGainPerLevel ) =>
		RoundCost( dpsGainPerLevel * TrainCostPerDpsGain );

	public static double WallPlaceCost( float maxHp ) =>
		RoundCost( maxHp * ScrapPerStructureHp );

	public static double BarracksPlaceCost( float maxHp, int ownedBarracks = 0 )
	{
		var baseCost = BarracksOverhead + maxHp * ScrapPerStructureHp * 0.4;
		var scale = ownedBarracks <= 0 ? 1.0 : Math.Pow( BarracksCostScalePerOwned, ownedBarracks );
		return RoundCost( baseCost * scale );
	}
}
