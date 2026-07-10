namespace ThinkDrink.Data;

/// <summary>Built-in fallback questions if JSON fails to load.</summary>
internal static class FallbackQuestions
{
	public static List<TriviaQuestion> Create() => new()
	{
		Q( "fb_geo_1", "Geography", Difficulty.Easy, "What is the capital of France?", "Paris" ),
		Q( "fb_sci_1", "Science", Difficulty.Easy, "What planet is known as the Red Planet?", "Mars" ),
		Q( "fb_mov_1", "Movies", Difficulty.Medium, "Who directed Jaws?", "Steven Spielberg", "Spielberg" ),
		Q( "fb_gam_1", "Gaming", Difficulty.Medium, "What company makes Mario?", "Nintendo" ),
		Q( "fb_spo_1", "Sports", Difficulty.Easy, "How many players on a soccer team on the field?", "11", "eleven" ),
		Q( "fb_ani_1", "Animals", Difficulty.Medium, "What is another name for a puma?", "Cougar", "Mountain Lion", "Panther" ),
		Q( "fb_foo_1", "Food", Difficulty.Easy, "What country is sushi from?", "Japan" ),
		Q( "fb_his_1", "History", Difficulty.Medium, "In what year did World War II end?", "1945" ),
	};

	private static TriviaQuestion Q( string id, string cat, Difficulty diff, string text, params string[] answers )
	{
		return new TriviaQuestion
		{
			Id = id,
			Category = cat,
			Difficulty = diff,
			Question = text,
			Accepted = new List<string> { answers[0] },
			Alternatives = answers.Skip( 1 ).ToList()
		};
	}
}
