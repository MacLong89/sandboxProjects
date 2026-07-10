namespace Terraingen.Multiplayer;

/// <summary>Player-owned worlds — each entry maps to its own host-local persistence file.</summary>
public sealed class ThornsHostedServerDto
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string PersistenceRelativePath { get; set; } = "";
	public bool RequireJoinPassword { get; set; }
	public string JoinPassword { get; set; } = "";
	public long CreatedUtcTicks { get; set; }
	public long LastHostedUtcTicks { get; set; }
}

public sealed class ThornsHostedServerCatalogDto
{
	public List<ThornsHostedServerDto> Servers { get; set; } = new();
}

public static class ThornsHostedServerCatalog
{
	public const string RelativePath = "Terraingen/hosted_servers.json";
	const int MaxServers = 32;

	public static ThornsHostedServerCatalogDto Current { get; private set; } = new();

	public static void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( RelativePath ) )
			{
				Current = new ThornsHostedServerCatalogDto();
				MigrateLegacyIfEmpty();
				return;
			}

			Current = FileSystem.Data.ReadJson<ThornsHostedServerCatalogDto>( RelativePath )
			          ?? new ThornsHostedServerCatalogDto();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to load hosted server catalog." );
			Current = new ThornsHostedServerCatalogDto();
		}

		Current.Servers ??= new List<ThornsHostedServerDto>();
		MigrateLegacyIfEmpty();
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.WriteJson( RelativePath, Current );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to save hosted server catalog." );
		}
	}

	static void MigrateLegacyIfEmpty()
	{
		if ( Current.Servers.Count > 0 )
			return;

		// Catalog was saved empty on purpose (e.g. user deleted all worlds) — do not re-seed.
		if ( FileSystem.Data.FileExists( RelativePath ) )
			return;

		var name = ThornsHostMenuPreferences.LoadLastHostedServerName();
		var entry = new ThornsHostedServerDto
		{
			Id = NewServerId(),
			DisplayName = name,
			PersistenceRelativePath = ThornsHostSavePaths.PersistencePathForServerName( name ),
			CreatedUtcTicks = DateTime.UtcNow.Ticks,
			LastHostedUtcTicks = DateTime.UtcNow.Ticks
		};
		Current.Servers.Add( entry );
		Save();
	}

	public static IReadOnlyList<ThornsHostedServerDto> ListOrdered()
	{
		return Current.Servers
			.OrderByDescending( s => s.LastHostedUtcTicks )
			.ThenBy( s => s.DisplayName, StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	public static ThornsHostedServerDto GetById( string id ) =>
		Current.Servers.FirstOrDefault( s => s.Id == id );

	public static ThornsHostedServerDto GetLastHosted() =>
		Current.Servers
			.OrderByDescending( s => s.LastHostedUtcTicks )
			.FirstOrDefault();

	public static ThornsHostedServerDto CreateNew( string displayName )
	{
		var trimmed = displayName?.Trim() ?? "";
		if ( string.IsNullOrEmpty( trimmed ) )
			trimmed = "My Thorns World";

		var id = NewServerId();
		var entry = new ThornsHostedServerDto
		{
			Id = id,
			DisplayName = trimmed,
			PersistenceRelativePath = ThornsHostSavePaths.PersistencePathForServerId( id ),
			CreatedUtcTicks = DateTime.UtcNow.Ticks,
			LastHostedUtcTicks = 0
		};

		Current.Servers.Insert( 0, entry );
		TrimOverflow();
		Save();
		return entry;
	}

	public static bool TryRename( string id, string newDisplayName )
	{
		var entry = GetById( id );
		if ( entry is null )
			return false;

		var trimmed = newDisplayName?.Trim() ?? "";
		if ( string.IsNullOrEmpty( trimmed ) )
			return false;

		entry.DisplayName = trimmed;
		Save();
		return true;
	}

	/// <summary>Removes the catalog entry and deletes its on-disk save file.</summary>
	public static bool TryRemove( string id, out string errorMessage )
	{
		errorMessage = null;
		var entry = GetById( id );
		if ( entry is null )
		{
			errorMessage = "World not found.";
			return false;
		}

		if ( !string.IsNullOrWhiteSpace( entry.PersistenceRelativePath )
		     && !ThornsWorldSaveWipe.TryDeleteWorldFile( entry.PersistenceRelativePath, out errorMessage ) )
			return false;

		Current.Servers.RemoveAll( s => s.Id == id );
		Save();
		return true;
	}

	public static void RecordHosted( ThornsHostedServerDto server )
	{
		if ( server is null || string.IsNullOrEmpty( server.Id ) )
			return;

		server.LastHostedUtcTicks = DateTime.UtcNow.Ticks;
		ThornsHostMenuPreferences.SaveLastHostedServerName( server.DisplayName );
		Save();
	}

	public static bool SaveExists( ThornsHostedServerDto server )
	{
		if ( server is null || string.IsNullOrWhiteSpace( server.PersistenceRelativePath ) )
			return false;

		return FileSystem.Data.FileExists( server.PersistenceRelativePath );
	}

	static void TrimOverflow()
	{
		if ( Current.Servers.Count <= MaxServers )
			return;

		var drop = Current.Servers
			.OrderBy( s => s.LastHostedUtcTicks )
			.Take( Current.Servers.Count - MaxServers )
			.ToList();

		foreach ( var s in drop )
			Current.Servers.Remove( s );
	}

	static string NewServerId() => Guid.NewGuid().ToString( "N" )[..12];
}
