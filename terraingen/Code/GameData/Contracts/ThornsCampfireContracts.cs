namespace Terraingen.GameData;

public sealed class ThornsCampfireSnapshotDto
{
	public bool IsOpen { get; set; }
	public string InstanceKey { get; set; } = "";
	public int SmeltBatchesRemaining { get; set; }
	public float SmeltSecondsRemaining { get; set; }
	public float SmeltSecondsPerBatch { get; set; } = ThornsCampfireSmelt.SecondsPerBatch;
	public int OrePerIngot { get; set; } = ThornsCampfireSmelt.OrePerIngot;
	public int WoodPerBatch { get; set; } = ThornsCampfireSmelt.WoodPerBatch;
	public int IngotPerBatch { get; set; } = ThornsCampfireSmelt.IngotPerBatch;
	public List<ThornsInventorySlotDto> StationSlots { get; set; } = new();
}
