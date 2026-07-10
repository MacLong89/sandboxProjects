namespace Dynasty.Persistence;

public sealed class SaveSlotInfo
{
	public string SlotId { get; set; } = "";
	public string LeagueName { get; set; } = "";
	public int Season { get; set; }
	public int Week { get; set; }
	public string Phase { get; set; } = "";
	public string UrgentLabel { get; set; } = "";
	public DateTime SavedUtc { get; set; }
	public string FilePath { get; set; } = "";
}
