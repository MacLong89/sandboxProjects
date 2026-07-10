using System;

namespace Sandbox;

/// <summary>Root JSON blob written by <see cref="ThornsWorldPersistence"/> (host-only).</summary>
public sealed class ThornsPersistentWorldDto
{
	public int Version { get; set; } = 1;

	public string SavedUtcIso { get; set; }

	/// <summary>Deterministic procedural layout (<see cref="ThornsTerrainNetSpec.Seed"/>); absent on saves written before terrain seed persistence.</summary>
	public int? WorldGenerationSeed { get; set; }

	public List<ThornsPersistentStructureDto> Structures { get; set; } = new();

	public List<ThornsPersistentWildlifeDto> Wildlife { get; set; } = new();

	public Dictionary<string, ThornsPersistentPlayerDto> PlayersByAccountKey { get; set; } = new();
}

public sealed class ThornsPersistentStructureDto
{
	public Guid InstanceId { get; set; }

	public string OwnerAccountKey { get; set; }

	public string StructureDefId { get; set; }

	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }

	public float RPitch { get; set; }
	public float RYaw { get; set; }
	public float RRoll { get; set; }

	public float CurrentHealth { get; set; }

	public int UpgradeTier { get; set; }

	/// <summary>Storage chest grid — reference-type rows so world JSON round-trips reliably (struct[] does not).</summary>
	public ThornsPersistInventorySlotDto[] ChestSlots { get; set; }

	/// <summary><see cref="ThornsCampfire"/> fuel / raw / output grid.</summary>
	public ThornsPersistInventorySlotDto[] CampfireSlots { get; set; }

	/// <summary><see cref="ThornsWorkbench"/> repair grid.</summary>
	public ThornsPersistInventorySlotDto[] WorkbenchSlots { get; set; }

	/// <summary><see cref="ThornsPlayerDoor"/> on <c>wood_doorframe</c> — open when true.</summary>
	public bool DoorOpen { get; set; }
}

public sealed class ThornsPersistentWildlifeDto
{
	public Guid WildlifeId { get; set; }

	public string Species { get; set; }

	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }

	public float RPitch { get; set; }
	public float RYaw { get; set; }
	public float RRoll { get; set; }

	public float CurrentHealth { get; set; }

	public string TameOwnerAccountKey { get; set; }

	public bool TameFollowOwner { get; set; }

	public string TameDisplayName { get; set; }

	/// <summary><see cref="ThornsWildlifeIdentity.TameTotalXp"/>; save v2 field (defaults to 0).</summary>
	public int TameTotalXp { get; set; }

	public int TameUnspentUpgradePoints { get; set; }

	public int TameHpUpgradeSteps { get; set; }

	public int TameDmgUpgradeSteps { get; set; }

	public int TameSpdUpgradeSteps { get; set; }

	/// <summary><see cref="ThornsLootRarity"/> ordinal — rolled once at tame.</summary>
	public byte TameQualityTier { get; set; }

	public float TameAffinityHp { get; set; }

	public float TameAffinityDmg { get; set; }

	public float TameAffinitySpd { get; set; }

	public byte TameLegendaryAbility { get; set; }
}

/// <summary>
/// Reference-type inventory rows for disk JSON (<see cref="ThornsInventorySlotNet"/> is a struct — some serializers drop or fail struct[] round-trips).
/// </summary>
public sealed class ThornsPersistInventorySlotDto
{
	public string ItemId { get; set; }
	public int Quantity { get; set; }
	public int HasDurability { get; set; }
	public float Durability { get; set; }
	public string WeaponInstanceId { get; set; }
	public int WeaponLoadedAmmo { get; set; }
	public string WeaponRollPayload { get; set; }
	public string ArmorRollPayload { get; set; }

	public static ThornsInventorySlotNet ToSlotNet( ThornsPersistInventorySlotDto row )
	{
		if ( row is null )
			return default;

		return new ThornsInventorySlotNet
		{
			ItemId = row.ItemId ?? "",
			Quantity = row.Quantity,
			HasDurability = row.HasDurability,
			Durability = row.Durability,
			WeaponInstanceId = row.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = row.WeaponLoadedAmmo,
			WeaponRollPayload = row.WeaponRollPayload ?? "",
			ArmorRollPayload = row.ArmorRollPayload ?? ""
		};
	}

	public static ThornsPersistInventorySlotDto FromSlotNet( ThornsInventorySlotNet row ) =>
		new ThornsPersistInventorySlotDto
		{
			ItemId = row.ItemId ?? "",
			Quantity = row.Quantity,
			HasDurability = row.HasDurability,
			Durability = row.Durability,
			WeaponInstanceId = row.WeaponInstanceId ?? "",
			WeaponLoadedAmmo = row.WeaponLoadedAmmo,
			WeaponRollPayload = row.WeaponRollPayload ?? "",
			ArmorRollPayload = row.ArmorRollPayload ?? ""
		};

	public static ThornsInventorySlotNet[] ToSlotNetArray( ThornsPersistInventorySlotDto[] rows )
	{
		var total = ThornsInventory.TotalSlots;
		var dst = new ThornsInventorySlotNet[total];
		if ( rows is null )
			return dst;

		for ( var i = 0; i < total; i++ )
			dst[i] = i < rows.Length ? ToSlotNet( rows[i] ) : default;

		return dst;
	}

