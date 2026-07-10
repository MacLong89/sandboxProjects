namespace Terraingen.Foliage;

using Sandbox.Network;
using Terraingen;
using Terraingen.Combat;
using Terraingen.Physics;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Harvestable tree registry, trunk collision proxies, and respawn timing.</summary>
[Title( "Thorns Tree World" )]
[Category( "Terrain" )]
public sealed class ThornsTreeWorldService : Component
{
	public static ThornsTreeWorldService Instance { get; private set; }

	/// <summary>
	/// Aim-window multiplier for chopping/punching trees to GATHER only (also drives the
	/// gather prompt, since it shares this pick). 2 = trees are twice as easy to aim at.
	/// This widens a spherecast around the crosshair; it does NOT resize the trunk collider,
	/// so it never affects walk collision or enemy/animal melee reach.
	/// </summary>
	[ConVar( "tree_gather_hitbox_scale" )]
	public static float GatherHitboxScale { get; set; } = 2f;

	public static ThornsTreeWorldService ResolveInstance()
	{
		if ( FoliagePlacerContext.ActiveTreeService is not null && FoliagePlacerContext.ActiveTreeService.IsValid() )
			return FoliagePlacerContext.ActiveTreeService;

		if ( Instance is not null && Instance.IsValid() )
			return Instance;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return null;

		return scene.GetAllComponents<ThornsTreeWorldService>().FirstOrDefault();
	}

	/// <summary>Claim the static instance before populate (OnStart may run later).</summary>
	public void EnsureActiveInstance()
	{
		if ( Instance is null || !Instance.IsValid() )
			Instance = this;
	}

	readonly Dictionary<int, ThornsTreeRuntime> _trees = new();
	readonly Dictionary<Vector2Int, GameObject> _collisionRoots = new();
	readonly Dictionary<Vector2Int, ThornsFoliageChunkInstances> _chunkInstances = new();
	readonly Dictionary<(Vector2Int Cell, FoliageSpecies Species, int Index), int> _treeByInstanceKey = new();
	readonly HashSet<int> _depleted = new();

	ThornsFoliagePlacer.FoliageModelSet _models;
	ThornsFoliageConfig _config;
	Terrain _terrain;
	Model _trunkCollisionProfile;
	int _nextTreeId = 1;

	protected override void OnStart()
	{
		if ( Instance is null || !Instance.IsValid() )
			Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( ThornsMultiplayer.IsHostOrOffline )
			TickRespawns();
	}

	public void Begin( ThornsFoliagePlacer.FoliageModelSet models, ThornsFoliageConfig config, Terrain terrain = null )
	{
		_models = models;
		_config = config;
		_terrain = terrain;
		_trunkCollisionProfile = models.IsValid ? models.Get( FoliageSpecies.Aspen ) : default;
	}

	public void Clear()
	{
		foreach ( var root in _collisionRoots.Values )
		{
			if ( root.IsValid() )
				root.Destroy();
		}

		_collisionRoots.Clear();
		_chunkInstances.Clear();
		_treeByInstanceKey.Clear();
		_trees.Clear();
		_depleted.Clear();
		_nextTreeId = 1;
	}

	public void RegisterChunk( ThornsFoliageChunkData chunk, GameObject foliageRoot, Terrain terrain )
	{
		if ( chunk is null || chunk.InstanceCount == 0 || foliageRoot is null || !foliageRoot.IsValid() )
			return;

		if ( !terrain.IsValid() || !_models.IsValid || _config is null )
			return;

		_terrain = terrain;

		if ( chunk.Instances is null )
		{
			ReconcileChunkSceneTrees( chunk );
			return;
		}

		if ( _collisionRoots.ContainsKey( chunk.Cell ) )
			return;

		_chunkInstances[chunk.Cell] = chunk.Instances;

		var root = Scene.CreateObject( true );
		root.Name = $"TreeCollisions {chunk.Cell.x}_{chunk.Cell.y}";
		root.Parent = foliageRoot;

		_collisionRoots[chunk.Cell] = root;

		RegisterList( chunk.Cell, chunk.Instances.Pine, FoliageSpecies.Pine, _models.Get( FoliageSpecies.Pine ), root );
		RegisterList( chunk.Cell, chunk.Instances.Aspen, FoliageSpecies.Aspen, _models.Get( FoliageSpecies.Aspen ), root );
		RegisterList( chunk.Cell, chunk.Instances.Oak, FoliageSpecies.Oak, _models.Get( FoliageSpecies.Oak ), root );
		root.Enabled = true;
	}

