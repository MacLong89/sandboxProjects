namespace ThinkDrink.GameModes;

public abstract class CreativeGameModeBase : GameModeBase, ICreativeGameMode
{
	public abstract CreativePromptKind PromptKind { get; }
	public abstract string SubmitLead { get; }
	public abstract string VoteLead { get; }

	static CreativePromptRepository _repository;

	static CreativePromptRepository Repository
	{
		get
		{
			_repository ??= new CreativePromptRepository();
			if ( _repository is not null )
			{
				// Load once on first creative round.
			}

			return _repository;
		}
	}

	public static void EnsurePromptsLoaded()
	{
		_repository ??= new CreativePromptRepository();
		_repository.Load();
	}

	public GameModePrompt PickPrompt( int roundNumber, Random random, IReadOnlyCollection<string> usedPromptIds )
	{
		EnsurePromptsLoaded();
		var entry = Repository.Pick( PromptKind, random, usedPromptIds );
		var question = FormatPrompt( entry.Prompt );

		return new GameModePrompt
		{
			Category = entry.Category,
			Question = question,
			AnswerQuestion = question,
			RevealedAnswer = "",
			Explanation = entry.Id,
			UseFuzzyValidation = false
		};
	}

	protected virtual string FormatPrompt( string prompt ) => prompt;

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random ) =>
		PickPrompt( roundNumber, random, Array.Empty<string>() );

	public override int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime ) =>
		0;

	public override GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent ) => new()
	{
		CategoryRevealSeconds = 2f,
		FirstCategoryRevealSeconds = 2.5f,
		QuestionRevealSeconds = 3f,
		FirstQuestionRevealSeconds = 4f,
		ScoreboardRevealSeconds = 3f,
		StealsEnabled = false,
		PredictionsEnabled = false,
		UsesBuzzers = false,
		UsesCreativeFlow = true,
		CreativeSubmitSeconds = 45f,
		CreativeVoteSeconds = 30f,
		CreativePointsPerVote = 2,
		CategoryLabel = "ROUND THEME",
		QuestionLabel = "PROMPT",
		AnswerLabel = "SUBMIT",
		BuzzLabel = "WRITE"
	};

	public override string GetScreenLabel( MatchPhase phase ) => phase switch
	{
		MatchPhase.CategoryReveal => "CREATIVE ROUND",
		MatchPhase.QuestionReveal => "READ THE PROMPT",
		MatchPhase.CreativeSubmit => SubmitLead,
		MatchPhase.CreativeVote => VoteLead,
		MatchPhase.AnswerReveal => "RESULTS",
		_ => Definition.ShortName
	};
}

public sealed class QuipFillMode : CreativeGameModeBase
{
	public override CreativePromptKind PromptKind => CreativePromptKind.QuipFill;
	public override string SubmitLead => "WRITE YOUR QUIP";
	public override string VoteLead => "PICK THE FUNNIEST";

	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.QuipFill,
		DisplayName = "Quip Fill",
		ShortName = "QUIP",
		Description = "Fill in the blank with something hilarious. Everyone votes on the best quip.",
		Implemented = true,
		SupportsBots = true
	};
}

public sealed class CaptionThisMode : CreativeGameModeBase
{
	public override CreativePromptKind PromptKind => CreativePromptKind.CaptionThis;
	public override string SubmitLead => "WRITE YOUR CAPTION";
	public override string VoteLead => "PICK THE BEST CAPTION";

	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.CaptionThis,
		DisplayName = "Caption This",
		ShortName = "CAPTION",
		Description = "Caption a ridiculous scenario. Vote for the line that made you laugh hardest.",
		Implemented = true,
		SupportsBots = true
	};

	protected override string FormatPrompt( string prompt ) =>
		$"Caption this:\n{prompt}";
}

public sealed class SketchQuipsMode : CreativeGameModeBase
{
	public override CreativePromptKind PromptKind => CreativePromptKind.SketchQuips;
	public override string SubmitLead => "DESCRIBE YOUR DOODLE";
	public override string VoteLead => "PICK THE BEST SKETCH QUIP";

	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.SketchQuips,
		DisplayName = "Sketch Quips",
		ShortName = "SKETCH",
		Description = "Imagine the doodle, describe it in one funny sentence. Vote on the best visual gag.",
		Implemented = true,
		SupportsBots = true
	};

	protected override string FormatPrompt( string prompt ) =>
		$"Imagine you drew this — describe your doodle in one sentence:\n{prompt}";
}
