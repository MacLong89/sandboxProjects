using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Per-owner index of tamed wildlife — avoids <c>GetAllComponents&lt;ThornsWildlifeIdentity&gt;</c> for mount/tame UI and bond perks.
/// Maintained on all peers from <see cref="ThornsWildlifeIdentity"/> lifecycle hooks.
/// </summary>
public static class ThornsWildlifeTameRegistry
{
	static readonly Dictionary<Guid, List<ThornsWildlifeIdentity>> ByConnectionId = new();
	static readonly Dictionary<string, List<ThornsWildlifeIdentity>> ByAccountKey = new( StringComparer.Ordinal );
	static readonly Dictionary<Guid, ThornsWildlifeIdentity> MountedByRider = new();

	public static int RegisteredTameCount { get; private set; }

	public static int MountedRiderCount => MountedByRider.Count;

	public static void Register( ThornsWildlifeIdentity wid )
	{
		if ( wid is null || !wid.IsValid() || !wid.HostIsTamed )
			return;

		RemoveFromOwnerLists( wid );

		var conn = wid.TameOwnerConnectionId;
		if ( conn != Guid.Empty )
			AddToList( ByConnectionId, conn, wid );

		var acct = wid.TameOwnerAccountKeySync;
		if ( !string.IsNullOrEmpty( acct ) )
			AddToList( ByAccountKey, acct, wid );

		RefreshRiderIndex( wid );
		RegisteredTameCount = Math.Max( RegisteredTameCount, CountRegisteredApprox() );
	}

	public static void Unregister( ThornsWildlifeIdentity wid )
	{
		if ( wid is null )
			return;

		RemoveFromOwnerLists( wid );
		RemoveRiderIndex( wid );
	}

	public static void RefreshRiderIndex( ThornsWildlifeIdentity wid )
	{
		if ( wid is null || !wid.IsValid() )
			return;

		RemoveRiderIndex( wid );

		if ( !wid.HostIsTamed || !wid.Definition.AllowPlayerMount )
			return;

		var rider = wid.TameRiderConnectionId;
		if ( rider == Guid.Empty )
			return;

		MountedByRider[rider] = wid;
	}

	public static void CopyOwnedTames( Guid connectionId, string accountKey, List<ThornsWildlifeIdentity> dst )
	{
		dst.Clear();
		if ( connectionId == Guid.Empty && string.IsNullOrEmpty( accountKey ) )
			return;

		if ( connectionId != Guid.Empty && ByConnectionId.TryGetValue( connectionId, out var byConn ) )
			AppendValid( dst, byConn );

		if ( !string.IsNullOrEmpty( accountKey ) && ByAccountKey.TryGetValue( accountKey, out var byAcct ) )
			AppendValidUnique( dst, byAcct );
	}

	public static void ForEachOwnedBy( Guid connectionId, string accountKey, Action<ThornsWildlifeIdentity> action )
	{
		if ( action is null )
			return;

		if ( connectionId != Guid.Empty && ByConnectionId.TryGetValue( connectionId, out var byConn ) )
		{
			for ( var i = 0; i < byConn.Count; i++ )
			{
				var wid = byConn[i];
				if ( wid is { IsValid: true } )
					action( wid );
			}
		}

		if ( string.IsNullOrEmpty( accountKey ) || !ByAccountKey.TryGetValue( accountKey, out var byAcct ) )
			return;

		for ( var i = 0; i < byAcct.Count; i++ )
		{
			var wid = byAcct[i];
			if ( wid is not { IsValid: true } )
				continue;
			if ( connectionId != Guid.Empty && wid.TameOwnerConnectionId == connectionId )
				continue;
			action( wid );
		}
	}

	public static bool TryGetMountedByRider( Guid riderConnectionId, out ThornsWildlifeIdentity wid )
	{
		wid = default;
		if ( riderConnectionId == Guid.Empty )
			return false;

		if ( !MountedByRider.TryGetValue( riderConnectionId, out wid ) || !wid.IsValid() )
		{
			MountedByRider.Remove( riderConnectionId );
			wid = default;
			return false;
		}

		return true;
	}

	public static int CountMountedRidersHost()
	{
		var n = 0;
		foreach ( var kv in MountedByRider )
		{
			if ( kv.Value is { IsValid: true } && kv.Value.TameRiderConnectionId != Guid.Empty )
				n++;
		}

		return n;
	}

	static void AddToList<TKey>( Dictionary<TKey, List<ThornsWildlifeIdentity>> map, TKey key, ThornsWildlifeIdentity wid )
	{
		if ( !map.TryGetValue( key, out var list ) )
		{
			list = new List<ThornsWildlifeIdentity>( 4 );
			map[key] = list;
		}

		if ( !list.Contains( wid ) )
			list.Add( wid );
	}

	static void RemoveFromOwnerLists( ThornsWildlifeIdentity wid )
	{
		RemoveFromAnyList( ByConnectionId, wid );
		RemoveFromAnyList( ByAccountKey, wid );
	}

	static void RemoveFromAnyList<TKey>( Dictionary<TKey, List<ThornsWildlifeIdentity>> map, ThornsWildlifeIdentity wid )
	{
		List<TKey> emptyKeys = null;
		foreach ( var kv in map )
		{
			var list = kv.Value;
			if ( !list.Remove( wid ) || list.Count != 0 )
				continue;

			emptyKeys ??= new List<TKey>( 2 );
			emptyKeys.Add( kv.Key );
		}

		if ( emptyKeys is null )
			return;

		for ( var i = 0; i < emptyKeys.Count; i++ )
			map.Remove( emptyKeys[i] );
	}

	static void RemoveRiderIndex( ThornsWildlifeIdentity wid )
	{
		var rider = wid.TameRiderConnectionId;
		if ( rider != Guid.Empty && MountedByRider.TryGetValue( rider, out var mounted ) && mounted == wid )
			MountedByRider.Remove( rider );
	}

	static void AppendValid( List<ThornsWildlifeIdentity> dst, List<ThornsWildlifeIdentity> src )
	{
		for ( var i = 0; i < src.Count; i++ )
		{
			var wid = src[i];
			if ( wid is { IsValid: true } )
				dst.Add( wid );
		}
	}

	static void AppendValidUnique( List<ThornsWildlifeIdentity> dst, List<ThornsWildlifeIdentity> src )
	{
		for ( var i = 0; i < src.Count; i++ )
		{
			var wid = src[i];
			if ( wid is not { IsValid: true } || dst.Contains( wid ) )
				continue;
			dst.Add( wid );
		}
	}

	static int CountRegisteredApprox()
	{
		var n = 0;
		foreach ( var kv in ByConnectionId )
			n += kv.Value.Count;
		return n;
	}
}
