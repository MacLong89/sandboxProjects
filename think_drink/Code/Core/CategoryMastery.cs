namespace ThinkDrink;

/// <summary>Category mastery labels from lifetime correct counts.</summary>
public static class CategoryMastery
{
	public static string GetBadge( int correct ) => correct switch
	{
		>= 100 => "Master",
		>= 50 => "Expert",
		>= 25 => "Regular",
		>= 10 => "Fan",
		>= 1 => "Rookie",
		_ => ""
	};

	public static IReadOnlyList<(string Category, int Count, string Badge)> GetTopCategories( PlayerProfile profile, int take = 4 )
	{
		if ( profile?.CategoryCorrect is null || profile.CategoryCorrect.Count == 0 )
			return Array.Empty<(string, int, string)>();

		return profile.CategoryCorrect
			.OrderByDescending( kv => kv.Value )
			.Take( take )
			.Select( kv => (kv.Key, kv.Value, GetBadge( kv.Value )) )
			.ToList();
	}
}