	public void RegisterSceneTree(
		GameObject instance,
		Vector3 worldPosition,
		Rotation worldRotation,
		Vector3 worldScale,
		FoliageSpecies species,
		Model model )
	{
		if ( !instance.IsValid() || !model.IsValid() )
			return;

		var existing = instance.Components.Get<ThornsTreeInstance>();
		if ( existing is not null && existing.IsValid() && existing.TreeId > 0 && _trees.ContainsKey( existing.TreeId ) )
		{
			if ( _trees.TryGetValue( existing.TreeId, out var registered ) )
				registered.SceneInstance = instance;

			return;
		}

		ThornsTreeTrunkCollision.Apply( instance, model, worldScale.x );

		var cell = _terrain.IsValid() && _config is not null
			? WorldToCell( worldPosition )
			: Vector2Int.Zero;

		var id = RegisterTree( cell, species, -1, worldPosition, worldRotation, worldScale, instance );
		if ( _trees.TryGetValue( id, out var runtime ) )
			runtime.SceneInstance = instance;

		AddTreeTag( instance, id );
	}

	/// <summary>Register any scene-tree GameObjects that missed populate-time <see cref="RegisterSceneTree"/>.</summary>
	public void ReconcileSceneTreeRegistry( IReadOnlyList<ThornsFoliageChunkData> chunks )
	{
		if ( chunks is null )
			return;

		foreach ( var chunk in chunks )
			ReconcileChunkSceneTrees( chunk );
	}

	void ReconcileChunkSceneTrees( ThornsFoliageChunkData chunk )
	{
		if ( chunk?.Root is null || !chunk.Root.IsValid() )
			return;

		foreach ( var child in chunk.Root.Children )
		{
			if ( !child.IsValid() )
				continue;

			if ( !TryParseSpeciesFromName( child.Name, out var species ) )
				continue;

			var model = ResolveSpeciesModel( species );
			if ( !model.IsValid() )
				continue;

			RegisterSceneTree(
				child,
				child.WorldPosition,
				child.WorldRotation,
				child.WorldScale,
				species,
				model );
		}
	}

	static bool TryParseSpeciesFromName( string name, out FoliageSpecies species )
	{
		species = default;
		if ( string.IsNullOrWhiteSpace( name ) )
			return false;

		if ( name.Contains( "Pine", StringComparison.OrdinalIgnoreCase ) )
		{
			species = FoliageSpecies.Pine;
			return true;
		}

		if ( name.Contains( "Aspen", StringComparison.OrdinalIgnoreCase ) )
		{
			species = FoliageSpecies.Aspen;
			return true;
		}

		if ( name.Contains( "Oak", StringComparison.OrdinalIgnoreCase ) )
		{
			species = FoliageSpecies.Oak;
			return true;
		}

		return false;
	}

	Vector2Int WorldToCell( Vector3 worldPosition )
	{
		if ( !_terrain.IsValid() || _config is null )
			return Vector2Int.Zero;

		var origin = _terrain.GameObject.WorldPosition;
		var x = (int)MathF.Floor( (worldPosition.x - origin.x) / _config.ChunkSizeInches );
		var y = (int)MathF.Floor( (worldPosition.y - origin.y) / _config.ChunkSizeInches );
		return new Vector2Int( x, y );
	}

	public IReadOnlyList<int> HostExportDepletedIds()
	{
		var list = new List<int>( _depleted.Count );
		foreach ( var id in _depleted )
		{
			if ( _trees.ContainsKey( id ) )
				list.Add( id );
		}

		return list;
	}

