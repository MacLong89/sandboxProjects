namespace Fauna2;

/// <summary>
/// Host-authoritative trees and rocks scattered across the purchasable world grid.
/// Players clear them before building on occupied cells.
/// </summary>
public sealed class TerrainObstacleSystem : Component
{
	public static TerrainObstacleSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int TotalCleared { get; set; }

	// AUDIT FIX B7: Host-authoritative clear session.
	// Client UiState used to run a 3s local timer then call RequestClear with no
	// host duration/proximity check — walk away or forge the RPC still paid out.
	// Flow: RequestBeginClear arms _pendingClear*; RequestClear completes only if
	// still in range and enough host time elapsed. Revert: delete these fields +
	// RequestBeginClear and the checks inside RequestClear.
	private string _pendingClearCellKey;
	private TimeSince _pendingClearStarted;
	private long _pendingClearCallerSteamId;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public static string CellKey( int gx, int gy ) => $"{gx},{gy}";

	public static bool TryParseCellKey( string key, out int gx, out int gy )
	{
		gx = gy = 0;
		var parts = key.Split( ',' );
		return parts.Length == 2 && int.TryParse( parts[0], out gx ) && int.TryParse( parts[1], out gy );
	}

	public static Vector3 CellCenter( int gx, int gy )
	{
		var cell = GameConstants.ObstacleCellSize;
		return new Vector3(
			gx * cell + cell * 0.5f,
			gy * cell + cell * 0.5f,
			0f );
	}

	public static IEnumerable<string> CellsOverlapping( Vector3 center, Vector2 size )
	{
		var cell = GameConstants.ObstacleCellSize;
		var half = size * 0.5f;
		var minGx = (int)MathF.Floor( (center.x - half.x) / cell );
		var maxGx = (int)MathF.Floor( (center.x + half.x) / cell );
		var minGy = (int)MathF.Floor( (center.y - half.y) / cell );
		var maxGy = (int)MathF.Floor( (center.y + half.y) / cell );

		for ( var gx = minGx; gx <= maxGx; gx++ )
		{
			for ( var gy = minGy; gy <= maxGy; gy++ )
				yield return CellKey( gx, gy );
		}
	}

	/// <summary>Host only — scatter obstacles across the purchasable plot grid.</summary>
	public void GenerateWorld( Biome biome, PlotSystem plots, int saveSlotId = 0 )
	{
		if ( !Networking.IsHost || plots is null ) return;

		DestroyAll();

		var seed = saveSlotId > 0
			? HashCode.Combine( (int)biome, saveSlotId, 48271 )
			: HashCode.Combine( (int)biome, 48271 );
		var rng = new Random( seed );
		var cell = GameConstants.ObstacleCellSize;
		var half = GameConstants.PlayableHalfExtent;
		var minG = (int)MathF.Floor( -half / cell );
		var maxG = (int)MathF.Floor( half / cell );
		var checkedCells = 0;
		var purchasableCells = 0;
		var ownedCells = 0;
		var spawned = 0;
		var ownedSpawned = 0;
		var trees = 0;
		var rocks = 0;

		for ( var gx = minG; gx <= maxG; gx++ )
		{
			for ( var gy = minG; gy <= maxG; gy++ )
			{
				checkedCells++;
				var center = CellCenter( gx, gy );
				if ( MathF.Abs( center.x ) > half || MathF.Abs( center.y ) > half )
					continue;

				var (px, py) = PlotSystem.PlotAt( center );
				if ( Math.Abs( px ) > GameConstants.PlotGridRadius || Math.Abs( py ) > GameConstants.PlotGridRadius )
					continue;

				purchasableCells++;
				var owned = plots.IsOwned( px, py );
				if ( owned ) ownedCells++;

				if ( !WildernessBiomeMap.IsDryLandAt( center, biome ) )
					continue;

				var regionalBiome = WildernessBiomeMap.BiomeAtWorld( center, biome );
				var treeDensity = WildernessBiomeMap.TreeDensityAtWorld( center, biome );
				var density = (owned ? 0.38f : 0.26f) * (0.55f + treeDensity * 0.6f);
				density *= BiomeEcology.ObstacleDensityMultiplier( regionalBiome );
				if ( rng.NextSingle() > density )
					continue;

				var type = BiomeEcology.PickObstacleType( regionalBiome, rng );
				if ( type is null )
					continue;
				if ( Spawn( CellKey( gx, gy ), type.Value, regionalBiome, rng.Next() ) is null )
					continue;

				spawned++;
				if ( owned ) ownedSpawned++;
				if ( type.Value == TerrainObstacleType.Tree ) trees++;
				else rocks++;
			}
		}

		Log.Info( $"[Fauna2 World] Terrain obstacle generation: checkedCells={checkedCells}, purchasableCells={purchasableCells}, ownedCells={ownedCells}, spawnedNow={spawned}, ownedSpawned={ownedSpawned} (trees={trees}, rocks={rocks}), registryNow={TerrainObstacleRegistry.Count}, cellSize={cell:0.##}, playableHalf={half:0.##}." );
		ClientWorldSync.Instance?.PushTerrainToClients();
	}

