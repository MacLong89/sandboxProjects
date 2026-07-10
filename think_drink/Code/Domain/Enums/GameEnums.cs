namespace ThinkDrink.Domain;

public enum MatchPhase
{
	Lobby,
	Countdown,
	CategoryReveal,
	QuestionReveal,
	BuzzIn,
	CreativeSubmit,
	CreativeVote,
	Answering,
	StealAttempt,
	AnswerReveal,
	ScoreboardReveal,
	RandomEvent,
	MatchEnd,
	PostMatch
}

public enum GameModeId
{
	TriviaShowdown,
	QuipFill,
	CaptionThis,
	SketchQuips,
	SpeedTrivia,
	MajorityRules,
	GuessTheImage,
	FibbageStyle,
	MemoryGrid,
	OddOneOut,
	EstimateBattle,
	LieLineup,
	SpotTheDifference,
	DrawingGame,
	TriviaRoyale,
	TriviaTower,
	RaidBossTrivia,
	HotSeat,
	GuessThePlayer
}

public enum LobbyType
{
	Party,
	Bot,
	QuickPlay,
	Private
}

public enum TeamMode
{
	FreeForAll,
	Teams
}

public enum Difficulty
{
	Easy,
	Medium,
	Hard,
	Expert
}

public enum RandomEventType
{
	None,
	DoublePoints,
	LightningRound,
	CategorySwap,
	SuddenDeath
}

public enum ChallengePeriod
{
	Daily,
	Weekly
}

public enum ChallengeMetric
{
	AnswerQuestions,
	WinMatches,
	AnswerCategory,
	BuzzFirst,
	CorrectAnswers,
	WinStreak,
	MaintainAccuracy
}

public enum AchievementTrigger
{
	FirstWin,
	TotalWins,
	TotalCorrect,
	WinStreak,
	PerfectMatch,
	ComebackWin,
	FastestAnswer,
	TotalGames,
	BuzzWins,
	MatchCorrect
}

public enum AudioEventId
{
	Buzz,
	BuzzerPress,
	Correct,
	Incorrect,
	Countdown,
	RoundStart,
	Win,
	RankUp,
	AchievementUnlock,
	Steal,
	CategoryReveal,
	ScoreboardReveal,
	RandomEventStinger,
	StreakBonus,
	UiClick
}
