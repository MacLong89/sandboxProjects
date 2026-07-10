namespace Sandbox;

public interface IThornsInventoryConsumableHost
{
	bool ValidateRpcCallerOwnsPawn();
	bool IsValidSlot( int slotIndex );
	bool TryGetHostSlot( int index, out ThornsInventorySlot slot );
	bool IsPlayerDead();
	bool HasVitalsComponent();
	GameObject GameObject { get; }
	T GetComponent<T>() where T : Component;
	void NotifyConsumableRejected( string reason );
	void RpcNotifyOwnerConsumableApplied( string itemId, string kind, float hunger, float thirst, float poison );
	int ServerRemoveItem( int slotIndex, int quantity );
	Connection RpcCaller { get; }
}