	public void Restore( IEnumerable<TerrainObstacleSave> obstacles, Biome biome )
	{
		if ( !Networking.IsHost ) return;

		DestroyAll();

		foreach ( var save in obstacles )
		{
			if ( string.IsNullOrEmpty( save.CellKey ) ) continue;
			var center = CellCenterFromKey( save.CellKey );
			var regional = WildernessBiomeMap.BiomeAtWorld( center, biome );
			Spawn( save.CellKey, (TerrainObstacleType)save.Type, regional, HashCode.Combine( save.CellKey, save.Type ) );
		}

		ClientWorldSync.Instance?.PushTerrainToClients();
	}

	/// <summary>Client mirror — rebuild obstacles from a host snapshot.</summary>
	public void ApplyClientSnapshot( string snapshot )
	{
		if ( Networking.IsHost ) return;

		DestroyAll();

		var biome = ZooState.Instance?.StarterBiome ?? Biome.Grassland;
		foreach ( var save in WorldSnapshotFormat.ParseTerrain( snapshot ) )
		{
			var center = CellCenterFromKey( save.CellKey );
			var regional = WildernessBiomeMap.BiomeAtWorld( center, biome );
			Spawn( save.CellKey, (TerrainObstacleType)save.Type, regional, HashCode.Combine( save.CellKey, save.Type ) );
		}
	}

	/// <summary>Client mirror — remove one cleared obstacle without host rewards.</summary>
	public void ClearLocalOnly( string cellKey )
	{
		if ( Networking.IsHost || string.IsNullOrEmpty( cellKey ) ) return;

		var obstacle = TerrainObstacleRegistry.Find( cellKey );
		if ( obstacle is null || !obstacle.IsValid() ) return;

		obstacle.GameObject.Destroy();
	}

	public List<TerrainObstacleSave> CaptureSave()
	{
		var list = new List<TerrainObstacleSave>();

		foreach ( var obstacle in TerrainObstacleRegistry.All )
		{
			if ( !obstacle.IsValid() ) continue;

			list.Add( new TerrainObstacleSave
			{
				CellKey = obstacle.CellKey,
				Type = obstacle.Type,
			} );
		}

		return list;
	}

	/// <summary>
	/// AUDIT FIX B7: Start a host-tracked clear. UI still shows its own progress bar,
	/// but completion is rejected unless this was armed and time/range still pass.
	/// </summary>
	[Rpc.Host]
	public void RequestBeginClear( string cellKey )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		if ( string.IsNullOrEmpty( cellKey ) ) return;

		var obstacle = TerrainObstacleRegistry.Find( cellKey );
		if ( obstacle is null || !obstacle.IsValid() ) return;

		if ( !RpcAuthorization.IsCallerWithinRange( obstacle.WorldPosition, GameConstants.ObstacleClearRadius ) )
			return;

		var callerId = Rpc.Caller?.SteamId.Value ?? 0;

