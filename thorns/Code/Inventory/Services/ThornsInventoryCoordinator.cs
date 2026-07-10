namespace Sandbox;

/// <summary>Inventory orchestration — replication, consumable, crafting, storage services bound to inventory host.</summary>
public sealed class ThornsInventoryCoordinator
{
	public ThornsInventoryReplicationService Replication { get; } = new();
	public ThornsInventoryConsumableService Consumable { get; } = new();
	public ThornsInventoryCraftingService Crafting { get; } = new();
	public ThornsInventoryStorageService Storage { get; } = new();

	public void Bind( ThornsInventory inv )
	{
		Replication.Bind( inv );
		Consumable.Bind( inv );
		Crafting.Bind( inv );
		Storage.Bind( inv );
	}
}
