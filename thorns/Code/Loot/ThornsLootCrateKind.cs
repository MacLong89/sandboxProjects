namespace Sandbox;

/// <summary>Determines crate visuals + rarity bias + loot pools.</summary>
public enum ThornsLootCrateKind : byte
{
	Medical = 0,
	Weapons = 1,
	Armor = 2,
	Provisions = 3,
	MilitaryMixed = 4,
	IndustrialScrap = 5,

	/// <summary>Dynamic world events (airdrop / convoy) — rarity-biased grid in <see cref="ThornsLootGenerator"/>.</summary>
	AirdropPremium = 6,

	/// <summary>Ore, cloth, bone, scrap metal — slow crafting grind pieces.</summary>
	SalvageComponents = 7,

	/// <summary>Raw meat, hides, bones — animal harvest bundle.</summary>
	HunterCache = 8,

	/// <summary>Pistol / rifle / SMG / shotgun / sniper ammo stacks only.</summary>
	Ammo = 9
}
