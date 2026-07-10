namespace Sandbox;

/// <summary>
/// Server-authoritative 38-slot inventory (8 hotbar + 30 backpack). THORNS_EVERYTHING_DOCUMENT §structure; owner-only snapshots.
/// Owner UI should send drag intent through <see cref="ThornsInventoryClientTransfer"/> where possible; RPCs remain the host boundary.
/// </summary>
[Title( "Thorns — Inventory" )]
[Category( "Thorns" )]
[Icon( "inventory_2" )]
[Order( 75 )]
public sealed partial class ThornsInventory : Component, Component.INetworkSpawn
{
	public const int HotbarSlotCount = 8;
	public const int BackpackSlotCount = 30;
	public const int TotalSlots = HotbarSlotCount + BackpackSlotCount;

	readonly ThornsInventorySlot[] _slots = new ThornsInventorySlot[TotalSlots];

	/// <summary>Host: mirrors <see cref="ThornsPlayer.HostPersistenceAccountKey"/> so quit-save can snapshot inventory even if session component destroyed first.</summary>
	string _hostPersistenceAccountKey = "";

	Guid _hostOwnerConnectionId;

	public void OnNetworkSpawn( Connection owner )
	{
		BindInventoryServices();

		if ( !Networking.IsHost )
			return;

		_hostPersistenceAccountKey = owner is not null ? ThornsPersistenceIdentity.GetStableAccountKey( owner ) : "";
		_hostOwnerConnectionId = owner?.Id ?? Guid.Empty;

		Log.Info( $"[Thorns] Inventory created (host) on '{GameObject.Name}' owner={owner?.Id}" );
		var skipLoadout = ThornsWorldPersistence.Instance is { } wp && wp.HostSpawnRestoreSkipsDefaultInventory( owner );
		if ( !skipLoadout )
		{
			ServerApplyEmptyPlayerLoadout();
			PushSnapshotToOwner();
		}

		_ = PushSnapshotAfterSpawnAsync();
	}

	protected override void OnStart()
	{
		BindInventoryServices();
		// Hotload-safe: stale delta mirror must not diff against a mismatched backing store.
		_invServices.Replication.ResetLastSentOwnerMirror();
	}

	protected override void OnDestroy()
	{
		if ( !Networking.IsHost || !Game.IsPlaying )
			return;

		var slots = HostSnapshotSlotsForPersistence();
		var session = Components.Get<ThornsPlayer>();
		if ( session.IsValid() )
			ThornsWorldPersistence.HostTryRememberPlayerBeforeTeardown( session, slots );
		else if ( !string.IsNullOrEmpty( _hostPersistenceAccountKey ) )
			ThornsWorldPersistence.HostTryRememberPlayerBeforeTeardownFromRoot( _hostPersistenceAccountKey, _hostOwnerConnectionId, GameObject, slots );
	}

	async Task PushSnapshotAfterSpawnAsync()
	{
		await Task.DelayRealtimeSeconds( 0.08f );
		if ( GameObject.IsValid() && Networking.IsHost )
			PushSnapshotToOwner();
	}

	protected override void OnFixedUpdate() => _invServices.Consumable.OnFixedUpdate();

	// ---------- Server API (host only) ----------

	/// <returns>Quantity that could not be added (leftover).</returns>
	public int ServerAddItem(
		string itemId,
		int quantity,
		bool rollWeaponInstanceForWeapons = true,
		bool suppressOwnerSnapshot = false,
		bool suppressMilestoneRecord = false )
	{
		if ( !Networking.IsHost )
			return quantity;

		if ( quantity <= 0 )
			return 0;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			Log.Warning( $"[Thorns] AddItem rejected: unknown item id '{itemId}'" );
			return quantity;
		}

		var remaining = quantity;

		while ( remaining > 0 )
		{
			var bestIdx = -1;
			var bestSpace = 0;
			void ConsiderMergeRange( int from, int toExcl )
			{
				for ( var i = from; i < toExcl; i++ )
				{
					ref var s = ref _slots[i];
					if ( s.IsEmpty )
						continue;
					if ( !CanMergeAddInto( def, s, itemId ) )
						continue;
					var space = def.MaxStack - s.Quantity;
					if ( space > bestSpace )
					{
						bestSpace = space;
						bestIdx = i;
					}
				}
			}

			ConsiderMergeRange( HotbarSlotCount, TotalSlots );
			if ( bestIdx < 0 || bestSpace <= 0 )
				ConsiderMergeRange( 0, HotbarSlotCount );

			if ( bestIdx >= 0 && bestSpace > 0 )
			{
				var put = Math.Min( remaining, bestSpace );
				_slots[bestIdx].Quantity += put;
				remaining -= put;
				Log.Info( $"[Thorns] Item added (merge): slot={bestIdx} item={itemId} +{put}, remaining overflow={remaining}" );
				continue;
			}

			var empty = FindFirstEmptySlot();
			if ( empty < 0 )
				break;

			var chunk = Math.Min( remaining, def.MaxStack );
			_slots[empty] = CreateNewStackSlot( def, itemId, chunk, rollWeaponInstanceForWeapons );
			remaining -= chunk;
			Log.Info( $"[Thorns] Item added (new stack): slot={empty} item={itemId} qty={chunk}, leftover={remaining}" );
		}

		if ( remaining > 0 )
			Log.Warning( $"[Thorns] AddItem partial: could not place {remaining}x '{itemId}' (inventory full)" );

		var added = quantity - remaining;
		HostNotifyMilestoneResourceCollected( def, itemId, added, suppressMilestoneRecord );

