namespace Offshore;

/// <summary>Runtime hooked-fish instance for bite and reel.</summary>
public sealed class FishEncounter
{
	public FishDefinition Definition { get; set; }
	public float Size { get; set; }
	public float Weight { get; set; }
	public float BaseValue { get; set; }
	public float FinalValue { get; set; }
	public float HookDepth { get; set; }
	public string LocationId { get; set; } = "";

	public float Offshore01 { get; set; }
	public string BaitId { get; set; } = "";
	public TimeOfDay TimeOfDay { get; set; }
	public WeatherType Weather { get; set; }
	public string ConditionNote { get; set; } = "";

	public float MaxStamina { get; set; } = 1f;
	public float Stamina { get; set; } = 1f;
	public float Strength { get; set; } = 1f;
	public float Speed { get; set; } = 1f;
	public float EscapeDifficulty { get; set; } = 0.3f;

	public float CatchProgress { get; set; }
	public float LineTension { get; set; }
	public float Direction { get; set; } = 1f;
	public float DirectionTimer { get; set; }

	public string DisplayName => Definition?.DisplayName ?? "Fish";
	public FishRarity Rarity => Definition?.Rarity ?? FishRarity.Common;

	public CatchRecord ToCatchRecord( bool isNewDiscovery, bool isPersonalRecord )
	{
		return new CatchRecord
		{
			FishId = Definition?.Id ?? "",
			DisplayName = DisplayName,
			Rarity = Rarity,
			Size = Size,
			Weight = Weight,
			BaseValue = BaseValue,
			FinalValue = FinalValue,
			LocationId = LocationId,
			WaterDepth = HookDepth,
			Offshore01 = Offshore01,
			BaitId = BaitId,
			TimeOfDay = TimeOfDay,
			Weather = Weather,
			ConditionNote = ConditionNote ?? "",
			IsNewDiscovery = isNewDiscovery,
			IsPersonalRecord = isPersonalRecord,
			CaughtAt = DateTime.UtcNow
		};
	}
}
