namespace Offshore;

/// <summary>Data-driven fish species definition. Behavior lives outside FishingController.</summary>
public sealed class FishDefinition
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Description { get; set; } = "";
	public FishRarity Rarity { get; set; } = FishRarity.Common;
	public float BaseValue { get; set; } = 5f;
	public float MinSize { get; set; } = 0.4f;
	public float MaxSize { get; set; } = 1.0f;
	public float MinWeight { get; set; } = 0.2f;
	public float MaxWeight { get; set; } = 2.0f;
	public string RequiredLocationId { get; set; } = "old_dock";
	public float MinDepth { get; set; } = 0.5f;
	public float MaxDepth { get; set; } = 12f;
	public float SpawnWeight { get; set; } = 1f;
	public float BiteSpeed { get; set; } = 1f;
	public float BiteCaution { get; set; } = 0.2f;
	public float Strength { get; set; } = 1f;
	public float Stamina { get; set; } = 1f;
	public float Speed { get; set; } = 1f;
	public float EscapeDifficulty { get; set; } = 0.3f;
	public float CapacityCost { get; set; } = 1f;
	public string SpritePath { get; set; } = "";
	public bool IsLegendary { get; set; }

	/// <summary>Behavior tags for bait/time/weather interactions (surface, bottom, pelagic, …).</summary>
	public string[] Tags { get; set; } = [];

	/// <summary>0–1 offshore band required before this species can appear (0 = anywhere).</summary>
	public float MinOffshore01 { get; set; }
}