		if ( !suppressOwnerSnapshot )
			PushSnapshotToOwner();
		return remaining;
	}

	void HostNotifyMilestoneResourceCollected(
		ThornsItemRegistry.ThornsItemDefinition def,
		string itemId,
		int quantityAdded,
		bool suppressMilestoneRecord )
	{
		if ( suppressMilestoneRecord || quantityAdded <= 0 )
			return;

		if ( def.ItemType != ThornsItemType.Resource )
			return;

		var ms = GameObject.Components.Get<ThornsPlayerMilestones>();
		if ( ms.IsValid() )
			ms.HostRecordResourceCollected( itemId, quantityAdded );
	}

	/// <summary>Host-only: true if the full <paramref name="quantity"/> can merge into existing stacks and/or empty slots (harvest / loot gate).</summary>
	public bool HostCanFitStackableResourceQuantity( string itemId, int quantity )
	{
		if ( !Networking.IsHost || quantity <= 0 )
			return false;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
			return false;

		if ( def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool || def.ItemType == ThornsItemType.Armor )
			return false;

		var remaining = quantity;

		for ( var i = 0; i < TotalSlots && remaining > 0; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !CanMergeAddInto( def, s, itemId ) )
				continue;
			var space = def.MaxStack - s.Quantity;
			if ( space <= 0 )
				continue;
			var put = Math.Min( remaining, space );
			remaining -= put;
		}

		while ( remaining > 0 )
		{
			var emptyIdx = -1;
			for ( var i = HotbarSlotCount; i < TotalSlots; i++ )
			{
				if ( _slots[i].IsEmpty )
				{
					emptyIdx = i;
					break;
				}
			}

			if ( emptyIdx < 0 )
			{
				for ( var i = 0; i < HotbarSlotCount; i++ )
				{
					if ( _slots[i].IsEmpty )
					{
						emptyIdx = i;
						break;
					}
				}
			}

			if ( emptyIdx < 0 )
				break;

			var chunk = Math.Min( remaining, def.MaxStack );
			remaining -= chunk;
		}

		return remaining <= 0;
	}

	/// <summary>Host-only read for combat / equip validation.</summary>
	public bool TryGetHostSlot( int index, out ThornsInventorySlot slot )
	{
		slot = default;
		if ( !Networking.IsHost || !IsValidSlot( index ) )
			return false;

		slot = _slots[index];
		return true;
	}

	/// <summary>Ensure weapon row has a stable instance id (THORNS doc — firing/durability/ammo state per instance).</summary>
	public bool ServerEnsureWeaponInstanceForHotbarSlot( int slotIndex )
	{
		if ( !Networking.IsHost )
			return false;
		if ( slotIndex < 0 || slotIndex >= HotbarSlotCount )
			return false;

		ref var s = ref _slots[slotIndex];
		if ( s.IsEmpty )
			return false;

		if ( !ThornsItemRegistry.TryGet( s.ItemId, out var def ) )
		{
			if ( string.Equals( s.ItemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new ThornsItemRegistry.ThornsItemDefinition(
					Id: "sniper",
					DisplayName: "Sniper",
					MaxStack: 1,
					ItemType: ThornsItemType.Weapon,
					CombatWeaponDefinitionId: "sniper",
					ViewModelAsset: ThornsViewModelController.SniperFirstPersonViewmodelPath,
					WorldModelAsset: ThornsViewModelController.SniperWorldModelPath );
			}
			else if ( string.Equals( s.ItemId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new ThornsItemRegistry.ThornsItemDefinition(
					Id: "m9_bayonet",
					DisplayName: "M9 Bayonet",
					MaxStack: 1,
					ItemType: ThornsItemType.Weapon,
					CombatWeaponDefinitionId: "m9_bayonet",
					ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
					WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath );
			}
			else
			{
				return false;
			}
		}

		if ( def.ItemType != ThornsItemType.Weapon )
			return false;

		if ( !string.IsNullOrEmpty( s.WeaponInstanceId ) )
			return true;

		s.WeaponInstanceId = Guid.NewGuid().ToString( "N" );
		ApplyNewWeaponRowDefaults( ref s, def, s.ItemId );
		PushSnapshotToOwner();
		return true;
	}

	/// <summary>Host-only writeback for combat/reload (single slot).</summary>
	public void ServerWriteSlot( int index, ThornsInventorySlot slot )
	{
		if ( !Networking.IsHost || !IsValidSlot( index ) )
			return;

		_slots[index] = slot;
		PushSnapshotToOwner();
	}

	/// <summary>Total reserve ammo items matching <paramref name="ammoTypeId"/> (THORNS item <see cref="ThornsItemRegistry.ThornsItemDefinition.AmmoTypeId"/>).</summary>
	public int ServerCountAmmoMatchingType( string ammoTypeId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( ammoTypeId ) )
			return 0;

		var count = 0;
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !ThornsItemRegistry.TryGet( s.ItemId, out var d ) || d.ItemType != ThornsItemType.Ammo )
				continue;
			if ( d.AmmoTypeId != ammoTypeId )
				continue;
			count += s.Quantity;
		}

		return count;
	}

	/// <summary>Removes up to <paramref name="amount"/> ammo across stacks in stable slot order.</summary>
	public int ServerRemoveAmmoMatchingType( string ammoTypeId, int amount )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( ammoTypeId ) || amount <= 0 )
			return 0;

		var need = amount;
		var removed = 0;
		for ( var i = 0; i < TotalSlots && need > 0; i++ )
		{
			ref var s = ref _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !ThornsItemRegistry.TryGet( s.ItemId, out var d ) || d.ItemType != ThornsItemType.Ammo )
				continue;
			if ( d.AmmoTypeId != ammoTypeId )
				continue;

			var take = Math.Min( need, s.Quantity );
			s.Quantity -= take;
			need -= take;
			removed += take;
			if ( s.Quantity <= 0 )
				s = ThornsInventorySlot.Empty;
		}

		if ( removed > 0 )
			PushSnapshotToOwner();

		return removed;
	}

	/// <returns>Amount actually removed.</returns>
	public int ServerRemoveItem( int slotIndex, int quantity )
	{
		if ( !Networking.IsHost )
			return 0;

		if ( !IsValidSlot( slotIndex ) || quantity <= 0 )
			return 0;

		ref var s = ref _slots[slotIndex];
		if ( s.IsEmpty )
			return 0;

		var take = Math.Min( quantity, s.Quantity );
		s.Quantity -= take;
		if ( s.Quantity <= 0 )
			s = ThornsInventorySlot.Empty;

		Log.Info( $"[Thorns] Item removed: slot={slotIndex} qty={take}" );
		PushSnapshotToOwner();
		return take;
	}

	/// <summary>Host-only: removes one food consumable (<see cref="ThornsConsumableKind.Food"/>) from a uniformly random eligible slot.</summary>
	public bool HostTryRemoveOneRandomFoodItem( out ThornsItemRegistry.ThornsItemDefinition consumedDef )
	{
		consumedDef = null;
		if ( !Networking.IsHost )
			return false;

		var count = 0;
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !ThornsItemRegistry.TryGet( s.ItemId, out var def ) )
				continue;
			if ( def.ItemType != ThornsItemType.Consumable || def.ConsumableKind != ThornsConsumableKind.Food )
				continue;
			count++;
		}

		if ( count == 0 )
			return false;

		var pick = Random.Shared.Next( count );
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			if ( !ThornsItemRegistry.TryGet( s.ItemId, out var def ) )
				continue;
			if ( def.ItemType != ThornsItemType.Consumable || def.ConsumableKind != ThornsConsumableKind.Food )
				continue;

			if ( pick == 0 )
			{
				consumedDef = def;
				ServerRemoveItem( i, 1 );
				Log.Info( $"[Thorns] Random food removed for tame feed: slot={i} item={def.Id}" );
				return true;
			}

			pick--;
		}

		return false;
	}

	/// <summary>Remove by item id across any slots until quantity reached.</summary>
	public int ServerRemoveItemId( string itemId, int quantity, bool suppressOwnerSnapshot = false )
	{
		if ( !Networking.IsHost || quantity <= 0 )
			return 0;

		var need = quantity;
		var removed = 0;
		for ( var i = 0; i < TotalSlots && need > 0; i++ )
		{
			ref var s = ref _slots[i];
			if ( s.IsEmpty || s.ItemId != itemId )
				continue;
			var take = Math.Min( need, s.Quantity );
			s.Quantity -= take;
			need -= take;
			removed += take;
			if ( s.Quantity <= 0 )
				s = ThornsInventorySlot.Empty;
		}

		if ( removed > 0 )
		{
			Log.Info( $"[Thorns] Item removed by id: item={itemId} totalRemoved={removed}" );
			if ( !suppressOwnerSnapshot )
				PushSnapshotToOwner();
		}

		return removed;
	}

	/// <summary>Host-only: total count of an item id across all stacks.</summary>
	public int ServerCountItemId( string itemId )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( itemId ) )
			return 0;

		var n = 0;
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty || s.ItemId != itemId )
				continue;
			n += s.Quantity;
		}

		return n;
	}

	/// <summary>Client mirror: approximate counts for crafting UI (non-authoritative).</summary>
	public int ClientMirrorCountItemId( string itemId ) =>
		_invServices.Replication.ClientMirrorCountItemId( itemId );

	/// <summary>Host-only: whether craft output can fully fit (stack rules + empty slot for weapon/armor).</summary>
	public bool HostCanAcceptCraftOutput( string itemId, int quantity ) =>
		_invServices.Crafting.HostCanAcceptCraftOutput( itemId, quantity );

	public bool ServerMoveItem( int fromSlot, int toSlot, int quantity )
	{
		if ( !Networking.IsHost )
			return false;

		if ( !IsValidSlot( fromSlot ) || !IsValidSlot( toSlot ) || fromSlot == toSlot )
			return false;

		if ( quantity <= 0 )
			return false;

		ref var from = ref _slots[fromSlot];
		if ( from.IsEmpty )
			return false;

		if ( !ThornsItemRegistry.TryGet( from.ItemId, out _ ) )
			return false;

		Log.Info( $"[Thorns][Inventory] Before move: slot {fromSlot} = {SlotDebug( from )}, slot {toSlot} = {SlotDebug( _slots[toSlot] )}" );

		var moveQty = Math.Min( quantity, from.Quantity );
		ref var to = ref _slots[toSlot];

		if ( to.IsEmpty )
		{
			var moved = new ThornsInventorySlot
			{
				ItemId = from.ItemId,
				Quantity = moveQty,
				HasDurability = from.HasDurability,
				Durability = from.Durability,
				WeaponInstanceId = from.WeaponInstanceId,
				WeaponLoadedAmmo = from.WeaponLoadedAmmo,
				WeaponRollPayload = from.WeaponRollPayload,
				ArmorRollPayload = from.ArmorRollPayload
			};
			_slots[toSlot] = moved;
			from.Quantity -= moveQty;
			if ( from.Quantity <= 0 )
				from = ThornsInventorySlot.Empty;
			Log.Info( $"[Thorns][Inventory] After move: slot {toSlot} = {SlotDebug( _slots[toSlot] )}, slot {fromSlot} = {SlotDebug( _slots[fromSlot] )}" );
			PushSnapshotToOwner();
			return true;
		}

		if ( !ThornsItemRegistry.TryGet( to.ItemId, out var toDef ) )
			return false;

		if ( from.ItemId != to.ItemId || !from.EqualsStackIdentity( to ) )
		{
			Log.Warning( "[Thorns] MoveItem rejected: destination occupied by different stack" );
			return false;
		}

		var space = toDef.MaxStack - to.Quantity;
		if ( space <= 0 )
			return false;

		var put = Math.Min( moveQty, space );
		to.Quantity += put;
		from.Quantity -= put;
		if ( from.Quantity <= 0 )
			from = ThornsInventorySlot.Empty;

		Log.Info( $"[Thorns][Inventory] After move: slot {toSlot} = {SlotDebug( _slots[toSlot] )}, slot {fromSlot} = {SlotDebug( _slots[fromSlot] )}" );
		PushSnapshotToOwner();
		return true;
	}

	public bool ServerSwapSlots( int a, int b )
	{
		if ( !Networking.IsHost )
			return false;
		if ( !IsValidSlot( a ) || !IsValidSlot( b ) || a == b )
			return false;

		(_slots[a], _slots[b]) = (_slots[b], _slots[a]);
		Log.Info( $"[Thorns] SwapSlots: {a} <-> {b}" );
		PushSnapshotToOwner();
		return true;
	}

	public void ServerClearInventory()
	{
		if ( !Networking.IsHost )
			return;

		for ( var i = 0; i < TotalSlots; i++ )
			_slots[i] = ThornsInventorySlot.Empty;

		Log.Info( $"[Thorns] ClearInventory: '{GameObject.Name}'" );
		PushSnapshotToOwner();
	}

	/// <summary>Host: default join / death respawn — empty inventory and equipment (no starter kit).</summary>
	public void ServerApplyEmptyPlayerLoadout()
	{
		if ( !Networking.IsHost )
			return;

		ServerClearInventory();
		Components.Get<ThornsHotbarEquipment>()?.HostClearEquipmentAfterDeath();
		Components.Get<ThornsArmorEquipment>()?.HostStripAllEquippedForDeath();

		var wallet = Components.Get<ThornsWallet>();
		if ( wallet.IsValid() )
		{
			wallet.Gold = 0;
			wallet.Metal = 0;
		}
	}

	/// <summary>Host-only deep copy for death crate serialization (THORNS_EVERYTHING_DOCUMENT §death).</summary>
	public ThornsInventorySlot[] HostCloneAllSlotsForDeath()
	{
		if ( !Networking.IsHost )
			return Array.Empty<ThornsInventorySlot>();

		var copy = new ThornsInventorySlot[TotalSlots];
		var n = Math.Min( _slots.Length, TotalSlots );
		for ( var i = 0; i < n; i++ )
			copy[i] = _slots[i];
		return copy;
	}

	/// <summary>Host: place loot from a death crate stack into this inventory (merge + first empty).</summary>
	public bool ServerTryImportLootStack( ThornsInventorySlot stack, out string failureReason )
	{
		failureReason = "";
		if ( !Networking.IsHost || stack.IsEmpty )
		{
			failureReason = "invalid";
			return false;
		}

		if ( !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
		{
			failureReason = "unknown_item";
			return false;
		}

		var remaining = stack.Quantity;
		var incoming = stack;

		while ( remaining > 0 )
		{
			var mergedAny = false;
			for ( var pass = 0; pass < 2; pass++ )
			{
				var from = pass == 0 ? HotbarSlotCount : 0;
				var toExcl = pass == 0 ? TotalSlots : HotbarSlotCount;
				for ( var i = from; i < toExcl; i++ )
				{
					ref var s = ref _slots[i];
					if ( s.IsEmpty )
						continue;
					if ( !CanMergeLootIntoExisting( def, incoming, s ) )
						continue;

					var space = def.MaxStack - s.Quantity;
					if ( space <= 0 )
						continue;

					var put = Math.Min( space, remaining );
					s.Quantity += put;
					remaining -= put;
					mergedAny = true;
					incoming = incoming with { Quantity = remaining };
					if ( remaining <= 0 )
						break;
				}

				if ( remaining <= 0 )
					break;
			}

			if ( remaining <= 0 )
				break;

			if ( !mergedAny )
				break;
		}

		if ( remaining > 0 )
		{
			var empty = FindFirstEmptySlot();
			if ( empty < 0 )
			{
				failureReason = "inventory_full";
				return false;
			}

			_slots[empty] = incoming with { Quantity = remaining };
			remaining = 0;
		}

		Log.Info( $"[Thorns] Loot imported to '{GameObject.Name}': item={stack.ItemId}" );

		if ( def.ItemType == ThornsItemType.Resource && stack.Quantity > 0 )
		{
			var ms = GameObject.Components.Get<ThornsPlayerMilestones>();
			if ( ms.IsValid() )
				ms.HostRecordResourceCollected( stack.ItemId, stack.Quantity );
		}

		PushSnapshotToOwner();
		return true;
	}

	static bool CanMergeLootIntoExisting( ThornsItemRegistry.ThornsItemDefinition def, ThornsInventorySlot incoming, ThornsInventorySlot existing )
	{
		if ( existing.ItemId != incoming.ItemId )
			return false;
		if ( def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool || def.ItemType == ThornsItemType.Armor )
			return existing.EqualsStackIdentity( incoming );
		return existing.Quantity < def.MaxStack;
	}

	bool HostSnapshotTargetsListenServerLocalOwner()
	{
		if ( !Networking.IsActive )
			return true;
		var local = Connection.Local;
		return local is not null && local.Id == GameObject.Network.OwnerId;
	}

	void PushSnapshotToOwner() => _invServices.Replication.PushSnapshotToOwner();

	/// <summary>Host-only: flush owner snapshot after batched mutations that used <c>suppressOwnerSnapshot</c> (e.g. building placement).</summary>
	public void HostPushInventorySnapshotToOwner() => PushSnapshotToOwner();

	internal bool HostIsValidInventorySlot( int i ) => i >= 0 && i < TotalSlots;

	internal ref ThornsInventorySlot HostGetSlotRef( int index ) => ref _slots[index];

	[Rpc.Host]
	public void RequestOpenStorageChest( string structureInstanceIdD ) =>
		_invServices.Storage.RequestOpenStorageChest( structureInstanceIdD );

	/// <summary>Host: open-build sting at a world point for this pawn (listen-server + <see cref="Rpc.Owner"/>).</summary>
	public void HostNotifyOpenBuildSfx( Vector3 worldEmit )
	{
		if ( HostSnapshotTargetsListenServerLocalOwner() )
			ThornsGameplaySfx.PlayOpenBuildAt( worldEmit );
		else
			RpcOwnerPlayOpenBuildSfx( worldEmit );
	}

	[Rpc.Owner]
	void RpcOwnerPlayOpenBuildSfx( Vector3 worldEmit ) =>
		ThornsGameplaySfx.PlayOpenBuildAt( worldEmit );

	[Rpc.Host]
	public void RequestStorageChestTransfer(
		string structureInstanceIdD,
		bool fromChest,
		int fromIdx,
		bool toChest,
		int toIdx ) =>
		_invServices.Storage.RequestStorageChestTransfer( structureInstanceIdD, fromChest, fromIdx, toChest, toIdx );

	[Rpc.Host]
	public void RequestStorageChestQuickTransfer( string structureInstanceIdD, bool fromChest, int fromIdx ) =>
		_invServices.Storage.RequestStorageChestQuickTransfer( structureInstanceIdD, fromChest, fromIdx );

	[Rpc.Owner]
	public void ClientReceiveStorageChestSnapshot( string structureInstanceIdD, ThornsInventorySlotNet[] slots )
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		if ( !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		shell.ApplyStorageChestSnapshot( sid, slots );
	}

	[Rpc.Host]
	public void RequestOpenCampfire( string structureInstanceIdD ) =>
		_invServices.Storage.RequestOpenCampfire( structureInstanceIdD );

	[Rpc.Host]
	public void RequestNotifyCampfireUiClosed( string structureInstanceIdD ) =>
		_invServices.Storage.RequestNotifyCampfireUiClosed( structureInstanceIdD );

	[Rpc.Host]
	public void RequestCampfireTransfer(
		string structureInstanceIdD,
		bool fromCampfire,
		int fromIdx,
		bool toCampfire,
		int toIdx ) =>
		_invServices.Storage.RequestCampfireTransfer( structureInstanceIdD, fromCampfire, fromIdx, toCampfire, toIdx );

	[Rpc.Host]
	public void RequestCampfireQuickTransfer( string structureInstanceIdD, bool fromCampfire, int fromIdx ) =>
		_invServices.Storage.RequestCampfireQuickTransfer( structureInstanceIdD, fromCampfire, fromIdx );

	[Rpc.Owner]
	public void ClientReceiveCampfireSnapshot(
		string structureInstanceIdD,
		ThornsInventorySlotNet[] slots,
		string processingInputLabel,
		string processingOutputLabel,
		float progress01,
		float remainingSeconds,
		bool presentOverlay )
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		if ( !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		shell.ApplyCampfireSnapshot( sid, slots, processingInputLabel, processingOutputLabel, progress01,
			remainingSeconds, presentOverlay );
	}

	[Rpc.Host]
	public void RequestOpenWorkbench( string structureInstanceIdD ) =>
		_invServices.Storage.RequestOpenWorkbench( structureInstanceIdD );

	[Rpc.Host]
	public void RequestWorkbenchTransfer(
		string structureInstanceIdD,
		bool fromBench,
		int fromIdx,
		bool toBench,
		int toIdx ) =>
		_invServices.Storage.RequestWorkbenchTransfer( structureInstanceIdD, fromBench, fromIdx, toBench, toIdx );

	[Rpc.Host]
	public void RequestWorkbenchQuickTransfer( string structureInstanceIdD, bool fromBench, int fromIdx ) =>
		_invServices.Storage.RequestWorkbenchQuickTransfer( structureInstanceIdD, fromBench, fromIdx );

	[Rpc.Owner]
	public void ClientReceiveWorkbenchSnapshot(
		string structureInstanceIdD,
		ThornsInventorySlotNet[] slots,
		string processingLabel,
		float progress01,
		float remainingSeconds )
	{
		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		if ( !Guid.TryParse( structureInstanceIdD, out var sid ) )
			return;

		shell.ApplyWorkbenchSnapshot( sid, slots, processingLabel, progress01, remainingSeconds );
	}

	[Rpc.Owner]
	void ClientReceiveInventorySnapshot( ThornsInventorySlotNet[] slots ) =>
		_invServices.Replication.ApplyInventoryClientMirror( slots );

	[Rpc.Owner]
	void ClientReceiveInventoryDelta( ThornsInventorySlotChangeNet[] changes ) =>
		_invServices.Replication.ApplyInventoryClientMirrorDelta( changes );

	/// <summary>Debug UI / HUD: read-only client mirror (THORNS doc — owner snapshots, non-authoritative).</summary>
	public int ClientMirrorRevision => _invServices.Replication.ClientMirrorRevision;

	/// <summary>Debug UI: non-authoritative slot mirror for local owner.</summary>
	public bool TryGetClientMirrorSlot( int index, out ThornsInventorySlotNet slot ) =>
		_invServices.Replication.TryGetClientMirrorSlot( index, out slot );

	// ---------- Client request RPCs (intent only) ----------

	[Rpc.Host]
	public void RequestMoveItem( int fromSlot, int toSlot, int quantity )
	{
		if ( !Networking.IsHost )
			return;
		Log.Info( $"[Thorns][Server] Move request received {fromSlot} -> {toSlot}" );
		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns][Server] Move rejected: caller does not own inventory" );
			return;
		}

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
		{
			Log.Warning( "[Thorns][Server] Move rejected: player is dead" );
			return;
		}

		if ( !IsValidSlot( fromSlot ) || !IsValidSlot( toSlot ) )
		{
			Log.Warning( "[Thorns][Server] Move rejected: invalid slot index" );
			return;
		}

		if ( !TryGetHostSlot( fromSlot, out var source ) || source.IsEmpty )
		{
			Log.Warning( "[Thorns][Server] Move rejected: source slot empty" );
			return;
		}

		if ( !ServerMoveItem( fromSlot, toSlot, quantity ) )
		{
			Log.Warning( $"[Thorns][Server] Move rejected: rules prevented move ({fromSlot}->{toSlot})" );
			return;
		}

		Log.Info( "[Thorns][Server] Move accepted" );
	}

	[Rpc.Host]
	public void RequestUseItemFromSlot( int slotIndex ) =>
		_invServices.Consumable.RequestUseItemFromSlot( slotIndex );

	[Rpc.Host]
	public void RequestDropInventorySlotToWorld( int slotIndex )
	{
		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] Drop rejected: caller does not own inventory" );
			return;
		}

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
		{
			Log.Warning( "[Thorns] Drop rejected: player is dead" );
			return;
		}

		if ( !IsValidSlot( slotIndex ) )
			return;

		if ( !TryGetHostSlot( slotIndex, out var slot ) || slot.IsEmpty || slot.Quantity <= 0 )
			return;

		var clone = CloneInventorySlotFull( slot );
		var dropPos = GameObject.WorldPosition + GameObject.WorldRotation.Forward * 42f + Vector3.Up * 14f;
		var crate = ThornsLootCrate.SpawnHostPlayerDrop( GameObject.Scene, dropPos, clone );
		if ( crate is null || !crate.IsValid() )
		{
			Log.Warning( "[Thorns] Drop rejected: failed to spawn world loot" );
			return;
		}

		var removed = ServerRemoveItem( slotIndex, clone.Quantity );
		if ( removed <= 0 )
		{
			Log.Warning( "[Thorns] Drop: remove failed after spawn — destroying orphan crate" );
			crate.GameObject.Destroy();
			return;
		}

		var nm = ThornsItemRegistry.TryGet( clone.ItemId, out var defDrop ) ? defDrop.DisplayName : clone.ItemId;
		ThornsGameShell.HostPushToastForPawnRoot(
			GameObject,
			$"Dropped · {nm}\nPress E nearby to pick up.",
			3.2f,
			ThornsGameplayToastKind.Hint );

		Log.Info( $"[Thorns] Dropped to world: slot={slotIndex} item={clone.ItemId} qty={clone.Quantity}" );
	}

	static ThornsInventorySlot CloneInventorySlotFull( ThornsInventorySlot src ) =>
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

	[Rpc.Host]
	public void RequestSwapSlots( int a, int b )
	{
		if ( !Networking.IsHost )
			return;
		Log.Info( $"[Thorns][Server] Move request received {a} -> {b} (swap)" );
		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns][Server] Move rejected: caller does not own inventory" );
			return;
		}

		var hp = Components.Get<ThornsHealth>();
		if ( hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive ) )
		{
			Log.Warning( "[Thorns][Server] Move rejected: player is dead" );
			return;
		}

		if ( !ServerSwapSlots( a, b ) )
		{
			Log.Warning( $"[Thorns][Server] Move rejected: swap failed ({a}<->{b})" );
			return;
		}

		Log.Info( "[Thorns][Server] Move accepted" );
	}

	[Rpc.Host]
	public void RequestCraftRecipe( string recipeId ) =>
		_invServices.Crafting.RequestCraftRecipe( recipeId );

	[Rpc.Owner]
	void ClientCraftResultNotify( string status, string detail ) =>
		_invServices.Crafting.ClientCraftResultNotify( status, detail );

	[Rpc.Owner]
	void ClientReceiveConsumableUseRejected( string reason )
	{
		Log.Warning( $"[Thorns] (local) Consumable use rejected: {reason}" );
	}

	[Rpc.Owner]
	void RpcNotifyOwnerConsumableApplied( string itemId, string kind, float hunger, float thirst, float poison )
	{
		Log.Info( $"[Thorns] Consumable applied (owner mirror) item={itemId} kind={kind} hunger={hunger:F1} thirst={thirst:F1} poison={poison:F1}" );

		var shell = Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		var nm = ThornsItemRegistry.TryGet( itemId, out var def ) ? def.DisplayName : itemId;
		var flavor = kind?.Trim() switch
		{
			nameof( ThornsConsumableKind.Food ) or "Food" => "Hunger restored.",
			nameof( ThornsConsumableKind.WaterClean ) or "WaterClean" => "Thirst quenched.",
			nameof( ThornsConsumableKind.WaterDirty ) or "WaterDirty" => "Thirst quenched.",
			nameof( ThornsConsumableKind.Medical ) or "Medical" => "Medical applied.",
			_ => "Consumable used."
		};

		shell.PushGameplayToast( $"{nm}\n{flavor}", 2.6f, ThornsGameplayToastKind.Positive );
	}

	public void HostCancelPendingConsumableUse() => _invServices.Consumable.HostCancelPendingConsumableUse();

	// ---------- TEMP dev / testing (disable via <see cref="ThornsInventoryDev.EnableDevRpcs"/>) ----------

	[Rpc.Host]
	public void RequestDevGrantStarter()
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
		{
			Log.Warning( "[Thorns] Dev grant disabled" );
			return;
		}

		if ( !Networking.IsHost )
			return;
		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] RequestDevGrantStarter rejected: caller" );
			return;
		}

		Log.Info( "[Thorns] [DEV] Grant starter kit (full tester loadout)" );
		ServerGrantDevFullTesterSpawnLoadout();
	}

	[Rpc.Host]
	public void RequestDevPrintInventory()
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
			return;
		if ( !Networking.IsHost )
			return;
		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] RequestDevPrintInventory rejected: caller" );
			return;
		}

		DumpInventoryToLog( "[DEV] inventory dump (server)" );
	}

	[Rpc.Host]
	public void RequestDevClearInventory()
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
			return;
		if ( !Networking.IsHost )
			return;
		if ( !ValidateRpcCallerOwnsPawn() )
			return;

		Log.Info( "[Thorns] [DEV] Clear inventory (dev)" );
		ServerClearInventory();
	}

	[Rpc.Host]
	public void RequestDevMoveTest( int from, int to, int qty )
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
			return;
		if ( !Networking.IsHost )
			return;
		if ( !ValidateRpcCallerOwnsPawn() )
			return;

		Log.Info( $"[Thorns] [DEV] Dev move test from={from} to={to} qty={qty}" );
		ServerMoveItem( from, to, qty );
	}

	[Rpc.Host]
	public void RequestDevSwapTest( int a, int b )
	{
		if ( !ThornsInventoryDev.EnableDevRpcs )
			return;
		if ( !Networking.IsHost )
			return;
		if ( !ValidateRpcCallerOwnsPawn() )
			return;

		Log.Info( $"[Thorns] [DEV] Dev swap test {a}<->{b}" );
		ServerSwapSlots( a, b );
	}

	// ---------- Helpers ----------

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );

	static bool IsValidSlot( int i ) => i >= 0 && i < TotalSlots;

	int FindFirstEmptySlot()
	{
		for ( var i = HotbarSlotCount; i < TotalSlots; i++ )
		{
			if ( _slots[i].IsEmpty )
				return i;
		}

		for ( var i = 0; i < HotbarSlotCount; i++ )
		{
			if ( _slots[i].IsEmpty )
				return i;
		}

		return -1;
	}

	/// <summary>Merging picked-up quantities into an existing stack (weapons never merge by instance rules).</summary>
	static bool CanMergeAddInto( ThornsItemRegistry.ThornsItemDefinition def, ThornsInventorySlot existing, string incomingItemId )
	{
		if ( existing.ItemId != incomingItemId )
			return false;
		if ( def.ItemType == ThornsItemType.Weapon || def.ItemType == ThornsItemType.Tool )
			return false;
		if ( def.ItemType == ThornsItemType.Armor )
			return false;
		return existing.Quantity < def.MaxStack;
	}

	static ThornsInventorySlot CreateNewStackSlot( ThornsItemRegistry.ThornsItemDefinition def, string itemId, int qty, bool rollWeaponInstanceForWeapons )
	{
		var s = new ThornsInventorySlot { ItemId = itemId, Quantity = qty };
		if ( def.ItemType is ThornsItemType.Weapon && rollWeaponInstanceForWeapons )
		{
			s.WeaponInstanceId = Guid.NewGuid().ToString( "N" );
			ApplyNewWeaponRowDefaults( ref s, def, itemId );
		}
		else if ( def.ItemType == ThornsItemType.Tool && def.ToolMaxDurability > 0.001f )
		{
			s.HasDurability = true;
			s.Durability = def.ToolMaxDurability;
		}
		else if ( def.ItemType == ThornsItemType.Armor )
		{
			s.HasDurability = true;
			s.Durability = def.ArmorMaxDurability;
			if ( !string.IsNullOrEmpty( def.ArmorRollPlaceholder ) )
				s.ArmorRollPayload = def.ArmorRollPlaceholder;
			else
				s.ArmorRollPayload = ThornsGearRoll.EncodeArmor( ThornsLootRarity.Common, 1f );
		}

		return s;
	}

	/// <summary>Host: place one stack into the first empty grid slot (used by armor unequip).</summary>
	public bool ServerTryPlaceSingleStackInFirstEmpty( ThornsInventorySlot slot )
	{
		if ( !Networking.IsHost || slot.IsEmpty || slot.Quantity <= 0 )
			return false;

		var idx = FindFirstEmptySlot();
		if ( idx < 0 )
			return false;

		_slots[idx] = slot;
		Log.Info( $"[Thorns] Placed stack in first empty slot={idx} item={slot.ItemId}" );
		PushSnapshotToOwner();
		return true;
	}

	/// <summary>Host: place unequipped armor into a specific empty inventory slot (UI drag target).</summary>
	public bool HostTryPlaceArmorUnequipAtSlot( ThornsInventorySlot slot, int slotIndex )
	{
		if ( !Networking.IsHost || slot.IsEmpty || slot.Quantity <= 0 )
			return false;

		if ( !IsValidSlot( slotIndex ) )
			return false;

		ref var dest = ref _slots[slotIndex];
		if ( !dest.IsEmpty )
			return false;

		dest = slot;
		Log.Info( $"[Thorns] Armor unequip placed at slot={slotIndex} item={slot.ItemId}" );
		PushSnapshotToOwner();
		return true;
	}

	internal static void ApplyNewWeaponRowDefaults( ref ThornsInventorySlot s, ThornsItemRegistry.ThornsItemDefinition def, string itemId )
	{
		var combatId = string.IsNullOrEmpty( def.CombatWeaponDefinitionId ) ? itemId : def.CombatWeaponDefinitionId;
		var w = ThornsWeaponDefinitions.Get( combatId );
		s.HasDurability = true;
		s.Durability = w.MaxDurability;
		s.WeaponLoadedAmmo = w.ClipSize;
	}

	/// <summary>Host: legacy tool stacks (before durability) pick up max once they take wear.</summary>
	internal static void HostBootstrapToolDurabilityIfNeeded( ref ThornsInventorySlot s, ThornsItemRegistry.ThornsItemDefinition def )
	{
		if ( def.ItemType != ThornsItemType.Tool || def.ToolMaxDurability <= 0.001f )
			return;

		if ( s.HasDurability )
			return;

		s.HasDurability = true;
		s.Durability = def.ToolMaxDurability;
	}

	/// <summary>Host: harvest or tool-melee strike — applies Reinforced upgrade like firearms.</summary>
	internal static void HostApplyToolDurabilityLoss(
		ref ThornsInventorySlot s,
		ThornsItemRegistry.ThornsItemDefinition def,
		float baseLoss,
		ThornsPlayerUpgrades ups )
	{
		if ( baseLoss <= 0.0001f || def.ItemType != ThornsItemType.Tool || def.ToolMaxDurability <= 0.001f )
			return;

		HostBootstrapToolDurabilityIfNeeded( ref s, def );
		var loss = baseLoss;
		if ( ups.IsValid() && ups.ReinforcedRank > 0 )
			loss *= ups.GetReinforcedDurabilityLossMultiplier();

		s.Durability = Math.Max( 0f, s.Durability - loss );
	}

	internal static ThornsInventorySlotNet ToNet( ThornsInventorySlot s )
	{
		return new ThornsInventorySlotNet
		{
			ItemId = s.ItemId ?? "",
			Quantity = s.Quantity,
			HasDurability = s.HasDurability ? 1 : 0,
			Durability = s.Durability,
			WeaponInstanceId = s.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = s.WeaponLoadedAmmo,
			WeaponRollPayload = s.WeaponRollPayload ?? "",
			ArmorRollPayload = s.ArmorRollPayload ?? ""
		};
	}

	internal static ThornsInventorySlot SlotFromNet( ThornsInventorySlotNet n )
	{
		if ( string.IsNullOrWhiteSpace( n.ItemId ) || n.Quantity <= 0 )
			return ThornsInventorySlot.Empty;

		return new ThornsInventorySlot
		{
			ItemId = n.ItemId ?? "",
			Quantity = n.Quantity,
			HasDurability = n.HasDurability != 0,
			Durability = n.Durability,
			WeaponInstanceId = n.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = n.WeaponLoadedAmmo,
			WeaponRollPayload = n.WeaponRollPayload ?? "",
			ArmorRollPayload = n.ArmorRollPayload ?? ""
		};
	}

	public ThornsInventorySlotNet[] HostSnapshotSlotsForPersistence()
	{
		if ( !Networking.IsHost )
			return Array.Empty<ThornsInventorySlotNet>();

		var payload = new ThornsInventorySlotNet[TotalSlots];
		for ( var i = 0; i < TotalSlots; i++ )
			payload[i] = ToNet( _slots[i] );

		return payload;
	}

	public void HostRestoreInventorySlotsFromPersistence( ThornsInventorySlotNet[] src )
	{
		if ( !Networking.IsHost || src is null )
			return;

		for ( var i = 0; i < TotalSlots; i++ )
			_slots[i] = i < src.Length ? SlotFromNet( src[i] ) : ThornsInventorySlot.Empty;

		PushSnapshotToOwner();
	}

	static string SlotDebug( ThornsInventorySlot s )
	{
		if ( s.IsEmpty )
			return "Empty";
		return $"{s.ItemId} x{s.Quantity} inst={(string.IsNullOrEmpty( s.WeaponInstanceId ) ? "-" : s.WeaponInstanceId)}";
	}

	void DumpInventoryToLog( string prefix )
	{
		var sb = new System.Text.StringBuilder();
		sb.Append( prefix ).Append( ' ' ).Append( GameObject.Name ).AppendLine();
		for ( var i = 0; i < TotalSlots; i++ )
		{
			var s = _slots[i];
			if ( s.IsEmpty )
				continue;
			sb.Append( "  slot " ).Append( i ).Append( ": " ).Append( s.ItemId ).Append( " x" ).Append( s.Quantity );
			if ( !string.IsNullOrEmpty( s.WeaponInstanceId ) )
				sb.Append( " inst=" ).Append( s.WeaponInstanceId );
			if ( s.WeaponLoadedAmmo > 0 )
				sb.Append( " loaded=" ).Append( s.WeaponLoadedAmmo );
			if ( s.HasDurability )
				sb.Append( " dur=" ).Append( s.Durability.ToString( "F1" ) );
			sb.AppendLine();
		}

		Log.Info( sb.ToString() );
	}

	/// <summary>TEMP: host-only starter grant for testing (doc: new players empty unless dev grant).</summary>
	public void ServerDevGrantStarterIfEnabled()
	{
		if ( !Networking.IsHost || !ThornsInventoryDev.EnableDevRpcs )
			return;

		Log.Info( "[Thorns] [DEV] ServerDevGrantStarterIfEnabled" );
		ServerGrantDevFullTesterSpawnLoadout();
	}

	/// <summary>Host: legacy dev tester loadout — <see cref="RequestDevGrantStarter"/> / dev bootstrap only.</summary>
	public void ServerGrantDevFullTesterSpawnLoadout()
	{
		if ( !Networking.IsHost )
			return;

		for ( var i = 0; i < TotalSlots; i++ )
			_slots[i] = ThornsInventorySlot.Empty;

		HostGrantHotbarPrimaryItem( 0, "stone_hatchet" );
		HostGrantHotbarPrimaryItem( 1, "axe" );
		HostGrantHotbarPrimaryItem( 2, "stone_pick" );
		HostGrantHotbarPrimaryItem( 3, "pickaxe" );
		HostGrantHotbarPrimaryItem( 4, "m4" );
		HostGrantHotbarPrimaryItem( 3, "mp5" );
		HostGrantHotbarPrimaryItem( 4, "shotgun" );
		HostGrantHotbarPrimaryItem( 5, "sniper" );
		HostGrantHotbarPrimaryItem( 6, "m9_bayonet" );
		ServerAddItem( "rifle_ammo", 90, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "smg_ammo", 90, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "shotgun_ammo", 32, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "sniper_ammo", 20, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "pistol_ammo", 60, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "apple", 30, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "water", 30, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "bandage", 2, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "kevlar_helmet", 1, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "kevlar_chest", 1, true, true, suppressMilestoneRecord: true );
		ServerAddItem( "kevlar_pants", 1, true, true, suppressMilestoneRecord: true );

		ServerAddItem( "wood", 2000, true, true, suppressMilestoneRecord: true );
		PushSnapshotToOwner();
	}

	void HostGrantHotbarPrimaryItem( int hotbarIndex, string itemId )
	{
		if ( hotbarIndex < 0 || hotbarIndex >= HotbarSlotCount )
			return;

		if ( !ThornsItemRegistry.TryGet( itemId, out var def ) )
		{
			// Some sessions hot-reload against an older cached registry; keep spawn loadout resilient for known dev weapons.
			if ( string.Equals( itemId, "sniper", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new ThornsItemRegistry.ThornsItemDefinition(
					Id: "sniper",
					DisplayName: "Sniper",
					MaxStack: 1,
					ItemType: ThornsItemType.Weapon,
					CombatWeaponDefinitionId: "sniper",
					ViewModelAsset: ThornsViewModelController.SniperFirstPersonViewmodelPath,
					WorldModelAsset: ThornsViewModelController.SniperWorldModelPath );
				Log.Warning( "[Thorns] Spawn loadout: 'sniper' missing from runtime registry; using resilient fallback definition for this session." );
			}
			else if ( string.Equals( itemId, "m9_bayonet", StringComparison.OrdinalIgnoreCase ) )
			{
				def = new ThornsItemRegistry.ThornsItemDefinition(
					Id: "m9_bayonet",
					DisplayName: "M9 Bayonet",
					MaxStack: 1,
					ItemType: ThornsItemType.Weapon,
					CombatWeaponDefinitionId: "m9_bayonet",
					ViewModelAsset: ThornsViewModelController.BayonetM9FirstPersonViewmodelPath,
					WorldModelAsset: ThornsViewModelController.BayonetM9WorldModelPath );
				Log.Warning( "[Thorns] Spawn loadout: 'm9_bayonet' missing from runtime registry; using resilient fallback definition for this session." );
			}
			else if ( string.Equals( itemId, "primitive_tool", StringComparison.OrdinalIgnoreCase ) )
			{
				def = ThornsItemRegistry.PrimitiveToolDefinition;
				Log.Warning(
					"[Thorns] Spawn loadout: 'primitive_tool' missing from runtime registry (stale DLL); using resilient fallback for this session." );
			}
			else
			{
			Log.Warning(
				$"[Thorns] Spawn loadout: unknown item '{itemId}' — add an entry to ThornsItemRegistry or rebuild so the game loads the latest code DLL." );
			return;
			}
		}

		if ( def.ItemType != ThornsItemType.Weapon && def.ItemType != ThornsItemType.Tool )
		{
			Log.Warning(
				$"[Thorns] Spawn loadout: '{itemId}' is {def.ItemType}, not Weapon/Tool — cannot put it in hotbar slot {hotbarIndex}" );
			return;
		}

		_slots[hotbarIndex] = CreateNewStackSlot( def, itemId, 1, true );
	}
}
