namespace Terraingen.Buildings;

using Sandbox.Network;
using Terraingen.Player;

/// <summary>
/// AUDIT FIX (2026-07): Central ownership / guild rules for player structures.
/// <para>
/// Prior behavior treated a blank <c>OwnerAccountKey</c> as "public" for demolish, door use,
/// and (implicitly) anyone could loot <c>struct:</c> storage. That let players grief mis-owned
/// pieces and loot foreign chests.
/// </para>
/// <para>
/// Revert guide: if co-op bases break, check guild membership paths first before loosening blank-owner.
/// Offline (no networking) still treats blank as allowable sandbox ownership.
/// </para>
/// </summary>
public static class ThornsBuildingOwnership
{
	/// <summary>
	/// May the caller demolish / upgrade / use owned interactables on this structure?
	/// Blank owner: allowed only when not networked (local sandbox). Networked blank = deny.
	/// Guild mates share access with the owner.
	/// </summary>
	public static bool HostAccountMayUseStructure( string ownerAccountKey, string callerAccountKey )
	{
		if ( string.IsNullOrWhiteSpace( callerAccountKey ) )
			return !Networking.IsActive;

		if ( string.IsNullOrWhiteSpace( ownerAccountKey ) )
		{
			// Offline / single-player: blank owners are common during early place bugs — allow.
			// Multiplayer: blank is no longer "free for all" (prevents orphaned public demolish/loot).
			return !Networking.IsActive;
		}

		if ( string.Equals( ownerAccountKey, callerAccountKey, StringComparison.OrdinalIgnoreCase ) )
			return true;

		return HostAccountsShareGuild( ownerAccountKey, callerAccountKey );
	}

	/// <summary>
	/// Foundation / wall support for new placement: must belong to caller or their guild.
	/// Blank-owner supports are ignored in multiplayer so you cannot piggyback foreign bases
	/// that lost their owner key — and cannot claim strangers' geometry as free snap hosts.
	/// </summary>
	public static bool HostAccountMayUseAsPlacementSupport( string supportOwnerAccountKey, string placerAccountKey )
	{
		// Same rules as use/modify — intentional so support ACL stays one place to edit.
		return HostAccountMayUseStructure( supportOwnerAccountKey, placerAccountKey );
	}

	public static bool HostAccountsShareGuild( string accountA, string accountB )
	{
		if ( string.IsNullOrWhiteSpace( accountA ) || string.IsNullOrWhiteSpace( accountB ) )
			return false;

		if ( string.Equals( accountA, accountB, StringComparison.OrdinalIgnoreCase ) )
			return true;

		var service = ThornsGuildWorldService.Instance;
		if ( service is null )
			return false;

		if ( !service.TryGetAccountGuildId( accountA, out var guildA )
		     || !service.TryGetAccountGuildId( accountB, out var guildB ) )
			return false;

		return !string.IsNullOrWhiteSpace( guildA )
		       && string.Equals( guildA, guildB, StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>Resolve a stable owner key for a connection, falling back to gameplay AccountKey.</summary>
	public static string ResolveOwnerAccountKey( Connection owner, ThornsPlayerGameplay gameplayFallback )
	{
		if ( owner is not null )
		{
			var fromConnection = Terraingen.Multiplayer.ThornsPersistenceIdentity.GetStableAccountKey( owner );
			if ( !string.IsNullOrWhiteSpace( fromConnection ) )
				return fromConnection;
		}

		if ( gameplayFallback is not null && gameplayFallback.IsValid()
		     && !string.IsNullOrWhiteSpace( gameplayFallback.AccountKey ) )
			return gameplayFallback.AccountKey.Trim();

		return "";
	}
}
