namespace UnderPressure;

/// <summary>
/// Central tuning for the whole game. Keeping every magic number here makes the
/// retention/economy curve easy to iterate without hunting through systems.
/// </summary>
public static class GameConstants
{
	// --- Persistence ---
	public const string SaveFile = "under_pressure/save.json";
	public const float AutosaveInterval = 20f;
	public const string PackageIdent = "maclgames.under_pressure";

	// --- Economy ---
	/// <summary>Base cash awarded per reference-area of dirt cleaned (before multipliers).</summary>
	public const double BaseCellValue = 1.0;

	/// <summary>Dirt-cell area (units²) that <see cref="BaseCellValue"/> is priced against.
	/// Payouts scale by (cell area / this), so changing tile size doesn't change job value.</summary>
	public const float ReferenceCellArea = 24f * 24f;
	/// <summary>Job completion bonus = job value * cell value * this.</summary>
	public const double CompletionBonusFactor = 0.75;
	/// <summary>Fraction of a job that must be cleaned before the player can leave (return to van).</summary>
	public const float JobCompleteThreshold = 0.99f;
	/// <summary>Extra one-time bonus for mopping a job all the way to a spotless 100% = job value * cell value * this.</summary>
	public const double PerfectBonusFactor = 0.5;

	// --- World map ---
	public const float DefaultMapSize = 7200f;
	public const float MapTransitionWidth = 520f;
	public const float HorizonDistanceFactor = 0.46f;

	// --- Daily reward ---
	public const double DailyBaseReward = 250;
	public const double DailyPerStreak = 150;
	public const int DailyMaxStreakReward = 10;

	// --- Prestige ---
	public const double PrestigeBaseRequirement = 6000;
	public const double PrestigeRequirementGrowth = 4.0;
	public const double PrestigeMultiplierPerLevel = 0.25;

	// --- Player ---
	public const float WalkSpeedBase = 150f;
	public const float WalkSpeedPerLevel = 18f;
	public const float RunMultiplier = 1.6f;
	public const float EyeHeight = 64f;
	public const float FieldOfView = 80f;

	// --- Spray tool ---
	public const float SprayRange = 260f;
	/// <summary>Extra pressure-washer reach per Reach upgrade level.</summary>
	public const float SprayRangePerLevel = 70f;
	/// <summary>How much clean-progress (0..1) a cell gains per second at the center of the jet, level 0.</summary>
	public const float CleanPowerBase = 1.6f;
	public const float CleanPowerPerLevel = 0.4f;
	public const float NozzleRadiusBase = 14f;
	public const float NozzleRadiusPerLevel = 2.4f;

	/// <summary>Level 1 (first job) — no pests, slightly stronger starter washer.</summary>
	public const int Level1JobIndex = 0;
	public const float Level1WasherPowerMultiplier = 1.2f;
	public const float Level1WasherRadiusMultiplier = 1.2f;

	// --- Hand tools (scrub brush & squeegee) ---
	// Base power/radius/range come from each tool's ToolDef; these are the per-upgrade-level gains.
	public const float BrushPowerPerLevel = 0.7f;
	public const float BrushRadiusPerLevel = 2.5f;
	public const float BrushRangePerLevel = 30f;
	public const float SqueegeePowerPerLevel = 0.8f;
	public const float SqueegeeRadiusPerLevel = 3f;
	public const float SqueegeeRangePerLevel = 34f;

	// --- Water tank (pressure washer) ---
	public const float TankBase = 100f;
	public const float TankPerLevel = 60f;
	public const float TankDrainPerSecond = 22f;
	public const float TankRefillPerSecond = 34f;
	public const float TankRefillDelay = 0.6f;

	// --- Stamina (hand tools: scrub brush & squeegee) ---
	// Elbow grease is finite: scrubbing/wiping drains stamina, which recovers when you rest.
	// "Energy Drinks" upgrade raises the max.
	public const float StaminaBase = 100f;
	public const float StaminaPerLevel = 55f;
	public const float StaminaDrainPerSecond = 30f;
	public const float StaminaRefillPerSecond = 42f;
	public const float StaminaRefillDelay = 0.5f;

	// --- Hitman contract arc ---
	/// <summary>Job index (0-based) where the fixer NPC briefs the player. Disabled for new campaign.</summary>
	public const int HitmanBriefingJobIndex = -1;

	/// <summary>Flat distance at which the fixer stops approaching and opens dialogue.</summary>
	public const float FixerBriefingStopDistance = 56f;

	/// <summary>Uniform height for every citizen humanoid (fixer NPC + humanoid pests).</summary>
	public const float CitizenHeightScale = 1.02f;

	/// <summary>Where the fixer spawns relative to the job spawn (ahead of the player).</summary>
	public static readonly Vector3 FixerBriefingSpawnOffset = new( 0f, 100f, 0f );

	/// <summary>Job index where contract-style targets appear (legacy — campaign uses per-job spawns).</summary>
	public const int HitmanContractJobIndex = 24;

	public const float GunRange = 420f;
	public const float GunPower = 28f;
	public const float GunRadius = 6f;
	public const float BloodSplatterRadius = 58f;

	public static readonly Color BloodColor = new( 0.58f, 0.04f, 0.04f );
	/// <summary>Job index (0-based) at which pests start chasing and harassing the player.</summary>
	public const int PestAttackUnlockJob = 2;
	/// <summary>Per-job difficulty bump applied to pest health and attack strength.</summary>
	public const float PestDifficultyPerJob = 0.18f;
	/// <summary>How long the HUD flashes a harassment alert.</summary>
	public const float HarassmentAlertDuration = 2.2f;
	/// <summary>Pest hits before the player passes out on the job site.</summary>
	public const int PestHitsUntilKnockout = 3;
	/// <summary>Seconds the screen stays dark while the player is out cold.</summary>
	public const float KnockoutBlackoutDuration = 2.8f;
	/// <summary>Fraction of current cash lost when waking up from a pest swarm.</summary>
	public const double KnockoutPenaltyPercent = 0.10;
	public const double KnockoutPenaltyMin = 35;
	public const float KnockoutPenaltyMax = 800;

	// --- Van departure transition ---
	public const float DepartFadeToBlack = 1.1f;
	/// <summary>Length of van.mp3 before the next job briefing (tune to match your clip).</summary>
	public const float DepartVanSoundLength = 3.8f;
	public const float DepartVanSoundFadeOut = 1.15f;
	public const float DepartFadeFromBlack = 0.95f;

	// --- Crew passive income (ticks only while the game is open) ---
	// Priced as a serious late-game investment; income scaled to keep a sensible payback.
	public const double AutoIncomePerLevel = 9.0;

	public static string FormatCash( double amount )
	{
		if ( amount >= 1_000_000_000 ) return $"${amount / 1_000_000_000:0.##}B";
		if ( amount >= 1_000_000 ) return $"${amount / 1_000_000:0.##}M";
		if ( amount >= 10_000 ) return $"${amount / 1_000:0.#}k";
		return "$" + amount.ToString( "N0" );
	}
}
