namespace Terraingen.Minerals;

using Sandbox.Network;
using Terraingen;
using Terraingen.Physics;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Harvestable mineral registry, respawn, and node visibility.</summary>
[Title( "Thorns Mineral World" )]
[Category( "Terrain" )]
public sealed class ThornsMineralWorldService : Component
{
	public static ThornsMineralWorldService Instance { get; private set; }

	public static ThornsMineralWorldService ResolveInstance()
	{
		if ( MineralPlacerContext.ActiveWorld is not null && MineralPlacerContext.ActiveWorld.IsValid() )
			return MineralPlacerContext.ActiveWorld;

		if ( Instance is not null && Instance.IsValid() )
			return Instance;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return null;

		return scene.GetAllComponents<ThornsMineralWorldService>().FirstOrDefault();
	}

	readonly Dictionary<int, ThornsMineralRuntime> _nodes = new();
	readonly HashSet<int> _depleted = new();

	int _nextNodeId = 1;

	protected override void OnStart()
	{
		if ( Instance is not null && Instance != this && Instance.IsValid() )
			return;

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

	public void Begin()
	{
		Instance = this;
	}

	public void Clear()
	{
		_nodes.Clear();
		_depleted.Clear();
		_nextNodeId = 1;
	}

	public int RegisteredNodeCount => _nodes.Count;

	/// <summary>Enable harvest box colliders only near the observer — keeps physics broadphase lean.</summary>
	public void SyncHarvestCollisionProximity( Vector3 observer, float rangeInches )
	{
		if ( rangeInches <= 0f || observer.LengthSquared < 1f )
			return;

		var rangeSq = rangeInches * rangeInches;
		var observerPlanar = observer.WithZ( 0f );

		foreach ( var (_, node) in _nodes )
		{
			if ( node.Depleted || _depleted.Contains( node.Id ) )
				continue;

			if ( !node.Instance.IsValid() )
				continue;

			var collider = node.Instance.Components.Get<BoxCollider>( FindMode.EverythingInSelf );
			if ( collider is not { IsValid: true } )
				continue;

			var enable = (node.SpawnPosition.WithZ( 0f ) - observerPlanar).LengthSquared <= rangeSq;
			if ( collider.Enabled != enable )
				collider.Enabled = enable;
		}
	}

	/// <summary>Register scatter props that spawned before <see cref="Instance"/> was ready.</summary>
	public void RescanFromRoot( GameObject mineralRoot, Model model )
	{
		if ( !mineralRoot.IsValid() || !model.IsValid )
			return;

		foreach ( var chunk in mineralRoot.Children )
		{
			if ( !chunk.IsValid() )
				continue;

			foreach ( var node in chunk.Children )
			{
				if ( !node.IsValid() )
					continue;

				var existing = node.Components.Get<ThornsMineralInstance>();
				if ( existing.IsValid() && existing.NodeId > 0 && _nodes.ContainsKey( existing.NodeId ) )
					continue;

				var kind = node.Name.Contains( "Ore", StringComparison.OrdinalIgnoreCase )
					? MineralKind.Ore
					: MineralKind.Stone;

				RegisterNode( node, node.WorldPosition, node.WorldRotation, node.WorldScale, kind );
			}
		}
	}

	public IReadOnlyList<int> HostExportDepletedIds()
	{
		var list = new List<int>( _depleted.Count );
		foreach ( var id in _depleted )
		{
			if ( _nodes.ContainsKey( id ) )
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
			if ( !_nodes.TryGetValue( id, out var node ) || node.Depleted )
				continue;

			DepleteNode( node );
		}
	}

	public bool TryGetLiveNodeKind( int nodeId, out MineralKind kind )
	{
		kind = default;
		if ( nodeId <= 0 || _depleted.Contains( nodeId ) )
			return false;

		if ( !_nodes.TryGetValue( nodeId, out var node ) || node.Depleted )
			return false;

		kind = node.Kind;
		return true;
	}

	public bool TryPickNodeAlongRay(
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int nodeId )
		=> TryPickNodeAlongRay( origin, direction, maxRange, ignoreRoot, out nodeId, out _ );

	public bool TryPickNodeAlongRay(
		Vector3 origin,
		Vector3 direction,
		float maxRange,
		GameObject ignoreRoot,
		out int nodeId,
		out Vector3 hitPosition )
	{
		nodeId = 0;
		hitPosition = default;
		if ( maxRange <= 0f )
			return false;

		var dir = direction.Normal;
		if ( dir.Length < 0.95f )
			return false;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return false;

		var trace = ThornsGatherTargeting.TraceGatherTarget( scene, origin, direction, maxRange, "mineral", ignoreRoot );
		if ( !trace.Hit || !TryResolveNodeFromTrace( trace, out nodeId ) )
			return false;

		hitPosition = trace.HitPosition;
		var playerPos = ignoreRoot.IsValid() ? ignoreRoot.WorldPosition : origin;
		return ThornsGatherTargeting.IsWithinGatherReach( playerPos, hitPosition, maxRange );
	}

	bool TryResolveNodeFromTrace( SceneTraceResult trace, out int nodeId )
	{
		nodeId = 0;
		if ( !trace.Hit || !trace.GameObject.IsValid() )
			return false;

		var tag = trace.GameObject.Components.Get<ThornsMineralInstance>( FindMode.EverythingInSelfAndParent );
		if ( tag.IsValid() && tag.NodeId > 0 && _nodes.ContainsKey( tag.NodeId ) )
		{
			nodeId = tag.NodeId;
			return true;
		}

		return TryResolveNodeFromHitObject( trace.GameObject, out nodeId );
	}

	bool TryResolveNodeFromHitObject( GameObject hit, out int nodeId )
	{
		nodeId = 0;
		if ( !hit.IsValid() )
			return false;

		foreach ( var (id, node) in _nodes )
		{
			if ( node.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !node.Instance.IsValid() )
				continue;

			if ( node.Instance == hit || IsUnderObject( hit, node.Instance ) )
			{
				nodeId = id;
				return true;
			}
		}

		return false;
	}

	static bool IsUnderObject( GameObject hit, GameObject root )
	{
		if ( !hit.IsValid() || !root.IsValid() )
			return false;

		for ( var go = hit; go.IsValid(); go = go.Parent )
		{
			if ( go == root )
				return true;
		}

		return false;
	}

	/// <summary>Debug volumes when <c>gather_target_debug 1</c>.</summary>
	public void DrawGatherDebug(
		Vector3 playerPos,
		Vector3 aimOrigin,
		Vector3 aimDir,
		float traceInches,
		float forgivenessInches,
		int pickedNodeId )
	{
		var scanRadius = forgivenessInches * 1.35f;

		foreach ( var (id, node) in _nodes )
		{
			if ( node.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !node.Instance.IsValid() || !node.Instance.Enabled )
				continue;

			var anchor = node.Instance.WorldPosition;
			if ( (anchor - playerPos).Length > scanRadius )
				continue;

			var isPick = id == pickedNodeId;
			if ( isPick )
			{
				ThornsCollisionDebugDraw.DrawCollidersOnObject(
					DebugOverlay,
					node.Instance,
					0.12f,
					ThornsCollisionDebugDraw.Category.Mineral );
				DebugOverlay.Text( anchor + Vector3.Up * 48f, $"{node.Kind} #{id}", duration: 0.12f );
			}
		}

		if ( pickedNodeId > 0 )
			DebugOverlay.Text( aimOrigin + aimDir.Normal * traceInches * 0.5f, $"pick mineral #{pickedNodeId}", duration: 0.12f );
	}

	/// <summary>Harvest rock colliders when <see cref="ThornsCollisionDebug.OverlayEnabled"/>.</summary>
	public void DrawCollisionDebugOverlay( Vector3 observer, float maxDistance, float duration )
	{
		var maxDistSq = maxDistance * maxDistance;

		foreach ( var (id, node) in _nodes )
		{
			if ( node.Depleted || _depleted.Contains( id ) )
				continue;

			if ( !node.Instance.IsValid() || !node.Instance.Enabled )
				continue;

			if ( (node.Instance.WorldPosition - observer).LengthSquared > maxDistSq )
				continue;

			ThornsCollisionDebugDraw.DrawCollidersOnObject(
				DebugOverlay,
				node.Instance,
				duration,
				ThornsCollisionDebugDraw.Category.Mineral );
		}
	}

	public int RegisterNode(
		GameObject instance,
		Vector3 worldPosition,
		Rotation worldRotation,
		Vector3 worldScale,
		MineralKind kind )
	{
		if ( !instance.IsValid() )
			return -1;

		TerraingenAnchoredPhysics.EnsureSolidTags( instance );

		if ( !instance.Tags.Contains( "mineral" ) )
			instance.Tags.Add( "mineral" );

		var id = _nextNodeId++;
		_nodes[id] = new ThornsMineralRuntime
		{
			Id = id,
			Kind = kind,
			SpawnPosition = worldPosition,
			SpawnRotation = worldRotation,
			SpawnScale = worldScale,
			Instance = instance
		};

		var tag = instance.Components.Get<ThornsMineralInstance>() ?? instance.Components.Create<ThornsMineralInstance>();
		tag.NodeId = id;
		tag.Kind = kind;
		return id;
	}

	public bool IsNodeDepleted( int nodeId )
	{
		if ( nodeId < 0 )
			return true;

		if ( _depleted.Contains( nodeId ) )
			return true;

		return _nodes.TryGetValue( nodeId, out var node ) && node.Depleted;
	}

	public bool HostTryMine(
		GameObject playerRoot,
		int nodeId,
		Vector3 aimOrigin,
		Vector3 aimDirection )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_nodes.TryGetValue( nodeId, out var node ) )
			return false;

