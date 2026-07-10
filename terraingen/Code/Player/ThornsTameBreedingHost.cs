namespace Terraingen.Player;

using Terraingen.Animals;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Victory;

public static class ThornsTameBreedingHost
{
	const int BreedXp = 150;

	public static bool TryBreed( Scene scene, string ownerAccountKey, ThornsPlayerGameplay gameplay, ThornsTameBreedRequest req, out string message )
	{
		message = "";
		if ( scene is null || !scene.IsValid() || gameplay is null || !gameplay.IsValid() || string.IsNullOrWhiteSpace( ownerAccountKey ) || req is null )
			return false;

		if ( req.ParentAEntityId == Guid.Empty || req.ParentBEntityId == Guid.Empty || req.ParentAEntityId == req.ParentBEntityId )
		{
			message = "Select two different tames.";
			return false;
		}

		if ( CountOwnedTames( ownerAccountKey ) >= ThornsTameCatalog.MaxTameSlots )
		{
			message = "Tame stable is full.";
			return false;
		}

		var parentA = FindOwnedTame( req.ParentAEntityId, ownerAccountKey );
		var parentB = FindOwnedTame( req.ParentBEntityId, ownerAccountKey );
		if ( !parentA.IsValid() || !parentB.IsValid() )
		{
			message = "Both parents must be living tames you own.";
			return false;
		}

		if ( parentA.IsOnBreedCooldown )
		{
			message = $"{FormatParentName( parentA )} must recover before breeding again ({ThornsTameCatalog.FormatBreedCooldownRemaining( parentA.BreedCooldownUntilUtcTicks )}).";
			return false;
		}

		if ( parentB.IsOnBreedCooldown )
		{
			message = $"{FormatParentName( parentB )} must recover before breeding again ({ThornsTameCatalog.FormatBreedCooldownRemaining( parentB.BreedCooldownUntilUtcTicks )}).";
			return false;
		}

		if ( !ThornsAnimalSpeciesRegistry.TryGet( parentA.SpeciesId, out var speciesA )
		     || !ThornsAnimalSpeciesRegistry.TryGet( parentB.SpeciesId, out var speciesB ) )
		{
			message = "Parent species data missing.";
			return false;
		}

		var childSpecies = ChooseChildBodySpecies( parentA, parentB, speciesA, speciesB );
		var genetics = BuildChildProfile( parentA, parentB, childSpecies );
		var spawnPos = ResolveChildSpawnPosition( scene, gameplay.GameObject.WorldPosition, childSpecies );
		var child = ThornsAnimalFactory.HostSpawnTameRestore(
			scene,
			childSpecies,
			spawnPos,
			Rotation.FromYaw( Game.Random.Float( 0f, 360f ) ) );
		if ( !child.IsValid() )
		{
			message = "Breeding failed to create offspring.";
			return false;
		}

		child.HostApplyBredTame(
			ownerAccountKey,
			genetics.DisplayName,
			genetics.Tier,
			genetics.Health,
			genetics.Attack,
			genetics.Speed,
			genetics.DetectionRange,
			genetics.SpeciesIds,
			genetics.TraitIds,
			genetics.IsCrossbreed,
			genetics.IsMutated );

		parentA.HostStartBreedCooldown();
		parentB.HostStartBreedCooldown();

		gameplay.HostGrantXp( BreedXp );
		gameplay.HostPushTameFeedToOwner( childSpecies.Key, genetics.DisplayName, genetics.Tier );
		gameplay.PushMilestoneToastToOwner( $"Bred {genetics.DisplayName}", BreedXp );
		AwardApexProgress( gameplay, genetics );
		gameplay.HostRebuildTamesFromWorld();
		ThornsWorldPersistence.Instance?.TryHostSaveNow();

		message = $"Bred {genetics.DisplayName}.";
		return true;
	}