	public void HostApplyDepletedIds( IEnumerable<int> ids )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || ids is null )
			return;

		foreach ( var id in ids.ToArray() )
		{
			if ( !_trees.TryGetValue( id, out var tree ) || tree.Depleted )
				continue;

			DepleteTree( tree );
		}
	}

	public bool IsDepleted( Vector2Int cell, FoliageSpecies species, int listIndex )
	{
		return _treeByInstanceKey.TryGetValue( (cell, species, listIndex), out var treeId )
		       && _depleted.Contains( treeId );
	}

	public void SyncChunkCollisionVisibility( Vector2Int cell, bool enabled )
	{
		if ( !_collisionRoots.TryGetValue( cell, out var root ) || !root.IsValid() )
			return;

		// Roots stay enabled; per-tree proxies are toggled in SyncHarvestCollisionProximity.
		root.Enabled = true;
	}

	/// <summary>Enable instanced trunk proxies individually near the observer (not whole-chunk toggles).</summary>
	public void SyncHarvestCollisionProximity( Vector3 observer, float rangeInches )
	{
		if ( rangeInches <= 0f || observer.LengthSquared < 1f )
			return;

		var rangeSq = rangeInches * rangeInches;
		var observerPlanar = observer.WithZ( 0f );

		foreach ( var (_, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( tree.Id ) )
				continue;

			if ( !TryGetTrunkWorldPosition( tree, out var trunk ) )
				continue;

			var enable = (trunk.WithZ( 0f ) - observerPlanar).LengthSquared <= rangeSq;

			if ( tree.CollisionProxy.IsValid() )
			{
				var proxy = tree.CollisionProxy;
				var collider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( proxy );
				if ( collider is { IsValid: true } )
				{
					if ( collider.Enabled != enable )
						collider.Enabled = enable;
				}

				if ( proxy.Enabled != enable )
					proxy.Enabled = enable;
			}

			if ( tree.SceneInstance.IsValid() )
			{
				var collider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( tree.SceneInstance );
				if ( collider is { IsValid: true } && collider.Enabled != enable )
					collider.Enabled = enable;
			}
		}
	}

	public int DebugTreeCount => _trees.Count;

	public float DebugNearestTrunkDistance( Vector3 worldPosition ) =>
		DebugNearestTrunkDistance( worldPosition, out _ );

	public float DebugNearestTrunkDistance( Vector3 worldPosition, out int treeId )
	{
		treeId = 0;
		var best = float.MaxValue;
		foreach ( var (id, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !TryGetTrunkWorldPosition( tree, out var trunk ) )
				continue;

			var dist = worldPosition.WithZ( 0f ).Distance( trunk.WithZ( 0f ) );
			if ( dist >= best )
				continue;

			best = dist;
			treeId = id;
		}

		return best == float.MaxValue ? -1f : best;
	}

	/// <summary>Rich chop failure log — enable with <c>tree_chop_debug 1</c>.</summary>
	public void DebugLogChopFailure(
		Vector3 playerPos,
		Vector3 aimOrigin,
		Vector3 aimDir,
		float maxRange,
		GameObject ignoreRoot )
	{
		var dir = aimDir.Normal;
		var end = aimOrigin + dir * maxRange;
		var scene = Game.ActiveScene;

		var taggedHit = false;
		var solidHit = false;
		string taggedName = "";
		string solidName = "";
		int taggedTreeId = 0;

		if ( scene is not null && scene.IsValid )
		{
			var tagged = scene.Trace
				.Sphere( 48f, aimOrigin, end )
				.WithTag( "tree" )
				.IgnoreGameObjectHierarchy( ignoreRoot )
				.Run();
			taggedHit = tagged.Hit;
			if ( tagged.GameObject.IsValid() )
			{
				taggedName = tagged.GameObject.Name;
				var tag = tagged.GameObject.Components.Get<ThornsTreeInstance>( FindMode.EverythingInSelfAndParent );
				if ( tag.IsValid() )
					taggedTreeId = tag.TreeId;
			}

			var solid = scene.Trace
				.Sphere( 48f, aimOrigin, end )
				.IgnoreGameObjectHierarchy( ignoreRoot )
				.Run();
			solidHit = solid.Hit;
			if ( solid.GameObject.IsValid() )
				solidName = solid.GameObject.Name;
		}

		var nearestDist = DebugNearestTrunkDistance( playerPos, out var nearestId );
		_trees.TryGetValue( nearestId, out var nearestTree );

		var meleeReach = ThornsGatheringRange.MeleeForgivenessInches( maxRange );
		var withinRange = 0;
		var withinRangeList = 0;
		foreach ( var (_, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( tree.Id ) )
				continue;

			if ( TryGetTrunkWorldPosition( tree, out var trunk ) )
			{
				if ( (trunk - playerPos).WithZ( 0f ).Length <= meleeReach )
					withinRange++;
			}
		}

		foreach ( var (cell, chunk) in _chunkInstances )
		{
			withinRangeList += CountListWithin( chunk.Pine, cell, FoliageSpecies.Pine, playerPos, meleeReach );
			withinRangeList += CountListWithin( chunk.Aspen, cell, FoliageSpecies.Aspen, playerPos, meleeReach );
			withinRangeList += CountListWithin( chunk.Oak, cell, FoliageSpecies.Oak, playerPos, meleeReach );
		}

		int CountListWithin( List<Transform> list, Vector2Int cell, FoliageSpecies species, Vector3 pos, float reach )
		{
			if ( list is null )
				return 0;

			var count = 0;
			for ( var i = 0; i < list.Count; i++ )
			{
				if ( IsDepleted( cell, species, i ) )
					continue;

				if ( (list[i].Position - pos).WithZ( 0f ).Length <= reach )
					count++;
			}

			return count;
		}

		Log.Warning(
			$"[Thorns Chop] DEBUG fail player={playerPos:F0} aim={aimOrigin:F0}->{end:F0} trace={maxRange:F0} forgive={meleeReach:F0} " +
			$"trace(tag={taggedHit} id={taggedTreeId} go='{taggedName}', solid={solidHit} go='{solidName}') " +
			$"registered={_trees.Count} withinRange(registry={withinRange} instanced={withinRangeList}) " +
			$"nearest=#{nearestId} dist={nearestDist:F0}in" );

		if ( nearestTree is not null )
			LogNearestTreePositionSources( nearestId, nearestTree, playerPos );

		LogTopNearestTrees( playerPos, 3 );
	}

	void LogNearestTreePositionSources( int id, ThornsTreeRuntime tree, Vector3 playerPos )
	{
		var proxy = tree.CollisionProxy.IsValid() ? tree.CollisionProxy.WorldPosition : default;
		var list = default( Vector3 );
		var hasList = false;
		if ( tree.ListIndex >= 0 && _chunkInstances.TryGetValue( tree.Cell, out var chunk ) )
		{
			var speciesList = chunk.GetList( tree.Species );
			if ( tree.ListIndex < speciesList.Count )
			{
				list = speciesList[tree.ListIndex].Position;
				hasList = true;
			}
		}

		var listLabel = hasList ? list.ToString() : "n/a";
		Log.Warning(
			$"[Thorns Chop] DEBUG tree #{id} cell={tree.Cell} {tree.Species}[{tree.ListIndex}] " +
			$"spawn={tree.SpawnPosition:F0} proxy={proxy:F0} list={listLabel} " +
			$"proxyEnabled={( tree.CollisionProxy.IsValid() && tree.CollisionProxy.Enabled )} " +
			$"dist(spawn)={( tree.SpawnPosition - playerPos ).WithZ( 0f ).Length:F0} " +
			$"dist(proxy)={( proxy - playerPos ).WithZ( 0f ).Length:F0}" +
			( hasList ? $" dist(list)={( list - playerPos ).WithZ( 0f ).Length:F0}" : "" ) );
	}

	void LogTopNearestTrees( Vector3 playerPos, int count )
	{
		var ranked = new List<(int id, float dist, Vector3 trunk)>();
		foreach ( var (id, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !TryGetTrunkWorldPosition( tree, out var trunk ) )
				continue;

			ranked.Add( (id, (trunk - playerPos).WithZ( 0f ).Length, trunk) );
		}

		foreach ( var entry in ranked.OrderBy( t => t.dist ).Take( count ) )
		{
			Log.Warning( $"[Thorns Chop] DEBUG near #{entry.id} dist={entry.dist:F0}in trunk={entry.trunk:F0}" );
		}
	}

	public bool HostTryChop(
		GameObject playerRoot,
		int treeId,
		Vector3 aimOrigin,
		Vector3 aimDirection )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_trees.TryGetValue( treeId, out var tree ) )
			return false;

		if ( tree.Depleted || _depleted.Contains( treeId ) )
			return false;

		if ( !TryPickTreeAlongRay(
			     aimOrigin,
			     aimDirection,
			     ThornsGatheringRange.Inches,
			     playerRoot,
			     out var authoritative )
		     || authoritative != treeId )
			return false;

		if ( !ThornsAxeTools.PlayerHasAxeEquipped( playerRoot ) )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );

		if ( !gameplay.IsValid() )
			return false;

		if ( !HostTryApplyTreeHit( tree, treeId, out var wood, out var justFelled ) )
			return false;

		if ( justFelled && Networking.IsActive )
		{
			TryGetTrunkWorldPosition( tree, out var trunkPos );
			ThornsNetInterest.HostBroadcastNear( trunkPos, () => RpcSetTreeDepleted( treeId ) );
		}

		gameplay.HostGrantHarvestItem( "wood", wood );
		return true;
	}

	public bool HostTrySalvageWood(
		GameObject playerRoot,
		int treeId,
		Vector3 aimOrigin,
		Vector3 aimDirection )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_trees.TryGetValue( treeId, out var tree ) )
			return false;

		if ( tree.Depleted || _depleted.Contains( treeId ) )
			return false;

		if ( !TryPickTreeAlongRay(
			     aimOrigin,
			     aimDirection,
			     ThornsGatheringRange.Inches,
			     playerRoot,
			     out var authoritative )
		     || authoritative != treeId )
			return false;

		if ( !ThornsGatherSalvage.PlayerHasEmptyHands( playerRoot ) )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );

		if ( !gameplay.IsValid() )
			return false;

		if ( !HostTryApplyTreeHit( tree, treeId, out var wood, out var justFelled, ThornsTreeHarvestRules.WoodPerSalvage, includeFellBonus: false ) )
			return false;

		if ( justFelled && Networking.IsActive )
		{
			TryGetTrunkWorldPosition( tree, out var trunkPos );
			ThornsNetInterest.HostBroadcastNear( trunkPos, () => RpcSetTreeDepleted( treeId ) );
		}

		gameplay.HostGrantHarvestItem( "wood", wood );
		return true;
	}

	/// <summary>Same flow as <see cref="ThornsMineralWorldService.TryPickNodeAlongRay"/>.</summary>
	public bool TryPickTreeAlongRay(
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int treeId,
		out Vector3 hitPosition )
	{
		treeId = 0;
		hitPosition = default;
		if ( maxRange <= 0f )
			return false;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var playerPos = ignoreRoot.IsValid() ? ignoreRoot.WorldPosition : origin;
		var gatherRange = maxRange > 0f ? maxRange : ThornsGatheringRange.Inches;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return false;

		// Widen the aim window for tree gathering only. A trunk's lateral half-extent is at
		// most LiveMaxRadiusInches, so sweeping a sphere of (scale-1)×that radius makes the
		// effective hittable cross-section up to `scale`× the trunk without resizing the
		// collider. Passed only from this tree path, so minerals/combat stay thin-ray.
		var hitboxScale = Math.Clamp( GatherHitboxScale, 1f, 6f );
		var sweepRadius = (hitboxScale - 1f) * ThornsTreeTrunkCollision.LiveMaxRadiusInches;

		var trace = ThornsGatherTargeting.TraceGatherTarget( scene, origin, direction, gatherRange, "tree", ignoreRoot, sweepRadius );
		if ( !trace.Hit || !TryResolveTreeFromTrace( trace, out treeId ) )
			return false;

		hitPosition = trace.HitPosition;
		return ThornsGatherTargeting.IsWithinGatherReach( playerPos, hitPosition, gatherRange );
	}

	public bool TryPickTreeAlongRay(
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int treeId )
		=> TryPickTreeAlongRay( origin, direction, maxRange, ignoreRoot, out treeId, out _ );

	/// <summary>Debug volumes when <c>gather_target_debug 1</c>.</summary>
	public void DrawGatherDebug(
		Vector3 playerPos,
		Vector3 aimOrigin,
		Vector3 aimDir,
		float traceInches,
		float forgivenessInches,
		int pickedTreeId )
	{
		foreach ( var (id, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !tree.CollisionProxy.IsValid() || !tree.CollisionProxy.Enabled )
				continue;

			var anchor = tree.CollisionProxy.WorldPosition;
			if ( (anchor - playerPos).Length > forgivenessInches * 1.5f )
				continue;

			var scale = MathF.Max( tree.SpawnScale.x, 1f );
			var radius = MathF.Min( MathF.Max( 32f, 10f * scale ), 120f );
			DebugOverlay.Sphere( new Sphere( anchor, radius ), duration: 0.12f );

			if ( id == pickedTreeId )
				DebugOverlay.Text( anchor + Vector3.Up * 48f, $"tree #{id}", duration: 0.12f );
		}

		if ( pickedTreeId > 0 )
			DebugOverlay.Text( aimOrigin + aimDir.Normal * traceInches * 0.5f, $"pick tree #{pickedTreeId}", duration: 0.12f );
	}

	/// <summary>Trunk collision proxies when <see cref="ThornsCollisionDebug.OverlayEnabled"/>.</summary>
	public void DrawCollisionDebugOverlay( Vector3 observer, float maxDistance, float duration )
	{
		var maxDistSq = maxDistance * maxDistance;
		var activeColor = ThornsCollisionDebugDraw.ColorFor( ThornsCollisionDebugDraw.Category.Tree );

		foreach ( var (id, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !tree.CollisionProxy.IsValid() || !tree.CollisionProxy.Enabled )
				continue;

			var planarDistSq = (tree.CollisionProxy.WorldPosition.WithZ( 0f ) - observer.WithZ( 0f )).LengthSquared;
			if ( planarDistSq > maxDistSq )
				continue;

			var trunkCollider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( tree.CollisionProxy );
			if ( trunkCollider is { IsValid: true } )
			{
				if ( trunkCollider.Enabled )
					ThornsCollisionDebugDraw.DrawCollider( DebugOverlay, trunkCollider, duration, activeColor );
				else
					ThornsCollisionDebugDraw.DrawColliderDisabled( DebugOverlay, trunkCollider, duration, activeColor );
				continue;
			}

			ThornsCollisionDebugDraw.DrawCollidersOnHierarchy(
				DebugOverlay,
				tree.CollisionProxy,
				duration,
				ThornsCollisionDebugDraw.Category.Tree );
		}
	}

	bool TryResolveTreeFromTrace( SceneTraceResult trace, out int treeId )
	{
		treeId = 0;
		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return false;

		var tag = trace.GameObject.Components.Get<ThornsTreeInstance>( FindMode.EverythingInSelfAndParent );
		if ( tag.IsValid() && tag.TreeId > 0 && _trees.ContainsKey( tag.TreeId ) )
		{
			treeId = tag.TreeId;
			return true;
		}

		return TryResolveTreeFromHitObject( trace.GameObject, out treeId );
	}

	bool TryResolveTreeFromHitObject( GameObject hit, out int treeId )
	{
		treeId = 0;
		if ( !hit.IsValid() )
			return false;

		foreach ( var (id, tree) in _trees )
		{
			if ( tree.Depleted || _depleted.Contains( id ) )
				continue;

			if ( tree.SceneInstance.IsValid() && tree.SceneInstance == hit )
			{
				treeId = id;
				return true;
			}

			if ( tree.CollisionProxy.IsValid() && tree.CollisionProxy == hit )
			{
				treeId = id;
				return true;
			}
		}

		return false;
	}

	/// <summary>World trunk base — collision proxy first (matches visible trunk + blocking collider).</summary>
	bool TryGetTrunkWorldPosition( ThornsTreeRuntime tree, out Vector3 trunkPos )
	{
		trunkPos = default;

		if ( tree.ListIndex >= 0
		     && _chunkInstances.TryGetValue( tree.Cell, out var chunk ) )
		{
			var list = chunk.GetList( tree.Species );
			if ( tree.ListIndex < list.Count )
			{
				trunkPos = list[tree.ListIndex].Position;
				return true;
			}
		}

		if ( tree.SpawnPosition != default )
		{
			trunkPos = tree.SpawnPosition;
			return true;
		}

		if ( tree.CollisionProxy.IsValid() )
		{
			trunkPos = tree.CollisionProxy.WorldPosition;
			return true;
		}

		return false;
	}

	bool TryGetTrunkBasePosition( ThornsTreeRuntime tree, out Vector3 trunkPos ) =>
		TryGetTrunkWorldPosition( tree, out trunkPos );

	Model ResolveSpeciesModel( FoliageSpecies species )
	{
		if ( !_models.IsValid )
			return default;

		return _models.Get( species );
	}

	Model ResolveTrunkCollisionProfile()
	{
		if ( _trunkCollisionProfile.IsValid() )
			return _trunkCollisionProfile;

		return _models.IsValid ? _models.Get( FoliageSpecies.Aspen ) : default;
	}

	[Rpc.Broadcast]
	void RpcSetTreeDepleted( int treeId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !_trees.TryGetValue( treeId, out var tree ) || tree.Depleted )
			return;

		DepleteTree( tree );
	}

	[Rpc.Broadcast]
	void RpcSetTreeRespawned( int treeId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !_trees.TryGetValue( treeId, out var tree ) )
			return;

		RespawnTree( tree );
		_depleted.Remove( treeId );
	}

	void RegisterList(
		Vector2Int cell,
		List<Transform> list,
		FoliageSpecies species,
		Model model,
		GameObject parent )
	{
		if ( !model.IsValid() )
			return;

		for ( var i = 0; i < list.Count; i++ )
		{
			var t = list[i];
			var proxy = Scene.CreateObject( true );
			proxy.Name = $"Tree_{species}_{i}";
			proxy.Parent = parent;
			proxy.WorldPosition = t.Position;
			proxy.WorldRotation = t.Rotation;
			proxy.WorldScale = t.Scale;
			proxy.Enabled = true;

			ThornsTreeTrunkCollision.Apply( proxy, model, t.Scale.x );

			var id = RegisterTree( cell, species, i, proxy.WorldPosition, proxy.WorldRotation, proxy.WorldScale, proxy );
			AddTreeTag( proxy, id );
		}
	}

	int RegisterTree(
		Vector2Int cell,
		FoliageSpecies species,
		int listIndex,
		Vector3 worldPosition,
		Rotation worldRotation,
		Vector3 worldScale,
		GameObject collisionProxy )
	{
		var id = _nextTreeId++;
		_trees[id] = new ThornsTreeRuntime
		{
			Id = id,
			Cell = cell,
			Species = species,
			ListIndex = listIndex,
			SpawnPosition = worldPosition,
			SpawnRotation = worldRotation,
			SpawnScale = worldScale,
			CollisionProxy = collisionProxy
		};

		if ( listIndex >= 0 )
			_treeByInstanceKey[(cell, species, listIndex)] = id;

		return id;
	}

	static void AddTreeTag( GameObject go, int treeId )
	{
		if ( !go.Tags.Contains( "tree" ) )
			go.Tags.Add( "tree" );

		var tag = go.Components.Get<ThornsTreeInstance>() ?? go.Components.Create<ThornsTreeInstance>();
		tag.TreeId = treeId;
	}

	/// <summary>Host-only chop increment with depletion claim to prevent double-fell under concurrent harvest.</summary>
	bool HostTryApplyTreeHit(
		ThornsTreeRuntime tree,
		int treeId,
		out int wood,
		out bool justFelled,
		int woodPerHit = -1,
		bool includeFellBonus = true )
	{
		wood = 0;
		justFelled = false;

		if ( tree is null || tree.Depleted || _depleted.Contains( treeId ) )
			return false;

		if ( tree.ChopHits >= ThornsTreeHarvestRules.HitsToFell )
			return false;

		tree.ChopHits++;
		wood = woodPerHit >= 0 ? woodPerHit : ThornsTreeHarvestRules.WoodPerHit;

		if ( tree.ChopHits < ThornsTreeHarvestRules.HitsToFell )
			return true;

		if ( !_depleted.Add( treeId ) )
		{
			tree.ChopHits--;
			return false;
		}

		if ( includeFellBonus )
			wood += ThornsTreeHarvestRules.WoodBonusOnFell;

		justFelled = true;
		DepleteTree( tree );
		return true;
	}

	void DepleteTree( ThornsTreeRuntime tree )
	{
		tree.Depleted = true;
		_depleted.Add( tree.Id );

		if ( tree.CollisionProxy.IsValid() )
			tree.CollisionProxy.Enabled = false;

		if ( tree.SceneInstance.IsValid() )
			tree.SceneInstance.Enabled = false;

		tree.RespawnAt = Time.Now + ThornsTreeHarvestRules.RespawnSeconds;
		ThornsWorldPersistence.RequestSave();
	}

	void TickRespawns()
	{
		foreach ( var id in _depleted.ToArray() )
		{
			if ( !_trees.TryGetValue( id, out var tree ) )
				continue;

			if ( Time.Now < tree.RespawnAt )
				continue;

			RespawnTree( tree );
			_depleted.Remove( id );

			if ( Networking.IsActive )
			{
				TryGetTrunkWorldPosition( tree, out var trunkPos );
				ThornsNetInterest.HostBroadcastNear( trunkPos, () => RpcSetTreeRespawned( id ) );
			}

			ThornsWorldPersistence.RequestSave();
		}
	}

	void RespawnTree( ThornsTreeRuntime tree )
	{
		tree.Depleted = false;
		tree.ChopHits = 0;
		tree.RespawnAt = 0;

		if ( tree.CollisionProxy.IsValid() )
		{
			tree.CollisionProxy.Enabled = true;
			tree.CollisionProxy.WorldPosition = tree.SpawnPosition;
			tree.CollisionProxy.WorldRotation = tree.SpawnRotation;
			tree.CollisionProxy.WorldScale = tree.SpawnScale;
		}

		if ( tree.SceneInstance.IsValid() )
		{
			tree.SceneInstance.Enabled = true;

			var collider = TerraingenAnchoredPhysics.FindTreeTrunkCollider( tree.SceneInstance );
			if ( collider.IsValid() )
				collider.Enabled = true;
		}
	}

	sealed class ThornsTreeRuntime
	{
		public int Id;
		public Vector2Int Cell;
		public FoliageSpecies Species;
		public int ListIndex;
		public Vector3 SpawnPosition;
		public Rotation SpawnRotation;
		public Vector3 SpawnScale;
		public GameObject CollisionProxy;
		public GameObject SceneInstance;
		public int ChopHits;
		public bool Depleted;
		public double RespawnAt;
	}
}
