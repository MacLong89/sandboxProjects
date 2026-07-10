namespace Terraingen.GameData;

public sealed class ThornsWorkbenchSnapshotDto
{
	public bool IsOpen { get; set; }
	public string InstanceKey { get; set; } = "";
	public bool IsWorldFurniture { get; set; }

	public ThornsContainerKind SelectedContainer { get; set; }
	public int SelectedIndex { get; set; } = -1;
	public string SelectedItemId { get; set; } = "";

	public bool RepairInProgress { get; set; }
	public float RepairSecondsRemaining { get; set; }
	public float RepairSecondsPerJob { get; set; } = ThornsWorkbenchRepair.SecondsPerJob;
	public List<ThornsInventorySlotDto> StationSlots { get; set; } = new();
}

public sealed class ThornsRepairItemRequest
{
	public ThornsContainerKind Container { get; set; }
	public int Index { get; set; }
}

public sealed class ThornsSelectWorkbenchItemRequest
{
	public ThornsContainerKind Container { get; set; }
	public int Index { get; set; }
}
