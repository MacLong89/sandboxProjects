namespace Fauna2;

using System.Globalization;

/// <summary>
/// Replicates host-local terrain obstacles and wild animals to visiting clients.
/// Host-only entities stay off the network budget; clients mirror snapshots instead.
/// </summary>
public sealed class ClientWorldSync : Component
{
	public static ClientWorldSync Instance { get; private set; }

	// ── AUDIT FIX B2 (2026-07): wild sync debounce ────────────────────────────
	// BUG: TimeUntil defaults to "already elapsed". The old OnFixedUpdate did:
	//   if (!_wildSyncDebounce) return;
	//   _wildSyncDebounce = 0f;   // still elapsed!
	//   PushWildToClients();
	// so the host blasted a full wild snapshot EVERY FixedUpdate forever.
	//
	// FIX: explicit pending flag. ScheduleWildSync arms the flag + debounce;
	// OnFixedUpdate fires once when debounce elapses, then clears pending and
	// does NOT re-arm. Revert hint: if wilds stop updating on clients after
	// catch/top-up, check that ScheduleWildSync still sets _wildSyncPending=true.
	private bool _wildSyncPending;
	private TimeUntil _wildSyncDebounce;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !_wildSyncPending )
			return;

		// Still inside the 0.35s coalesce window after the last schedule.
		if ( !_wildSyncDebounce )
			return;

		_wildSyncPending = false;
		PushWildToClients();
	}

	public void PushFullWorldToClients()
	{
		if ( !Networking.IsHost ) return;
		PushTerrainToClients();
		// Immediate full push for joins — bypass debounce so late joiners aren't empty.
		_wildSyncPending = false;
		PushWildToClients();
	}

	public void PushTerrainToClients()
	{
		if ( !Networking.IsHost ) return;

		var terrain = TerrainObstacleSystem.Instance;
		if ( !terrain.IsValid() ) return;

		BroadcastTerrainSnapshot( WorldSnapshotFormat.SerializeTerrain( terrain.CaptureSave() ) );
	}

	public void PushTerrainClearToClients( string cellKey )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( cellKey ) ) return;
		BroadcastTerrainClear( cellKey );
	}

	/// <summary>
	/// Coalesce wild changes (catch, flee, top-up, plot buy) into one snapshot ~0.35s later.
	/// Calling this repeatedly only extends the window; it does not queue multiple pushes.
	/// </summary>
	public void ScheduleWildSync()
	{
		if ( !Networking.IsHost ) return;

		// AUDIT FIX B2: arm pending; debounce countdown starts / resets here.
		_wildSyncPending = true;
		_wildSyncDebounce = 0.35f;
	}

	public void PushWildToClients()
	{
		if ( !Networking.IsHost ) return;

		var spawner = WildernessSpawner.Instance;
		if ( !spawner.IsValid() ) return;

		BroadcastWildSnapshot( WorldSnapshotFormat.SerializeWild( spawner.CaptureSave() ) );
	}

	[Rpc.Broadcast]
	private void BroadcastTerrainSnapshot( string snapshot )
	{
		if ( Networking.IsHost ) return;
		TerrainObstacleSystem.Instance?.ApplyClientSnapshot( snapshot );
	}

	[Rpc.Broadcast]
	private void BroadcastTerrainClear( string cellKey )
	{
		if ( Networking.IsHost ) return;
		TerrainObstacleSystem.Instance?.ClearLocalOnly( cellKey );
	}

	[Rpc.Broadcast]
	private void BroadcastWildSnapshot( string snapshot )
	{
		if ( Networking.IsHost ) return;
		WildernessSpawner.Instance?.ApplyClientSnapshot( snapshot );
	}
}

internal static class WorldSnapshotFormat
{
	public static string SerializeTerrain( IEnumerable<TerrainObstacleSave> obstacles )
	{
		var parts = new List<string>();
		foreach ( var obstacle in obstacles )
		{
			if ( string.IsNullOrEmpty( obstacle.CellKey ) ) continue;
			parts.Add( $"{obstacle.CellKey}:{obstacle.Type}" );
		}

		return string.Join( "|", parts );
	}

	public static IEnumerable<TerrainObstacleSave> ParseTerrain( string snapshot )
	{
		if ( string.IsNullOrEmpty( snapshot ) )
			yield break;

		foreach ( var part in snapshot.Split( '|', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var split = part.Split( ':', 2 );
			if ( split.Length != 2 || !int.TryParse( split[1], out var type ) )
				continue;

			yield return new TerrainObstacleSave
			{
				CellKey = split[0],
				Type = type,
			};
		}
	}

	public static string SerializeWild( IEnumerable<WildAnimalSave> animals )
	{
		var parts = new List<string>();
		foreach ( var wild in animals )
		{
			if ( string.IsNullOrEmpty( wild.SpeciesId ) ) continue;
			var pos = wild.Position?.ToVector3() ?? Vector3.Zero;
			parts.Add(
				$"{wild.WildId};{wild.SpeciesId};{wild.PlotX};{wild.PlotY};" +
				$"{pos.x.ToString( "0.##", CultureInfo.InvariantCulture )};" +
				$"{pos.y.ToString( "0.##", CultureInfo.InvariantCulture )};" +
				$"{pos.z.ToString( "0.##", CultureInfo.InvariantCulture )}" );
		}

		return string.Join( "|", parts );
	}

	public static IEnumerable<WildAnimalSave> ParseWild( string snapshot )
	{
		if ( string.IsNullOrEmpty( snapshot ) )
			yield break;

		foreach ( var part in snapshot.Split( '|', StringSplitOptions.RemoveEmptyEntries ) )
		{
			var split = part.Split( ';' );
			if ( split.Length < 7 ) continue;
			if ( !int.TryParse( split[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var px )
				|| !int.TryParse( split[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var py ) )
				continue;
			if ( !float.TryParse( split[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var x )
				|| !float.TryParse( split[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var y )
				|| !float.TryParse( split[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var z ) )
				continue;

			yield return new WildAnimalSave
			{
				WildId = split[0],
				SpeciesId = split[1],
				PlotX = px,
				PlotY = py,
				Position = new SaveVector3( new Vector3( x, y, z ) ),
			};
		}
	}
}
