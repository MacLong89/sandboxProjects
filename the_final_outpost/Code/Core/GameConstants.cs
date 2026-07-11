namespace FinalOutpost;

/// <summary>Central tuning for the outpost defense loop.</summary>
public static class GameConstants
{
	public const string SaveFile = "the_final_outpost/save.json";
	public const string ProfileFile = "the_final_outpost/profile.json";
	public const string SurvivalSaveFile = "the_final_outpost/survival.json";
	public const string CureSaveFile = "the_final_outpost/cure.json";
	public const string LeaderboardStat = "nights_survived";
	public const float AutosaveInterval = 20f;

	// --- Audio defaults ---
	public const float DefaultAudioVolume = 0.25f;
	public const float DefaultAmbienceVolume = DefaultAudioVolume;
	public const float DefaultMusicVolume = DefaultAudioVolume;
	/// <summary>Extra attenuation for loud weapon SFX (shotgun + turret).</summary>
	public const float ShotgunVolumeScale = 0.5f;
	/// <summary>Extra attenuation for zombies striking walls/buildings/recruits.</summary>
	public const float ZombieImpactVolumeScale = 0.5f;
	/// <summary>Set false to restore pre-director combat audio (see Combat/COMBAT_AUDIO_BASELINE.md).</summary>
	public static bool UseCombatAudioDirector = true;
	/// <summary>
	/// When true, night plays looping combat.mp3 + combat2.mp3 and blocks all other sounds during Night.
	/// Set false to revert (see Core/NIGHT_COMBAT_MUSIC.md).
	/// </summary>
	public static bool UseNightCombatMusicLoop = true;
	public const float NightCombatTrackVolume = 0.5f;
	/// <summary>MusicPlayer lacks the decibel boost that .sound SFX get — scale loops so slider defaults are audible.</summary>
	public const float LoopMusicGain = 3.5f;

	// --- Combat audio director (only when UseCombatAudioDirector is true) ---
	public const int CombatMaxGunfireVoices = 5;
	public const float CombatGunfireMinInterval = 0.035f;
	public const float CombatGunfireVoiceDuration = 0.1f;
	public const float CombatGunfireVolume = 0.78f;
	public const float CombatGunfirePitchMin = 0.94f;
	public const float CombatGunfirePitchMax = 1.06f;

	public const int CombatMaxImpactVoices = 3;
	public const int CombatMaxHighImpactVoices = 2;
	public const float CombatImpactMinInterval = 0.05f;
	public const float CombatImpactTargetCooldown = 0.4f;
	public const float CombatHighImpactTargetCooldown = 0.2f;
	public const float CombatCoreImpactMinInterval = 0.15f;
	public const float CombatImpactVoiceDuration = 0.14f;
	public const float CombatImpactPitchMin = 0.9f;
	public const float CombatImpactPitchMax = 1.08f;
	public const float CombatWallImpactVolume = 0.42f;
	public const float CombatBuildingImpactVolume = 0.46f;
	public const float CombatRecruitHitVolume = 0.5f;
	public const float CombatCoreImpactVolume = 0.72f;
	public const float CombatBomberImpactVolume = 0.8f;
	public const float CombatRecruitKillVolume = 0.85f;

	public const int CombatMaxKillVoices = 4;
	public const float CombatKillVoiceDuration = 0.12f;
	public const float CombatKillVolume = 0.88f;
	public const float CombatKillPitchMin = 0.92f;
	public const float CombatKillPitchMax = 1.1f;
	/// <summary>Legacy alias — use <see cref="DefaultAmbienceVolume"/> for new code.</summary>
	public const float AmbienceVolume = DefaultAmbienceVolume;

	// --- Outpost layout (XY ground, Z up) ---
	public const float ArenaHalf = 420f;
	public const float WallThickness = 28f;
	public const float WallHeight = 110f;
	public const int SegmentsPerSide = 5;

	// --- World / terrain (much larger than the base so plots can surround it) ---
	// Half-extent 3250 ≈ √2 × 2300 → twice the land surface area of the old map.
	public const float TerrainHalfExtent = 3250f;
	public const float TerrainCellSize = 80f;
	public const float TerrainAmplitude = 26f;
	/// <summary>Flat ocean plane sits at this world Z; terrain dips toward it only at the far rim.</summary>
	public const float SeaLevel = -14f;
	/// <summary>How far past the terrain edge the water quad extends (moves with terrain size).</summary>
	public const float SeaSheetOvershoot = 500f;
	/// <summary>Width of the outer ring where terrain slopes down to meet the sea.</summary>
	public const float SeaRimBlendWidth = 220f;

	// --- Plots (large parcels surrounding the home base) ---
	// One plot equals the base footprint; the centre plot (0,0) is the home base.
	public const float PlotSize = ArenaHalf * 2f;      // 840
	public const int PlotGridRadius = 2;               // (2R+1)^2 plots total
	public const double PlotBuyBaseCost = 180;
	public const double PlotBuyCostPerRing = 1.85;

