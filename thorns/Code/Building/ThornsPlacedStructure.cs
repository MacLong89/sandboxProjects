namespace Sandbox;

/// <summary>Server-spawned world structure (ownership + health for future damage/persistence).</summary>
[Title( "Thorns — Placed Structure" )]
[Category( "Thorns" )]
[Icon( "foundation" )]
[Order( 40 )]
public sealed class ThornsPlacedStructure : Component
{
	public static readonly Dictionary<Guid, ThornsPlacedStructure> ActiveByInstanceId = new();

	[Sync( SyncFlags.FromHost )] public string InstanceIdSync { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public string OwnerConnectionIdSync { get; set; } = "";

	/// <summary>Steam/local stable key — survives server restarts (see <see cref="ThornsPersistenceIdentity"/>).</summary>
	[Sync( SyncFlags.FromHost )] public string OwnerAccountKeySync { get; set; } = "";

	public Guid InstanceId => SyncGuidParse( InstanceIdSync );

	public Guid OwnerConnectionId => SyncGuidParse( OwnerConnectionIdSync );

	[Sync( SyncFlags.FromHost )] public string StructureDefId { get; set; }

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; }

	[Sync( SyncFlags.FromHost )] public float MaxHealthSync { get; set; }

	[Sync( SyncFlags.FromHost )] public int UpgradeTierPlaceholder { get; set; }

	public int MaterialTier => Math.Clamp( UpgradeTierPlaceholder, 0, 2 );

