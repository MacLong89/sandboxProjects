namespace Terraingen.UI.Menu;

using Sandbox.Network;

/// <summary>Lobby data keys for server browser columns and previews.</summary>
public static class ThornsLobbyMetadata
{
	public const string RegionKey = "thorns_region";
	public const string BiomeKey = "thorns_biome";
	public const string ServerTypeKey = "thorns_server_type";

	public static string GetRegion( in LobbyInformation lobby )
	{
		var region = GetLobbyData( lobby, RegionKey )?.Trim();
		return string.IsNullOrEmpty( region ) ? "" : region;
	}

	public static string GetBiome( in LobbyInformation lobby )
	{
		var biome = GetLobbyData( lobby, BiomeKey )?.Trim();
		if ( !string.IsNullOrEmpty( biome ) )
			return biome;

		return InferBiomeFromName( lobby.Name );
	}

	public static bool IsOfficial( in LobbyInformation lobby )
	{
		var type = GetLobbyData( lobby, ServerTypeKey )?.Trim();
		if ( string.Equals( type, "official", StringComparison.OrdinalIgnoreCase ) )
			return true;

		var name = lobby.Name ?? "";
		return name.Contains( "[Official]", StringComparison.OrdinalIgnoreCase )
		       || name.Contains( "(Official)", StringComparison.OrdinalIgnoreCase );
	}

	public static string InferBiomeFromName( string serverName )
	{
		if ( string.IsNullOrWhiteSpace( serverName ) )
			return "Forest";

		var n = serverName.ToLowerInvariant();
		if ( n.Contains( "snow" ) || n.Contains( "frost" ) || n.Contains( "ice" ) )
			return "Snow";
		if ( n.Contains( "lake" ) || n.Contains( "shore" ) || n.Contains( "coast" ) )
			return "Lake";
		if ( n.Contains( "mountain" ) || n.Contains( "peak" ) || n.Contains( "ridge" ) )
			return "Mountain";
		if ( n.Contains( "plain" ) || n.Contains( "meadow" ) || n.Contains( "field" ) )
			return "Plains";
		return "Forest";
	}

	public static void PublishHostMetadata( string region, string biome, bool official )
	{
		if ( !Networking.IsHost )
			return;

		if ( !string.IsNullOrWhiteSpace( region ) )
			Networking.SetData( RegionKey, region.Trim() );

		if ( !string.IsNullOrWhiteSpace( biome ) )
			Networking.SetData( BiomeKey, biome.Trim() );

		Networking.SetData( ServerTypeKey, official ? "official" : "community" );
	}

	static string GetLobbyData( in LobbyInformation lobby, string key, string fallback = "" )
	{
		try
		{
			return lobby.Get( key, fallback ) ?? fallback;
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"[Thorns Menu] Lobby.Get failed for '{key}'." );
			return fallback;
		}
	}
}
