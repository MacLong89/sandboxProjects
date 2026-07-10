namespace Terraingen.GameData;

public enum ThornsCraftStationKind : byte
{
	Hand,
	Workbench,
	Campfire,
	Forge,
	Special
}

public sealed class ThornsRecipeIngredient
{
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
}

/// <summary>Recipe definition. Host validates materials and station proximity.</summary>
public sealed class ThornsRecipeDefinition
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Description { get; set; } = "";
	public string CategoryId { get; set; } = "";
	public string OutputItemId { get; set; } = "";
	public int OutputCount { get; set; } = 1;
	public float CraftSeconds { get; set; } = 5f;
	public int RequiredCraftTier { get; set; } = 1;
	public ThornsCraftStationKind Station { get; set; } = ThornsCraftStationKind.Hand;
	public List<ThornsRecipeIngredient> Ingredients { get; set; } = new();
	public string IconPath { get; set; } = "";
}

public sealed class ThornsCraftQueueEntryDto
{
	public string EntryId { get; set; } = "";
	public string RecipeId { get; set; } = "";
	public int QuantityRemaining { get; set; }
	public float SecondsRemaining { get; set; }
	public string OutputItemId { get; set; } = "";
}

public sealed class ThornsCraftSnapshotDto
{
	public List<ThornsCraftQueueEntryDto> Queue { get; set; } = new();
	public ThornsCraftStationKind NearestStation { get; set; }
	public bool HasWorkbench { get; set; }
	public bool HasCampfire { get; set; }
	public bool HasForge { get; set; }
}

public sealed class ThornsUpgradeItemRequest
{
	public ThornsContainerKind Container { get; set; }
	public int Index { get; set; }
}
