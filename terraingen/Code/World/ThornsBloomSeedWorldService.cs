namespace Terraingen.World;

using Sandbox.Network;
using Terraingen;
using Terraingen.Animals;
using Terraingen.Core;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Physics;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.Victory;

/// <summary>Timed Bloom infection nodes that spawn corrupted creatures and feed Purification progress.</summary>
[Title( "Thorns Bloom Seed World" )]
[Category( "Thorns/World" )]
public sealed class ThornsBloomSeedWorldService : Component
{
	public const float SpawnIntervalMinSeconds = 10f * 60f;
	public const float SpawnIntervalMaxSeconds = 20f * 60f;
	public const float PurifyHoldSeconds = 5f;
	public const float InteractRange = 220f;
	public const float InfectionRadius = 1800f;
	public const int SpawnedCreatureCount = 3;
	public const int XpPurified = 300;
	public const int MaxActiveSeeds = 12;

	const string BoxModelPath = "models/bloomseed/bloomseed.vmdl";
	const string FallbackBoxModelPath = ThornsModelResourceLoad.DevBoxPath;
	const string VictorySourceKey = "bloom_seed_purified";

	static readonly Vector3 SeedWorldSize = new( 60f, 60f, 110f );

	public static ThornsBloomSeedWorldService Instance { get; private set; }

	/// <summary>Prefer the terrain bootstrap host so we do not tick a stale scene-wide instance.</summary>
	public static ThornsBloomSeedWorldService EnsureOnHost( GameObject hostObject )
	{
		if ( hostObject is null || !hostObject.IsValid() )
			return Instance is not null && Instance.IsValid() ? Instance : null;

		var service = hostObject.Components.Get<ThornsBloomSeedWorldService>();
		if ( service is null || !service.IsValid() )
			service = hostObject.Components.Create<ThornsBloomSeedWorldService>();

		Instance = service;
		return service;
	}

	readonly Dictionary<int, BloomSeedEntry> _seeds = new();
	readonly HashSet<int> _purified = new();
	readonly List<int> _trimScratch = new();

	GameObject _root;
	Terrain _terrain;
	ThornsTerrainConfig _terrainConfig;
	Model _boxModel;
	bool _spawnAssetsLoaded;
	int _nextId = 1;
	int _worldSeed;
	TimeUntil _nextSpawn;
	TimeUntil _nextInfectionTick;
	bool _initialSeedSpawned;
	int _spawnWarnStreak;
	int _initialSpawnRetryLogStreak;

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
		if ( !ThornsMultiplayer.IsHostOrOffline || Instance != this )
			return;

		if ( !ResolveTerrain().IsValid() )
			return;

		if ( _nextSpawn )
		{
			if ( !_initialSeedSpawned )
				HostEnsureInitialSeed();
			else
			{
				_nextSpawn = RollNextSpawnDelay();
				TrySpawnSeed();
			}
		}

