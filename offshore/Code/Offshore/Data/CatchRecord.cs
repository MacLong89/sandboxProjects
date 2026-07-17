namespace Offshore;

/// <summary>Runtime catch instance stored in the cooler until sold.</summary>
public sealed class CatchRecord
{
	public string FishId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public FishRarity Rarity { get; set; }
	public float Size { get; set; }
	public float Weight { get; set; }
	public float BaseValue { get; set; }
	public float FinalValue { get; set; }
	public string LocationId { get; set; } = "";
	public float WaterDepth { get; set; }
	public float Offshore01 { get; set; }
	public string BaitId { get; set; } = "";
	public TimeOfDay TimeOfDay { get; set; }
	public WeatherType Weather { get; set; }
	public string ConditionNote { get; set; } = "";
	public bool IsNewDiscovery { get; set; }
	public bool IsPersonalRecord { get; set; }
	public DateTime CaughtAt { get; set; } = DateTime.UtcNow;
}
