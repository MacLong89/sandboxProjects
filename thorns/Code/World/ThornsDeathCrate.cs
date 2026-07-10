namespace Sandbox;

/// <summary>
/// Server-authoritative recoverable crate (THORNS_EVERYTHING_DOCUMENT §death penalties).
/// </summary>
[Title( "Thorns — Death Crate" )]
[Category( "Thorns" )]
[Icon( "inventory" )]
public sealed class ThornsDeathCrate : Component
{
	public static readonly Dictionary<Guid, ThornsDeathCrate> ActiveById = new();

	/// <summary>Addon material for the dev box mesh (uses <c>crate_death.png</c> in the vmat).</summary>
	public const string DeathCrateMaterialPath = "materials/crate_death.vmat";

	static Model _cachedBoxModel;
	static Material _cachedMaterial;

	[Property] public float InteractionRadius { get; set; } = 120f;

	[Property] public float DespawnAfterSeconds { get; set; } = 600f;

	[Sync( SyncFlags.FromHost )] public string CrateIdSync { get; set; } = "";

	public Guid CrateId => SyncGuidParse( CrateIdSync );

	ThornsInventorySlot[] _grid;
	ThornsEquippedArmorPiece[] _armor;
	double _despawnAt;

	/// <summary>Host: spawn a networked crate. Pass copies of grid (38) and armor (3).</summary>
	public static ThornsDeathCrate SpawnHost( Scene scene, Vector3 worldPosition, ThornsInventorySlot[] gridCopy, ThornsEquippedArmorPiece[] armorCopy )
	{
		if ( !Networking.IsHost )
			return null;

		// Host spawn must succeed whenever invoked from Die() — inventory strip depends on it (THORNS doc §death penalties).
		_ = scene;

		gridCopy ??= new ThornsInventorySlot[ThornsInventory.TotalSlots];
		if ( gridCopy.Length != ThornsInventory.TotalSlots )
		{
			var full = new ThornsInventorySlot[ThornsInventory.TotalSlots];
			Array.Copy( gridCopy, full, Math.Min( gridCopy.Length, ThornsInventory.TotalSlots ) );
			gridCopy = full;
		}

		var go = new GameObject( true, "ThornsDeathCrate" );
		go.WorldPosition = worldPosition;
		go.Tags.Add( "thorns_death_crate" );
		go.LocalScale = new Vector3( 0.35f, 0.35f, 0.35f );

		var body = new GameObject( true, "CrateVisual" );
		body.SetParent( go );
		body.LocalPosition = Vector3.Zero;
		body.LocalScale = Vector3.One;
		var deathBoxModel = LoadDeathCrateBoxModel();
		var mr = body.Components.Create<ModelRenderer>();
		mr.Model = deathBoxModel;
		ApplyDeathCrateMaterialToVisual( go );

		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( go, deathBoxModel );

		var crate = go.Components.Create<ThornsDeathCrate>();
		crate.CrateIdSync = Guid.NewGuid().ToString( "D" );
		crate._grid = gridCopy;
		crate._armor = armorCopy ?? new ThornsEquippedArmorPiece[3];
		if ( crate._armor.Length != 3 )
		{
			var a = new ThornsEquippedArmorPiece[3];
			Array.Copy( crate._armor, a, Math.Min( 3, crate._armor.Length ) );
			crate._armor = a;
		}

		crate._despawnAt = Time.Now + crate.DespawnAfterSeconds;

		if ( Networking.IsActive )
		{
			if ( !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
				Log.Warning( "[Thorns] Death crate NetworkSpawn failed — joiners may not see this crate." );
		}

		ActiveById[crate.CrateId] = crate;

		Log.Info( $"[Thorns] Death crate spawned CrateId={crate.CrateId} pos={worldPosition}" );

		return crate;
	}

	protected override void OnStart()
	{
		// Network replicas often spawn without the host's initial MaterialOverride — match <see cref="ThornsLootCrate"/>.
		ApplyDeathCrateMaterialToVisual( GameObject );

		var id = CrateId;
		if ( id != Guid.Empty )
			ActiveById[id] = this;
	}

	static Material LoadDeathCrateMaterialOrFallback()
	{
		if ( _cachedMaterial.IsValid() )
			return _cachedMaterial;

		var m = Material.Load( DeathCrateMaterialPath );
		if ( m.IsValid() )
			return _cachedMaterial = m;

		var fb = Material.Load( "materials/default.vmat" );
		return fb.IsValid() ? fb : m;
	}

	static Model LoadDeathCrateBoxModel()
	{
		if ( _cachedBoxModel.IsValid() && !_cachedBoxModel.IsError )
			return _cachedBoxModel;

		return _cachedBoxModel = Model.Load( "models/dev/box.vmdl" );
	}

	static void ApplyDeathCrateMaterialToVisual( GameObject root )
	{
		if ( root is null || !root.IsValid() )
			return;

		var mat = LoadDeathCrateMaterialOrFallback();
		foreach ( var ch in root.Children )
		{
			if ( !ch.IsValid() || ch.Name != "CrateVisual" )
				continue;

			var mr = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( mr is null || !mr.IsValid() )
				continue;

			if ( mat.IsValid() )
				mr.MaterialOverride = mat;

			mr.Tint = Color.White;
			ThornsModelMaterialUvScale.ApplyForGameObject( mr, ch, sourceMaterial: mat );
			return;
		}
	}

	protected override void OnDestroy()
	{
		var id = CrateId;
		if ( id != Guid.Empty )
			ActiveById.Remove( id );
	}

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty);

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( Time.Now >= _despawnAt )
		{
			Log.Info( $"[Thorns] Death crate despawned (timer): id={CrateId}" );
			GameObject.Destroy();
			return;
		}

