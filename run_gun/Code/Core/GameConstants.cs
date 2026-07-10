namespace RunGun;

/// <summary>
/// Central tuning for the whole game. Every magic number lives here so the run feel and
/// economy curve can be iterated without hunting through systems.
/// </summary>
public static class GameConstants
{
	// --- Persistence ---
	public const string SaveFile = "run_gun/save.json";
	public const float AutosaveInterval = 20f;

	// --- World layout ---
	public const float LaneHalf = 170f;
	public const float TrackLength = 200_000f;
	public const float WallHeight = 120f;
	public const float UnitsPerMeter = 40f;

	// --- Player / run ---
	public const float RunSpeed = 330f;
	public const float StrafeSpeedBase = 360f;
	public const float StrafeSpeedPerLevel = 45f;
	public const float PlayerRadius = 26f;
	public const float BodyHeight = 74f;

	// --- Camera rig ---
	public const float CamBack = 300f;
	public const float CamUp = 165f;
	public const float CamYFollow = 0.5f;
	public const float CamLookAhead = 320f;
	public const float CamLookUp = 45f;
	public const float CamFov = 75f;
	public const float CamShakeDecay = 8f;
	public const float CamShakeHurt = 18f;

	// --- Build / gun ---
	public const float BaseDamage = 2f;
	public const int BaseMultishot = 1;
	public const float BaseFireInterval = 0.26f;
	public const float MinFireInterval = 0.05f;
	public const float BulletSpeed = 2600f;
	public const float BulletLife = 0.8f;
	public const int MaxBulletsPerShot = 7;
	public const int MaxPierce = 5;
	public const float CritDamageMult = 2.2f;
	public const float CritChanceCap = 0.65f;

	// --- Squad (the crowd) — your runners ARE your firepower and your life ---
	public const int StartSquad = 5;               // base crew at run start
	public const int SquadPerRecruitLevel = 1;     // "Recruits" meta upgrade (was MaxHealth)
	public const int MaxSquad = 500;
	public const int MaxVisibleSquad = 28;         // perf cap on drawn bodies
	public const int MaxBulletLanes = 22;          // perf cap on bullets fired per volley
	public const float SquadOverflowDamagePer = 0.015f; // each runner past the lane cap adds damage
	public const float SquadLaneSpacing = 22f;     // bullet lane spacing (clamped to lane width)
	public const float SquadRowDepth = 46f;        // spacing between crowd formation rows
	public const int SquadPerRow = 5;
	public const float SquadColSpacing = 34f;
	public const float SquadFollowLerp = 10f;
	public const float SquadPopSpeed = 6f;         // how fast a body scales in/out
	public const float SquadBob = 5f;

	// squad gates — crew is ADD-ONLY now (no xN) so growth stays linear and beatable.
	public const float GateSquadAddMin = 2f;
	public const float GateSquadAddMax = 6f;
	public const float GateSquadAddCap = 30f;      // a single gate can never hand out more than this
	public const float GateSquadAddPump = 0.2f;    // bullets nudge the gate up only slightly
	public const float GateSquadDistanceScale = 0.008f;

	// squad damage — how many runners each threat costs
	public const int SquadContactCostStart = 1;
	public const int SquadContactCostMax = 3;
	public const int SquadBossContactCost = 6;
	public const float HazardSquadLossFraction = 0.28f; // a hazard rips a chunk of the crowd
	public const int HazardSquadLossMin = 3;
	public const int ProjectileSquadCost = 1;

	// --- Gates ---
	public const float GateSpacing = 720f;
	public const float GateWidth = LaneHalf;
	public const float GateHeight = 190f;
	public const float GateAddPumpPerHit = 1f;
	public const float GateMultPumpPerHit = 0.02f;
	public const float GateDamagePump = 0.5f;
	public const float GateFireRatePump = 0.04f;
	public const float GateCritPump = 0.01f;
	public const float GateShieldPump = 4f;
	public const float GateHealPump = 6f;
	public const float GateCoinPump = 0.05f;
	public const float GateDamageCap = 30f;
	public const float GateFireRateCap = 3.5f;
	public const float GateMultishotCap = 4f;
	public const float GatePierceCap = 3f;
	public const float GateCritCap = 0.25f;
	public const float GateShieldCap = 80f;
	public const float GateHealCap = 50f;
	public const float GateCoinCap = 3f;
	public const float GateLabelScale = 2.4f;
	public const float GateAddStartMin = 2f;
	public const float GateAddStartMax = 6f;
	public const float GateAddCap = 400f;
	public const float GateMultCap = 4f;

	// --- Enemies ---
	public const float EnemyBaseHealth = 5f;
	public const float EnemyHealthPerMeter = 1.35f;
	public const float EnemyHealthAccel = 0.0016f;   // super-linear HP growth so damage builds can't coast forever
	public const float EnemyAdvanceSpeed = 60f;
	public const float EnemyContactDamage = 34f;
	public const float EnemyRadius = 40f;

