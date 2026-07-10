namespace Fauna2;

/// <summary>
/// Host-authoritative trees and rocks scattered across the purchasable world grid.
/// Players clear them before building on occupied cells.
/// </summary>
public sealed class TerrainObstacleSystem : Component
{
	public static TerrainObstacleSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public int TotalCleared { get; set; }

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

	[Rpc.Host]
	public void RequestClear( string cellKey )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var obstacle = TerrainObstacleRegistry.Find( cellKey );
		if ( obstacle is null || !obstacle.IsValid() ) return;

		var label = obstacle.DisplayName;

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
