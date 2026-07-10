namespace Fauna2;

/// <summary>Starter-biome habitat + species pairing for early goals.</summary>
public static class StarterGoalGuide
{
	public static Biome StarterBiome => ZooState.Instance?.StarterBiome ?? Biome.Grassland;

	public static PlaceableDefinition RecommendedHabitat( Biome? biome = null )
	{
		biome ??= StarterBiome;
		return Defs.Placeables
			.Where( p => p.IsHabitat && p.HabitatBiome == biome )
			.Where( p => AnimalHabitatRules.GetSizeTier( p.HabitatSize ) == HabitatSizeTier.Small )
			.OrderBy( p => p.Cost )
			.FirstOrDefault();
	}

	/// <summary>Canonical early small-habitat species for a starter biome.</summary>
	public static AnimalDefinition RecommendedAnimal( Biome? biome = null )
	{
		biome ??= StarterBiome;
		return Defs.Animals
			.Where( a => a.Biome == biome && a.MinHabitatSize == HabitatSizeTier.Small )
			.OrderBy( a => a.UnlockLevel )
			.ThenBy( a => a.Cost )
			.FirstOrDefault();
	}

	/// <summary>Species the player can adopt right now for this biome.</summary>
	public static AnimalDefinition RecommendedAdoptableAnimal( Biome? biome = null )
	{
		biome ??= StarterBiome;
		return Defs.Animals
			.Where( a => a.Biome == biome && a.MinHabitatSize == HabitatSizeTier.Small )
			.Where( a => BuildValidation.IsUnlocked( a ) )
			.OrderBy( a => a.UnlockLevel )
			.ThenBy( a => a.Cost )
			.FirstOrDefault()
			?? RecommendedAnimal( biome );
	}

	public static bool IsValidStarterAdopt( AnimalDefinition def, Biome? biome = null )
	{
		if ( def is null )
			return false;

		biome ??= StarterBiome;
		return def.Biome == biome
			&& def.MinHabitatSize == HabitatSizeTier.Small
			&& BuildValidation.IsUnlocked( def );
	}

	public static bool HasStarterSmallHabitat( Biome? biome = null )
	{
		biome ??= StarterBiome;
		foreach ( var habitat in HabitatRegistry.All )
		{
			if ( habitat.Biome != biome )
				continue;

			if ( AnimalHabitatRules.GetSizeTier( habitat.Size ) == HabitatSizeTier.Small )
				return true;
		}

		return false;
	}

	public static bool HasStarterAnimalPlaced( Biome? biome = null )
	{
		biome ??= StarterBiome;
		foreach ( var animal in AnimalRegistry.All )
		{
			var def = animal.Definition;
			if ( def is null )
				continue;

			var habitat = animal.Habitat;
			if ( habitat is null )
				continue;

			if ( habitat.Biome != biome )
				continue;

			if ( AnimalHabitatRules.GetSizeTier( habitat.Size ) != HabitatSizeTier.Small )
				continue;

			if ( !AnimalHabitatRules.CanHouse( habitat, def, out _ ) )
				continue;

			return true;
		}

		return false;
	}

	public static string BiomeLabel( Biome? biome = null ) =>
		Fauna2.UI.UiFormat.BiomeLabel( biome ?? StarterBiome );

	public static string HabitatGoalTitle() =>
		$"Build a small {BiomeLabel()} habitat";

	public static string AnimalGoalTitle() =>
		$"Adopt a {BiomeLabel()} animal";

	public static string HabitatGoalDescription()
	{
		var habitat = RecommendedHabitat();
		var animal = RecommendedAdoptableAnimal();
		var example = animal is not null ? $" A {animal.DisplayName} fits well." : "";

		if ( habitat is null )
			return $"Open Build (B) and place any small {BiomeLabel()} habitat.{example}";

		return $"Open Build (B) and place a small {BiomeLabel()} habitat — {habitat.DisplayName} works.{example}";
	}

	public static string AnimalGoalDescription()
	{
		var animal = RecommendedAdoptableAnimal();
		var habitat = RecommendedHabitat();
		if ( animal is null )
			return $"Open Animals (N) and adopt any small {BiomeLabel()} species into your habitat.";

		var cost = AnimalSystem.Instance?.GetPurchaseCost( animal ) ?? animal.Cost;
		var freeLine = cost == 0 ? " Your first matching adoption is free." : "";
		var habitatLine = habitat is not null
			? $" Place it in your {habitat.DisplayName}."
			: $" Place it in your small {BiomeLabel()} habitat.";

		return $"Open Animals (N) and adopt a {BiomeLabel()} animal (e.g. {animal.DisplayName}).{freeLine}{habitatLine}";
	}

	public static string HabitatProgressLabel()
	{
		if ( HasStarterSmallHabitat() )
			return $"Small {BiomeLabel()} habitat placed";

		return "Not placed";
	}

	public static string AnimalProgressLabel()
	{
		if ( HasStarterAnimalPlaced() )
			return "Animal in habitat";

		return "0/1 in starter habitat";
	}
}
