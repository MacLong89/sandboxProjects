namespace Terraingen.Multiplayer;

using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Versioned world save load/save with migration hooks.</summary>
public static class ThornsSaveFormat
{
	public const int CurrentVersion = 12;

	public static ThornsPersistentWorldDto CreateEmpty() => new()
	{
		Version = CurrentVersion,
		SavedUtcIso = DateTime.UtcNow.ToString( "o" ),
		PlayersByAccountKey = new Dictionary<string, ThornsPersistentPlayerDto>(),
		PlayerProgressByAccountKey = new Dictionary<string, ThornsPersistentPlayerProgressDto>(),
		Tames = new List<ThornsPersistentTameDto>(),
		Guilds = new List<ThornsPersistentGuildDto>(),
		AccountGuildIds = new Dictionary<string, string>(),
		Structures = new List<ThornsPersistentStructureDto>(),
		DepletedTreeIds = new List<int>(),
		DepletedMineralNodeIds = new List<int>(),
		LootedFurnitureIds = new List<int>(),
		FurnitureContainers = new List<ThornsPersistentFurnitureContainerDto>(),
		StructureStorages = new List<ThornsPersistentStructureStorageEntryDto>(),
		PlayerMapsByAccountKey = new Dictionary<string, ThornsPersistentPlayerMapDto>(),
		VictoryState = new ThornsVictoryPersistentStateDto(),
		NpcGuild = new ThornsPersistentNpcGuildDto(),
		NpcGuilds = new List<ThornsPersistentNpcGuildDto>()
	};

	public static bool TryLoad( string relativePath, out ThornsPersistentWorldDto dto, out string error )
	{
		dto = null;
		error = "";

		if ( !FileSystem.Data.FileExists( relativePath ) )
		{
			dto = CreateEmpty();
			return true;
		}

		if ( !ThornsAtomicFileSave.TryReadJson( relativePath, out ThornsPersistentWorldDto loaded, out error ) || loaded is null )
		{
			var backupPath = $"{relativePath}.bak";
			if ( FileSystem.Data.FileExists( backupPath )
			     && ThornsAtomicFileSave.TryReadJson( backupPath, out loaded, out error )
			     && loaded is not null )
			{
				Log.Warning( $"[Thorns Persistence] Primary save unreadable — recovered from '{backupPath}'." );
			}
			else
			{
				error = string.IsNullOrWhiteSpace( error ) ? "deserialize failed" : error;
				return false;
			}
		}

		try
		{
			dto = Migrate( loaded );
			return true;
		}
		catch ( Exception e )
		{
			error = e.Message;
			dto = null;
			return false;
		}
	}

	public static bool TrySave( string relativePath, ThornsPersistentWorldDto dto, out string error )
	{
		if ( dto is null )
		{
			error = "null dto";
			return false;
		}

		dto.Version = CurrentVersion;
		dto.SavedUtcIso = DateTime.UtcNow.ToString( "o" );
		return ThornsAtomicFileSave.TryWriteJson( relativePath, dto, out error );
	}

