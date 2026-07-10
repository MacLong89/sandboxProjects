namespace ThinkDrink.GameModes;

public abstract class GameModeBase : IGameMode
{
	protected MatchManager Match { get; private set; }

	public abstract GameModeDefinition Definition { get; }

	public virtual void Initialize( MatchManager match ) => Match = match;

	public virtual void StartRound( int roundNumber )
	{
	}

	public virtual void EndRound()
	{
	}

	public virtual void Cleanup() => Match = null;

	public virtual GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		if ( McqPromptBuilder.TryBuild( source, random, out var mcqPrompt ) )
			return mcqPrompt;

		var accepted = source?.Accepted?.ToList() ?? new List<string>();
		var alternatives = source?.Alternatives?.ToList() ?? new List<string>();
		var question = McqPromptBuilder.CleanOpenEndedStem( source?.Question ?? "" );
		return new GameModePrompt
		{
			Category = source?.Category ?? "",
			Question = question,
			AnswerQuestion = question,
			Accepted = accepted,
			Alternatives = alternatives,
			RevealedAnswer = accepted.FirstOrDefault() ?? "",
			Explanation = source?.Explanation ?? ""
		};
	}

	public virtual AnswerValidationResult ValidateAnswer( string submission, GameModePrompt prompt, TriviaQuestion source, IAnswerValidator validator )
	{
		if ( prompt is null )
			return new AnswerValidationResult();

		var question = new TriviaQuestion
		{
			Question = prompt.Question,
			Accepted = prompt.Accepted,
			Alternatives = prompt.Alternatives
		};

		if ( prompt.UseFuzzyValidation )
			return validator.Validate( submission, question );

		var normalized = AnswerValidationService.Normalize( submission );
		foreach ( var answer in question.AllAnswers() )
		{
			if ( normalized == AnswerValidationService.Normalize( answer ) )
				return new AnswerValidationResult { IsCorrect = true, MatchedAnswer = answer, Confidence = 1f };
		}

		return new AnswerValidationResult();
	}

	public virtual int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime )
	{
		var points = QuestionSelectionService.PointsForDifficulty( question.Difficulty, activeEvent );
		return isSteal ? Math.Max( 1, points - 1 ) : points;
	}

	public virtual bool HandlePlayerInput( GameModeInput input ) => true;

	public virtual ThinkDrinkPlayer DetermineWinner()
	{
		if ( Match is null ) return null;
		return ThinkDrinkPlayer.All
			.Where( p => p.IsParticipant )
			.OrderByDescending( p => p.MatchScore )
			.FirstOrDefault();
	}

	public virtual GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent )
	{
		if ( activeEvent == RandomEventType.LightningRound )
		{
			return new GameModeRoundSettings
			{
				QuestionRevealSeconds = 1f,
				FirstQuestionRevealSeconds = 1.5f,
				BuzzWindowSeconds = 5f
			};
		}

		return new GameModeRoundSettings();
	}

	public virtual string GetScreenLabel( MatchPhase phase ) => phase switch
	{
		MatchPhase.CategoryReveal => "CATEGORY",
		MatchPhase.QuestionReveal => "QUESTION",
		MatchPhase.BuzzIn => "BUZZ IN",
		MatchPhase.Answering => "ANSWER",
		MatchPhase.StealAttempt => "STEAL",
		MatchPhase.AnswerReveal => "ANSWER REVEALED",
		MatchPhase.ScoreboardReveal => "STANDINGS",
		_ => Definition.ShortName
	};
}
