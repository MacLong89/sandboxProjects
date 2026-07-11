namespace FinalOutpost;

public sealed class SavedBuilding
{
	public string Type { get; set; }
	public int CellX { get; set; }
	public int CellY { get; set; }
	public int Level { get; set; } = 1;
	public float Health { get; set; }
	/// <summary>Stable placement order — used to assign recruits to barracks in build order.</summary>
	public int PlaceOrder { get; set; }
}

public sealed class SavedWorker
{
	public string Role { get; set; }
	// Assigned plot for foragers; (int.MinValue) means unassigned.
	public int PlotX { get; set; } = int.MinValue;
	public int PlotY { get; set; } = int.MinValue;

	public bool HasPlot => PlotX != int.MinValue && PlotY != int.MinValue;
}

public sealed class SavedRunRecord
{
	public int Nights { get; set; }
	public long CompletedUnix { get; set; }
}

public sealed class SaveData
{
	public int Version { get; set; } = 17;

	public double Scrap { get; set; } = GameConstants.StartingScrap;
	public double LifetimeEarned { get; set; }

	public int CurrentNight { get; set; } = 1;
	public int BestNight { get; set; }

	// Legacy fields for migration.
	public int CurrentWave { get; set; }
	public int BestWave { get; set; }

	public Dictionary<string, int> Upgrades { get; set; } = new();
	public List<SavedBuilding> Buildings { get; set; } = new();

	// Legacy single-type recruit fields (kept for v2 -> v3 migration).
	public int DefenderCount { get; set; }
	public int DefenderTrainLevel { get; set; }

	// v3: recruits are per gun type. One entry per recruit (weapon type name).
	public List<string> Recruits { get; set; } = new();
	// v7: parallel recruit HP (same count/order as Recruits).
	public List<float> RecruitHealth { get; set; } = new();
	// Per gun-type training level (weapon type name -> level).
	public Dictionary<string, int> RecruitTrainLevels { get; set; } = new();

	// v4: territory expansion + non-combat economy.
	// Owned plot keys ("x,y"). The home plot "0,0" is always owned.
	public List<string> OwnedPlots { get; set; } = new();
	// v6: plots whose land has been cleared by foragers ("x,y") — these become buildable.
	public List<string> ClearedPlots { get; set; } = new();
	// v6: perimeter wall segment keys the player has torn down ("x,y" of the segment centre).
	public List<string> RemovedWalls { get; set; } = new();
	// Harvested resource stockpiles (ResourceKind name -> amount).
	public Dictionary<string, double> Resources { get; set; } = new();
	// Hired non-combat workers.
	public List<SavedWorker> Workers { get; set; } = new();

	// v4: active scout expedition (only one at a time).
	public bool ExpeditionActive { get; set; }
	public int ExpeditionParty { get; set; }
	public bool ExpeditionLong { get; set; }
	public long ExpeditionEndUnix { get; set; }
	public bool EverSentScouts { get; set; }
	// Units actually committed to the active expedition (restored on return, minus losses).
	public List<string> ExpeditionSoldiers { get; set; } = new();
	public List<SavedWorker> ExpeditionWorkers { get; set; } = new();

	// v4: completed onboarding objective ids.
	public List<string> ObjectivesDone { get; set; } = new();

	// v5: retention systems.
	public long TotalKills { get; set; }                 // lifetime zombie kills (for milestones)
	public long LastDailyClaimUnix { get; set; }         // last daily-bonus claim
	public int DailyStreak { get; set; }                 // consecutive daily claims
	public int PrestigeLevel { get; set; }               // times evacuated & rebuilt
	public int LegacyPoints { get; set; }                // permanent scrap-bonus points
	public List<string> MilestonesDone { get; set; } = new();

	// v8: early-night tutorial coach marks.
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();
	// v9 legacy — read during Migrate from older saves only.
	public bool FirstDayBonusClaimed { get; set; }
	// v10: one-time scrap after surviving night 1 (tutorial day-2 fund).
	public bool FirstNightBonusClaimed { get; set; }
	// v12: one-time scrap after surviving night 5 (plot expansion fund).
	public bool FifthNightPlotBonusClaimed { get; set; }
	// v12: every completed run — one row per run on the local leaderboard.
	public List<SavedRunRecord> RunHistory { get; set; } = new();

	// v14: zombie bestiary — types encountered and per-type kill counts.
	public List<string> DiscoveredZombies { get; set; } = new();
	public Dictionary<string, int> ZombieKills { get; set; } = new();

