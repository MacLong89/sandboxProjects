namespace Terraingen.Multiplayer;

using Terraingen.GameData;

public sealed class ThornsPersistentWorldDto
{
	public int Version { get; set; } = ThornsSaveFormat.CurrentVersion;
	public string SavedUtcIso { get; set; } = "";
	public Dictionary<string, ThornsPersistentPlayerDto> PlayersByAccountKey { get; set; } = new();
	public Dictionary<string, ThornsPersistentPlayerProgressDto> PlayerProgressByAccountKey { get; set; } = new();
	public List<ThornsPersistentTameDto> Tames { get; set; } = new();
	public List<ThornsPersistentGuildDto> Guilds { get; set; } = new();
	public Dictionary<string, string> AccountGuildIds { get; set; } = new();
	public List<ThornsPersistentStructureDto> Structures { get; set; } = new();
	public List<int> DepletedTreeIds { get; set; } = new();
	public List<int> DepletedMineralNodeIds { get; set; } = new();
	public List<int> LootedFurnitureIds { get; set; } = new();
	public List<ThornsPersistentFurnitureContainerDto> FurnitureContainers { get; set; } = new();
	public List<ThornsPersistentStructureStorageEntryDto> StructureStorages { get; set; } = new();
	public Dictionary<string, ThornsPersistentPlayerMapDto> PlayerMapsByAccountKey { get; set; } = new();
	public ThornsVictoryPersistentStateDto VictoryState { get; set; } = new();
	public ThornsPersistentNpcGuildDto NpcGuild { get; set; } = new();
	public List<ThornsPersistentNpcGuildDto> NpcGuilds { get; set; } = new();
	public List<ThornsPersistentDeathCrateDto> DeathCrates { get; set; } = new();
}

/// <summary>Persisted death/loot crate (save v13+).</summary>
public sealed class ThornsPersistentDeathCrateDto
{
	public int Id { get; set; }
	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }
	public string Title { get; set; } = "Death Crate";
	public bool EnemyLootTint { get; set; }
	public float LifetimeSeconds { get; set; }
	public float RemainingLifetimeSeconds { get; set; }
	public List<ThornsPersistentItemStackDto> Slots { get; set; } = new();
}

public sealed class ThornsPersistentNpcGuildDto
{
	public string GuildId { get; set; } = "npc_iron_wolves";
	public bool IsEliminated { get; set; }
	public bool HasDominionVictory { get; set; }
	public float ExpansionAccumulatorSeconds { get; set; }
	public int NextOutpostSeed { get; set; } = 1;
	public List<ThornsPersistentNpcGuildOutpostDto> Outposts { get; set; } = new();
}

public sealed class ThornsPersistentNpcGuildOutpostDto
{
	public string OutpostId { get; set; } = "";
	public bool IsHeadquarters { get; set; }
	public int OutpostSeed { get; set; }
	public int BuildingIndexOffset { get; set; }
	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }
	public float RYaw { get; set; }
}

public sealed class ThornsPersistentStructureStorageEntryDto
{
	public string InstanceKey { get; set; } = "";
	public List<ThornsPersistentItemStackDto> Slots { get; set; } = new();
}

public sealed class ThornsPersistentItemStackDto
{
	public int SlotIndex { get; set; }
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
	public int ItemTier { get; set; }
	public float StatRoll { get; set; }
	public float Durability { get; set; }
	public bool HasDurability { get; set; }
}

public sealed class ThornsPersistentFurnitureContainerDto
{
	public int FurnitureId { get; set; }
	public string LootTable { get; set; } = "";
	public int LootSeed { get; set; }
	public bool HasRolledLoot { get; set; }
	public double EmptySinceUtc { get; set; } = -1;
	public List<ThornsPersistentItemStackDto> Slots { get; set; } = new();
}

public sealed class ThornsPersistentStructureStorageDto
{
	public List<ThornsPersistentItemStackDto> Slots { get; set; } = new();
}

public sealed class ThornsPersistentPlayerMapDto
{
	public List<ThornsPersistentWaypointDto> Waypoints { get; set; } = new();
	public bool HasLastDeath { get; set; }
	public float LastDeathWorldX { get; set; }
	public float LastDeathWorldY { get; set; }
}

