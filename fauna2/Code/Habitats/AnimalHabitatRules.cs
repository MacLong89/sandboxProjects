namespace Fauna2;

/// <summary>Minimum habitat footprint an animal requires.</summary>
public enum HabitatSizeTier
{
	Small,
	Large,
}

/// <summary>Biome and habitat size rules for placing animals.</summary>
public static class AnimalHabitatRules
{
	// Large habitats are 18×18 build tiles after the fence-margin expansion.
	public const float LargeAreaThreshold = 900_000f;

	public static HabitatSizeTier GetSizeTier( Vector2 size )
	{
		var area = size.x * size.y;
		if ( area >= LargeAreaThreshold )
			return HabitatSizeTier.Large;

		return HabitatSizeTier.Small;
	}

	public static string TierLabel( HabitatSizeTier tier ) => tier switch
	{
		HabitatSizeTier.Small => "small",
		HabitatSizeTier.Large => "large",
		_ => "small",
	};

	public static string TierLabelTitle( HabitatSizeTier tier ) => tier switch
	{
		HabitatSizeTier.Small => "Small",
		HabitatSizeTier.Large => "Large",
		_ => "Small",
	};

	public static bool MeetsSize( HabitatSizeTier habitatTier, HabitatSizeTier required ) =>
		habitatTier >= required;

	public static bool CanHouse( HabitatComponent habitat, AnimalDefinition def, out string error )
	{
		error = null;

		if ( habitat is null || def is null )
		{
			error = "Invalid habitat.";
			return false;
		}

		if ( habitat.Biome != def.Biome )
		{
			error = $"{def.DisplayName} needs a {BiomeIdentity.Label( def.Biome )} habitat.";
			return false;
		}

		var habitatTier = GetSizeTier( habitat.Size );
		if ( !MeetsSize( habitatTier, def.MinHabitatSize ) )
		{
			error = $"{def.DisplayName} needs at least a {TierLabelTitle( def.MinHabitatSize )} {BiomeIdentity.Label( def.Biome )} habitat.";
			return false;
		}

		return true;
	}

	public static string RequirementText( AnimalDefinition def )
	{
		if ( def is null )
			return "";

		return $"{BiomeIdentity.Label( def.Biome )} · {TierLabelTitle( def.MinHabitatSize )}+ habitat";
	}

	/// <summary>True when at least one buildable habitat satisfies this species' biome and size.</summary>
	public static bool HasBuildableHabitat( AnimalDefinition def, IEnumerable<PlaceableDefinition> placeables )
	{
		if ( def is null )
			return false;

		foreach ( var placeable in placeables )
		{
			if ( placeable is null || !placeable.IsHabitat || placeable.HabitatBiome != def.Biome )
				continue;

			if ( MeetsSize( GetSizeTier( placeable.HabitatSize ), def.MinHabitatSize ) )
				return true;
		}

		return false;
	}

	public static void LogMissingHabitatCoverage(
		IReadOnlyList<AnimalDefinition> animals,
		IReadOnlyList<PlaceableDefinition> placeables )
	{
		foreach ( var def in animals )
		{
			if ( HasBuildableHabitat( def, placeables ) )
				continue;

			Log.Warning( $"[Fauna] No buildable habitat for {def.DisplayName} ({RequirementText( def )})." );
		}

		LogHabitatsWithoutAnimals( animals, placeables );
	}

	/// <summary>Warn when a habitat type has no species that can legally live there.</summary>
	public static void LogHabitatsWithoutAnimals(
		IReadOnlyList<AnimalDefinition> animals,
		IReadOnlyList<PlaceableDefinition> placeables )
	{
		foreach ( var placeable in placeables.Where( p => p.IsHabitat ) )
		{
			var habitatTier = GetSizeTier( placeable.HabitatSize );
			var hasResident = animals.Any( a =>
				a.Biome == placeable.HabitatBiome
				&& MeetsSize( habitatTier, a.MinHabitatSize ) );

			if ( !hasResident )
			{
				Log.Warning(
					$"[Fauna] No animal fits {placeable.DisplayName} ({placeable.HabitatBiome} · {TierLabelTitle( habitatTier )}+)." );
			}

			if ( habitatTier == HabitatSizeTier.Small
				&& !animals.Any( a => a.Biome == placeable.HabitatBiome && a.MinHabitatSize == HabitatSizeTier.Small ) )
			{
				Log.Warning(
					$"[Fauna] No small-habitat species for {placeable.DisplayName} ({placeable.HabitatBiome})." );
			}
		}
	}
}
