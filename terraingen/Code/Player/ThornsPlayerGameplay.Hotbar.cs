namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;

/// <summary>Active hotbar selection and slot accessors (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public bool TryGetActiveHotbarIndex( out int index )
	{
		index = _activeHotbarIndex;
		return true;
	}

	public ThornsItemStack GetHotbarSlot( int index ) =>
		_inventory.GetSlot( ThornsContainerKind.Hotbar, index );

	public void SetHotbarSlot( int index, ThornsItemStack stack, bool pushInventory = true )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		MarkInventorySyncDirty();
		index = Math.Clamp( index, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		_inventory.SetSlot( ThornsContainerKind.Hotbar, index, stack );
		if ( pushInventory )
			PushInventoryToOwner();
	}

	public void RequestHotbarSlot( int index )
	{
		if ( !IsLocalPlayer() )
			return;

		ApplyLocalHotbarIndex( index );

		if ( Networking.IsActive && !Networking.IsHost )
			RpcHotbarSlot( index );
		else
			HostActivateHotbarSlot( index );
	}

	public void RequestHotbarStep( int delta )
	{
		if ( delta == 0 )
			return;

		var next = (_activeHotbarIndex + delta) % ThornsInventoryContainer.HotbarSlotCount;
		if ( next < 0 )
			next += ThornsInventoryContainer.HotbarSlotCount;

		RequestHotbarSlot( next );
	}

	[Rpc.Host]
	void RpcHotbarSlot( int index )
	{
		if ( !ValidateCaller() )
			return;

		HostActivateHotbarSlot( index );
	}

	public bool TryGetActiveHotbarItemId( out string itemId )
	{
		itemId = "";
		var stack = _inventory.GetSlot( ThornsContainerKind.Hotbar, _activeHotbarIndex );
		if ( !stack.IsEmpty && !string.IsNullOrWhiteSpace( stack.ItemId ) )
		{
			itemId = stack.ItemId;
			return true;
		}

		if ( !IsLocalPlayer() || !ThornsUiClientState.HasSnapshot )
			return false;

		var inventory = ThornsUiClientState.Snapshot?.Inventory;
		var slots = inventory?.Slots;
		if ( slots is not { Count: > 0 } )
			return false;

		var idx = Math.Clamp( inventory.ActiveHotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		var slot = slots.FirstOrDefault( s =>
			s is not null && s.Container == ThornsContainerKind.Hotbar && s.Index == idx );

		if ( slot is null || string.IsNullOrWhiteSpace( slot.ItemId ) || slot.Count <= 0 )
			return false;

		itemId = slot.ItemId;
		return true;
	}

	public bool HostTryGetActiveHotbarItemId( out string itemId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
		{
			itemId = "";
			return false;
		}

		return TryGetActiveHotbarItemId( out itemId );
	}

	public void HostActivateHotbarSlot( int index )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		index = Math.Clamp( index, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		_activeHotbarIndex = index;

		var inventoryChanged = false;
		var stack = _inventory.GetSlot( ThornsContainerKind.Hotbar, index );
		if ( !stack.IsEmpty )
		{
			var def = ThornsDefinitionRegistry.GetItem( stack.ItemId );
			if ( def?.EquipSlot is ThornsEquipSlot.Head or ThornsEquipSlot.Chest or ThornsEquipSlot.Legs )
			{
				var armorKind = def.EquipSlot switch
				{
					ThornsEquipSlot.Head => ThornsContainerKind.Head,
					ThornsEquipSlot.Chest => ThornsContainerKind.Chest,
					_ => ThornsContainerKind.Legs
				};

				TrySwapOrMerge( ThornsContainerKind.Hotbar, index, armorKind, _inventory.ArmorIndexFor( armorKind ) );
				ThornsMilestoneTracker.OnInventoryChanged( this );
				PlayOwnerSfx( ThornsGameplaySfx.ArmorEquip );
				inventoryChanged = true;
			}
		}

		if ( inventoryChanged )
		{
			MarkInventorySyncDirty();
			PushInventoryToOwner();
		}
		else
			PushHotbarIndexToOwner();

		if ( IsLocalPlayer() )
			Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	void ApplyLocalHotbarIndex( int index )
	{
		index = Math.Clamp( index, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		if ( !IsLocalPlayer() || !ThornsUiClientState.HasSnapshot )
			return;

		if ( ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex == index )
			return;

		ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex = index;
		UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
		Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	void PushHotbarIndexToOwner()
	{
		if ( !Networking.IsActive )
		{
			ApplyLocalHotbarIndex( _activeHotbarIndex );
			return;
		}

		RpcSyncHotbarIndex( _activeHotbarIndex );
	}

	[Rpc.Owner]
	void RpcSyncHotbarIndex( int index )
	{
		index = Math.Clamp( index, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		if ( ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex == index )
			return;

		ThornsUiClientState.Snapshot.Inventory.ActiveHotbarIndex = index;
		UiRevisionBus.Publish( UiRevisionChannel.Hotbar );
		if ( IsLocalPlayer() )
			Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}

	public bool HostTryConsumePlacementCostsPreferHotbar(
		int hotbarIndex,
		IReadOnlyList<(string ItemId, int Count)> costs )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || costs is null or { Count: 0 } )
			return false;

		if ( !HostCanConsumeItems( costs ) )
			return false;

		if ( costs.Count == 1 )
		{
			var (itemId, count) = costs[0];
			if ( !string.IsNullOrWhiteSpace( itemId ) && count > 0 )
			{
				hotbarIndex = Math.Clamp( hotbarIndex, 0, ThornsInventoryContainer.HotbarSlotCount - 1 );
				var stack = _inventory.GetSlot( ThornsContainerKind.Hotbar, hotbarIndex );
				if ( !stack.IsEmpty
				     && string.Equals( stack.ItemId, itemId, StringComparison.OrdinalIgnoreCase )
				     && stack.Count >= count )
				{
					var left = stack.Count - count;
					_inventory.SetSlot( ThornsContainerKind.Hotbar, hotbarIndex, left > 0
						? new ThornsItemStack { ItemId = stack.ItemId, Count = left }
						: ThornsItemStack.EmptyStack );
					ThornsMilestoneTracker.OnInventoryChanged( this );
					PushInventoryToOwner();
					HostPersistPlayerState();
					return true;
				}
			}
		}

		return HostTryConsumeItems( costs );
	}
}