	// Enemy body size grows with health so threats read bigger the deeper you push.
	public const float EnemySizeHealthRef = 300f;    // health at which size growth plateaus
	public const float EnemyMaxSizeBonus = 0.85f;    // up to +85% body scale from health
	public const float EliteSizeMult = 1.3f;
	public const int MaxFormationSize = 11;          // perf ceiling on bodies per pack (citizens are heavy)

	// --- Difficulty ramp (gentle start -> full intensity by DifficultyRampMeters) ---
	public const float DifficultyRampMeters = 500f;
	public const float Tier1Meters = 130f;   // lines + light swarms appear
	public const float Tier2Meters = 300f;   // walls + pincers appear
	public const float Tier3Meters = 480f;   // funnels + heavy mixes appear
	public const float EnemyContactDamageStart = 15f;
	public const float EnemyAdvanceSpeedStartMult = 0.68f;
	public const float PackSpawnChanceStart = 0.45f;

	// --- Hazards (unshootable obstacles you must strafe around) ---
	public const float HazardStartMeters = 140f;
	public const float HazardChanceStart = 0.14f;
	public const float HazardChanceMax = 0.72f;
	public const float HazardRampMeters = 1000f;
	public const float HazardDamage = 32f;
	public const float HazardHeight = 155f;
	public const float HazardMinGap = 120f;   // narrowest guaranteed passable gap
	public const float HazardMaxGap = 240f;   // widest gap early on
	public const double EnemyCoinPerHealth = 0.6;
	public const float EliteSpawnChance = 0.22f;
	public const float EliteHealthMult = 2.2f;
	public const float EliteCoinMult = 2.5f;
	public const float RusherSpeedMult = 1.8f;
	public const float TankSpeedMult = 0.45f;
	public const float TankHealthMult = 2.5f;
	public const float SwarmHealthMult = 0.28f;
	public const float ShieldedFrontArmor = 0.65f;
	public const float SpitterRange = 900f;
	public const float SpitterCooldown = 2.2f;
	public const float ProjectileSpeed = 420f;
	public const float ProjectileDamage = 22f;

	// --- Boss ---
	public const float BossIntervalMeters = 600f;
	public const float BossHealthMult = 18f;
	public const float BossCoinMult = 8f;
	public const float BossAttackInterval = 3.5f;

	// --- Section pacing ---
	public const float SectionCycleMeters = 240f;
	public const int BiomeCount = 4;

	// --- Combo / overdrive ---
	public const float ComboMultPerKill = 0.04f;
	public const float ComboMaxMult = 2.5f;
	public const float ComboDecayTime = 2.5f;
	public const float OverdriveMax = 100f;
	public const float OverdriveCost = 100f;
	public const float OverdriveDuration = 4f;
	public const float OverdrivePerKill = 8f;
	public const float OverdrivePerGate = 12f;
	public const float OverdrivePerGateHit = 2f;
	public const float OverdriveFireMult = 0.45f;
	public const float OverdriveBuildMult = 1.35f;
	public const float OverdriveCoinMult = 1.5f;

	// --- Scoring / milestones ---
	public const float ScorePerMeter = 2f;
	public const float ScorePerKill = 25f;
	public const float MilestoneIntervalMeters = 100f;

	// --- Difficulty pacing ---
	public const float ContentStartX = 900f;
	public const float SpawnAhead = 2600f;
	public const float DespawnBehind = 700f;

	// --- Health ---
	public const float HealthBase = 100f;
	public const float HealthPerLevel = 25f;

	// --- Economy ---
	public const double DistanceCoinPerMeter = 1.4;
	public const double CoinMultPerLevel = 0.2;

	// --- Meta upgrade curve ---
	public const float StartDamagePerLevel = 0.8f;
	public const float DamageMultPerLevel = 0.15f;
	public const float FireRateMultPerLevel = 0.08f;
	public const float CritPerLevel = 0.012f;
	public const float PiercePerLevel = 0.15f;
	public const float LifestealPerLevel = 0.008f;
	public const float OverdriveChargePerLevel = 0.04f;
	public const float GateLuckPerLevel = 0.03f;
	public const float PrestigeCoinMultPerLevel = 0.1f;

	public static string FormatNumber( double amount )
	{
		if ( amount >= 1_000_000_000 ) return $"{amount / 1_000_000_000:0.##}B";
		if ( amount >= 1_000_000 ) return $"{amount / 1_000_000:0.##}M";
		if ( amount >= 10_000 ) return $"{amount / 1_000:0.#}k";
		return amount.ToString( "N0" );
	}

	public static string FormatCash( double amount ) => "$" + FormatNumber( amount );
}
