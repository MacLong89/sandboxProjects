namespace Fauna2;

/// <summary>Early-goal helpers: habitat recommendations from catch biome, tutorial checks.</summary>
public static class StarterGoalGuide
{
	public static Biome StarterBiome => ZooState.Instance?.StarterBiome ?? Biome.Grassland;

	/// <summary>Biome to build for — prefer carried catch, else starter pack biome.</summary>
	public static Biome RecommendedHabitatBiome()
	{
		var carried = PlayerInventory.Local?.FirstCarriedSpecies();
		if ( !string.IsNullOrEmpty( carried ) )
		{
			var def = Defs.Animal( carried );
			if ( def is not null )
				return def.Biome;
		}

		return StarterBiome;
	}

	public static PlaceableDefinition RecommendedHabitat( Biome? biome = null )
	{
		biome ??= RecommendedHabitatBiome();
		return Defs.Placeables
			.Where( p => p.IsHabitat && p.HabitatBiome == biome )
			.Where( p => AnimalHabitatRules.GetSizeTier( p.HabitatSize ) == HabitatSizeTier.Small )
			.OrderBy( p => p.Cost )
			.FirstOrDefault()
			?? Defs.Placeables
				.Where( p => p.IsHabitat )
				.Where( p => AnimalHabitatRules.GetSizeTier( p.HabitatSize ) == HabitatSizeTier.Small )
				.OrderBy( p => p.Cost )
				.FirstOrDefault();
	}

	/// <summary>Canonical early small-habitat species for a biome.</summary>
	public static AnimalDefinition RecommendedAnimal( Biome? biome = null )
	{
		biome ??= RecommendedHabitatBiome();
		return Defs.Animals
			.Where( a => a.Biome == biome && a.MinHabitatSize == HabitatSizeTier.Small )
			.OrderBy( a => a.UnlockLevel )
			.ThenBy( a => a.Cost )
			.FirstOrDefault();
	}

	/// <summary>Species the player can adopt right now for this biome.</summary>
	public static AnimalDefinition RecommendedAdoptableAnimal( Biome? biome = null )
	{
		biome ??= RecommendedHabitatBiome();
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

		biome ??= RecommendedHabitatBiome();
		return def.Biome == biome
			&& def.MinHabitatSize == HabitatSizeTier.Small
			&& BuildValidation.IsUnlocked( def );
	}

	/// <summary>Tutorial habitat goal — any placed habitat counts.</summary>
	public static bool HasTutorialHabitat() => HabitatRegistry.Count > 0;

	/// <summary>Tutorial place goal — any animal living in a habitat.</summary>
	public static bool HasAnimalInHabitat()
	{
		foreach ( var animal in AnimalRegistry.All )
		{
			if ( animal.IsValid() && animal.Habitat is not null )
				return true;
		}

		return false;
	}

	/// <summary>Find goal — near a wild animal, or already caught (skip ahead).</summary>
	public static bool HasSpottedWildAnimal()
	{
		if ( (ZooState.Instance?.TotalAnimalsCaught ?? 0) > 0 )
			return true;

		var player = PlayerState.Local;
		if ( player is null || !player.IsValid() )
			return false;

		var pos = player.FeetPosition;
		var range = GameConstants.WildAnimalInteractRange * 1.75f;

		foreach ( var wild in WildAnimalRegistry.All )
		{
			if ( !wild.IsValid() || wild.Fled )
				continue;

			if ( (wild.GameObject.WorldPosition - pos).Length <= range )
				return true;
		}

		return false;
	}

	public static bool HasStarterSmallHabitat( Biome? biome = null ) => HasTutorialHabitat();

	public static bool HasStarterAnimalPlaced( Biome? biome = null ) => HasAnimalInHabitat();

	public static string BiomeLabel( Biome? biome = null ) =>
		Fauna2.UI.UiFormat.BiomeLabel( biome ?? RecommendedHabitatBiome() );

	public static string HabitatGoalTitle()
	{
		var biome = RecommendedHabitatBiome();
		var carried = PlayerInventory.Local?.FirstCarriedSpecies();
		if ( !string.IsNullOrEmpty( carried ) )
		{
			var animal = Defs.Animal( carried );
			if ( animal is not null )
				return $"Build a habitat for your {animal.DisplayName}";
		}

		return $"Build a {BiomeLabel( biome )} habitat";
	}

	public static string AnimalGoalTitle() =>
		$"Adopt a {BiomeLabel()} animal";

	public static string HabitatGoalDescription()
	{
		var habitat = RecommendedHabitat();
		var carried = PlayerInventory.Local?.FirstCarriedSpecies();
		var animal = !string.IsNullOrEmpty( carried ) ? Defs.Animal( carried ) : null;

		if ( animal is not null && habitat is not null )
			return $"Open Build (B) and place a {BiomeLabel( animal.Biome )} habitat — {habitat.DisplayName} fits your {animal.DisplayName}.";

		if ( habitat is not null )
			return $"Open Build (B) and place a habitat — {habitat.DisplayName} is a good start. Any habitat completes this goal.";

		return "Open Build (B) and place any habitat for your animals.";
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

	public static string HabitatProgressLabel() =>
		HasTutorialHabitat() ? "Habitat placed" : "Not placed";

	public static string AnimalProgressLabel() =>
		HasAnimalInHabitat() ? "Animal in habitat" : "Not housed yet";
}