	// v11: persisted audio mix.
	public float AudioMaster { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioSfx { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioAmbience { get; set; } = GameConstants.DefaultAudioVolume;
	public float AudioMusic { get; set; } = GameConstants.DefaultAudioVolume;

	public long LastPlayedUnix { get; set; }

	/// <summary>True once the player has entered the world at least once this save.</summary>
	public bool HasStartedRun { get; set; }

	// v16: Road to a Cure mode state.
	public int CurrentSeason { get; set; }
	public int CurrentYear { get; set; } = 1;
	public int SeasonDay { get; set; } = 1;
	public float SeasonTimeAccum { get; set; }
	public int CureResearchTier { get; set; }
	public double CureLabPoints { get; set; }
	public List<string> CureObjectivesDone { get; set; } = new();
	public float ColonySickness { get; set; }
	public string SelectedTeam { get; set; }
	public string SeasonCheckpointJson { get; set; }
	public int CheckpointYear { get; set; } = 1;
	public int CheckpointSeason { get; set; }
	public int ThreatsSurvivedThisSeason { get; set; }
	public int TotalThreatsSurvived { get; set; }
	public float NextThreatTimer { get; set; }
	public int CureThreatIndex { get; set; }
	public bool EverSurvivedWinterThreat { get; set; }
	public bool CureRunComplete { get; set; }

	// v17: civ-lite colony sim (Road to a Cure).
	public List<string> UnlockedTech { get; set; } = new();
	public List<string> AlliedCivPlots { get; set; } = new();
	public List<string> RaidedCivPlots { get; set; } = new();
	public List<string> ClearedBossPlots { get; set; } = new();

	/// <summary>Whether the main menu should offer Continue vs Start New Game (Survival).</summary>
	public bool HasRunInProgress =>
		HasStartedRun
		|| BestNight > 0
		|| Buildings.Count > 0
		|| Recruits.Count > 0
		|| Workers.Count > 0
		|| TotalKills > 0
		|| Upgrades.Count > 0
		|| OwnedPlots.Count > 1
		|| ObjectivesDone.Count > 0
		|| MilestonesDone.Count > 0
		|| PrestigeLevel > 0
		|| LegacyPoints > 0;

	/// <summary>Whether a Cure run is in progress.</summary>
	public bool HasCureRunInProgress =>
		!CureRunComplete
		&& ( HasStartedRun
		|| CurrentYear > 1
		|| CurrentSeason > 0
		|| SeasonDay > 1
		|| CureResearchTier > 0
		|| CureLabPoints > 0
		|| Buildings.Count > 0
		|| Recruits.Count > 0
		|| Workers.Count > 0
		|| OwnedPlots.Count > 1
		|| CureObjectivesDone.Count > 0
		|| TotalThreatsSurvived > 0
		|| !string.IsNullOrEmpty( SelectedTeam ) );

	public void Migrate()
	{
		if ( CurrentNight <= 1 && CurrentWave > 1 )
			CurrentNight = CurrentWave;

		if ( BestNight <= 0 && BestWave > 0 )
			BestNight = BestWave;

		Buildings ??= new List<SavedBuilding>();
		Upgrades ??= new Dictionary<string, int>();
		Recruits ??= new List<string>();
		RecruitHealth ??= new List<float>();
		RecruitTrainLevels ??= new Dictionary<string, int>();
		OwnedPlots ??= new List<string>();
		ClearedPlots ??= new List<string>();
		RemovedWalls ??= new List<string>();
		Resources ??= new Dictionary<string, double>();
		Workers ??= new List<SavedWorker>();
		ObjectivesDone ??= new List<string>();
		ExpeditionSoldiers ??= new List<string>();
		ExpeditionWorkers ??= new List<SavedWorker>();
		MilestonesDone ??= new List<string>();
		TutorialTipsShown ??= new List<string>();
		RunHistory ??= new List<SavedRunRecord>();
		DiscoveredZombies ??= new List<string>();
		ZombieKills ??= new Dictionary<string, int>();
		CureObjectivesDone ??= new List<string>();

		// The home plot is always owned.
		if ( !OwnedPlots.Contains( "0,0" ) )
			OwnedPlots.Add( "0,0" );

		// v6: the build grid became a global, origin-centred grid. Older saves stored building
		// cells in a 0..11 grid whose origin was offset by -6 cells; recentre them so bases persist.
		if ( Version < 6 )
		{
			foreach ( var b in Buildings )
			{
				b.CellX -= 6;
				b.CellY -= 6;
			}
		}

		if ( Version < 10 )
		{
			if ( FirstNightBonusClaimed == false && (FirstDayBonusClaimed || CurrentNight > 1) )
				FirstNightBonusClaimed = true;
		}

		if ( Version < 11 )
		{
			if ( AudioAmbience <= 0f )
				AudioAmbience = GameConstants.DefaultAudioVolume;
			if ( AudioMusic <= 0f )
				AudioMusic = GameConstants.DefaultAudioVolume;
			if ( AudioMaster <= 0f )
				AudioMaster = GameConstants.DefaultAudioVolume;
			if ( AudioSfx <= 0f )
				AudioSfx = GameConstants.DefaultAudioVolume;
		}

		if ( Version < 12 )
		{
			RunHistory ??= new List<SavedRunRecord>();
			if ( RunHistory.Count == 0 && BestNight > 0 )
			{
				RunHistory.Add( new SavedRunRecord
				{
					Nights = BestNight,
					CompletedUnix = LastPlayedUnix > 0 ? LastPlayedUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
				} );
			}
		}

		if ( Version < 13 )
		{
			for ( var i = 0; i < Buildings.Count; i++ )
			{
				if ( Buildings[i].PlaceOrder <= 0 )
					Buildings[i].PlaceOrder = i + 1;
			}
		}

		if ( Version < 14 )
			ZombieBestiary.BackfillFromProgress( this );

		if ( Version < 15 )
		{
			AudioMaster *= 0.5f;
			AudioSfx *= 0.5f;
			AudioAmbience *= 0.5f;
			AudioMusic *= 0.5f;
		}

		if ( Version < 16 )
		{
			if ( CurrentYear < 1 ) CurrentYear = 1;
			if ( SeasonDay < 1 ) SeasonDay = 1;
			CureObjectivesDone ??= new List<string>();
		}

		if ( Version < 17 )
		{
			UnlockedTech ??= new List<string>();
			AlliedCivPlots ??= new List<string>();
			RaidedCivPlots ??= new List<string>();
			ClearedBossPlots ??= new List<string>();
		}

		Version = 17;

		while ( RecruitHealth.Count < Recruits.Count )
			RecruitHealth.Add( GameConstants.RecruitMaxHealth );
		while ( RecruitHealth.Count > Recruits.Count && RecruitHealth.Count > 0 )
			RecruitHealth.RemoveAt( RecruitHealth.Count - 1 );

		// v2 recruits were all rifle-equipped; migrate them to Assault Rifle recruits.
		if ( Recruits.Count == 0 && DefenderCount > 0 )
		{
			var type = RecruitWeaponType.AssaultRifle.ToString();
			for ( var i = 0; i < DefenderCount; i++ )
				Recruits.Add( type );

			if ( DefenderTrainLevel > 0 )
				RecruitTrainLevels[type] = DefenderTrainLevel;

			DefenderCount = 0;
			DefenderTrainLevel = 0;
		}

		if ( !HasStartedRun && HasRunInProgress )
			HasStartedRun = true;
	}

	public void RecordCompletedRun( int nights )
	{
		if ( nights < 1 ) return;

		RunHistory ??= new List<SavedRunRecord>();
		RunHistory.Add( new SavedRunRecord
		{
			Nights = nights,
			CompletedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		} );

		if ( nights > BestNight )
			BestNight = nights;
	}

	public void ResetToNew()
	{
		Scrap = GameConstants.StartingScrap;
		LifetimeEarned = 0;
		CurrentNight = 1;
		BestNight = 0;
		CurrentWave = 0;
		BestWave = 0;
		Upgrades = new Dictionary<string, int>();
		Buildings = new List<SavedBuilding>();
		DefenderCount = 0;
		DefenderTrainLevel = 0;
		Recruits = new List<string>();
		RecruitHealth = new List<float>();
		RecruitTrainLevels = new Dictionary<string, int>();
		OwnedPlots = new List<string> { "0,0" };
		ClearedPlots = new List<string>();
		RemovedWalls = new List<string>();
		Resources = new Dictionary<string, double>();
		Workers = new List<SavedWorker>();
		ExpeditionActive = false;
		ExpeditionParty = 0;
		ExpeditionLong = false;
		ExpeditionEndUnix = 0;
		EverSentScouts = false;
		ExpeditionSoldiers = new List<string>();
		ExpeditionWorkers = new List<SavedWorker>();
		ObjectivesDone = new List<string>();
		TotalKills = 0;
		LastDailyClaimUnix = 0;
		DailyStreak = 0;
		PrestigeLevel = 0;
		LegacyPoints = 0;
		MilestonesDone = new List<string>();
		DiscoveredZombies = new List<string>();
		ZombieKills = new Dictionary<string, int>();
		TutorialTipsShown = new List<string>();
		FirstNightBonusClaimed = false;
		FifthNightPlotBonusClaimed = false;
		HasStartedRun = false;
	}

	/// <summary>
	/// Hard reset after the command post is destroyed. Wipes the current run but keeps account-wide
	/// meta (legacy, milestones, bestiary, audio, etc.).
	/// </summary>
	public void WipeAfterDefeat()
	{
		Scrap = GameConstants.StartingScrap;
		CurrentNight = 1;
		CurrentWave = 0;
		Upgrades = new Dictionary<string, int>();
		Buildings = new List<SavedBuilding>();
		DefenderCount = 0;
		DefenderTrainLevel = 0;
		Recruits = new List<string>();
		RecruitHealth = new List<float>();
		RecruitTrainLevels = new Dictionary<string, int>();
		OwnedPlots = new List<string> { "0,0" };
		ClearedPlots = new List<string>();
		RemovedWalls = new List<string>();
		Resources = new Dictionary<string, double>();
		Workers = new List<SavedWorker>();
		ExpeditionActive = false;
		ExpeditionParty = 0;
		ExpeditionLong = false;
		ExpeditionEndUnix = 0;
		ExpeditionSoldiers = new List<string>();
		ExpeditionWorkers = new List<SavedWorker>();
		TutorialTipsShown = new List<string>();
		FirstNightBonusClaimed = false;
		FifthNightPlotBonusClaimed = false;
		HasStartedRun = false;
	}

	/// <summary>
	/// Soft reset for prestige ("Evacuate &amp; Rebuild"): wipes the current run but keeps permanent
	/// meta-progression — legacy points, prestige level, best night, lifetime stats, daily streak,
	/// and completed milestones/objectives.
	/// </summary>
	public void Prestige( int gainedLegacy )
	{
		PrestigeLevel += 1;
		LegacyPoints += Math.Max( 0, gainedLegacy );

		Scrap = GameConstants.StartingScrap;
		CurrentNight = 1;
		Upgrades = new Dictionary<string, int>();
		Buildings = new List<SavedBuilding>();
		Recruits = new List<string>();
		RecruitHealth = new List<float>();
		RecruitTrainLevels = new Dictionary<string, int>();
		OwnedPlots = new List<string> { "0,0" };
		ClearedPlots = new List<string>();
		RemovedWalls = new List<string>();
		Resources = new Dictionary<string, double>();
		Workers = new List<SavedWorker>();
		ExpeditionActive = false;
		ExpeditionParty = 0;
		ExpeditionLong = false;
		ExpeditionEndUnix = 0;
		ExpeditionSoldiers = new List<string>();
		ExpeditionWorkers = new List<SavedWorker>();
		TutorialTipsShown = new List<string>();
		FirstNightBonusClaimed = false;
		FifthNightPlotBonusClaimed = false;
	}

	public int GetUpgrade( string id ) => Upgrades.TryGetValue( id, out var lvl ) ? lvl : 0;
	public void SetUpgrade( string id, int level ) => Upgrades[id] = level;

	public void ResetCureRun( CureTeamId team )
	{
		ResetToNew();
		CurrentSeason = 0;
		CurrentYear = 1;
		SeasonDay = 1;
		SeasonTimeAccum = 0f;
		CureResearchTier = 0;
		CureLabPoints = 0;
		CureObjectivesDone = new List<string>();
		ColonySickness = 0f;
		SelectedTeam = team.ToString();
		SeasonCheckpointJson = null;
		CheckpointYear = 1;
		CheckpointSeason = 0;
		ThreatsSurvivedThisSeason = 0;
		TotalThreatsSurvived = 0;
		NextThreatTimer = CureConstants.ThreatInterval( 0 ) * 0.5f;
		CureThreatIndex = 0;
		EverSurvivedWinterThreat = false;
		CureRunComplete = false;
		UnlockedTech = new List<string>();
		AlliedCivPlots = new List<string>();
		RaidedCivPlots = new List<string>();
		ClearedBossPlots = new List<string>();
		ObjectivesDone = new List<string>();
		MilestonesDone = new List<string>();
		TotalKills = 0;
		PrestigeLevel = 0;
		LegacyPoints = 0;
		RunHistory = new List<SavedRunRecord>();
		DiscoveredZombies = new List<string>();
		ZombieKills = new Dictionary<string, int>();
	}

	public void ResetCureRunKeepingTeam()
	{
		var team = SelectedTeam;
		ResetCureRun( Enum.TryParse<CureTeamId>( team, out var t ) ? t : CureTeamId.None );
	}

	public void ClearCureProgressForVictory()
	{
		HasStartedRun = false;
		CureRunComplete = true;
	}
}
