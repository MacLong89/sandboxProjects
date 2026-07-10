namespace Dynasty.Core.Enums;

public enum CalendarMonth
{
	February,
	March,
	April,
	May,
	June,
	July,
	August,
	September,
	October,
	November,
	December,
	January
}

public enum FranchiseEventType
{
	GameDay,
	InjuryReport,
	ContractDemand,
	TradeOffer,
	DraftPick,
	PhaseTransition,
	CoachingChange,
	FreeAgencySigning,
	PracticeReport,
	WeeklyRecap,
	AwardsCeremony,
	RetirementWave,
	TradeDeadline,
	RosterCut
}

public enum InboxCategory
{
	General,
	Roster,
	Contract,
	Trade,
	Draft,
	Coaching,
	Injury,
	Fan,
	League
}

public enum InboxPriority
{
	Low,
	Normal,
	High,
	Urgent
}

public enum TeamBuildingWindow
{
	Rebuilding,
	Contending,
	WinNow,
	Dynasty,
	Declining
}

public enum TeamPlayStyle
{
	Balanced,
	AirRaid,
	GroundAndPound,
	DefensiveDynasty,
	YouthMovement,
	VeteranLeadership
}