		if ( IsEmpty() )
		{
			Log.Info( $"[Thorns] Death crate emptied — despawn: id={CrateId}" );
			GameObject.Destroy();
		}
	}

	bool IsEmpty()
	{
		if ( _grid is null || _armor is null )
			return true;

		for ( var i = 0; i < _grid.Length; i++ )
		{
			if ( !_grid[i].IsEmpty )
				return false;
		}

		for ( var i = 0; i < _armor.Length; i++ )
		{
			if ( !_armor[i].IsEmpty )
				return false;
		}

		return true;
	}

	[Rpc.Host]
	public void RequestLootFirstAvailable( Guid crateId )
	{
		Log.Info( $"[Thorns] Death crate loot request (first available): crate={crateId}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateCallerAliveAndRange() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
		{
			Log.Warning( "[Thorns] Loot rejected: crate id mismatch" );
			return;
		}

		for ( var i = 0; i < _grid.Length; i++ )
		{
			if ( _grid[i].IsEmpty )
				continue;

			TryLootGridSlotInternal( i, _grid[i].Quantity );
			return;
		}

		for ( var a = 0; a < _armor.Length; a++ )
		{
			if ( _armor[a].IsEmpty )
				continue;

			TryLootArmorInternal( a );
			return;
		}

		Log.Warning( "[Thorns] Loot rejected: crate already empty" );
	}

	[Rpc.Host]
	public void RequestLootAll( Guid crateId )
	{
		Log.Info( $"[Thorns] Death crate loot all: crate={crateId}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateCallerAliveAndRange() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
		{
			Log.Warning( "[Thorns] Loot-all rejected: crate id mismatch" );
			return;
		}

		HostNotifyCallerOpenBuildSfx();

		for ( var iter = 0; iter < 512; iter++ )
		{
			if ( IsEmpty() )
				break;

			if ( !HostTryLootOneStackFromCrate() )
				break;
		}
	}

	/// <summary>Loot a single stack or one armor piece — false if inventory full / nothing moved.</summary>
	bool HostTryLootOneStackFromCrate()
	{
		for ( var i = 0; i < _grid.Length; i++ )
		{
			if ( _grid[i].IsEmpty )
				continue;

			var qBefore = _grid[i].Quantity;
			TryLootGridSlotInternal( i, _grid[i].Quantity );
			if ( _grid[i].IsEmpty || _grid[i].Quantity < qBefore )
				return true;
		}

		for ( var a = 0; a < _armor.Length; a++ )
		{
			if ( _armor[a].IsEmpty )
				continue;

			TryLootArmorInternal( a );
			if ( _armor[a].IsEmpty )
				return true;
		}

		return false;
	}

	[Rpc.Host]
	public void RequestLootInventorySlot( Guid crateId, int gridSlotIndex )
	{
		Log.Info( $"[Thorns] UI loot request (inventory slot): crate={crateId} grid={gridSlotIndex}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateLootCallerShared() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
		{
			Log.Warning( "[Thorns] Loot rejected: crate id mismatch" );
			return;
		}

		if ( gridSlotIndex < 0 || gridSlotIndex >= _grid.Length )
		{
			Log.Warning( "[Thorns] Loot rejected: bad grid index" );
			return;
		}

		if ( _grid[gridSlotIndex].IsEmpty )
		{
			Log.Warning( "[Thorns] Loot rejected: empty grid slot" );
			return;
		}

		TryLootGridSlotInternal( gridSlotIndex, _grid[gridSlotIndex].Quantity );
	}

	[Rpc.Host]
	public void RequestLootArmorPiece( Guid crateId, int armorIndex )
	{
		Log.Info( $"[Thorns] UI loot request (armor piece): crate={crateId} armor={armorIndex}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateLootCallerShared() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
		{
			Log.Warning( "[Thorns] Loot rejected: crate id mismatch" );
			return;
		}

		if ( armorIndex < 0 || armorIndex >= _armor.Length )
		{
			Log.Warning( "[Thorns] Loot rejected: bad armor index" );
			return;
		}

		TryLootArmorInternal( armorIndex );
	}

	/// <summary>Host-only: tab-separated lines for debug UI (G|slot|... / A|slot|...).</summary>
	public string HostFormatUiSnapshotText()
	{
		if ( !Networking.IsHost )
			return "";

		using var sw = new System.IO.StringWriter();
		for ( var i = 0; i < _grid.Length; i++ )
		{
			if ( _grid[i].IsEmpty )
				continue;
			var s = _grid[i];
			sw.Write( $"G|{i}|{s.ItemId}|{s.Quantity}|{(s.HasDurability ? s.Durability : 0f)}|{s.WeaponLoadedAmmo}|{s.WeaponInstanceId}|{s.WeaponRollPayload}|{s.ArmorRollPayload}\n" );
		}

		for ( var a = 0; a < _armor.Length; a++ )
		{
			if ( _armor[a].IsEmpty )
				continue;
			var p = _armor[a];
			sw.Write( $"A|{a}|{p.ItemId}|{p.DurabilityRemaining}|{p.ArmorRollPayload}\n" );
		}

		return sw.ToString();
	}

	/// <summary>Host validates caller for loot — alive and within <see cref="InteractionRadius"/>.</summary>
	public bool HostValidateCallerForLoot( Connection caller )
	{
		if ( !Networking.IsHost || caller is null )
			return false;

		var callerRoot = FindConnectionPawnRoot( GameObject.Scene, caller );
		if ( !callerRoot.IsValid() )
			return false;

		var health = callerRoot.Components.Get<ThornsHealth>( FindMode.EnabledInSelf );
		if ( !health.IsValid() || !health.IsAlive || health.IsDeadState )
			return false;

		var dist = (callerRoot.WorldPosition - GameObject.WorldPosition).Length;
		if ( dist > InteractionRadius )
			return false;

		return ThornsWorldUseAim.PawnLooksAtInteractableRoot( callerRoot, GameObject, InteractionRadius );
	}

	/// <summary>Debug UI snapshot only — alive pawn; no distance (crate may be far after respawn).</summary>
	public bool HostValidateCallerForUiSnapshot( Connection caller )
	{
		if ( !Networking.IsHost || caller is null )
			return false;

		var callerRoot = FindConnectionPawnRoot( GameObject.Scene, caller );
		if ( !callerRoot.IsValid() )
			return false;

		var health = callerRoot.Components.Get<ThornsHealth>( FindMode.EnabledInSelf );
		return health.IsValid() && health.IsAlive && !health.IsDeadState;
	}

	bool ValidateLootCallerShared()
	{
		if ( Rpc.Caller is null )
		{
			Log.Warning( "[Thorns] Loot rejected: no RPC caller" );
			return false;
		}

		if ( !HostValidateCallerForLoot( Rpc.Caller ) )
		{
			Log.Warning( "[Thorns] Loot rejected: alive/range check failed" );
			return false;
		}

		return true;
	}

	void TryLootGridSlotInternal( int slotIndex, int quantity )
	{
		ref var src = ref _grid[slotIndex];
		if ( src.IsEmpty )
			return;

		var take = Math.Min( quantity, src.Quantity );
		var looterInv = FindCallerInventory();
		if ( !looterInv.IsValid() )
		{
			Log.Warning( "[Thorns] Loot rejected: no inventory" );
			return;
		}

		var chunk = CloneSlotPartial( src, take );
		if ( !looterInv.ServerTryImportLootStack( chunk, out var reason ) )
		{
			Log.Warning( $"[Thorns] Loot rejected: {reason}" );
			return;
		}

		src.Quantity -= take;
		if ( src.Quantity <= 0 )
			src = ThornsInventorySlot.Empty;

		Log.Info( $"[Thorns] Loot request accepted: crate={CrateId} gridSlot={slotIndex} qty={take}" );
		ThornsGameShell.HostPushLootPickupToast( looterInv, chunk.ItemId, take, "Loot secured · backpack updated." );
	}

	void TryLootArmorInternal( int armorIndex )
	{
		var piece = _armor[armorIndex];
		if ( piece.IsEmpty )
			return;

		var looterInv = FindCallerInventory();
		if ( !looterInv.IsValid() )
			return;

		var slot = new ThornsInventorySlot
		{
			ItemId = piece.ItemId,
			Quantity = 1,
			HasDurability = true,
			Durability = piece.DurabilityRemaining,
			ArmorRollPayload = piece.ArmorRollPayload ?? ""
		};

		if ( !looterInv.ServerTryImportLootStack( slot, out var reason ) )
		{
			Log.Warning( $"[Thorns] Armor loot rejected: {reason}" );
			return;
		}

		_armor[armorIndex] = default;
		Log.Info( $"[Thorns] Loot request accepted (armor): crate={CrateId} armorIdx={armorIndex}" );
		ThornsGameShell.HostPushLootPickupToast( looterInv, slot.ItemId, 1, "Armor recovered from crate." );
	}

	bool ValidateCallerAliveAndRange()
	{
		if ( Rpc.Caller is null )
		{
			Log.Warning( "[Thorns] Loot rejected: no RPC caller" );
			return false;
		}

		if ( !HostValidateCallerForLoot( Rpc.Caller ) )
		{
			Log.Warning( "[Thorns] Loot rejected: dead players cannot loot or out of range" );
			return false;
		}

		return true;
	}

	void HostNotifyCallerOpenBuildSfx()
	{
		var inv = FindCallerInventory();
		if ( inv.IsValid() )
			inv.HostNotifyOpenBuildSfx( GameObject.WorldPosition );
	}

	ThornsInventory FindCallerInventory()
	{
		if ( Rpc.Caller is null )
			return default;

		var root = FindConnectionPawnRoot( GameObject.Scene, Rpc.Caller );
		if ( !root.IsValid() )
			return default;

		return root.Components.Get<ThornsInventory>( FindMode.EnabledInSelf );
	}

	static GameObject FindConnectionPawnRoot( Scene scene, Connection c )
	{
		_ = scene;
		return ThornsPawnConnectionIndex.TryGetPawnGameObject( c, out var root ) ? root : default;
	}

	static ThornsInventorySlot CloneSlotPartial( ThornsInventorySlot src, int qty )
	{
		var q = Math.Min( qty, src.Quantity );
		return new ThornsInventorySlot
		{
			ItemId = src.ItemId,
			Quantity = q,
			HasDurability = src.HasDurability,
			Durability = src.Durability,
			WeaponInstanceId = src.WeaponInstanceId,
			WeaponLoadedAmmo = src.WeaponLoadedAmmo,
			WeaponRollPayload = src.WeaponRollPayload ?? "",
			ArmorRollPayload = src.ArmorRollPayload ?? ""
		};
	}
}
