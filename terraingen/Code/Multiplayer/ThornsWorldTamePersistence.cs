namespace Terraingen.Multiplayer;

using Terraingen;
using Terraingen.Animals;
using Terraingen.Core;
using Terraingen.Player;

/// <summary>Host capture/restore of tamed animals inside the per-server world save.</summary>
public static class ThornsWorldTamePersistence
{
	/// <summary>
	/// Snapshots live tamed animals into the world DTO.
	/// When <paramref name="forceReplace"/> is false and nothing is in the scene (e.g. scene teardown),
	/// existing saved tames are kept so quit/reload does not wipe them.
	/// Before tame restore runs, never clear loaded tames on an empty capture.
	/// </summary>
	public static int CaptureTames( ThornsPersistentWorldDto dto, bool forceReplace )
	{
		dto.Tames ??= new List<ThornsPersistentTameDto>();

		var captured = new List<ThornsPersistentTameDto>();

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || brain.IsDead || !brain.IsTamed )
				continue;

			if ( string.IsNullOrEmpty( brain.TamedOwnerAccountKey ) )
				continue;

			var pos = brain.GameObject.WorldPosition;
			var yaw = brain.GameObject.WorldRotation.Angles().yaw;
			captured.Add( new ThornsPersistentTameDto
			{
				OwnerAccountKey = brain.TamedOwnerAccountKey,
				SpeciesId = brain.SpeciesId,
				DisplayName = brain.TamedDisplayName,
				CurrentHealth = brain.CurrentHealth,
				MaxHealth = brain.MaxHealth,
				Attack = brain.SpawnDamage,
				MoveSpeed = brain.SpawnSpeed,
				DetectionRange = brain.DetectionRangeForUi,
				BreedTier = brain.BreedTier,
				TameLevel = brain.TameLevel,
				TameExperience = brain.TameExperience,
				UnspentStatPoints = brain.UnspentStatPoints,
				StatStrength = brain.StatStrength,
				StatDefense = brain.StatDefense,
				StatStamina = brain.StatStamina,
				StatAgility = brain.StatAgility,
				StatIntelligence = brain.StatIntelligence,
				IsCrossbreed = brain.IsCrossbreed,
				IsMutated = brain.IsMutatedBreed,
				GeneticSpeciesIdsCsv = brain.GeneticSpeciesIdsCsv,
				GeneticTraitIdsCsv = brain.GeneticTraitIdsCsv,
				Px = pos.x,
				Py = pos.y,
				Pz = pos.z,
				RYaw = yaw,
				BreedCooldownUntilUtcTicks = brain.BreedCooldownUntilUtcTicks
			} );
		}

		if ( captured.Count > 0 )
		{
			dto.Tames = captured;
			return captured.Count;
		}

		if ( !forceReplace )
			return dto.Tames.Count;

		if ( !ThornsAnimalManager.PersistedTamesRestoreAttempted && dto.Tames.Count > 0 )
			return dto.Tames.Count;

		if ( captured.Count == 0 && dto.Tames.Count > 0
		     && ThornsAnimalManager.PersistedTamesRestoreAttempted
		     && !ThornsAnimalManager.PersistedTamesRestoreHadSuccess )
			return dto.Tames.Count;

		dto.Tames = captured;
		return dto.Tames.Count;
	}

	public static void HostRestoreTames( Scene scene )
	{
		if ( scene is null || !scene.IsValid || !ThornsMultiplayer.IsHostOrOffline )
			return;

		var persistence = ThornsWorldPersistence.Instance;
		if ( persistence is null )
		{
			Log.Warning( "[Thorns Terrain] Tame restore skipped — no ThornsWorldPersistence instance." );
			return;
		}

		var tames = persistence.GetSavedTames();
		if ( tames.Count == 0 )
			return;

		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		ThornsPlayerRootCache.Refresh( scene );
		var restored = 0;
		var skipped = 0;
		var ringByOwner = new Dictionary<string, int>( StringComparer.Ordinal );

		foreach ( var dto in tames )
		{
			if ( dto is null || string.IsNullOrEmpty( dto.OwnerAccountKey ) )
			{
				skipped++;
				continue;
			}

			if ( !ThornsAnimalSpeciesRegistry.TryGet( dto.SpeciesId, out var species ) )
			{
				Log.Warning( $"[Thorns Terrain] Tame restore skipped — unknown species id {dto.SpeciesId}." );
				skipped++;
				continue;
			}

			Vector3 pos;
			var owner = ThornsPlayerRootCache.TryGetByAccountKey( scene, dto.OwnerAccountKey );
			if ( owner.IsValid() )
			{
				var ring = ringByOwner.GetValueOrDefault( dto.OwnerAccountKey, 0 );
				if ( !ThornsTameSummonUtil.TryResolveNearOwnerPosition( scene, owner, ring, out pos ) )
					pos = owner.WorldPosition;
				ringByOwner[dto.OwnerAccountKey] = ring + 1;
			}
			else
			{
				pos = new Vector3( dto.Px, dto.Py, dto.Pz );
				if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( scene, pos, 320f, out pos, out _ ) )
				{
					skipped++;
					continue;
				}
			}

			var rot = Rotation.FromYaw( dto.RYaw );

			var brain = ThornsAnimalFactory.HostSpawnTameRestore( scene, species, pos, rot );
			if ( !brain.IsValid() )
			{
				Log.Warning( $"[Thorns Terrain] Tame restore failed to spawn {species.DisplayName} at {pos:F0}." );
				skipped++;
				continue;
			}

			brain.HostRestoreTamedState( dto );
			restored++;
		}

		if ( restored > 0 )
		{
			ThornsAnimalManager.NotifyTameRestoreHadSuccess();
			Log.Info( $"[Thorns Terrain] Restored {restored} tamed animal(s) from '{persistence.RelativeSavePath}'." );
			ThornsTameSummonUtil.HostSummonAllOwnedTamesNearPlayers( scene );
		}

		if ( skipped > 0 && restored == 0 )
			Log.Warning( $"[Thorns Terrain] Failed to restore {skipped} saved tame(s) from '{persistence.RelativeSavePath}'." );
	}

	static void HostRefreshPlayerTameSnapshots( Scene scene )
	{
		foreach ( var gameplay in scene.GetAllComponents<ThornsPlayerGameplay>() )
		{
			if ( !gameplay.IsValid() )
				continue;

			gameplay.HostRebuildTamesFromWorld();
		}
	}
}
