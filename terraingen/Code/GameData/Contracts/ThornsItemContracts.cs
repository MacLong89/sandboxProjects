namespace Terraingen.GameData;

public enum ThornsItemCategory : byte
{
	Unknown,
	Resource,
	Tool,
	Weapon,
	Consumable,
	Ammo,
	Armor,
	Attachment,
	Blueprint
}

public enum ThornsEquipSlot : byte
{
	None,
	Head,
	Chest,
	Legs,
	Hotbar,
	Inventory
}

public enum ThornsHarvestToolKind : byte
{
	None,
	Axe,
	Pickaxe,
	Primitive
}

/// <summary>Authoritative item definition. Loaded from data files; host validates all requests.</summary>
public sealed partial class ThornsItemDefinition
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Description { get; set; } = "";
	public ThornsItemCategory Category { get; set; }
	public ThornsEquipSlot EquipSlot { get; set; }
	public int MaxStack { get; set; } = 99;
	public string IconPath { get; set; } = "";
	public float WeightKg { get; set; }
	public bool CanSplit { get; set; } = true;

	public string CombatWeaponDefinitionId { get; set; } = "";
	public string ViewModelAsset { get; set; } = "";
	public string WorldModelAsset { get; set; } = "";
	public ThornsHarvestToolKind HarvestToolKind { get; set; }
	public Vector3 FpViewmodelRootLocalOffset { get; set; }
	public Vector3 FpViewmodelRootLocalScale { get; set; }
	public Vector3 FpViewmodelRootLocalEulerDegrees { get; set; }
}

/// <summary>Runtime stack in a container slot (weapons/tools carry durability + loaded ammo).</summary>
public struct ThornsItemStack
{
	public string ItemId;
	public int Count;
	public bool HasDurability;
	public float Durability;
	public string WeaponInstanceId;
	public int WeaponLoadedAmmo;
	public string AttachmentId0;
	public string AttachmentId1;
	public string AttachmentId2;
	public int ItemTier;
	public float StatRoll;

	public bool IsEmpty => string.IsNullOrEmpty( ItemId ) || Count <= 0;

	public static readonly ThornsItemStack EmptyStack = default;

	public bool IsWeaponBroken( string combatWeaponDefinitionId )
	{
		if ( !HasDurability || string.IsNullOrWhiteSpace( combatWeaponDefinitionId ) )
			return false;

		var max = Terraingen.Combat.ThornsWeaponDefinitions.Get( combatWeaponDefinitionId ).MaxDurability;
		return max > 0.001f && Durability <= 0.001f;
	}
}

/// <summary>UI / client mirror fields for a single inventory cell.</summary>
public sealed class ThornsInventorySlotMirrorDto
{
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
	public bool HasDurability { get; set; }
	public float Durability { get; set; }
	public int WeaponLoadedAmmo { get; set; }
	public int WeaponClipSize { get; set; }
	public int AmmoReserve { get; set; }
	public int WeaponTier { get; set; }
	public int ItemTier { get; set; }
	public float StatRoll { get; set; }
	public bool WeaponBroken { get; set; }
	public bool Reloading { get; set; }
	public List<string> WeaponAttachmentIds { get; set; } = new();
}

public sealed class ThornsInventorySlotDto
{
	public int Index { get; set; }
	public string ItemId { get; set; } = "";
	public int Count { get; set; }
	public ThornsContainerKind Container { get; set; }
	public bool HasDurability { get; set; }
	public float Durability { get; set; }
	public int WeaponLoadedAmmo { get; set; }
	public int WeaponClipSize { get; set; }
	public int AmmoReserve { get; set; }
	public int WeaponTier { get; set; }
	public int ItemTier { get; set; }
	public float StatRoll { get; set; }
	public bool WeaponBroken { get; set; }
	public List<string> WeaponAttachmentIds { get; set; } = new();
}

public enum ThornsContainerKind : byte
{
	Inventory,
	Hotbar,
	Head,
	Chest,
	Legs,
	WorldLoot,
	CampfireStation,
	WorkbenchStation
}

public sealed class ThornsInventorySnapshotDto
{
	public List<ThornsInventorySlotDto> Slots { get; set; } = new();
	public int ActiveHotbarIndex { get; set; }
	public bool CraftPanelExpanded { get; set; } = true;
	public string ActiveCraftCategoryId { get; set; } = ThornsCraftCatalog.AllCraftCategoryId;
	public string SelectedRecipeId { get; set; } = "";
}
