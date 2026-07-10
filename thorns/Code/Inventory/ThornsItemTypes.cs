namespace Sandbox;

/// <summary>THORNS_EVERYTHING_DOCUMENT — item categories for stacking, crafting, equip validation.</summary>
public enum ThornsItemType
{
	Resource,
	Weapon,
	/// <summary>Harvest tools (axe / pickaxe) — hotbar equip, no combat weapon instance.</summary>
	Tool,
	Ammo,
	Consumable,
	/// <summary>Equippable helmet/chest/pants — separate from grid hotbar (THORNS_EVERYTHING_DOCUMENT §3).</summary>
	Armor,
	Misc
}

/// <summary>Which resource nodes a <see cref="ThornsItemType.Tool"/> can strike (must match equipped hotbar).</summary>
public enum ThornsHarvestToolKind
{
	None,
	Axe,
	Pickaxe,
	/// <summary>Trees and stone only — no fiber/metal (see <see cref="ThornsItemRegistry.HarvestToolMatchesResourceKind"/>).</summary>
	Primitive
}
