namespace ThinkDrink.Services;

/// <summary>Engine-agnostic trivia bot decision logic.</summary>
public static class TriviaBotService
{
	public static float RollBuzzDelay( Difficulty difficulty, Random rng )
	{
		var min = difficulty switch
		{
			Difficulty.Easy => 0.35f,
			Difficulty.Expert => 1.2f,
			_ => 0.55f
		};
		var max = difficulty switch
		{
			Difficulty.Easy => 2.2f,
			Difficulty.Expert => 4.5f,
			Difficulty.Hard => 3.8f,
			_ => 3.2f
		};
		return min + rng.NextSingle() * (max - min);
	}

	public static float RollAnswerDelay( Difficulty difficulty, Random rng )
	{
		var min = difficulty switch
		{
			Difficulty.Easy => 1.0f,
			Difficulty.Expert => 4.0f,
			_ => 1.8f
		};
		var max = difficulty switch
		{
			Difficulty.Easy => 3.5f,
			Difficulty.Expert => 9.0f,
			_ => 6.0f
		};
		return min + rng.NextSingle() * (max - min);
	}

	public static float RollStealDelay( Random rng ) => 0.8f + rng.NextSingle() * 2.5f;

	public static bool ShouldAttemptBuzz( Difficulty difficulty, Random rng )
	{
		var chance = difficulty switch
		{
			Difficulty.Easy => 0.88f,
			Difficulty.Medium => 0.72f,
			Difficulty.Hard => 0.58f,
			Difficulty.Expert => 0.42f,
			_ => 0.65f
		};
		return rng.NextSingle() <= chance;
	}

	public static bool ShouldAnswerCorrectly( Difficulty difficulty, Random rng )
	{
		var chance = difficulty switch
		{
			Difficulty.Easy => 0.92f,
			Difficulty.Medium => 0.78f,
			Difficulty.Hard => 0.62f,
			Difficulty.Expert => 0.48f,
			_ => 0.7f
		};
		return rng.NextSingle() <= chance;
	}

	public static bool ShouldAttemptSteal( Difficulty difficulty, Random rng ) =>
		rng.NextSingle() <= 0.35f + (int)difficulty * 0.04f;

	public static string PickAnswer( TriviaQuestion question, bool correct, Random rng )
	{
		if ( question is null ) return "";

		if ( correct )
		{
			var answers = question.AllAnswers().ToList();
			return answers.Count > 0 ? answers[rng.Next( answers.Count )] : "";
		}

		return PickPlausibleWrongAnswer( question, rng );
	}

	private static string PickPlausibleWrongAnswer( TriviaQuestion question, Random rng )
	{
		var wrongPool = new[]
		{
			"Unknown", "Not sure", "Pass", "Skip", "No idea",
			"Maybe", "Something else", "I don't know"
		};
		return wrongPool[rng.Next( wrongPool.Length )];
	}
}