		if ( node.Depleted || _depleted.Contains( nodeId ) )
			return false;

		if ( !ThornsPickaxeTools.PlayerHasPickaxeEquipped( playerRoot ) )
			return false;

		if ( !TryPickNodeAlongRay(
			     aimOrigin,
			     aimDirection,
			     ThornsGatheringRange.Inches,
			     playerRoot,
			     out var authoritative )
		     || authoritative != nodeId )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			return false;

		if ( !HostTryApplyMineHit( node, nodeId, out var itemId, out var amount, out var justBroken ) )
			return false;

		if ( justBroken && Networking.IsActive )
		{
			ThornsNetInterest.HostBroadcastNear(
				ResolveNodeBroadcastPosition( node ),
				() => RpcSetNodeDepleted( nodeId ) );
		}

		gameplay.HostGrantHarvestItem( itemId, amount );
		return true;
	}

	public bool HostTrySalvageStone(
		GameObject playerRoot,
		int nodeId,
		Vector3 aimOrigin,
		Vector3 aimDirection )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !_nodes.TryGetValue( nodeId, out var node ) )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( $"[Thorns Salvage] HostTrySalvageStone #{nodeId}: missing node (registered={_nodes.Count})." );

			return false;
		}

		if ( node.Depleted || _depleted.Contains( nodeId ) )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( $"[Thorns Salvage] HostTrySalvageStone #{nodeId}: depleted." );

			return false;
		}

		if ( node.Kind != MineralKind.Stone )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( $"[Thorns Salvage] HostTrySalvageStone #{nodeId}: kind={node.Kind}." );

			return false;
		}

		if ( !TryPickNodeAlongRay(
			     aimOrigin,
			     aimDirection,
			     ThornsGatheringRange.Inches,
			     playerRoot,
			     out var authoritative )
		     || authoritative != nodeId )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( $"[Thorns Salvage] HostTrySalvageStone #{nodeId}: re-pick failed (authoritative={authoritative})." );

			return false;
		}

		if ( !ThornsGatherSalvage.PlayerHasEmptyHands( playerRoot ) )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( "[Thorns Salvage] HostTrySalvageStone: blocked — tool/weapon equipped." );

			return false;
		}

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );

		if ( !gameplay.IsValid() )
		{
			if ( ThornsGatherSalvage.Debug )
				Log.Info( "[Thorns Salvage] HostTrySalvageStone: no ThornsPlayerGameplay on pawn." );

			return false;
		}

		if ( !HostTryApplyMineHit(
			     node,
			     nodeId,
			     out _,
			     out var amount,
			     out var justBroken,
			     yieldOverride: ThornsMineralHarvestRules.StonePerSalvage,
			     includeBreakBonus: false ) )
			return false;

		if ( justBroken && Networking.IsActive )
		{
			ThornsNetInterest.HostBroadcastNear(
				ResolveNodeBroadcastPosition( node ),
				() => RpcSetNodeDepleted( nodeId ) );
		}

		gameplay.HostGrantHarvestItem( "stone", amount );
		return true;
	}

	[Rpc.Broadcast]
	void RpcSetNodeDepleted( int nodeId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !_nodes.TryGetValue( nodeId, out var node ) || node.Depleted )
			return;

		DepleteNode( node );
	}

	[Rpc.Broadcast]
	void RpcSetNodeRespawned( int nodeId )
	{
		if ( ThornsNetAuthority.RejectClientBroadcastOrigin() )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !_nodes.TryGetValue( nodeId, out var node ) )
			return;

		RespawnNode( node );
		_depleted.Remove( nodeId );
	}

	bool HostTryApplyMineHit(
		ThornsMineralRuntime node,
		int nodeId,
		out string itemId,
		out int amount,
		out bool justBroken,
		int? yieldOverride = null,
		bool includeBreakBonus = true )
	{
		itemId = "";
		amount = 0;
		justBroken = false;

		if ( node is null || node.Depleted || _depleted.Contains( nodeId ) )
			return false;

		if ( node.MineHits >= ThornsMineralHarvestRules.HitsToBreak )
			return false;

		node.MineHits++;
		itemId = ThornsMineralHarvestRules.ResourceItemId( node.Kind );
		amount = yieldOverride ?? ThornsMineralHarvestRules.YieldPerHit( node.Kind );

		if ( node.MineHits < ThornsMineralHarvestRules.HitsToBreak )
			return true;

		if ( !_depleted.Add( nodeId ) )
		{
			node.MineHits--;
			return false;
		}

		if ( includeBreakBonus )
			amount += ThornsMineralHarvestRules.BonusOnBreak( node.Kind );

		justBroken = true;
		DepleteNode( node );
		return true;
	}

	void DepleteNode( ThornsMineralRuntime node )
	{
		node.Depleted = true;
		_depleted.Add( node.Id );
		node.RespawnAt = Time.Now + ThornsMineralHarvestRules.RespawnSeconds;

		if ( !node.Instance.IsValid() )
			return;

		node.Instance.Enabled = false;
		ThornsWorldPersistence.RequestSave();
	}

	void TickRespawns()
	{
		foreach ( var id in _depleted.ToArray() )
		{
			if ( !_nodes.TryGetValue( id, out var node ) )
				continue;

			if ( Time.Now < node.RespawnAt )
				continue;

			RespawnNode( node );
			_depleted.Remove( id );

			if ( Networking.IsActive )
			{
				ThornsNetInterest.HostBroadcastNear(
					ResolveNodeBroadcastPosition( node ),
					() => RpcSetNodeRespawned( id ) );
			}

			ThornsWorldPersistence.RequestSave();
		}
	}

	void RespawnNode( ThornsMineralRuntime node )
	{
		node.Depleted = false;
		node.MineHits = 0;
		node.RespawnAt = 0;

		if ( !node.Instance.IsValid() )
			return;

		node.Instance.Enabled = true;
		node.Instance.WorldPosition = node.SpawnPosition;
		node.Instance.WorldRotation = node.SpawnRotation;
		node.Instance.WorldScale = node.SpawnScale;
	}

	static Vector3 ResolveNodeBroadcastPosition( ThornsMineralRuntime node )
	{
		if ( node is null )
			return Vector3.Zero;

		return node.Instance.IsValid() ? node.Instance.WorldPosition : node.SpawnPosition;
	}

	sealed class ThornsMineralRuntime
	{
		public int Id;
		public MineralKind Kind;
		public Vector3 SpawnPosition;
		public Rotation SpawnRotation;
		public Vector3 SpawnScale;
		public GameObject Instance;
		public int MineHits;
		public bool Depleted;
		public double RespawnAt;
	}
}
