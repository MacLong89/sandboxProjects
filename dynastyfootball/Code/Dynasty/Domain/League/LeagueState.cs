using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Factories;
using Dynasty.Domain.Calendar;
using Dynasty.Domain.Coaches;
using Dynasty.Domain.Draft;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.FreeAgency;
using Dynasty.Domain.Trades;
using Dynasty.Domain.History;
using Dynasty.Domain.Inbox;
using Dynasty.Domain.News;
using Dynasty.Domain.Players;
using Dynasty.Domain.Schedule;
using Dynasty.Domain.Teams;

namespace Dynasty.Domain.League;

/// <summary>
/// Root aggregate for all league data. Pure data — no behavior. All mutations flow through services/systems.
/// </summary>
public sealed class LeagueState
{
	public const int CurrentSchemaVersion = 7;

	public LeagueId Id { get; set; }
	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public LeagueSettings Settings { get; set; } = new();
	public LeaguePhase Phase { get; set; } = LeaguePhase.Preseason;
	public OffseasonSubPhase OffseasonSubPhase { get; set; } = OffseasonSubPhase.Retirements;
	public int CurrentSeason { get; set; } = 1;
	public int CurrentWeek { get; set; } = 1;
	public int RandomSeed { get; set; }
	public ulong StateRevision { get; set; }
	public long EventSequence { get; set; }

	public Dictionary<TeamId, TeamState> Teams { get; set; } = new();
	public Dictionary<PlayerId, PlayerState> Players { get; set; } = new();
	public Dictionary<CoachId, CoachState> Coaches { get; set; } = new();

	public ScheduleState Schedule { get; set; } = new();
	public DraftState Draft { get; set; } = new();
	public FreeAgencyState FreeAgency { get; set; } = new();
	public List<NewsItem> News { get; set; } = new();
	public LeagueHistory History { get; set; } = new();
	public Dictionary<ulong, GmAssignment> GmAssignments { get; set; } = new();
	public LeagueCalendarState Calendar { get; set; } = new();
	public List<InboxMessage> Inbox { get; set; } = new();
	public List<FranchiseQueuedEvent> EventQueue { get; set; } = new();
	public FranchiseProgressState FranchiseProgress { get; set; } = new();
	public List<PendingTradeOffer> PendingTradeOffers { get; set; } = new();
	public string NextSuggestedAction { get; set; } = "Review your inbox and advance time.";

	public void BumpRevision( string source )
	{
		StateRevision++;
	}
}

public sealed class LeagueSettings
{
	public string LeagueName { get; set; } = "Sunday Dynasty";
	public int TeamCount { get; set; } = 32;
	public int PreseasonWeeks { get; set; } = 4;
	public int RegularSeasonWeeks { get; set; } = 18;
	public int PlayoffWeeks { get; set; } = 4;
	public int OffseasonWeeks { get; set; } = 2;
	public int FreeAgencyWeeks { get; set; } = 2;
	public int PlayoffTeams { get; set; } = 14;
	public int SalaryCap { get; set; } = 255_000_000;
	public DynastyStartMode StartMode { get; set; } = DynastyStartMode.RookieDraft;
	public string HumanTeamAbbreviation { get; set; } = "";
	/// <summary>Rookie draft round count (7 rounds × 32 teams).</summary>
	public int RookieDraftRounds { get; set; } = 7;
	public int ExpansionDraftRounds { get; set; } = 52;

	/// <summary>Alias for <see cref="RookieDraftRounds"/>.</summary>
	public int DraftRounds
	{
		get => RookieDraftRounds;
		set => RookieDraftRounds = value;
	}
	public int RosterSize { get; set; } = LeaguePlayerGenerator.DefaultRosterSize;
	public int LeaguePlayerPoolSize { get; set; } = LeaguePlayerGenerator.DefaultLeaguePoolSize;
	public int RookieProspectCount { get; set; } = LeaguePlayerGenerator.DefaultRookieClassSize;
	public int MinPlayersPerPositionPerTeam { get; set; } = LeaguePlayerGenerator.DefaultMinPerPositionPerTeam;
	public int DraftPickTimerSeconds { get; set; } = 60;
	public GameVisualizationMode DefaultVisualizationMode { get; set; } = GameVisualizationMode.TopDownField;
	public ChallengeMode ChallengeMode { get; set; } = ChallengeMode.Standard;
	/// <summary>Shorter onboarding path for new dynasties.</summary>
	public bool IsFtueExperience { get; set; } = true;
	public int FtuePreseasonWeeks { get; set; } = 1;
	/// <summary>Allow multiple human GMs to claim teams in the same save.</summary>
	public bool EnableMultiGm { get; set; }
}

public sealed class GmAssignment
{
	public ulong SteamId { get; set; }
	public TeamId TeamId { get; set; }
	public GmControlType ControlType { get; set; }
	public bool IsCommissioner { get; set; }
}
