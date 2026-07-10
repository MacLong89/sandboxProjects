namespace FinalOutpost;

/// <summary>Serializable run snapshot for season checkpoint retry in Cure mode.</summary>
public sealed class SeasonCheckpointSnapshot
{
	public double Scrap { get; set; }
	public Dictionary<string, int> Upgrades { get; set; } = new();
	public List<SavedBuilding> Buildings { get; set; } = new();
	public List<string> Recruits { get; set; } = new();
	public List<float> RecruitHealth { get; set; } = new();
	public Dictionary<string, int> RecruitTrainLevels { get; set; } = new();
	public List<string> OwnedPlots { get; set; } = new();
	public List<string> ClearedPlots { get; set; } = new();
	public List<string> RemovedWalls { get; set; } = new();
	public Dictionary<string, double> Resources { get; set; } = new();
	public List<SavedWorker> Workers { get; set; } = new();
	public bool ExpeditionActive { get; set; }
	public int ExpeditionParty { get; set; }
	public bool ExpeditionLong { get; set; }
	public long ExpeditionEndUnix { get; set; }
	public List<string> ExpeditionSoldiers { get; set; } = new();
	public List<SavedWorker> ExpeditionWorkers { get; set; } = new();
	public List<string> CureObjectivesDone { get; set; } = new();
	public int CureResearchTier { get; set; }
	public double CureLabPoints { get; set; }
	public float ColonySickness { get; set; }
	public int ThreatsSurvivedThisSeason { get; set; }
	public int TotalThreatsSurvived { get; set; }
	public int SeasonDay { get; set; } = 1;
	public float SeasonTimeAccum { get; set; }
	public float NextThreatTimer { get; set; }
	public int CureThreatIndex { get; set; }
}

public static class SeasonCheckpoint
{
	public static void Save( SaveData save )
	{
		var snap = Capture( save );
		save.SeasonCheckpointJson = Json.Serialize( snap );
		save.CheckpointYear = save.CurrentYear;
		save.CheckpointSeason = save.CurrentSeason;
	}

	public static void Restore( SaveData save )
	{
		if ( string.IsNullOrEmpty( save.SeasonCheckpointJson ) )
		{
			save.ResetCureRunKeepingTeam();
			return;
		}

		try
		{
			var snap = Json.Deserialize<SeasonCheckpointSnapshot>( save.SeasonCheckpointJson );
			if ( snap is null )
			{
				save.ResetCureRunKeepingTeam();
				return;
			}

			Apply( save, snap );
			save.CurrentYear = save.CheckpointYear;
			save.CurrentSeason = save.CheckpointSeason;
			save.SeasonDay = snap.SeasonDay;
			save.SeasonTimeAccum = snap.SeasonTimeAccum;
			save.NextThreatTimer = snap.NextThreatTimer;
		}
		catch
		{
			save.ResetCureRunKeepingTeam();
		}
	}

	public static SeasonCheckpointSnapshot Capture( SaveData save ) => new()
	{
		Scrap = save.Scrap,
		Upgrades = new Dictionary<string, int>( save.Upgrades ),
		Buildings = save.Buildings.Select( b => new SavedBuilding
		{
			Type = b.Type,
			CellX = b.CellX,
			CellY = b.CellY,
			Level = b.Level,
			Health = b.Health,
			PlaceOrder = b.PlaceOrder
		} ).ToList(),
		Recruits = new List<string>( save.Recruits ),
		RecruitHealth = new List<float>( save.RecruitHealth ),
		RecruitTrainLevels = new Dictionary<string, int>( save.RecruitTrainLevels ),
		OwnedPlots = new List<string>( save.OwnedPlots ),
		ClearedPlots = new List<string>( save.ClearedPlots ),
		RemovedWalls = new List<string>( save.RemovedWalls ),
		Resources = new Dictionary<string, double>( save.Resources ),
		Workers = save.Workers.Select( w => new SavedWorker
		{
			Role = w.Role,
			PlotX = w.PlotX,
			PlotY = w.PlotY
		} ).ToList(),
		ExpeditionActive = save.ExpeditionActive,
		ExpeditionParty = save.ExpeditionParty,
		ExpeditionLong = save.ExpeditionLong,
		ExpeditionEndUnix = save.ExpeditionEndUnix,
		ExpeditionSoldiers = new List<string>( save.ExpeditionSoldiers ),
		ExpeditionWorkers = save.ExpeditionWorkers.Select( w => new SavedWorker
		{
			Role = w.Role,
			PlotX = w.PlotX,
			PlotY = w.PlotY
		} ).ToList(),
		CureObjectivesDone = new List<string>( save.CureObjectivesDone ),
		CureResearchTier = save.CureResearchTier,
		CureLabPoints = save.CureLabPoints,
		ColonySickness = save.ColonySickness,
		ThreatsSurvivedThisSeason = save.ThreatsSurvivedThisSeason,
		TotalThreatsSurvived = save.TotalThreatsSurvived,
		SeasonDay = save.SeasonDay,
		SeasonTimeAccum = save.SeasonTimeAccum,
		NextThreatTimer = save.NextThreatTimer,
		CureThreatIndex = save.CureThreatIndex
	};

	private static void Apply( SaveData save, SeasonCheckpointSnapshot snap )
	{
		save.Scrap = snap.Scrap;
		save.Upgrades = snap.Upgrades ?? new Dictionary<string, int>();
		save.Buildings = snap.Buildings ?? new List<SavedBuilding>();
		save.Recruits = snap.Recruits ?? new List<string>();
		save.RecruitHealth = snap.RecruitHealth ?? new List<float>();
		save.RecruitTrainLevels = snap.RecruitTrainLevels ?? new Dictionary<string, int>();
		save.OwnedPlots = snap.OwnedPlots ?? new List<string> { "0,0" };
		save.ClearedPlots = snap.ClearedPlots ?? new List<string>();
		save.RemovedWalls = snap.RemovedWalls ?? new List<string>();
		save.Resources = snap.Resources ?? new Dictionary<string, double>();
		save.Workers = snap.Workers ?? new List<SavedWorker>();
		save.ExpeditionActive = snap.ExpeditionActive;
		save.ExpeditionParty = snap.ExpeditionParty;
		save.ExpeditionLong = snap.ExpeditionLong;
		save.ExpeditionEndUnix = snap.ExpeditionEndUnix;
		save.ExpeditionSoldiers = snap.ExpeditionSoldiers ?? new List<string>();
		save.ExpeditionWorkers = snap.ExpeditionWorkers ?? new List<SavedWorker>();
		save.CureObjectivesDone = snap.CureObjectivesDone ?? new List<string>();
		save.CureResearchTier = snap.CureResearchTier;
		save.CureLabPoints = snap.CureLabPoints;
		save.ColonySickness = snap.ColonySickness;
		save.ThreatsSurvivedThisSeason = snap.ThreatsSurvivedThisSeason;
		save.TotalThreatsSurvived = snap.TotalThreatsSurvived;
		save.SeasonDay = snap.SeasonDay;
		save.SeasonTimeAccum = snap.SeasonTimeAccum;
		save.NextThreatTimer = snap.NextThreatTimer;
		save.CureThreatIndex = snap.CureThreatIndex;
	}
}
