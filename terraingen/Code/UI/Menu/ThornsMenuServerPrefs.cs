namespace Terraingen.UI.Menu;

public enum ThornsServerBrowserTab
{
	Official,
	Community,
	Favorites,
	Recent
}

public sealed class ThornsMenuServerPrefsDto
{
	public List<string> FavoriteLobbyIds { get; set; } = new();
	public List<ThornsMenuRecentServerDto> RecentServers { get; set; } = new();
}

public sealed class ThornsMenuRecentServerDto
{
	public string LobbyId { get; set; } = "";
	public string Name { get; set; } = "";
	public string Region { get; set; } = "";
	public string Biome { get; set; } = "";
	public long SeenUtcTicks { get; set; }
}

public static class ThornsMenuServerPrefs
{
	public const string RelativePath = "Terraingen/menu_server_prefs.json";
	const int MaxRecent = 24;

	public static ThornsMenuServerPrefsDto Current { get; private set; } = new();

	public static void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( RelativePath ) )
			{
				Current = new ThornsMenuServerPrefsDto();
				return;
			}

			Current = FileSystem.Data.ReadJson<ThornsMenuServerPrefsDto>( RelativePath ) ?? new ThornsMenuServerPrefsDto();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to load server prefs." );
			Current = new ThornsMenuServerPrefsDto();
		}

		Current.FavoriteLobbyIds ??= new List<string>();
		Current.RecentServers ??= new List<ThornsMenuRecentServerDto>();
	}

	public static void Save()
	{
		try
		{
			FileSystem.Data.WriteJson( RelativePath, Current );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Failed to save server prefs." );
		}
	}

	public static bool IsFavorite( string lobbyId ) =>
		!string.IsNullOrEmpty( lobbyId ) && Current.FavoriteLobbyIds.Contains( lobbyId );

	public static void ToggleFavorite( string lobbyId )
	{
		if ( string.IsNullOrEmpty( lobbyId ) )
			return;

		if ( Current.FavoriteLobbyIds.Contains( lobbyId ) )
			Current.FavoriteLobbyIds.Remove( lobbyId );
		else
			Current.FavoriteLobbyIds.Add( lobbyId );

		Save();
		UiRevisionBus.Publish( UiRevisionChannel.Menu );
	}

	public static void RecordRecent( Sandbox.Network.LobbyInformation lobby )
	{
		if ( lobby.LobbyId == 0 )
			return;

		var id = lobby.LobbyId.ToString();
		Current.RecentServers.RemoveAll( r => r.LobbyId == id );
		Current.RecentServers.Insert( 0, new ThornsMenuRecentServerDto
		{
			LobbyId = id,
			Name = lobby.Name ?? "",
			Region = ThornsLobbyMetadata.GetRegion( lobby ),
			Biome = ThornsLobbyMetadata.GetBiome( lobby ),
			SeenUtcTicks = DateTime.UtcNow.Ticks
		} );

		if ( Current.RecentServers.Count > MaxRecent )
			Current.RecentServers.RemoveRange( MaxRecent, Current.RecentServers.Count - MaxRecent );

		Save();
	}
}
