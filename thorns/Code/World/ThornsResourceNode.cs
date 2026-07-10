using System.Collections.Generic;
using Terraingen.Foliage;

namespace Sandbox;

/// <summary>
/// Server-authoritative harvest node (THORNS_EVERYTHING_DOCUMENT §Harvest & resources).
/// Distance + alive checks run only on host; clients send intent via <see cref="ThornsHarvestInteractor"/>.
/// </summary>
[Title( "Thorns — Resource Node" )]
[Category( "Thorns" )]
[Icon( "forest" )]
public sealed class ThornsResourceNode : Component
{
	public const float ResourceRespawnSeconds = 300f;

	public static readonly Dictionary<Guid, ThornsResourceNode> ActiveById = new();

	[Property] public ThornsResourceKind ResourceKind { get; set; } = ThornsResourceKind.Wood;

	// Dense hand-placed node vs the default harvest reach band.
	[Property] public bool DensePlacementReachBand { get; set; }

	[Property] public float MaxHealth { get; set; } = 100f;

	[Property] public float DamageTakenPerHarvest { get; set; } = 25f;

	[Property] public int YieldPerHarvest { get; set; } = 5;

	[Sync( SyncFlags.FromHost )] public string NodeIdSync { get; set; } = "";

	public Guid NodeId => SyncGuidParse( NodeIdSync );

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; }

	[Sync( SyncFlags.FromHost )] public bool IsDepleted { get; set; }

	bool _respawnScheduled;
	double _respawnAtWorldTime;
	bool _lastVisualDepleted;
	Guid _registeredLookupId;
	bool _visualRefsCached;
	readonly List<ModelRenderer> _cachedRenderers = new();
	readonly List<BoxCollider> _cachedBoxColliders = new();
	readonly List<ModelCollider> _cachedModelColliders = new();

	static bool WorldSimulationAuthoritative =>
		!Networking.IsActive || Networking.IsHost;

	protected override void OnStart()
	{
		if ( WorldSimulationAuthoritative )
		{
			if ( Networking.IsActive )
				GameObject.NetworkMode = NetworkMode.Object;

			if ( string.IsNullOrWhiteSpace( NodeIdSync ) )
				NodeIdSync = Guid.NewGuid().ToString( "D" );

			CurrentHealth = MaxHealth;
			IsDepleted = false;
		}

		TryRegisterActiveLookup();
		_registeredLookupId = NodeId;
		_lastVisualDepleted = IsDepleted;
		UpdateVisualFromState();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		var id = NodeId;
		if ( id != Guid.Empty && _registeredLookupId != id )
		{
			TryRegisterActiveLookup();
			_registeredLookupId = id;
		}

		if ( _lastVisualDepleted == IsDepleted )
		{
			if ( ResourceKind == ThornsResourceKind.Wood && !IsDepleted )
				EnsureWoodTrunkCollider();
			return;
		}

		_lastVisualDepleted = IsDepleted;
		UpdateVisualFromState();
	}

	protected override void OnFixedUpdate()
	{
		if ( !WorldSimulationAuthoritative || !_respawnScheduled )
			return;

		if ( Time.Now < _respawnAtWorldTime )
			return;

		_respawnScheduled = false;
		CurrentHealth = MaxHealth;
		IsDepleted = false;
		UpdateVisualFromState();
	}

	protected override void OnDestroy()
	{
		var id = NodeId;
		if ( id != Guid.Empty )
			ActiveById.Remove( id );
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);

	/// <summary>Joiners need <see cref="ActiveById"/> populated for client-side harvest intent resolution — host previously registered only on authoritative start.</summary>
	void TryRegisterActiveLookup()
	{
		var id = NodeId;
		if ( id == Guid.Empty )
			return;

		ActiveById[id] = this;
	}

	/// <summary>Host-only effective harvest radius for this node + pawn (tools upgrade later).</summary>
	public float HostGetHarvestRangeForPawn( GameObject pawnRoot )
	{
		var baseRange = DensePlacementReachBand
			? ThornsHarvestTuning.DenseBandHarvestRange
			: ThornsHarvestTuning.DefaultBandHarvestRange;

		var toolMul = ThornsHarvestTuning.FutureToolReachMultiplier;
		var extended = HostHasExtendedHarvestReach( pawnRoot );
		var extMul = extended ? ThornsHarvestTuning.FutureExtendedReachMultiplier : 1f;
		return baseRange * toolMul * extMul;
	}

	bool HostHasExtendedHarvestReach( GameObject pawnRoot )
	{
		_ = pawnRoot;
		return false;
	}

	/// <summary>Host validates distance from pawn root to this node (never trust client).</summary>
	public bool HostIsCallerInHarvestRange( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() || !GameObject.IsValid() || !GameObject.Enabled || IsDepleted )
			return false;

		var maxDist = HostGetHarvestRangeForPawn( pawnRoot );
		var pw = pawnRoot.WorldPosition;
		var nw = GameObject.WorldPosition;

		// Wood: measure XY reach only — large tree variants dwarf base band vs pivot; Z mismatch from slopes is tolerated.
		if ( ResourceKind == ThornsResourceKind.Wood )
		{
			var planar = (pw.WithZ( 0 ) - nw.WithZ( 0 )).Length;
			var allowed = maxDist + WoodHarvestPlanarExtraReach( GameObject.LocalScale.x );
			return planar <= allowed;
		}

		return (pw - nw).Length <= maxDist;
	}

	/// <summary>Host-only: apply one harvest strike from validated caller inventory.</summary>
	public bool HostTryHarvestStrike(
		ThornsInventory callerInventory,
		GameObject callerPawnRoot,
		float yieldMultiplier,
		out int grantedQuantity,
		out string rejectReason,
		out bool resourceFullyDepleted )
	{
		rejectReason = "";
		grantedQuantity = 0;
		resourceFullyDepleted = false;

		if ( !WorldSimulationAuthoritative )
		{
			rejectReason = "not_host";
			return false;
		}

		if ( IsDepleted )
		{
			rejectReason = "depleted";
			return false;
		}

		if ( !callerInventory.IsValid() || callerInventory.GameObject != callerPawnRoot )
		{
			rejectReason = "bad_inventory";
			return false;
		}

		if ( !HostIsCallerInHarvestRange( callerPawnRoot ) )
		{
			rejectReason = "distance";
			return false;
		}

		var equip = callerPawnRoot.Components.Get<ThornsHotbarEquipment>();
		if ( !equip.IsValid() )
		{
			rejectReason = "no_equipment";
			return false;
		}

		var sel = equip.ServerGetSelectedHotbarIndex();
		if ( sel < 0 || !callerInventory.TryGetHostSlot( sel, out var toolSlot ) )
		{
			rejectReason = "no_tool_selected";
			return false;
		}

		ThornsItemRegistry.ThornsItemDefinition toolDef;
		if ( toolSlot.IsEmpty )
		{
			if ( !ThornsToolMeleeCombat.HostSelectedActsAsPrimitiveToolCombat( equip, callerInventory ) )
			{
				rejectReason = "no_tool_selected";
				return false;
			}

			toolDef = ThornsItemRegistry.PrimitiveToolDefinition;
		}
		else if ( !ThornsItemRegistry.TryGet( toolSlot.ItemId, out toolDef ) )
		{
			rejectReason = "unknown_tool";
			return false;
		}

		if ( toolDef.ItemType != ThornsItemType.Tool
		     || !ThornsItemRegistry.HarvestToolMatchesResourceKind( toolDef.HarvestToolKind, ResourceKind ) )
		{
			rejectReason = "wrong_tool";
			return false;
		}

		var itemId = HostResolveYieldItemId();
		var qty = toolDef.HarvestToolKind == ThornsHarvestToolKind.Primitive
			? 1
			: (int)Math.Max(
				1,
				Math.Floor(
					YieldPerHarvest * Math.Max( 0.01f, yieldMultiplier )
					                      * Math.Max( 0.25f, toolDef.ToolHarvestYieldMultiplier ) ) );

		if ( !callerInventory.HostCanFitStackableResourceQuantity( itemId, qty ) )
		{
			Log.Warning( $"[Thorns] Harvest rejected inventory_full node={NodeId} item={itemId} qty={qty}" );
			rejectReason = "inventory_full";
			return false;
		}

		var hpBefore = CurrentHealth;
		CurrentHealth -= DamageTakenPerHarvest;

		var leftover = callerInventory.ServerAddItem( itemId, qty );

		if ( leftover > 0 )
		{
			CurrentHealth = hpBefore;
			Log.Warning( $"[Thorns] Harvest rollback unexpected leftover node={NodeId} leftover={leftover}" );
			rejectReason = "inventory_full";
			return false;
		}

		grantedQuantity = qty;
		ThornsResourceHarvestLog.Strike( NodeId, ResourceKind, hpBefore, CurrentHealth, itemId, qty );

		if ( CurrentHealth <= 0f )
		{
			CurrentHealth = 0f;
			IsDepleted = true;
			ThornsResourceHarvestLog.Depleted( NodeId, ResourceKind );
			resourceFullyDepleted = true;
			_respawnScheduled = true;
			_respawnAtWorldTime = Time.Now + ResourceRespawnSeconds;
			UpdateVisualFromState();
			return true;
		}

		UpdateVisualFromState();
		resourceFullyDepleted = false;
		return true;
	}

	void EnsureWoodTrunkCollider()
	{
		EnsureVisualRefsCached();
		foreach ( var bc in _cachedBoxColliders )
		{
			if ( bc.IsValid() && bc.Enabled && bc.Static && !bc.IsTrigger )
				return;
		}

		var mr = GameObject.Components.Get<ModelRenderer>();
		if ( !mr.IsValid() || !mr.Model.IsValid() )
			return;

		ThornsAnchoredWorldPhysics.EnsureAnchoredWoodTrunkBoxPhysics( GameObject, mr.Model, GameObject.LocalScale.x );
		ThornsCollisionTags.EnsureWoodTreeTrunkSolidCollision( GameObject );
		_visualRefsCached = false;
		EnsureVisualRefsCached();
	}

	void EnsureVisualRefsCached()
	{
		if ( _visualRefsCached )
			return;

		_visualRefsCached = true;
		_cachedRenderers.Clear();
		_cachedBoxColliders.Clear();
		_cachedModelColliders.Clear();

		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				_cachedRenderers.Add( mr );
		}

		foreach ( var bc in GameObject.Components.GetAll<BoxCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( bc.IsValid() )
				_cachedBoxColliders.Add( bc );
		}

		foreach ( var mc in GameObject.Components.GetAll<ModelCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( mc.IsValid() )
				_cachedModelColliders.Add( mc );
		}
	}

	void UpdateVisualFromState()
	{
		EnsureVisualRefsCached();

		var lodProxy = GameObject.Components.Get<ThornsResourceLodProxy>( FindMode.EnabledInSelf );

		// Depleted: hide mesh entirely until respawn (no faded "ghost" stump). Collider off so nothing blocks or traces.
		if ( IsDepleted )
		{
			foreach ( var mr in _cachedRenderers )
			{
				if ( mr.IsValid() )
					mr.Enabled = false;
			}

			foreach ( var bc in _cachedBoxColliders )
			{
				if ( bc.IsValid() )
					bc.Enabled = false;
			}

			foreach ( var mc in _cachedModelColliders )
			{
				if ( mc.IsValid() && !mc.IsTrigger )
					mc.Enabled = false;
			}

			return;
		}

		var lod = GameObject.Components.Get<ThornsResourceLodProxy>( FindMode.EnabledInSelf );
		var collidersOn = !lod.IsValid() || lod.CollidersMatchNearMesh;

		foreach ( var bc in _cachedBoxColliders )
		{
			if ( bc.IsValid() )
				bc.Enabled = collidersOn;
		}

		foreach ( var mc in _cachedModelColliders )
		{
			if ( mc.IsValid() && !mc.IsTrigger )
				mc.Enabled = collidersOn;
		}

		var tint = ResourceKind == ThornsResourceKind.Wood
			? Color.White
			: ResourceKind == ThornsResourceKind.Fiber
				? new Color( 0.55f, 0.82f, 0.48f, 1f )
				: ResourceKind == ThornsResourceKind.MetalOre
					? new Color( 0.78f, 0.52f, 0.38f, 1f )
					: new Color( 0.92f, 0.91f, 0.9f, 1f );

		foreach ( var mr in _cachedRenderers )
		{
			if ( !mr.IsValid() )
				continue;
			mr.Tint = tint;
			// Stone / ore: <see cref="ThornsResourceLodProxy"/> owns NearRenderer.Enabled for distance — only force-enable when no LOD.
			if ( !lodProxy.IsValid() )
				mr.Enabled = true;
		}
	}

	/// <summary>Nearest non-depleted node within radius (client approximation for UX / input routing).</summary>
	public static ThornsResourceNode FindNearestHarvestable( Scene scene, Vector3 fromWorld, float radius ) =>
		FindNearestHarvestableFromRegistry( fromWorld, radius );

	/// <summary>Max distance for owner-client harvest hint + input routing (matches host band + wood canopy slack).</summary>
	public float ClientGetHarvestHintMaxDistance()
	{
		var maxDist = DensePlacementReachBand
			? ThornsHarvestTuning.DenseBandHarvestRange
			: ThornsHarvestTuning.DefaultBandHarvestRange;

		if ( ResourceKind == ThornsResourceKind.Wood )
			maxDist += WoodHarvestPlanarExtraReach( GameObject.LocalScale.x );

		return maxDist;
	}

	/// <summary>True when the local pawn is close enough to show harvest prompts / send harvest intent.</summary>
	public bool ClientIsWithinHarvestHintRange( Vector3 pawnWorldPosition ) =>
		ClientHarvestDistanceToNode( this, pawnWorldPosition ) <= ClientGetHarvestHintMaxDistance();

	/// <summary>Client-side harvest proximity — wood uses planar reach like <see cref="HostIsCallerInHarvestRange"/>.</summary>
	public static float ClientHarvestDistanceToNode( ThornsResourceNode node, Vector3 fromWorld )
	{
		if ( !node.IsValid() )
			return float.MaxValue;

		var nw = node.GameObject.WorldPosition;
		if ( node.ResourceKind == ThornsResourceKind.Wood )
			return (fromWorld.WithZ( 0 ) - nw.WithZ( 0 )).Length;

		return (nw - fromWorld).Length;
	}

	/// <summary>Client harvest routing — skip depleted, disabled, or LOD-hidden nodes.</summary>
	public bool ClientIsHarvestTargetable()
	{
		if ( !IsValid || IsDepleted || !GameObject.IsValid() || !GameObject.Enabled )
			return false;

		EnsureVisualRefsCached();
		if ( ResourceKind == ThornsResourceKind.Wood )
		{
			foreach ( var mr in _cachedRenderers )
			{
				if ( mr.IsValid() && mr.Enabled )
					return true;
			}

			return false;
		}

		return true;
	}

	/// <summary>Foliage distance LOD — toggle mesh/collider without disabling the harvest node.</summary>
	public void ApplyFoliageLodVisual( bool visible, bool castShadows )
	{
		if ( !GameObject.IsValid() || IsDepleted )
			return;

		EnsureVisualRefsCached();

		foreach ( var mr in _cachedRenderers )
		{
			if ( !mr.IsValid() )
				continue;

			mr.Enabled = visible;
			if ( visible )
				mr.RenderType = castShadows ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.Off;
		}

		foreach ( var bc in _cachedBoxColliders )
		{
			if ( bc.IsValid() )
				bc.Enabled = true;
		}

		foreach ( var mc in _cachedModelColliders )
		{
			if ( mc.IsValid() && !mc.IsTrigger )
				mc.Enabled = true;
		}
	}

	static ThornsResourceNode FindNearestHarvestableFromRegistry( Vector3 fromWorld, float radius )
	{
		ThornsResourceNode best = default;
		var bestD = radius;

		foreach ( var n in ActiveById.Values )
		{
			if ( !n.IsValid || n.IsDepleted || !n.ClientIsHarvestTargetable() )
				continue;

			var d = ClientHarvestDistanceToNode( n, fromWorld );
			if ( d < bestD )
			{
				bestD = d;
				best = n;
			}
		}

		return best;
	}

	/// <summary>Host/offline: register lookup immediately after runtime spawn (foliage batch may delay <see cref="OnStart"/>).</summary>
	public void HostEnsureHarvestLookupRegistered()
	{
		if ( !WorldSimulationAuthoritative )
			return;

		if ( string.IsNullOrWhiteSpace( NodeIdSync ) )
			NodeIdSync = Guid.NewGuid().ToString( "D" );

		TryRegisterActiveLookup();
		_registeredLookupId = NodeId;
	}

	/// <summary>Stackable item id granted per harvest strike for this node.</summary>
	public string HostResolveYieldItemId() =>
		ResourceKind switch
		{
			ThornsResourceKind.Wood => "wood",
			ThornsResourceKind.Stone => "stone",
			ThornsResourceKind.MetalOre => "metal_ore",
			ThornsResourceKind.Fiber => "cloth",
			_ => "wood"
		};

	/// <summary>Extra X/Y shrink on wood tree <see cref="BoxCollider"/> (planar footprint); Z kept at 1 so height / traces still behave).</summary>
	const float WoodTreeHullPlanarMul = 0.45f;

	/// <summary>
	/// Pivot stays near trunk base while foliage2 tree meshes extend a wide canopy — host harvest range must include planar distance to canopy work volume.
	/// </summary>
	static float WoodHarvestPlanarExtraReach( float uniformRootScale ) =>
		Math.Clamp( uniformRootScale * 0.38f, 96f, 260f );

	/// <summary>Fallback uniform when foliage2 model load fails.</summary>
	const float WoodTreeDefaultUniformScale = 220f;

	const float Foliage2TargetHeightInches = 4200f;
	const float Foliage2InchesPerMeter = 39.37f;
	const float Foliage2ScaleMultiplier = 2f;
	const float Foliage2TreeSizeMultiplier = 2f;
	const float Foliage2MinRenderScale = 80f;

	/// <summary>Match <see cref="Terraingen.Foliage.ThornsFoliagePlacer"/> sizing for foliage2 pine/aspen/oak.</summary>
	public static float ResolveWoodHarvestUniformScale( Model foliage2Model, float inspectorUniformScale )
	{
		if ( inspectorUniformScale > 0.01f )
			return inspectorUniformScale;

		if ( !foliage2Model.IsValid )
			return WoodTreeDefaultUniformScale;

		var bounds = foliage2Model.Bounds;
		var maxAxis = Math.Max( bounds.Size.x, Math.Max( bounds.Size.y, bounds.Size.z ) );
		var meshMeters = Math.Max( maxAxis, 0.01f );
		var targetMeters = Foliage2TargetHeightInches / Foliage2InchesPerMeter;
		var uniform = targetMeters / meshMeters * Foliage2ScaleMultiplier * Foliage2TreeSizeMultiplier;
		return Math.Max( uniform, Foliage2MinRenderScale );
	}

	/// <summary>Box hull shrink when scaling mesh AABB to world size on the scaled visual child.</summary>
	const float MineralHarvestHullExtentMul = 0.62f;

	/// <summary>Same rock meshes as clutter/boulders — harvest nodes ~67–127 uniform (1/3 of former 200–380).</summary>
	const float MineralHarvestUniformScaleMin = 200f / 3f;
	const float MineralHarvestUniformScaleMax = 380f / 3f;

	const float MineralHarvestGroundEmbedInches = 8f;

	static bool _loggedFirstMineralNode;
	static bool _loggedStoneModelMissing;
	static bool _loggedOreModelMissing;
	static int _mineralStoneSpawnOk;
	static int _mineralStoneSpawnFail;
	static int _mineralOreSpawnOk;
	static int _mineralOreSpawnFail;
	static bool _mineralCatalogWarmed;

	/// <summary>Places a scatter point on the live terraingen terrain mesh (pivot-aware).</summary>
	public static bool TryResolveHostScatterWorldPosition(
		Scene scene,
		in ThornsTerrainNetSpec spec,
		GameObject chunkRoot,
		float localX,
		float localY,
		ThornsResourceKind kind,
		float woodHarvestUniformScale,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( chunkRoot is null || !chunkRoot.IsValid() )
			return false;

		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			return TryResolveHostMineralScatterWorldPosition( scene, spec, chunkRoot, localX, localY, kind, out worldPosition );

		var planar = chunkRoot.WorldPosition + chunkRoot.WorldRotation * new Vector3( localX, localY, 0f );
		var model = HostTryLoadResourceVisual( kind, planar );
		if ( !model.IsValid() )
			return false;

		var woodUniform = ResolveWoodHarvestUniformScale( model, woodHarvestUniformScale );
		var scale = kind switch
		{
			ThornsResourceKind.Wood => new Vector3( woodUniform, woodUniform, woodUniform ),
			ThornsResourceKind.Fiber => FiberHarvestUniformScale( model, worldPosition ),
			_ => Vector3.One
		};

		return ThornsTerraingenTerrainQueries.TryResolveScatterWorldOnTerrain(
			scene,
			spec,
			chunkRoot,
			localX,
			localY,
			model,
			scale,
			clutterGrassLift: false,
			out worldPosition );
	}

	static bool TryResolveHostMineralScatterWorldPosition(
		Scene scene,
		in ThornsTerrainNetSpec spec,
		GameObject chunkRoot,
		float localX,
		float localY,
		ThornsResourceKind kind,
		out Vector3 worldPosition )
	{
		worldPosition = default;
		var planar = chunkRoot.WorldPosition + chunkRoot.WorldRotation * new Vector3( localX, localY, 0f );

		if ( !TrySampleMineralGroundAtPlanar( scene, planar.x, planar.y, planar.z, out var ground ) )
			ground = planar;

		worldPosition = ground;
		return worldPosition.z >= spec.WaterLevelWorldZ;
	}

	static Vector3 AlignMineralRootOnGround( Vector3 terrainSurface, Model model, float uniformScale, Rotation worldRotation )
	{
		var visualScale = new Vector3( uniformScale, uniformScale, uniformScale );
		var pos = ThornsFoliageScatter.AlignPivotWorldPositionMeshBottomOnGround(
			terrainSurface,
			model,
			visualScale,
			worldRotation );
		return pos - Vector3.Up * MineralHarvestGroundEmbedInches;
	}

	static bool TrySampleMineralGroundAtPlanar(
		Scene scene,
		float worldX,
		float worldY,
		float heightHintZ,
		out Vector3 groundWorld )
	{
		groundWorld = default;
		if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld( scene, worldX, worldY, 0f, out groundWorld ) )
			return true;

		if ( !ThornsTerraingenTerrainQueries.TryFindTerrain( scene, out var terrain ) )
			return false;

		if ( ThornsTerraingenTerrainQueries.TryRaycastTerrain( terrain, worldX, worldY, out groundWorld ) )
			return true;

		return ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
			scene,
			new Vector3( worldX, worldY, heightHintZ ),
			startLiftZ: 4096f,
			segmentLength: 32768f,
			out groundWorld );
	}

	static Vector3 HostResolveMineralSpawnWorld(
		Scene scene,
		Vector3 planarHint,
		Model model,
		Rotation worldRotation,
		float uniformScale )
	{
		if ( !model.IsValid() )
			return planarHint;

		if ( !TrySampleMineralGroundAtPlanar( scene, planarHint.x, planarHint.y, planarHint.z, out var ground ) )
			ground = planarHint.WithZ( planarHint.z );

		return AlignMineralRootOnGround( ground, model, uniformScale, worldRotation );
	}

	static float ResolveMineralHarvestUniformScale( Model model, Vector3 worldPosition, ThornsResourceKind kind )
	{
		var qx = (int)MathF.Round( worldPosition.x * 0.25f );
		var qy = (int)MathF.Round( worldPosition.y * 0.25f );
		var qz = (int)MathF.Round( worldPosition.z * 0.25f );
		var salt = kind == ThornsResourceKind.MetalOre ? unchecked( (int)0x4F52454Eu ) : unchecked( (int)0x53544F4Eu );
		var rnd = new Random( HashCode.Combine( salt, qx, qy, qz ) );
		var uniform = MathX.Lerp(
			MineralHarvestUniformScaleMin,
			MineralHarvestUniformScaleMax,
			(float)rnd.NextDouble() );
		if ( kind == ThornsResourceKind.MetalOre )
			uniform *= MathX.Lerp( 0.94f, 1.06f, (float)rnd.NextDouble() );

		_ = model;
		return uniform;
	}

	/// <summary>
	/// Adds wood harvest + trunk physics to an existing foliage2 tree (terraingen placement). Host/offline only; call after the <see cref="ModelRenderer"/> exists.
	/// </summary>
	public static ThornsResourceNode AttachWoodHarvestGameplay( GameObject go, Model model, float uniformScale )
	{
		if ( !go.IsValid() || !model.IsValid() )
			return default;

		if ( Networking.IsActive && !Networking.IsHost )
			return default;

		var uniform = ResolveWoodHarvestUniformScale( model, uniformScale );
		ThornsAnchoredWorldPhysics.EnsureAnchoredWoodTrunkBoxPhysics( go, model, uniform );
		ThornsCollisionTags.EnsureWoodTreeTrunkSolidCollision( go );

		var node = go.Components.Get<ThornsResourceNode>() ?? go.Components.Create<ThornsResourceNode>();
		node.ResourceKind = ThornsResourceKind.Wood;

		node.HostEnsureHarvestLookupRegistered();

		if ( Networking.IsActive && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
			Log.Warning( "[Thorns] Foliage tree NetworkSpawn failed — joiners may not see this tree." );

		if ( ThornsCollisionAudit.SpawnValidationEnabled )
			ThornsCollisionAudit.TrySpawnAudit( go, "resource_node:Wood" );

		return node;
	}

	/// <summary>Host spawn helper — networked object mode for replication.</summary>
	public static ThornsResourceNode SpawnHost( Scene scene, Vector3 worldPosition, ThornsResourceKind kind, bool denseBand, float woodHarvestUniformScale = -1f )
	{
		_ = scene;
		if ( Networking.IsActive && !Networking.IsHost )
			return default;
		if ( kind == ThornsResourceKind.Fiber )
			return default;

		var name = kind switch
		{
			ThornsResourceKind.Wood => "ThornsTreeNode",
			ThornsResourceKind.Stone => "ThornsRockNode",
			ThornsResourceKind.MetalOre => "ThornsMetalVeinNode",
			ThornsResourceKind.Fiber => "ThornsFiberNode",
			_ => "ThornsResourceNode"
		};
		var go = new GameObject( true, name );
		go.WorldRotation = BuildDeterministicScatterRotation( kind, worldPosition );

		// Wood: foliage2 harvest visuals. Stone / ore: models/resources/ with clutter rock fallback.
		var visModel = HostTryLoadResourceVisual( kind, worldPosition );
		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
		{
			if ( !visModel.IsValid() )
			{
				HostLogMineralModelMissingOnce( kind );
				go.Destroy();
				HostRecordMineralSpawnResult( kind, spawned: false );
				return default;
			}
		}

		var rendered = visModel.IsValid() ? visModel : Model.Load( "models/dev/box.vmdl" );
		var woodUniformBase = kind == ThornsResourceKind.Wood
			? ResolveWoodHarvestUniformScale( visModel, woodHarvestUniformScale )
			: woodHarvestUniformScale > 0.01f ? woodHarvestUniformScale : WoodTreeDefaultUniformScale;
		var woodUniform = kind == ThornsResourceKind.Wood
			? woodUniformBase * WoodDeterministicScaleVariationMul12x( worldPosition )
			: woodUniformBase;
		var fiberUniform = kind == ThornsResourceKind.Fiber
			? FiberHarvestUniformScale( visModel, worldPosition )
			: woodUniform;
		var mineralUniform = kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre
			? ResolveMineralHarvestUniformScale( visModel, worldPosition, kind )
			: 1f;

		var spawnWorld = worldPosition;
		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			spawnWorld = HostResolveMineralSpawnWorld( scene, worldPosition, visModel, go.WorldRotation, mineralUniform );
		else if ( kind == ThornsResourceKind.Wood && visModel.IsValid() && rendered.IsValid() )
			spawnWorld += Vector3.Up * WoodVerticalLiftForBottomOnGroundZ( rendered, woodUniform );

		if ( kind == ThornsResourceKind.Wood
		     && !ThornsTerraingenTerrainQueries.TryFindTerrain( scene, out _ ) )
			spawnWorld -= Vector3.Up * ThornsFoliageScatter.FoliagePostAlignSinkWorldZ;

		go.WorldPosition = spawnWorld;

		ModelRenderer vis;
		GameObject materialScaleHost;
		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
		{
			go.LocalScale = Vector3.One;
			var visual = new GameObject( true, "NodeVisual" );
			visual.SetParent( go );
			visual.LocalPosition = Vector3.Zero;
			visual.LocalRotation = Rotation.Identity;
			visual.LocalScale = new Vector3( mineralUniform, mineralUniform, mineralUniform );
			vis = visual.Components.Create<ModelRenderer>();
			materialScaleHost = visual;
		}
		else
		{
			go.LocalScale = visModel.IsValid()
				? kind switch
				{
					ThornsResourceKind.Wood => new Vector3( woodUniform, woodUniform, woodUniform ),
					ThornsResourceKind.Fiber => new Vector3( fiberUniform, fiberUniform, fiberUniform ),
					_ => Vector3.One
				}
				: kind switch
				{
					ThornsResourceKind.Wood or ThornsResourceKind.Fiber => new Vector3( 0.45f, 0.45f, 1.1f ),
					_ => Vector3.One
				};
			vis = go.Components.Create<ModelRenderer>();
			materialScaleHost = go;
		}

		vis.Model = rendered;
		var modelPath = visModel.IsValid() ? visModel.Name : rendered.Name;
		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			ApplyMineralHarvestVisualPresentation( vis, materialScaleHost, rendered, kind, modelPath );

		var collisionSource = visModel.IsValid() ? visModel : ThornsAnchoredWorldPhysics.DevBoxCollisionModel;
		var hullMul = kind switch
		{
			ThornsResourceKind.Wood or ThornsResourceKind.Fiber => 0.56f,
			ThornsResourceKind.Stone or ThornsResourceKind.MetalOre => MineralHarvestHullExtentMul,
			_ => 1f
		};
		if ( kind == ThornsResourceKind.Wood )
			ThornsAnchoredWorldPhysics.EnsureAnchoredWoodTrunkBoxPhysics( go, collisionSource, woodUniform );
		else if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			HostEnsureMineralHarvestPhysics( go, collisionSource, mineralUniform );
		else
			ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( go, collisionSource, hullMul );

		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			ThornsCollisionTags.EnsureMineralHarvestSolidCollision( go );
		else if ( kind == ThornsResourceKind.Wood )
			ThornsCollisionTags.EnsureWoodTreeTrunkSolidCollision( go );
		else
			ThornsCollisionTags.EnsureResourceNodeMovementPassthrough( go );

		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre && !_loggedFirstMineralNode )
		{
			_loggedFirstMineralNode = true;
			var bb = visModel.Bounds;
			var estH = bb.Size.z * mineralUniform;
			Log.Info(
				$"[Thorns] First {kind} harvest node at {spawnWorld}, model={modelPath}, scale={mineralUniform:F0}, bounds={bb.Size}, estHeight≈{estH:F0} in" );
		}

		var node = go.Components.Create<ThornsResourceNode>();
		node.ResourceKind = kind;
		node.DensePlacementReachBand = denseBand;
		switch ( kind )
		{
			case ThornsResourceKind.MetalOre:
				node.MaxHealth = 150f;
				node.YieldPerHarvest = 3;
				node.DamageTakenPerHarvest = 20f;
				break;
			case ThornsResourceKind.Fiber:
				node.MaxHealth = 88f;
				node.YieldPerHarvest = 4;
				node.DamageTakenPerHarvest = 23f;
				break;
		}

		if ( Networking.IsActive
		     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
			Log.Warning( "[Thorns] Resource node NetworkSpawn failed — joiners may not see this node." );

		if ( ThornsCollisionAudit.SpawnValidationEnabled )
			ThornsCollisionAudit.TrySpawnAudit( go, $"resource_node:{kind}" );

		if ( kind is ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			HostRecordMineralSpawnResult( kind, spawned: node.IsValid() );

		return node;
	}

	/// <summary>Host scatter start — reset deferred mineral spawn counters.</summary>
	public static void HostResetMineralSpawnStats()
	{
		_mineralStoneSpawnOk = 0;
		_mineralStoneSpawnFail = 0;
		_mineralOreSpawnOk = 0;
		_mineralOreSpawnFail = 0;
		_loggedFirstMineralNode = false;
	}

	static void HostRecordMineralSpawnResult( ThornsResourceKind kind, bool spawned )
	{
		switch ( kind )
		{
			case ThornsResourceKind.Stone:
				if ( spawned )
					_mineralStoneSpawnOk++;
				else
					_mineralStoneSpawnFail++;
				break;
			case ThornsResourceKind.MetalOre:
				if ( spawned )
					_mineralOreSpawnOk++;
				else
					_mineralOreSpawnFail++;
				break;
		}
	}

	/// <summary>Log once when the deferred spawn queue drains.</summary>
	public static void HostLogMineralSpawnSummaryIfAny()
	{
		var total = _mineralStoneSpawnOk + _mineralStoneSpawnFail + _mineralOreSpawnOk + _mineralOreSpawnFail;
		if ( total <= 0 )
			return;

		Log.Info(
			$"[Thorns] Mineral harvest spawn summary: stone ok={_mineralStoneSpawnOk} fail={_mineralStoneSpawnFail} ore ok={_mineralOreSpawnOk} fail={_mineralOreSpawnFail}" );
	}

	/// <summary>Preload harvest rock models and log which paths resolve (primary or clutter fallback).</summary>
	public static void HostWarmupMineralHarvestCatalog()
	{
		if ( _mineralCatalogWarmed )
			return;

		_mineralCatalogWarmed = true;

		var stone = HostTryLoadFirstValidInOrder( StoneHarvestVisualPaths );
		var stoneFallback = !stone.IsValid();
		if ( !stone.IsValid() )
			stone = HostTryLoadFirstValidInOrder( MineralHarvestVisualFallbackPaths );

		var ore = HostTryLoadFirstValidInOrder( MetalOreHarvestVisualPaths );
		var oreFallback = !ore.IsValid();
		if ( !ore.IsValid() )
			ore = stone.IsValid() ? stone : HostTryLoadFirstValidInOrder( MineralHarvestVisualFallbackPaths );

		if ( stone.IsValid() )
		{
			Log.Info(
				$"[Thorns] Mineral harvest catalog stone={stone.Name} bounds={stone.Bounds.Size} fallback={stoneFallback}" );
		}
		else
		{
			Log.Warning(
				$"[Thorns] Mineral harvest catalog: no stone model — compile Assets/models/resources/ or ensure models/clutter/rock*.vmdl." );
		}

		if ( ore.IsValid() )
		{
			Log.Info(
				$"[Thorns] Mineral harvest catalog ore={ore.Name} bounds={ore.Bounds.Size} fallback={oreFallback}" );
		}
		else
		{
			Log.Warning( "[Thorns] Mineral harvest catalog: no ore model loaded." );
		}
	}

	static Rotation BuildDeterministicScatterRotation( ThornsResourceKind kind, Vector3 worldPosition )
	{
		var qx = (int)MathF.Round( worldPosition.x * 0.25f );
		var qy = (int)MathF.Round( worldPosition.y * 0.25f );
		var qz = (int)MathF.Round( worldPosition.z * 0.25f );
		var rnd = new Random( HashCode.Combine( (int)kind, qx, qy, qz ) );

		var yaw = (float)(rnd.NextDouble() * 360.0);
		if ( kind is ThornsResourceKind.Wood or ThornsResourceKind.Stone or ThornsResourceKind.MetalOre )
			return Rotation.FromYaw( yaw );

		var pitch = ((float)rnd.NextDouble() * 2f - 1f) * 8f;
		var roll = ((float)rnd.NextDouble() * 2f - 1f) * 8f;
		return Rotation.From( pitch, yaw, roll );
	}

	static void HostEnsureMineralHarvestPhysics( GameObject root, Model collisionModel, float visualUniformScale )
	{
		if ( !root.IsValid() || !collisionModel.IsValid() )
			return;

		var uniform = Math.Max( visualUniformScale, 1f );
		var hullExtent = uniform * MineralHarvestHullExtentMul;
		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysicsMatchVisualMesh( root, collisionModel, hullExtent );
	}

	/// <summary>Stone/metal: harvest models (resources/ or clutter fallback). Wood: foliage2. Fiber: mushroom.</summary>
	static Model HostTryLoadResourceVisual( ThornsResourceKind kind, Vector3 worldPosition )
	{
		switch ( kind )
		{
			case ThornsResourceKind.Wood:
				return HostTryLoadWoodTreeVisual( worldPosition );
			case ThornsResourceKind.Fiber:
				return HostTryLoadFiberVisual( worldPosition );
			case ThornsResourceKind.Stone:
				return HostTryLoadHarvestMineralVisual( StoneHarvestVisualPaths, worldPosition, salt: unchecked( (int)0x53544F4Eu ) );
			case ThornsResourceKind.MetalOre:
			{
				var oreModel = HostTryLoadHarvestMineralVisual(
					MetalOreHarvestVisualPaths,
					worldPosition,
					salt: unchecked( (int)0x4F52454Eu ) );
				return oreModel.IsValid()
					? oreModel
					: HostTryLoadHarvestMineralVisual(
						StoneHarvestVisualPaths,
						worldPosition,
						salt: unchecked( (int)0x4F524546u ) );
			}
			default:
				return default;
		}
	}

	static void HostLogMineralModelMissingOnce( ThornsResourceKind kind )
	{
		ref var flag = ref (kind == ThornsResourceKind.MetalOre ? ref _loggedOreModelMissing : ref _loggedStoneModelMissing);
		if ( flag )
			return;

		flag = true;
		var paths = kind == ThornsResourceKind.MetalOre ? MetalOreHarvestVisualPaths : StoneHarvestVisualPaths;
		Log.Warning(
			$"[Thorns] {kind} harvest node: no model loaded ({string.Join( ", ", paths )} or clutter fallback)." );
	}

	static Model HostTryLoadHarvestMineralVisual( string[] paths, Vector3 worldPosition, int salt )
	{
		var m = HostTryLoadHarvestMineralVisualFromPaths( paths, worldPosition, salt );
		if ( m.IsValid() )
			return m;

		return HostTryLoadHarvestMineralVisualFromPaths(
			MineralHarvestVisualFallbackPaths,
			worldPosition,
			salt ^ unchecked( (int)0x46414C42u ) );
	}

	static Model HostTryLoadHarvestMineralVisualFromPaths( string[] paths, Vector3 worldPosition, int salt )
	{
		var n = paths.Length;
		if ( n <= 0 )
			return default;

		var start = DeterministicVariantIndex( worldPosition, n, salt );
		for ( var i = 0; i < n; i++ )
		{
			var path = paths[(start + i) % n];
			var m = ThornsFoliageModelCache.Load( path );
			if ( IsRenderableModel( m ) )
				return m;
		}

		return default;
	}

	static Model HostTryLoadFiberVisual( Vector3 worldPosition )
	{
		var paths = FiberVisualModelPaths;
		var n = paths.Length;
		if ( n > 0 )
		{
			var start = DeterministicVariantIndex( worldPosition, n, salt: unchecked((int)0x46696272u) );
			for ( var i = 0; i < n; i++ )
			{
				var m = Terraingen.Foliage.ThornsFoliageModelCache.Load( paths[(start + i) % n] );
				if ( IsRenderableModel( m ) )
					return m;
			}
		}

		return default;
	}

	static Model HostTryLoadWoodTreeVisual( Vector3 worldPosition )
	{
		var paths = WoodHarvestVisualPaths;
		var n = paths.Length;
		if ( n > 0 )
		{
			var start = DeterministicVariantIndex( worldPosition, n, salt: 0x54726565 );
			for ( var i = 0; i < n; i++ )
			{
				var m = Terraingen.Foliage.ThornsFoliageModelCache.Load( paths[(start + i) % n] );
				if ( IsRenderableModel( m ) )
					return m;
			}
		}

		var oak = Terraingen.Foliage.ThornsFoliageModelCache.Load( WoodTreeFallbackVisualPath );
		return IsRenderableModel( oak ) ? oak : default;
	}

	static int DeterministicVariantIndex( Vector3 worldPosition, int variantCount, int salt )
	{
		if ( variantCount <= 1 )
			return 0;
		var qx = (int)MathF.Round( worldPosition.x * 0.25f );
		var qy = (int)MathF.Round( worldPosition.y * 0.25f );
		var qz = (int)MathF.Round( worldPosition.z * 0.25f );
		var u = unchecked((uint)HashCode.Combine( salt, qx, qy, qz ));
		return (int)(u % (uint)variantCount);
	}

	static Model HostTryLoadFirstValidInOrder( string[] paths )
	{
		foreach ( var p in paths )
		{
			var m = Terraingen.Foliage.ThornsFoliageModelCache.Load( p );
			if ( IsRenderableModel( m ) )
				return m;
		}

		return default;
	}

	static bool IsClutterRockHarvestPath( string modelAssetPath ) =>
		!string.IsNullOrWhiteSpace( modelAssetPath )
		&& modelAssetPath.Contains( "models/clutter/rock", StringComparison.OrdinalIgnoreCase );

	/// <summary>
	/// All foliage tree variants — always seven slots so tree4–7 are not dropped when <see cref="Model.Load"/> failed once during early startup caching.
	/// <see cref="HostTryLoadWoodTreeVisual"/> skips broken entries per spawn.
	/// </summary>
	static readonly string[] WoodHarvestVisualPaths =
	{
		"models/foliage2/pine_tree.vmdl",
		"models/foliage2/aspen_tree.vmdl",
		"models/foliage2/oak_tree.vmdl",
	};

	const string WoodTreeFallbackVisualPath = "models/foliage2/oak_tree.vmdl";

	static bool IsRenderableModel( Model m ) =>
		m.IsValid() && !m.IsError;

	/// <summary>
	/// Pivot is usually at model center; terrain snap hits ground at pivot height — lift root so mesh AABB bottom (local −Z face) meets that ground height.
	/// Uses render <see cref="Model.Bounds"/> in model space × uniform root scale; valid for yaw-only trees (horizontal plane XY, +Z up).
	/// </summary>
	static float WoodVerticalLiftForBottomOnGroundZ( Model visualModel, float uniformScale )
	{
		if ( !visualModel.IsValid() || uniformScale <= 0.01f )
			return 0f;

		var bb = visualModel.Bounds;
		if ( bb.Size.LengthSquared < 1e-12f )
			return 0f;

		var minLocalZ = bb.Center.z - bb.Size.z * 0.5f;
		return -minLocalZ * uniformScale;
	}

	/// <summary>Deterministic multiplier in [1, 2] from spawn position so trees vary in size while staying synced in MP.</summary>
	static float WoodDeterministicScaleVariationMul12x( Vector3 worldPosition )
	{
		var qx = (int)MathF.Round( worldPosition.x * 0.29f );
		var qy = (int)MathF.Round( worldPosition.y * 0.29f );
		var qz = (int)MathF.Round( worldPosition.z * 0.11f );
		var rng = new Random( HashCode.Combine( 0x5343414cu, qx, qy, qz ) );
		return 1f + (float)rng.NextDouble();
	}

	static readonly string[] FiberVisualModelPaths =
	{
		ThornsFoliageScatter.DefaultMushroomModelPath,
	};

	static float FiberHarvestUniformScale( Model model, Vector3 worldPosition )
	{
		if ( !model.IsValid() )
			return 0.45f;

		var qx = (int)MathF.Round( worldPosition.x * 0.25f );
		var qy = (int)MathF.Round( worldPosition.y * 0.25f );
		var qz = (int)MathF.Round( worldPosition.z * 0.25f );
		var rnd = new Random( HashCode.Combine( (int)ThornsResourceKind.Fiber, qx, qy, qz ) );
		var bounds = model.Bounds;
		var meshHeight = MathF.Max( bounds.Size.z, 0.01f );
		const float targetInches = 22f;
		var uniform = (targetInches / meshHeight) * MathX.Lerp( 0.88f, 1.12f, (float)rnd.NextDouble() );
		return Math.Clamp( uniform, 0.12f, 2.5f );
	}

	/// <summary>
	/// Server-authoritative stone harvest nodes — project assets under <c>models/resources/</c> (not clutter props).
	/// </summary>
	static readonly string[] StoneHarvestVisualPaths =
	{
		"models/resources/stone_node_a.vmdl",
		"models/resources/stone_node_b.vmdl",
	};

	static readonly string[] MetalOreHarvestVisualPaths =
	{
		"models/resources/ore_node_a.vmdl",
		"models/resources/ore_node_b.vmdl",
	};

	/// <summary>Known-good clutter rocks when <c>models/resources/*</c> are not compiled yet.</summary>
	static readonly string[] MineralHarvestVisualFallbackPaths =
	{
		"models/clutter/rock1.vmdl",
		"models/clutter/rock2.vmdl",
	};

	static void ApplyMineralHarvestVisualPresentation(
		ModelRenderer vis,
		GameObject scaleHost,
		Model model,
		ThornsResourceKind kind,
		string modelAssetPath )
	{
		if ( !vis.IsValid() )
			return;

		if ( IsClutterRockHarvestPath( modelAssetPath ) )
		{
			ThornsModelMaterialUvScale.ApplyClutterRockMaterial( vis, scaleHost, model, modelAssetPath );
			vis.Tint = kind == ThornsResourceKind.MetalOre
				? new Color( 0.94f, 0.84f, 0.58f, 1f )
				: Color.White;
			return;
		}

		vis.Tint = kind == ThornsResourceKind.MetalOre
			? new Color( 0.94f, 0.84f, 0.58f, 1f )
			: Color.White;

		ThornsModelMaterialUvScale.ApplyForScaledModel( vis, scaleHost, model, modelAssetPath );
		ThornsModelMaterialUvScale.EnsureFixupOnHierarchy( scaleHost, includeChildren: false );
	}
}
