namespace Terraingen.GameData;

using Terraingen.Progression;

/// <summary>Full UI state pushed to owning client on connect and after major resets.</summary>
public sealed class ThornsPlayerSnapshotBundle
{
	public ThornsInventorySnapshotDto Inventory { get; set; } = new();
	public ThornsCraftSnapshotDto Craft { get; set; } = new();
	public ThornsJournalSnapshotDto Journal { get; set; } = new();
	public ThornsSkillsSnapshotDto Skills { get; set; } = new();
	public ThornsTamesSnapshotDto Tames { get; set; } = new();
	public ThornsGuildSnapshotDto Guild { get; set; } = new();
	public ThornsMapSnapshotDto Map { get; set; } = new();
	public ThornsVitalsSnapshotDto Vitals { get; set; } = new();
	public ThornsExternalContainerSnapshotDto ExternalContainer { get; set; } = new();
	public ThornsRadioShopSnapshotDto RadioShop { get; set; } = new();
	public ThornsResearchSnapshotDto Research { get; set; } = new();
	public ThornsCampfireSnapshotDto Campfire { get; set; } = new();
	public ThornsWorkbenchSnapshotDto Workbench { get; set; } = new();
	public ThornsVictorySnapshot Victory { get; set; } = new();
	public ThornsSurvivorContractsSnapshotDto Contracts { get; set; } = new();
}

/// <summary>Owner-only snapshot for an open world loot / storage container.</summary>
public sealed class ThornsExternalContainerSnapshotDto
{
	public bool IsOpen { get; set; }
	public string ContainerKey { get; set; } = "";
	public string Title { get; set; } = "Container";
	public int SlotCount { get; set; }
	public float RefillSecondsRemaining { get; set; }
	public List<ThornsInventorySlotDto> Slots { get; set; } = new();
}

public sealed class ThornsVitalsSnapshotDto
{
	public float Health { get; set; }
	public float MaxHealth { get; set; } = 100f;
	public float Stamina { get; set; } = 100f;
	public float MaxStamina { get; set; } = 100f;
	public float Food { get; set; } = 100f;
	public float MaxFood { get; set; } = 100f;
	public float Water { get; set; } = 100f;
	public float MaxWater { get; set; } = 100f;
	public float TemperatureC { get; set; } = 18f;
	public bool ShowHealth { get; set; }
	public bool ShowStamina { get; set; }
	public bool ShowFood { get; set; }
	public bool ShowWater { get; set; }
	public bool ShowTemperature { get; set; }
	public bool HasCampfireWarmth { get; set; }
}
