namespace Terraingen;

using Terraingen.Multiplayer;


/// <summary>
/// Shared authority checks for deterministic world generation in multiplayer.
/// </summary>
public static class ThornsMultiplayer
{
	public static bool IsNetworked => Networking.IsActive;

	public static bool IsHostOrOffline => !IsNetworked || Networking.IsHost;

	/// <summary>Remote joiner loading gameplay before/after Connect — must not sculpt or proc-generate world structures.</summary>
	public static bool IsRemoteJoinClient =>
		ThornsSessionBootstrap.IsJoiningRemoteLobby
		|| ( IsNetworked && !Networking.IsHost );

	/// <summary>Host-only disk persistence — never on remote join clients (including offline preload or post-abort).</summary>
	public static bool ShouldRunHostPersistence =>
		( IsNetworked && Networking.IsHost )
		|| ( !IsNetworked && ThornsSessionBootstrap.IsHostingLocalSave );

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

	/// <summary>Buildings, boulders, dirt paths — host sculpts once; joiners receive host placement sync.</summary>
	public static bool ShouldPopulateWorldStructures( bool hostAuthoritative )
	{
		if ( !IsNetworked )
			return true;

		if ( !hostAuthoritative )
			return true;

		return IsHostOrOffline;
	}

	/// <summary>Near-player foliage/clutter/minerals — safe on all peers once heightfield matches host.</summary>
	public static bool ShouldPopulateVisualCosmetics( bool hostAuthoritative, bool clientsGenerateDeterministic )
	{
		_ = hostAuthoritative;
		_ = clientsGenerateDeterministic;

		if ( !IsNetworked )
			return true;

		return true;
	}

	/// <summary>Legacy wrapper — true when either structures or visual cosmetics should run.</summary>
	public static bool ShouldPopulateCosmetics( bool hostAuthoritative, bool clientsGenerateDeterministic )
	{
		return ShouldPopulateWorldStructures( hostAuthoritative )
		       || ShouldPopulateVisualCosmetics( hostAuthoritative, clientsGenerateDeterministic );
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
