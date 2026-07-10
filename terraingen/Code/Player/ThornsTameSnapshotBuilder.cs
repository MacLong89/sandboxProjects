namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;

public static class ThornsTameSnapshotBuilder
{
	public static ThornsTamesSnapshotDto BuildForAccount( Scene scene, string accountKey )
	{
		var dto = new ThornsTamesSnapshotDto();
		if ( string.IsNullOrEmpty( accountKey ) || scene is null )
			return dto;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.TamedOwnerAccountKey != accountKey )
				continue;

			ThornsAnimalSpeciesRegistry.TryGet( brain.SpeciesId, out var species );
			var key = species?.Key ?? "";
			var speciesName = species?.DisplayName ?? "Tame";
			var displayName = string.IsNullOrWhiteSpace( brain.TamedDisplayName ) ? speciesName : brain.TamedDisplayName;
			var maxHealth = brain.MaxHealth > 0f ? brain.MaxHealth : species?.BaseHealth ?? 100f;
			var moveSpeed = brain.SpawnSpeed > 0f
				? brain.SpawnSpeed
				: brain.ReplicatedMoveSpeed > 0f ? brain.ReplicatedMoveSpeed : species?.BaseSpeed ?? 280f;
			var geneticSpeciesIds = ParseSpeciesIds( brain.GeneticSpeciesIdsCsv, brain.SpeciesId );
			var geneticSpeciesNames = geneticSpeciesIds
				.Select( id => ThornsAnimalSpeciesRegistry.TryGet( id, out var s ) ? s.DisplayName : $"Species {id}" )
				.ToList();
			var traits = BuildTraits( key, brain.GeneticTraitIdsCsv, brain.IsCrossbreed, brain.IsMutatedBreed );
			var resolvedVisual = ThornsAnimalModelResolve.ResolveForBrain( brain );

			dto.Tames.Add( new ThornsTameListEntryDto
			{
				EntityId = brain.GameObject.Id,
				SpeciesId = brain.SpeciesId,
				SpeciesKey = key,
				SpeciesName = speciesName,
				Tier = Math.Clamp( brain.BreedTier > 0 ? brain.BreedTier : species?.TameTier ?? 1, 1, 5 ),
				IsCrossbreed = brain.IsCrossbreed,
				IsMutated = brain.IsMutatedBreed,
				IsSuperCrossbreed = brain.BreedTier >= 5 && geneticSpeciesIds.Count >= ThornsAnimalSpeciesRegistry.All.Count,
				GeneticSpeciesIds = geneticSpeciesIds,
				GeneticSpeciesNames = geneticSpeciesNames,
				DisplayName = displayName,
				PortraitPath = ThornsTameCatalog.CreaturePortraitPath( key ),
				ModelPath = string.IsNullOrWhiteSpace( resolvedVisual.ModelPath ) ? species?.ModelPath ?? "" : resolvedVisual.ModelPath,
				AnimPrefix = string.IsNullOrWhiteSpace( resolvedVisual.AnimPrefix ) ? species?.AnimPrefix ?? "" : resolvedVisual.AnimPrefix,
				Level = Math.Max( 1, brain.TameLevel ),
				CurrentExperience = Math.Max( 0, brain.TameExperience ),
				ExperienceToNextLevel = Math.Max( 1, brain.TameExperienceToNextLevel ),
				UnspentStatPoints = Math.Max( 0, brain.UnspentStatPoints ),
				StatStrength = Math.Max( 0, brain.StatStrength ),
				StatDefense = Math.Max( 0, brain.StatDefense ),
				StatStamina = Math.Max( 0, brain.StatStamina ),
				StatAgility = Math.Max( 0, brain.StatAgility ),
				StatIntelligence = Math.Max( 0, brain.StatIntelligence ),
				CurrentHealth = brain.CurrentHealth,
				MaxHealth = maxHealth,
				Attack = brain.SpawnDamage > 0f ? brain.SpawnDamage : species?.BaseDamage ?? 10f,
				SpeedPercent = ThornsTameCatalog.SpeedPercent( moveSpeed ),
				Perception = ThornsTameCatalog.PerceptionScore( brain.DetectionRangeForUi ),
				Traits = traits,
				ActiveCommand = ThornsTameCommandHost.GetCommand( brain.GameObject.Id ),
				BreedCooldownUntilUtcTicks = brain.BreedCooldownUntilUtcTicks
			} );
		}

		if ( dto.Tames.Count > 0 && dto.SelectedEntityId == Guid.Empty )
			dto.SelectedEntityId = dto.Tames[0].EntityId;

		return dto;
	}

	static List<ushort> ParseSpeciesIds( string csv, ushort fallback )
	{
		var ids = new List<ushort>();
		foreach ( var part in (csv ?? "").Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( ushort.TryParse( part, out var id ) && id > 0 && !ids.Contains( id ) )
				ids.Add( id );
		}

		if ( ids.Count == 0 && fallback > 0 )
			ids.Add( fallback );

		ids.Sort();
		return ids;
	}

	static List<ThornsTameTraitDto> BuildTraits( string speciesKey, string traitCsv, bool isCrossbreed, bool isMutated )
	{
		var traits = new Dictionary<string, ThornsTameTraitDto>( StringComparer.OrdinalIgnoreCase );
		foreach ( var trait in ThornsTameCatalog.GetTraitsForSpecies( speciesKey ) )
			traits[trait.Id] = trait;

		foreach ( var id in (traitCsv ?? "").Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ) )
		{
			if ( traits.ContainsKey( id ) )
				continue;

			traits[id] = new ThornsTameTraitDto
			{
				Id = id,
				Title = FormatTraitTitle( id ),
				Description = "Inherited through breeding.",
				IconPath = ThornsTameCatalog.TraitIconPath( id )
			};
		}

		if ( isCrossbreed )
		{
			traits["crossbreed"] = new ThornsTameTraitDto
			{
				Id = "crossbreed",
				Title = "Crossbreed",
				Description = "Carries traits and lineage from multiple species.",
				IconPath = ThornsTameCatalog.TraitIconPath( "crossbreed" )
			};
		}

		if ( isMutated )
		{
			traits["mutation"] = new ThornsTameTraitDto
			{
				Id = "mutation",
				Title = "Mutation",
				Description = "A mutation pushed this tame beyond normal inheritance.",
				IconPath = ThornsTameCatalog.TraitIconPath( "mutation" )
			};
		}

		return traits.Values.OrderBy( t => t.Title ).ToList();
	}

	static string FormatTraitTitle( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return "Inherited Trait";

		return string.Join( " ", id.Split( '_', StringSplitOptions.RemoveEmptyEntries )
			.Select( p => char.ToUpperInvariant( p[0] ) + p[1..] ) );
	}
}
