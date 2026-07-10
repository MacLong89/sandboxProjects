using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Rows for the Guild TAB (server session + Steam friends).</summary>
public readonly struct ThornsGuildListEntry
{
	public string AccountKey { get; init; }
	public string DisplayName { get; init; }
	public bool IsOnline { get; init; }
	public bool IsOnThisServer { get; init; }
	public ulong SteamId { get; init; }
}

/// <summary>Connected players and Steam friends for guild invites.</summary>
public static class ThornsGuildLists
{
	public static List<ThornsGuildListEntry> BuildServerPlayerEntries( GameObject localPawnRoot )
	{
		var list = new List<ThornsGuildListEntry>();
		if ( localPawnRoot is null || !localPawnRoot.IsValid() )
			return list;

		ThornsGuildRoster.TryGetAccountKeyForPawnRoot( localPawnRoot, out var selfKey );

		var scene = localPawnRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return list;

		foreach ( var session in scene.GetAllComponents<ThornsPlayer>() )
		{
			if ( !session.IsValid() )
				continue;

			var key = session.HostPersistenceAccountKey;
			if ( string.IsNullOrWhiteSpace( key ) && session.OwnerConnection is not null )
				key = ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );

			key = ThornsGuildRoster.NormalizeAccountKey( key );
			if ( string.IsNullOrEmpty( key ) )
				continue;

			if ( !string.IsNullOrEmpty( selfKey ) && string.Equals( key, selfKey, StringComparison.Ordinal ) )
				continue;

			var name = session.OwnerConnection?.DisplayName ?? session.GameObject.Name;
			list.Add( new ThornsGuildListEntry
			{
				AccountKey = key,
				DisplayName = name,
				IsOnline = true,
				IsOnThisServer = true,
				SteamId = session.OwnerConnection is not null
					? SteamIdToUlong( session.OwnerConnection.SteamId )
					: 0ul
			} );
		}

		list.Sort( ( a, b ) => string.Compare( a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase ) );
		return list;
	}

	/// <summary>
	/// Party members and Steam friends in this session (full offline friend list is menu-only in s&amp;box).
	/// </summary>
	public static List<ThornsGuildListEntry> BuildSteamFriendEntries( GameObject localPawnRoot )
	{
		var list = new List<ThornsGuildListEntry>();
		ThornsGuildRoster.TryGetAccountKeyForPawnRoot( localPawnRoot, out var selfKey );

		var onServer = new HashSet<string>( StringComparer.Ordinal );
		foreach ( var s in BuildServerPlayerEntries( localPawnRoot ) )
			onServer.Add( s.AccountKey );

		var seenSteam = new HashSet<ulong>();

		foreach ( var friend in EnumerateGuildCandidateFriends() )
		{
			if ( friend.IsMe )
				continue;

			var steamId = friend.Id;
			if ( steamId == 0 || !seenSteam.Add( steamId ) )
				continue;

			var key = ThornsGuildRoster.AccountKeyFromSteamId( steamId );
			if ( string.IsNullOrEmpty( key ) )
				continue;

			if ( !string.IsNullOrEmpty( selfKey ) && string.Equals( key, selfKey, StringComparison.Ordinal ) )
				continue;

			var name = friend.Name;
			if ( string.IsNullOrWhiteSpace( name ) )
				name = $"Friend {steamId}";

			list.Add( new ThornsGuildListEntry
			{
				AccountKey = key,
				DisplayName = name.Trim(),
				IsOnline = friend.IsOnline,
				IsOnThisServer = onServer.Contains( key ),
				SteamId = steamId
			} );
		}

		list.Sort( ( a, b ) =>
		{
			var o = b.IsOnline.CompareTo( a.IsOnline );
			return o != 0 ? o : string.Compare( a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase );
		} );

		return list;
	}

	static ulong SteamIdToUlong( SteamId steamId )
	{
		var raw = steamId.Value;
		return raw > 0 ? (ulong)raw : 0;
	}

	static IEnumerable<Friend> EnumerateGuildCandidateFriends()
	{
		var party = PartyRoom.Current;
		if ( party is not null && party.Members is not null )
		{
			foreach ( var member in party.Members )
				yield return member;
		}

		var local = Connection.Local;
		var hasParty = local is not null && SteamIdToUlong( local.PartyId ) != 0;
		var partyId = local is not null ? local.PartyId : default;
		var all = Connection.All;
		if ( all is null )
			yield break;

		foreach ( var conn in all )
		{
			if ( conn is null )
				continue;

			var steamId = SteamIdToUlong( conn.SteamId );
			if ( steamId == 0 )
				continue;

			var friend = new Friend( steamId );

			var inParty = hasParty && conn.PartyId == partyId;
			if ( !inParty && !friend.IsFriend )
				continue;

			yield return friend;
		}
	}
}