		// Same clear already armed — do not reset the host timer (UI retries Begin).
		if ( _pendingClearCellKey == cellKey
			&& ( _pendingClearCallerSteamId == 0 || _pendingClearCallerSteamId == callerId ) )
			return;

		_pendingClearCellKey = cellKey;
		_pendingClearStarted = 0f;
		_pendingClearCallerSteamId = callerId;
	}

	[Rpc.Host]
	public void RequestClear( string cellKey )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var obstacle = TerrainObstacleRegistry.Find( cellKey );
		if ( obstacle is null || !obstacle.IsValid() ) return;

		// AUDIT FIX B7: require matching begin + duration + still in range.
		var callerId = Rpc.Caller?.SteamId.Value ?? 0;
		// Require ~90% of the clear duration so RPC latency cannot soft-fail a legit clear.
		// Full Duration would reject when the client timer finished but the begin RPC arrived late.
		var clearedPending =
			!string.IsNullOrEmpty( _pendingClearCellKey )
			&& _pendingClearCellKey == cellKey
			&& ( _pendingClearCallerSteamId == 0 || _pendingClearCallerSteamId == callerId )
			&& _pendingClearStarted >= GameConstants.ObstacleClearDuration * 0.9f;

		if ( !clearedPending )
		{
			// Soft fail — UI retries for a short grace window; do not award.
			return;
		}

		if ( !RpcAuthorization.IsCallerWithinRange( obstacle.WorldPosition, GameConstants.ObstacleClearRadius ) )
		{
			_pendingClearCellKey = null;
			ZooState.Instance?.Notify( "Move closer to finish clearing.", "warning" );
			return;
		}

		var label = obstacle.DisplayName;

		_pendingClearCellKey = null;

		if ( !Clear( cellKey ) ) return;

		ClientWorldSync.Instance?.PushTerrainClearToClients( cellKey );

		var state = ZooState.Instance;
		if ( state.IsValid() )
		{
			state.AddMoney( GameConstants.ObstacleClearReward );
			state.AddXp( GameConstants.XpClearObstacle );
			state.Notify( $"Cleared a {label} (+${GameConstants.ObstacleClearReward:n0})", "forest" );
		}

		GameEvents.RaiseZooModified();
	}

	public bool Clear( string cellKey )
	{
		if ( !Networking.IsHost ) return false;

		var obstacle = TerrainObstacleRegistry.Find( cellKey );
		if ( obstacle is null || !obstacle.IsValid() ) return false;

		obstacle.GameObject.Destroy();
		TotalCleared++;
		return true;
	}

	private TerrainObstacleComponent Spawn( string cellKey, TerrainObstacleType type, Biome regionalBiome, int seed )
	{
		if ( TerrainObstacleRegistry.Find( cellKey ) is not null )
			return null;

		var go = new GameObject( true, $"Terrain {type}" );
		go.Tags.Add( "terrain_obstacle" );
		go.Tags.Add( "walk_block" );
		go.WorldPosition = CellCenterFromKey( cellKey );

		var obstacle = go.AddComponent<TerrainObstacleComponent>();
		obstacle.CellKey = cellKey;
		obstacle.Type = (int)type;

		var collider = go.AddComponent<SphereCollider>();
		collider.Radius = GameConstants.ObstacleBlockRadius;
		collider.Static = true;

		// Local-only — do not NetworkSpawn thousands of trees/rocks (exhausts networked entity budget).
		return obstacle;
	}

	private static Vector3 CellCenterFromKey( string cellKey )
	{
		if ( !TryParseCellKey( cellKey, out var gx, out var gy ) )
			return Vector3.Zero;

		return CellCenter( gx, gy );
	}

	private void DestroyAll()
	{
		foreach ( var obstacle in Scene.GetAllComponents<TerrainObstacleComponent>().ToList() )
		{
			if ( obstacle.IsValid() )
				obstacle.GameObject.Destroy();
		}

		TerrainObstacleRegistry.Clear();
	}
}

public sealed class TerrainObstacleSave
{
	public string CellKey { get; set; } = "";
	public int Type { get; set; }
}
