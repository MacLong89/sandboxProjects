namespace Deep;

public sealed class UpgradeDefinition
{
	public string Id { get; init; }
	public string DisplayName { get; init; }
	public string Description { get; init; }
	public UpgradeCategory Category { get; init; }
	public int MaxLevel { get; init; } = 10;
	public float BasePrice { get; init; } = 40f;
	public float GrowthRate { get; init; } = 1.45f;
	public float EffectPerLevel { get; init; } = 1f;
	/// <summary>If &gt; 0, upgrade spends shells instead of gold.</summary>
	public float ShellBasePrice { get; init; }
}
