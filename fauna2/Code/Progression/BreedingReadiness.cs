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
		if ( animal.Happiness < animal.Definition.BreedingHappiness )
			issues.Add( $"Happiness {animal.Happiness:0}% — needs {animal.Definition.BreedingHappiness:0}%" );

		var habitat = animal.Habitat;
		if ( habitat is null )
		{
			issues.Add( "Not in a habitat" );
		}
		else if ( habitat.Score < GameConstants.BreedMinHabitatScore )
		{
			issues.Add( $"Habitat quality {habitat.Score:0} — needs {GameConstants.BreedMinHabitatScore:0}" );
		}

		if ( animal.TimeSinceBred < GameConstants.BreedCooldownDuration )
		{
			var left = GameConstants.BreedCooldownDuration - animal.TimeSinceBred;
			issues.Add( $"Breeding cooldown — {left:0}s remaining" );
		}

		return issues;
	}

	public static bool IsReady( AnimalComponent animal ) => GetIssues( animal ).Count == 0;

	public static IReadOnlyList<string> GetPairIssues( AnimalComponent a, AnimalComponent b )
	{
		var issues = new List<string>();
		if ( a is null || b is null ) return issues;

		issues.AddRange( GetIssues( a ) );
		issues.AddRange( GetIssues( b ) );

		if ( a.DefinitionId != b.DefinitionId )
		{
			if ( !HybridSystem.CouldHybridize( a.Definition, b.Definition ) )
				issues.Add( "Different species — no hybrid recipe" );
			else if ( (ZooState.Instance?.Prestige ?? 0) < 25 )
				issues.Add( "Hybrid needs 25 prestige" );
		}

		var habitat = a.Habitat ?? b.Habitat;
		if ( habitat is not null && a.HabitatId != b.HabitatId )
			issues.Add( "Pair must share the same habitat" );

		if ( habitat is not null && a.Definition is not null && !habitat.HasRoomFor( a.Definition ) )
			issues.Add( "Habitat is full for offspring" );

		return issues.Distinct().ToList();
	}

	public static bool PairReady( AnimalComponent a, AnimalComponent b ) => GetPairIssues( a, b ).Count == 0;

	public static string CooldownText( AnimalComponent animal )
	{
		if ( animal.TimeSinceBred >= GameConstants.BreedCooldownDuration )
			return "Ready";

		var left = GameConstants.BreedCooldownDuration - animal.TimeSinceBred;
		return $"{left:0}s";
	}
}
