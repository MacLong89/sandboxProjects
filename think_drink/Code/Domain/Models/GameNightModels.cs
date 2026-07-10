namespace ThinkDrink.Domain;

public sealed class GameModeDefinition
{
	public GameModeId Id { get; init; }
	public string DisplayName { get; init; } = "";
	public string ShortName { get; init; } = "";
	public string Description { get; init; } = "";
	public bool Implemented { get; init; }
	public bool SupportsBots { get; init; } = true;
	public bool SupportsTeams { get; init; }
	public int MinPlayers { get; init; } = GameConstants.MinPlayers;
	public int MaxPlayers { get; init; } = GameConstants.MaxPlayers;
}

public sealed class GameModeRoundSettings
{
	public float CategoryRevealSeconds { get; init; } = GameConstants.CategoryRevealSeconds;
	public float FirstCategoryRevealSeconds { get; init; } = GameConstants.FirstRoundCategoryRevealSeconds;
	public float QuestionRevealSeconds { get; init; } = GameConstants.QuestionRevealSeconds;
	public float FirstQuestionRevealSeconds { get; init; } = GameConstants.FirstRoundQuestionRevealSeconds;
	public float BuzzWindowSeconds { get; init; } = GameConstants.BuzzWindowSeconds;
	public float AnswerWindowSeconds { get; init; } = GameConstants.AnswerWindowSeconds;
	public float StealWindowSeconds { get; init; } = GameConstants.StealWindowSeconds;
	public float ScoreboardRevealSeconds { get; init; } = GameConstants.ScoreboardRevealSeconds;
	public bool StealsEnabled { get; init; } = true;
	public bool PredictionsEnabled { get; init; } = true;
	public bool UsesBuzzers { get; init; } = true;
	public bool ScoresAllCorrectAnswers { get; init; }
	public bool UsesCreativeFlow { get; init; }
	public float CreativeSubmitSeconds { get; init; } = 45f;
	public float CreativeVoteSeconds { get; init; } = 30f;
	public int CreativePointsPerVote { get; init; } = 2;
	public string CategoryLabel { get; init; } = "CATEGORY";
	public string QuestionLabel { get; init; } = "QUESTION";
	public string BuzzLabel { get; init; } = "BUZZ IN";
	public string AnswerLabel { get; init; } = "ANSWER";
}

public sealed class GameModeInput
{
	public ThinkDrinkPlayer Player { get; init; }
	public string Text { get; init; } = "";
	public bool IsSteal { get; init; }
}

public sealed class GameModePrompt
{
	public string Category { get; init; } = "";
	public string Question { get; init; } = "";
	public string AnswerQuestion { get; init; } = "";
	public List<string> Accepted { get; init; } = new();
	public List<string> Alternatives { get; init; } = new();
	public List<McqChoice> Choices { get; init; } = new();
	public string RevealedAnswer { get; init; } = "";
	public string Explanation { get; init; } = "";
	public bool UseFuzzyValidation { get; init; } = true;
}

public sealed class McqChoice
{
	public string Letter { get; init; } = "";
	public string Text { get; init; } = "";
}
