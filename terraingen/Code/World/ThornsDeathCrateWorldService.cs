namespace Terraingen.World;

using Sandbox.Network;
using Terraingen;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Physics;
using Terraingen.Player;
using Terraingen.Rendering;
using Terraingen.TerrainGen;
using Terraingen.UI;

/// <summary>Host-spawned loot crates (player death, enemy drops) using world container UI.</summary>
[Title( "Thorns Death Crate World" )]
[Category( "Thorns/World" )]
public sealed class ThornsDeathCrateWorldService : Component
{
	public const float LootHoldSeconds = 1.1f;
	public const float InteractRange = 220f;
	public const int MaxActiveCrates = 48;
	public const float EnemyLootCrateLifetimeSeconds = 120f;
	public const float CrateVisualScale = 24f;

	const string CrateModelPath = ThornsPlaceableModels.Chest;
	const string FallbackBoxModelPath = "models/dev/box.vmdl";

	public static ThornsDeathCrateWorldService Instance { get; private set; }

	readonly Dictionary<int, DeathCrateEntry> _crates = new();
	readonly HashSet<int> _looted = new();
	readonly List<int> _trimScratch = new();

	GameObject _root;
	Terrain _terrain;
	Model _crateModel;
	Model _fallbackModel;
	bool _spawnAssetsLoaded;
	int _nextId = 1;

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

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		HostTickExpiringCrates();
	}

	void HostTickExpiringCrates()
	{
		foreach ( var (id, entry) in _crates )
		{
			if ( entry.LifetimeSeconds <= 0f || entry.Age < entry.LifetimeSeconds )
				continue;

			HostForceDespawn( id );
		}
	}

	public void OnWorldReady( Terrain terrain )
	{
		_terrain = terrain;
		try { EnsureSpawnAssets(); }
		catch ( Exception e ) { Log.Warning( $"[Thorns Death Crates] Asset load deferred: {e.Message}" ); }
	}

	public void Clear()
	{
		_crates.Clear();
		_looted.Clear();
		_nextId = 1;
		_spawnAssetsLoaded = false;

		if ( _root is not null && _root.IsValid() )
			_root.Destroy();

		_root = null;
	}

	public bool HasTargetInFront( GameObject playerRoot ) =>
		TryPickAlongRay( playerRoot, out _, out _ );

	public bool TryPickAlongRay( GameObject playerRoot, out int crateId, out DeathCrateEntry entry )
	{
		crateId = 0;
		entry = default;

		if ( !TryResolveAim( playerRoot, out var origin, out var forward ) )
			return false;

		if ( TryPickFromTrace( origin, forward, InteractRange, playerRoot, out crateId, out entry ) )
			return true;

		return TryPickFromRegistry( origin, forward, InteractRange, out crateId, out entry );
	}

	public bool TryGetCrateWorldPosition( int crateId, out Vector3 worldPos )
	{
		worldPos = default;
		if ( crateId <= 0 || !_crates.TryGetValue( crateId, out var entry ) )
			return false;

		worldPos = entry.WorldPosition;
		return true;
	}

	public void HostTrySpawnForPlayer( GameObject playerRoot, ThornsPlayerGameplay gameplay, Vector3 deathPosition )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !playerRoot.IsValid() || gameplay is null )
			return;

		var items = gameplay.HostExtractAllCarriedItems();
		if ( items.Count == 0 )
			return;

		EnsureSpawnAssets();
		TrimOldestIfNeeded();

		var worldPos = SnapCratePosition( deathPosition );
		var id = _nextId++;
		var obj = CreateCrateVisual( id, worldPos );
		if ( !obj.IsValid() )
		{
			foreach ( var stack in items )
				gameplay.HostTryGrantItemStack( stack );
			return;
		}

		_crates[id] = new DeathCrateEntry( id, obj, worldPos, items );
		ThornsWorldLootContainerService.Instance?.HostRegisterDeathCrate( id, items );
		if ( Networking.IsActive && Networking.IsHost )
			HostBroadcastSpawnVisual( id, worldPos );

		Log.Info( $"[Thorns Death Crates] Dropped crate #{id} with {items.Count} stack(s) at {worldPos:F0}." );
	}

	/// <summary>Enemy loot crate with rolled table loot; despawns after <see cref="EnemyLootCrateLifetimeSeconds"/>.</summary>
	public bool HostTrySpawnEnemyLootCrate( Vector3 worldPosition, string lootTable, int lootSeed, string title )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return false;

		var items = ThornsEnemyLootTables.RollStacks( lootTable, lootSeed );
		if ( items.Count == 0 )
			return false;

		return HostTrySpawnEnemyLootCrate( worldPosition, items, title );
	}

	/// <summary>Enemy loot crate with pre-rolled stacks; despawns after <see cref="EnemyLootCrateLifetimeSeconds"/>.</summary>
	public bool HostTrySpawnEnemyLootCrate( Vector3 worldPosition, IReadOnlyList<ThornsItemStack> items, string title )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || items is null or { Count: 0 } )
			return false;

		return HostTrySpawnLootCrate(
			worldPosition,
			items.ToList(),
			title,
			EnemyLootCrateLifetimeSeconds,
			enemyLootTint: true );
	}

	bool HostTrySpawnLootCrate(
		Vector3 worldPosition,
		List<ThornsItemStack> items,
		string title,
		float lifetimeSeconds,
		bool enemyLootTint )
	{
		if ( items is null or { Count: 0 } )
			return false;

		EnsureSpawnAssets();
		TrimOldestIfNeeded();

		var worldPos = SnapCratePosition( worldPosition );
		var id = _nextId++;
		var obj = CreateCrateVisual( id, worldPos, enemyLootTint );
		if ( !obj.IsValid() )
			return false;

		_crates[id] = new DeathCrateEntry( id, obj, worldPos, items, lifetimeSeconds );
		ThornsWorldLootContainerService.Instance?.HostRegisterDeathCrate( id, items, title );
		if ( Networking.IsActive && Networking.IsHost )
			HostBroadcastSpawnVisual( id, worldPos );

		return true;
	}

	public void HostForceDespawn( int crateId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_crates.ContainsKey( crateId ) )
			return;

		ThornsWorldLootContainerService.Instance?.HostUnregister( ThornsWorldLootContainerService.DeathCrateKey( crateId ) );

		var broadcastPos = Vector3.Zero;
		if ( _crates.TryGetValue( crateId, out var entry ) )
		{
			broadcastPos = entry.WorldPosition;
			if ( entry.Object.IsValid() )
				entry.Object.Destroy();
		}

		_crates.Remove( crateId );
		_looted.Add( crateId );

		if ( Networking.IsActive )
			HostBroadcastDespawn( crateId, broadcastPos );
	}

	/// <summary>Drop a single stack in front of the player (inventory discard).</summary>
	public bool HostTrySpawnPlayerDrop( GameObject playerRoot, ThornsItemStack stack )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !playerRoot.IsValid() || stack.IsEmpty )
			return false;

		EnsureSpawnAssets();
		TrimOldestIfNeeded();

		var worldPos = ComputePlayerDropPosition( playerRoot );
		var id = _nextId++;
		var obj = CreateCrateVisual( id, worldPos );
		if ( !obj.IsValid() )
			return false;

		var items = new List<ThornsItemStack> { stack };
		_crates[id] = new DeathCrateEntry( id, obj, worldPos, items );
		ThornsWorldLootContainerService.Instance?.HostRegisterDeathCrate( id, items, "Dropped Item" );
		if ( Networking.IsActive && Networking.IsHost )
			HostBroadcastSpawnVisual( id, worldPos );

		return true;
	}

	Vector3 ComputePlayerDropPosition( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return default;

		var forward = playerRoot.WorldRotation.Forward.WithZ( 0f );
		if ( forward.Length < 0.01f )
			forward = Vector3.Forward;

		return SnapCratePosition( playerRoot.WorldPosition + forward.Normal * 64f );
	}

	public void HostDespawnWhenEmpty( int crateId ) => HostForceDespawn( crateId );

	/// <summary>
	/// AUDIT NOTE: unused legacy bulk-loot API. Live loot uses container UI +
	/// <see cref="ThornsWorldLootContainerService"/>. Do not call — it does not unregister /
	/// stay in sync with container slots. Kept only so old call sites are obvious if searched.
	/// </summary>
	[Obsolete( "Use world container UI / ThornsWorldLootContainerService moves instead of bulk HostTryLoot." )]
	public bool HostTryLoot( GameObject playerRoot, int crateId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_crates.TryGetValue( crateId, out var entry ) )
			return false;

		if ( _looted.Contains( crateId ) || entry.Items.Count == 0 || !entry.Object.IsValid() )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			return false;

		if ( Vector3.DistanceBetween( playerRoot.WorldPosition, entry.WorldPosition ) > InteractRange + 40f )
			return false;

		var grantedAny = false;
		for ( var i = entry.Items.Count - 1; i >= 0; i-- )
		{
			if ( !gameplay.HostTryGrantItemStack( entry.Items[i] ) )
				continue;

			entry.Items.RemoveAt( i );
			grantedAny = true;
		}

		if ( !grantedAny )
		{
			var notify = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
			if ( notify.IsValid() && notify.IsLocalPlayer() )
				ThornsNotificationBus.Push( "Inventory full — make room to loot the death crate.", "warning" );
			return false;
		}

		ThornsMilestoneTracker.OnInventoryChanged( gameplay );
		gameplay.PushInventoryToOwner();

		if ( entry.Items.Count == 0 )
		{
			_looted.Add( crateId );
			if ( entry.Object.IsValid() )
				entry.Object.Destroy();

			_crates.Remove( crateId );
			if ( Networking.IsActive )
				HostBroadcastDespawn( crateId, entry.WorldPosition );
		}

		return true;
	}

	Vector3 SnapCratePosition( Vector3 deathPosition )
	{
		if ( _terrain.IsValid() && ThornsTerrainSurface.TrySnapToTerrain( _terrain, deathPosition, out var snapped ) )
			return snapped + Vector3.Up * 8f;

		return ThornsPlayerSpawnLocations.SnapToTerrain( Scene, deathPosition, heightOffset: 8f );
	}

	void EnsureSpawnAssets()
	{
		if ( _spawnAssetsLoaded )
			return;

		_crateModel = ThornsModelResourceLoad.LoadOrFallback( CrateModelPath, FallbackBoxModelPath );
		_fallbackModel = ThornsModelResourceLoad.LoadOrFallback( FallbackBoxModelPath );
		_spawnAssetsLoaded = true;
	}

	static bool ModelReady( Model model )
	{
		try { return model.IsValid && !model.IsError; }
		catch ( NullReferenceException ) { return false; }
	}

	Model ResolveCrateModel()
	{
		EnsureSpawnAssets();
		return ModelReady( _crateModel ) ? _crateModel : _fallbackModel;
	}

	void EnsureDropRoot()
	{
		if ( _root.IsValid() || Scene is null || !Scene.IsValid() )
			return;

		_root = Scene.CreateObject( true );
		_root.Name = "Thorns Death Crates";
		if ( GameObject.IsValid() )
			_root.Parent = GameObject;
	}

	GameObject CreateCrateVisual( int id, Vector3 worldPos, bool enemyLootTint = false )
	{
		if ( Scene is null || !Scene.IsValid() )
			return null;

		EnsureDropRoot();
		if ( !_root.IsValid() )
			return null;

		var model = ResolveCrateModel();
		var obj = Scene.CreateObject( true );
		if ( !obj.IsValid() )
			return null;

		obj.Name = $"Death Crate {id}";
		obj.Parent = _root;
		obj.WorldPosition = worldPos;
		obj.WorldRotation = Rotation.FromYaw( new Random( id ).NextSingle() * 360f );
		obj.LocalScale = ModelReady( _crateModel )
			? Vector3.One * CrateVisualScale
			: new Vector3( 1.2f, 1.2f, 1.0f ) * CrateVisualScale;

		var renderer = obj.Components.Create<ModelRenderer>();
		if ( renderer is null )
		{
			obj.Destroy();
			return null;
		}

		renderer.Model = model;
		renderer.Tint = enemyLootTint
			? new Color( 0.58f, 0.48f, 0.32f )
			: new Color( 0.72f, 0.22f, 0.18f );
		ThornsWorldShadowUtil.EnableWorldShadows( renderer );

		TerraingenAnchoredPhysics.EnsureSolidTags( obj );
		TryAddTag( obj, "death_crate" );

		var bounds = ModelReady( model ) ? model.Bounds : new BBox( Vector3.One * -25f, Vector3.One * 25f );
		var collider = obj.Components.Create<BoxCollider>();
		collider.Center = bounds.Center;
		collider.Scale = bounds.Size;
		collider.Static = true;

		var marker = obj.Components.Create<ThornsLootableDeathCrate>();
		marker.DeathCrateId = id;
		return obj;
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
		// AUDIT FIX (2026-07): Cap trim used to Destroy() the visual and drop `_crates` WITHOUT
		// calling HostForceDespawn / HostUnregister. Items already extracted from players were then
		// permanently lost (and death: container records could orphan). Airdrops already called
		// HostDespawnExpired — match that pattern here.
		// Revert: restore the inline Destroy/_crates.Remove/_looted.Add block if HostForceDespawn
		// ever double-despawns (it should be idempotent via !_crates.ContainsKey).
		_trimScratch.Clear();
		foreach ( var id in _crates.Keys )
		{
			if ( !_looted.Contains( id ) )
				_trimScratch.Add( id );
		}

		_trimScratch.Sort();
		while ( _trimScratch.Count >= MaxActiveCrates )
		{
			var removeId = _trimScratch[0];
			_trimScratch.RemoveAt( 0 );
			Log.Warning( $"[Thorns Death Crates] Cap trim despawning crate #{removeId} (MaxActiveCrates={MaxActiveCrates}). Loot unregistered — not returned to players." );
			HostForceDespawn( removeId );
		}
	}

	void HostBroadcastSpawnVisual( int crateId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcSpawnVisual( crateId, worldPosition ) );

	void HostBroadcastDespawn( int crateId, Vector3 worldPosition )
		=> ThornsNetInterest.HostBroadcastNear( worldPosition, () => RpcDespawnCrate( crateId ) );

	bool TryPickFromTrace( Vector3 origin, Vector3 forward, float maxRange, GameObject ignoreRoot, out int crateId, out DeathCrateEntry entry )
	{
		crateId = 0;
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
		       && TryAcceptCrateHit( trace.GameObject, out crateId, out entry );
	}

	bool TryAcceptCrateHit( GameObject hit, out int crateId, out DeathCrateEntry entry )
	{
		crateId = 0;
		entry = default;

		var marker = hit.Components.Get<ThornsLootableDeathCrate>( FindMode.EverythingInSelfAndParent );
		if ( !marker.IsValid() || marker.DeathCrateId <= 0 || _looted.Contains( marker.DeathCrateId ) )
			return false;

		if ( !_crates.TryGetValue( marker.DeathCrateId, out entry ) || !entry.Object.IsValid() )
			return false;

		crateId = marker.DeathCrateId;
		return true;
	}

	bool TryPickFromRegistry( Vector3 origin, Vector3 forward, float maxRange, out int crateId, out DeathCrateEntry entry )
	{
		crateId = 0;
		entry = default;
		var dir = forward.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var best = float.MaxValue;
		foreach ( var (id, crate) in _crates )
		{
			if ( _looted.Contains( id ) || !crate.Object.IsValid() )
				continue;

			var center = crate.WorldPosition;
			var pickRadius = 52f;
			var collider = crate.Object.Components.Get<BoxCollider>();
			if ( collider.IsValid() )
			{
				center = crate.Object.WorldPosition + collider.Center * crate.Object.WorldScale;
				pickRadius = MathF.Max( pickRadius, collider.Scale.Length * 0.55f );
			}

			if ( !Terraingen.Combat.ThornsInteractAimPick.TryRaySphere( origin, dir, center, pickRadius, out var along )
			     || along < 8f
			     || along > maxRange
			     || along >= best )
				continue;

			best = along;
			crateId = id;
			entry = crate;
		}

		return crateId > 0;
	}

	static bool TryResolveAim( GameObject playerRoot, out Vector3 origin, out Vector3 forward )
		=> Terraingen.Combat.ThornsInteractAimPick.TryResolveCrosshairAimRay( playerRoot, out origin, out forward );

	[Rpc.Broadcast]
	void RpcSpawnVisual( int crateId, Vector3 worldPosition )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() || ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( _crates.ContainsKey( crateId ) )
			return;

		var obj = CreateCrateVisual( crateId, worldPosition );
		if ( !obj.IsValid() )
			return;

		_crates[crateId] = new DeathCrateEntry( crateId, obj, worldPosition, new List<ThornsItemStack>() );
	}

	[Rpc.Broadcast]
	void RpcDespawnCrate( int crateId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		_looted.Add( crateId );
		if ( _crates.TryGetValue( crateId, out var entry ) && entry.Object.IsValid() )
			entry.Object.Destroy();
		_crates.Remove( crateId );
	}

	public sealed class DeathCrateEntry
	{
		public int Id { get; }
		public GameObject Object { get; }
		public Vector3 WorldPosition { get; }
		public List<ThornsItemStack> Items { get; }
		public float LifetimeSeconds { get; }
		public TimeSince Age { get; }

		public DeathCrateEntry(
			int id,
			GameObject obj,
			Vector3 worldPosition,
			List<ThornsItemStack> items,
			float lifetimeSeconds = 0f )
		{
			Id = id;
			Object = obj;
			WorldPosition = worldPosition;
			Items = items ?? new List<ThornsItemStack>();
			LifetimeSeconds = Math.Max( 0f, lifetimeSeconds );
			Age = 0f;
		}
	}
}

public sealed class ThornsLootableDeathCrate : Component
{
	[Property] public int DeathCrateId { get; set; }
}
