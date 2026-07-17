namespace Terraingen.World;

using Sandbox.Network;
using Terraingen;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Physics;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.Animals;
using Terraingen.TerrainGen;
using Terraingen.UI;

/// <summary>Host-spawned supply drops on a timer; synced visuals and map markers for all peers.</summary>
[Title( "Thorns Airdrop World" )]
[Category( "Thorns/World" )]
public sealed class ThornsAirdropWorldService : Component
{
	public const float SpawnIntervalSeconds = 15f * 60f;
	public const float LootHoldSeconds = 1.15f;
	public const float InteractRange = 220f;
	public const int MaxActiveDrops = 10;

	const string BoxModelPath = ThornsModelResourceLoad.DevBoxPath;

	public static ThornsAirdropWorldService Instance { get; private set; }

	readonly Dictionary<int, AirdropEntry> _airdrops = new();
	readonly HashSet<int> _looted = new();
	readonly List<ThornsBuildingLoot> _lootScratch = new();
	readonly List<int> _trimScratch = new();

	GameObject _root;
	Terrain _terrain;
	ThornsTerrainConfig _terrainConfig;
	Model _boxModel;
	Material _airdropMaterial;
	bool _spawnAssetsLoaded;
	int _nextId = 1;
	int _worldSeed;
	int _spawnWarnStreak;
	TimeUntil _nextSpawn;