	static ThornsPersistentWorldDto Migrate( ThornsPersistentWorldDto loaded )
	{
		loaded ??= CreateEmpty();

		if ( loaded.Version > CurrentVersion )
			throw new InvalidOperationException(
				$"Save version {loaded.Version} is newer than supported version {CurrentVersion}." );
		loaded.PlayersByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerDto>();
		loaded.Tames ??= new List<ThornsPersistentTameDto>();
		loaded.Guilds ??= new List<ThornsPersistentGuildDto>();
		loaded.AccountGuildIds ??= new Dictionary<string, string>();
		loaded.PlayerProgressByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerProgressDto>();
		loaded.Structures ??= new List<ThornsPersistentStructureDto>();
		loaded.DepletedTreeIds ??= new List<int>();
		loaded.DepletedMineralNodeIds ??= new List<int>();
		loaded.LootedFurnitureIds ??= new List<int>();
		loaded.FurnitureContainers ??= new List<ThornsPersistentFurnitureContainerDto>();
		loaded.StructureStorages ??= new List<ThornsPersistentStructureStorageEntryDto>();
		loaded.PlayerMapsByAccountKey ??= new Dictionary<string, ThornsPersistentPlayerMapDto>();
		loaded.VictoryState ??= new ThornsVictoryPersistentStateDto();
		loaded.VictoryState.PlayerProgressByAccount ??= new Dictionary<string, Dictionary<string, long>>();
		loaded.VictoryState.GuildProgressByGuildId ??= new Dictionary<string, Dictionary<string, long>>();
		loaded.VictoryState.WorldProgressByPath ??= new Dictionary<string, long>();
		loaded.VictoryState.LastPlayerLeaderByPath ??= new Dictionary<string, string>();
		loaded.VictoryState.LastGuildLeaderByPath ??= new Dictionary<string, string>();
		loaded.VictoryState.LeadershipChanges ??= new List<ThornsVictoryLeadershipChangePersistentDto>();

		if ( loaded.Version < 4 )
		{
			// v3 and earlier had no progression blob — start fresh progression per account.
			loaded.Version = 4;
		}

		if ( loaded.Version < 5 )
			loaded.Version = 5;

		if ( loaded.Version < 6 )
			loaded.Version = 6;

		if ( loaded.Version < 7 )
			loaded.Version = 7;

		if ( loaded.Version < 8 )
		{
			foreach ( var structure in loaded.Structures )
			{
				if ( structure is null || !ThornsPlayerBuildingDefinitions.TryGet( structure.StructureId, out var def ) )
					continue;

				if ( def.SnapKind is ThornsPlayerBuildSnapKind.Foundation or ThornsPlayerBuildSnapKind.Portable )
					structure.Pz -= def.Size.z * 0.5f;
			}

			loaded.Version = 8;
		}

		if ( loaded.Version < 9 )
		{
			loaded.VictoryState ??= new ThornsVictoryPersistentStateDto();
			loaded.Version = 9;
		}

		if ( loaded.Version < 10 )
		{
			loaded.NpcGuild ??= new ThornsPersistentNpcGuildDto();
			loaded.Version = 10;
		}

		if ( loaded.Version < 11 )
		{
			loaded.NpcGuilds ??= new List<ThornsPersistentNpcGuildDto>();
			if ( loaded.NpcGuild is not null
			     && ( loaded.NpcGuild.IsEliminated
			          || loaded.NpcGuild.HasDominionVictory
			          || loaded.NpcGuild.Outposts?.Count > 0
			          || loaded.NpcGuild.ExpansionAccumulatorSeconds > 0f
			          || loaded.NpcGuild.NextOutpostSeed > 1 ) )
			{
				var already = loaded.NpcGuilds.Any( g => g is not null
				                                       && string.Equals(
					                                       g.GuildId,
					                                       loaded.NpcGuild.GuildId,
					                                       StringComparison.OrdinalIgnoreCase ) );
				if ( !already )
					loaded.NpcGuilds.Add( loaded.NpcGuild );
			}

			loaded.Version = 11;
		}

		if ( loaded.Version < 12 )
			loaded.Version = 12;

		loaded.NpcGuild ??= new ThornsPersistentNpcGuildDto();
		loaded.NpcGuilds ??= new List<ThornsPersistentNpcGuildDto>();
		EnsureCatalogNpcGuildPersistenceStubs( loaded );
		return loaded;
	}

	static void EnsureCatalogNpcGuildPersistenceStubs( ThornsPersistentWorldDto loaded )
	{
		if ( loaded is null )
			return;

		loaded.NpcGuilds ??= new List<ThornsPersistentNpcGuildDto>();

		foreach ( var template in ThornsNpcGuildCatalog.All )
		{
			var exists = loaded.NpcGuilds.Any( g => g is not null
			                                        && string.Equals(
				                                        g.GuildId,
				                                        template.GuildId,
				                                        StringComparison.OrdinalIgnoreCase ) );
			if ( exists )
				continue;

			loaded.NpcGuilds.Add( new ThornsPersistentNpcGuildDto { GuildId = template.GuildId } );
		}
	}

	public static void CapturePlayerProgress( ThornsPersistentWorldDto world, ThornsPlayerGameplay gameplay )
	{
		if ( world is null || gameplay is null || !gameplay.IsValid() || string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return;

		world.PlayerProgressByAccountKey[gameplay.AccountKey] = ThornsPlayerProgressPersistence.Capture( gameplay );
	}

	public static void RestorePlayerProgress( ThornsPersistentWorldDto world, ThornsPlayerGameplay gameplay )
	{
		ThornsPlayerProgressPersistence.TryRestoreFromWorld( world, gameplay );
	}
}