	// --- Enemy spawning ---
	// Enemies appear on a square ring this far beyond the claimed territory edge.
	public const float SpawnMargin = 170f;

	// --- Workers (non-combat: forager / craftsman / repairman) ---
	public const int MaxWorkers = 10;
	public const double WorkerHireCost = 90;
	public const float WorkerMoveSpeed = 105f;
	public const float ForagerHarvestPerSec = 0.55f;      // resource units / sec
	public const float PlotClearSeconds = 55f;            // forager-seconds to fully clear a plot for building
	public const float CraftsmanConvertPerSec = 0.45f;    // resource units consumed / sec
	public const double CraftsmanScrapPerResource = 3.5;    // scrap produced per resource unit

	// --- Build grid ---
	public const float CellSize = 60f;
	public const int GridCellsPerSide = 12;
	public const float GridOrigin = -(GridCellsPerSide * CellSize) * 0.5f;

	// --- Command post ---
	public const float CoreBaseHp = 500f;
	public const float CoreHpPerFortify = 120f;
	public const float CoreSize = 90f;

	// --- Tower combat ---
	public const float TurretBaseDamage = 6f;
	public const float TurretDamagePerLevel = 3f;
	public const float TurretBaseRange = 380f;
	public const float TurretRangePerLevel = 35f;
	public const float TurretFireInterval = 0.35f;

	// --- Defender recruits ---
	public const int RecruitsPerBarracks = 3;
	public const int MaxDefenders = 12;
	public const double DefenderRecruitCost = 75;
	public const double DefenderTrainCost = 120;
	public const float DefenderBaseDamage = 8f;
	public const float DefenderDamagePerTrain = 2.5f;
	public const float DefenderFireInterval = 0.5f;
	public const float DefenderRange = 320f;
	public const float DefenderMoveSpeed = 130f;
	public const float DefenderAcquireRange = 2200f;
	public const float DefenderHomeDeadzone = 14f;

	// --- Camera ---
	public const float CameraFov = 55f;
	public const float CameraPanSpeed = 520f;
	public const float CameraZoomSpeed = 80f;
	public const float CameraMinDistance = 400f;
	public const float CameraMaxDistance = 3200f;
	public const float CameraRotateSpeed = 90f;

	// --- Bullets ---
	public const float BulletSpeed = 3600f;
	public const float BulletLife = 0.7f;

	// --- Zombies ---
	public const float ZombieBaseHp = 8.25f;
	public const float ZombieHpPerNight = 2.5f;
	/// <summary>Extra HP scaling that kicks in after night 2.</summary>
	public const int ZombieRampFromNight = 3;
	public const float ZombieExtraHpPerNight = 2.8f;
	public const float ZombieExtraHpMultPerNight = 0.11f;
	public const float ZombieBaseSpeed = 95f;
	public const float ZombieSpeedPerNight = 1.5f;
	public const float ZombieBaseDamage = 8f;
	public const float ZombieDamagePerNight = 0.6f;
	public const float ZombieRadius = 32f;
	/// <summary>XY pathing radius for humanoids (smaller than <see cref="ZombieRadius"/> hit volume).</summary>
	public const float UnitCollisionRadius = 10f;
	/// <summary>Movement blockers use a tighter slice of building visuals so pathing matches what you see.</summary>
	public const float BuildingCollisionScale = 0.82f;
	public const float CommandPostCollisionScale = 0.85f;
	/// <summary>When pathing stalls this close to melee, start attacking anyway.</summary>
	public const float ZombieStuckEngageSlack = 24f;
	public const float ZombieAttackInterval = 0.9f;
	public const float ZombieMeleeRange = 70f;
	public const float ZombieEngageExitBuffer = 18f;
	public const float ZombieSeekRadius = 2800f;
	public const float ZombieSpawnRing = 80f;

	// --- Recruits ---
	public const float RecruitMaxHealth = 120f;
	public const float BarracksHealRadius = 420f;
	public const float BarracksHealPerSec = 14f;
	public const double ScrapPerKillBase = 2;
	public const double ScrapPerKillPerNight = 0.11;

	// --- Nights ---
	public const int ZombiesBase = 12;
	public const float ZombiesPerNight = 5.3f;
	/// <summary>Extra zombies per night beyond <see cref="ZombieRampFromNight"/> - 1.</summary>
	public const float ZombiesExtraPerNight = 5f;
	public const float SpawnStagger = 0.45f;
	public const double NightClearBonusBase = 12.5;
	public const double NightClearBonusPerNight = 2.5;
	/// <summary>One-time scrap paid after surviving night 1 — funds day-2 tutorial buys (barracks, then recruits).</summary>
	public const double FirstNightSurvivalBonus = 200;
	/// <summary>One-time scrap paid after surviving night 5 — intended to fund claiming an adjacent plot.</summary>
	public const double FifthNightPlotBonus = 200;
	/// <summary>Max defense towers + barracks combined on a single plot (walls excluded).</summary>
	public const int MaxPlotStructures = 6;