	protected override void OnStart()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		Clear();
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_terrain.IsValid() )
			return;

		if ( _nextSpawn )
		{
			_nextSpawn = SpawnIntervalSeconds;
			TrySpawnDrop();
		}
	}

	public void OnWorldReady( Terrain terrain, ThornsTerrainConfig config )
	{
		ResetSession();
		_terrain = terrain;
		_terrainConfig = config;
		_worldSeed = config?.WorldSeed ?? 0;

		try
		{
			EnsureSpawnAssets();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Thorns Airdrops] Asset load deferred: {e.Message}" );
		}

		_nextSpawn = SpawnIntervalSeconds;
		Log.Info( "[Thorns Airdrops] World service ready — drops every 15 minutes." );
	}

	public void Clear()
	{
		ResetSession();
	}

	void ResetSession()
	{
		ThornsAirdropGarrison.HostClearAll();
		_airdrops.Clear();
		_looted.Clear();
		_nextId = 1;
		_spawnAssetsLoaded = false;
		_spawnWarnStreak = 0;

		if ( _root is not null && _root.IsValid() )
			_root.Destroy();

		_root = null;
	}

	public bool HasTargetInFront( GameObject playerRoot ) =>
		TryPickAlongRay( playerRoot, out _, out _ );

	public bool TryPickAlongRay( GameObject playerRoot, out int airdropId, out AirdropEntry entry )
	{
		airdropId = 0;
		entry = default;

		if ( !TryResolveAim( playerRoot, out var origin, out var forward ) )
			return false;

		if ( TryPickFromTrace( origin, forward, InteractRange, playerRoot, out airdropId, out entry ) )
			return true;

		return TryPickFromRegistry( origin, forward, InteractRange, out airdropId, out entry );
	}

	public bool TryGetAirdropWorldPosition( int airdropId, out Vector3 worldPos )
	{
		worldPos = default;
		if ( airdropId <= 0 || !_airdrops.TryGetValue( airdropId, out var entry ) )
			return false;

		worldPos = entry.WorldPosition;
		return true;
	}

	void TrySpawnDrop()
	{
		if ( !_terrain.IsValid() || Scene is null || !Scene.IsValid() )
			return;

		try
		{
			EnsureSpawnAssets();
			if ( !SpawnAssetsReady() )
				return;

			TrimOldestIfNeeded();

			var rng = new Random( HashCode.Combine( _worldSeed, _nextId, (int)(Time.Now * 1000), _airdrops.Count ) );
			var positionHits = 0;
			var visualFails = 0;

			for ( var attempt = 0; attempt < 64; attempt++ )
			{
				if ( !TryPickSpawnPosition( rng, out var worldPos ) )
					continue;

				positionHits++;
				var id = _nextId++;
				var lootSeed = rng.Next();
				var loot = ThornsAirdropLootTables.Roll( new Random( lootSeed ) );
				var obj = CreateCrateVisual( id, worldPos );
				if ( !obj.IsValid() )
				{
					visualFails++;
					continue;
				}

				var entry = new AirdropEntry( id, obj, worldPos, lootSeed, loot );
				_airdrops[id] = entry;
				ThornsWorldLootContainerService.Instance?.HostRegisterAirdrop( id, loot, lootSeed );

				ThornsGameplaySfx.PlayAirdropSpawnAt( worldPos );

				if ( Networking.IsActive && Networking.IsHost )
					HostBroadcastSpawnVisual( id, worldPos, lootSeed );

				SafeNotifyMapMarkersChanged();
				ThornsWorldEventHudBus.PushAirdropIncoming( worldPos.x, worldPos.y );
				ThornsAirdropGarrison.HostSpawnDefenders( Scene, id, worldPos );
				_spawnWarnStreak = 0;
				Log.Info( $"[Thorns Airdrops] Spawned drop #{id} at {worldPos}." );
				return;
			}

			LogSpawnFailure( positionHits, visualFails );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Thorns Airdrops] Spawn failed: {e}" );
		}
	}

	void EnsureSpawnAssets()
	{
		if ( _spawnAssetsLoaded && SpawnAssetsReady() )
			return;

		try
		{
			_boxModel = ThornsModelResourceLoad.LoadOrFallback( BoxModelPath );
			_airdropMaterial = ThornsAirdropVisual.ResolveMaterial();
			_spawnAssetsLoaded = SpawnAssetsReady();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Thorns Airdrops] Could not load airdrop assets yet: {e.Message}" );
		}
	}

	bool SpawnAssetsReady() => ModelReady( _boxModel );

	void LogSpawnFailure( int positionHits, int visualFails )
	{
		_spawnWarnStreak++;
		if ( _spawnWarnStreak != 1 && _spawnWarnStreak % 6 != 0 )
			return;

		if ( positionHits == 0 )
			Log.Warning( "[Thorns Airdrops] Failed to find valid spawn position." );
		else if ( visualFails > 0 )
			Log.Warning( $"[Thorns Airdrops] Found {positionHits} spawn point(s) but crate visual failed ({visualFails} attempt(s))." );
		else
			Log.Warning( "[Thorns Airdrops] Failed to spawn drop after exhausting attempts." );
	}

	static bool ModelReady( Model model )
	{
		try
		{
			return model.IsValid && !model.IsError;
		}
		catch ( NullReferenceException )
		{
			return false;
		}
	}

	Model ResolveBoxModel()
	{
		EnsureSpawnAssets();
		return _boxModel;
	}

	void EnsureDropRoot()
	{
		if ( _root.IsValid() )
			return;

		_root = null;
		if ( Scene is null || !Scene.IsValid() )
			return;

		_root = CreateRoot();
	}

	bool TryPickSpawnPosition( Random rng, out Vector3 worldPos )
	{
		worldPos = default;
		if ( Scene is null || !Scene.IsValid() || !TryGetSpawnBounds( out var minX, out var maxX, out var minY, out var maxY ) )
			return false;

		var x = minX + rng.NextSingle() * (maxX - minX);
		var y = minY + rng.NextSingle() * (maxY - minY);
		var requested = new Vector3( x, y, 0f );
		return ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( Scene, requested, 640f, out worldPos, out _ );
	}

	bool TryGetSpawnBounds( out float minX, out float maxX, out float minY, out float maxY )
	{
		minX = maxX = minY = maxY = 0f;
		if ( !_terrain.IsValid() )
			return false;

		var terrainSize = _terrain.TerrainSize;
		var origin = _terrain.GameObject.WorldPosition;
		var margin = Math.Clamp( terrainSize * 0.06f, 600f, terrainSize * 0.42f );

		minX = origin.x + margin;
		maxX = origin.x + terrainSize - margin;
		minY = origin.y + margin;
		maxY = origin.y + terrainSize - margin;

		if ( maxX - minX < 256f || maxY - minY < 256f )
		{
			margin = Math.Max( 64f, terrainSize * 0.02f );
			minX = origin.x + margin;
			maxX = origin.x + terrainSize - margin;
			minY = origin.y + margin;
			maxY = origin.y + terrainSize - margin;
		}

		return maxX > minX && maxY > minY;
	}

	GameObject CreateCrateVisual( int id, Vector3 worldPos )
	{
		if ( Scene is null || !Scene.IsValid() )
			return null;

		EnsureDropRoot();
		if ( !_root.IsValid() )
			return null;

		EnsureSpawnAssets();
		var model = ResolveBoxModel();
		if ( !ModelReady( model ) )
			return null;

		var scale = ThornsAirdropVisual.ScaleBox( model );

		var obj = Scene.CreateObject( true );
		if ( !obj.IsValid() )
			return null;

		obj.Name = $"Airdrop {id}";
		obj.Parent = _root;
		obj.WorldPosition = ThornsAirdropVisual.GroundCenterPosition( worldPos );
		obj.WorldRotation = Rotation.FromYaw( new Random( id ).NextSingle() * 360f );
		obj.LocalScale = scale;

		var renderer = obj.Components.Create<ModelRenderer>();
		if ( renderer is null )
		{
			obj.Destroy();
			return null;
		}

		renderer.Model = model;
		renderer.MaterialOverride = _airdropMaterial;
		renderer.Tint = Color.White;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		TryAddTag( obj, "airdrop" );

		var bounds = ThornsAirdropVisual.ResolveColliderBounds( model );
		var collider = obj.Components.Create<BoxCollider>();
		collider.Center = bounds.Center;
		collider.Scale = bounds.Size;
		collider.Static = true;

		var marker = obj.Components.Create<ThornsLootableAirdrop>();
		marker.AirdropId = id;

		return obj;
	}

	static void TryAddTag( GameObject obj, string tag )
	{
		if ( !obj.IsValid() || string.IsNullOrWhiteSpace( tag ) || obj.Tags is null )
			return;

		if ( !obj.Tags.Contains( tag ) )
			obj.Tags.Add( tag );
	}

	GameObject CreateRoot()
	{
		var root = Scene.CreateObject( true );
		root.Name = "Thorns Airdrops";
		if ( GameObject.IsValid() )
			root.Parent = GameObject;
		return root;
	}

	void TrimOldestIfNeeded()
	{
		_trimScratch.Clear();
		foreach ( var id in _airdrops.Keys )
		{
			if ( !_looted.Contains( id ) )
				_trimScratch.Add( id );
		}

		_trimScratch.Sort();
		while ( _trimScratch.Count >= MaxActiveDrops )
		{
			var removeId = _trimScratch[0];
			_trimScratch.RemoveAt( 0 );
			HostDespawnExpired( removeId );
		}
	}

	/// <summary>
	/// AUDIT NOTE: unused legacy bulk-loot API. Live path is container UI.
	/// See death-crate notes — do not reintroduce without syncing WorldLootContainerService.
	/// </summary>
	[Obsolete( "Use world container UI / ThornsWorldLootContainerService moves instead of bulk HostTryLoot." )]
	public bool HostTryLoot( GameObject playerRoot, int airdropId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_airdrops.TryGetValue( airdropId, out var entry ) )
			return false;

		if ( _looted.Contains( airdropId ) || !entry.Object.IsValid() )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			return false;

		if ( Vector3.DistanceBetween( playerRoot.WorldPosition, entry.WorldPosition ) > InteractRange + 40f )
			return false;

		_lootScratch.Clear();
		_lootScratch.AddRange( entry.Loot );

		foreach ( var loot in _lootScratch )
			gameplay.HostGrantHarvestItem( loot.ItemId, loot.Count );

		gameplay.HostTryFireMilestoneEventOnce( "loot_airdrop" );
		HostDespawnWhenEmpty( airdropId );
		return true;
	}

	public void HostDespawnWhenEmpty( int airdropId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_airdrops.ContainsKey( airdropId ) )
			return;

		ThornsWorldLootContainerService.Instance?.HostUnregister( ThornsWorldLootContainerService.AirdropKey( airdropId ) );

		var broadcastPos = Vector3.Zero;
		if ( _airdrops.TryGetValue( airdropId, out var entry ) )
			broadcastPos = entry.WorldPosition;

		_looted.Add( airdropId );
		RemoveDrop( airdropId );

		if ( Networking.IsActive )
			HostBroadcastSetLooted( airdropId, broadcastPos );

		SafeNotifyMapMarkersChanged();
	}

	static void SafeNotifyMapMarkersChanged()
	{
		try
		{
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Airdrops] Map marker refresh failed after spawn." );
		}
	}

	void RemoveDrop( int airdropId )
	{
		ThornsAirdropGarrison.HostReleaseGarrison( airdropId );

		if ( _airdrops.TryGetValue( airdropId, out var entry ) && entry.Object.IsValid() )
			entry.Object.Destroy();

		_airdrops.Remove( airdropId );
	}

	void HostDespawnExpired( int airdropId )
	{
		var broadcastPos = Vector3.Zero;
		if ( _airdrops.TryGetValue( airdropId, out var entry ) )
			broadcastPos = entry.WorldPosition;

		RemoveDrop( airdropId );
		if ( Networking.IsActive )
			HostBroadcastDespawnVisual( airdropId, broadcastPos );
	}

	void HostBroadcastSpawnVisual( int airdropId, Vector3 worldPosition, int lootSeed )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcSpawnVisual( airdropId, worldPosition, lootSeed ) );

	void HostBroadcastDespawnVisual( int airdropId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcDespawnVisual( airdropId ) );

	void HostBroadcastSetLooted( int airdropId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcSetLooted( airdropId ) );

	[Rpc.Broadcast]
	void RpcSpawnVisual( int airdropId, Vector3 worldPosition, int lootSeed )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline || _airdrops.ContainsKey( airdropId ) )
			return;

		var loot = ThornsAirdropLootTables.Roll( new Random( lootSeed ) );
		var obj = CreateCrateVisual( airdropId, worldPosition );
		if ( !obj.IsValid() )
			return;

		_airdrops[airdropId] = new AirdropEntry( airdropId, obj, worldPosition, lootSeed, loot );
		ThornsWorldLootContainerService.Instance?.HostRegisterAirdrop( airdropId, loot, lootSeed );
		SafeNotifyMapMarkersChanged();
		ThornsWorldEventHudBus.PushAirdropIncoming( worldPosition.x, worldPosition.y );
		ThornsGameplaySfx.PlayAirdropSpawnAt( worldPosition );
	}

	[Rpc.Broadcast]
	void RpcSetLooted( int airdropId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		_looted.Add( airdropId );
		DespawnLocal( airdropId );
		SafeNotifyMapMarkersChanged();
	}

	[Rpc.Broadcast]
	void RpcDespawnVisual( int airdropId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		DespawnLocal( airdropId );
	}

	void DespawnLocal( int airdropId )
	{
		if ( _airdrops.TryGetValue( airdropId, out var entry ) && entry.Object.IsValid() )
			entry.Object.Destroy();

		_airdrops.Remove( airdropId );
	}

	public void AppendMapMarkers( List<ThornsMapMarkerDto> markers )
	{
		foreach ( var (id, entry) in _airdrops )
		{
			if ( _looted.Contains( id ) )
				continue;

			markers.Add( new ThornsMapMarkerDto
			{
				Id = $"airdrop_{id}",
				Kind = ThornsMapMarkerKind.Airdrop,
				WorldX = entry.WorldPosition.x,
				WorldY = entry.WorldPosition.y,
				Label = "Supply Drop"
			} );
		}
	}

	bool TryPickFromTrace( Vector3 origin, Vector3 forward, float maxRange, GameObject ignoreRoot, out int airdropId, out AirdropEntry entry )
	{
		airdropId = 0;
		entry = default;

		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var end = origin + dir * maxRange;
		var trace = Scene.Trace
			.Sphere( Terraingen.Combat.ThornsInteractAimPick.DefaultSphereTraceRadius, origin, end )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.Run();

		return trace.Hit
		       && trace.GameObject.IsValid()
		       && TryAcceptAirdropHit( trace.GameObject, out airdropId, out entry );
	}

	bool TryAcceptAirdropHit( GameObject hit, out int airdropId, out AirdropEntry entry )
	{
		airdropId = 0;
		entry = default;

		var marker = hit.Components.Get<ThornsLootableAirdrop>( FindMode.EverythingInSelfAndParent );
		if ( !marker.IsValid() || marker.AirdropId <= 0 || _looted.Contains( marker.AirdropId ) )
			return false;

		if ( !_airdrops.TryGetValue( marker.AirdropId, out entry ) )
			return false;

		airdropId = marker.AirdropId;
		return true;
	}

	bool TryPickFromRegistry( Vector3 origin, Vector3 forward, float maxRange, out int airdropId, out AirdropEntry entry )
	{
		airdropId = 0;
		entry = default;
		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var best = float.MaxValue;
		foreach ( var (id, drop) in _airdrops )
		{
			if ( _looted.Contains( id ) || !drop.Object.IsValid() )
				continue;

			var center = drop.WorldPosition;
			var pickRadius = 56f;
			var collider = drop.Object.Components.Get<BoxCollider>();
			if ( collider.IsValid() )
			{
				center = drop.Object.WorldPosition + collider.Center * drop.Object.WorldScale;
				pickRadius = MathF.Max( pickRadius, collider.Scale.Length * 0.55f );
			}

			if ( !Terraingen.Combat.ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along )
			     || along < 8f
			     || along > maxRange
			     || along >= best )
				continue;

			best = along;
			airdropId = id;
			entry = drop;
		}

		return airdropId > 0;
	}

	static bool TryResolveAim( GameObject playerRoot, out Vector3 origin, out Vector3 forward )
		=> Terraingen.Combat.ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out origin, out forward );

	public readonly struct AirdropEntry
	{
		public readonly int Id;
		public readonly GameObject Object;
		public readonly Vector3 WorldPosition;
		public readonly int LootSeed;
		public readonly IReadOnlyList<ThornsBuildingLoot> Loot;

		public AirdropEntry( int id, GameObject obj, Vector3 worldPosition, int lootSeed, IReadOnlyList<ThornsBuildingLoot> loot )
		{
			Id = id;
			Object = obj;
			WorldPosition = worldPosition;
			LootSeed = lootSeed;
			Loot = loot;
		}
	}
}

public sealed class ThornsLootableAirdrop : Component
{
	[Property] public int AirdropId { get; set; }
}