		if ( _nextInfectionTick )
		{
			_nextInfectionTick = 5f;
			HostInfectNearbyWildlife();
		}
	}

	public void OnWorldReady( Terrain terrain, ThornsTerrainConfig config )
	{
		Instance = this;
		ResetSession();
		_terrain = terrain;
		_terrainConfig = config;
		_worldSeed = config?.WorldSeed ?? 0;

		EnsureSpawnAssetsSafe();
		_nextInfectionTick = 5f;

		if ( TrySpawnSeed( "initial world seed" ) )
		{
			_initialSeedSpawned = true;
			_nextSpawn = RollNextSpawnDelay();
			Log.Info( "[Thorns Bloom] World service ready - initial seed placed, next spawn in 10-20 minutes." );
			return;
		}

		_nextSpawn = 2f;
		Log.Warning( $"[Thorns Bloom] Initial seed deferred ({DescribeSpawnBlocker()}) - retry in ~2s, then every 10-20 minutes." );
	}

	public void Clear() => ResetSession();

	void ResetSession()
	{
		_seeds.Clear();
		_purified.Clear();
		_nextId = 1;
		_spawnAssetsLoaded = false;
		_initialSeedSpawned = false;
		_spawnWarnStreak = 0;
		_initialSpawnRetryLogStreak = 0;

		if ( _root.IsValid() )
			_root.Destroy();

		_root = null;
	}

	public bool HasTargetInFront( GameObject playerRoot ) =>
		TryPickAlongRay( playerRoot, out _, out _ );

	public bool TryPickAlongRay( GameObject playerRoot, out int seedId, out BloomSeedEntry entry )
	{
		seedId = 0;
		entry = default;

		if ( !TryResolveAim( playerRoot, out var origin, out var forward ) )
			return false;

		if ( TryPickFromTrace( origin, forward, InteractRange, playerRoot, out seedId, out entry ) )
			return true;

		return TryPickFromRegistry( origin, forward, InteractRange, out seedId, out entry );
	}

	public bool HostTryPurifyResolved( ThornsPlayerGameplay gameplay )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !gameplay.IsValid() )
			return false;

		if ( !TryPickAlongRay( gameplay.GameObject, out var seedId, out _ ) )
			return false;

		return HostTryPurify( gameplay, seedId );
	}

	public bool HostTryPurify( ThornsPlayerGameplay gameplay, int seedId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !gameplay.IsValid() )
			return false;

		if ( !_seeds.TryGetValue( seedId, out var entry ) || _purified.Contains( seedId ) || !entry.Object.IsValid() )
			return false;

		if ( Vector3.DistanceBetween( gameplay.GameObject.WorldPosition, entry.WorldPosition ) > InteractRange + 40f )
			return false;

		_purified.Add( seedId );
		var broadcastPos = entry.WorldPosition;
		RemoveSeed( seedId );

		gameplay.HostGrantXp( XpPurified );
		ThornsVictoryBridge.Report( gameplay, VictorySourceKey );
		gameplay.PushMilestoneToastToOwner( "Bloom Seed Purified", XpPurified );
		gameplay.HostRecordJournalWorldEvent( "event_bloom_purified" );

		if ( Networking.IsActive )
			HostBroadcastPurified( seedId, broadcastPos );

		SafeNotifyMapMarkersChanged();
		Log.Info( $"[Thorns Bloom] Seed #{seedId} purified by {gameplay.AccountKey}." );
		return true;
	}

	void HostEnsureInitialSeed()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || _initialSeedSpawned )
			return;

		if ( TrySpawnSeed( "initial world seed" ) )
		{
			_initialSeedSpawned = true;
			_nextSpawn = RollNextSpawnDelay();
			return;
		}

		_nextSpawn = SpawnAssetsReady() ? 8f : 2f;

		if ( _initialSpawnRetryLogStreak++ % 3 == 0 )
			Log.Warning( $"[Thorns Bloom] Initial seed still pending ({DescribeSpawnBlocker()})." );
	}

	bool TrySpawnSeed( string reason = "timed spawn" )
	{
		if ( !ResolveTerrain().IsValid() || Scene is null || !Scene.IsValid() )
			return false;

		try
		{
			EnsureSpawnAssetsSafe();
			if ( !SpawnAssetsReady() )
			{
				_spawnWarnStreak++;
				if ( _spawnWarnStreak == 1 || _spawnWarnStreak % 6 == 0 )
					Log.Warning( $"[Thorns Bloom] Spawn assets not ready ({DescribeModelState()})." );

				return false;
			}

			TrimOldestIfNeeded();
			var rng = new Random( HashCode.Combine( _worldSeed, _nextId, (int)(Time.Now * 1000), _seeds.Count ) );
			var positionHits = 0;
			var visualFails = 0;

			for ( var attempt = 0; attempt < 64; attempt++ )
			{
				if ( !TryPickSpawnPosition( rng, out var worldPos ) )
					continue;

				positionHits++;
				var id = _nextId++;
				var obj = CreateSeedVisual( id, worldPos );
				if ( !obj.IsValid() )
				{
					visualFails++;
					continue;
				}

				_seeds[id] = new BloomSeedEntry( id, obj, worldPos );
				HostSpawnBloomedCreatures( id, worldPos );

				if ( Networking.IsActive && Networking.IsHost )
					HostBroadcastSpawnVisual( id, worldPos );

				SafeNotifyMapMarkersChanged();
				ThornsWorldEventHudBus.PushBloomSeedDetected( worldPos.x, worldPos.y );
				_spawnWarnStreak = 0;
				Log.Info( $"[Thorns Bloom] Spawned Bloom Seed #{id} at {worldPos} ({reason})." );
				return true;
			}

			LogSpawnFailure( positionHits, visualFails );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Thorns Bloom] Spawn failed: {e}" );
		}

		return false;
	}

	bool SpawnAssetsReady() => ModelReady( _boxModel );

	void LogSpawnFailure( int positionHits, int visualFails )
	{
		_spawnWarnStreak++;
		if ( _spawnWarnStreak != 1 && _spawnWarnStreak % 6 != 0 )
			return;

		if ( positionHits == 0 )
		{
			Log.Warning( "[Thorns Bloom] Failed to find dry land for a Bloom Seed spawn." );
			return;
		}

		Log.Warning( $"[Thorns Bloom] Found {positionHits} spawn point(s) but could not create Bloom Seed visuals ({visualFails} failed)." );
	}

	void HostSpawnBloomedCreatures( int seedId, Vector3 seedWorldPos )
	{
		ThornsAnimalSpeciesRegistry.EnsureInitialized();
		var spawned = 0;
		for ( var i = 0; i < SpawnedCreatureCount; i++ )
		{
			if ( !TryPickBloomSpecies( out var species ) )
				break;

			var angle = (i / (float)SpawnedCreatureCount) * MathF.PI * 2f + Game.Random.Float( -0.35f, 0.35f );
			var requested = seedWorldPos + new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * Game.Random.Float( 180f, 360f );
			if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( Scene, requested, species.WanderRadius, out var pos, out _ ) )
				continue;

			ThornsAnimalSeparation.TryResolveClearSpawn( Scene, species, pos, out pos );
			var brain = ThornsAnimalFactory.HostSpawn( Scene, species, pos, Rotation.FromYaw( Game.Random.Float( 0f, 360f ) ), ignorePopulationCap: true );
			if ( !brain.IsValid() )
				continue;

			brain.GameObject.Name = $"Bloomed {species.DisplayName} ({seedId})";
			brain.HostApplyBloomMutation();
			spawned++;
		}

		if ( spawned < SpawnedCreatureCount )
			Log.Warning( $"[Thorns Bloom] Seed #{seedId} spawned {spawned}/{SpawnedCreatureCount} Bloomed creature(s)." );
	}

	static bool TryPickBloomSpecies( out ThornsAnimalSpeciesData species )
	{
		var key = Game.Random.Float( 0f, 1f ) < 0.55f ? "wolf" : "panther";
		if ( ThornsAnimalSpeciesRegistry.TryGet( key, out species ) )
			return true;

		if ( ThornsAnimalSpeciesRegistry.TryGet( ThornsAnimalSpeciesCatalog.WolfId, out species ) )
			return true;

		return ThornsAnimalSpeciesRegistry.TryGet( ThornsAnimalSpeciesCatalog.PantherId, out species );
	}

	void HostInfectNearbyWildlife()
	{
		if ( _seeds.Count == 0 )
			return;

		var radiusSq = InfectionRadius * InfectionRadius;
		var seedPositions = new Vector3[_seeds.Count];
		var seedIndex = 0;
		foreach ( var (_, seed) in _seeds )
			seedPositions[seedIndex++] = seed.WorldPosition;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || brain.IsDead || brain.IsBloomed || brain.IsTamed )
				continue;

			var species = brain.Species;
			if ( species is null || species.BaseDamage <= 0f )
				continue;

			if ( species.BehaviorType != ThornsAnimalBehaviorType.Predator
			     && species.BehaviorType != ThornsAnimalBehaviorType.Mixed )
				continue;

			var animalPos = brain.GameObject.WorldPosition;
			for ( var s = 0; s < seedPositions.Length; s++ )
			{
				if ( (animalPos - seedPositions[s]).LengthSquared > radiusSq )
					continue;

				brain.HostApplyBloomMutation();
				break;
			}
		}
	}

	bool TryPickSpawnPosition( Random rng, out Vector3 worldPos )
	{
		worldPos = default;
		if ( !TryGetSpawnBounds( out var minX, out var maxX, out var minY, out var maxY ) )
			return false;

		var x = minX + rng.NextSingle() * (maxX - minX);
		var y = minY + rng.NextSingle() * (maxY - minY);
		var requested = new Vector3( x, y, 0f );
		return ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( Scene, requested, 640f, out worldPos, out _ );
	}

	Terrain ResolveTerrain()
	{
		if ( _terrain.IsValid() )
			return _terrain;

		_terrain = ThornsTerrainCache.Resolve( Scene );
		return _terrain;
	}

	string DescribeSpawnBlocker()
	{
		if ( !ResolveTerrain().IsValid() )
			return "terrain missing";

		if ( Scene is null || !Scene.IsValid() )
			return "scene invalid";

		if ( !SpawnAssetsReady() )
			return DescribeModelState();

		return "no valid dry-land position";
	}

	bool TryGetSpawnBounds( out float minX, out float maxX, out float minY, out float maxY )
	{
		minX = maxX = minY = maxY = 0f;
		var terrain = ResolveTerrain();
		if ( !terrain.IsValid() )
			return false;

		var terrainSize = terrain.TerrainSize;
		var origin = terrain.GameObject.WorldPosition;
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

	void EnsureSpawnAssetsSafe()
	{
		if ( _spawnAssetsLoaded && SpawnAssetsReady() )
			return;

		try
		{
			_boxModel = ThornsModelResourceLoad.LoadOrFallback( BoxModelPath, FallbackBoxModelPath );
			_spawnAssetsLoaded = ModelReady( _boxModel );

			if ( _spawnAssetsLoaded )
				Log.Info( $"[Thorns Bloom] Spawn model ready: '{_boxModel.Name}'." );
		}
		catch ( Exception e )
		{
			_spawnAssetsLoaded = false;
			Log.Warning( $"[Thorns Bloom] Could not load Bloom Seed assets yet: {e.Message}" );
		}
	}

	string DescribeModelState()
	{
		try
		{
			if ( !_boxModel.IsValid )
				return $"model invalid (primary='{BoxModelPath}', fallback='{FallbackBoxModelPath}')";

			if ( _boxModel.IsError )
				return $"model error '{_boxModel.Name}'";

			return "model loading";
		}
		catch ( NullReferenceException )
		{
			return $"model not loaded (primary='{BoxModelPath}', fallback='{FallbackBoxModelPath}')";
		}
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

	void EnsureRoot()
	{
		if ( _root.IsValid() )
			return;

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Bloom Seeds";
		if ( GameObject.IsValid() )
			_root.Parent = GameObject;
	}

	GameObject CreateSeedVisual( int id, Vector3 surfacePos )
	{
		if ( Scene is null || !Scene.IsValid() )
			return null;

		EnsureRoot();
		if ( !_root.IsValid() )
			return null;

		EnsureSpawnAssetsSafe();
		if ( !ModelReady( _boxModel ) )
			return null;

		var obj = Scene.CreateObject( true );
		obj.Name = $"Bloom Seed {id}";
		obj.Parent = _root;
		var scale = ThornsNatureScaleVariance.Apply( ScaleBox( _boxModel ), new Random( id ) );
		obj.LocalScale = scale;
		obj.WorldPosition = GroundPosition( surfacePos, _boxModel, scale );
		obj.WorldRotation = Rotation.FromYaw( new Random( id ).NextSingle() * 360f );

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = _boxModel;
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		TryAddTag( obj, "bloom_seed" );

		var collider = obj.Components.Create<BoxCollider>();
		collider.Center = ResolveColliderBounds( _boxModel ).Center;
		collider.Scale = ResolveColliderBounds( _boxModel ).Size;
		collider.Static = true;

		var marker = obj.Components.Create<ThornsBloomSeedMarker>();
		marker.SeedId = id;
		return obj;
	}

	static Vector3 GroundPosition( Vector3 surfacePos, Model model, Vector3 scale )
	{
		if ( !model.IsValid() )
			return surfacePos + Vector3.Up * (SeedWorldSize.z * 0.5f);

		var bounds = model.Bounds;
		return surfacePos + Vector3.Up * (-bounds.Mins.z * scale.z);
	}

	static Vector3 ScaleBox( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
		{
			var bounds = model.Bounds;
			return new Vector3(
				SeedWorldSize.x / Math.Max( 1f, bounds.Size.x ),
				SeedWorldSize.y / Math.Max( 1f, bounds.Size.y ),
				SeedWorldSize.z / Math.Max( 1f, bounds.Size.z ) );
		}

		return new Vector3( 1.6f, 0.9f, 1f );
	}

	static BBox ResolveColliderBounds( Model model )
	{
		if ( model.IsValid && model.Bounds.Size.LengthSquared > 1e-8f )
			return model.Bounds;

		return new BBox( new Vector3( -25f, -25f, -25f ), new Vector3( 25f, 25f, 25f ) );
	}

	static void TryAddTag( GameObject obj, string tag )
	{
		if ( !obj.IsValid() || string.IsNullOrWhiteSpace( tag ) || obj.Tags is null )
			return;

		if ( !obj.Tags.Contains( tag ) )
			obj.Tags.Add( tag );
	}

	void TrimOldestIfNeeded()
	{
		_trimScratch.Clear();
		foreach ( var id in _seeds.Keys )
		{
			if ( !_purified.Contains( id ) )
				_trimScratch.Add( id );
		}

		_trimScratch.Sort();
		while ( _trimScratch.Count >= MaxActiveSeeds )
		{
			var removeId = _trimScratch[0];
			_trimScratch.RemoveAt( 0 );
			HostDespawnExpired( removeId );
		}
	}

	void RemoveSeed( int seedId )
	{
		if ( _seeds.TryGetValue( seedId, out var entry ) && entry.Object.IsValid() )
			entry.Object.Destroy();

		_seeds.Remove( seedId );
	}

	void HostDespawnExpired( int seedId )
	{
		var broadcastPos = Vector3.Zero;
		if ( _seeds.TryGetValue( seedId, out var entry ) )
			broadcastPos = entry.WorldPosition;

		RemoveSeed( seedId );
		if ( Networking.IsActive )
			HostBroadcastDespawnVisual( seedId, broadcastPos );
	}

	void HostBroadcastSpawnVisual( int seedId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcSpawnVisual( seedId, worldPosition ) );

	void HostBroadcastDespawnVisual( int seedId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcDespawnVisual( seedId ) );

	void HostBroadcastPurified( int seedId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcSetPurified( seedId ) );

	[Rpc.Broadcast]
	void RpcSpawnVisual( int seedId, Vector3 worldPosition )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline || _seeds.ContainsKey( seedId ) )
			return;

		var obj = CreateSeedVisual( seedId, worldPosition );
		if ( !obj.IsValid() )
			return;

		_seeds[seedId] = new BloomSeedEntry( seedId, obj, worldPosition );
		SafeNotifyMapMarkersChanged();
		ThornsWorldEventHudBus.PushBloomSeedDetected( worldPosition.x, worldPosition.y );
	}

	[Rpc.Broadcast]
	void RpcSetPurified( int seedId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		_purified.Add( seedId );
		DespawnLocal( seedId );
		SafeNotifyMapMarkersChanged();
	}

	[Rpc.Broadcast]
	void RpcDespawnVisual( int seedId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		DespawnLocal( seedId );
		SafeNotifyMapMarkersChanged();
	}

	void DespawnLocal( int seedId )
	{
		if ( _seeds.TryGetValue( seedId, out var entry ) && entry.Object.IsValid() )
			entry.Object.Destroy();

		_seeds.Remove( seedId );
	}

	public void AppendMapMarkers( List<ThornsMapMarkerDto> markers )
	{
		foreach ( var (id, entry) in _seeds )
		{
			if ( _purified.Contains( id ) )
				continue;

			markers.Add( new ThornsMapMarkerDto
			{
				Id = $"bloom_seed_{id}",
				Kind = ThornsMapMarkerKind.BloomSeed,
				WorldX = entry.WorldPosition.x,
				WorldY = entry.WorldPosition.y,
				Label = $"Bloom Seed {id}"
			} );
		}
	}

	bool TryPickFromTrace( Vector3 origin, Vector3 forward, float maxRange, GameObject ignoreRoot, out int seedId, out BloomSeedEntry entry )
	{
		seedId = 0;
		entry = default;

		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var trace = Scene.Trace.Ray( origin, origin + dir * maxRange )
			.WithTag( "bloom_seed" )
			.IgnoreGameObjectHierarchy( ignoreRoot )
			.Run();

		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return false;

		var marker = trace.GameObject.Components.Get<ThornsBloomSeedMarker>( FindMode.EverythingInSelfAndParent );
		if ( !marker.IsValid() || marker.SeedId <= 0 || _purified.Contains( marker.SeedId ) )
			return false;

		if ( !_seeds.TryGetValue( marker.SeedId, out entry ) )
			return false;

		seedId = marker.SeedId;
		return true;
	}

	bool TryPickFromRegistry( Vector3 origin, Vector3 forward, float maxRange, out int seedId, out BloomSeedEntry entry )
	{
		seedId = 0;
		entry = default;
		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var best = float.MaxValue;
		foreach ( var (id, seed) in _seeds )
		{
			if ( _purified.Contains( id ) )
				continue;

			var to = seed.WorldPosition - origin;
			var along = Vector3.Dot( to, dir );
			if ( along < 8f || along > maxRange )
				continue;

			var perp = (to - dir * along).Length;
			if ( perp > 95f )
				continue;

			if ( along < best )
			{
				best = along;
				seedId = id;
				entry = seed;
			}
		}

		return seedId > 0;
	}

	static bool TryResolveAim( GameObject playerRoot, out Vector3 origin, out Vector3 forward )
	{
		if ( ThornsSceneObserver.TryResolveLocalAimRay( playerRoot, out origin, out forward ) )
			return true;

		var controller = playerRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf );
		if ( !controller.IsValid() )
			return false;

		origin = playerRoot.WorldPosition + Vector3.Up * 64f;
		forward = controller.EyeAngles.ToRotation().Forward.Normal;
		return true;
	}

	static void SafeNotifyMapMarkersChanged()
	{
		try
		{
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Bloom] Map marker refresh failed." );
		}
	}

	static float RollNextSpawnDelay() =>
		Game.Random.Float( SpawnIntervalMinSeconds, SpawnIntervalMaxSeconds );

	public readonly struct BloomSeedEntry
	{
		public readonly int Id;
		public readonly GameObject Object;
		public readonly Vector3 WorldPosition;

		public BloomSeedEntry( int id, GameObject obj, Vector3 worldPosition )
		{
			Id = id;
			Object = obj;
			WorldPosition = worldPosition;
		}
	}
}

public sealed class ThornsBloomSeedMarker : Component
{
	[Property] public int SeedId { get; set; }
}
