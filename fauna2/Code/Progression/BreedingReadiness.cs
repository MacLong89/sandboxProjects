namespace Fauna2;

/// <summary>Explains why an animal can or cannot breed — used by inspect UI and alerts.</summary>
public static class BreedingReadiness
{
	public static IReadOnlyList<string> GetIssues( AnimalComponent animal )
	{
		var issues = new List<string>();
		if ( animal?.Definition is null ) return issues;

		if ( !animal.IsAdult )
			issues.Add( "Still growing — must be an adult" );
		if ( animal.IsElder )
			issues.Add( "Too old to breed" );
		var firstBreed = BreedingSystem.FirstBreedPending;
		var requiredHappiness = firstBreed
			? MathF.Max( 50f, animal.Definition.BreedingHappiness - 10f )
			: animal.Definition.BreedingHappiness;
		if ( animal.Happiness < requiredHappiness )
			issues.Add( $"Happiness {animal.Happiness:0}% — needs {requiredHappiness:0}%" );

		var habitat = animal.Habitat;
		if ( habitat is null )
		{
			issues.Add( "Not in a habitat" );
		}
		else
		{
			var requiredScore = firstBreed ? 45f : GameConstants.BreedMinHabitatScore;
			if ( habitat.Score < requiredScore )
				issues.Add( $"Habitat quality {habitat.Score:0} — needs {requiredScore:0}" );
		}

		if ( animal.TimeSinceBred < GameConstants.BreedCooldownDuration )
		{
			var left = GameConstants.BreedCooldownDuration - animal.TimeSinceBred;
			issues.Add( $"Breeding cooldown — {left:0}s remaining" );
		}

		return issues;
	}

	public static bool IsReady( AnimalComponent animal ) => GetIssues( animal ).Count == 0;

	public static string CooldownText( AnimalComponent animal )
	{
		if ( animal.TimeSinceBred >= GameConstants.BreedCooldownDuration )
			return "Ready";

		var left = GameConstants.BreedCooldownDuration - animal.TimeSinceBred;
		return $"{left:0}s";
	}
}