public sealed class ThornsPersistentWaypointDto
{
	public string Id { get; set; } = "waypoint";
	public string Kind { get; set; } = "";
	public float WorldX { get; set; }
	public float WorldY { get; set; }
	public string Label { get; set; } = "Waypoint";
}

/// <summary>Inventory, skills, journal, and vitals for one account (save v4+).</summary>
public sealed class ThornsPersistentPlayerProgressDto
{
	public string InventoryJson { get; set; } = "";
	public string CraftJson { get; set; } = "";
	public string JournalJson { get; set; } = "";
	public string SkillsJson { get; set; } = "";
	public string VitalsJson { get; set; } = "";
	public string ResearchJson { get; set; } = "";
	// Legacy mirror of SkillsJson — kept for save v4 compatibility; capture derives from SkillsJson.
	public int TotalXp { get; set; }
	public int PlayerLevel { get; set; } = 1;
	public int ActiveHotbarIndex { get; set; }
	public string CraftCategory { get; set; } = "tools";
	public string SelectedRecipeId { get; set; } = "recipe_stone_pickaxe";
	public bool CraftPanelExpanded { get; set; } = true;
	public string ContractsJson { get; set; } = "";
}

public sealed class ThornsPersistentStructureDto
{
	public string InstanceKey { get; set; } = "";
	public string StructureId { get; set; } = "";
	public string OwnerAccountKey { get; set; } = "";
	public int MaterialTier { get; set; }
	public float CurrentHealth { get; set; }
	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }
	public float RPitch { get; set; }
	public float RYaw { get; set; }
	public float RRoll { get; set; }
	/// <summary><see cref="ThornsPlayerDoor"/> on <c>wood_doorframe</c> — open when true.</summary>
	public bool DoorOpen { get; set; }
}

public sealed class ThornsPersistentGuildDto
{
	public string GuildId { get; set; } = "";
	public string GuildName { get; set; } = "";
	public int GuildLevel { get; set; } = 1;
	public float GuildXp { get; set; }
	public string Motto { get; set; } = "";
	public bool IsNpcGuild { get; set; }
	public bool IsEliminated { get; set; }
	public bool HasDominionVictory { get; set; }
	public int NpcOutpostCount { get; set; }
	public int NpcOutpostTarget { get; set; } = 10;
	public string Announcement { get; set; } = "";
	public string AnnouncementAuthor { get; set; } = "";
	public string AnnouncementTimestampUtc { get; set; } = "";
	public List<ThornsGuildMemberDto> Members { get; set; } = new();
	public List<ThornsGuildActivityDto> Activity { get; set; } = new();
}

public sealed class ThornsPersistentPlayerDto
{
	public string DisplayName { get; set; } = "";
	public string LastSeenUtcIso { get; set; } = "";

	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }

	public float RPitch { get; set; }
	public float RYaw { get; set; }
	public float RRoll { get; set; }

	public bool HasBedSpawn { get; set; }
	public float BedSpawnX { get; set; }
	public float BedSpawnY { get; set; }
	public float BedSpawnZ { get; set; }
	public float BedSpawnYaw { get; set; }
}

public sealed class ThornsPersistentTameDto
{
	public string OwnerAccountKey { get; set; } = "";
	public ushort SpeciesId { get; set; }
	public string DisplayName { get; set; } = "";
	public float CurrentHealth { get; set; }
	public float MaxHealth { get; set; }
	public float Attack { get; set; }
	public float MoveSpeed { get; set; }
	public float DetectionRange { get; set; }
	public int BreedTier { get; set; }
	public int TameLevel { get; set; } = 1;
	public int TameExperience { get; set; }
	public int UnspentStatPoints { get; set; }
	public int StatStrength { get; set; }
	public int StatDefense { get; set; }
	public int StatStamina { get; set; }
	public int StatAgility { get; set; }
	public int StatIntelligence { get; set; }
	public bool IsCrossbreed { get; set; }
	public bool IsMutated { get; set; }
	public string GeneticSpeciesIdsCsv { get; set; } = "";
	public string GeneticTraitIdsCsv { get; set; } = "";
	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }
	public float RYaw { get; set; }
	public long BreedCooldownUntilUtcTicks { get; set; }
}
