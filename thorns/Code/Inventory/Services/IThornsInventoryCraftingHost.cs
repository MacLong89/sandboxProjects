namespace Sandbox;

public interface IThornsInventoryCraftingHost
{
	bool ValidateRpcCallerOwnsPawn();
	bool IsPlayerDead();
	int ServerCountItemId( string itemId );
	int ServerRemoveItemId( string itemId, int quantity, bool suppressOwnerSnapshot = false );
	int ServerAddItem(
		string itemId,
		int quantity,
		bool rollWeaponInstanceForWeapons = true,
		bool suppressOwnerSnapshot = false,
		bool suppressMilestoneRecord = false );
	int FindFirstEmptySlot();
	bool HostCanFitStackableResourceQuantity( string itemId, int quantity );
	void PushSnapshotToOwner();
	void ClientCraftResultNotify( string status, string detail );
	ThornsPlayerUpgrades GetPlayerUpgrades();
	void HostRecordRecipeCrafted( string recipeId );
	T GetComponent<T>() where T : Component;
	Connection RpcCaller { get; }
}
