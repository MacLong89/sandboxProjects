namespace Terraingen.GameData;

public sealed class ThornsResearchIngredientDto
{
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
}

public sealed class ThornsResearchLevelDefinition
{
	public int Level { get; set; }
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public float ResearchSeconds { get; set; }
	public List<ThornsResearchIngredientDto> Costs { get; set; } = new();
	public string RewardItemId { get; set; } = "";
	public int RewardCount { get; set; }
}

public sealed class ThornsResearchLevelDto
{
	public int Level { get; set; }
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public float ResearchSeconds { get; set; }
	public bool Completed { get; set; }
	public bool Active { get; set; }
	public bool Available { get; set; }
	public float SecondsRemaining { get; set; }
	public List<ThornsResearchIngredientDto> Costs { get; set; } = new();
	public string RewardItemId { get; set; } = "";
	public int RewardCount { get; set; }
}

public sealed class ThornsResearchSnapshotDto
{
	public bool IsOpen { get; set; }
	public string StationInstanceKey { get; set; } = "";
	public int CompletedLevel { get; set; }
	public int ActiveLevel { get; set; }
	public float ActiveSecondsRemaining { get; set; }
	public float PercentComplete { get; set; }
	public List<ThornsResearchLevelDto> Levels { get; set; } = new();
}
