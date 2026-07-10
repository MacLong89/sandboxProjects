namespace Dynasty.Core.Enums;

public enum LeaguePhase
{
	Preseason,
	RegularSeason,
	Playoffs,
	Offseason,
	Draft,
	FreeAgency
}

public enum DynastyStartMode
{
	/// <summary>Full roster — inaugural rookie draft before season one.</summary>
	RookieDraft,
	/// <summary>Empty rosters — draft veterans from a league-wide pool.</summary>
	ExpansionDraft,
	/// <summary>Full roster — skip inaugural draft and jump into preseason.</summary>
	QuickStart
}

public enum DraftType
{
	Rookie,
	Expansion
}

public enum OffseasonSubPhase
{
	Retirements,
	CoachingChanges,
	FreeAgency,
	Scouting,
	Draft,
	FacilityUpgrades,
	Complete
}

public enum ChallengeMode
{
	Standard,
	Rebuild,
	WinNow,
	DraftGenius
}

public enum PlayoffRound
{
	None,
	WildCard,
	Divisional,
	Conference,
	SuperBowl
}

public enum Position
{
	QB,
	RB,
	FB,
	WR,
	TE,
	OT,
	OG,
	C,
	DE,
	DT,
	LB,
	CB,
	S,
	K,
	P,
	LS
}

public enum PositionGroup
{
	Quarterback,
	RunningBack,
	WideReceiver,
	TightEnd,
	OffensiveLine,
	DefensiveLine,
	Linebacker,
	DefensiveBack,
	SpecialTeams
}

public enum CoachRole
{
	HeadCoach,
	OffensiveCoordinator,
	DefensiveCoordinator,
	Scout
}

public enum PlayerTrait
{
	Clutch,
	IronMan,
	Leader,
	TeamFriendly,
	InjuryProne,
	Diva,
	Choker,
	Lazy
}

public enum InjurySeverity
{
	None,
	Questionable,
	Doubtful,
	Out,
	SeasonEnding,
	CareerThreatening
}

public enum FacilityType
{
	Stadium,
	TrainingFacility,
	MedicalCenter,
	ScoutingDepartment,
	FanAmenities
}

public enum GmControlType
{
	Human,
	AI,
	Commissioner
}

public enum TradeAssetType
{
	Player,
	DraftPick,
	Cash
}

public enum NewsCategory
{
	General,
	Injury,
	Trade,
	Draft,
	Coaching,
	Championship,
	Record,
	Retirement
}

public enum GameVisualizationMode
{
	TopDownField,
	DrivePanel
}

public enum SimEventType
{
	Kickoff,
	DriveStart,
	Play,
	Score,
	Turnover,
	Punt,
	FieldGoalAttempt,
	DriveEnd,
	GameEnd
}

public enum TimeAdvanceTarget
{
	OneWeek,
	MidSeason,
	EndRegularSeason,
	SuperBowl,
	NextDraft
}
