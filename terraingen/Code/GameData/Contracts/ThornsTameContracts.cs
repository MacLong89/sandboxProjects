namespace Terraingen.GameData;

public enum ThornsTameCommand : byte
{
	Follow,
	Stay,
	Guard,
	Passive,
	Attack,
	Summon
}

public sealed class ThornsTameTraitDto
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public string IconPath { get; set; } = "";
}

public sealed class ThornsTameListEntryDto
{
	public Guid EntityId { get; set; }
	public ushort SpeciesId { get; set; }
	public string SpeciesKey { get; set; } = "";
	public string SpeciesName { get; set; } = "";
	public int Tier { get; set; } = 1;
	public bool IsCrossbreed { get; set; }
	public bool IsMutated { get; set; }
	public bool IsSuperCrossbreed { get; set; }
	public List<ushort> GeneticSpeciesIds { get; set; } = new();
	public List<string> GeneticSpeciesNames { get; set; } = new();
	public string DisplayName { get; set; } = "";
	public string PortraitPath { get; set; } = "";
	public string ModelPath { get; set; } = "";
	public string AnimPrefix { get; set; } = "";
	public int Level { get; set; } = 1;
	public int CurrentExperience { get; set; }
	public int ExperienceToNextLevel { get; set; } = 300;
	public int UnspentStatPoints { get; set; }
	public int StatStrength { get; set; }
	public int StatDefense { get; set; }
	public int StatStamina { get; set; }
	public int StatAgility { get; set; }
	public int StatIntelligence { get; set; }
	public float CurrentHealth { get; set; }
	public float MaxHealth { get; set; }
	public float Attack { get; set; }
	public int SpeedPercent { get; set; }
	public int Perception { get; set; }
	public List<ThornsTameTraitDto> Traits { get; set; } = new();
	public ThornsTameCommand ActiveCommand { get; set; } = ThornsTameCommand.Follow;
	public long BreedCooldownUntilUtcTicks { get; set; }
}

public sealed class ThornsTamesSnapshotDto
{
	public Guid SelectedEntityId { get; set; }
	public bool BreedPanelOpen { get; set; }
	public Guid BreedParentAId { get; set; }
	public Guid BreedParentBId { get; set; }
	public List<ThornsTameListEntryDto> Tames { get; set; } = new();
}
