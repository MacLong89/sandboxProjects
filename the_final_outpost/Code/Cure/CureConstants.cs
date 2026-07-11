namespace FinalOutpost;

public enum CureSeason
{
	Spring = 0,
	Summer = 1,
	Fall = 2,
	Winter = 3
}

public static class CureConstants
{
	public const int DaysPerSeason = 28;
	public const float RealSecondsPerDay = 45f;

	public const float SpringThreatInterval = 120f;
	public const float SummerThreatInterval = 100f;
	public const float FallThreatInterval = 80f;
	public const float WinterThreatInterval = 55f;

	public const float SpringThreatMult = 0.7f;
	public const float SummerThreatMult = 0.85f;
	public const float FallThreatMult = 1f;
	public const float WinterThreatMult = 1.35f;

	public const int BaseThreatZombies = 10;
	public const float ThreatZombiesPerYear = 4f;
	public const float ThreatZombiesPerSeason = 2f;

	public const float SicknessPerDaySpring = 0.15f;
	public const float SicknessPerDaySummer = 0.2f;
	public const float SicknessPerDayFall = 0.35f;
	public const float SicknessPerDayWinter = 0.5f;
	public const float SicknessAfterThreatDrop = 8f;
	public const float MaxSickness = 100f;
	public const float SicknessWorkerPenalty = 0.004f;

	public const double LabPointsPerSeasonBase = 12;
	public const double LabPointsPerLevel = 6;
	public const double LabPointsPerCraftsman = 4;
	public const double LabPointsPerScholar = 4;

	// Civic building output (per building, per second)
	public const float FarmFoodPerSec = 1.5f;
	public const float FactorySuppliesPerSec = 1.0f;
	public const float FactoryFoodPerSec = 0.35f;
	public const float LibraryKnowledgePerSec = 0.75f;
	public const float SchoolKnowledgePerSec = 0.45f;
	public const float HospitalRecruitHealPerSec = 2.5f;
	public const float HospitalSicknessHealPerSec = 0.02f;
	public const float HospitalHealRadius = 220f;
	public const float ShopScrapPerSec = 0.3f;
	/// <summary>Baseline command-post scrap drip (Cure mode only).</summary>
	public const float CommandPostScrapPerSec = 5f;

	// Colony worker output (per worker, per second)
	public const float FarmerFoodPerSec = 0.9f;
	public const float ScholarKnowledgePerSec = 0.45f;
	public const float OperatorSuppliesPerSec = 0.55f;
	public const float MedicRecruitHealPerSec = 2.0f;
	public const float MedicHealRadius = 180f;
	public const float MerchantScrapPerSec = 0.22f;

	public const int ResearchTierCount = 4;

	public static string SeasonName( int season ) => ((CureSeason)(season % 4)) switch
	{
		CureSeason.Spring => "Spring",
		CureSeason.Summer => "Summer",
		CureSeason.Fall => "Fall",
		CureSeason.Winter => "Winter",
		_ => "Spring"
	};

	public static float ThreatInterval( int season ) => ((CureSeason)(season % 4)) switch
	{
		CureSeason.Spring => SpringThreatInterval,
		CureSeason.Summer => SummerThreatInterval,
		CureSeason.Fall => FallThreatInterval,
		CureSeason.Winter => WinterThreatInterval,
		_ => SpringThreatInterval
	};

	public static float ThreatMult( int season ) => ((CureSeason)(season % 4)) switch
	{
		CureSeason.Spring => SpringThreatMult,
		CureSeason.Summer => SummerThreatMult,
		CureSeason.Fall => FallThreatMult,
		CureSeason.Winter => WinterThreatMult,
		_ => SpringThreatMult
	};

	public static float SicknessPerDay( int season ) => ((CureSeason)(season % 4)) switch
	{
		CureSeason.Spring => SicknessPerDaySpring,
		CureSeason.Summer => SicknessPerDaySummer,
		CureSeason.Fall => SicknessPerDayFall,
		CureSeason.Winter => SicknessPerDayWinter,
		_ => SicknessPerDaySpring
	};

	public static int ZombiesForThreat( SaveData save )
	{
		var baseCount = BaseThreatZombies
			+ (int)((save.CurrentYear - 1) * ThreatZombiesPerYear)
			+ save.CurrentSeason * (int)ThreatZombiesPerSeason;
		return Math.Max( 6, (int)(baseCount * ThreatMult( save.CurrentSeason )) );
	}

	public static int ProgressSeason( SaveData save ) =>
		(save.CurrentYear - 1) * 4 + save.CurrentSeason + 1;
}
