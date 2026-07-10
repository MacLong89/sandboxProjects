namespace ThinkDrink;

/// <summary>Cosmetic titles unlocked by level — display-only progression hooks.</summary>
public static class ProgressionTitles
{
	public static string GetTitle( int level ) => level switch
	{
		>= 50 => "Trivia Legend",
		>= 30 => "Quiz Master",
		>= 20 => "Brainiac",
		>= 10 => "Contender",
		>= 5 => "Regular",
		>= 2 => "Newcomer",
		_ => "Rookie"
	};

	public static string GetTitleForXp( int xp ) => GetTitle( GameConstants.LevelFromXp( xp ) );
}
