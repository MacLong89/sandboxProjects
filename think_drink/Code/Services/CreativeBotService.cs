namespace ThinkDrink.Services;

public static class CreativeBotService
{
	static readonly string[] Quips =
	{
		"My emotional support crocodile",
		"Surprise! I never learned to read.",
		"Has anyone seen my personality?",
		"I brought my own microwaved fish",
		"Plot twist: I am the problem",
		"A PowerPoint about my ex",
		"Live bees. Just bees.",
		"My collection of haunted dolls",
		"Unfiltered honesty and a kazoo",
		"I would like to speak to the manager of gravity"
	};

	public static string PickQuip( Random random ) => Quips[random.Next( Quips.Length )];

	public static string PickVoteLetter( IReadOnlyList<string> letters, string botAuthorLetter, Random random )
	{
		var options = letters.Where( l => !string.Equals( l, botAuthorLetter, StringComparison.OrdinalIgnoreCase ) ).ToList();
		if ( options.Count == 0 && letters.Count > 0 )
			return letters[random.Next( letters.Count )];

		return options[random.Next( options.Count )];
	}
}
