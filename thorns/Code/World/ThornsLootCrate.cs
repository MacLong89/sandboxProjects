using System;

namespace Sandbox;

/// <summary>
/// Procedural loot crate — host RNG fills a compact grid (THORNS loot expansion).
/// Loot rules mirror <see cref="ThornsDeathCrate"/> RPC validation (range, alive, inventory fit).
/// </summary>
[Title( "Thorns — Loot Crate" )]
[Category( "Thorns/World" )]
[Icon( "inventory_2" )]
public sealed class ThornsLootCrate : Component
{
	public const int LootGridSlots = 16;

	/// <summary>Root <see cref="GameObject.LocalScale"/> for standard world loot crates (<c>models/dev/box.vmdl</c>, non-airdrop).</summary>
	public const float ProceduralCrateUniformScale = 0.38f;

	/// <summary>Uniform edge length in world units — matches <c>DevReferenceSize</c> × <see cref="ProceduralCrateUniformScale"/>.</summary>
	public static float ProceduralCrateWorldExtent =>
		ThornsBuildingModule.DevReferenceSize * ProceduralCrateUniformScale;

	/// <summary>
	/// Procedural buildings place the crate root on the floor plane — <c>models/dev/box.vmdl</c> is center-pivoted, so this is half the scaled Z extent.
	/// </summary>
	public static float ProceduralFloorTopToCrateRootZ =>
		ThornsBuildingModule.DevReferenceSize * 0.5f * ProceduralCrateUniformScale;

	public static readonly Dictionary<Guid, ThornsLootCrate> ActiveById = new();

	/// <summary>Delay before a world loot crate that regenerates comes back (same anchor; random kind + loot roll).</summary>
	public const float WorldLootRegenSeconds = 300f;

	[Property] public float InteractionRadius { get; set; } = 118f;

	/// <summary>
	/// Procedural building crates (and other authored world loot): when fully emptied, hide until <see cref="WorldLootRegenSeconds"/> (5 min), then reroll crate kind + loot grid.
	/// Airdrops / player drops use different rules — see <see cref="OnFixedUpdate"/>.
	/// </summary>
	[Property] public bool WorldRegeneratesWhenEmpty { get; set; }

	[Sync( SyncFlags.FromHost )] public string CrateIdSync { get; set; } = "";

	public Guid CrateId => SyncGuidParse( CrateIdSync );

	[Sync( SyncFlags.FromHost )] public ThornsLootCrateKind CrateKindSync { get; set; }

	/// <summary>Hide mesh/collision while emptied and waiting for timed regen (replicated so proxies match host).</summary>
	[Sync( SyncFlags.FromHost )] public bool SuppressCrateVisualForRegen { get; set; }

	ThornsInventorySlot[] _loot;

	bool _spawnedAsPlayerDrop;

	bool _worldRegenScheduled;
	double _worldRegenAt;
	bool _suppressVisualApplied;

	/// <summary>Host: Scavenger — at most one bonus roll per fill of this crate.</summary>
	bool _hostScavengerProcAttempted;

	/// <summary>Procedural building interior crate: <see cref="ThornsBuildingMaterialTier"/> for legacy regen; <c>-1</c> = global weights.</summary>
	int _interiorProcBuildingMaterialTier = -1;

	/// <summary>Proc building archetype for regen kind rolls; <c>-1</c> = not tied to a building type.</summary>
	int _interiorProcBuildingTypeOrdinal = -1;

	protected override void OnDestroy()
	{
		var id = CrateId;
		if ( id != Guid.Empty )
			ActiveById.Remove( id );
	}

	protected override void OnStart()
	{
		// Network replicas often spawn without the host's initial MaterialOverride — reapply so clients aren't pink.
		TryApplyCrateMaterialOverride();

		// Host assigns ActiveById at spawn; proxies must register too — ThornsDeathCrateInteractor scans this map for Use (E).
		var id = CrateId;
		if ( id != Guid.Empty )
			ActiveById[id] = this;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		ApplySuppressVisualFromSync();
	}

	void ApplySuppressVisualFromSync()
	{
		var hide = SuppressCrateVisualForRegen;
		if ( hide == _suppressVisualApplied )
			return;

		_suppressVisualApplied = hide;
		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				mr.Enabled = !hide;
		}

