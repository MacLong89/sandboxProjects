namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Combat;
using Terraingen.Combat.Attachments;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Inventory containers, moves, item grants, and UI sync (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public ThornsInventoryContainer Inventory => _inventory;

	string _lastPushedInventoryJson = "";
	string _lastPushedCraftJson = "";
	bool _inventorySyncDirty = true;
	string _lastFpPresentationRefreshKey = "";
	double _nextThrottledInventoryPushTime;

	void MarkInventorySyncDirty() => _inventorySyncDirty = true;

	public void FlushThrottledInventorySyncIfDirty( float intervalSeconds = 0.4f, bool force = false )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		if ( !force )
		{
			if ( !_inventorySyncDirty )
				return;

			if ( Time.Now < _nextThrottledInventoryPushTime )
				return;
		}

		_nextThrottledInventoryPushTime = Time.Now + intervalSeconds;
		PushInventoryToOwner( force );
	}

	public void PatchOwnerHotbarWeaponUi(
		int hotbarIndex,
		in ThornsItemStack stack,
		string combatId,
		ThornsWeaponDefinitions.WeaponDefinition def )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var clipSize = ThornsWeaponEffectiveStats.ResolveClipSize( def, combatId, stack );
		var broken = stack.IsWeaponBroken( combatId );

		if ( IsLocalPlayer() || !Networking.IsActive )
		{
			ThornsUiClientState.PatchHotbarSlotWeaponState(
				hotbarIndex,
				stack.WeaponLoadedAmmo,
				stack.Durability,
				stack.HasDurability,
				broken,
				clipSize );
		}

		if ( Networking.IsActive && !IsLocalPlayer() )
			RpcPatchHotbarWeaponUi(
				hotbarIndex,
				stack.WeaponLoadedAmmo,
				stack.Durability,
				stack.HasDurability,
				broken,
				clipSize );
	}

	[Rpc.Owner]
	void RpcPatchHotbarWeaponUi(
		int hotbarIndex,
		int loadedAmmo,
		float durability,
		bool hasDurability,
		bool weaponBroken,
		int weaponClipSize )
	{
		ThornsUiClientState.PatchHotbarSlotWeaponState(
			hotbarIndex,
			loadedAmmo,
			durability,
			hasDurability,
			weaponBroken,
			weaponClipSize );
	}

	bool ShouldRefreshFpPresentationFromInventoryPush( ThornsInventorySnapshotDto inv )
	{
		if ( inv is null )
			return true;

		var idx = Math.Clamp( inv.ActiveHotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		var itemId = "";
		for ( var i = 0; i < inv.Slots.Count; i++ )
		{
			var slot = inv.Slots[i];
			if ( slot.Container != ThornsContainerKind.Hotbar || slot.Index != idx )
				continue;

			itemId = slot.ItemId?.Trim() ?? "";
			break;
		}

		var key = $"{idx}:{itemId}";
		if ( string.Equals( key, _lastFpPresentationRefreshKey, StringComparison.Ordinal ) )
			return false;

		_lastFpPresentationRefreshKey = key;
		return true;
	}

	public void HostConsumeAmmoItems( string ammoTypeId, int count )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || count <= 0 || string.IsNullOrWhiteSpace( ammoTypeId ) )
			return;

		HostRemoveItemCount( ammoTypeId.Trim(), count );
	}

	public void HostNormalizeWeaponRows()
	{
		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
			NormalizeSlot( ThornsContainerKind.Hotbar, i );

		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
			NormalizeSlot( ThornsContainerKind.Inventory, i );

		NormalizeSlot( ThornsContainerKind.Head, 0 );
		NormalizeSlot( ThornsContainerKind.Chest, 0 );
		NormalizeSlot( ThornsContainerKind.Legs, 0 );
		HostRefreshEquippedArmor();
	}

	void NormalizeSlot( ThornsContainerKind kind, int index )
	{
		var stack = _inventory.GetSlot( kind, index );
		if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return;

		if ( ThornsItemTier.SupportsTiering( def ) && stack.ItemTier <= 0 )
			ThornsItemTier.ApplyCraftDefaults( ref stack, def );

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId );
		ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, combatId );
		ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref stack, def );
		_inventory.SetSlot( kind, index, stack );
	}

	public void PushInventoryToOwner( bool force = false )
	{
		if ( !CanPushOwnerRpcs() && Networking.IsActive && !IsLocalPlayer() )
			return;

		if ( !force && !_inventorySyncDirty )
			return;

		if ( ThornsMultiplayer.IsHostOrOffline )
			HostRefreshEquippedArmor();

		var invSnapshot = _inventory.ToSnapshot( _craftPanelExpanded, _craftCategory, _selectedRecipeId, _activeHotbarIndex );
		var craftSnapshot = _craftQueue.ToSnapshot( _nearestStation );

		if ( !Networking.IsActive )
		{
			ThornsUiClientState.ApplyPartialInventory( invSnapshot, craftSnapshot );
			_inventorySyncDirty = false;
			return;
		}

		var invJson = Json.Serialize( invSnapshot );
		var craftJson = Json.Serialize( craftSnapshot );

		if ( !force && invJson == _lastPushedInventoryJson && craftJson == _lastPushedCraftJson )
			return;

		_lastPushedInventoryJson = invJson;
		_lastPushedCraftJson = craftJson;
		_inventorySyncDirty = false;

		// Listen-server / offline: apply immediately so container UI reflects moves on the same frame.
		if ( IsLocalPlayer() )
		{
			ThornsUiClientState.ApplyPartialInventory( invSnapshot, craftSnapshot );
			if ( ShouldRefreshFpPresentationFromInventoryPush( invSnapshot ) )
				Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
			return;
		}

		RpcSyncInventoryJson( invJson, craftJson );
	}

	[Rpc.Owner]
	void RpcSyncInventoryJson( string invJson, string craftJson )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( invJson, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsInventorySnapshotDto inv )
		     || !ThornsNetAuthority.TryDeserializeJson( craftJson, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsCraftSnapshotDto craft ) )
			return;

		ThornsUiClientState.ApplyPartialInventory( inv, craft );

		if ( IsLocalPlayer() && ShouldRefreshFpPresentationFromInventoryPush( inv ) )
			Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	public void RequestMoveItem( ThornsMoveItemRequest req )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcMoveItem( req );
		else
			HostMoveItem( req );
	}

	public void HostGrantHarvestItem( string itemId, int count )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() || count <= 0 || string.IsNullOrWhiteSpace( itemId ) )
			return;

		MarkInventorySyncDirty();
		HostAddItem( itemId, count );
		HostInitializeMatchingWeaponStacks( itemId );
		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		PushLootFeedToOwner( itemId, count );
	}

	/// <summary>Host-only: place one exact stack into the first empty inventory or hotbar slot.</summary>
	public bool HostTryGrantItemStack( ThornsItemStack stack )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || stack.IsEmpty )
			return false;

		stack = ThornsItemIdAliases.CanonicalizeStack( stack );
		if ( ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
		{
			var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId );
			ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, combatId );
			ThornsInventoryWeaponState.EnsureToolDurabilityInitialized( ref stack, def );
		}

		foreach ( var kind in new[] { ThornsContainerKind.Inventory, ThornsContainerKind.Hotbar } )
		{
			var max = kind == ThornsContainerKind.Inventory
				? ThornsInventoryContainer.InventorySlotCount
				: ThornsInventoryContainer.HotbarSlotCount;

			for ( var i = 0; i < max; i++ )
			{
				if ( !_inventory.GetSlot( kind, i ).IsEmpty )
					continue;

				_inventory.SetSlot( kind, i, stack );
				return true;
			}
		}

		return false;
	}

	/// <summary>Host-only: move all hotbar, inventory, and armor items into a new list and clear the player.</summary>
	public List<ThornsItemStack> HostExtractAllCarriedItems()
	{
		var items = new List<ThornsItemStack>();
		ExtractContainer( ThornsContainerKind.Hotbar, ThornsInventoryContainer.HotbarSlotCount, items );
		ExtractContainer( ThornsContainerKind.Inventory, ThornsInventoryContainer.InventorySlotCount, items );
		ExtractContainer( ThornsContainerKind.Head, 1, items );
		ExtractContainer( ThornsContainerKind.Chest, 1, items );
		ExtractContainer( ThornsContainerKind.Legs, 1, items );

		if ( items.Count > 0 )
		{
			ThornsMilestoneTracker.OnInventoryChanged( this );
			MarkInventorySyncDirty();
			PushInventoryToOwner();
			if ( IsLocalPlayer() )
				Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
			HostPersistPlayerState();
		}

		return items;
	}

	void ExtractContainer( ThornsContainerKind kind, int slotCount, List<ThornsItemStack> items )
	{
		for ( var i = 0; i < slotCount; i++ )
		{
			var index = kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
				? _inventory.ArmorIndexFor( kind )
				: i;
			var stack = _inventory.GetSlot( kind, index );
			if ( stack.IsEmpty )
				continue;

			items.Add( stack );
			_inventory.SetSlot( kind, index, ThornsItemStack.EmptyStack );
		}
	}

	/// <summary>After granting a weapon item, ensure clip/durability rows are initialized.</summary>
	public void HostInitializeMatchingWeaponStacks( string itemId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( itemId ) )
			return;

		var def = ThornsDefinitionRegistry.GetItem( itemId );
		if ( def is null || def.Category != ThornsItemCategory.Weapon )
			return;

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, itemId );
		InitializeWeaponStacksInContainer( ThornsContainerKind.Inventory, combatId, itemId );
		InitializeWeaponStacksInContainer( ThornsContainerKind.Hotbar, combatId, itemId );
	}

	void InitializeWeaponStacksInContainer( ThornsContainerKind kind, string combatId, string itemId )
	{
		var slotCount = kind == ThornsContainerKind.Hotbar
			? ThornsInventoryContainer.HotbarSlotCount
			: ThornsInventoryContainer.InventorySlotCount;

		for ( var i = 0; i < slotCount; i++ )
		{
			var stack = _inventory.GetSlot( kind, i );
			if ( stack.IsEmpty || !string.Equals( stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			ThornsInventoryWeaponState.EnsureWeaponRowInitialized( ref stack, combatId );
			_inventory.SetSlot( kind, i, stack );
		}
	}

	void PushLootFeedToOwner( string itemId, int count )
	{
		if ( !Networking.IsActive )
		{
			ThornsLootFeedBus.Push( itemId, count );
			return;
		}

		RpcLootFeedPickup( itemId, count );
	}

	public void HostPushTameFeedToOwner( string speciesKey, string speciesName, int tier )
	{
		if ( !Networking.IsActive )
		{
			ThornsLootFeedBus.PushTame( speciesKey, speciesName, tier );
			return;
		}

		RpcTameFeedPickup( speciesKey, speciesName, tier );
	}

	[Rpc.Owner]
	void RpcLootFeedPickup( string itemId, int count )
	{
		ThornsLootFeedBus.Push( itemId, count );
	}

	[Rpc.Owner]
	void RpcTameFeedPickup( string speciesKey, string speciesName, int tier )
	{
		ThornsLootFeedBus.PushTame( speciesKey, speciesName, tier );
	}

	[Rpc.Host]
	void RpcMoveItem( ThornsMoveItemRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostMoveItem( req );
	}

	public void HostMoveItem( ThornsMoveItemRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
			return;

		MarkInventorySyncDirty();

		if ( HostMoveUsesWorldContainer( req ) )
		{
			HostMoveItemWithWorldContainer( req );
			return;
		}

		if ( HostMoveUsesStation( req ) )
		{
			HostMoveItemWithStation( req );
			return;
		}

		if ( req.Mode == ThornsMoveItemMode.QuickTransfer || req.ShiftHeld )
		{
			HostQuickTransfer( req.FromContainer, req.FromIndex );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			HostPersistPlayerState();
			return;
		}

		if ( req.Mode == ThornsMoveItemMode.DoubleClickTransfer )
		{
			HostDoubleClickTransfer( req.FromContainer, req.FromIndex );
			ThornsMilestoneTracker.OnInventoryChanged( this );
			PushInventoryToOwner();
			HostPersistPlayerState();
			return;
		}

		var from = _inventory.GetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, req.FromIndex ) );
		if ( from.IsEmpty )
			return;

		var toIndex = NormalizeIndex( req.ToContainer, req.ToIndex );
		var to = _inventory.GetSlot( req.ToContainer, toIndex );

		if ( req.Mode == ThornsMoveItemMode.SplitHalf )
		{
			var half = from.Count / 2;
			if ( half <= 0 )
				return;

			if ( to.IsEmpty )
			{
				_inventory.SetSlot( req.ToContainer, toIndex, ThornsInventoryWeaponState.CopyStackWithCount( from, half ) );
				_inventory.SetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, req.FromIndex ),
					ThornsInventoryWeaponState.CopyStackWithCount( from, from.Count - half ) );
			}
		}
		else if ( req.Mode == ThornsMoveItemMode.SplitAmount && req.SplitAmount > 0 )
		{
			var amt = Math.Min( req.SplitAmount, from.Count - 1 );
			if ( to.IsEmpty && amt > 0 )
			{
				_inventory.SetSlot( req.ToContainer, toIndex, ThornsInventoryWeaponState.CopyStackWithCount( from, amt ) );
				_inventory.SetSlot( req.FromContainer, NormalizeIndex( req.FromContainer, req.FromIndex ),
					ThornsInventoryWeaponState.CopyStackWithCount( from, from.Count - amt ) );
			}
		}
		else
		{
			TrySwapOrMerge( req.FromContainer, NormalizeIndex( req.FromContainer, req.FromIndex ), req.ToContainer, toIndex );
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		HostPersistPlayerState();
	}

	int NormalizeIndex( ThornsContainerKind kind, int index )
	{
		if ( kind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs )
			return _inventory.ArmorIndexFor( kind );

		return index;
	}

	void TrySwapOrMerge( ThornsContainerKind fromKind, int fromIdx, ThornsContainerKind toKind, int toIdx )
	{
		var from = ThornsItemIdAliases.CanonicalizeStack( _inventory.GetSlot( fromKind, fromIdx ) );
		if ( from.IsEmpty )
			return;

		var to = ThornsItemIdAliases.CanonicalizeStack( _inventory.GetSlot( toKind, toIdx ) );
		var def = ThornsDefinitionRegistry.GetItem( from.ItemId );
		if ( def is null )
		{
			Log.Warning( $"[Thorns] Move rejected — no definition for item '{from.ItemId}'." );
			return;
		}

		if ( toKind is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs )
		{
			if ( def.EquipSlot != MapEquipSlot( toKind ) )
				return;
		}

		if ( !to.IsEmpty && to.ItemId == from.ItemId && ThornsItemTier.StacksMatchForMerge( from, to, def ) )
		{
			var max = def.MaxStack;
			var space = max - to.Count;
			if ( space <= 0 )
				return;

			var move = Math.Min( space, from.Count );
			_inventory.SetSlot( toKind, toIdx, ThornsInventoryWeaponState.CopyStackWithCount( to, to.Count + move ) );
			var left = from.Count - move;
			_inventory.SetSlot( fromKind, fromIdx, left > 0
				? ThornsInventoryWeaponState.CopyStackWithCount( from, left )
				: ThornsItemStack.EmptyStack );
			return;
		}

		if ( !to.IsEmpty )
		{
			_inventory.SetSlot( fromKind, fromIdx, to );
			_inventory.SetSlot( toKind, toIdx, from );
			return;
		}

		_inventory.SetSlot( toKind, toIdx, from );
		_inventory.SetSlot( fromKind, fromIdx, ThornsItemStack.EmptyStack );
	}

	static ThornsEquipSlot MapEquipSlot( ThornsContainerKind kind ) => kind switch
	{
		ThornsContainerKind.Head => ThornsEquipSlot.Head,
		ThornsContainerKind.Chest => ThornsEquipSlot.Chest,
		ThornsContainerKind.Legs => ThornsEquipSlot.Legs,
		_ => ThornsEquipSlot.None
	};

	void HostQuickTransfer( ThornsContainerKind from, int fromIndex )
	{
		var stack = ThornsItemIdAliases.CanonicalizeStack( _inventory.GetSlot( from, NormalizeIndex( from, fromIndex ) ) );
		if ( stack.IsEmpty )
			return;

		if ( from is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.Hotbar )
		{
			TryMoveToFirstFree( stack, ThornsContainerKind.Inventory, from, fromIndex );
			return;
		}

		var def = ThornsDefinitionRegistry.GetItem( stack.ItemId );
		if ( def?.EquipSlot is ThornsEquipSlot.Head or ThornsEquipSlot.Chest or ThornsEquipSlot.Legs )
		{
			var armorKind = def.EquipSlot switch
			{
				ThornsEquipSlot.Head => ThornsContainerKind.Head,
				ThornsEquipSlot.Chest => ThornsContainerKind.Chest,
				_ => ThornsContainerKind.Legs
			};
			TrySwapOrMerge( from, NormalizeIndex( from, fromIndex ), armorKind, _inventory.ArmorIndexFor( armorKind ) );
			return;
		}

		TryMoveToFirstFree( stack, ThornsContainerKind.Hotbar, from, fromIndex );
	}

	void HostDoubleClickTransfer( ThornsContainerKind from, int fromIndex )
	{
		HostQuickTransfer( from, fromIndex );
	}

	void TryMoveToFirstFree( ThornsItemStack stack, ThornsContainerKind targetKind, ThornsContainerKind fromKind, int fromIndex )
	{
		var fromSlot = NormalizeIndex( fromKind, fromIndex );
		if ( fromKind is ThornsContainerKind.Inventory or ThornsContainerKind.Hotbar
		     && targetKind is ThornsContainerKind.Inventory or ThornsContainerKind.Hotbar )
		{
			var searchMode = targetKind == ThornsContainerKind.Hotbar
				? ThornsInventoryQuickTransfer.PlayerStorageSearchMode.HotbarOnly
				: ThornsInventoryQuickTransfer.PlayerStorageSearchMode.InventoryOnly;

			ThornsInventoryQuickTransfer.TryQuickTransferToPlayerStorage(
				_ => _inventory.GetSlot( fromKind, fromSlot ),
				( _, s ) => _inventory.SetSlot( fromKind, fromSlot, s ),
				0,
				( container, slotIndex ) => _inventory.GetSlot( container, slotIndex ),
				( container, slotIndex, s ) => _inventory.SetSlot( container, slotIndex, s ),
				searchMode,
				fromKind,
				fromSlot,
				excludeSlot: true );

			return;
		}

		var max = targetKind == ThornsContainerKind.Hotbar
			? ThornsInventoryContainer.HotbarSlotCount
			: ThornsInventoryContainer.InventorySlotCount;
		var singleTargetIndex = ThornsInventoryQuickTransfer.FindQuickDepositIndex(
			stack,
			i => _inventory.GetSlot( targetKind, i ),
			max );
		if ( singleTargetIndex < 0 )
			return;

		TrySwapOrMerge( fromKind, fromSlot, targetKind, singleTargetIndex );
	}

	public int HostCountItem( string itemId )
	{
		var total = 0;
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount; i++ )
		{
			var s = _inventory.GetSlot( ThornsContainerKind.Inventory, i );
			if ( s.ItemId == itemId )
				total += s.Count;
		}

		for ( var i = 0; i < ThornsInventoryContainer.HotbarSlotCount; i++ )
		{
			var s = _inventory.GetSlot( ThornsContainerKind.Hotbar, i );
			if ( s.ItemId == itemId )
				total += s.Count;
		}

		return total;
	}

	public bool HostCanConsumeItems( IReadOnlyList<(string ItemId, int Count)> costs )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || costs is null )
			return false;

		foreach ( var cost in costs )
		{
			if ( string.IsNullOrWhiteSpace( cost.ItemId ) || cost.Count <= 0 )
				continue;

			if ( HostCountItem( cost.ItemId ) < cost.Count )
				return false;
		}

		return true;
	}

	public bool HostTryConsumeItems( IReadOnlyList<(string ItemId, int Count)> costs )
	{
		if ( !HostCanConsumeItems( costs ) )
			return false;

		foreach ( var cost in costs )
		{
			if ( string.IsNullOrWhiteSpace( cost.ItemId ) || cost.Count <= 0 )
				continue;

			HostRemoveItemCount( cost.ItemId, cost.Count );
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		HostPersistPlayerState();
		return true;
	}

	void HostRemoveItemCount( string itemId, int count )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || count <= 0 )
			return;

		MarkInventorySyncDirty();
		var remaining = count;
		for ( var c = 0; c < 2 && remaining > 0; c++ )
		{
			var kind = c == 0 ? ThornsContainerKind.Inventory : ThornsContainerKind.Hotbar;
			var max = kind == ThornsContainerKind.Inventory ? ThornsInventoryContainer.InventorySlotCount : ThornsInventoryContainer.HotbarSlotCount;
			for ( var i = 0; i < max && remaining > 0; i++ )
			{
				var s = _inventory.GetSlot( kind, i );
				if ( s.ItemId != itemId )
					continue;

				var take = Math.Min( remaining, s.Count );
				remaining -= take;
				var left = s.Count - take;
				_inventory.SetSlot( kind, i, left > 0
					? new ThornsItemStack { ItemId = itemId, Count = left }
					: ThornsItemStack.EmptyStack );
			}
		}
	}

	void HostAddItem( string itemId, int count )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || count <= 0 )
			return;

		MarkInventorySyncDirty();
		var remaining = count;
		for ( var i = 0; i < ThornsInventoryContainer.InventorySlotCount && remaining > 0; i++ )
		{
			var s = _inventory.GetSlot( ThornsContainerKind.Inventory, i );
			var def = ThornsDefinitionRegistry.GetItem( itemId );
			if ( def is null )
				return;

			if ( !s.IsEmpty && s.ItemId != itemId )
				continue;

			if ( s.IsEmpty )
			{
				var put = Math.Min( remaining, def.MaxStack );
				var stack = new ThornsItemStack { ItemId = itemId, Count = put };
				if ( ThornsItemTier.SupportsTiering( def ) )
					ThornsItemTier.ApplyCraftDefaults( ref stack, def );
				_inventory.SetSlot( ThornsContainerKind.Inventory, i, stack );
				remaining -= put;
			}
			else if ( ThornsItemTier.StacksMatchForMerge( s, new ThornsItemStack { ItemId = itemId, ItemTier = s.ItemTier, StatRoll = s.StatRoll }, def ) )
			{
				var space = def.MaxStack - s.Count;
				if ( space <= 0 )
					continue;

				var put = Math.Min( space, remaining );
				_inventory.SetSlot( ThornsContainerKind.Inventory, i, ThornsInventoryWeaponState.CopyStackWithCount( s, s.Count + put ) );
				remaining -= put;
			}
		}

		var added = count - remaining;
		if ( added > 0 )
			ThornsMilestoneTracker.OnItemCollected( this, itemId, added );
	}

	public void RequestEquipAttachment( ThornsEquipAttachmentRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
		{
			ThornsAttachmentDragDebug.LogReject( "RequestEquipAttachment", "not local player or null request" );
			return;
		}

		ThornsAttachmentDragDebug.Write(
			$"RequestEquipAttachment weapon={req.WeaponContainer}[{req.WeaponIndex}] attachSlot={req.AttachmentSlotIndex} from={req.FromContainer}[{req.FromIndex}] unequip={req.Unequip}" );

		if ( Networking.IsActive && !Networking.IsHost )
			RpcEquipAttachment( req );
		else
			HostEquipAttachment( req );
	}

	[Rpc.Host]
	void RpcEquipAttachment( ThornsEquipAttachmentRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostEquipAttachment( req );
	}

	public void HostEquipAttachment( ThornsEquipAttachmentRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
		{
			ThornsAttachmentDragDebug.LogReject( "HostEquipAttachment", "not host, null req, or dead" );
			return;
		}

		if ( req.WeaponContainer is ThornsContainerKind.Head or ThornsContainerKind.Chest or ThornsContainerKind.Legs
		     or ThornsContainerKind.WorldLoot )
		{
			ThornsAttachmentDragDebug.LogReject( "HostEquipAttachment", $"invalid weapon container {req.WeaponContainer}" );
			return;
		}

		MarkInventorySyncDirty();

		var weaponIdx = NormalizeIndex( req.WeaponContainer, req.WeaponIndex );
		var weaponStack = _inventory.GetSlot( req.WeaponContainer, weaponIdx );
		if ( weaponStack.IsEmpty || !ThornsItemRegistry.TryGet( weaponStack.ItemId, out var weaponDef )
		     || weaponDef.Category != ThornsItemCategory.Weapon )
		{
			ThornsAttachmentDragDebug.LogReject(
				"HostEquipAttachment",
				$"weapon slot empty/invalid {req.WeaponContainer}[{weaponIdx}] item='{weaponStack.ItemId}'" );
			return;
		}

		var combatId = ThornsInventoryWeaponState.ResolveCombatId( weaponDef, weaponStack.ItemId );

		if ( req.Unequip )
		{
			var slotIndex = req.AttachmentSlotIndex;
			if ( slotIndex < 0 || slotIndex >= ThornsAttachmentCatalog.MaxSlotsPerWeapon )
				return;

			var removedId = ThornsWeaponAttachmentState.ClearAttachmentAtSlot( ref weaponStack, slotIndex, combatId );
			if ( string.IsNullOrWhiteSpace( removedId ) )
				return;

			ThornsInventoryWeaponState.ClampLoadedAmmoToClip( ref weaponStack, combatId );
			_inventory.SetSlot( req.WeaponContainer, weaponIdx, weaponStack );

			if ( !HostTryGrantAttachmentToInventory( removedId ) )
			{
				ThornsWeaponAttachmentState.SetAttachmentItemIdAtSlot( ref weaponStack, slotIndex, removedId, combatId );
				_inventory.SetSlot( req.WeaponContainer, weaponIdx, weaponStack );
				return;
			}
		}
		else
		{
			var fromIdx = NormalizeIndex( req.FromContainer, req.FromIndex );
			var attachmentStack = ThornsItemIdAliases.CanonicalizeStack( _inventory.GetSlot( req.FromContainer, fromIdx ) );
			if ( attachmentStack.IsEmpty || !ThornsItemRegistry.TryGet( attachmentStack.ItemId, out var attachmentDef )
			     || attachmentDef.Category != ThornsItemCategory.Attachment )
			{
				ThornsAttachmentDragDebug.LogReject(
					"HostEquipAttachment",
					$"from slot empty/not attachment {req.FromContainer}[{fromIdx}] item='{attachmentStack.ItemId}'" );
				return;
			}

			if ( !ThornsAttachmentItemIds.TryParseItemId( attachmentStack.ItemId, out var attachmentId )
			     || !ThornsAttachmentCatalog.IsCompatible( combatId, attachmentId ) )
			{
				ThornsAttachmentDragDebug.LogReject(
					"HostEquipAttachment",
					$"parse/compat failed item='{attachmentStack.ItemId}' combatId={combatId}" );
				return;
			}

			var slotIndex = req.AttachmentSlotIndex;
			if ( slotIndex < 0 )
			{
				for ( var i = 0; i < ThornsAttachmentCatalog.MaxSlotsPerWeapon; i++ )
				{
					if ( string.IsNullOrWhiteSpace( ThornsWeaponAttachmentState.GetAttachmentItemIdAtSlot( weaponStack, i ) ) )
					{
						slotIndex = i;
						break;
					}
				}

				if ( slotIndex < 0 )
					slotIndex = 0;
			}

			if ( slotIndex < 0 || slotIndex >= ThornsAttachmentCatalog.MaxSlotsPerWeapon )
				return;

			var displacedId = ThornsWeaponAttachmentState.GetAttachmentItemIdAtSlot( weaponStack, slotIndex );
			ThornsWeaponAttachmentState.SetAttachmentItemIdAtSlot( ref weaponStack, slotIndex, attachmentStack.ItemId, combatId );
			ThornsInventoryWeaponState.ClampLoadedAmmoToClip( ref weaponStack, combatId );
			_inventory.SetSlot( req.WeaponContainer, weaponIdx, weaponStack );
			_inventory.SetSlot( req.FromContainer, fromIdx, ThornsItemStack.EmptyStack );

			if ( !string.IsNullOrWhiteSpace( displacedId ) && displacedId != attachmentStack.ItemId
			     && !HostTryGrantAttachmentToInventory( displacedId ) )
			{
				ThornsWeaponAttachmentState.SetAttachmentItemIdAtSlot( ref weaponStack, slotIndex, displacedId, combatId );
				_inventory.SetSlot( req.WeaponContainer, weaponIdx, weaponStack );
				_inventory.SetSlot( req.FromContainer, fromIdx, attachmentStack );
			}
		}

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		HostPersistPlayerState();
		if ( IsLocalPlayer() )
			Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();

		ThornsAttachmentDragDebug.Write( $"HostEquipAttachment OK weapon={req.WeaponContainer}[{weaponIdx}] slot={req.AttachmentSlotIndex}" );
	}

	public void RequestRepairItem( ThornsRepairItemRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		RequestSelectWorkbenchItem( req.Container, req.Index );
		RequestStartWorkbenchRepair();
	}

	public void RequestUpgradeItem( ThornsUpgradeItemRequest req )
	{
		if ( !IsLocalPlayer() || req is null )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcUpgradeItem( req );
		else
			HostUpgradeItem( req );
	}

	[Rpc.Host]
	void RpcUpgradeItem( ThornsUpgradeItemRequest req )
	{
		if ( !ValidateCaller() )
			return;

		HostUpgradeItem( req );
	}

	public void HostUpgradeItem( ThornsUpgradeItemRequest req )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || req is null || HostIsDead() )
			return;

		if ( !HostValidateOpenWorkbench() )
			return;

		if ( req.Container is ThornsContainerKind.WorldLoot )
			return;

		MarkInventorySyncDirty();
		ThornsItemStack stack;
		if ( req.Container == ThornsContainerKind.WorkbenchStation )
		{
			if ( req.Index < 0 || req.Index >= _workbenchStation.Length )
				return;

			stack = _workbenchStation[req.Index];
		}
		else
		{
			var idx = NormalizeIndex( req.Container, req.Index );
			stack = _inventory.GetSlot( req.Container, idx );
		}

		if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
			return;

		if ( !ThornsItemTier.CanUpgrade( stack, def ) )
			return;

		var tier = ThornsItemTier.ResolveTier( stack, def );
		foreach ( var cost in ThornsItemTier.GetUpgradeCost( tier ) )
		{
			if ( HostCountItem( cost.ItemId ) < cost.Count )
				return;
		}

		foreach ( var cost in ThornsItemTier.GetUpgradeCost( tier ) )
			HostRemoveItemCount( cost.ItemId, cost.Count );

		ThornsItemTier.ApplyUpgrade( ref stack, def );
		if ( req.Container == ThornsContainerKind.WorkbenchStation )
			_workbenchStation[req.Index] = stack;
		else
			_inventory.SetSlot( req.Container, NormalizeIndex( req.Container, req.Index ), stack );

		ThornsMilestoneTracker.OnInventoryChanged( this );
		PushInventoryToOwner();
		PushWorkbenchToOwner();
		HostPersistPlayerState();
	}

	public void HostRefreshEquippedArmor()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		var total = 0f;
		foreach ( var kind in new[] { ThornsContainerKind.Head, ThornsContainerKind.Chest, ThornsContainerKind.Legs } )
		{
			var stack = _inventory.GetSlot( kind, 0 );
			if ( stack.IsEmpty || !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) )
				continue;

			total += ThornsItemTier.ResolveArmorProtection( stack, def );
		}

		var receiver = Components.Get<ThornsPlayerDamageReceiver>();
		if ( receiver.IsValid() )
			receiver.ArmorDamageReduction = Math.Clamp( total, 0f, 0.55f );
	}

	bool HostTryGrantAttachmentToInventory( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		foreach ( var kind in new[] { ThornsContainerKind.Inventory, ThornsContainerKind.Hotbar } )
		{
			var max = kind == ThornsContainerKind.Inventory
				? ThornsInventoryContainer.InventorySlotCount
				: ThornsInventoryContainer.HotbarSlotCount;

			for ( var i = 0; i < max; i++ )
			{
				if ( !_inventory.GetSlot( kind, i ).IsEmpty )
					continue;

				_inventory.SetSlot( kind, i, new ThornsItemStack { ItemId = itemId, Count = 1 } );
				return true;
			}
		}

		return false;
	}
}
