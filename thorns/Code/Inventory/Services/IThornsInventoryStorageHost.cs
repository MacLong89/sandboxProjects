namespace Sandbox;

public interface IThornsInventoryStorageHost
{
	bool ValidateRpcCallerOwnsPawn();
	bool IsPlayerDead();
	GameObject GameObject { get; }
	ThornsInventory Inventory { get; }
	void HostNotifyOpenBuildSfx( Vector3 worldEmit );
}
