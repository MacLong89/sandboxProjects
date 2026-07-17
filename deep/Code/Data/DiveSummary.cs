namespace Deep;

public sealed class DiveBonusLine
{
	public string Label { get; init; }
	public float Amount { get; init; }
}

public sealed class DiveTopItem
{
	public string Name { get; init; }
	public float Value { get; init; }
	public CollectibleRarity Rarity { get; init; }
}

public sealed class DiveSummary
{
	public bool Success { get; init; }
	public DiveFailureReason FailureReason { get; init; }
	public int DiveNumber { get; init; }
	public float MaxDepthMeters { get; init; }
	public float DurationSeconds { get; init; }
	public int ItemCount { get; init; }
	public float HaulValue { get; init; }
	public float BonusValue { get; init; }
	public float MoneyEarned { get; init; }
	public bool BrokeDepthRecord { get; init; }
	public bool FullHaulBonus { get; init; }
	public bool LowDamageBonus { get; init; }
	public bool OxygenRemainingBonus { get; init; }
	public float OxygenUsedFraction { get; init; }
	public float DamageTakenFraction { get; init; }
	public float OxygenRemainingFraction { get; init; }
	public string Headline { get; set; } = "";
	public bool SaleCommitted { get; set; }

	public List<DiveTopItem> TopItems { get; init; } = new();
	public List<DiveBonusLine> Bonuses { get; init; } = new();

	public int ArtifactsFound { get; init; }
	public int ArtifactsTotal { get; init; }
	public int LocationsFound { get; init; }
	public int LocationsTotal { get; init; }
	public int CreaturesFound { get; init; }
	public int CreaturesTotal { get; init; }

	public int ProgressionLevel { get; init; }
	public int ProgressionXp { get; init; }
	public int ProgressionXpMax { get; init; }
	public string NextUnlock { get; init; } = "Deep-Sea Sub";
}