		foreach ( var bc in GameObject.Components.GetAll<BoxCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( bc.IsValid() )
				bc.Enabled = !hide;
		}
	}

	static void ApplyCrateMaterialToVisualChild( GameObject root, ThornsLootCrateKind kind )
	{
		if ( root is null || !root.IsValid() )
			return;
		foreach ( var ch in root.Children )
		{
			if ( !ch.IsValid() || ch.Name != "LootCrateVisual" )
				continue;
			var mr = ch.Components.Get<ModelRenderer>( FindMode.EnabledInSelf );
			if ( mr is null || !mr.IsValid() )
				continue;
			var mat = MaterialForCrateVisual( kind );
			if ( mat.IsValid() )
				mr.MaterialOverride = mat;

			ThornsModelMaterialUvScale.ApplyForGameObject( mr, ch, sourceMaterial: mat );
			return;
		}
	}

	void TryApplyCrateMaterialOverride() =>
		ApplyCrateMaterialToVisualChild( GameObject, CrateKindSync );

	static Guid SyncGuidParse( string s ) =>
		string.IsNullOrWhiteSpace( s ) ? Guid.Empty : (Guid.TryParse( s, out var g ) ? g : Guid.Empty );

	/// <summary>One slot per <see cref="ThornsLootCrateKind"/> ordinal (0–9).</summary>
	static Material[] _matByKind;

	/// <summary>Bump when loot-crate material paths change so cached statics refresh after hot reload.</summary>
	const int LootCrateMaterialRevision = 8;
	static int _lootMatAppliedRevision = -1;

	static void InvalidateLootMaterialCacheIfStale()
	{
		if ( _lootMatAppliedRevision == LootCrateMaterialRevision )
			return;
		_lootMatAppliedRevision = LootCrateMaterialRevision;
		_matByKind = null;
	}

	static Material TryLoadFirstValid( params string[] paths )
	{
		foreach ( var p in paths )
		{
			var m = Material.Load( p );
			if ( m.IsValid() )
				return m;
		}

		return default;
	}

	static string PrimaryMaterialPathForKind( ThornsLootCrateKind kind ) =>
		kind switch
		{
			ThornsLootCrateKind.Medical => "materials/crate_medical.vmat",
			ThornsLootCrateKind.Weapons => "materials/crate_weapons.vmat",
			ThornsLootCrateKind.Armor => "materials/crate_armor.vmat",
			ThornsLootCrateKind.Provisions => "materials/crate_provisions.vmat",
			ThornsLootCrateKind.MilitaryMixed => "materials/crate_military.vmat",
			ThornsLootCrateKind.IndustrialScrap => "materials/crate_industrial.vmat",
			ThornsLootCrateKind.AirdropPremium => "materials/airdrop.vmat",
			ThornsLootCrateKind.SalvageComponents => "materials/crate_salvagecomponents.vmat",
			ThornsLootCrateKind.HunterCache => "materials/crate_huntercache.vmat",
			ThornsLootCrateKind.Ammo => "materials/crate_military.vmat",
			_ => "materials/crate.vmat"
		};

	static Model _cachedCrateBoxModel;

	static Model CachedCrateBoxModel()
	{
		if ( _cachedCrateBoxModel.IsValid() && !_cachedCrateBoxModel.IsError )
			return _cachedCrateBoxModel;

		_cachedCrateBoxModel = Model.Load( "models/dev/box.vmdl" );
		return _cachedCrateBoxModel;
	}

	static Material MaterialForCrateVisual( ThornsLootCrateKind kind )
	{
		InvalidateLootMaterialCacheIfStale();

		var ord = (int)kind;
		if ( ord < 0 || ord > 9 )
			ord = 0;

		_matByKind ??= new Material[10];

		if ( _matByKind[ord].IsValid() )
			return _matByKind[ord];

		var primary = PrimaryMaterialPathForKind( kind );
		var mat = TryLoadFirstValid(
			primary,
			"materials/crate.vmat",
			"materials/stone.vmat",
			"materials/metal.vmat" );
		if ( !mat.IsValid() )
		{
			Log.Warning( $"[Thorns] Crate material failed for {kind} (tried {primary} + fallbacks)." );
			mat = Material.Load( "materials/default.vmat" );
		}

		_matByKind[ord] = mat;
		return mat;
	}

	public static ThornsLootCrate SpawnHost(
		Scene scene,
		Vector3 worldPosition,
		ThornsLootCrateKind kind,
		Random rng,
		bool worldRegeneratesWhenEmpty = false,
		int interiorProcBuildingMaterialTier = -1,
		ThornsProcBuildingType? interiorProcBuildingType = null ) =>
		SpawnHostWithGrid(
			scene,
			worldPosition,
			kind,
			ThornsLootGenerator.GenerateLootGrid( kind, rng ),
			inventoryDrop: false,
			worldRegeneratesWhenEmpty,
			interiorProcBuildingMaterialTier,
			interiorProcBuildingType );

	/// <summary>Host: single dropped stack — same E interaction as procedural crates (<see cref="ThornsDeathCrateInteractor"/>).</summary>
	public static ThornsLootCrate SpawnHostPlayerDrop( Scene scene, Vector3 worldPosition, ThornsInventorySlot stack )
	{
		if ( stack.IsEmpty )
			return default;

		var grid = new ThornsInventorySlot[LootGridSlots];
		grid[0] = CloneSlotFull( stack );
		return SpawnHostWithGrid( scene, worldPosition, ThornsLootCrateKind.SalvageComponents, grid, inventoryDrop: true );
	}

	static ThornsInventorySlot CloneSlotFull( ThornsInventorySlot src ) =>
		new ThornsInventorySlot
		{
			ItemId = src.ItemId ?? "",
			Quantity = src.Quantity,
			HasDurability = src.HasDurability,
			Durability = src.Durability,
			WeaponInstanceId = src.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = src.WeaponLoadedAmmo,
			WeaponRollPayload = src.WeaponRollPayload ?? "",
			ArmorRollPayload = src.ArmorRollPayload ?? ""
		};

	/// <param name="inventoryDrop">Player dropped from inventory: half default scale, no physics on the root.</param>
	/// <param name="interiorProcBuildingMaterialTier">Wood/stone/metal tier for timed regen kind rolls; <c>-1</c> = not tied to a procedural building.</param>
	public static ThornsLootCrate SpawnHostWithGrid(
		Scene scene,
		Vector3 worldPosition,
		ThornsLootCrateKind kind,
		ThornsInventorySlot[] grid,
		bool inventoryDrop = false,
		bool worldRegeneratesWhenEmpty = false,
		int interiorProcBuildingMaterialTier = -1,
		ThornsProcBuildingType? interiorProcBuildingType = null )
	{
		_ = scene;
		if ( Networking.IsActive && !Networking.IsHost )
			return default;

		if ( grid is null || grid.Length != LootGridSlots )
		{
			var full = new ThornsInventorySlot[LootGridSlots];
			if ( grid is not null )
				Array.Copy( grid, full, Math.Min( grid.Length, LootGridSlots ) );

			grid = full;
		}

		var go = new GameObject( true, $"ThornsLootCrate_{kind}" );
		go.WorldPosition = worldPosition;
		go.Tags.Add( "thorns_loot_crate" );
		var baseScale = kind == ThornsLootCrateKind.AirdropPremium
			? new Vector3( 0.46f, 0.46f, 0.46f )
			: new Vector3( ProceduralCrateUniformScale, ProceduralCrateUniformScale, ProceduralCrateUniformScale );
		go.LocalScale = inventoryDrop ? baseScale * 0.5f : baseScale;

		var body = new GameObject( true, "LootCrateVisual" );
		body.SetParent( go );
		body.LocalPosition = Vector3.Zero;
		body.LocalScale = Vector3.One;
		var crateBoxModel = CachedCrateBoxModel();
		var mr = body.Components.Create<ModelRenderer>();
		mr.Model = crateBoxModel;
		mr.MaterialOverride = MaterialForCrateVisual( kind );
		mr.Tint = Color.White;
		ThornsModelMaterialUvScale.ApplyForGameObject( mr, body, sourceMaterial: mr.MaterialOverride );
		// Proxies may construct later — component also reapplies in <see cref="OnStart"/>.

		// Collider on the networked root — child colliders + runtime spawn historically missed collision rules (s&box issue #2936).
		if ( !inventoryDrop )
			ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( go, crateBoxModel );

		var crate = go.Components.Create<ThornsLootCrate>();
		if ( inventoryDrop )
			crate.InteractionRadius = 59f;

		crate._spawnedAsPlayerDrop = inventoryDrop;
		crate.WorldRegeneratesWhenEmpty = worldRegeneratesWhenEmpty;
		crate._interiorProcBuildingMaterialTier = interiorProcBuildingMaterialTier;
		crate._interiorProcBuildingTypeOrdinal = interiorProcBuildingType.HasValue
			? (int)interiorProcBuildingType.Value
			: -1;

		crate.CrateIdSync = Guid.NewGuid().ToString( "D" );
		crate.CrateKindSync = kind;
		crate._loot = grid;

		if ( Networking.IsActive
		     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( go ) )
			Log.Warning( "[Thorns] Loot crate NetworkSpawn failed — joiners may not see this crate." );

		ActiveById[crate.CrateId] = crate;
		crate._hostScavengerProcAttempted = false;

		return crate;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( _spawnedAsPlayerDrop && IsEmpty() )
		{
			GameObject.Destroy();
			return;
		}

		if ( WorldRegeneratesWhenEmpty )
		{
			if ( IsEmpty() )
			{
				if ( !_worldRegenScheduled )
				{
					_worldRegenScheduled = true;
					_worldRegenAt = Time.Now + WorldLootRegenSeconds;
					SuppressCrateVisualForRegen = true;
				}
				else if ( Time.Now >= _worldRegenAt )
				{
					HostRerollKindAndLootGrid();
					SuppressCrateVisualForRegen = false;
					_worldRegenScheduled = false;
				}
			}
			else
			{
				_worldRegenScheduled = false;
				SuppressCrateVisualForRegen = false;
			}

			return;
		}

		if ( IsEmpty() )
			GameObject.Destroy();
	}

	/// <summary>Host: independent rolls for crate kind (building weights when applicable) and slot contents.</summary>
	void HostRerollKindAndLootGrid()
	{
		var rng = Random.Shared;
		var kind = RollWorldLootCrateKind( rng );
		CrateKindSync = kind;
		_loot = ThornsLootGenerator.GenerateLootGrid( kind, rng );
		_hostScavengerProcAttempted = false;
		TryApplyCrateMaterialOverride();
	}

	ThornsLootCrateKind RollWorldLootCrateKind( Random rng )
	{
		if ( _interiorProcBuildingTypeOrdinal >= 0 )
			return ThornsLootGenerator.PickKindForProcBuilding( (ThornsProcBuildingType)_interiorProcBuildingTypeOrdinal, rng );
		if ( _interiorProcBuildingMaterialTier >= 0 )
			return ThornsLootGenerator.PickRandomKindForProceduralBuilding( rng, _interiorProcBuildingMaterialTier );
		return ThornsLootGenerator.PickRandomKind( rng );
	}

	bool IsEmpty()
	{
		if ( _loot is null )
			return true;

		foreach ( var s in _loot )
		{
			if ( !s.IsEmpty )
				return false;
		}

		return true;
	}

	/// <summary>Host: Scavenger skill — first loot interaction may inject one bonus stack into an empty grid slot.</summary>
	void HostTryInjectScavengerBonusLoot( Connection caller )
	{
		if ( !Networking.IsHost || _hostScavengerProcAttempted || _loot is null || IsEmpty() )
			return;

		_hostScavengerProcAttempted = true;

		var root = FindConnectionPawnRoot( GameObject.Scene, caller );
		if ( !root.IsValid() )
			return;

		var ups = root.Components.Get<ThornsPlayerUpgrades>();
		if ( !ups.IsValid() || ups.ScavengerRank <= 0 )
			return;

		var chance = Math.Min(
			0.58f,
			ups.ScavengerRank * ThornsUpgradeBalance.ScavengerExtraLootChancePerRank );
		if ( Random.Shared.NextDouble() >= chance )
			return;

		if ( !ThornsLootGenerator.TryRollBonusStackForCrateKind( CrateKindSync, Random.Shared, out var bonus ) )
			return;

		for ( var i = 0; i < _loot.Length; i++ )
		{
			if ( !_loot[i].IsEmpty )
				continue;
			_loot[i] = bonus;
			return;
		}
	}

	[Rpc.Host]
	public void RequestLootFirstAvailable( Guid crateId )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateCallerAliveAndRange() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
			return;

		if ( _loot is null )
			return;

		if ( Rpc.Caller is not null )
			HostTryInjectScavengerBonusLoot( Rpc.Caller );

		HostNotifyCallerOpenBuildSfx();

		for ( var i = 0; i < _loot.Length; i++ )
		{
			if ( _loot[i].IsEmpty )
				continue;

			TryLootSlotInternal( i, _loot[i].Quantity );
			return;
		}
	}

	[Rpc.Host]
	public void RequestLootAll( Guid crateId )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateCallerAliveAndRange() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
			return;

		if ( _loot is null )
			return;

		if ( Rpc.Caller is not null )
			HostTryInjectScavengerBonusLoot( Rpc.Caller );

		HostNotifyCallerOpenBuildSfx();

		for ( var iter = 0; iter < 512; iter++ )
		{
			if ( IsEmpty() )
				break;

			if ( !HostTryLootOneStackFromCrate() )
				break;
		}
	}

	void HostNotifyCallerOpenBuildSfx()
	{
		var inv = FindCallerInventory();
		if ( inv.IsValid() )
			inv.HostNotifyOpenBuildSfx( GameObject.WorldPosition );
	}

	bool HostTryLootOneStackFromCrate()
	{
		if ( _loot is null )
			return false;

		for ( var i = 0; i < _loot.Length; i++ )
		{
			if ( _loot[i].IsEmpty )
				continue;

			var qBefore = _loot[i].Quantity;
			TryLootSlotInternal( i, _loot[i].Quantity );
			if ( _loot[i].IsEmpty || _loot[i].Quantity < qBefore )
				return true;
		}

		return false;
	}

	[Rpc.Host]
	public void RequestLootInventorySlot( Guid crateId, int gridSlotIndex )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateLootCallerShared() )
			return;

		if ( crateId != CrateId || !ActiveById.TryGetValue( crateId, out var reg ) || reg != this )
			return;

		if ( _loot is null || gridSlotIndex < 0 || gridSlotIndex >= _loot.Length || _loot[gridSlotIndex].IsEmpty )
			return;

		if ( Rpc.Caller is not null )
			HostTryInjectScavengerBonusLoot( Rpc.Caller );

		TryLootSlotInternal( gridSlotIndex, _loot[gridSlotIndex].Quantity );
	}

	bool ValidateCallerAliveAndRange() =>
		Rpc.Caller is not null && HostValidateCallerForLoot( Rpc.Caller );

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

	bool ValidateLootCallerShared()
	{
		if ( Rpc.Caller is null )
			return false;

		return HostValidateCallerForLoot( Rpc.Caller );
	}

	void TryLootSlotInternal( int slotIndex, int quantity )
	{
		ref var src = ref _loot[slotIndex];
		if ( src.IsEmpty )
			return;

		var take = Math.Min( quantity, src.Quantity );
		var looterInv = FindCallerInventory();
		if ( !looterInv.IsValid() )
			return;

		var chunk = CloneSlotPartial( src, take );
		if ( !looterInv.ServerTryImportLootStack( chunk, out _ ) )
			return;

		src.Quantity -= take;
		if ( src.Quantity <= 0 )
			src = ThornsInventorySlot.Empty;

		if ( Networking.IsHost && looterInv.IsValid() )
		{
			var sub = CrateKindSync == ThornsLootCrateKind.AirdropPremium
				? "Priority supply secured."
				: "Salvaged from crate.";
			ThornsGameShell.HostPushLootPickupToast( looterInv, chunk.ItemId, take, sub );

			var ms = looterInv.GameObject.Components.Get<ThornsPlayerMilestones>();
			if ( ms.IsValid() )
			{
				ms.HostRecordEvent( ThornsMilestoneEventTokens.LootWorldCrate );
				if ( CrateKindSync == ThornsLootCrateKind.AirdropPremium )
					ms.HostRecordEvent( ThornsMilestoneEventTokens.LootAirdrop );
				else if ( CrateKindSync == ThornsLootCrateKind.MilitaryMixed )
					ms.HostRecordEvent( ThornsMilestoneEventTokens.LootMilitaryCrate );
			}
		}
	}

	ThornsInventory FindCallerInventory()
	{
		if ( Rpc.Caller is null )
			return default;

		var root = FindConnectionPawnRoot( GameObject.Scene, Rpc.Caller );
		return root.IsValid() ? root.Components.Get<ThornsInventory>( FindMode.EnabledInSelf ) : default;
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

	/// <summary>Debug UI snapshot — tab-separated rows <c>G|slot|...</c>.</summary>
	public string HostFormatUiSnapshotText()
	{
		if ( !Networking.IsHost || _loot is null )
			return "";

		using var sw = new System.IO.StringWriter();
		for ( var i = 0; i < _loot.Length; i++ )
		{
			if ( _loot[i].IsEmpty )
				continue;
			var s = _loot[i];
			sw.Write(
				$"G|{i}|{s.ItemId}|{s.Quantity}|{(s.HasDurability ? s.Durability : 0f)}|{s.WeaponLoadedAmmo}|{s.WeaponInstanceId}|{s.WeaponRollPayload}|{s.ArmorRollPayload}\n" );
		}

		return sw.ToString();
	}
}
