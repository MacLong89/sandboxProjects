namespace ThinkDrink.Services;

/// <summary>
/// Random weighted question selection that excludes questions already used in the current session.
/// </summary>
public sealed class QuestionSelectionService : IQuestionSelector
{
	public TriviaQuestion SelectNext(
		IReadOnlyList<TriviaQuestion> pool,
		IReadOnlyCollection<string> recentIds,
		string preferredCategory,
		Random random )
	{
		if ( pool is null || pool.Count == 0 )
			return null;

		var used = recentIds ?? Array.Empty<string>();
		var candidates = new List<(TriviaQuestion q, float weight)>( pool.Count );

		foreach ( var q in pool )
		{
			if ( used.Contains( q.Id ) )
				continue;

			var weight = 1f;

			if ( !string.IsNullOrEmpty( preferredCategory ) &&
				 string.Equals( q.Category, preferredCategory, StringComparison.OrdinalIgnoreCase ) )
				weight *= 1.35f;

			weight *= q.Difficulty switch
			{
				Difficulty.Easy => 1.1f,
				Difficulty.Medium => 1f,
				Difficulty.Hard => 0.85f,
				Difficulty.Expert => 0.7f,
				_ => 1f
			};

			if ( weight > 0.001f )
				candidates.Add( (q, weight) );
		}

		if ( candidates.Count == 0 )
			return pool[random.Next( pool.Count )];

		var total = 0f;
		for ( var i = 0; i < candidates.Count; i++ )
			total += candidates[i].weight;

		var roll = random.NextSingle() * total;
		var acc = 0f;

		for ( var i = 0; i < candidates.Count; i++ )
		{
			acc += candidates[i].weight;
			if ( roll <= acc )
				return candidates[i].q;
		}

		return candidates[^1].q;
	}

	public static string PickCategory( IReadOnlyList<string> categories, Random random )
	{
		if ( categories is null || categories.Count == 0 )
			return "General";
		return categories[random.Next( categories.Count )];
	}

	public static int PointsForDifficulty( Difficulty difficulty, RandomEventType evt )
	{
		var basePoints = difficulty switch
		{
			Difficulty.Easy => 1,
			Difficulty.Medium => 2,
			Difficulty.Hard => 3,
			Difficulty.Expert => 4,
			_ => 1
		};

		if ( evt == RandomEventType.DoublePoints )
			basePoints *= 2;

		return basePoints;
	}

	public static int XpForDifficulty( Difficulty difficulty )
	{
		return difficulty switch
		{
			Difficulty.Easy => GameConstants.BaseXpPerCorrect,
			Difficulty.Medium => GameConstants.BaseXpPerCorrect + 10,
			Difficulty.Hard => GameConstants.BaseXpPerCorrect + 25,
			Difficulty.Expert => GameConstants.BaseXpPerCorrect + 45,
			_ => GameConstants.BaseXpPerCorrect
		};
	}
}
