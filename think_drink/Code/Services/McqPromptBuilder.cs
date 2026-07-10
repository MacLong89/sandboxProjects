namespace ThinkDrink.Services;

using System.Text.RegularExpressions;

/// <summary>Builds multiple-choice prompts from stored distractors or cleans open-ended stems.</summary>
public static class McqPromptBuilder
{
	public static bool TryBuild( TriviaQuestion source, Random random, out GameModePrompt prompt )
	{
		prompt = null;
		if ( source?.Accepted is null || source.Accepted.Count == 0 )
			return false;
		if ( source.Distractors is null || source.Distractors.Count == 0 )
			return false;

		var correct = source.Accepted[0];
		if ( string.IsNullOrWhiteSpace( correct ) )
			return false;

		var options = new List<string> { correct };
		foreach ( var distractor in source.Distractors )
		{
			if ( string.IsNullOrWhiteSpace( distractor ) )
				continue;

			if ( options.Any( o => string.Equals( o, distractor, StringComparison.OrdinalIgnoreCase ) ) )
				continue;

			options.Add( distractor );
			if ( options.Count >= 4 )
				break;
		}

		if ( options.Count < 2 )
			return false;

		for ( var i = options.Count - 1; i > 0; i-- )
		{
			var j = random.Next( i + 1 );
			(options[i], options[j]) = (options[j], options[i]);
		}

		var correctIndex = options.FindIndex( o => string.Equals( o, correct, StringComparison.OrdinalIgnoreCase ) );
		if ( correctIndex < 0 )
			return false;

		var correctLetter = Letter( correctIndex );
		var choices = options.Select( ( text, index ) => new McqChoice
		{
			Letter = Letter( index ),
			Text = text
		} ).ToList();

		var accepted = new List<string> { correctLetter, correct, $"{correctLetter}) {correct}" };
		foreach ( var choice in choices )
		{
			if ( !accepted.Any( a => string.Equals( a, choice.Text, StringComparison.OrdinalIgnoreCase ) ) )
				accepted.Add( choice.Text );
			accepted.Add( $"{choice.Letter}) {choice.Text}" );
		}

		prompt = new GameModePrompt
		{
			Category = source.Category ?? "",
			Question = source.Question ?? "",
			AnswerQuestion = source.Question ?? "",
			Accepted = accepted,
			Choices = choices,
			RevealedAnswer = $"{correctLetter}: {correct}",
			Explanation = source.Explanation ?? "",
			UseFuzzyValidation = false
		};
		return true;
	}

	public static string CleanOpenEndedStem( string question )
	{
		if ( string.IsNullOrWhiteSpace( question ) )
			return "";

		var text = question.Trim();

		text = Regex.Replace( text, @"^Which of the following\s+(is|are|was|were)\s+", "What $1 ", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"^Which of these\s+(is|are|was|were)\s+", "What $1 ", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"^Which one of the following\s+(is|are|was|were)\s+", "What $1 ", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"^Which one of these\s+(is|are|was|were)\s+", "What $1 ", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"^Select the correct answer:\s*", "", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"^Pick the correct answer:\s*", "", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"\s+from the following options\.?$", "", RegexOptions.IgnoreCase );
		text = Regex.Replace( text, @"\s+from the following\.?$", "", RegexOptions.IgnoreCase );

		if ( text.Length > 0 && char.IsLower( text[0] ) )
			text = char.ToUpper( text[0] ) + text[1..];

		return text;
	}

	private static string Letter( int index ) => index switch
	{
		0 => "A",
		1 => "B",
		2 => "C",
		_ => "D"
	};
}
