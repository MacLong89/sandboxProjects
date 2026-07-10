namespace ThinkDrink.GameModes;

public sealed class MemoryGridMode : GameModeBase
{
	private static readonly string[] Tiles = { "RED", "BLUE", "GREEN", "GOLD", "STAR", "MOON", "CROWN", "WAVE" };

	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.MemoryGrid,
		DisplayName = "Memory Grid",
		ShortName = "MEMORY",
		Description = "Memorize and repeat a short symbol sequence.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var count = Math.Clamp( 3 + roundNumber / 3, 3, 6 );
		var sequence = new List<string>();
		for ( var i = 0; i < count; i++ )
			sequence.Add( Tiles[random.Next( Tiles.Length )] );

		var answer = string.Join( " ", sequence );
		return new GameModePrompt
		{
			Category = "Memory",
			Question = $"Memorize this sequence:\n{answer}",
			AnswerQuestion = "Type the sequence from memory.",
			Accepted = new List<string> { answer },
			Alternatives = new List<string> { string.Join( "", sequence ) },
			RevealedAnswer = answer,
			Explanation = "Memory Grid scores the player who can recall the full sequence under pressure.",
			UseFuzzyValidation = false
		};
	}

	public override GameModeRoundSettings GetRoundSettings( int roundNumber, RandomEventType activeEvent ) => new()
	{
		FirstCategoryRevealSeconds = 3f,
		CategoryRevealSeconds = 1.5f,
		FirstQuestionRevealSeconds = 6f,
		QuestionRevealSeconds = 5f,
		BuzzWindowSeconds = 8f,
		AnswerWindowSeconds = 10f,
		StealWindowSeconds = 5f,
		CategoryLabel = "MEMORY GRID",
		QuestionLabel = "REMEMBER",
		BuzzLabel = "RECALL",
		AnswerLabel = "TYPE SEQUENCE"
	};
}

public sealed class OddOneOutMode : GameModeBase
{
	private static readonly string[][] Groups =
	{
		new[] { "Ruby", "Emerald", "Sapphire", "Topaz" },
		new[] { "Piano", "Violin", "Trumpet", "Drums" },
		new[] { "Mercury", "Venus", "Mars", "Jupiter" },
		new[] { "Circle", "Triangle", "Square", "Hexagon" },
		new[] { "Oak", "Maple", "Cedar", "Willow" }
	};

	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.OddOneOut,
		DisplayName = "Odd One Out",
		ShortName = "ODD ONE",
		Description = "Find the option that does not belong.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var groupIndex = random.Next( Groups.Length );
		var oddGroupIndex = (groupIndex + 1 + random.Next( Groups.Length - 1 )) % Groups.Length;
		var family = Groups[groupIndex].OrderBy( _ => random.Next() ).Take( 3 ).ToList();
		var odd = Groups[oddGroupIndex][random.Next( Groups[oddGroupIndex].Length )];
		family.Add( odd );

		for ( var i = family.Count - 1; i > 0; i-- )
		{
			var j = random.Next( i + 1 );
			(family[i], family[j]) = (family[j], family[i]);
		}

		var oddIndex = family.IndexOf( odd );
		var letter = ChoiceLetter( oddIndex );
		return new GameModePrompt
		{
			Category = "Odd One Out",
			Question = $"Which option does NOT belong with the others?\n{FormatOptions( family )}",
			AnswerQuestion = $"Which option does NOT belong with the others?\n{FormatOptions( family )}",
			Accepted = new List<string> { letter, odd, $"{letter} {odd}" },
			Choices = family.Select( ( option, i ) => new McqChoice { Letter = ChoiceLetter( i ), Text = option } ).ToList(),
			RevealedAnswer = $"{letter}: {odd}",
			Explanation = $"{odd} is from a different group than the other three options.",
			UseFuzzyValidation = false
		};
	}

	private static string FormatOptions( IReadOnlyList<string> options ) =>
		string.Join( "   ", options.Select( ( option, i ) => $"{ChoiceLetter( i )}) {option}" ) );

	private static string ChoiceLetter( int index ) => index switch { 0 => "A", 1 => "B", 2 => "C", _ => "D" };
}

