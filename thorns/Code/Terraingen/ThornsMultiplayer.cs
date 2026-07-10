namespace Terraingen;

/// <summary>
/// Shared authority checks for deterministic world generation in multiplayer.
/// </summary>
public static class ThornsMultiplayer
{
	public static bool IsNetworked => Networking.IsActive;

	public static bool IsHostOrOffline => !IsNetworked || Networking.IsHost;

	/// <summary>
	/// Host runs generation by default; clients can mirror the same deterministic world locally.
	/// </summary>
	public static bool ShouldGenerateWorld( bool hostAuthoritative, bool clientsGenerateDeterministic )
	{
		if ( !IsNetworked )
			return true;

		if ( !hostAuthoritative )
			return true;

		return Networking.IsHost || clientsGenerateDeterministic;
	}

	public static bool ShouldSpawnLocalExplorer( bool hostAuthoritative, bool clientsSpawnLocalExplorer )
	{
		if ( !IsNetworked )
			return true;

		if ( !hostAuthoritative )
			return true;

		return Networking.IsHost || clientsSpawnLocalExplorer;
	}
}
