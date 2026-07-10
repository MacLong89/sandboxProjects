namespace ThinkDrink.GameModes;

public sealed class SpeedTriviaMode : GameModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.SpeedTrivia,
		DisplayName = "Speed Trivia",
		ShortName = "SPEED",
		Description = "Rapid-fire trivia with shorter reveal windows and a fast-answer point bonus.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent ) => new()
	{
		CategoryRevealSeconds = 1.2f,
		FirstCategoryRevealSeconds = 2.5f,
		QuestionRevealSeconds = 1f,
		FirstQuestionRevealSeconds = 2f,
		BuzzWindowSeconds = activeEvent == RandomEventType.LightningRound ? 3.5f : 5f,
		AnswerWindowSeconds = 8f,
		StealWindowSeconds = 5f,
		CategoryLabel = "SPEED CATEGORY",
		QuestionLabel = "RAPID QUESTION",
		BuzzLabel = "FASTEST BUZZ",
		AnswerLabel = "TYPE FAST"
	};

	public override int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime )
	{
		var points = base.AwardPoints( question, activeEvent, isSteal, responseTime );
		if ( !isSteal && responseTime <= 3f )
			points += 1;
		return points;
	}

	public override string GetScreenLabel( MatchPhase phase ) => phase switch
	{
		MatchPhase.CategoryReveal => "SPEED CATEGORY",
		MatchPhase.QuestionReveal => "RAPID QUESTION",
		MatchPhase.BuzzIn => "FASTEST BUZZ WINS",
		MatchPhase.Answering => "TYPE FAST",
		_ => base.GetScreenLabel( phase )
	};
}
