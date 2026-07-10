using System.Collections.Generic;
using System.Diagnostics;

namespace Sandbox;

/// <summary>
/// Lightweight hooks for survival-scale replication / persistence observability (large public servers, JIP).
/// Does not run every frame — only logs when thresholds are crossed or on explicit snapshot events.
/// </summary>
public static class ThornsReplicationDiagnostics
{
	/// <summary>~UTF-16 length of a synced <see cref="string"/> (Base64/JSON payloads) — log warning above this.</summary>
	public const int WarnLargeSyncStringChars = 95_000;

	/// <summary>Rough ceiling for a single owner inventory RPC snapshot before warning (serialized struct estimate).</summary>
	public const int WarnInventoryOwnerSnapshotApproxBytes = 22_000;

	public static void WarnIfLargeSyncString( string context, int utf16Length )
	{
		if ( utf16Length < WarnLargeSyncStringChars )
			return;

		Log.Warning(
			$"[Thorns][Repl] Very large synced string ({context}): utf16Len={utf16Length} — check POI/terrain chunking and snapshot cadence." );
	}

	/// <summary>Coarse byte estimate for <see cref="ThornsInventorySlotNet"/> payload (items with long ids / roll JSON dominate).</summary>
	public static int EstimateInventorySlotNetRpcBytes( ThornsInventorySlotNet[] slots )
	{
		if ( slots is null || slots.Length == 0 )
			return 0;

		var n = slots.Length * 64;
		foreach ( var s in slots )
		{
			n += (s.ItemId?.Length ?? 0) * 2;
			n += (s.WeaponInstanceId?.Length ?? 0) * 2;
			n += (s.WeaponRollPayload?.Length ?? 0) * 2;
			n += (s.ArmorRollPayload?.Length ?? 0) * 2;
		}

		return n;
	}

	public static void WarnIfHeavyInventoryOwnerSnapshot( string pawnName, Guid ownerId, ThornsInventorySlotNet[] payload )
	{
		var est = EstimateInventorySlotNetRpcBytes( payload );
		if ( est < WarnInventoryOwnerSnapshotApproxBytes )
			return;

		Log.Warning(
			$"[Thorns][Repl] Heavy owner inventory snapshot pawn='{pawnName}' owner={ownerId} approxBytes≈{est} (consider throttling / diffing if this spams)." );
	}

	public static void WarnIfHeavyInventoryOwnerDelta( string pawnName, Guid ownerId, IReadOnlyList<ThornsInventorySlotChangeNet> changes )
	{
		if ( changes is null || changes.Count == 0 )
			return;

		var est = changes.Count * 96;
		for ( var i = 0; i < changes.Count; i++ )
		{
			var s = changes[i].Slot;
			est += (s.ItemId?.Length ?? 0) * 2;
			est += (s.WeaponInstanceId?.Length ?? 0) * 2;
			est += (s.WeaponRollPayload?.Length ?? 0) * 2;
			est += (s.ArmorRollPayload?.Length ?? 0) * 2;
		}

		if ( est < WarnInventoryOwnerSnapshotApproxBytes )
			return;

		Log.Warning(
			$"[Thorns][Repl] Heavy owner inventory delta pawn='{pawnName}' owner={ownerId} slots={changes.Count} approxBytes≈{est}" );
	}

	public static void LogPersistenceWriteFootprint( ThornsPersistentWorldDto dto, string relativePath )
	{
		if ( dto is null )
			return;

		var structN = dto.Structures?.Count ?? 0;
		var wildN = dto.Wildlife?.Count ?? 0;
		var playerN = dto.PlayersByAccountKey?.Count ?? 0;
		var invBlobChars = 0;
		if ( dto.PlayersByAccountKey is not null )
		{
			foreach ( var kv in dto.PlayersByAccountKey )
			{
				if ( kv.Value?.InventorySlotsBlob is { Length: > 0 } blob )
					invBlobChars += blob.Length;
			}
		}

		Log.Info(
			$"[Thorns][Persistence] snapshot footprint path='{relativePath}' structures={structN} wildlife={wildN} players={playerN} inventoryBlobCharsSum≈{invBlobChars}" );
	}

	public static void LogJoinSpawnTiming(
		string displayName,
		Guid connectionId,
		bool restoredFromDisk,
		double elapsedMs,
		int placedStructureCount,
		int registeredWildlifeBrains )
	{
		Log.Info(
			$"[Thorns][JIP] player spawn complete name='{displayName}' id={connectionId} restore={restoredFromDisk} elapsedMs={elapsedMs:F1} structures={placedStructureCount} wildlifeBrains={registeredWildlifeBrains}" );
	}

	public static Stopwatch StartTiming() => Stopwatch.StartNew();
}