	// --- Scout expeditions ---
	public const int ExpeditionMinParty = 1;
	public const int ExpeditionMaxParty = 6;
	public const float ExpeditionShortSeconds = 45f;
	public const float ExpeditionLongSeconds = 130f;
	public const double ExpeditionProvisionPerScout = 12;   // scrap paid up front per scout
	public const double ExpeditionScrapPerScoutShort = 22.5;
	public const double ExpeditionScrapPerScoutLong = 60;
	public const double ExpeditionResourcePerScoutLong = 5; // raw resources returned per scout (long only)
	public const float ExpeditionRareBonusChance = 0.16f;   // "big find" multiplier roll
	public const float ExpeditionRareBonusMult = 2.4f;

	public const double ExpeditionLongLossChance = 0.12;    // per committed unit on a long expedition
	public const double ExpeditionLootBoostPerCivilian = 0.12; // civilians boost the resource haul
	public const double ExpeditionSafetyPerSoldier = 0.015;  // soldiers reduce each unit's loss chance

	// --- Idle / offline progress ---
	public const double OfflineCapSeconds = 8 * 3600;       // reward at most 8h of away time
	public const double OfflineMinSeconds = 60;             // ignore very short gaps

	// --- Daily login bonus ---
	public const double DailyBaseReward = 22.5;
	public const double DailyRewardPerStreak = 15;          // added per consecutive day
	public const int DailyStreakCap = 7;                    // reward stops growing past this

	// --- Kill combo (night) ---
	public const float ComboWindowSeconds = 3.25f;          // time to land the next kill before reset
	public const float ComboAddPerKill = 0.08f;             // multiplier added per chained kill
	public const float ComboMaxMult = 3f;

	// --- Milestones & prestige ---
	public const int PrestigeMinNight = 10;                 // must reach this night to evacuate
	public const int PrestigeNightsPerPoint = 5;            // legacy points earned = reachedNight / this
	public const double LegacyScrapBonusPer = 0.05;         // +5% scrap per legacy point (permanent)

	// --- Economy ---
	public const double StartingScrap = 150;
	/// <summary>Global scale on scrap earned (kills, bonuses, rewards) — not sell refunds.</summary>
	public const double ScrapIncomeMult = 0.8;

	// --- Repairs ---
	public const double RepairCostPerHp = 0.15;
	/// <summary>Repair duration per scrap spent — 100 scrap ≈ 55s, like clearing one plot.</summary>
	public const float RepairSecondsPerScrap = 0.55f;
	public const float MinRepairDuration = 2f;
	/// <summary>Each repairman cuts paid repair job duration by this fraction (stacking).</summary>
	public const float RepairmanTimeReductionPer = 0.30f;
	public const float SellRefundFraction = 0.5f;

	public static float RepairDurationForCost( double scrapCost, int repairmanCount = 0 )
	{
		var duration = Math.Max( MinRepairDuration, (float)(scrapCost * RepairSecondsPerScrap) );
		if ( repairmanCount <= 0 ) return duration;

		var mult = 1f - RepairmanTimeReductionPer * repairmanCount;
		mult = Math.Max( 0.1f, mult );
		return Math.Max( MinRepairDuration, duration * mult );
	}

	public static int MaxRecruitCapacity( int barracksCount ) =>
		Math.Min( MaxDefenders, barracksCount * RecruitsPerBarracks );

	public static float ZombieHp( int night, float typeHpMult )
	{
		var hp = (ZombieBaseHp + night * ZombieHpPerNight) * typeHpMult;
		if ( night >= ZombieRampFromNight )
		{
			var extraNights = night - (ZombieRampFromNight - 1);
			hp += extraNights * ZombieExtraHpPerNight * typeHpMult;
			hp *= 1f + extraNights * ZombieExtraHpMultPerNight;
		}

		return hp;
	}

	public static int ZombiesForNight( int night )
	{
		var count = ZombiesBase + (int)(night * ZombiesPerNight);
		if ( night >= ZombieRampFromNight )
			count += (int)((night - (ZombieRampFromNight - 1)) * ZombiesExtraPerNight);
		return count;
	}

	public static string FormatScrap( double amount )
	{
		if ( amount >= 1_000_000 ) return $"{amount / 1_000_000:0.##}M scrap";
		if ( amount >= 10_000 ) return $"{amount / 1_000:0.#}k scrap";
		return $"{amount:N0} scrap";
	}
}
