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

	/// <summary>Host sculpts heightfield; clients load cache or fallback once per seed.</summary>
	public static bool ShouldHostSculptTerrain( bool hostAuthoritative )
	{
		if ( !IsNetworked )
			return true;

		return !hostAuthoritative || Networking.IsHost;
	}

	/// <summary>
	/// Visual populate (foliage/grass/minerals). All peers run when heightfield is shared (cache/RPC) so worlds match.
	/// <paramref name="clientsGenerateDeterministic"/> is reserved for a future host-only cosmetic mode.
	/// </summary>
	public static bool ShouldPopulateCosmetics( bool hostAuthoritative, bool clientsGenerateDeterministic )
	{
		_ = hostAuthoritative;
		_ = clientsGenerateDeterministic;

		if ( !IsNetworked )
			return true;

		return true;
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
