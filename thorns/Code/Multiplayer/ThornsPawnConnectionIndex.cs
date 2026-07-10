using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host gameplay: O(1) lookup from connection id / stable account key to <see cref="ThornsPawn"/> — avoids
/// <see cref="Scene.GetAllComponents{T}"/> in tame AI, interaction, and combat-adjacent paths (THORNS §13 perf).
/// </summary>
public static class ThornsPawnConnectionIndex
{
	static readonly Dictionary<Guid, ThornsPawn> ByOwnerId = new();
	static readonly Dictionary<string, ThornsPawn> ByAccountKey = new( StringComparer.Ordinal );

	/// <summary>All registered pawns with a non-empty <see cref="GameObject.Network.OwnerId"/> (iteration only for combat/analytics; count is modest).</summary>
	public static IEnumerable<ThornsPawn> AllWithOwnerId => ByOwnerId.Values;

	public static bool TryGetByOwnerId( Guid ownerConnectionId, out ThornsPawn pawn )
	{
		pawn = default;
		if ( ownerConnectionId == Guid.Empty )
			return false;

		return ByOwnerId.TryGetValue( ownerConnectionId, out pawn ) && pawn.IsValid();
	}

	public static bool TryGetByAccountKey( string accountKey, out ThornsPawn pawn )
	{
		pawn = default;
		if ( string.IsNullOrEmpty( accountKey ) )
			return false;

		return ByAccountKey.TryGetValue( accountKey, out pawn ) && pawn.IsValid();
	}

	/// <summary>O(1) pawn root for RPC callers — replaces per-RPC <c>GetAllComponents&lt;ThornsPlayer&gt;</c> scans.</summary>
	public static bool TryGetPawnGameObject( Connection connection, out GameObject pawnRoot )
	{
		pawnRoot = default;
		if ( connection is null )
			return false;

		if ( !TryGetByOwnerId( connection.Id, out var pawn ) || !pawn.IsValid() )
			return false;

		pawnRoot = pawn.GameObject;
		return pawnRoot.IsValid();
	}

	public static void Register( ThornsPawn pawn )
	{
		if ( pawn is null || !pawn.IsValid() )
			return;

		var go = pawn.GameObject;
		if ( !go.IsValid() )
			return;

		var ownerId = go.Network.OwnerId;
		if ( ownerId == Guid.Empty )
			return;

		ByOwnerId[ownerId] = pawn;

		var conn = pawn.OwnerConnection ?? Connection.Find( ownerId );
		var key = ThornsPersistenceIdentity.GetStableAccountKey( conn );
		if ( !string.IsNullOrEmpty( key ) )
			ByAccountKey[key] = pawn;
	}

	public static void Unregister( ThornsPawn pawn )
	{
		if ( pawn is null || !pawn.IsValid() )
			return;

		var go = pawn.GameObject;
		if ( !go.IsValid() )
			return;

		var ownerId = go.Network.OwnerId;
		if ( ownerId != Guid.Empty
		     && ByOwnerId.TryGetValue( ownerId, out var reg ) && reg == pawn )
			ByOwnerId.Remove( ownerId );

		var conn = pawn.OwnerConnection ?? Connection.Find( ownerId );
		var key = ThornsPersistenceIdentity.GetStableAccountKey( conn );
		if ( !string.IsNullOrEmpty( key )
		     && ByAccountKey.TryGetValue( key, out var regAk ) && regAk == pawn )
			ByAccountKey.Remove( key );
	}
}
