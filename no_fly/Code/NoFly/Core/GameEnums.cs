namespace NoFly;

public enum RoundState
{
	WaitingForPlayers,
	LobbyCountdown,
	AssigningRoles,
	RoleReveal,
	Preparation,
	AirportOpen,
	Boarding,
	Chase,
	RoundEnd,
	Results,
	Resetting
}

public enum TeamType
{
	None,
	Passenger,
	Tsa
}

public enum RoleType
{
	None,
	RegularPassenger,
	Smuggler,
	UndercoverAgent,
	DocumentAgent,
	ScannerAgent,
	SecurityOfficer
}

public enum RolePreference
{
	NoPreference,
	Passenger,
	Tsa
}

public enum PassengerFlowState
{
	EnteringAirport,
	GoingToDocuments,
	DocumentQueue,
	DocumentInspection,
	GoingToScanner,
	ScannerQueue,
	BagInspection,
	InTerminal,
	CompletingObjective,
	GoingToGate,
	Boarding,
	Detained,
	Arrested,
	MissedFlight,
	Escaping,
	Boarded
}

public enum FlightStatus
{
	CheckIn,
	SecurityOpen,
	BoardingSoon,
	Boarding,
	FinalCall,
	Closed,
	Departed
}

public enum DocumentFieldType
{
	Photo,
	Name,
	Date,
	PassportNumber,
	CountrySymbol,
	SecuritySeal,
	Destination,
	BackgroundPattern
}

public enum DiscrepancyDifficulty
{
	Easy,
	Medium,
	Hard
}

public enum AlertType
{
	DocumentFlag,
	BagFlag,
	PassengerReport,
	UndercoverMark,
	RestrictedArea,
	Chase,
	AbandonedBag
}

public enum ReportReason
{
	StrangeBehavior,
	RanFromCheckpoint,
	EnteredRestrictedArea,
	SuspiciousLuggage,
	SuspiciousDocument,
	FollowingSomeone,
	AbandonedBag
}

public enum WinSide
{
	None,
	Smuggler,
	Tsa
}

public enum InteractionKind
{
	None,
	PresentDocument,
	PlaceBag,
	PickUpLuggage,
	UseShop,
	Sit,
	OpenDoor,
	ReportPlayer,
	QuestionPassenger,
	Detain,
	Arrest,
	BoardFlight,
	JoinQueue,
	LeaveQueue,
	MarkSuspect,
	AlertTsa,
	ManStation
}