	public static ThornsPlacedStructure SpawnHost(
		Scene scene,
		Guid ownerConnectionId,
		string ownerAccountKey,
		string structureDefId,
		Vector3 worldPosition,
		Rotation worldRotation,
		out string failReason )
	{
		failReason = "";
		if ( !Networking.IsHost )
		{
			failReason = "not_host";
			return null;
		}

		if ( !ThornsBuildingDefinitions.TryGet( structureDefId, out var def ) )
		{
			failReason = "unknown_def";
			return null;
		}

		_ = scene;

		var spawnPos = worldPosition;
		var spawnRot = worldRotation;
		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( structureDefId, ref spawnPos, spawnRot );

		var go = new GameObject( true, $"ThornsStructure_{structureDefId}" );
		go.WorldPosition = spawnPos;
		go.WorldRotation = spawnRot;
		go.Tags.Add( "thorns_structure" );
		HostSetupStructureVisual( go, structureDefId );

		var ps = go.Components.Create<ThornsPlacedStructure>();
		ps.InstanceIdSync = Guid.NewGuid().ToString( "D" );
		ps.OwnerConnectionIdSync = ownerConnectionId.ToString( "D" );
		ps.OwnerAccountKeySync = string.IsNullOrEmpty( ownerAccountKey )
			? ThornsPersistenceIdentity.TryGetStableAccountKeyForConnection( ownerConnectionId, out var ak )
				? ak
				: ""
			: ownerAccountKey;
		ps.StructureDefId = structureDefId;
		ps.UpgradeTierPlaceholder = def.UpgradeTierPlaceholder;
		ThornsBuildingDurability.HostApplyMaxHealthFromDurability( ps, refillToFull: true );
		ps.HostApplyVisualTier();

		if ( structureDefId == "storage_chest" )
			go.Components.Create<ThornsStorageChest>();
		else if ( ThornsFurnitureLootPolicy.ShouldSpawnPlayerStorageContainer( structureDefId ) )
		{
			var fc = go.Components.Create<ThornsFurnitureContainer>();
			fc.HostInitializePlayerStorage( ps );
		}
		else if ( string.Equals( structureDefId, "campfire", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsCampfire>();
		else if ( string.Equals( structureDefId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsWorkbench>();
		else if ( string.Equals( structureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsBed>();

		go.NetworkMode = NetworkMode.Object;
		HostNetworkSpawnWorldStructure( go );

		ActiveByInstanceId[ps.InstanceId] = ps;
		ThornsWorldPersistence.HostNotifyWorldStructureSpawned();

		if ( string.Equals( structureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			ThornsPlayerDoor.HostEnsureOnDoorframe( ps );

		Log.Info( $"[Thorns] Structure spawned id={structureDefId} instance={ps.InstanceId} owner={ownerConnectionId} pos={worldPosition} hp={ps.CurrentHealth}" );

		return ps;
	}

	/// <summary>Host-only restore from <see cref="ThornsWorldPersistence"/> — preserves <paramref name="instanceId"/> for chest grids.</summary>
	public static ThornsPlacedStructure SpawnHostFromSave(
		Scene scene,
		Guid instanceId,
		string ownerAccountKey,
		Guid ownerConnectionId,
		string structureDefId,
		Vector3 worldPosition,
		Rotation worldRotation,
		float currentHealth,
		int upgradeTier,
		out string failReason )
	{
		failReason = "";
		if ( !Networking.IsHost )
		{
			failReason = "not_host";
			return null;
		}

		if ( instanceId == Guid.Empty )
		{
			failReason = "bad_instance";
			return null;
		}

		if ( !ThornsBuildingDefinitions.TryGet( structureDefId, out var def ) )
		{
			failReason = "unknown_def";
			return null;
		}

		var spawnPos = worldPosition;
		var spawnRot = worldRotation;
		ThornsPlaceableFurniturePresentation.AlignPlacementPivotOnSurface( structureDefId, ref spawnPos, spawnRot );

		var go = new GameObject( true, $"ThornsStructure_{structureDefId}" );
		go.WorldPosition = spawnPos;
		go.WorldRotation = spawnRot;
		go.Tags.Add( "thorns_structure" );
		HostSetupStructureVisual( go, structureDefId );

		var ps = go.Components.Create<ThornsPlacedStructure>();
		ps.InstanceIdSync = instanceId.ToString( "D" );
		ps.OwnerConnectionIdSync = ownerConnectionId == Guid.Empty ? "" : ownerConnectionId.ToString( "D" );
		ps.OwnerAccountKeySync = ownerAccountKey ?? "";
		ps.StructureDefId = structureDefId;
		ps.UpgradeTierPlaceholder = Math.Clamp( upgradeTier, 0, 2 );
		ThornsBuildingDurability.HostApplyMaxHealthFromDurability( ps, refillToFull: false );
		if ( currentHealth > 0.01f )
			ps.CurrentHealth = Math.Min( currentHealth, ps.MaxHealthSync );
		else
			ps.CurrentHealth = ps.MaxHealthSync;
		ps.HostApplyVisualTier();

		if ( structureDefId == "storage_chest" )
			go.Components.Create<ThornsStorageChest>();
		else if ( ThornsFurnitureLootPolicy.ShouldSpawnPlayerStorageContainer( structureDefId ) )
		{
			var fc = go.Components.Create<ThornsFurnitureContainer>();
			fc.HostInitializePlayerStorage( ps );
		}
		else if ( string.Equals( structureDefId, "campfire", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsCampfire>();
		else if ( string.Equals( structureDefId, "workbench", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsWorkbench>();
		else if ( string.Equals( structureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			go.Components.Create<ThornsBed>();

		go.NetworkMode = NetworkMode.Object;
		HostNetworkSpawnWorldStructure( go );

		ActiveByInstanceId[ps.InstanceId] = ps;
		ThornsWorldPersistence.HostNotifyWorldStructureSpawned();

		if ( string.Equals( structureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
			ThornsPlayerDoor.HostEnsureOnDoorframe( ps );

		Log.Info(
			$"[Thorns] Structure restored id={structureDefId} instance={ps.InstanceId} account={(ownerAccountKey ?? "")} hp={ps.CurrentHealth}" );

		return ps;
	}

	/// <summary>
	/// Host-only collision setup: procedural <b>building</b> pieces use a static <see cref="ModelCollider"/> on the structure root;
	/// portable kits (chest / campfire / workbench) use a static <see cref="BoxCollider"/> hull from the procedural model bounds.
	/// Non-procedural defs use <see cref="ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics"/>.
	/// </summary>
	static void HostEnsureStructurePhysics( GameObject structureRoot, Model collisionModel, string structureDefId )
	{
		if ( ThornsBuildingVisuals.UsesProceduralStructureModel( structureDefId ) )
		{
			// Kit placeables: cheap box hull from procedural bounds — avoids triangle mesh mismatch vs traces / movement.
			if ( HostUseBoxHullForProceduralKit( structureDefId ) )
			{
				var existingModelCollider = structureRoot.Components.Get<ModelCollider>();
				if ( existingModelCollider.IsValid() )
					existingModelCollider.Destroy();
				ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( structureRoot, collisionModel );
				if ( ThornsCollisionAudit.SpawnValidationEnabled )
					ThornsCollisionAudit.TrySpawnAudit( structureRoot, $"placed_structure:{structureDefId}" );
				return;
			}

			ThornsAnchoredWorldPhysics.EnsureWorldSolidTags( structureRoot );
			var bc = structureRoot.Components.Get<BoxCollider>();
			if ( bc.IsValid() )
				bc.Destroy();
			var procModelCollider = structureRoot.Components.GetOrCreate<ModelCollider>();
			procModelCollider.Model = collisionModel;
			procModelCollider.IsTrigger = false;
			procModelCollider.Static = true;
			procModelCollider.Enabled = true;
			if ( ThornsCollisionAudit.SpawnValidationEnabled )
				ThornsCollisionAudit.TrySpawnAudit( structureRoot, $"placed_structure:{structureDefId}_proc_mesh" );
			return;
		}

		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( structureRoot, collisionModel );
		if ( ThornsCollisionAudit.SpawnValidationEnabled )
			ThornsCollisionAudit.TrySpawnAudit( structureRoot, $"placed_structure:{structureDefId}" );
	}

	static bool HostUseBoxHullForProceduralKit( string structureDefId ) =>
		ThornsBuildingDefinitions.IsPortableKitPlaceableId( structureDefId );

	static void TryApplyLegacyKitMeshUvScale( ModelRenderer mr, string structureDefId )
	{
		if ( mr is null || !mr.IsValid() )
			return;

		if ( ThornsPlaceableFurnitureCatalog.TryGet( structureDefId, out var entry ) )
			ThornsBuildingVisuals.ApplyPlaceableMeshUvScale( mr, entry.ModelPath );
	}

	static Model HostCreateStructureVisual( GameObject go, string structureDefId, out ModelRenderer mr )
	{
		var structureModel = ThornsBuildingVisuals.StructureModel( structureDefId, 0 );
		if ( ThornsBuildingVisuals.UsesOffsetKitVisualChild( structureDefId ) )
		{
			mr = string.Equals( structureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase )
				? ThornsBuildingVisuals.GetOrCreateStorageChestOffsetModelRenderer( go )
				: ThornsBuildingVisuals.GetOrCreateBedOffsetModelRenderer( go );
			if ( string.Equals( structureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
				ThornsBuildingVisuals.ApplyStorageChestVisual( mr );
			else
				ThornsBuildingVisuals.ApplyBedVisual( mr );
		}
		else
		{
			mr = go.Components.Create<ModelRenderer>();
			mr.Model = structureModel;
		}

		return structureModel;
	}

	static void HostSetupStructureVisual( GameObject go, string structureDefId )
	{
		if ( ThornsPlaceableFurniturePresentation.UsesCatalogPresentation( structureDefId )
		     && ThornsPlaceableFurniturePresentation.TryGetEntry( structureDefId, out var furnitureEntry ) )
		{
			ThornsPlaceableFurniturePresentation.Apply( go, in furnitureEntry );
			var mr = go.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( mr.IsValid() )
				mr.Tint = HostTintForStructure( structureDefId );
			return;
		}

		go.LocalScale = ThornsBuildingVisuals.StructureLocalScale( structureDefId );
		var structureModel = HostCreateStructureVisual( go, structureDefId, out var legacyMr );
		legacyMr.Tint = HostTintForStructure( structureDefId );
		TryApplyLegacyKitMeshUvScale( legacyMr, structureDefId );
		HostEnsureStructurePhysics( go, structureModel, structureDefId );
	}

	/// <summary>
	/// Structures are spawned from host RPC context; plain <see cref="GameObject.NetworkSpawn()"/> can assign
	/// ownership to <see cref="Rpc.Caller"/> so pieces disappear when the builder disconnects.
	/// Spawn as <see cref="Connection.Host"/> with <see cref="NetworkOrphaned.Host"/> so world objects persist.
	/// </summary>
	static void HostNetworkSpawnWorldStructure( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		if ( Networking.IsActive )
		{
			ThornsNetworkReplication.SetSubtreeNetworkModeObject( go );
			var opts = new NetworkSpawnOptions
			{
				Owner = Connection.Host,
				OrphanedMode = NetworkOrphaned.Host
			};

			if ( !go.NetworkSpawn( opts ) )
				Log.Warning( "[Thorns] Structure NetworkSpawn failed — replication/ownership may be wrong." );
		}
		else
		{
			go.NetworkSpawn();
		}
	}

	/// <summary>Vertex tint — neutral so <c>materials/wood.png</c> albedo isn’t multiplied down by brown overlays.</summary>
	static Color HostTintForStructure( string _ ) => Color.White;

	protected override void OnStart()
	{
		if ( Game.IsPlaying )
			ApplySyncedStructureVisual();
	}

	public void HostApplyVisualTier()
	{
		ApplySyncedStructureVisual();
	}

	/// <summary>Rebuild world mesh from synced def + tier so joiners resolve <see cref="Material"/> / procedural <see cref="Model"/> locally (same idea as <see cref="ThornsStorageChest.OnStart"/>).</summary>
	void ApplySyncedStructureVisual()
	{
		if ( !GameObject.IsValid() )
			return;

		if ( ThornsPlaceableFurniturePresentation.UsesCatalogPresentation( StructureDefId )
		     && ThornsPlaceableFurniturePresentation.TryGetEntry( StructureDefId, out var furnitureEntry ) )
		{
			ThornsPlaceableFurniturePresentation.Apply( GameObject, in furnitureEntry );
			var kitMr = GameObject.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( kitMr.IsValid() )
				kitMr.Tint = HostTintForStructure( StructureDefId );
			return;
		}

		var mr = ThornsBuildingVisuals.UsesOffsetKitVisualChild( StructureDefId )
			? string.Equals( StructureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase )
				? ThornsBuildingVisuals.TryResolveStorageChestModelRenderer( GameObject )
				: ThornsBuildingVisuals.TryResolveBedModelRenderer( GameObject )
			: GameObject.Components.Get<ModelRenderer>( FindMode.EverythingInSelf );
		if ( !mr.IsValid() )
			return;

		if ( string.Equals( StructureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.EnsureStorageChestVisualChildLocalTransform( GameObject );
		else if ( string.Equals( StructureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.EnsureBedVisualChildLocalTransform( GameObject );

		var model = ThornsBuildingVisuals.StructureModel( StructureDefId, MaterialTier );
		if ( string.Equals( StructureDefId, "storage_chest", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyStorageChestVisual( mr );
		else if ( string.Equals( StructureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			ThornsBuildingVisuals.ApplyBedVisual( mr );
		else
			mr.Model = model;
		mr.Tint = HostTintForStructure( StructureDefId );

		if ( string.Equals( StructureDefId, "wood_doorframe", StringComparison.OrdinalIgnoreCase ) )
		{
			var door = Components.Get<ThornsPlayerDoor>();
			door?.RefreshPanelTier( MaterialTier );
		}
	}

	protected override void OnDestroy()
	{
		var id = InstanceId;
		if ( id != Guid.Empty )
		{
			ActiveByInstanceId.Remove( id );
			ThornsBuildingSocketLedger.HostClearInstance( id );
		}
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);
}
