using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Data;
using Dynasty.Domain.Trades;

namespace Dynasty.Domain.Teams;

public sealed class TeamState
{
	public TeamId Id { get; set; }
	public TeamIdentity Identity { get; set; } = new();
	public TeamFinances Finances { get; set; } = new();
	public TeamPrestigeState Prestige { get; set; } = new();
	public FacilityState Facilities { get; set; } = new();
	public ChemistryState Chemistry { get; set; } = new();
	public FanState Fans { get; set; } = new();
	public List<PlayerId> RosterPlayerIds { get; set; } = new();
	public List<CoachId> CoachIds { get; set; } = new();
	public List<DraftPickAsset> DraftPicks { get; set; } = new();
	public TeamRecord Record { get; set; } = new();
	public TeamLifetimeRecord LifetimeRecord { get; set; } = new();
	public GmControlType ControlType { get; set; } = GmControlType.AI;
	public ulong? HumanOwnerSteamId { get; set; }
	public TeamBuildingWindow BuildingWindow { get; set; } = TeamBuildingWindow.Rebuilding;
	public TeamPlayStyle PlayStyle { get; set; } = TeamPlayStyle.Balanced;
	public Dictionary<string, List<PlayerId>> DepthChart { get; set; } = new();
	public FormationType ActiveOffenseFormation { get; set; } = FormationType.Offense11;
	public FormationType ActiveDefenseFormation { get; set; } = FormationType.Defense43;
	public WeeklyGamePlan WeeklyGamePlan { get; set; } = WeeklyGamePlan.None;
}

public sealed class TeamIdentity
{
	public string City { get; set; } = "";
	public string Name { get; set; } = "";
	public string Abbreviation { get; set; } = "";
	public string PrimaryColor { get; set; } = "#1a1a2e";
	public string SecondaryColor { get; set; } = "#e94560";
	public string Stadium { get; set; } = "";
}

public sealed class TeamFinances
{
	public long Budget { get; set; }
	public long SalaryCapSpace { get; set; }
	public long DeadCap { get; set; }
}

public sealed class TeamPrestigeState
{
	public int Prestige { get; set; } = 50;
	public int FanSupport { get; set; } = 50;
	public int RecentSuccessScore { get; set; }
}

public sealed class FacilityState
{
	public Dictionary<FacilityType, int> Levels { get; set; } = new()
	{
		[FacilityType.Stadium] = 1,
		[FacilityType.TrainingFacility] = 1,
		[FacilityType.MedicalCenter] = 1,
		[FacilityType.ScoutingDepartment] = 1,
		[FacilityType.FanAmenities] = 1
	};
}

public sealed class ChemistryState
{
	public int Morale { get; set; } = 70;
	public int LockerRoomHealth { get; set; } = 70;
	public int Leadership { get; set; } = 50;
}

public sealed class FanState
{
	public int Attendance { get; set; }
	public int Happiness { get; set; } = 60;
	public int Popularity { get; set; } = 50;
}

public sealed class TeamRecord
{
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Ties { get; set; }
	public int PointsFor { get; set; }
	public int PointsAgainst { get; set; }
	public int ConferenceRank { get; set; }
	public PlayoffRound PlayoffStatus { get; set; } = PlayoffRound.None;
}

public sealed class TeamLifetimeRecord
{
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int Ties { get; set; }
}

public sealed class DraftPickAsset
{
	public DraftPickId Id { get; set; }
	public int Season { get; set; }
	public int Round { get; set; }
	public int PickNumber { get; set; }
	public TeamId OriginalOwnerId { get; set; }
	public TeamId CurrentOwnerId { get; set; }
}
