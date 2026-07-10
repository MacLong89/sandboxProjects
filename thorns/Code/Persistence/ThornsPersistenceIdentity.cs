namespace Sandbox;

/// <summary>
/// Stable keys for per-player rows in <see cref="ThornsPersistentWorldDto.PlayersByAccountKey"/>.
/// Prefer Steam (persists across reconnects); without Steam the key uses <see cref="Connection.Id"/> (LAN-only sessions may not survive restart the same way).
/// </summary>
public static class ThornsPersistenceIdentity
{
	public static string GetStableAccountKey( Connection connection )
	{
		if ( connection is null )
			return "";

		var steam = connection.SteamId;
		if ( steam.Value != 0 )
			return $"steam:{steam.Value}";

		return $"conn:{connection.Id:D}";
	}

	public static bool TryGetStableAccountKeyForConnection( Guid connectionId, out string key )
	{
		key = "";
		if ( connectionId == Guid.Empty )
			return false;

		var c = Connection.Find( connectionId );
		if ( c is null )
			return false;

		key = GetStableAccountKey( c );
		return !string.IsNullOrEmpty( key );
	}
}