public sealed class EstimateBattleMode : GameModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.EstimateBattle,
		DisplayName = "Estimate Battle",
		ShortName = "ESTIMATE",
		Description = "Use the clue to estimate a hidden number.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var answer = source.Accepted.FirstOrDefault() ?? source.Question;
		var target = Math.Clamp( AnswerValidationService.Normalize( answer ).Replace( " ", "" ).Length, 1, 30 );
		return new GameModePrompt
		{
			Category = "Estimate",
			Question = $"Estimate Battle: how many letters are in the correct answer?\nClue: {source.Question}",
			AnswerQuestion = $"Estimate Battle: how many letters are in the correct answer?\nClue: {source.Question}",
			Accepted = new List<string> { target.ToString() },
			RevealedAnswer = target.ToString(),
			Explanation = $"The answer was {answer}, which has {target} letters after spaces and punctuation are removed.",
			UseFuzzyValidation = false
		};
	}

	public override int AwardPoints( TriviaQuestion question, RandomEventType activeEvent, bool isSteal, float responseTime ) =>
		Math.Max( 1, base.AwardPoints( question, activeEvent, isSteal, responseTime ) - 1 );
}

public sealed class GuessTheImageMode : GameModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.GuessTheImage,
		DisplayName = "Guess The Image",
		ShortName = "IMAGE",
		Description = "Guess the hidden subject from staged visual-style clues.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var answer = source.Accepted.FirstOrDefault() ?? "";
		return new GameModePrompt
		{
			Category = "Image Clue",
			Question = BuildVisualClue( source.Category, answer, source.Question ),
			AnswerQuestion = BuildVisualClue( source.Category, answer, source.Question ),
			Accepted = source.Accepted.ToList(),
			Alternatives = source.Alternatives.ToList(),
			RevealedAnswer = answer,
			Explanation = source.Explanation
		};
	}

	private static string MaskAnswer( string answer )
	{
		if ( string.IsNullOrWhiteSpace( answer ) ) return "????";
		return string.Join( " ", answer.Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Select( w => $"{w[0]}{new string( '_', Math.Max( 1, w.Length - 1 ) )}" ) );
	}

	private static string BuildVisualClue( string category, string answer, string prompt )
	{
		var mask = MaskAnswer( answer );
		return $"Guess The Image: identify the hidden {category} subject.\n" +
			$"[ {mask} ]\n" +
			$"Visual clue: {BuildSilhouette( answer )}\n" +
			$"Prompt: {prompt}";
	}

	private static string BuildSilhouette( string answer )
	{
		var length = Math.Clamp( AnswerValidationService.Normalize( answer ).Replace( " ", "" ).Length, 4, 12 );
		var top = new string( '#', length );
		var middle = $"#{new string( '.', Math.Max( 2, length - 2 ) )}#";
		return $"{top} / {middle} / {top}";
	}
}

public sealed class SpotTheDifferenceMode : GameModeBase
{
	public override GameModeDefinition Definition { get; } = new()
	{
		Id = GameModeId.SpotTheDifference,
		DisplayName = "Spot The Difference",
		ShortName = "DIFF",
		Description = "Compare two text panels and identify the changed word.",
		Implemented = true,
		SupportsBots = true
	};

	public override GameModePrompt BuildPrompt( TriviaQuestion source, int roundNumber, Random random )
	{
		var words = source.Question.Split( ' ', StringSplitOptions.RemoveEmptyEntries )
			.Select( CleanWord )
			.Where( w => w.Length >= 4 )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();
		var original = words.Count > 0 ? words[random.Next( words.Count )] : "question";
		var changed = PickReplacement( original, random );
		var alteredQuestion = ReplaceFirstWord( source.Question, original, changed );
		return new GameModePrompt
		{
			Category = "Spot The Difference",
			Question = $"Spot the changed word.\nPanel A: {source.Question}\nPanel B: {alteredQuestion}",
			AnswerQuestion = $"Which word changed?\nPanel A: {source.Question}\nPanel B: {alteredQuestion}",
			Accepted = new List<string> { changed },
			RevealedAnswer = changed,
			Explanation = $"The changed word was {changed}; it replaced {original}.",
			UseFuzzyValidation = false
		};
	}

	private static string CleanWord( string word ) =>
		new string( word.Where( char.IsLetter ).ToArray() );

	private static string PickReplacement( string original, Random random )
	{
		var replacements = new[] { "Lighthouse", "Velvet", "Orbit", "Copper", "Festival", "Puzzle", "Harbor", "Signal" };
		for ( var i = 0; i < 12; i++ )
		{
			var candidate = replacements[random.Next( replacements.Length )];
			if ( !string.Equals( candidate, original, StringComparison.OrdinalIgnoreCase ) )
				return candidate;
		}

		return $"{original}X";
	}

	private static string ReplaceFirstWord( string text, string original, string replacement )
	{
		var index = text.IndexOf( original, StringComparison.OrdinalIgnoreCase );
		if ( index < 0 ) return $"{text} {replacement}";
		return text[..index] + replacement + text[(index + original.Length)..];
	}
}
