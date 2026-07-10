namespace ThinkDrink.GameModes;

public abstract class MultipleChoiceModeBase : GameModeBase
{
	private static readonly string[] Decoys =
	{
		"Moonlight", "Thunder", "Glass", "Jupiter", "Compass", "Velvet",
		"Harbor", "Copper", "Falcon", "Mirage", "Atlas", "Neon"
	};

	protected virtual string PromptLead => "Choose the correct option.";
	protected virtual string RevealPrefix => "Correct choice";

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var correct = source.Accepted.FirstOrDefault() ?? "Unknown";
		var options = BuildOptions( correct, random );
		var correctIndex = options.IndexOf( correct );
		var correctLetter = Letter( correctIndex );

		return new GameModePrompt
		{
			Category = Definition.DisplayName,
			Question = $"{PromptLead}\n{source.Question}\n{FormatOptions( options )}",
			AnswerQuestion = $"{PromptLead}\n{source.Question}\n{FormatOptions( options )}",
			Accepted = new List<string> { correctLetter, correct, $"{correctLetter} {correct}" },
			Choices = options.Select( ( option, i ) => new McqChoice { Letter = Letter( i ), Text = option } ).ToList(),
			RevealedAnswer = $"{correctLetter}: {correct}",
			Explanation = string.IsNullOrWhiteSpace( source.Explanation )
				? $"{RevealPrefix}: {correct}"
				: source.Explanation,
			UseFuzzyValidation = false
		};
	}

	private static List<string> BuildOptions( string correct, Random random )
	{
		var options = new List<string> { correct };
		var guard = 0;
		while ( options.Count < 4 && guard++ < 40 )
		{
			var decoy = Decoys[random.Next( Decoys.Length )];
			if ( options.Any( o => string.Equals( o, decoy, StringComparison.OrdinalIgnoreCase ) ) )
				continue;
			options.Add( decoy );
		}

		for ( var i = options.Count - 1; i > 0; i-- )
		{
			var j = random.Next( i + 1 );
			(options[i], options[j]) = (options[j], options[i]);
		}

		return options;
	}

	protected static string FormatOptions( IReadOnlyList<string> options ) =>
		string.Join( "   ", options.Select( ( option, i ) => $"{Letter( i )}) {option}" ) );

	protected static string Letter( int index ) => index switch
	{
		0 => "A",
		1 => "B",
		2 => "C",
		_ => "D"
	};
}

public sealed class MajorityRulesMode : MultipleChoiceModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.MajorityRules,
		DisplayName = "Majority Rules",
		ShortName = "MAJORITY",
		Description = "Pick the answer the crowd should rally around from a fast option set.",
		Implemented = true,
		SupportsBots = true
	};

	protected override string PromptLead => "Majority Rules: pick the answer most players should choose.";
	protected override string RevealPrefix => "The majority target was";

	public override GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent ) => new()
	{
		QuestionRevealSeconds = 2.5f,
		BuzzWindowSeconds = 8f,
		AnswerWindowSeconds = 12f,
		StealWindowSeconds = 0f,
		StealsEnabled = false,
		PredictionsEnabled = false,
		UsesBuzzers = false,
		ScoresAllCorrectAnswers = true,
		CategoryLabel = "POLL CATEGORY",
		QuestionLabel = "VOTE QUESTION",
		BuzzLabel = "VOTE NOW",
		AnswerLabel = "LOCK YOUR VOTE"
	};

	public override string GetScreenLabel( MatchPhase phase ) => phase switch
	{
		MatchPhase.QuestionReveal => "VOTE QUESTION",
		MatchPhase.Answering => "EVERYONE VOTES",
		MatchPhase.AnswerReveal => "VOTE REVEAL",
		_ => base.GetScreenLabel( phase )
	};
}

public sealed class FibbageStyleMode : MultipleChoiceModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.FibbageStyle,
		DisplayName = "Fibbage Style",
		ShortName = "FIB",
		Description = "Find the truth among plausible fake answers.",
		Implemented = true,
		SupportsBots = true
	};

	protected override string PromptLead => "Fibbage: three answers are fake. Pick the truth.";
	protected override string RevealPrefix => "Truth";

	public override int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime ) =>
		base.AwardPoints( question, activeEvent, isSteal, responseTime ) + 1;
}

public sealed class LieLineupMode : MultipleChoiceModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.LieLineup,
		DisplayName = "Lie Lineup",
		ShortName = "LINEUP",
		Description = "Spot the one truthful answer in a lineup of lies.",
		Implemented = true,
		SupportsBots = true
	};

	protected override string PromptLead => "Lie Lineup: one option is true. Find it.";
	protected override string RevealPrefix => "The true answer was";
}