	static ThornsAnimalBrain FindOwnedTame( Guid entityId, string ownerAccountKey )
	{
		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || brain.IsDead || !brain.IsTamed )
				continue;
			if ( brain.GameObject.Id != entityId || brain.TamedOwnerAccountKey != ownerAccountKey )
				continue;
			return brain;
		}

		return null;
	}

	static int CountOwnedTames( string ownerAccountKey )
	{
		var count = 0;
		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( brain.IsValid() && !brain.IsDead && brain.IsTamed && brain.TamedOwnerAccountKey == ownerAccountKey )
				count++;
		}

		return count;
	}

	static ThornsAnimalSpeciesData ChooseChildBodySpecies(
		ThornsAnimalBrain parentA,
		ThornsAnimalBrain parentB,
		ThornsAnimalSpeciesData speciesA,
		ThornsAnimalSpeciesData speciesB )
	{
		if ( parentA.SpeciesId == parentB.SpeciesId )
			return speciesA;

		var scoreA = parentA.BreedTier * 1000f + parentA.MaxHealth + parentA.SpawnDamage * 12f + parentA.SpawnSpeed * 0.2f;
		var scoreB = parentB.BreedTier * 1000f + parentB.MaxHealth + parentB.SpawnDamage * 12f + parentB.SpawnSpeed * 0.2f;
		return scoreA >= scoreB ? speciesA : speciesB;
	}

	static BreedProfile BuildChildProfile( ThornsAnimalBrain a, ThornsAnimalBrain b, ThornsAnimalSpeciesData childSpecies )
	{
		var speciesIds = ParseSpeciesIds( a.GeneticSpeciesIdsCsv, a.SpeciesId )
			.Concat( ParseSpeciesIds( b.GeneticSpeciesIdsCsv, b.SpeciesId ) )
			.Distinct()
			.OrderBy( id => id )
			.ToList();

		var traitIds = ParseTraitIds( a.GeneticTraitIdsCsv )
			.Concat( ParseTraitIds( b.GeneticTraitIdsCsv ) )
			.Concat( ThornsTameCatalog.GetTraitsForSpecies( childSpecies.Key ).Select( t => t.Id ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();

		var sameSpecies = a.SpeciesId == b.SpeciesId && speciesIds.Count == 1;
		var isCrossbreed = speciesIds.Count > 1;
		var mutationRoll = Game.Random.Float( 0f, 1f );
		var isMutated = mutationRoll < 0.25f;
		var majorMutation = mutationRoll < 0.05f;

		if ( isMutated )
			traitIds.Add( majorMutation ? "major_mutation" : "mutation" );

		var tier = Math.Max( a.BreedTier, b.BreedTier );
		if ( sameSpecies )
			tier++;
		if ( isCrossbreed && speciesIds.Count >= 3 )
			tier++;
		if ( majorMutation )
			tier++;
		tier = Math.Clamp( tier, 1, 5 );

		var crossMul = 1f + Math.Max( 0, speciesIds.Count - 1 ) * 0.12f;
		var tierMul = 1f + Math.Max( 0, tier - 1 ) * 0.08f;
		var mutationMul = majorMutation ? 1.55f : isMutated ? 1.25f : 1f;
		var sameSpeciesMul = sameSpecies ? 1.12f : 1f;
		var mul = crossMul * tierMul * mutationMul * sameSpeciesMul;

		var health = Math.Max( a.MaxHealth, b.MaxHealth ) * mul;
		var attack = Math.Max( a.SpawnDamage, b.SpawnDamage ) * mul;
		if ( attack <= 0f && isCrossbreed )
			attack = Math.Max( 6f, (a.SpawnDamage + b.SpawnDamage) * 0.5f + 8f );
		var speed = Math.Max( a.SpawnSpeed, b.SpawnSpeed ) * Math.Min( 2.2f, 1f + (mul - 1f) * 0.45f );
		var detection = Math.Max( a.DetectionRangeForUi, b.DetectionRangeForUi ) * Math.Min( 2.5f, 1f + (mul - 1f) * 0.35f );

		var lineageLabel = speciesIds.Count > 1
			? string.Join( "/", speciesIds.Select( SpeciesShortName ) )
			: childSpecies.DisplayName;
		var prefix = majorMutation ? "Mutant " : isMutated ? "Mutated " : "";
		var displayName = $"{prefix}{lineageLabel}";
		if ( displayName.Length > 32 )
			displayName = displayName[..32];

		return new BreedProfile(
			displayName,
			tier,
			health,
			attack,
			speed,
			detection,
			speciesIds,
			traitIds,
			isCrossbreed,
			isMutated );
	}

	static Vector3 ResolveChildSpawnPosition( Scene scene, Vector3 ownerPos, ThornsAnimalSpeciesData species )
	{
		var requested = ownerPos + Vector3.Random.WithZ( 0f ).Normal * Game.Random.Float( 96f, 180f );
		if ( ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( scene, requested, Math.Max( 240f, species.WanderRadius ), out var pos, out _ ) )
			return pos;

		return ownerPos + Vector3.Forward * 120f;
	}

	static void AwardApexProgress( ThornsPlayerGameplay gameplay, BreedProfile profile )
	{
		var victory = ThornsVictoryManager.EnsureInstance();
		if ( victory is null )
			return;

		victory.HostReportSource( gameplay.AccountKey, "breeding_milestone" );
		victory.HostReportSource( gameplay.AccountKey, $"apex_breed_tier_{Math.Clamp( profile.Tier, 1, 5 )}" );
		if ( profile.SpeciesIds.Count >= 2 )
			victory.HostReportSource( gameplay.AccountKey, $"apex_crossbreed_{Math.Clamp( profile.SpeciesIds.Count, 2, 4 )}" );
		if ( profile.IsMutated )
			victory.HostReportSource( gameplay.AccountKey, "apex_mutation" );

		var allSpeciesCount = ThornsAnimalSpeciesRegistry.All.Count;
		if ( profile.Tier >= 5 && profile.SpeciesIds.Count >= allSpeciesCount )
			victory.HostReportSource( gameplay.AccountKey, "apex_super_crossbreed" );

		gameplay.HostPushVictorySnapshot();
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

		return ids;
	}

	static List<string> ParseTraitIds( string csv ) =>
		(csv ?? "").Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Where( id => !string.IsNullOrWhiteSpace( id ) )
			.Select( id => id.Trim().ToLowerInvariant() )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();

	static string SpeciesShortName( ushort id ) =>
		ThornsAnimalSpeciesRegistry.TryGet( id, out var species ) ? species.DisplayName : $"S{id}";

	static string FormatParentName( ThornsAnimalBrain parent )
	{
		if ( !string.IsNullOrWhiteSpace( parent.TamedDisplayName ) )
			return parent.TamedDisplayName.Trim();

		return ThornsAnimalSpeciesRegistry.TryGet( parent.SpeciesId, out var species )
			? species.DisplayName
			: "Tame";
	}

	readonly record struct BreedProfile(
		string DisplayName,
		int Tier,
		float Health,
		float Attack,
		float Speed,
		float DetectionRange,
		List<ushort> SpeciesIds,
		List<string> TraitIds,
		bool IsCrossbreed,
		bool IsMutated );
}