	public static ThornsPersistInventorySlotDto[] FromSlotNetArray( ThornsInventorySlotNet[] rows )
	{
		if ( rows is null || rows.Length == 0 )
			return Array.Empty<ThornsPersistInventorySlotDto>();

		var dst = new ThornsPersistInventorySlotDto[rows.Length];
		for ( var i = 0; i < rows.Length; i++ )
			dst[i] = FromSlotNet( rows[i] );

		return dst;
	}
}

public sealed class ThornsPersistentPlayerDto
{
	public float Px { get; set; }
	public float Py { get; set; }
	public float Pz { get; set; }

	public float RPitch { get; set; }
	public float RYaw { get; set; }
	public float RRoll { get; set; }

	public float HealthCurrent { get; set; }
	public float HealthMax { get; set; }
	public bool HealthIsDeadState { get; set; }

	public int UnspentUpgradePoints { get; set; }

	/// <summary>Legacy pre–skill-tree migration — merged into new ranks on load.</summary>
	public int MiningRank { get; set; }
	public int WoodcuttingRank { get; set; }
	public int HungerMaxRank { get; set; }
	public int ThirstMaxRank { get; set; }
	public int StaminaMaxRank { get; set; }
	public int TamingThresholdRank { get; set; }
	public int CraftingTierRank { get; set; }

	public int HydrationRank { get; set; }
	public int IronGutRank { get; set; }
	public int StrongStomachRank { get; set; }
	public int WeatheredRank { get; set; }
	public int ThickHideRank { get; set; }

	public int EnduranceRank { get; set; }
	public int GhostRank { get; set; }
	public int BeastmasterRank { get; set; }
	public int HardenedRank { get; set; }
	public int LuckyChamberRank { get; set; }

	public int LumberjackRank { get; set; }
	public int MinerRank { get; set; }
	public int ScavengerRank { get; set; }
	public int ReinforcedRank { get; set; }
	public int TechnicianRank { get; set; }

	public float Hunger { get; set; }
	public float Thirst { get; set; }
	public float Stamina { get; set; }
	public float PoisonLevel { get; set; }
	public int TotalXp { get; set; }
	public bool ServerSprinting { get; set; }
	public bool ServerCrouching { get; set; }

	public int WalletGold { get; set; }

	public int WalletMetal { get; set; }

	/// <summary>Legacy: nested array in the world JSON (some FileSystem.Data serializers still drop or mangle this).</summary>
	public ThornsPersistInventorySlotDto[] InventorySlots { get; set; }

	/// <summary>Preferred: inner JSON array string — survives host file round-trip reliably.</summary>
	public string InventorySlotsBlob { get; set; }

	public ThornsPersistentArmorPieceDto Helmet { get; set; }
	public ThornsPersistentArmorPieceDto Chest { get; set; }
	public ThornsPersistentArmorPieceDto Pants { get; set; }

	public int SelectedHotbarIndex { get; set; } = -1;

	/// <summary>Milestone chain (host-authored); added in save v2. Prefer <see cref="MilestoneProgressPacked"/> when present.</summary>
	public int ActiveMilestoneIndex { get; set; }

	public int MilestoneActiveProgress { get; set; }

	/// <summary>Parallel journal progress (<see cref="ThornsMilestoneProgressCodec"/>); persisted since save format extension (additive JSON).</summary>
	public string MilestoneProgressPacked { get; set; }

	/// <summary><see cref="ThornsCharacterProgression"/>; added in save v2.</summary>
	public int CharacterLevel { get; set; } = 1;

	public float XpProgressInCurrentLevel { get; set; }

	/// <summary>Most recent owned <c>bed</c> for death respawn — empty when unset.</summary>
	public string BedInstanceId { get; set; } = "";

	public float BedPx { get; set; }
	public float BedPy { get; set; }
	public float BedPz { get; set; }
	public float BedRPitch { get; set; }
	public float BedRYaw { get; set; }
	public float BedRRoll { get; set; }
	public long BedPlacementSequence { get; set; }

	/// <summary>Newline-separated stable account keys (<see cref="ThornsGuildRoster"/>).</summary>
	public string GuildMemberKeysPacked { get; set; } = "";

	/// <summary>Legacy save field from before guild rename — read on load only.</summary>
	public string ClanMemberKeysPacked { get; set; } = "";

	public string ResolveGuildMemberKeysPacked() =>
		!string.IsNullOrEmpty( GuildMemberKeysPacked ) ? GuildMemberKeysPacked : (ClanMemberKeysPacked ?? "");

	/// <summary>Moves ranks from legacy columns into the skill-tree fields (safe on every load).</summary>
	public static void MergeLegacySkillRankMigration( ThornsPersistentPlayerDto dto )
	{
		if ( dto is null )
			return;

		dto.MinerRank = Math.Max( dto.MinerRank, dto.MiningRank );
		dto.LumberjackRank = Math.Max( dto.LumberjackRank, dto.WoodcuttingRank );
		dto.IronGutRank = Math.Max( dto.IronGutRank, dto.HungerMaxRank );
		dto.HydrationRank = Math.Max( dto.HydrationRank, dto.ThirstMaxRank );
		dto.EnduranceRank = Math.Max( dto.EnduranceRank, dto.StaminaMaxRank );
		dto.BeastmasterRank = Math.Max( dto.BeastmasterRank, dto.TamingThresholdRank );
		dto.TechnicianRank = Math.Max( dto.TechnicianRank, dto.CraftingTierRank );
	}
}

public sealed class ThornsPersistentArmorPieceDto
{
	public string ItemId { get; set; }
	public float DurabilityRemaining { get; set; }
	public string ArmorRollPayload { get; set; }
}
