namespace Sandbox;

using System.Collections.Generic;

public sealed partial class ThornsInventory
	: IThornsInventoryReplicationHost
	, IThornsInventoryConsumableHost
	, IThornsInventoryCraftingHost
	, IThornsInventoryStorageHost
{
	readonly ThornsInventoryCoordinator _invServices = new();

	void BindInventoryServices() => _invServices.Bind( this );

	Guid IThornsInventoryReplicationHost.OwnerConnectionId => _hostOwnerConnectionId;

	int IThornsInventoryReplicationHost.TotalSlots => TotalSlots;

	bool IThornsInventoryReplicationHost.HostSnapshotTargetsListenServerLocalOwner() =>
		HostSnapshotTargetsListenServerLocalOwner();

	void IThornsInventoryReplicationHost.ApplyClientMirror( ThornsInventorySlotNet[] slots ) =>
		_invServices.Replication.ApplyInventoryClientMirror( slots );

	void IThornsInventoryReplicationHost.ApplyClientMirrorDelta( IReadOnlyList<ThornsInventorySlotChangeNet> changes ) =>
		_invServices.Replication.ApplyInventoryClientMirrorDelta( changes );

	void IThornsInventoryReplicationHost.RpcClientReceiveSnapshot( ThornsInventorySlotNet[] slots ) =>
		ClientReceiveInventorySnapshot( slots );

	void IThornsInventoryReplicationHost.RpcClientReceiveDelta( ThornsInventorySlotChangeNet[] delta ) =>
		ClientReceiveInventoryDelta( delta );

	void IThornsInventoryReplicationHost.OnMirrorUpdated()
	{
		if ( ThornsPawn.IsLocalConnectionOwner( this ) )
			Components.Get<ThornsHotbarEquipment>()?.ClientTryBootstrapEquipmentFromObservers();
	}

	ThornsInventorySlotNet IThornsInventoryReplicationHost.BuildSlotNet( ThornsInventorySlot slot ) => ToNet( slot );

	ref ThornsInventorySlot IThornsInventoryReplicationHost.HostGetSlotRef( int index ) => ref HostGetSlotRef( index );

	GameObject IThornsInventoryReplicationHost.GameObject => GameObject;

	bool IThornsInventoryConsumableHost.ValidateRpcCallerOwnsPawn() => ValidateRpcCallerOwnsPawn();

	bool IThornsInventoryConsumableHost.IsValidSlot( int slotIndex ) => IsValidSlot( slotIndex );

	bool IThornsInventoryConsumableHost.TryGetHostSlot( int index, out ThornsInventorySlot slot ) =>
		TryGetHostSlot( index, out slot );

	bool IThornsInventoryConsumableHost.IsPlayerDead()
	{
		var hp = Components.Get<ThornsHealth>();
		return hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive );
	}

	bool IThornsInventoryConsumableHost.HasVitalsComponent() => Components.Get<ThornsVitals>().IsValid();

	GameObject IThornsInventoryConsumableHost.GameObject => GameObject;

	T IThornsInventoryConsumableHost.GetComponent<T>() => Components.Get<T>();

	void IThornsInventoryConsumableHost.NotifyConsumableRejected( string reason )
	{
		Log.Warning( $"[Thorns] Use consumable rejected: {reason}" );
		ClientReceiveConsumableUseRejected( reason );
	}

	void IThornsInventoryConsumableHost.RpcNotifyOwnerConsumableApplied(
		string itemId,
		string kind,
		float hunger,
		float thirst,
		float poison ) =>
		RpcNotifyOwnerConsumableApplied( itemId, kind, hunger, thirst, poison );

	int IThornsInventoryConsumableHost.ServerRemoveItem( int slotIndex, int quantity ) =>
		ServerRemoveItem( slotIndex, quantity );

	Connection IThornsInventoryConsumableHost.RpcCaller => Rpc.Caller;

	bool IThornsInventoryCraftingHost.ValidateRpcCallerOwnsPawn() => ValidateRpcCallerOwnsPawn();

	bool IThornsInventoryCraftingHost.IsPlayerDead()
	{
		var hp = Components.Get<ThornsHealth>();
		return hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive );
	}

	int IThornsInventoryCraftingHost.ServerCountItemId( string itemId ) => ServerCountItemId( itemId );

	int IThornsInventoryCraftingHost.ServerRemoveItemId( string itemId, int quantity, bool suppressOwnerSnapshot ) =>
		ServerRemoveItemId( itemId, quantity, suppressOwnerSnapshot );

	int IThornsInventoryCraftingHost.ServerAddItem(
		string itemId,
		int quantity,
		bool rollWeaponInstanceForWeapons,
		bool suppressOwnerSnapshot,
		bool suppressMilestoneRecord ) =>
		ServerAddItem( itemId, quantity, rollWeaponInstanceForWeapons, suppressOwnerSnapshot, suppressMilestoneRecord );

	int IThornsInventoryCraftingHost.FindFirstEmptySlot() => FindFirstEmptySlot();

	bool IThornsInventoryCraftingHost.HostCanFitStackableResourceQuantity( string itemId, int quantity ) =>
		HostCanFitStackableResourceQuantity( itemId, quantity );

	void IThornsInventoryCraftingHost.PushSnapshotToOwner() => PushSnapshotToOwner();

	void IThornsInventoryCraftingHost.ClientCraftResultNotify( string status, string detail ) =>
		ClientCraftResultNotify( status, detail );

	ThornsPlayerUpgrades IThornsInventoryCraftingHost.GetPlayerUpgrades() => Components.Get<ThornsPlayerUpgrades>();

	void IThornsInventoryCraftingHost.HostRecordRecipeCrafted( string recipeId )
	{
		var msCraft = Components.Get<ThornsPlayerMilestones>();
		if ( msCraft.IsValid() )
			msCraft.HostRecordRecipeCrafted( recipeId );
	}

	T IThornsInventoryCraftingHost.GetComponent<T>() => Components.Get<T>();

	Connection IThornsInventoryCraftingHost.RpcCaller => Rpc.Caller;

	bool IThornsInventoryStorageHost.ValidateRpcCallerOwnsPawn() => ValidateRpcCallerOwnsPawn();

	bool IThornsInventoryStorageHost.IsPlayerDead()
	{
		var hp = Components.Get<ThornsHealth>();
		return hp.IsValid() && ( hp.IsDeadState || !hp.IsAlive );
	}

	GameObject IThornsInventoryStorageHost.GameObject => GameObject;

	ThornsInventory IThornsInventoryStorageHost.Inventory => this;

	void IThornsInventoryStorageHost.HostNotifyOpenBuildSfx( Vector3 worldEmit ) => HostNotifyOpenBuildSfx( worldEmit );
}
