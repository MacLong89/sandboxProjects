namespace Sandbox;

/// <summary>
/// World metadata snapshot domain — version, seed, timestamp, root DTO assembly.
/// </summary>
public static class ThornsWorldMetaSnapshotService
{
	public static int? HostResolveWorldGenerationSeed( Scene scene, ThornsPersistentWorldDto live )
	{
		int? worldGenPersist = live?.WorldGenerationSeed;
		foreach ( var terr in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( terr.IsValid() )
			{
				worldGenPersist = terr.ResolvedWorldGenerationSeed;
				break;
			}
		}

		return worldGenPersist;
	}

	public static ThornsPersistentWorldDto AssembleSnapshot(
		Scene scene,
		ThornsPersistentWorldDto live,
		Dictionary<string, ThornsPersistentPlayerDto> players,
		List<ThornsPersistentStructureDto> structures,
		List<ThornsPersistentWildlifeDto> wildlife )
	{
		return new ThornsPersistentWorldDto
		{
			Version = 2,
			SavedUtcIso = DateTime.UtcNow.ToString( "o" ),
			WorldGenerationSeed = HostResolveWorldGenerationSeed( scene, live ),
			Structures = structures ?? new List<ThornsPersistentStructureDto>(),
			Wildlife = wildlife ?? new List<ThornsPersistentWildlifeDto>(),
			PlayersByAccountKey = players ?? new Dictionary<string, ThornsPersistentPlayerDto>()
		};
	}
}
